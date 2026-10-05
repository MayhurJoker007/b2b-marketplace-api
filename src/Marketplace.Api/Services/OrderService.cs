using System.Globalization;
using System.Text.Json;
using Marketplace.Api.Contracts;
using Marketplace.Api.Data;
using Marketplace.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Api.Services;

public class OrderRejectedException(IReadOnlyList<string> problems)
    : Exception(string.Join("; ", problems))
{
    public IReadOnlyList<string> Problems { get; } = problems;
}

/// <summary>
/// Every order rule is checked here, on the server. The storefront does its own checks for a nicer
/// experience, but it can't be trusted: an old app version or a direct API call would skip them.
/// </summary>
public class OrderService(MarketplaceDb db, TimeProvider clock)
{
    public async Task<Order> PlaceAsync(int outletId, CreateOrderRequest request, CancellationToken ct = default)
    {
        var problems = new List<string>();

        var distributor = await db.Distributors.FirstOrDefaultAsync(d => d.Id == request.DistributorId, ct);
        if (distributor is null || !distributor.IsActive)
            throw new OrderRejectedException(["Distributor is not available"]);

        var shipping = await db.ShippingMethods
            .FirstOrDefaultAsync(s => s.Id == request.ShippingMethodId && s.DistributorId == distributor.Id, ct);
        if (shipping is null)
            problems.Add("Shipping method does not belong to this distributor");

        if (request.Lines is not { Count: > 0 })
            throw new OrderRejectedException(["Order has no lines"]);

        var duplicates = request.Lines.GroupBy(l => l.Sku).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicates.Count > 0)
            problems.Add($"SKU listed more than once: {string.Join(", ", duplicates)}");

        var skus = request.Lines.Select(l => l.Sku).Distinct().ToList();
        var products = await db.Products
            .Where(p => p.DistributorId == distributor.Id && skus.Contains(p.Sku))
            .ToDictionaryAsync(p => p.Sku, ct);

        var order = new Order
        {
            DistributorId = distributor.Id,
            OutletId = outletId,
            ShippingMethodId = request.ShippingMethodId,
            CreatedUtc = clock.GetUtcNow().UtcDateTime,
        };

        foreach (var line in request.Lines)
        {
            if (!products.TryGetValue(line.Sku, out var product) || !product.IsListed)
            {
                problems.Add($"{line.Sku}: not sold by {distributor.Name}");
                continue;
            }
            if (line.Quantity < product.MinimumOrderQuantity)
                problems.Add($"{line.Sku}: minimum order is {product.MinimumOrderQuantity}");
            if (line.Quantity % product.CaseSize != 0)
                problems.Add($"{line.Sku}: must be ordered in multiples of {product.CaseSize}");
            if (line.Quantity > product.StockOnHand)
                problems.Add($"{line.Sku}: only {product.StockOnHand} in stock");

            order.Lines.Add(new OrderLine
            {
                ProductId = product.Id,
                Product = product,
                Quantity = line.Quantity,
                UnitPrice = product.Price,
                LineTotal = product.Price * line.Quantity,
            });
        }

        order.Subtotal = order.Lines.Sum(l => l.LineTotal);
        if (order.Lines.Count > 0 && order.Subtotal < distributor.MinimumOrderValue)
            problems.Add(string.Create(CultureInfo.InvariantCulture,
                $"Minimum order value is {distributor.MinimumOrderValue:0.00} {distributor.Currency}"));

        if (problems.Count > 0)
            throw new OrderRejectedException(problems);

        order.ShippingFee = shipping!.FreeAbove is { } freeAbove && order.Subtotal >= freeAbove ? 0m : shipping.Fee;
        order.Total = order.Subtotal + order.ShippingFee;
        order.ExpectedDelivery = DeliveryCalendar.ExpectedDelivery(LocalNow(), shipping.LeadTimeDays);

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // Reserve stock with a conditional update so two buyers can't both take the last cases.
        foreach (var line in order.Lines)
        {
            var reserved = await db.Products
                .Where(p => p.Id == line.ProductId && p.StockOnHand >= line.Quantity)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.StockOnHand, p => p.StockOnHand - line.Quantity), ct);
            if (reserved == 0)
                throw new OrderRejectedException([$"{line.Product!.Sku}: stock changed while ordering, please retry"]);
        }

        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);
        db.Outbox.Add(new OutboxMessage
        {
            Type = "order.submitted",
            CreatedUtc = order.CreatedUtc,
            Payload = JsonSerializer.Serialize(new
            {
                orderId = order.Id,
                erp = distributor.Erp.ToString(),
                distributorId = distributor.Id,
                outletId,
                expectedDelivery = order.ExpectedDelivery,
                lines = order.Lines.Select(l => new { sku = l.Product!.Sku, l.Quantity, l.UnitPrice }),
            }),
        });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return order;
    }

    public async Task<Order?> CancelAsync(int orderId, int outletId, CancellationToken ct = default)
    {
        var order = await db.Orders.Include(o => o.Lines).ThenInclude(l => l.Product)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.OutletId == outletId, ct);
        if (order is null) return null;

        if (order.Status is not OrderStatus.Submitted)
            throw new OrderRejectedException([$"Order is {order.Status} and can no longer be cancelled"]);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        order.Status = OrderStatus.Cancelled;
        await db.SaveChangesAsync(ct);
        foreach (var line in order.Lines)
        {
            await db.Products.Where(p => p.Id == line.ProductId)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.StockOnHand, p => p.StockOnHand + line.Quantity), ct);
        }
        await tx.CommitAsync(ct);
        return order;
    }

    private DateTime LocalNow()
    {
        var sast = TimeZoneInfo.CreateCustomTimeZone("SAST", TimeSpan.FromHours(2), "SAST", "SAST");
        return TimeZoneInfo.ConvertTime(clock.GetUtcNow(), sast).DateTime;
    }
}

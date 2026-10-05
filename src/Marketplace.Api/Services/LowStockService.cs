using Marketplace.Api.Data;
using Marketplace.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Api.Services;

public record LowStockItem(string Sku, string Name, int StockOnHand, decimal AverageDailySales, decimal DaysOfCover, int SuggestedReorder);

/// <summary>
/// Flags products that will run out before a restock could arrive. Uses the last 30 days of
/// non-cancelled orders as the sales rate and the distributor's slowest lead time plus a safety buffer.
/// </summary>
public class LowStockService(MarketplaceDb db, TimeProvider clock)
{
    private const int LookbackDays = 30;
    private const int SafetyDays = 7;

    public async Task<List<LowStockItem>> ForDistributorAsync(int distributorId, CancellationToken ct = default)
    {
        var since = clock.GetUtcNow().UtcDateTime.AddDays(-LookbackDays);

        var leadTime = await db.ShippingMethods.Where(s => s.DistributorId == distributorId)
            .MaxAsync(s => (int?)s.LeadTimeDays, ct) ?? 0;

        var sold = await db.OrderLines
            .Where(l => l.Product!.DistributorId == distributorId)
            .Where(l => db.Orders.Any(o => o.Id == l.OrderId && o.CreatedUtc >= since && o.Status != OrderStatus.Cancelled))
            .GroupBy(l => l.ProductId)
            .Select(g => new { ProductId = g.Key, Units = g.Sum(l => l.Quantity) })
            .ToDictionaryAsync(x => x.ProductId, x => x.Units, ct);

        var products = await db.Products.Where(p => p.DistributorId == distributorId && p.IsListed).ToListAsync(ct);
        var horizon = leadTime + SafetyDays;

        var result = new List<LowStockItem>();
        foreach (var p in products)
        {
            var daily = Math.Round(sold.GetValueOrDefault(p.Id) / (decimal)LookbackDays, 2);
            if (daily == 0 && p.StockOnHand > 0) continue;   // not selling, nothing to plan for

            var cover = daily == 0 ? 0 : Math.Round(p.StockOnHand / daily, 1);
            if (cover >= horizon) continue;

            var needed = (int)Math.Ceiling(daily * horizon * 2) - p.StockOnHand;
            var reorder = Math.Max(needed, p.MinimumOrderQuantity);
            reorder = (int)Math.Ceiling(reorder / (decimal)p.CaseSize) * p.CaseSize;
            result.Add(new LowStockItem(p.Sku, p.Name, p.StockOnHand, daily, cover, reorder));
        }

        return result.OrderBy(r => r.DaysOfCover).ToList();
    }
}

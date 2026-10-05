using Marketplace.Api.Domain;

namespace Marketplace.Api.Contracts;

public record LoginRequest(string Username, string Password);
public record TokenResponse(string AccessToken, DateTime ExpiresUtc);

public record ProductDto(int Id, string Sku, string Name, decimal Price, int MinimumOrderQuantity, int CaseSize, bool InStock);

public record CreateOrderRequest(int DistributorId, int ShippingMethodId, List<CreateOrderLine> Lines);
public record CreateOrderLine(string Sku, int Quantity);

public record OrderDto(
    int Id,
    string Distributor,
    string Outlet,
    OrderStatus Status,
    DateOnly ExpectedDelivery,
    decimal Subtotal,
    decimal ShippingFee,
    decimal Total,
    List<OrderLineDto> Lines)
{
    public static OrderDto From(Order o) => new(
        o.Id, o.Distributor!.Name, o.Outlet!.Name, o.Status, o.ExpectedDelivery, o.Subtotal, o.ShippingFee, o.Total,
        o.Lines.Select(l => new OrderLineDto(l.Product!.Sku, l.Product.Name, l.Quantity, l.UnitPrice, l.LineTotal)).ToList());
}

public record OrderLineDto(string Sku, string Name, int Quantity, decimal UnitPrice, decimal LineTotal);

namespace Marketplace.Api.Domain;

public class Distributor
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Country { get; set; }
    public string Currency { get; set; } = "ZAR";
    public bool IsActive { get; set; } = true;
    public decimal MinimumOrderValue { get; set; }
    public ErpSystem Erp { get; set; }
    public List<Product> Products { get; set; } = [];
    public List<ShippingMethod> ShippingMethods { get; set; } = [];
}

public enum ErpSystem { None, Sap, Syspro }

public class Product
{
    public int Id { get; set; }
    public int DistributorId { get; set; }
    public Distributor? Distributor { get; set; }
    public required string Sku { get; set; }
    public required string Name { get; set; }
    public decimal Price { get; set; }
    public int MinimumOrderQuantity { get; set; } = 1;
    public int CaseSize { get; set; } = 1;
    public int StockOnHand { get; set; }
    public bool IsListed { get; set; } = true;
}

public class ShippingMethod
{
    public int Id { get; set; }
    public int DistributorId { get; set; }
    public required string Name { get; set; }
    public int LeadTimeDays { get; set; }
    public decimal Fee { get; set; }
    public decimal? FreeAbove { get; set; }
}

/// <summary>A retail outlet that buys from distributors.</summary>
public class Outlet
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Region { get; set; }
}

public class AppUser
{
    public int Id { get; set; }
    public required string Username { get; set; }
    public required string PasswordHash { get; set; }
    public UserRole Role { get; set; }
    public int? OutletId { get; set; }
    public int? DistributorId { get; set; }
}

public enum UserRole { Buyer, Distributor }

public class Order
{
    public int Id { get; set; }
    public int DistributorId { get; set; }
    public Distributor? Distributor { get; set; }
    public int OutletId { get; set; }
    public Outlet? Outlet { get; set; }
    public int ShippingMethodId { get; set; }
    public ShippingMethod? ShippingMethod { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Submitted;
    public DateTime CreatedUtc { get; set; }
    public DateOnly ExpectedDelivery { get; set; }
    public decimal Subtotal { get; set; }
    public decimal ShippingFee { get; set; }
    public decimal Total { get; set; }
    public string? ErpReference { get; set; }
    public List<OrderLine> Lines { get; set; } = [];
}

public enum OrderStatus { Submitted, SentToErp, Confirmed, Cancelled }

public class OrderLine
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}

/// <summary>
/// Written in the same transaction as the order, then picked up by the ERP integration.
/// Means an order is never saved without its outbound message, or the other way round.
/// </summary>
public class OutboxMessage
{
    public int Id { get; set; }
    public required string Type { get; set; }
    public required string Payload { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime? ProcessedUtc { get; set; }
}

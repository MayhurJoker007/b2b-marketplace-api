using Marketplace.Api.Domain;
using Microsoft.AspNetCore.Identity;

namespace Marketplace.Api.Data;

/// <summary>Demo data so the API is usable straight after `dotnet run`. All names are made up.</summary>
public static class SeedData
{
    public const string DemoPassword = "Demo@12345";

    public static void Apply(MarketplaceDb db)
    {
        if (db.Distributors.Any()) return;

        var highveld = new Distributor
        {
            Name = "Highveld Beverages", Country = "South Africa", MinimumOrderValue = 1500m, Erp = ErpSystem.Sap,
            Products =
            [
                new() { Sku = "HB-COLA-330", Name = "Cola 330ml can (24)", Price = 189.90m, MinimumOrderQuantity = 2, CaseSize = 1, StockOnHand = 420 },
                new() { Sku = "HB-WATER-500", Name = "Still water 500ml (24)", Price = 96.00m, MinimumOrderQuantity = 5, CaseSize = 5, StockOnHand = 900 },
                new() { Sku = "HB-JUICE-1L", Name = "Orange juice 1L (12)", Price = 254.50m, MinimumOrderQuantity = 1, CaseSize = 1, StockOnHand = 35 },
                new() { Sku = "HB-ENERGY-250", Name = "Energy drink 250ml (24)", Price = 312.00m, MinimumOrderQuantity = 1, CaseSize = 1, StockOnHand = 0 },
            ],
            ShippingMethods =
            [
                new() { Name = "Standard delivery", LeadTimeDays = 3, Fee = 250m, FreeAbove = 5000m },
                new() { Name = "Collect from depot", LeadTimeDays = 1, Fee = 0m },
            ],
        };
        var lusaka = new Distributor
        {
            Name = "Copperbelt Foods", Country = "Zambia", Currency = "ZMW", MinimumOrderValue = 800m, Erp = ErpSystem.Syspro,
            Products =
            [
                new() { Sku = "CF-MEAL-10", Name = "Maize meal 10kg", Price = 210.00m, MinimumOrderQuantity = 10, CaseSize = 10, StockOnHand = 1200 },
                new() { Sku = "CF-OIL-2L", Name = "Cooking oil 2L (6)", Price = 395.00m, MinimumOrderQuantity = 2, CaseSize = 2, StockOnHand = 140 },
            ],
            ShippingMethods = [new() { Name = "Road freight", LeadTimeDays = 5, Fee = 450m }],
        };
        db.Distributors.AddRange(highveld, lusaka);

        var spaza = new Outlet { Name = "Mamelodi Corner Store", Region = "Gauteng" };
        var tavern = new Outlet { Name = "Soweto Sports Tavern", Region = "Gauteng" };
        db.Outlets.AddRange(spaza, tavern);
        db.SaveChanges();

        var hasher = new PasswordHasher<AppUser>();
        AppUser User(string name, UserRole role, int? outlet = null, int? distributor = null)
        {
            var user = new AppUser { Username = name, PasswordHash = "", Role = role, OutletId = outlet, DistributorId = distributor };
            user.PasswordHash = hasher.HashPassword(user, DemoPassword);
            return user;
        }

        db.Users.AddRange(
            User("buyer.mamelodi", UserRole.Buyer, outlet: spaza.Id),
            User("buyer.soweto", UserRole.Buyer, outlet: tavern.Id),
            User("highveld.admin", UserRole.Distributor, distributor: highveld.Id));
        db.SaveChanges();
    }
}

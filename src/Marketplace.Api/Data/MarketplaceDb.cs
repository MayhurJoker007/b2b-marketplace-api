using Marketplace.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Api.Data;

public class MarketplaceDb(DbContextOptions<MarketplaceDb> options) : DbContext(options)
{
    public DbSet<Distributor> Distributors => Set<Distributor>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ShippingMethod> ShippingMethods => Set<ShippingMethod>();
    public DbSet<Outlet> Outlets => Set<Outlet>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();
    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Product>().HasIndex(p => new { p.DistributorId, p.Sku }).IsUnique();
        b.Entity<AppUser>().HasIndex(u => u.Username).IsUnique();
        b.Entity<Order>().HasIndex(o => new { o.OutletId, o.CreatedUtc });
        b.Entity<OutboxMessage>().HasIndex(m => m.ProcessedUtc);

        // SQLite has no decimal type, so store money as TEXT to keep it exact
        foreach (var property in b.Model.GetEntityTypes().SelectMany(t => t.GetProperties())
                     .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
        {
            property.SetColumnType("TEXT");
        }
    }
}

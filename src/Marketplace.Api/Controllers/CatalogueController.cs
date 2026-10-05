using Marketplace.Api.Contracts;
using Marketplace.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/distributors")]
public class CatalogueController(MarketplaceDb db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List() =>
        Ok(await db.Distributors.Where(d => d.IsActive)
            .Select(d => new { d.Id, d.Name, d.Country, d.Currency, d.MinimumOrderValue })
            .ToListAsync());

    [HttpGet("{id:int}/products")]
    public async Task<ActionResult<List<ProductDto>>> Products(int id, string? search = null)
    {
        var query = db.Products.Where(p => p.DistributorId == id && p.IsListed);
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p => EF.Functions.Like(p.Name, $"%{search}%") || p.Sku == search);

        return await query.OrderBy(p => p.Name)
            .Select(p => new ProductDto(p.Id, p.Sku, p.Name, p.Price, p.MinimumOrderQuantity, p.CaseSize, p.StockOnHand > 0))
            .ToListAsync();
    }

    [HttpGet("{id:int}/shipping-methods")]
    public async Task<IActionResult> ShippingMethods(int id) =>
        Ok(await db.ShippingMethods.Where(s => s.DistributorId == id)
            .Select(s => new { s.Id, s.Name, s.LeadTimeDays, s.Fee, s.FreeAbove })
            .ToListAsync());
}

using Marketplace.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Marketplace.Api.Controllers;

[ApiController]
[Authorize(Roles = "Distributor")]
[Route("api/stock")]
public class StockController(LowStockService lowStock) : ControllerBase
{
    [HttpGet("low")]
    public async Task<ActionResult<List<LowStockItem>>> Low(CancellationToken ct)
    {
        var distributorId = int.Parse(User.FindFirst(AppClaims.DistributorId)!.Value);
        return await lowStock.ForDistributorAsync(distributorId, ct);
    }
}

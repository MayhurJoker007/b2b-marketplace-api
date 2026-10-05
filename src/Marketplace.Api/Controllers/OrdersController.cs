using Marketplace.Api.Contracts;
using Marketplace.Api.Data;
using Marketplace.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Api.Controllers;

[ApiController]
[Authorize(Roles = "Buyer")]
[Route("api/orders")]
public class OrdersController(MarketplaceDb db, OrderService orders) : ControllerBase
{
    // The outlet always comes from the token, never the request body,
    // so a buyer can't place or read orders for someone else's store.
    private int OutletId => int.Parse(User.FindFirst(AppClaims.OutletId)!.Value);

    [HttpPost]
    public async Task<ActionResult<OrderDto>> Create(CreateOrderRequest request, CancellationToken ct)
    {
        try
        {
            var order = await orders.PlaceAsync(OutletId, request, ct);
            var dto = await Load(order.Id, ct);
            return CreatedAtAction(nameof(Get), new { id = order.Id }, dto);
        }
        catch (OrderRejectedException e)
        {
            return Rejected(e);
        }
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OrderDto>> Get(int id, CancellationToken ct)
    {
        var dto = await Load(id, ct);
        return dto is null ? NotFound() : dto;
    }

    [HttpGet]
    public async Task<ActionResult<List<OrderDto>>> Mine(CancellationToken ct)
    {
        var list = await Query().Where(o => o.OutletId == OutletId)
            .OrderByDescending(o => o.CreatedUtc).Take(50).ToListAsync(ct);
        return list.Select(OrderDto.From).ToList();
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<ActionResult<OrderDto>> Cancel(int id, CancellationToken ct)
    {
        try
        {
            var order = await orders.CancelAsync(id, OutletId, ct);
            return order is null ? NotFound() : await Load(id, ct) ?? (ActionResult<OrderDto>)NotFound();
        }
        catch (OrderRejectedException e)
        {
            return Rejected(e);
        }
    }

    private IQueryable<Domain.Order> Query() => db.Orders.AsNoTracking()
        .Include(o => o.Distributor).Include(o => o.Outlet)
        .Include(o => o.Lines).ThenInclude(l => l.Product);

    private async Task<OrderDto?> Load(int id, CancellationToken ct)
    {
        var order = await Query().FirstOrDefaultAsync(o => o.Id == id && o.OutletId == OutletId, ct);
        return order is null ? null : OrderDto.From(order);
    }

    private ObjectResult Rejected(OrderRejectedException e) =>
        UnprocessableEntity(new ValidationProblemDetails(new Dictionary<string, string[]>
        {
            ["order"] = e.Problems.ToArray(),
        }) { Title = "Order rejected" });
}

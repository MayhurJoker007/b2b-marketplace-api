using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Marketplace.Api.Contracts;
using Marketplace.Api.Domain;

namespace Marketplace.Tests;

public class OrderApiTests : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly ApiFactory _api;
    private readonly int _highveld;
    private readonly int _standardDelivery;

    public OrderApiTests(ApiFactory api)
    {
        _api = api;
        _api.CreateClient().Dispose(); // makes sure the app has started and seeded
        (_highveld, _standardDelivery) = api.WithDb(db =>
        {
            var d = db.Distributors.Single(x => x.Name == "Highveld Beverages");
            return (d.Id, db.ShippingMethods.Single(s => s.DistributorId == d.Id && s.Name == "Standard delivery").Id);
        });
    }

    private CreateOrderRequest Order(params (string sku, int qty)[] lines) =>
        new(_highveld, _standardDelivery, lines.Select(l => new CreateOrderLine(l.sku, l.qty)).ToList());

    private static async Task<string[]> Problems(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("errors").GetProperty("order").EnumerateArray().Select(e => e.GetString()!).ToArray();
    }

    [Fact]
    public async Task Requires_a_token()
    {
        var response = await _api.CreateClient().PostAsJsonAsync("/api/orders", Order(("HB-COLA-330", 10)));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Wrong_password_is_rejected()
    {
        var response = await _api.CreateClient().PostAsJsonAsync("/api/auth/token", new LoginRequest("buyer.soweto", "nope"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Places_a_valid_order_and_reserves_stock()
    {
        var client = await _api.LoginAs("buyer.mamelodi");
        var before = _api.WithDb(db => db.Products.Single(p => p.Sku == "HB-COLA-330").StockOnHand);

        var response = await client.PostAsJsonAsync("/api/orders", Order(("HB-COLA-330", 10)));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var order = await response.Content.ReadFromJsonAsync<OrderDto>(Json);
        Assert.Equal(1899.00m, order!.Subtotal);
        Assert.Equal(250m, order.ShippingFee);
        Assert.Equal(OrderStatus.Submitted, order.Status);
        Assert.Equal(before - 10, _api.WithDb(db => db.Products.Single(p => p.Sku == "HB-COLA-330").StockOnHand));
        Assert.True(_api.WithDb(db => db.Outbox.Any(m => m.Payload.Contains($"\"orderId\":{order.Id}"))));
    }

    [Fact]
    public async Task Free_shipping_above_the_threshold()
    {
        var client = await _api.LoginAs("buyer.mamelodi");
        var response = await client.PostAsJsonAsync("/api/orders", Order(("HB-COLA-330", 30)));
        var order = await response.Content.ReadFromJsonAsync<OrderDto>(Json);
        Assert.Equal(0m, order!.ShippingFee);
    }

    [Fact]
    public async Task Rejects_every_broken_rule_at_once()
    {
        var client = await _api.LoginAs("buyer.mamelodi");

        var response = await client.PostAsJsonAsync("/api/orders", Order(
            ("HB-WATER-500", 7),      // not a multiple of the case size (5)
            ("HB-JUICE-1L", 500),     // more than in stock
            ("HB-ENERGY-250", 1),     // out of stock
            ("CF-MEAL-10", 10)));     // belongs to another distributor

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problems = await Problems(response);
        Assert.Contains(problems, p => p.Contains("multiples of 5"));
        Assert.Contains(problems, p => p.StartsWith("HB-JUICE-1L: only"));
        Assert.Contains(problems, p => p.StartsWith("HB-ENERGY-250: only 0"));
        Assert.Contains(problems, p => p.StartsWith("CF-MEAL-10: not sold"));
    }

    [Fact]
    public async Task Rejects_orders_under_the_minimum_value()
    {
        var client = await _api.LoginAs("buyer.mamelodi");
        var response = await client.PostAsJsonAsync("/api/orders", Order(("HB-COLA-330", 2)));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains(await Problems(response), p => p.StartsWith("Minimum order value is 1500.00"));
    }

    [Fact]
    public async Task Rejects_shipping_method_from_another_distributor()
    {
        var client = await _api.LoginAs("buyer.mamelodi");
        var otherShipping = _api.WithDb(db => db.ShippingMethods.Single(s => s.Name == "Road freight").Id);

        var response = await client.PostAsJsonAsync("/api/orders",
            new CreateOrderRequest(_highveld, otherShipping, [new("HB-COLA-330", 10)]));

        Assert.Contains(await Problems(response), p => p.Contains("Shipping method"));
    }

    [Fact]
    public async Task Buyers_cannot_see_other_outlets_orders()
    {
        var mamelodi = await _api.LoginAs("buyer.mamelodi");
        var created = await mamelodi.PostAsJsonAsync("/api/orders", Order(("HB-COLA-330", 10)));
        var order = await created.Content.ReadFromJsonAsync<OrderDto>(Json);

        var soweto = await _api.LoginAs("buyer.soweto");
        var response = await soweto.GetAsync($"/api/orders/{order!.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Cancelling_returns_the_stock()
    {
        var client = await _api.LoginAs("buyer.soweto");
        var before = _api.WithDb(db => db.Products.Single(p => p.Sku == "HB-WATER-500").StockOnHand);
        var created = await client.PostAsJsonAsync("/api/orders", Order(("HB-WATER-500", 20)));
        var order = await created.Content.ReadFromJsonAsync<OrderDto>(Json);

        var response = await client.PostAsync($"/api/orders/{order!.Id}/cancel", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(before, _api.WithDb(db => db.Products.Single(p => p.Sku == "HB-WATER-500").StockOnHand));

        var again = await client.PostAsync($"/api/orders/{order.Id}/cancel", null);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, again.StatusCode);
    }

    [Fact]
    public async Task Distributor_users_cannot_place_orders()
    {
        var client = await _api.LoginAs("highveld.admin");
        var response = await client.PostAsJsonAsync("/api/orders", Order(("HB-COLA-330", 10)));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Low_stock_report_flags_out_of_stock_items()
    {
        var client = await _api.LoginAs("highveld.admin");
        var items = await client.GetFromJsonAsync<JsonElement>("/api/stock/low");
        Assert.Contains(items.EnumerateArray(), i => i.GetProperty("sku").GetString() == "HB-ENERGY-250");
    }
}

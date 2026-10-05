# B2B Marketplace API

An ASP.NET Core Web API for a wholesale ordering marketplace: retail outlets (spaza shops, taverns, corner
stores) order stock from distributors, and every order rule is enforced on the server.

It's modelled on the kind of platform I work on day to day, rebuilt from scratch with made-up distributors and
products. The interesting parts are the business rules and the integration hand-off, not the CRUD.

**Stack:** .NET 9, ASP.NET Core controllers, EF Core (SQLite locally), JWT bearer auth, Swagger, xUnit +
WebApplicationFactory.

## The rules

An order is rejected with a 422 and a list of *every* problem, not just the first one, so the storefront can show
them all at once:

- the distributor must be active, and the shipping method must be one of theirs
- each SKU must be listed by that distributor
- quantity must meet the product's minimum order quantity and be a multiple of its case size
- quantity can't exceed stock on hand
- the order must reach the distributor's minimum order value

```json
{
  "title": "Order rejected",
  "status": 422,
  "errors": {
    "order": [
      "HB-WATER-500: must be ordered in multiples of 5",
      "HB-ENERGY-250: only 0 in stock",
      "CF-MEAL-10: not sold by Highveld Beverages"
    ]
  }
}
```

Other details:

- **Stock is reserved with a conditional update** (`UPDATE ... WHERE stock >= qty`) inside the order
  transaction, so two buyers can't both take the last few cases.
- **Delivery dates count working days only**, with a 14:00 cut-off. An order on Friday afternoon with a one-day
  lead time arrives on Tuesday.
- **Free shipping** above a per-method threshold.
- **The outlet comes from the JWT**, never from the request body. A buyer can't place or read orders for
  another store, and gets a 404 rather than a 403 so order IDs can't be probed.
- **Transactional outbox:** placing an order writes an `order.submitted` message in the same transaction. The
  ERP integration ([erp-integration-simulator](https://github.com/MayhurJoker007/erp-integration-simulator))
  picks it up from there, so an order can never be saved without its message or the other way round.
- **Low stock report** for distributors: 30-day sales rate against the slowest lead time plus a week's buffer,
  with a suggested reorder rounded up to the case size.

## Running it

```
dotnet run --project src/Marketplace.Api
```

Open `http://localhost:5095/swagger`. The database is created and seeded on first run. Demo logins, all with
password `Demo@12345`:

| User | Role |
|---|---|
| `buyer.mamelodi` | Buyer for Mamelodi Corner Store |
| `buyer.soweto` | Buyer for Soweto Sports Tavern |
| `highveld.admin` | Distributor (Highveld Beverages) |

Call `POST /api/auth/token`, then click Authorize in Swagger and paste the token.

The development signing key in `appsettings.Development.json` is only for local use. Anywhere else, set
`Jwt__SigningKey` as an environment variable.

## Endpoints

| | | |
|---|---|---|
| POST | `/api/auth/token` | log in |
| GET | `/api/distributors` | active distributors |
| GET | `/api/distributors/{id}/products?search=` | catalogue |
| GET | `/api/distributors/{id}/shipping-methods` | |
| POST | `/api/orders` | place an order (buyer) |
| GET | `/api/orders`, `/api/orders/{id}` | your outlet's orders |
| POST | `/api/orders/{id}/cancel` | only while still Submitted |
| GET | `/api/stock/low` | low stock report (distributor) |

## Tests

```
dotnet test
```

These are integration tests that start the real API against an in-memory SQLite database. They cover auth, each
validation rule, stock reservation and release, outlet isolation, role checks and the delivery date calendar.

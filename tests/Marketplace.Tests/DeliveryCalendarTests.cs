using Marketplace.Api.Services;

namespace Marketplace.Tests;

public class DeliveryCalendarTests
{
    [Theory]
    [InlineData("2026-10-05 09:00", 3, "2026-10-08")]   // Monday morning, plain 3 working days
    [InlineData("2026-10-05 15:30", 3, "2026-10-09")]   // after the 14:00 cut-off
    [InlineData("2026-10-08 10:00", 3, "2026-10-13")]   // Thursday, skips the weekend
    [InlineData("2026-10-10 10:00", 1, "2026-10-13")]   // placed on a Saturday
    [InlineData("2026-10-09 16:00", 1, "2026-10-13")]   // Friday after cut-off
    public void Counts_working_days_only(string placed, int leadTime, string expected)
    {
        var result = DeliveryCalendar.ExpectedDelivery(DateTime.Parse(placed), leadTime);
        Assert.Equal(DateOnly.Parse(expected), result);
    }
}

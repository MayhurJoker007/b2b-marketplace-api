namespace Marketplace.Api.Services;

/// <summary>Lead times are in working days. Orders placed after the cut-off count from the next working day.</summary>
public static class DeliveryCalendar
{
    public static readonly TimeOnly CutOff = new(14, 0);

    public static DateOnly ExpectedDelivery(DateTime placedLocal, int leadTimeDays)
    {
        var day = DateOnly.FromDateTime(placedLocal);
        if (TimeOnly.FromDateTime(placedLocal) >= CutOff || !IsWorkingDay(day))
            day = NextWorkingDay(day);

        for (var i = 0; i < leadTimeDays; i++)
            day = NextWorkingDay(day);
        return day;
    }

    private static DateOnly NextWorkingDay(DateOnly day)
    {
        do day = day.AddDays(1); while (!IsWorkingDay(day));
        return day;
    }

    private static bool IsWorkingDay(DateOnly day) =>
        day.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);
}

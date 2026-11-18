namespace Gearbox.Modules.Notch.Logic;

public static class BillingPeriod
{
    public static DateTimeOffset MonthStart(DateTimeOffset now)
    {
        var utc = now.UtcDateTime;
        return new DateTimeOffset(utc.Year, utc.Month, 1, 0, 0, 0, TimeSpan.Zero);
    }
}

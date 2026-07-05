namespace AreWeDoomd.Domain.Scheduling;

/// <summary>
/// "Bugün" = Türkiye takvim günü, sabit UTC+3.
/// TimeZoneInfo bilinçli olarak kullanılmıyor: ID Windows'ta "Turkey Standard Time",
/// Linux/Docker'da "Europe/Istanbul" — bu repo iki ortamda da çalışıyor.
/// Türkiye 2016'dan beri DST uygulamadığı için sabit offset güvenli.
/// </summary>
public static class TurkeySchedulingWindow
{
    public static readonly TimeSpan Offset = TimeSpan.FromHours(3);

    public static DateOnly TurkeyDateOf(DateTimeOffset utc)
    {
        return DateOnly.FromDateTime(utc.UtcDateTime.Add(Offset));
    }

    public static DateTimeOffset DayStartUtc(DateOnly turkeyDate)
    {
        return new DateTimeOffset(turkeyDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero) - Offset;
    }

    public static DateTimeOffset DayEndUtc(DateOnly turkeyDate)
    {
        return DayStartUtc(turkeyDate).AddDays(1);
    }
}

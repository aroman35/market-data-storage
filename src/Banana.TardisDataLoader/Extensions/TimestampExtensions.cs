namespace Banana.TardisDataLoader.Extensions;

public static class TimestampExtensions
{
    private const long TICKS_PER_MICROSECOND = TimeSpan.TicksPerMillisecond / 1000; // 10

    /// <summary>
    /// Создаёт DateTimeOffset (UTC) из количества микросекунд, прошедших с Unix-эпохи (1970-01-01T00:00:00Z).
    /// Бросает ArgumentOutOfRangeException при переполнении диапазона DateTimeOffset.
    /// </summary>
    public static DateTimeOffset AsUnixMicroseconds(this long microsecondsUtc)
    {
        try
        {
            var ticksSinceEpoch = checked(microsecondsUtc * TICKS_PER_MICROSECOND);
            return DateTimeOffset.UnixEpoch.AddTicks(ticksSinceEpoch);
        }
        catch (OverflowException)
        {
            throw new ArgumentOutOfRangeException(
                nameof(microsecondsUtc),
                microsecondsUtc,
                "Value is outside the supported DateTimeOffset range.");
        }
    }

    /// <summary>
    /// Date component in the offset's local wall-clock time (offset is respected, timezone not applied).
    /// </summary>
    public static DateOnly ToDateOnly(this DateTimeOffset value)
        => DateOnly.FromDateTime(value.DateTime);

    /// <summary>
    /// Time component in the offset's local wall-clock time (microsecond precision preserved).
    /// </summary>
    public static TimeOnly ToTimeOnly(this DateTimeOffset value)
        => TimeOnly.FromTimeSpan(value.TimeOfDay);

    /// <summary>
    /// Date component in UTC.
    /// </summary>
    public static DateOnly ToUtcDateOnly(this DateTimeOffset value)
        => DateOnly.FromDateTime(value.UtcDateTime);

    /// <summary>
    /// Time component in UTC (microsecond precision preserved).
    /// </summary>
    public static TimeOnly ToUtcTimeOnly(this DateTimeOffset value)
        => TimeOnly.FromTimeSpan(value.UtcDateTime.TimeOfDay);

    /// <summary>
    /// Date component in a specific time zone.
    /// </summary>
    public static DateOnly ToDateOnly(this DateTimeOffset value, TimeZoneInfo timeZone)
    {
        var zoned = TimeZoneInfo.ConvertTime(value, timeZone);
        return DateOnly.FromDateTime(zoned.DateTime);
    }

    /// <summary>
    /// Time component in a specific time zone (microsecond precision preserved).
    /// </summary>
    public static TimeOnly ToTimeOnly(this DateTimeOffset value, TimeZoneInfo timeZone)
    {
        var zoned = TimeZoneInfo.ConvertTime(value, timeZone);
        return TimeOnly.FromTimeSpan(zoned.TimeOfDay);
    }
}

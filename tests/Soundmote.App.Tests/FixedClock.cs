namespace Soundmote.App.Tests;

/// <summary>A clock that moves only when told, in UTC, so clock scenes, end times and saving are the same on every run.</summary>
public sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}

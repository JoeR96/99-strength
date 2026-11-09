namespace A2S.Domain.Common;

/// <summary>
/// The one place that turns instants into the calendar dates a person experiences.
/// Every human-facing date in the app (habits, sessions, streaks, weeks) is a <see cref="DateOnly"/>
/// in Europe/London produced through this interface, so day boundaries, week rollover and streaks are
/// right across the BST/GMT changes.
/// </summary>
public interface ILocalClock
{
    /// <summary>The current instant.</summary>
    DateTimeOffset NowUtc();

    /// <summary>Today's local date.</summary>
    DateOnly Today();

    /// <summary>The local date on which <paramref name="utc"/> happened.</summary>
    DateOnly ToLocalDate(DateTime utc);

    /// <summary>The local date on which <paramref name="instant"/> happened.</summary>
    DateOnly ToLocalDate(DateTimeOffset instant);

    /// <summary>The UTC instants [start, end) that make up <paramref name="localDate"/>. 23, 24 or 25 hours long.</summary>
    (DateTime StartUtc, DateTime EndUtc) BoundsOf(DateOnly localDate);
}

/// <summary>Europe/London implementation on top of <see cref="TimeProvider"/>.</summary>
public sealed class LocalClock : ILocalClock
{
    public const string TimeZoneId = "Europe/London";

    public static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);

    private readonly TimeProvider _timeProvider;

    public LocalClock(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public DateTimeOffset NowUtc() => _timeProvider.GetUtcNow();

    public DateOnly Today() => ToLocalDate(_timeProvider.GetUtcNow());

    public DateOnly ToLocalDate(DateTime utc)
    {
        var asUtc = utc.Kind switch
        {
            DateTimeKind.Utc => utc,
            DateTimeKind.Local => utc.ToUniversalTime(),
            _ => DateTime.SpecifyKind(utc, DateTimeKind.Utc)
        };
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(asUtc, Zone));
    }

    public DateOnly ToLocalDate(DateTimeOffset instant) => ToLocalDate(instant.UtcDateTime);

    public (DateTime StartUtc, DateTime EndUtc) BoundsOf(DateOnly localDate)
    {
        var startLocal = localDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var endLocal = localDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return (TimeZoneInfo.ConvertTimeToUtc(startLocal, Zone), TimeZoneInfo.ConvertTimeToUtc(endLocal, Zone));
    }
}

/// <summary>Calendar helpers for the Monday-to-Sunday training week.</summary>
public static class LocalDates
{
    /// <summary>The Monday that starts the week containing <paramref name="date"/>.</summary>
    public static DateOnly StartOfWeek(DateOnly date)
    {
        var offset = ((int)date.DayOfWeek + 6) % 7; // Monday = 0 ... Sunday = 6
        return date.AddDays(-offset);
    }

    /// <summary>The Sunday that ends the week containing <paramref name="date"/>.</summary>
    public static DateOnly EndOfWeek(DateOnly date) => StartOfWeek(date).AddDays(6);

    /// <summary>
    /// The first Monday on or after <paramref name="date"/>. A plan started on a Wednesday begins the
    /// coming Monday; one started on a Monday begins that day.
    /// </summary>
    public static DateOnly ComingMonday(DateOnly date)
    {
        var daysUntil = ((int)DayOfWeek.Monday - (int)date.DayOfWeek + 7) % 7;
        return date.AddDays(daysUntil);
    }
}

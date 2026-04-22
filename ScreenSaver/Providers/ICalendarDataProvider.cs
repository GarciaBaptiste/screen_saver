namespace ScreenSaver.Providers;

/// <summary>
/// Contract for Phase 3 calendar data providers (journées internationales, iCal, etc.).
/// </summary>
public interface ICalendarDataProvider
{
    /// <summary>Human-readable name shown in the UI (Phase 4).</summary>
    string Name { get; }

    /// <summary>Returns events/annotations for the given date.</summary>
    Task<IReadOnlyList<CalendarEntry>> GetEntriesAsync(DateOnly date, CancellationToken ct = default);
}

public sealed record CalendarEntry(
    string Title,
    string? Description = null,
    Uri?    MoreInfoUrl = null
);

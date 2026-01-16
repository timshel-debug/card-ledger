using CardService.Application.Common;

namespace CardService.Tests.Common.TestHelpers;

/// <summary>
/// Test double for IClock that returns a fixed, controllable time.
/// </summary>
/// <remarks>
/// This shared test helper eliminates duplication across test projects and provides
/// a consistent way to control time in integration and unit tests.
/// </remarks>
public class FixedClock : IClock
{
    /// <summary>
    /// Gets or sets the current UTC time returned by this clock.
    /// </summary>
    public DateTime UtcNow { get; set; }

    /// <summary>
    /// Gets the current UTC date (date portion of UtcNow).
    /// </summary>
    public DateOnly UtcToday => DateOnly.FromDateTime(UtcNow);

    /// <summary>
    /// Initializes a new instance of the <see cref="FixedClock"/> class with the specified time.
    /// </summary>
    /// <param name="utcNow">The fixed UTC time this clock will return.</param>
    public FixedClock(DateTime utcNow)
    {
        UtcNow = utcNow;
    }

    /// <summary>
    /// Updates the clock to return a different time.
    /// </summary>
    /// <param name="utcNow">The new UTC time this clock will return.</param>
    public void SetTime(DateTime utcNow)
    {
        UtcNow = utcNow;
    }
}

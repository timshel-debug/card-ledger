using CardService.Application.Common;

namespace CardService.Infrastructure.Services;

/// <summary>
/// Implementation of the clock port that provides system time.
/// </summary>
/// <remarks>
/// <para>
/// This service is a simple wrapper around <c>DateTime.UtcNow</c> and <c>DateOnly</c>,
/// enabling dependency injection and testability by providing a replaceable abstraction
/// for time-based operations.
/// </para>
/// <para>
/// In production, this is injected as the <see cref="IClock"/> implementation.
/// In testing, a mock clock can be injected to control time behavior and verify
/// time-sensitive logic (e.g., FX rate window calculations, 6-month lookback periods).
/// </para>
/// </remarks>
public class SystemClock : IClock
{
    /// <summary>
    /// Gets the current UTC date and time from the system clock.
    /// </summary>
    /// <remarks>
    /// This property returns <c>DateTime.UtcNow</c>, providing the current UTC time with full precision.
    /// Used for recording creation/modification timestamps in audit trails and business operations.
    /// </remarks>
    /// <value>The current UTC date and time (with millisecond precision).</value>
    public DateTime UtcNow => DateTime.UtcNow;

    /// <summary>
    /// Gets the current UTC date (date only, without time component).
    /// </summary>
    /// <remarks>
    /// This property returns a <see cref="DateOnly"/> representing today's date in UTC.
    /// Used for FX rate resolution and other business logic that operates at date granularity
    /// (rather than timestamp granularity).
    /// </remarks>
    /// <value>The current UTC date as <see cref="DateOnly"/> (no time component).</value>
    public DateOnly UtcToday => DateOnly.FromDateTime(DateTime.UtcNow);
}

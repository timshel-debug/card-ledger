namespace CardService.Application.Common;

/// <summary>
/// Port interface for abstracting system time to enable testability and deterministic behavior.
/// </summary>
/// <remarks>
/// <para>
/// This interface abstracts the system clock, enabling:
/// <list type="bullet">
/// <item><strong>Testability:</strong> Tests can inject a mock clock to verify behavior at specific points in time.</item>
/// <item><strong>Determinism:</strong> Use cases can accept an <c>asOfDate</c> parameter to override the clock for historical queries.</item>
/// <item><strong>Timezone Safety:</strong> Always uses UTC to avoid timezone-related bugs and ensure consistent behavior across deployments.</item>
/// </list>
/// </para>
/// <para>
/// Rather than directly calling <c>DateTime.UtcNow</c> or <c>DateOnly.FromDateTime(DateTime.UtcNow)</c>,
/// use cases and services depend on this interface for time-based operations.
/// </para>
/// </remarks>
public interface IClock
{
    /// <summary>
    /// Gets the current UTC date and time.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This property returns <c>DateTime.UtcNow</c> or an equivalent time from a test double.
    /// Always returns UTC to ensure timezone-neutral behavior.
    /// </para>
    /// <para>
    /// Used for:
    /// <list type="bullet">
    /// <item>Recording creation timestamps for cards and purchases</item>
    /// <item>Providing default timestamps for audit trails</item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <value>The current UTC date and time.</value>
    /// <example>
    /// <code>
    /// var now = clock.UtcNow; // 2024-11-15T14:30:45.123Z
    /// var card = Card.Create(cardNumber, creditLimit, hash, now);
    /// </code>
    /// </example>
    DateTime UtcNow { get; }

    /// <summary>
    /// Gets the current UTC date (without time component).
    /// </summary>
    /// <remarks>
    /// <para>
    /// This property returns the date portion of <c>DateTime.UtcNow</c> or an equivalent
    /// date from a test double. Always uses UTC.
    /// </para>
    /// <para>
    /// Used for:
    /// <list type="bullet">
    /// <item>FX rate resolution when no explicit <c>asOfDate</c> is provided (real-time conversions)</item>
    /// <item>Determining the current "day" for business logic that operates on date granularity</item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <value>The current UTC date as a <see cref="DateOnly"/>.</value>
    /// <example>
    /// <code>
    /// var today = clock.UtcToday; // 2024-11-15
    /// var fxRate = await fxRateResolver.ResolveRateAsync("Australia-Dollar", today);
    /// </code>
    /// </example>
    DateOnly UtcToday { get; }
}

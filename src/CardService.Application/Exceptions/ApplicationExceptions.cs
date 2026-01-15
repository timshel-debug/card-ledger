namespace CardService.Application.Exceptions;

/// <summary>
/// Base exception class for application-level errors in CardService.
/// </summary>
/// <remarks>
/// <para>All application exceptions inherit from this class and carry a stable error code for client-side handling.</para>
/// <para>Error codes follow the pattern: TYPE-HTTPSTATUS (e.g., "VAL-0001" for validation errors returning 400).</para>
/// </remarks>
public class ApplicationException : Exception
{
    /// <summary>
    /// Gets the stable error code for this exception.
    /// </summary>
    /// <value>A string code in the format TYPE-HTTPSTATUS (e.g., "VAL-0001", "RES-4040").</value>
    public string Code { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ApplicationException"/> class with a code and message.
    /// </summary>
    /// <param name="code">The error code (e.g., "VAL-0001").</param>
    /// <param name="message">A message that describes the error.</param>
    public ApplicationException(string code, string message) : base(message)
    {
        Code = code;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ApplicationException"/> class with a code, message, and inner exception.
    /// </summary>
    /// <param name="code">The error code (e.g., "VAL-0001").</param>
    /// <param name="message">A message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public ApplicationException(string code, string message, Exception innerException) 
        : base(message, innerException)
    {
        Code = code;
    }
}

/// <summary>
/// Thrown when input validation fails (e.g., invalid card number, negative amount).
/// </summary>
/// <remarks>
/// <para>Error code: "VAL-0001"</para>
/// <para>HTTP status: 400 Bad Request</para>
/// </remarks>
public class ValidationException : ApplicationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationException"/> class with a message.
    /// </summary>
    /// <param name="message">A description of the validation error.</param>
    public ValidationException(string message) : base("VAL-0001", message) { }
}

/// <summary>
/// Thrown when a requested resource is not found (e.g., card ID does not exist).
/// </summary>
/// <remarks>
/// <para>Error code: "RES-4040"</para>
/// <para>HTTP status: 404 Not Found</para>
/// </remarks>
public class ResourceNotFoundException : ApplicationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ResourceNotFoundException"/> class with a message.
    /// </summary>
    /// <param name="message">A description of which resource was not found.</param>
    public ResourceNotFoundException(string message) : base("RES-4040", message) { }
}

/// <summary>
/// Thrown when attempting to create a resource that already exists (e.g., duplicate card number).
/// </summary>
/// <remarks>
/// <para>Error code: "DB-4090"</para>
/// <para>HTTP status: 409 Conflict</para>
/// </remarks>
public class DuplicateResourceException : ApplicationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DuplicateResourceException"/> class with a message.
    /// </summary>
    /// <param name="message">A description of the duplicate resource.</param>
    public DuplicateResourceException(string message) : base("DB-4090", message) { }
}

/// <summary>
/// Thrown when a currency conversion cannot be performed because no exchange rate is available within the required window.
/// </summary>
/// <remarks>
/// <para>Error code: "FX-4220"</para>
/// <para>HTTP status: 422 Unprocessable Entity</para>
/// <para>Typically indicates no Treasury FX rate exists within the 6-month lookback window.</para>
/// </remarks>
public class FxConversionUnavailableException : ApplicationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FxConversionUnavailableException"/> class with a message.
    /// </summary>
    /// <param name="message">A description of which currency and date range were problematic.</param>
    public FxConversionUnavailableException(string message) : base("FX-4220", message) { }
}

/// <summary>
/// Thrown when the Treasury FX upstream service is unavailable and no cached fallback rate exists.
/// </summary>
/// <remarks>
/// <para>Error code: "FX-5030"</para>
/// <para>HTTP status: 503 Service Unavailable</para>
/// <para>Indicates that the Treasury API is unreachable and no valid cached rate is available.</para>
/// </remarks>
public class FxUpstreamUnavailableException : ApplicationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FxUpstreamUnavailableException"/> class with a message.
    /// </summary>
    /// <param name="message">A description of the upstream failure.</param>
    public FxUpstreamUnavailableException(string message) : base("FX-5030", message) { }
    
    /// <summary>
    /// Initializes a new instance of the <see cref="FxUpstreamUnavailableException"/> class with a message and inner exception.
    /// </summary>
    /// <param name="message">A description of the upstream failure.</param>
    /// <param name="innerException">The underlying exception from the upstream provider (e.g., <see cref="HttpRequestException"/>).</param>
    public FxUpstreamUnavailableException(string message, Exception innerException) 
        : base("FX-5030", message, innerException) { }
}

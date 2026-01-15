using CardService.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace CardService.Api.Middleware;

/// <summary>
/// Middleware for handling application exceptions and converting them to Problem Details responses.
/// </summary>
/// <remarks>
/// <para>
/// This middleware wraps the entire request pipeline and catches exceptions, converting them into
/// RFC 7807 Problem Details responses with appropriate HTTP status codes and error codes.
/// </para>
/// <para>
/// Exception Mapping:
/// <list type="bullet">
/// <item><see cref="ValidationException"/> → 400 Bad Request (VAL-0001)</item>
/// <item><see cref="ResourceNotFoundException"/> → 404 Not Found (RES-4040)</item>
/// <item><see cref="DuplicateResourceException"/> → 409 Conflict (DB-4090)</item>
/// <item><see cref="FxConversionUnavailableException"/> → 422 Unprocessable Entity (FX-4220)</item>
/// <item><see cref="FxUpstreamUnavailableException"/> → 503 Service Unavailable (FX-5030)</item>
/// <item>Unhandled exceptions → 500 Internal Server Error (INTERNAL-5000)</item>
/// </list>
/// </para>
/// <para>
/// Responses are formatted as JSON with camelCase property names for consistency with
/// REST API conventions. The error code is included in the Problem Details extension property.
/// </para>
/// </remarks>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExceptionHandlingMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="logger">Logger for recording exception details.</param>
    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Invokes the middleware, catching exceptions and converting them to Problem Details responses.
    /// </summary>
    /// <remarks>
    /// This method wraps the next middleware in a try-catch block. Any unhandled exception
    /// is logged and converted to an appropriate Problem Details response.
    /// </remarks>
    /// <param name="context">The HTTP context.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    /// <summary>
    /// Handles an exception by converting it to a Problem Details response.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method:
    /// <list type="number">
    /// <item>Logs the exception with error level</item>
    /// <item>Determines the HTTP status code, error code, and title based on exception type</item>
    /// <item>Constructs a <see cref="ProblemDetails"/> object with exception details</item>
    /// <item>Serializes to JSON with camelCase naming policy</item>
    /// <item>Writes the response to the HTTP context</item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="context">The HTTP context to write the error response to.</param>
    /// <param name="exception">The exception to handle.</param>
    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        _logger.LogError(exception, "An error occurred processing the request.");

        var (statusCode, code, title) = exception switch
        {
            ValidationException => (StatusCodes.Status400BadRequest, "VAL-0001", "Validation Error"),
            ResourceNotFoundException => (StatusCodes.Status404NotFound, "RES-4040", "Resource Not Found"),
            DuplicateResourceException => (StatusCodes.Status409Conflict, "DB-4090", "Duplicate Resource"),
            FxConversionUnavailableException => (StatusCodes.Status422UnprocessableEntity, "FX-4220", "Conversion Unavailable"),
            FxUpstreamUnavailableException => (StatusCodes.Status503ServiceUnavailable, "FX-5030", "Upstream Service Unavailable"),
            _ => (StatusCodes.Status500InternalServerError, "INTERNAL-5000", "Internal Server Error")
        };

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = exception.Message,
            Instance = context.Request.Path
        };

        problemDetails.Extensions["code"] = code;

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        var json = JsonSerializer.Serialize(problemDetails, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}

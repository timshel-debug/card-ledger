using CardService.Application.DTOs;
using CardService.Application.UseCases;
using Microsoft.AspNetCore.Mvc;

namespace CardService.Api.Endpoints;

/// <summary>
/// API endpoints for purchase transaction management and conversion operations.
/// </summary>
/// <remarks>
/// <para>
/// This static class defines minimal API endpoints for:
/// <list type="bullet">
/// <item><strong>POST /cards/{cardId}/purchases</strong> — Create a new purchase transaction</item>
/// <item><strong>GET /cards/{cardId}/purchases/{purchaseId}</strong> — Retrieve purchase with currency conversion</item>
/// </list>
/// </para>
/// <para>
/// Endpoints use ASP.NET Core's minimal APIs approach with dependency injection of use cases.
/// Exception handling is delegated to the <see cref="ExceptionHandlingMiddleware"/>.
/// </para>
/// </remarks>
public static class PurchaseEndpoints
{
    /// <summary>
    /// Maps all purchase-related endpoints to the application.
    /// </summary>
    /// <remarks>
    /// This extension method is called from Program.cs to register purchase endpoints.
    /// Creates a route group at "/cards/{cardId}/purchases" tagged as "Purchases" for OpenAPI documentation.
    /// </remarks>
    /// <param name="app">The WebApplication to configure.</param>
    public static void MapPurchaseEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/cards/{cardId:guid}/purchases").WithTags("Purchases");

        /// <summary>
        /// Creates a new purchase transaction for a specified card.
        /// </summary>
        /// <remarks>
        /// <para>
        /// HTTP Method: <c>POST</c>
        /// Route: <c>/cards/{cardId}/purchases</c>
        /// </para>
        /// <para>
        /// Invokes <see cref="CreatePurchaseUseCase"/> which:
        /// <list type="bullet">
        /// <item>Validates the description (max 50 characters, non-empty)</item>
        /// <item>Validates the USD amount (must be positive)</item>
        /// <item>Verifies the card exists</item>
        /// <item>Creates the purchase through the Card aggregate</item>
        /// <item>Persists the purchase and returns the assigned ID</item>
        /// </list>
        /// </para>
        /// </remarks>
        group.MapPost("", async (
            [FromRoute] Guid cardId,
            [FromBody] CreatePurchaseRequest request,
            CreatePurchaseUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var response = await useCase.ExecuteAsync(cardId, request, cancellationToken);
            return Results.Created($"/cards/{cardId}/purchases/{response.PurchaseId}", response);
        })
        .WithName("CreatePurchase")
        .WithDescription("Create a new purchase transaction")
        .Produces<CreatePurchaseResponse>(StatusCodes.Status201Created)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        /// <summary>
        /// Retrieves a purchase transaction converted to a specified currency.
        /// </summary>
        /// <remarks>
        /// <para>
        /// HTTP Method: <c>GET</c>
        /// Route: <c>/cards/{cardId}/purchases/{purchaseId}</c>
        /// </para>
        /// <para>
        /// Query Parameters:
        /// <list type="bullet">
        /// <item><c>currencyKey</c> (required) — Treasury country_currency_desc identifier
        /// (e.g., "Australia-Dollar", "Austria-Euro"). Specifies the target currency for conversion.</item>
        /// </list>
        /// </para>
        /// <para>
        /// Invokes <see cref="GetPurchaseConvertedUseCase"/> which:
        /// <list type="bullet">
        /// <item>Queries the purchase and ensures it belongs to the specified card</item>
        /// <item>Resolves an exchange rate using cache-first strategy with upstream fallback</item>
        /// <item>Converts the USD amount to the target currency (rounded to 2 decimals)</item>
        /// <item>Returns comprehensive conversion details (original amount, rate, rate date, converted amount)</item>
        /// </list>
        /// </para>
        /// <para>
        /// Conversion Rules (per Treasury Fiscal Data):
        /// <list type="bullet">
        /// <item>Rate must have an effective date ≤ purchase transaction date</item>
        /// <item>Rate must be within 6 calendar months prior to purchase date (inclusive at boundary)</item>
        /// <item>If no suitable rate exists: HTTP 422 (FX-4220)</item>
        /// <item>If Treasury API unavailable and no cached fallback: HTTP 503 (FX-5030)</item>
        /// </list>
        /// </para>
        /// </remarks>
        group.MapGet("{purchaseId:guid}", async (
            [FromRoute] Guid cardId,
            [FromRoute] Guid purchaseId,
            [FromQuery] string currencyKey,
            GetPurchaseConvertedUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var response = await useCase.ExecuteAsync(cardId, purchaseId, currencyKey, cancellationToken);
            return Results.Ok(response);
        })
        .WithName("GetPurchaseConverted")
        .WithDescription("Get purchase with currency conversion")
        .Produces<ConvertedPurchaseResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)
        .Produces<ProblemDetails>(StatusCodes.Status503ServiceUnavailable);
    }
}

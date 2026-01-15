using CardService.Application.DTOs;
using CardService.Application.UseCases;
using Microsoft.AspNetCore.Mvc;

namespace CardService.Api.Endpoints;

/// <summary>
/// API endpoints for card management operations.
/// </summary>
/// <remarks>
/// <para>
/// This static class defines minimal API endpoints for:
/// <list type="bullet">
/// <item><strong>POST /cards</strong> — Create a new card with a credit limit</item>
/// <item><strong>GET /cards/{cardId}/balance</strong> — Retrieve available balance (optionally converted)</item>
/// </list>
/// </para>
/// <para>
/// Endpoints use ASP.NET Core's minimal APIs approach with dependency injection of use cases.
/// Exception handling is delegated to the <see cref="ExceptionHandlingMiddleware"/>.
/// </para>
/// </remarks>
public static class CardEndpoints
{
    /// <summary>
    /// Maps all card-related endpoints to the application.
    /// </summary>
    /// <remarks>
    /// This extension method is called from Program.cs to register card endpoints.
    /// Creates a route group at "/cards" tagged as "Cards" for OpenAPI documentation.
    /// </remarks>
    /// <param name="app">The WebApplication to configure.</param>
    public static void MapCardEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/cards").WithTags("Cards");

        /// <summary>
        /// Creates a new card with the specified card number and credit limit.
        /// </summary>
        /// <remarks>
        /// <para>
        /// HTTP Method: <c>POST</c>
        /// Route: <c>/cards</c>
        /// </para>
        /// <para>
        /// Invokes <see cref="CreateCardUseCase"/> which:
        /// <list type="bullet">
        /// <item>Validates the 16-digit card number</item>
        /// <item>Validates the credit limit</item>
        /// <item>Checks for duplicate card numbers</item>
        /// <item>Persists the card and returns the assigned ID</item>
        /// </list>
        /// </para>
        /// </remarks>
        group.MapPost("", async (
            [FromBody] CreateCardRequest request,
            CreateCardUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var response = await useCase.ExecuteAsync(request, cancellationToken);
            return Results.Created($"/cards/{response.CardId}", response);
        })
        .WithName("CreateCard")
        .WithDescription("Create a new card")
        .Produces<CreateCardResponse>(StatusCodes.Status201Created)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        /// <summary>
        /// Retrieves the available balance for a card.
        /// </summary>
        /// <remarks>
        /// <para>
        /// HTTP Method: <c>GET</c>
        /// Route: <c>/cards/{cardId}/balance</c>
        /// </para>
        /// <para>
        /// Query Parameters:
        /// <list type="bullet">
        /// <item><c>currencyKey</c> (optional) — Treasury country_currency_desc (e.g., "Australia-Dollar").
        /// If omitted, balance is returned in USD only.</item>
        /// <item><c>asOfDate</c> (optional) — Date for FX rate resolution (ISO date format).
        /// If omitted, defaults to current UTC date.</item>
        /// </list>
        /// </para>
        /// <para>
        /// Invokes <see cref="GetAvailableBalanceUseCase"/> which:
        /// <list type="bullet">
        /// <item>Calculates available balance = credit limit − total purchases</item>
        /// <item>If currencyKey provided, applies FX conversion using cache-first strategy</item>
        /// <item>Returns comprehensive balance information including rate details (if converted)</item>
        /// </list>
        /// </para>
        /// </remarks>
        group.MapGet("{cardId:guid}/balance", async (
            [FromRoute] Guid cardId,
            [FromQuery] string? currencyKey,
            [FromQuery] DateOnly? asOfDate,
            GetAvailableBalanceUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var response = await useCase.ExecuteAsync(cardId, currencyKey, asOfDate, cancellationToken);
            return Results.Ok(response);
        })
        .WithName("GetCardBalance")
        .WithDescription("Get card balance (with optional currency conversion)")
        .Produces<BalanceResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)
        .Produces<ProblemDetails>(StatusCodes.Status503ServiceUnavailable);
    }
}

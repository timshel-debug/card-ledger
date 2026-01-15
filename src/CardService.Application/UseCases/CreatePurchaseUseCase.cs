using CardService.Application.Common;
using CardService.Application.DTOs;
using CardService.Application.Exceptions;
using CardService.Application.Ports;
using CardService.Domain.ValueObjects;

namespace CardService.Application.UseCases;

/// <summary>
/// Use case for creating a purchase transaction associated with a card.
/// </summary>
/// <remarks>
/// <para>
/// This use case orchestrates the creation of a purchase transaction, including:
/// <list type="bullet">
/// <item>Validating the purchase description (must not be empty, max 50 characters)</item>
/// <item>Validating the purchase amount (must be positive)</item>
/// <item>Verifying the card exists and is accessible</item>
/// <item>Creating the purchase through the Card aggregate's <see cref="Domain.Entities.Card.AddPurchase"/> method</item>
/// <item>Persisting the purchase and returning the assigned purchase ID</item>
/// </list>
/// </para>
/// <para>
/// The use case enforces strong association: purchases belong to a specific card, and
/// the card aggregate ensures consistency of the relationship.
/// </para>
/// </remarks>
public class CreatePurchaseUseCase
{
    private readonly ICardRepository _cardRepository;
    private readonly IPurchaseRepository _purchaseRepository;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreatePurchaseUseCase"/> class.
    /// </summary>
    /// <param name="cardRepository">Repository for querying card data.</param>
    /// <param name="purchaseRepository">Repository for persisting purchase transactions.</param>
    /// <param name="clock">Clock abstraction for obtaining the current UTC time during purchase creation.</param>
    public CreatePurchaseUseCase(ICardRepository cardRepository, IPurchaseRepository purchaseRepository, IClock clock)
    {
        _cardRepository = cardRepository;
        _purchaseRepository = purchaseRepository;
        _clock = clock;
    }

    /// <summary>
    /// Executes the purchase creation use case with the provided card ID and request data.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Execution flow:
    /// <list type="number">
    /// <item>Validate the purchase description is non-empty and does not exceed 50 characters.</item>
    /// <item>Validate the purchase amount is positive and can be parsed to <see cref="Money"/>.</item>
    /// <item>Query the card repository to ensure the card exists.</item>
    /// <item>Invoke <see cref="Domain.Entities.Card.AddPurchase"/> on the card aggregate to create the purchase.</item>
    /// <item>Persist the purchase via the purchase repository.</item>
    /// <item>Return the assigned purchase ID.</item>
    /// </list>
    /// </para>
    /// <para>
    /// Any validation failures result in <see cref="ValidationException"/> (HTTP 400).
    /// Non-existent cards result in <see cref="ResourceNotFoundException"/> (HTTP 404).
    /// </para>
    /// </remarks>
    /// <param name="cardId">The GUID of the card to associate with the purchase.</param>
    /// <param name="request">The <see cref="CreatePurchaseRequest"/> containing description, transaction date, and amount.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>A <see cref="CreatePurchaseResponse"/> containing the newly assigned purchase ID.</returns>
    /// <exception cref="ValidationException">Thrown if the description is empty/whitespace, exceeds 50 characters,
    /// or if the amount is invalid (not positive or fails decimal parsing).</exception>
    /// <exception cref="ResourceNotFoundException">Thrown if no card with the given <paramref name="cardId"/> exists.</exception>
    /// <example>
    /// <code>
    /// var useCase = new CreatePurchaseUseCase(cardRepository, purchaseRepository, clock);
    /// var request = new CreatePurchaseRequest 
    /// { 
    ///     Description = "Grocery Store", 
    ///     TransactionDate = new DateOnly(2024, 11, 15), 
    ///     AmountUsd = 125.50 
    /// };
    /// var response = await useCase.ExecuteAsync(cardId, request);
    /// Console.WriteLine($"Purchase created with ID: {response.PurchaseId}");
    /// </code>
    /// </example>
    public async Task<CreatePurchaseResponse> ExecuteAsync(Guid cardId, CreatePurchaseRequest request, CancellationToken cancellationToken = default)
    {
        // Validate request
        if (string.IsNullOrWhiteSpace(request.Description))
            throw new ValidationException("Description cannot be empty.");

        if (request.Description.Length > 50)
            throw new ValidationException("Description cannot exceed 50 characters.");

        if (request.AmountUsd <= 0)
            throw new ValidationException("Purchase amount must be greater than zero.");

        Money amount;
        try
        {
            amount = Money.FromUsd(request.AmountUsd);
        }
        catch (ArgumentException ex)
        {
            throw new ValidationException(ex.Message);
        }

        // Check card exists
        var card = await _cardRepository.GetByIdAsync(cardId, cancellationToken);
        if (card == null)
            throw new ResourceNotFoundException($"Card with ID {cardId} not found.");

        // Create purchase
        var purchase = card.AddPurchase(request.Description, request.TransactionDate, amount, _clock.UtcNow);

        await _purchaseRepository.AddAsync(purchase, cancellationToken);
        await _purchaseRepository.SaveChangesAsync(cancellationToken);

        return new CreatePurchaseResponse(purchase.Id);
    }
}

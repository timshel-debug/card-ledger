using CardService.Application.Common;
using CardService.Application.DTOs;
using CardService.Application.Exceptions;
using CardService.Application.Ports;
using CardService.Domain.Entities;
using CardService.Domain.ValueObjects;

namespace CardService.Application.UseCases;

/// <summary>
/// Use case for creating a new card with a credit limit.
/// </summary>
/// <remarks>
/// <para>
/// This use case encapsulates the business logic for card creation, including:
/// <list type="bullet">
/// <item>Validating the card number (must be exactly 16 numeric digits)</item>
/// <item>Validating the credit limit (must be positive)</item>
/// <item>Hashing the card number for secure storage</item>
/// <item>Ensuring card number uniqueness (no duplicate cards allowed)</item>
/// <item>Creating the Card aggregate with the current timestamp</item>
/// <item>Persisting the card and returning the assigned card ID</item>
/// </list>
/// </para>
/// <para>
/// The use case depends on repositories and utilities injected via constructor,
/// following the Dependency Inversion Principle.
/// </para>
/// </remarks>
public class CreateCardUseCase
{
    private readonly ICardRepository _cardRepository;
    private readonly ICardNumberHasher _hasher;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateCardUseCase"/> class.
    /// </summary>
    /// <param name="cardRepository">Repository for persisting and querying cards.</param>
    /// <param name="hasher">Service for securely hashing card numbers (SHA-256).</param>
    /// <param name="clock">Clock abstraction for obtaining the current UTC time during card creation.</param>
    public CreateCardUseCase(ICardRepository cardRepository, ICardNumberHasher hasher, IClock clock)
    {
        _cardRepository = cardRepository;
        _hasher = hasher;
        _clock = clock;
    }

    /// <summary>
    /// Executes the card creation use case with the provided request data.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Execution flow:
    /// <list type="number">
    /// <item>Validate and parse the card number using <see cref="CardNumber.Create"/>.</item>
    /// <item>Validate the credit limit is greater than zero.</item>
    /// <item>Convert the USD credit limit to <see cref="Money"/> (cent-based storage).</item>
    /// <item>Hash the card number for secure storage.</item>
    /// <item>Check for duplicate card numbers (uniqueness constraint).</item>
    /// <item>Create the <see cref="Card"/> aggregate using the domain factory.</item>
    /// <item>Persist the card and return its assigned ID.</item>
    /// </list>
    /// </para>
    /// <para>
    /// Any validation failures result in <see cref="ValidationException"/> (HTTP 400).
    /// Duplicate card numbers result in <see cref="DuplicateResourceException"/> (HTTP 409).
    /// </para>
    /// </remarks>
    /// <param name="request">The <see cref="CreateCardRequest"/> containing card number and credit limit.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>A <see cref="CreateCardResponse"/> containing the newly assigned card ID.</returns>
    /// <exception cref="ValidationException">Thrown if the card number is invalid (not 16 digits)
    /// or if the credit limit is invalid (not positive or fails decimal parsing).</exception>
    /// <exception cref="DuplicateResourceException">Thrown if a card with the same number already exists.</exception>
    /// <example>
    /// <code>
    /// var useCase = new CreateCardUseCase(cardRepository, hasher, clock);
    /// var request = new CreateCardRequest { CardNumber = "4111111111111111", CreditLimitUsd = 5000.00 };
    /// var response = await useCase.ExecuteAsync(request);
    /// Console.WriteLine($"Card created with ID: {response.CardId}");
    /// </code>
    /// </example>
    public async Task<CreateCardResponse> ExecuteAsync(CreateCardRequest request, CancellationToken cancellationToken = default)
    {
        // Validate and parse card number
        CardNumber cardNumber;
        try
        {
            cardNumber = CardNumber.Create(request.CardNumber);
        }
        catch (ArgumentException ex)
        {
            throw new ValidationException(ex.Message);
        }

        // Validate credit limit
        if (request.CreditLimitUsd <= 0)
            throw new ValidationException("Credit limit must be greater than zero.");

        Money creditLimit;
        try
        {
            creditLimit = Money.FromUsd(request.CreditLimitUsd);
        }
        catch (ArgumentException ex)
        {
            throw new ValidationException(ex.Message);
        }

        // Hash the card number
        var cardNumberHash = _hasher.Hash(cardNumber.Value);

        // Check for duplicate
        if (await _cardRepository.ExistsByHashAsync(cardNumberHash, cancellationToken))
            throw new DuplicateResourceException("A card with this number already exists.");

        // Create card
        var card = Card.Create(cardNumber, creditLimit, cardNumberHash, _clock.UtcNow);

        await _cardRepository.AddAsync(card, cancellationToken);
        await _cardRepository.SaveChangesAsync(cancellationToken);

        return new CreateCardResponse(card.Id);
    }
}

using CardService.Application.Ports;
using CardService.Domain.Entities;
using CardService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CardService.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of the card repository port.
/// </summary>
/// <remarks>
/// <para>
/// This repository provides persistence operations for the <see cref="Card"/> aggregate root,
/// implementing the <see cref="ICardRepository"/> port interface.
/// </para>
/// <para>
/// Key responsibilities:
/// <list type="bullet">
/// <item>Query cards by ID with optional eager-loading of purchases</item>
/// <item>Check card number hash uniqueness to prevent duplicates</item>
/// <item>Persist new cards and changes via EF Core's DbContext</item>
/// </list>
/// </para>
/// </remarks>
public class CardRepository : ICardRepository
{
    private readonly AppDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="CardRepository"/> class.
    /// </summary>
    /// <param name="context">The EF Core DbContext for database access.</param>
    public CardRepository(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Retrieves a card by its ID without eagerly loading purchases.
    /// </summary>
    /// <remarks>
    /// This method queries the Cards table by primary key. Purchases are not eagerly loaded;
    /// use <see cref="GetByIdWithPurchasesAsync"/> if purchase data is needed.
    /// </remarks>
    /// <param name="cardId">The GUID of the card to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>The <see cref="Card"/> if found; otherwise <c>null</c>.</returns>
    public async Task<Card?> GetByIdAsync(Guid cardId, CancellationToken cancellationToken = default)
    {
        return await _context.Cards
            .FirstOrDefaultAsync(c => c.Id == cardId, cancellationToken);
    }

    /// <summary>
    /// Retrieves a card by its ID with eagerly loaded purchases.
    /// </summary>
    /// <remarks>
    /// This method queries the Cards table and explicitly loads the private <c>_purchases</c> collection
    /// using EF Core's Entry API. This is necessary because purchase collection is private on the Card aggregate.
    /// </remarks>
    /// <param name="cardId">The GUID of the card to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>The <see cref="Card"/> with purchases loaded if found; otherwise <c>null</c>.</returns>
    public async Task<Card?> GetByIdWithPurchasesAsync(Guid cardId, CancellationToken cancellationToken = default)
    {
        var card = await _context.Cards
            .FirstOrDefaultAsync(c => c.Id == cardId, cancellationToken);

        if (card != null)
        {
            // Load purchases separately due to private collection
            await _context.Entry(card)
                .Collection("_purchases")
                .LoadAsync(cancellationToken);
        }

        return card;
    }

    /// <summary>
    /// Checks whether a card with the specified card number hash already exists.
    /// </summary>
    /// <remarks>
    /// This method is used to detect duplicate card numbers before creating a new card,
    /// ensuring the uniqueness constraint is enforced at the application level as well
    /// as the database level.
    /// </remarks>
    /// <param name="cardNumberHash">The SHA-256 hash of the card number to check.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns><c>true</c> if a card with the given hash exists; otherwise <c>false</c>.</returns>
    public async Task<bool> ExistsByHashAsync(string cardNumberHash, CancellationToken cancellationToken = default)
    {
        return await _context.Cards
            .AnyAsync(c => c.CardNumberHash == cardNumberHash, cancellationToken);
    }

    /// <summary>
    /// Adds a new card to the context for persistence (but does not commit).
    /// </summary>
    /// <remarks>
    /// This method stages the card in the DbContext's change tracker. Changes are not persisted
    /// until <see cref="SaveChangesAsync"/> is called.
    /// </remarks>
    /// <param name="card">The <see cref="Card"/> aggregate to add.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    public async Task AddAsync(Card card, CancellationToken cancellationToken = default)
    {
        await _context.Cards.AddAsync(card, cancellationToken);
    }

    /// <summary>
    /// Commits all changes made to cards in the context to the database.
    /// </summary>
    /// <remarks>
    /// This method executes a database transaction to persist all staged changes.
    /// Should be called after all Add/Update operations are staged.
    /// </remarks>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}

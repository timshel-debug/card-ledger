using CardService.Application.Ports;
using CardService.Domain.Entities;
using CardService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CardService.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of the purchase repository port.
/// </summary>
/// <remarks>
/// <para>
/// This repository provides persistence operations for <see cref="PurchaseTransaction"/> entities,
/// implementing the <see cref="IPurchaseRepository"/> port interface.
/// </para>
/// <para>
/// Key responsibilities:
/// <list type="bullet">
/// <item>Query purchases by ID, with optional card ID filtering for data isolation</item>
/// <item>Aggregate purchases for a specific card</item>
/// <item>Persist new purchases and changes via EF Core's DbContext</item>
/// </list>
/// </para>
/// </remarks>
public class PurchaseRepository : IPurchaseRepository
{
    private readonly AppDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="PurchaseRepository"/> class.
    /// </summary>
    /// <param name="context">The EF Core DbContext for database access.</param>
    public PurchaseRepository(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Retrieves a purchase by its ID without verifying card ownership.
    /// </summary>
    /// <remarks>
    /// This method queries the Purchases table by primary key only. For secure retrieval that
    /// verifies ownership, use <see cref="GetByCardAndPurchaseIdAsync"/> instead.
    /// </remarks>
    /// <param name="purchaseId">The GUID of the purchase to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>The <see cref="PurchaseTransaction"/> if found; otherwise <c>null</c>.</returns>
    public async Task<PurchaseTransaction?> GetByIdAsync(Guid purchaseId, CancellationToken cancellationToken = default)
    {
        return await _context.Purchases
            .FirstOrDefaultAsync(p => p.Id == purchaseId, cancellationToken);
    }

    /// <summary>
    /// Retrieves a purchase by ID with verification that it belongs to the specified card.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method enforces data isolation by verifying both the purchase ID and card ID match.
    /// This prevents a user from accessing purchases that don't belong to their card.
    /// </para>
    /// <para>
    /// Always use this method when handling user requests to ensure proper authorization.
    /// </para>
    /// </remarks>
    /// <param name="cardId">The GUID of the card that should own the purchase.</param>
    /// <param name="purchaseId">The GUID of the purchase to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>The <see cref="PurchaseTransaction"/> if found and belongs to the card; otherwise <c>null</c>.</returns>
    public async Task<PurchaseTransaction?> GetByCardAndPurchaseIdAsync(Guid cardId, Guid purchaseId, CancellationToken cancellationToken = default)
    {
        return await _context.Purchases
            .FirstOrDefaultAsync(p => p.Id == purchaseId && p.CardId == cardId, cancellationToken);
    }

    /// <summary>
    /// Retrieves all purchases associated with a specific card.
    /// </summary>
    /// <remarks>
    /// This method aggregates all purchases for a card, enabling balance calculation and
    /// purchase history queries.
    /// </remarks>
    /// <param name="cardId">The GUID of the card whose purchases should be retrieved.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>A read-only list of <see cref="PurchaseTransaction"/> entities for the card.
    /// Returns an empty list if the card has no purchases.</returns>
    public async Task<IReadOnlyList<PurchaseTransaction>> GetByCardIdAsync(Guid cardId, CancellationToken cancellationToken = default)
    {
        return await _context.Purchases
            .Where(p => p.CardId == cardId)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Adds a new purchase to the context for persistence (but does not commit).
    /// </summary>
    /// <remarks>
    /// This method stages the purchase in the DbContext's change tracker. Changes are not persisted
    /// until <see cref="SaveChangesAsync"/> is called.
    /// </remarks>
    /// <param name="purchase">The <see cref="PurchaseTransaction"/> entity to add.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    public async Task AddAsync(PurchaseTransaction purchase, CancellationToken cancellationToken = default)
    {
        await _context.Purchases.AddAsync(purchase, cancellationToken);
    }

    /// <summary>
    /// Commits all changes made to purchases in the context to the database.
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

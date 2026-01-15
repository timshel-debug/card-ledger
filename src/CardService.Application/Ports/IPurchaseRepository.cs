using CardService.Domain.Entities;

namespace CardService.Application.Ports;

/// <summary>
/// Port (interface) for persisting and retrieving <see cref="PurchaseTransaction"/> entities.
/// </summary>
/// <remarks>
/// <para>Implementations of this port handle all database operations for purchase transactions.</para>
/// <para>This interface abstracts the persistence mechanism from the application logic.</para>
/// </remarks>
public interface IPurchaseRepository
{
    /// <summary>
    /// Retrieves a purchase transaction by its unique identifier.
    /// </summary>
    /// <param name="purchaseId">The unique identifier of the purchase to retrieve.</param>
    /// <param name="cancellationToken">A cancellation token to support async cancellation.</param>
    /// <returns>The <see cref="PurchaseTransaction"/> if found; otherwise <see langword="null"/>.</returns>
    Task<PurchaseTransaction?> GetByIdAsync(Guid purchaseId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Retrieves a purchase transaction by both its ID and the card ID it belongs to.
    /// </summary>
    /// <param name="cardId">The unique identifier of the card that owns the purchase.</param>
    /// <param name="purchaseId">The unique identifier of the purchase to retrieve.</param>
    /// <param name="cancellationToken">A cancellation token to support async cancellation.</param>
    /// <returns>The <see cref="PurchaseTransaction"/> if found and belongs to the specified card; otherwise <see langword="null"/>.</returns>
    /// <remarks>This method enforces data isolation by ensuring the purchase belongs to the specified card.</remarks>
    Task<PurchaseTransaction?> GetByCardAndPurchaseIdAsync(Guid cardId, Guid purchaseId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Adds a new purchase transaction to the repository (marks it for insertion).
    /// </summary>
    /// <param name="purchase">The <see cref="PurchaseTransaction"/> entity to add.</param>
    /// <param name="cancellationToken">A cancellation token to support async cancellation.</param>
    /// <remarks>The changes are not persisted until <see cref="SaveChangesAsync"/> is called.</remarks>
    Task AddAsync(PurchaseTransaction purchase, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Retrieves all purchase transactions for a specified card.
    /// </summary>
    /// <param name="cardId">The unique identifier of the card.</param>
    /// <param name="cancellationToken">A cancellation token to support async cancellation.</param>
    /// <returns>A read-only collection of all purchases for the card, ordered by transaction date descending.</returns>
    Task<IReadOnlyList<PurchaseTransaction>> GetByCardIdAsync(Guid cardId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Persists all pending changes (inserts, updates, deletes) to the database.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to support async cancellation.</param>
    /// <remarks>This method is typically called after adding or modifying purchases.</remarks>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

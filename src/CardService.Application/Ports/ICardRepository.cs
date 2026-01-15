using CardService.Domain.Entities;

namespace CardService.Application.Ports;

/// <summary>
/// Port (interface) for persisting and retrieving <see cref="Card"/> aggregate roots.
/// </summary>
/// <remarks>
/// <para>Implementations of this port are responsible for all database operations related to cards.</para>
/// <para>This interface abstracts the persistence mechanism, allowing the application layer to remain technology-agnostic.</para>
/// </remarks>
public interface ICardRepository
{
    /// <summary>
    /// Retrieves a card by its unique identifier.
    /// </summary>
    /// <param name="cardId">The unique identifier of the card to retrieve.</param>
    /// <param name="cancellationToken">A cancellation token to support async cancellation.</param>
    /// <returns>The <see cref="Card"/> if found; otherwise <see langword="null"/>.</returns>
    /// <remarks>This method does not load the card's purchases collection.</remarks>
    Task<Card?> GetByIdAsync(Guid cardId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Retrieves a card by its unique identifier, including all associated purchase transactions.
    /// </summary>
    /// <param name="cardId">The unique identifier of the card to retrieve.</param>
    /// <param name="cancellationToken">A cancellation token to support async cancellation.</param>
    /// <returns>The <see cref="Card"/> with all purchases loaded if found; otherwise <see langword="null"/>.</returns>
    /// <remarks>Use this method when you need to compute the available balance or iterate over purchases.</remarks>
    Task<Card?> GetByIdWithPurchasesAsync(Guid cardId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks whether a card with the specified number hash already exists.
    /// </summary>
    /// <param name="cardNumberHash">The SHA-256 hash of the card number to check.</param>
    /// <param name="cancellationToken">A cancellation token to support async cancellation.</param>
    /// <returns><see langword="true"/> if a card with the given hash exists; otherwise <see langword="false"/>.</returns>
    /// <remarks>Used to enforce uniqueness of card numbers and prevent duplicates.</remarks>
    Task<bool> ExistsByHashAsync(string cardNumberHash, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Adds a new card to the repository (marks it for insertion).
    /// </summary>
    /// <param name="card">The <see cref="Card"/> aggregate root to add.</param>
    /// <param name="cancellationToken">A cancellation token to support async cancellation.</param>
    /// <remarks>The changes are not persisted until <see cref="SaveChangesAsync"/> is called.</remarks>
    Task AddAsync(Card card, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Persists all pending changes (inserts, updates, deletes) to the database.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to support async cancellation.</param>
    /// <remarks>This method is typically called after adding or modifying cards.</remarks>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

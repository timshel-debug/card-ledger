using CardService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CardService.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext for the Card Service application.
/// </summary>
/// <remarks>
/// <para>
/// This context encapsulates the database schema and configuration for all aggregates and entities:
/// <list type="bullet">
/// <item><see cref="Card"/> - Credit card aggregate root</item>
/// <item><see cref="PurchaseTransaction"/> - Purchase transaction entity owned by cards</item>
/// <item><see cref="FxRateCacheEntry"/> - Foreign exchange rate cache entries</item>
/// </list>
/// </para>
/// <para>
/// Configuration is applied through the EF Core Configuration classes in the Persistence/Configurations directory,
/// which define table names, column mappings, indexes, and constraints for each entity type.
/// </para>
/// <para>
/// The context uses SQLite as the backing database for zero-install, portable deployment.
/// </para>
/// </remarks>
public class AppDbContext : DbContext
{
    /// <summary>
    /// Gets the DbSet for <see cref="Card"/> entities.
    /// </summary>
    /// <remarks>
    /// This DbSet provides queryable access to all cards in the database.
    /// </remarks>
    public DbSet<Card> Cards => Set<Card>();

    /// <summary>
    /// Gets the DbSet for <see cref="PurchaseTransaction"/> entities.
    /// </summary>
    /// <remarks>
    /// This DbSet provides queryable access to all purchase transactions in the database.
    /// </remarks>
    public DbSet<PurchaseTransaction> Purchases => Set<PurchaseTransaction>();

    /// <summary>
    /// Gets the DbSet for <see cref="FxRateCacheEntry"/> entities.
    /// </summary>
    /// <remarks>
    /// This DbSet provides queryable access to all cached foreign exchange rates.
    /// </remarks>
    public DbSet<FxRateCacheEntry> FxRateCache => Set<FxRateCacheEntry>();

    /// <summary>
    /// Initializes a new instance of the <see cref="AppDbContext"/> class.
    /// </summary>
    /// <param name="options">EF Core context options configured by the DI container.</param>
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// Configures the database schema and mappings for all entity types.
    /// </summary>
    /// <remarks>
    /// This method applies all entity configurations from the Persistence/Configurations assembly,
    /// delegating schema definition to configuration classes for maintainability.
    /// </remarks>
    /// <param name="modelBuilder">The EF Core model builder.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all IEntityTypeConfiguration implementations from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}

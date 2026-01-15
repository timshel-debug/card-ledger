using CardService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CardService.Infrastructure.Persistence.Configurations;

public class FxRateCacheEntryConfiguration : IEntityTypeConfiguration<FxRateCacheEntry>
{
    public void Configure(EntityTypeBuilder<FxRateCacheEntry> builder)
    {
        builder.ToTable("fx_rate_cache");

        builder.HasKey(e => new { e.CurrencyKey, e.RecordDate });

        builder.Property(e => e.CurrencyKey)
            .HasColumnName("currency_key")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.RecordDate)
            .HasColumnName("record_date")
            .IsRequired();

        builder.Property(e => e.ExchangeRate)
            .HasColumnName("exchange_rate")
            .HasColumnType("NUMERIC")
            .IsRequired();

        builder.Property(e => e.CachedUtc)
            .HasColumnName("cached_utc")
            .IsRequired();

        builder.HasIndex(e => new { e.CurrencyKey, e.RecordDate })
            .IsDescending(false, true); // Currency ascending, Date descending
    }
}

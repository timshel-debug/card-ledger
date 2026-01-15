using CardService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CardService.Infrastructure.Persistence.Configurations;

public class PurchaseTransactionConfiguration : IEntityTypeConfiguration<PurchaseTransaction>
{
    public void Configure(EntityTypeBuilder<PurchaseTransaction> builder)
    {
        builder.ToTable("purchases");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(p => p.CardId)
            .HasColumnName("card_id")
            .IsRequired();

        builder.Property(p => p.Description)
            .HasColumnName("description")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(p => p.TransactionDate)
            .HasColumnName("transaction_date")
            .IsRequired();

        builder.Property(p => p.AmountCents)
            .HasColumnName("amount_cents")
            .IsRequired();

        builder.Property(p => p.CreatedUtc)
            .HasColumnName("created_utc")
            .IsRequired();

        builder.HasIndex(p => new { p.CardId, p.TransactionDate });
        builder.HasIndex(p => p.CardId);
    }
}

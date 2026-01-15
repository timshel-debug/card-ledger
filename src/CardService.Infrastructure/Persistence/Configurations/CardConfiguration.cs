using CardService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CardService.Infrastructure.Persistence.Configurations;

public class CardConfiguration : IEntityTypeConfiguration<Card>
{
    public void Configure(EntityTypeBuilder<Card> builder)
    {
        builder.ToTable("cards");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(c => c.CardNumberHash)
            .HasColumnName("card_number_hash")
            .HasMaxLength(64)
            .IsRequired();

        builder.HasIndex(c => c.CardNumberHash)
            .IsUnique();

        builder.Property(c => c.Last4)
            .HasColumnName("last4")
            .HasMaxLength(4)
            .IsRequired();

        builder.Property(c => c.CreditLimitCents)
            .HasColumnName("credit_limit_cents")
            .IsRequired();

        builder.Property(c => c.CreatedUtc)
            .HasColumnName("created_utc")
            .IsRequired();

        builder.Ignore(c => c.Purchases);
    }
}

using LogVault.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LogVault.Infrastructure.Data.Configurations;

public class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
{
    public void Configure(EntityTypeBuilder<ApiKey> builder)
    {
        builder.HasKey(k => k.Id);
        builder.Property(k => k.KeyHash).IsRequired();
        builder.Property(k => k.Label).IsRequired();

        builder.HasIndex(k => k.KeyHash).IsUnique();
        builder.HasIndex(k => new { k.IsEnabled, k.IsRevoked, k.ExpiresAt });

        builder.HasOne(k => k.RotatedFrom)
            .WithMany()
            .HasForeignKey(k => k.RotatedFromId)
            .OnDelete(DeleteBehavior.NoAction)
            .IsRequired(false);

        builder.Ignore(k => k.IsExpired);
        builder.Ignore(k => k.IsUsable);
    }
}

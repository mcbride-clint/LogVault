using LogVault.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LogVault.Infrastructure.Data.Configurations;

public class SavedFilterConfiguration : IEntityTypeConfiguration<SavedFilter>
{
    public void Configure(EntityTypeBuilder<SavedFilter> builder)
    {
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Name).IsRequired().HasMaxLength(200);
        builder.Property(f => f.OwnerId).IsRequired().HasMaxLength(256);
        builder.Property(f => f.FilterJson).IsRequired();
        builder.HasIndex(f => f.OwnerId);
    }
}

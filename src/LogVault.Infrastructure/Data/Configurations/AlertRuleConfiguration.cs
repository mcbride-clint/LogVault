using LogVault.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LogVault.Infrastructure.Data.Configurations;

public class AlertRuleConfiguration : IEntityTypeConfiguration<AlertRule>
{
    public void Configure(EntityTypeBuilder<AlertRule> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Name).IsRequired();
        builder.Property(r => r.OwnerId).IsRequired();
        builder.Property(r => r.FilterExpression).IsRequired();

        builder.HasIndex(r => r.OwnerId);

        builder.HasMany(r => r.Recipients)
            .WithOne(rec => rec.AlertRule)
            .HasForeignKey(rec => rec.AlertRuleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

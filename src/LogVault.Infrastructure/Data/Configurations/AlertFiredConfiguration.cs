using LogVault.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LogVault.Infrastructure.Data.Configurations;

public class AlertFiredConfiguration : IEntityTypeConfiguration<AlertFired>
{
    public void Configure(EntityTypeBuilder<AlertFired> builder)
    {
        builder.HasKey(f => f.Id);

        builder.HasOne(f => f.AlertRule)
            .WithMany()
            .HasForeignKey(f => f.AlertRuleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(f => f.AlertRuleId);
        builder.HasIndex(f => f.FiredAt);
    }
}

using LogVault.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LogVault.Infrastructure.Data.Configurations;

public class DashboardConfiguration : IEntityTypeConfiguration<Dashboard>
{
    public void Configure(EntityTypeBuilder<Dashboard> builder)
    {
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Name).IsRequired().HasMaxLength(200);
        builder.Property(d => d.OwnerId).IsRequired().HasMaxLength(256);
        builder.HasIndex(d => d.OwnerId);
        builder.HasMany(d => d.Widgets)
               .WithOne(w => w.Dashboard)
               .HasForeignKey(w => w.DashboardId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}

public class DashboardWidgetConfiguration : IEntityTypeConfiguration<DashboardWidget>
{
    public void Configure(EntityTypeBuilder<DashboardWidget> builder)
    {
        builder.HasKey(w => w.Id);
        builder.Property(w => w.WidgetType).IsRequired().HasMaxLength(50);
        builder.Property(w => w.Title).IsRequired().HasMaxLength(200);
        builder.Property(w => w.ConfigJson).IsRequired();
    }
}

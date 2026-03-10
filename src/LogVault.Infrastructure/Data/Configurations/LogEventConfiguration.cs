using LogVault.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LogVault.Infrastructure.Data.Configurations;

public class LogEventConfiguration : IEntityTypeConfiguration<LogEvent>
{
    public void Configure(EntityTypeBuilder<LogEvent> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.MessageTemplate).IsRequired();
        builder.Property(e => e.RenderedMessage).IsRequired();
        builder.Property(e => e.PropertiesJson).IsRequired().HasColumnType("TEXT");

        builder.HasIndex(e => new { e.Timestamp, e.Level });
        builder.HasIndex(e => e.SourceApplication);
        builder.HasIndex(e => e.TraceId);
        builder.HasIndex(e => e.IngestedAt);
    }
}

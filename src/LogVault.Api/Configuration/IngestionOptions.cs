using System.ComponentModel.DataAnnotations;

namespace LogVault.Api.Configuration;

public class IngestionOptions
{
    public const string Section = "Ingestion";

    [Range(1, 10000, ErrorMessage = "Ingestion:MaxBatchSize must be between 1 and 10000.")]
    public int MaxBatchSize { get; set; } = 500;

    [Range(50, 60000, ErrorMessage = "Ingestion:FlushIntervalMs must be between 50 and 60000.")]
    public int FlushIntervalMs { get; set; } = 500;

    [Range(100, 100000, ErrorMessage = "Ingestion:ChannelCapacity must be between 100 and 100000.")]
    public int ChannelCapacity { get; set; } = 1000;
}

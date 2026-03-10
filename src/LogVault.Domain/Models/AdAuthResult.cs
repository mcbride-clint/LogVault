namespace LogVault.Domain.Models;

public record AdAuthResult(bool Success, string? DisplayName, string? Email, string? ErrorMessage);

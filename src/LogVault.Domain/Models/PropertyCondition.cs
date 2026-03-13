namespace LogVault.Domain.Models;

public record PropertyCondition(string Key, string Value, PropertyFilterOp Op);

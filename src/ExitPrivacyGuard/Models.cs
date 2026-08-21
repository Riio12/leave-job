using System.ComponentModel;

namespace ExitPrivacyGuard;

public enum PrivacyLevel { 低 = 1, 中 = 2, 高 = 3, 严重 = 4 }

public sealed class ScanResult
{
    public bool Selected { get; set; }
    public required string FileName { get; init; }
    public required string FullPath { get; init; }
    public required string Category { get; init; }
    public required PrivacyLevel Level { get; init; }
    public required int Relevance { get; init; }
    public required string Reason { get; init; }
    public long Size { get; init; }
    public DateTime Modified { get; init; }
}

public sealed class ScanOptions
{
    public required string RootPath { get; init; }
    public required string[] Keywords { get; init; }
    public bool SearchContents { get; init; }
    public bool PrivacyCorrelation { get; init; }
    public long MaxContentBytes { get; init; } = 20 * 1024 * 1024;
}

public sealed class AuditEntry
{
    public DateTime Time { get; init; } = DateTime.Now;
    public required string Action { get; init; }
    public required string Path { get; init; }
    public required string Result { get; init; }
    public string? Sha256 { get; init; }
}

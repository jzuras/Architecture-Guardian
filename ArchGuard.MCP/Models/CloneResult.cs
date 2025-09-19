namespace ArchGuard.MCP.Models;

public class CloneResult 
{
    public string WindowsPath { get; init; } = string.Empty;
    public string WslPath { get; init; } = string.Empty;
    public string CommitSha { get; init; } = string.Empty;
    public bool Success { get; init; }
    public string ErrorMessage { get; init; } = string.Empty;
    public DateTime ClonedAt { get; init; }
}
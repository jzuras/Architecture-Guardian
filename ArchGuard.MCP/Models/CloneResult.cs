namespace ArchGuard.MCP.Models;

public class CloneResult 
{
    public string LocalPath { get; init; } = string.Empty;
    public string AgentPath { get; init; } = string.Empty;
    public string CommitSha { get; init; } = string.Empty;
    public bool Success { get; init; }
    public string ErrorMessage { get; init; } = string.Empty;
    public DateTime ClonedAt { get; init; }
}
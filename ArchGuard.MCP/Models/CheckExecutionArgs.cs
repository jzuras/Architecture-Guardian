namespace ArchGuard.MCP.Models;

public class CheckExecutionArgs
{
    public string RepoOwner { get; set; } = string.Empty;
    public string RepoName { get; set; } = string.Empty;
    public string CommitSha { get; set; } = string.Empty;
    public string CheckName { get; set; } = string.Empty;
    public long InstallationId { get; set; }

    // If provided, update this existing check run; otherwise, create a new one
    public long? ExistingCheckRunId { get; set; }

    // For initial creation, the title/summary of the first status
    public string InitialTitle { get; set; } = string.Empty;
    public string InitialSummary { get; set; } = string.Empty;
}
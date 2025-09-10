using ArchGuard.MCP.Models;

namespace ArchGuard.MCP.Services;

public interface IRepositoryCloneService
{
    Task<CloneResult> CloneRepositoryAsync(string cloneUrl, string commitOrBranch, string repoFullName);
    Task CleanupRepositoryAsync(string clonePath);
}
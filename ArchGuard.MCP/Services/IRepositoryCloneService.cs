using ArchGuard.MCP.Models;
using ArchGuard.Shared;

namespace ArchGuard.MCP.Services;

public interface IRepositoryCloneService
{
    Task<ContextFile[]> GetContextFilesAsync(string commitOrBranch, string repoFullName);
    Task<CloneResult> CloneRepositoryAsync(string cloneUrl, string commitOrBranch, string repoFullName);
    Task CleanupRepositoryAsync(string clonePath);
}
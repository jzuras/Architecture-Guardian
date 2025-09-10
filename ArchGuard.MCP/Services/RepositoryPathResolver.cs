using ArchGuard.MCP.Models;

namespace ArchGuard.MCP.Services;

public interface IRepositoryPathResolver
{
    Task<string> GetRootFromWebhookAsync(GitHubPushWebhookPayload payload, long installationId);
    Task<string> GetRootFromWebhookAsync(GitHubPullRequestWebhookPayload payload, long installationId);
    Task<string> GetRootFromWebhookAsync(GitHubCheckRunWebhookPayload payload, long installationId);
    Task<string> GetRootFromWebhookAsync(GitHubCheckSuiteWebhookPayload payload, long installationId);
    string GetRootFromRepo(string repoFullName);
}

public class RepositoryPathResolver : IRepositoryPathResolver
{
    private IRepositoryCloneService CloneService { get; set; }
    private ILogger<RepositoryPathResolver> Logger { get; set; }

    public RepositoryPathResolver(IRepositoryCloneService cloneService, ILogger<RepositoryPathResolver> logger)
    {
        this.CloneService = cloneService;
        this.Logger = logger;
    }

    public async Task<string> GetRootFromWebhookAsync(GitHubPushWebhookPayload payload, long installationId)
    {
        try
        {
            var cloneResult = await this.CloneService.CloneRepositoryAsync(
                payload.Repository.CloneUrl, 
                payload.After, 
                payload.Repository.FullName);

            if (!cloneResult.Success)
            {
                this.Logger.LogError("Failed to clone repository {RepoFullName}: {ErrorMessage}", 
                    payload.Repository.FullName, cloneResult.ErrorMessage);
                throw new InvalidOperationException($"Failed to clone repository: {cloneResult.ErrorMessage}");
            }

            return cloneResult.AgentPath;
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error resolving path for push webhook on {RepoFullName}", 
                payload.Repository.FullName);
            throw;
        }
    }

    public async Task<string> GetRootFromWebhookAsync(GitHubPullRequestWebhookPayload payload, long installationId)
    {
        try
        {
            var cloneResult = await this.CloneService.CloneRepositoryAsync(
                payload.PullRequest.Head.Repo.CloneUrl,
                payload.PullRequest.Head.Sha,
                payload.PullRequest.Head.Repo.FullName);

            if (!cloneResult.Success)
            {
                this.Logger.LogError("Failed to clone repository {RepoFullName}: {ErrorMessage}", 
                    payload.PullRequest.Head.Repo.FullName, cloneResult.ErrorMessage);
                throw new InvalidOperationException($"Failed to clone repository: {cloneResult.ErrorMessage}");
            }

            return cloneResult.AgentPath;
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error resolving path for pull request webhook on {RepoFullName}", 
                payload.PullRequest.Head.Repo.FullName);
            throw;
        }
    }

    public async Task<string> GetRootFromWebhookAsync(GitHubCheckRunWebhookPayload payload, long installationId)
    {
        try
        {
            var cloneResult = await this.CloneService.CloneRepositoryAsync(
                payload.Repository.CloneUrl,
                payload.CheckRun.HeadSha,
                payload.Repository.FullName);

            if (!cloneResult.Success)
            {
                this.Logger.LogError("Failed to clone repository {RepoFullName}: {ErrorMessage}", 
                    payload.Repository.FullName, cloneResult.ErrorMessage);
                throw new InvalidOperationException($"Failed to clone repository: {cloneResult.ErrorMessage}");
            }

            return cloneResult.AgentPath;
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error resolving path for check run webhook on {RepoFullName}", 
                payload.Repository.FullName);
            throw;
        }
    }

    public async Task<string> GetRootFromWebhookAsync(GitHubCheckSuiteWebhookPayload payload, long installationId)
    {
        try
        {
            var cloneResult = await this.CloneService.CloneRepositoryAsync(
                payload.Repository.CloneUrl,
                payload.CheckSuite.HeadSha,
                payload.Repository.FullName);

            if (!cloneResult.Success)
            {
                this.Logger.LogError("Failed to clone repository {RepoFullName}: {ErrorMessage}", 
                    payload.Repository.FullName, cloneResult.ErrorMessage);
                throw new InvalidOperationException($"Failed to clone repository: {cloneResult.ErrorMessage}");
            }

            return cloneResult.AgentPath;
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error resolving path for check suite webhook on {RepoFullName}", 
                payload.Repository.FullName);
            throw;
        }
    }

    public string GetRootFromRepo(string repoFullName)
    {
        if (repoFullName.Contains("RulesDemo", StringComparison.OrdinalIgnoreCase))
        {
            return "/mnt/c/Users/Jim/source/DevChecks/RulesDemo";
        }

        return "unknown repo";
    }
}
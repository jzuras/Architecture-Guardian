using ArchGuard.MCP.Models;
using Octokit;
using System.Text.Json;

namespace ArchGuard.MCP.Services.WebhookHandlers;

public class PullRequestWebhookHandler : WebhookHandlerBase
{
    public override string EventType => "pull_request";
    
    private ILogger<PullRequestWebhookHandler> Logger { get; set; }
    private IRepositoryCloneService CloneService { get; set; }
    private IConfiguration Configuration { get; set; }

    public PullRequestWebhookHandler(
        GitHubCheckService checkService,
        GitHubAppAuthService authService,
        IGitHubClient githubClient,
        IRepositoryPathResolver pathResolver,
        IRepositoryCloneService cloneService,
        IConfiguration configuration,
        ILogger<PullRequestWebhookHandler> logger)
        : base(checkService, authService, githubClient, pathResolver)
    {
        this.Logger = logger;
        this.CloneService = cloneService;
        this.Configuration = configuration;
    }

    public override async Task<IResult> HandleAsync(string requestBody, long installationId, string deliveryId)
    {
        try
        {
            var pullRequestPayload = JsonSerializer.Deserialize<GitHubPullRequestWebhookPayload>(requestBody);

            if (pullRequestPayload?.PullRequest is null)
            {
                this.Logger.LogWarning("Invalid pull request payload structure for delivery {DeliveryId}", deliveryId);
                return Results.BadRequest("Invalid pull request event payload structure");
            }

            this.Logger.LogInformation("Pull Request event: Action '{Action}', PR #{Number}, Title: '{Title}'",
                pullRequestPayload.Action, pullRequestPayload.Number, pullRequestPayload.PullRequest.Title);

            // Authenticate with GitHub
            var installationToken = await AuthService.GetInstallationTokenAsync(installationId);
            GitHubClient.Connection.Credentials = new Credentials(installationToken, AuthenticationType.Bearer);

            // Get repository root path via cloning
            var root = await PathResolver.GetRootFromWebhookAsync(pullRequestPayload, installationId);

            // Prepare check execution arguments
            var checkArgs = new CheckExecutionArgs
            {
                RepoOwner = pullRequestPayload.Repository.Owner.Login,
                RepoName = pullRequestPayload.Repository.Name,
                CommitSha = pullRequestPayload.PullRequest.Head.Sha,
                CheckName = GitHubCheckService.DependencyRegistrationCheckName,
                InstallationId = installationId,
                ExistingCheckRunId = null,
                InitialTitle = GitHubCheckService.DependencyRegistrationCheckName,
                InitialSummary = "Starting DI registration validation for this pull request."
            };

            // Fire-and-forget: Intentionally not awaited to allow immediate return
            // The Task.Run executes async Octokit operations in background
            _ = Task.Run(async () => 
            {
                try
                {
                    await CheckService.ExecuteDepInjectionCheckAsync(checkArgs, root, installationId, GitHubClient);
                    
                    // Clean up repository after validation if configured to do so
                    var cleanupAfterValidation = this.Configuration.GetValue("RepositoryCloning:CleanupAfterValidation", true);
                    if (cleanupAfterValidation)
                    {
                        this.Logger.LogInformation("Cleaning up repository after validation: {RepoFullName} at {CommitSha}", 
                            pullRequestPayload.Repository.FullName, pullRequestPayload.PullRequest.Head.Sha);
                        
                        // We need to get the LocalPath from the clone result, but PathResolver only returns AgentPath
                        // For now, we'll reconstruct the path based on the known pattern
                        var tempBasePath = Path.Combine(Path.GetTempPath(), "archguard-clones");
                        var sanitizedRepoName = pullRequestPayload.Repository.FullName.Replace('/', '-').Replace('\\', '-');
                        var shortCommitSha = pullRequestPayload.PullRequest.Head.Sha.Length > 8 ? pullRequestPayload.PullRequest.Head.Sha.Substring(0, 8) : pullRequestPayload.PullRequest.Head.Sha;
                        var expectedPath = Path.Combine(tempBasePath, $"{sanitizedRepoName}-{shortCommitSha}");
                        
                        if (Directory.Exists(expectedPath))
                        {
                            await this.CloneService.CleanupRepositoryAsync(expectedPath);
                        }
                    }
                }
                catch (Exception ex)
                {
                    this.Logger.LogError(ex, "Error during validation or cleanup for {RepoFullName}", pullRequestPayload.Repository.FullName);
                }
            });

            return Results.Accepted("Pull request checks initiated");
        }
        catch (JsonException ex)
        {
            this.Logger.LogError(ex, "Failed to parse pull request JSON payload for delivery {DeliveryId}", deliveryId);
            return Results.BadRequest("Invalid JSON payload for pull request event");
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error processing pull request webhook for delivery {DeliveryId}", deliveryId);
            throw;
        }
    }

}
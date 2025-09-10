using ArchGuard.MCP.Models;
using Octokit;
using System.Text.Json;

namespace ArchGuard.MCP.Services.WebhookHandlers;

public class PushWebhookHandler : WebhookHandlerBase
{
    public override string EventType => "push";
    
    private ILogger<PushWebhookHandler> Logger { get; set; }
    private IRepositoryCloneService CloneService { get; set; }
    private IConfiguration Configuration { get; set; }

    #region Constructor

    public PushWebhookHandler(
        GitHubCheckService checkService,
        GitHubAppAuthService authService,
        IGitHubClient githubClient,
        IRepositoryPathResolver pathResolver,
        IRepositoryCloneService cloneService,
        IConfiguration configuration,
        ILogger<PushWebhookHandler> logger)
        : base(checkService, authService, githubClient, pathResolver)
    {
        this.Logger = logger;
        this.CloneService = cloneService;
        this.Configuration = configuration;
    }

    #endregion

    #region Public Methods

    public override async Task<IResult> HandleAsync(string requestBody, long installationId, string deliveryId)
    {
        try
        {
            var pushPayload = JsonSerializer.Deserialize<GitHubPushWebhookPayload>(requestBody);

            if (pushPayload is null)
            {
                this.Logger.LogError("Failed to deserialize push payload for delivery {DeliveryId}, installation {InstallationId}", 
                    deliveryId, installationId);
                return Results.BadRequest("Invalid push event payload structure");
            }

            this.Logger.LogInformation("Push event: Repo {RepoFullName}, Branch {Branch}, Commit {HeadCommitId}, Message: {HeadCommitMessage}",
                pushPayload.Repository.FullName,
                pushPayload.Ref,
                pushPayload.After,
                pushPayload.HeadCommit.Message);

            // Log commit details
            LogCommitDetails(pushPayload);

            // Authenticate with GitHub
            var installationToken = await AuthService.GetInstallationTokenAsync(installationId);
            GitHubClient.Connection.Credentials = new Credentials(installationToken, AuthenticationType.Bearer);

            // Get repository root path via cloning
            var root = await PathResolver.GetRootFromWebhookAsync(pushPayload, installationId);

            // Prepare check execution arguments
            var checkArgs = new CheckExecutionArgs
            {
                RepoOwner = pushPayload.Repository.Owner.Login,
                RepoName = pushPayload.Repository.Name,
                CommitSha = pushPayload.After,
                CheckName = GitHubCheckService.DependencyRegistrationCheckName,
                InstallationId = installationId,
                ExistingCheckRunId = null,
                InitialTitle = GitHubCheckService.DependencyRegistrationCheckName,
                InitialSummary = "Starting DI registration validation for this push."
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
                            pushPayload.Repository.FullName, pushPayload.After);
                        
                        // We need to get the LocalPath from the clone result, but PathResolver only returns AgentPath
                        // For now, we'll reconstruct the path based on the known pattern
                        var tempBasePath = Path.Combine(Path.GetTempPath(), "archguard-clones");
                        var sanitizedRepoName = pushPayload.Repository.FullName.Replace('/', '-').Replace('\\', '-');
                        var shortCommitSha = pushPayload.After.Length > 8 ? pushPayload.After.Substring(0, 8) : pushPayload.After;
                        var expectedPath = Path.Combine(tempBasePath, $"{sanitizedRepoName}-{shortCommitSha}");
                        
                        if (Directory.Exists(expectedPath))
                        {
                            await this.CloneService.CleanupRepositoryAsync(expectedPath);
                        }
                    }
                }
                catch (Exception ex)
                {
                    this.Logger.LogError(ex, "Error during validation or cleanup for {RepoFullName}", pushPayload.Repository.FullName);
                }
            });

            return Results.Accepted("Push event check initiated");
        }
        catch (JsonException ex)
        {
            this.Logger.LogError(ex, "Failed to parse push JSON payload for delivery {DeliveryId}, installation {InstallationId}", 
                deliveryId, installationId);
            return Results.BadRequest("Invalid JSON payload for push event");
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error processing push webhook for delivery {DeliveryId}, installation {InstallationId}", 
                deliveryId, installationId);
            throw;
        }
    }

    #endregion

    #region Private Methods

    private void LogCommitDetails(GitHubPushWebhookPayload pushPayload)
    {
        if (pushPayload.Commits?.Any() is true)
        {
            foreach (var commit in pushPayload.Commits)
            {
                this.Logger.LogInformation("  Commit SHA: {CommitId}, Message: {CommitMessage}", 
                    commit.Id.Substring(0, 7), commit.Message);

                LogFileChanges("Added", commit.Added);
                LogFileChanges("Modified", commit.Modified);
                LogFileChanges("Removed", commit.Removed);
            }
        } 
        else
        {
            this.Logger.LogInformation("No commits found in the push event payload");
        }
    }

    private void LogFileChanges(string changeType, List<string> files)
    {
        if (files?.Any() is true)
        {
            this.Logger.LogInformation("    {ChangeType} files: {Files}", changeType, string.Join(", ", files));
        }
        else
        {
            this.Logger.LogInformation("    No files {ChangeType}", changeType.ToLower());
        }
    }

    #endregion
}

using ArchGuard.MCP.Models;
using Octokit;
using System.Text.Json;

namespace ArchGuard.MCP.Services.WebhookHandlers;

public class CheckSuiteWebhookHandler : WebhookHandlerBase
{
    public override string EventType => "check_suite";
    
    private ILogger<CheckSuiteWebhookHandler> Logger { get; set; }
    private IRepositoryCloneService CloneService { get; set; }
    private IConfiguration Configuration { get; set; }

    public CheckSuiteWebhookHandler(
        GitHubCheckService checkService,
        GitHubAppAuthService authService,
        IGitHubClient githubClient,
        IRepositoryPathResolver pathResolver,
        IRepositoryCloneService cloneService,
        IConfiguration configuration,
        ILogger<CheckSuiteWebhookHandler> logger)
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
            var checkSuitePayload = JsonSerializer.Deserialize<GitHubCheckSuiteWebhookPayload>(requestBody);

            if (checkSuitePayload?.CheckSuite is null)
            {
                this.Logger.LogWarning("Invalid check suite payload structure for delivery {DeliveryId}", deliveryId);
                return Results.BadRequest("Invalid check_suite event payload structure");
            }

            // Authenticate with GitHub
            var installationToken = await AuthService.GetInstallationTokenAsync(installationId);
            GitHubClient.Connection.Credentials = new Credentials(installationToken, AuthenticationType.Bearer);

            return checkSuitePayload.Action.ToLowerInvariant() switch
            {
                "rerequested" or "requested" => await HandleSuiteActionAsync(checkSuitePayload, installationId),
                _ => HandleInformationalAction(checkSuitePayload)
            };
        }
        catch (JsonException ex)
        {
            this.Logger.LogError(ex, "Failed to parse check suite JSON payload for delivery {DeliveryId}", deliveryId);
            return Results.BadRequest("Invalid JSON payload for check suite event");
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error processing check suite webhook for delivery {DeliveryId}", deliveryId);
            throw;
        }
    }

    private async Task<IResult> HandleSuiteActionAsync(GitHubCheckSuiteWebhookPayload checkSuitePayload, long installationId)
    {
        this.Logger.LogInformation("Check Suite for commit {CommitSha} was {Action}. Triggering all checks...",
            checkSuitePayload.CheckSuite.HeadSha.Substring(0, 7), checkSuitePayload.Action);

        // Get repository root path via cloning
        var root = await PathResolver.GetRootFromWebhookAsync(checkSuitePayload, installationId);

        // Trigger dependency injection check
        var diCheckArgs = new CheckExecutionArgs
        {
            RepoOwner = checkSuitePayload.Repository.Owner.Login,
            RepoName = checkSuitePayload.Repository.Name,
            CommitSha = checkSuitePayload.CheckSuite.HeadSha,
            CheckName = GitHubCheckService.DependencyRegistrationCheckName,
            InstallationId = installationId,
            ExistingCheckRunId = null, // Create new check runs for "re-run all"
            InitialTitle = GitHubCheckService.DependencyRegistrationCheckName,
            InitialSummary = "Starting DI validation via 'Re-run all checks'."
        };

        // Fire-and-forget: Intentionally not awaited to allow immediate return
        // The Task.Run executes async Octokit operations in background
        _ = Task.Run(async () => 
        {
            try
            {
                await CheckService.ExecuteDepInjectionCheckAsync(diCheckArgs, root, installationId, GitHubClient);
                
                // Clean up repository after validation if configured to do so
                var cleanupAfterValidation = this.Configuration.GetValue("RepositoryCloning:CleanupAfterValidation", true);
                if (cleanupAfterValidation)
                {
                    this.Logger.LogInformation("Cleaning up repository after validation: {RepoFullName} at {CommitSha}", 
                        checkSuitePayload.Repository.FullName, checkSuitePayload.CheckSuite.HeadSha);
                    
                    // We need to get the LocalPath from the clone result, but PathResolver only returns AgentPath
                    // For now, we'll reconstruct the path based on the known pattern
                    var tempBasePath = Path.Combine(Path.GetTempPath(), "archguard-clones");
                    var sanitizedRepoName = checkSuitePayload.Repository.FullName.Replace('/', '-').Replace('\\', '-');
                    var shortCommitSha = checkSuitePayload.CheckSuite.HeadSha.Length > 8 ? checkSuitePayload.CheckSuite.HeadSha.Substring(0, 8) : checkSuitePayload.CheckSuite.HeadSha;
                    var expectedPath = Path.Combine(tempBasePath, $"{sanitizedRepoName}-{shortCommitSha}");
                    
                    if (Directory.Exists(expectedPath))
                    {
                        await this.CloneService.CleanupRepositoryAsync(expectedPath);
                    }
                }
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex, "Error during validation or cleanup for {RepoFullName}", checkSuitePayload.Repository.FullName);
            }
        });

        // Add additional checks here as needed
        // Example:
        // var otherCheckArgs = new CheckExecutionArgs { ... };
        // _ = Task.Run(() => _checkService.ExecuteOtherCheckAsync(otherCheckArgs, ...));

        return Results.Accepted("All checks initiated from check_suite event");
    }

    private IResult HandleInformationalAction(GitHubCheckSuiteWebhookPayload checkSuitePayload)
    {
        this.Logger.LogInformation("Check Suite for commit {CommitSha} received informational action: '{Action}'. Acknowledging.",
            checkSuitePayload.CheckSuite.HeadSha.Substring(0, 7), checkSuitePayload.Action);

        return Results.Ok("Check suite event acknowledged");
    }

}
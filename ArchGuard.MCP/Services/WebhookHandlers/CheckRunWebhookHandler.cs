using ArchGuard.MCP.Models;
using Octokit;
using System.Text.Json;

namespace ArchGuard.MCP.Services.WebhookHandlers;

public class CheckRunWebhookHandler : WebhookHandlerBase
{
    public override string EventType => "check_run";
    
    private ILogger<CheckRunWebhookHandler> Logger { get; set; }
    private IRepositoryCloneService CloneService { get; set; }
    private IConfiguration Configuration { get; set; }

    public CheckRunWebhookHandler(
        GitHubCheckService checkService,
        GitHubAppAuthService authService,
        IGitHubClient githubClient,
        IRepositoryPathResolver pathResolver,
        IRepositoryCloneService cloneService,
        IConfiguration configuration,
        ILogger<CheckRunWebhookHandler> logger)
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
            var checkRunPayload = JsonSerializer.Deserialize<GitHubCheckRunWebhookPayload>(requestBody);

            if (checkRunPayload?.CheckRun is null)
            {
                this.Logger.LogWarning("Invalid check run payload structure for delivery {DeliveryId}", deliveryId);
                return Results.BadRequest("Invalid check_run event payload structure");
            }

            // Authenticate with GitHub
            var installationToken = await AuthService.GetInstallationTokenAsync(installationId);
            GitHubClient.Connection.Credentials = new Credentials(installationToken, AuthenticationType.Bearer);

            return checkRunPayload.Action.ToLowerInvariant() switch
            {
                "rerequested" => await HandleRerequestedAsync(checkRunPayload, installationId),
                "requested_action" => HandleRequestedAction(checkRunPayload),
                _ => HandleInformationalAction(checkRunPayload)
            };
        }
        catch (JsonException ex)
        {
            this.Logger.LogError(ex, "Failed to parse check run JSON payload for delivery {DeliveryId}", deliveryId);
            return Results.BadRequest("Invalid JSON payload for check run event");
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error processing check run webhook for delivery {DeliveryId}", deliveryId);
            throw;
        }
    }

    private async Task<IResult> HandleRerequestedAsync(GitHubCheckRunWebhookPayload checkRunPayload, long installationId)
    {
        // Get repository root path via cloning
        var root = await PathResolver.GetRootFromWebhookAsync(checkRunPayload, installationId);

        var checkArgs = new CheckExecutionArgs
        {
            RepoOwner = checkRunPayload.Repository.Owner.Login,
            RepoName = checkRunPayload.Repository.Name,
            CommitSha = checkRunPayload.CheckRun.HeadSha,
            CheckName = checkRunPayload.CheckRun.Name,
            InstallationId = installationId,
            ExistingCheckRunId = checkRunPayload.CheckRun.Id,
            InitialTitle = checkRunPayload.CheckRun.Output?.Title ?? checkRunPayload.CheckRun.Name,
            InitialSummary = checkRunPayload.CheckRun.Output?.Summary ?? $"Re-running {checkRunPayload.CheckRun.Name}..."
        };

        // Route to appropriate check based on name
        if (checkArgs.CheckName == GitHubCheckService.DependencyRegistrationCheckName)
        {
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
                            checkRunPayload.Repository.FullName, checkRunPayload.CheckRun.HeadSha);
                        
                        // Reconstruct the path based on the known pattern
                        var tempBasePath = Path.Combine(Path.GetTempPath(), "archguard-clones");
                        var sanitizedRepoName = checkRunPayload.Repository.FullName.Replace('/', '-').Replace('\\', '-');
                        var shortCommitSha = checkRunPayload.CheckRun.HeadSha.Length > 8 ? checkRunPayload.CheckRun.HeadSha.Substring(0, 8) : checkRunPayload.CheckRun.HeadSha;
                        var expectedPath = Path.Combine(tempBasePath, $"{sanitizedRepoName}-{shortCommitSha}");
                        
                        if (Directory.Exists(expectedPath))
                        {
                            await this.CloneService.CleanupRepositoryAsync(expectedPath);
                        }
                        else
                        {
                            this.Logger.LogWarning("Expected repository path not found for cleanup: {ExpectedPath}", expectedPath);
                        }
                    }
                }
                catch (Exception ex)
                {
                    this.Logger.LogError(ex, "Error during validation or cleanup for {RepoFullName}", checkRunPayload.Repository.FullName);
                }
            });
        }
        else
        {
            this.Logger.LogWarning("Received rerequested action for unknown check name: {CheckName}", checkArgs.CheckName);
        }

        return Results.Accepted("Check run rerequest initiated");
    }

    private IResult HandleRequestedAction(GitHubCheckRunWebhookPayload checkRunPayload)
    {
        var actionIdentifier = checkRunPayload.RequestedAction?.Identifier ?? "Unknown_Action_Identifier";

        this.Logger.LogInformation("Check run '{CheckName}' (ID: {CheckRunId}) received requested_action with Identifier: '{ActionId}'",
            checkRunPayload.CheckRun.Name, checkRunPayload.CheckRun.Id, actionIdentifier);

        // TODO: Implement custom action handling if needed
        return Results.Accepted("Requested action acknowledged");
    }

    private IResult HandleInformationalAction(GitHubCheckRunWebhookPayload checkRunPayload)
    {
        this.Logger.LogInformation("Check run '{CheckName}' (ID: {CheckRunId}) received informational action: '{Action}'. Acknowledging.",
            checkRunPayload.CheckRun.Name, checkRunPayload.CheckRun.Id, checkRunPayload.Action);

        return Results.Ok("Check run event acknowledged");
    }

}
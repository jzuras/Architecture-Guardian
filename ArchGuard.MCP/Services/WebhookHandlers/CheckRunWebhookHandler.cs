using ArchGuard.MCP.Models;
using ArchGuard.Shared;
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
        IRepositoryCloneService cloneService,
        IConfiguration configuration,
        ILogger<CheckRunWebhookHandler> logger)
        : base(checkService, authService, githubClient)
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
        // Clone repository directly to get both path formats
        var cloneResult = await CloneService.CloneRepositoryAsync(
            checkRunPayload.Repository.CloneUrl,
            checkRunPayload.CheckRun.HeadSha,
            checkRunPayload.Repository.FullName);

        if (!cloneResult.Success)
        {
            this.Logger.LogError("Failed to clone repository: {ErrorMessage}", cloneResult.ErrorMessage);
            return Results.Problem("Failed to clone repository");
        }

        var diCheckArgs = new CheckExecutionArgs
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
        if (diCheckArgs.CheckName == ValidationService.DependencyRegistrationCheckName)
        {
            // Fire-and-forget: Intentionally not awaited to allow immediate return
            // The Task.Run executes async Octokit operations in background
            _ = Task.Run(async () =>
            {
                try
                {
                    // ARCHGUARD_TEMPLATE_WEBHOOK_RULE_START
                    await CheckService.ExecuteDepInjectionCheckAsync(diCheckArgs, cloneResult.WindowsPath, cloneResult.WslPath, installationId, GitHubClient);
                    // ARCHGUARD_TEMPLATE_WEBHOOK_RULE_END

                    // Clean up repository after validation if configured to do so
                    var cleanupAfterValidation = this.Configuration.GetValue("RepositoryCloning:CleanupAfterValidation", true);
                    if (cleanupAfterValidation)
                    {
                        this.Logger.LogInformation("Cleaning up repository after validation: {RepoFullName} at {CommitSha}",
                            checkRunPayload.Repository.FullName, checkRunPayload.CheckRun.HeadSha);

                        await this.CloneService.CleanupRepositoryAsync(cloneResult.WindowsPath);
                    }
                }
                catch (Exception ex)
                {
                    this.Logger.LogError(ex, "Error during validation or cleanup for {RepoFullName}", checkRunPayload.Repository.FullName);
                }
            });
        }
        // ARCHGUARD_INSERTION_POINT_RULE_ROUTING_START
        // New rule routing conditions go here in alphabetical order by rule name
        // Format: else if (diCheckArgs.CheckName == RuleCheckName) { ... }

        // ARCHGUARD_GENERATED_RULE_START - ValidateEntityDtoPropertyMapping
        // Generated from template on: 9/17/25
        // DO NOT EDIT - This code will be regenerated
        else if (diCheckArgs.CheckName == ValidationService.EntityDtoPropertyMappingCheckName)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await CheckService.ExecuteEntityDtoPropertyMappingCheckAsync(diCheckArgs, cloneResult.WindowsPath, cloneResult.WslPath, installationId, GitHubClient);

                    // Clean up repository after validation if configured to do so
                    var cleanupAfterValidation = this.Configuration.GetValue("RepositoryCloning:CleanupAfterValidation", true);
                    if (cleanupAfterValidation)
                    {
                        this.Logger.LogInformation("Cleaning up repository after validation: {RepoFullName} at {CommitSha}",
                            checkRunPayload.Repository.FullName, checkRunPayload.CheckRun.HeadSha);

                        await this.CloneService.CleanupRepositoryAsync(cloneResult.WindowsPath);
                    }
                }
                catch (Exception ex)
                {
                    this.Logger.LogError(ex, "Error during validation or cleanup for {RepoFullName}", checkRunPayload.Repository.FullName);
                }
            });
        }
        // ARCHGUARD_GENERATED_RULE_END - ValidateEntityDtoPropertyMapping

        // ARCHGUARD_INSERTION_POINT_RULE_ROUTING_END
        else
        {
            this.Logger.LogWarning("Received rerequested action for unknown check name: {CheckName}", diCheckArgs.CheckName);
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
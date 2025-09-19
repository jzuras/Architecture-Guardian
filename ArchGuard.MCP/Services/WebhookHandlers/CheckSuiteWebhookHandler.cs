using ArchGuard.MCP.Models;
using ArchGuard.Shared;
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
        IRepositoryCloneService cloneService,
        IConfiguration configuration,
        ILogger<CheckSuiteWebhookHandler> logger)
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
        // Skip automatic check_suite events triggered by pushes to avoid race conditions
        // Only process explicit user-initiated "re-run all" requests (action = "rerequested")
        if (checkSuitePayload.Action.Equals("requested", StringComparison.OrdinalIgnoreCase))
        {
            this.Logger.LogInformation("Check Suite for commit {CommitSha} was automatically '{Action}' (likely triggered by push). Ignoring to avoid race condition with push handler.",
                checkSuitePayload.CheckSuite.HeadSha.Substring(0, 7), checkSuitePayload.Action);
            return Results.Ok("Automatic check_suite event ignored - push handler will process");
        }

        this.Logger.LogInformation("Check Suite for commit {CommitSha} was {Action} by user ({SenderLogin}). Triggering all checks...",
            checkSuitePayload.CheckSuite.HeadSha.Substring(0, 7), checkSuitePayload.Action, checkSuitePayload.Sender.Login);

        // Trigger dependency injection check
        var diCheckArgs = new CheckExecutionArgs
        {
            RepoOwner = checkSuitePayload.Repository.Owner.Login,
            RepoName = checkSuitePayload.Repository.Name,
            CommitSha = checkSuitePayload.CheckSuite.HeadSha,
            CheckName = ValidationService.DependencyRegistrationCheckName,
            InstallationId = installationId,
            ExistingCheckRunId = null,
            InitialTitle = ValidationService.DependencyRegistrationCheckName,
            InitialSummary = "Starting " + ValidationService.DependencyRegistrationCheckName + " validation for 'Re-run all checks'."
        };

        // ARCHGUARD_INSERTION_POINT_ARGS_START
        // New rule CheckExecutionArgs declarations go here in alphabetical order by rule name

        // ARCHGUARD_GENERATED_RULE_START - ValidateEntityDtoPropertyMapping
        // Generated from template on: 9/17/25
        // DO NOT EDIT - This code will be regenerated
        var entityDtoPropertyMappingCheckArgs = new CheckExecutionArgs
        {
            RepoOwner = checkSuitePayload.Repository.Owner.Login,
            RepoName = checkSuitePayload.Repository.Name,
            CommitSha = checkSuitePayload.CheckSuite.HeadSha,
            CheckName = ValidationService.EntityDtoPropertyMappingCheckName,
            InstallationId = installationId,
            ExistingCheckRunId = null,
            InitialTitle = ValidationService.EntityDtoPropertyMappingCheckName,
            InitialSummary = "Starting " + ValidationService.EntityDtoPropertyMappingCheckName + " validation for 'Re-run all checks'."
        };
        // ARCHGUARD_GENERATED_RULE_END - ValidateEntityDtoPropertyMapping

        // ARCHGUARD_INSERTION_POINT_ARGS_END

        // Phase 1: Create all checks immediately (synchronous for immediate GitHub UI visibility)
        // ARCHGUARD_TEMPLATE_CHECK_CREATION_START
        var depCheckId = await CheckService.CreateCheckAsync(ValidationService.DependencyRegistrationCheckName, diCheckArgs, GitHubClient, GitHubCheckService.DependencyRegistrationDetailsUrlForCheck);
        // ARCHGUARD_TEMPLATE_CHECK_CREATION_END

        // ARCHGUARD_INSERTION_POINT_CHECK_CREATION_START
        // New rule check creation calls go here in alphabetical order by rule name

        // ARCHGUARD_GENERATED_RULE_START - ValidateEntityDtoPropertyMapping
        // Generated from template on: 9/17/25
        // DO NOT EDIT - This code will be regenerated
        var entityDtoPropertyMappingCheckId = await CheckService.CreateCheckAsync(ValidationService.EntityDtoPropertyMappingCheckName, entityDtoPropertyMappingCheckArgs, GitHubClient, GitHubCheckService.EntityDtoPropertyMappingDetailsUrlForCheck);
        // ARCHGUARD_GENERATED_RULE_END - ValidateEntityDtoPropertyMapping

        // ARCHGUARD_INSERTION_POINT_CHECK_CREATION_END

        // Phase 2: Execute all checks (fire-and-forget background processing)
        _ = Task.Run(async () =>
        {
            try
            {
                // Clone repository directly to get both path formats
                var cloneResult = await CloneService.CloneRepositoryAsync(
                    checkSuitePayload.Repository.CloneUrl,
                    checkSuitePayload.CheckSuite.HeadSha,
                    checkSuitePayload.Repository.FullName);

                if (!cloneResult.Success)
                {
                    this.Logger.LogError("Failed to clone repository: {ErrorMessage}", cloneResult.ErrorMessage);
                    return;
                }

                // ARCHGUARD_TEMPLATE_WEBHOOK_RULE_START
                await CheckService.ExecuteDepInjectionCheckAsync(diCheckArgs, cloneResult.WindowsPath, cloneResult.WslPath, installationId, GitHubClient, depCheckId);
                // ARCHGUARD_TEMPLATE_WEBHOOK_RULE_END

                // ARCHGUARD_INSERTION_POINT_RULE_EXECUTION_START
                // New rule execution calls go here in alphabetical order by rule name

                // ARCHGUARD_GENERATED_RULE_START - ValidateEntityDtoPropertyMapping
                // Generated from template on: 9/17/25
                // DO NOT EDIT - This code will be regenerated
                await CheckService.ExecuteEntityDtoPropertyMappingCheckAsync(entityDtoPropertyMappingCheckArgs, cloneResult.WindowsPath, cloneResult.WslPath, installationId, GitHubClient, entityDtoPropertyMappingCheckId);
                // ARCHGUARD_GENERATED_RULE_END - ValidateEntityDtoPropertyMapping

                // ARCHGUARD_INSERTION_POINT_RULE_EXECUTION_END

                // Clean up repository after validation if configured to do so
                var cleanupAfterValidation = this.Configuration.GetValue("RepositoryCloning:CleanupAfterValidation", true);
                if (cleanupAfterValidation)
                {
                    this.Logger.LogInformation("Cleaning up repository after validation: {RepoFullName} at {CommitSha}", 
                        checkSuitePayload.Repository.FullName, checkSuitePayload.CheckSuite.HeadSha);
                    
                    // Note: With timestamp-based directory names, we can't predict the exact path
                    // The background cleanup service will handle cleanup based on age
                    // Manual cleanup here is skipped to avoid path reconstruction complexity
                }
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex, "Error during validation or cleanup for {RepoFullName}", checkSuitePayload.Repository.FullName);
            }
        });

        return Results.Accepted("All checks initiated from check_suite event");
    }

    private IResult HandleInformationalAction(GitHubCheckSuiteWebhookPayload checkSuitePayload)
    {
        this.Logger.LogInformation("Check Suite for commit {CommitSha} received informational action: '{Action}'. Acknowledging.",
            checkSuitePayload.CheckSuite.HeadSha.Substring(0, 7), checkSuitePayload.Action);

        return Results.Ok("Check suite event acknowledged");
    }

}
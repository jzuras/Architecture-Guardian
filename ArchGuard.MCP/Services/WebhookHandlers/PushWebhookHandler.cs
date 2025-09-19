using ArchGuard.MCP.Models;
using ArchGuard.Shared;
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
        IRepositoryCloneService cloneService,
        IConfiguration configuration,
        ILogger<PushWebhookHandler> logger)
        : base(checkService, authService, githubClient)
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

            // Prepare check execution arguments
            var diCheckArgs = new CheckExecutionArgs
            {
                RepoOwner = pushPayload.Repository.Owner.Login,
                RepoName = pushPayload.Repository.Name,
                CommitSha = pushPayload.After,
                CheckName = ValidationService.DependencyRegistrationCheckName,
                InstallationId = installationId,
                ExistingCheckRunId = null,
                InitialTitle = ValidationService.DependencyRegistrationCheckName,
                InitialSummary = "Starting " + ValidationService.DependencyRegistrationCheckName + " validation for this push."
            };

            // ARCHGUARD_INSERTION_POINT_ARGS_START
            // New rule CheckExecutionArgs declarations go here in alphabetical order by rule name

            // ARCHGUARD_GENERATED_RULE_START - ValidateEntityDtoPropertyMapping
            // Generated from template on: 9/17/25
            // DO NOT EDIT - This code will be regenerated
            var entityDtoPropertyMappingCheckArgs = new CheckExecutionArgs
            {
                RepoOwner = pushPayload.Repository.Owner.Login,
                RepoName = pushPayload.Repository.Name,
                CommitSha = pushPayload.After,
                CheckName = ValidationService.EntityDtoPropertyMappingCheckName,
                InstallationId = installationId,
                ExistingCheckRunId = null,
                InitialTitle = ValidationService.EntityDtoPropertyMappingCheckName,
                InitialSummary = "Starting " + ValidationService.EntityDtoPropertyMappingCheckName + " validation for this push."
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
                        pushPayload.Repository.CloneUrl,
                        pushPayload.After,
                        pushPayload.Repository.FullName);

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
                            pushPayload.Repository.FullName, pushPayload.After);

                        await this.CloneService.CleanupRepositoryAsync(cloneResult.WindowsPath);
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

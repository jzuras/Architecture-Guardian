using ArchGuard.MCP.Models;
using ArchGuard.Shared;
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
        IRepositoryCloneService cloneService,
        IConfiguration configuration,
        ILogger<PullRequestWebhookHandler> logger)
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

            // Prepare check execution arguments
            var diCheckArgs = new CheckExecutionArgs
            {
                RepoOwner = pullRequestPayload.Repository.Owner.Login,
                RepoName = pullRequestPayload.Repository.Name,
                CommitSha = pullRequestPayload.PullRequest.Head.Sha,
                CheckName = ValidationService.DependencyRegistrationCheckName,
                InstallationId = installationId,
                ExistingCheckRunId = null,
                InitialTitle = ValidationService.DependencyRegistrationCheckName,
                InitialSummary = "Starting " + ValidationService.DependencyRegistrationCheckName + " validation for this pull request."
            };

            // ARCHGUARD_INSERTION_POINT_ARGS_START
            // New rule CheckExecutionArgs declarations go here in alphabetical order by rule name

            // ARCHGUARD_GENERATED_RULE_START - ValidateEntityDtoPropertyMapping
            // Generated from template on: 9/17/25
            // DO NOT EDIT - This code will be regenerated
            var entityDtoPropertyMappingCheckArgs = new CheckExecutionArgs
            {
                RepoOwner = pullRequestPayload.Repository.Owner.Login,
                RepoName = pullRequestPayload.Repository.Name,
                CommitSha = pullRequestPayload.PullRequest.Head.Sha,
                CheckName = ValidationService.EntityDtoPropertyMappingCheckName,
                InstallationId = installationId,
                ExistingCheckRunId = null,
                InitialTitle = ValidationService.EntityDtoPropertyMappingCheckName,
                InitialSummary = "Starting " + ValidationService.EntityDtoPropertyMappingCheckName + " validation for this pull request."
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
                        pullRequestPayload.PullRequest.Head.Repo.CloneUrl,
                        pullRequestPayload.PullRequest.Head.Sha,
                        pullRequestPayload.PullRequest.Head.Repo.FullName);

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
                            pullRequestPayload.Repository.FullName, pullRequestPayload.PullRequest.Head.Sha);

                        await this.CloneService.CleanupRepositoryAsync(cloneResult.WindowsPath);
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
using ArchGuard.MCP.Models;
using ArchGuard.Shared;
using Octokit;

namespace ArchGuard.MCP.Services.WebhookHandlers;

public interface IWebhookHandler
{
    string EventType { get; }
    Task<IResult> HandleAsync(string requestBody, long installationId, string deliveryId);
}

public abstract class WebhookHandlerBase : IWebhookHandler
{
    public abstract string EventType { get; }

    public static CodingAgent SelectedCodingAgent { get; set; } = CodingAgent.ClaudeCode;

    protected GitHubCheckService CheckService { get; set; }
    protected GitHubAppAuthService AuthService { get; set; }
    protected IGitHubClient GitHubClient { get; set; }
    protected IRepositoryCloneService CloneService { get; set; }
    protected IConfiguration Configuration { get; set; }
    protected IGitHubFileContentService FileContentService { get; set; }

    protected WebhookHandlerBase(
        GitHubCheckService checkService,
        GitHubAppAuthService authService,
        IGitHubClient githubClient,
        IRepositoryCloneService cloneService,
        IConfiguration configuration,
        IGitHubFileContentService fileContentService)
    {
        this.CheckService = checkService;
        this.AuthService = authService;
        this.GitHubClient = githubClient;
        this.CloneService = cloneService;
        this.Configuration = configuration;
        this.FileContentService = fileContentService;
    }

    public abstract Task<IResult> HandleAsync(string requestBody, long installationId, string deliveryId);

    protected async Task<IResult> ExecuteAllChecksAsync(
        string repoOwner,
        string repoName,
        string commitSha,
        long installationId,
        long? checkRunId,
        string initialSummaryEndText,
        string cloneUrl,
        string repoFullName,
        string requestBody,
        string eventTypeForLogMessages,
        ILogger logger,
        string? checkNameFromCheckRun = null)
    {
        // Authenticate with GitHub
        await AuthenticateWithGitHubAsync(installationId);

        // Phase 1: Create all checks immediately (synchronous for immediate GitHub UI visibility)

        // ARCHGUARD_TEMPLATE_CHECK_CREATION_START
        // Prepare check execution arguments and create check
        CheckExecutionArgs diCheckArgs = new();
        long diCheckId = 0;
        if (checkNameFromCheckRun is null || checkNameFromCheckRun.Equals(GitHubCheckService.DependencyRegistrationCheckName, StringComparison.InvariantCultureIgnoreCase))
        {
            diCheckArgs = new CheckExecutionArgs
            {
                RepoOwner = repoOwner,
                RepoName = repoName,
                CommitSha = commitSha,
                CheckName = GitHubCheckService.DependencyRegistrationCheckName,
                InstallationId = installationId,
                ExistingCheckRunId = checkRunId,
                InitialTitle = GitHubCheckService.DependencyRegistrationCheckName,
                InitialSummary = $"Starting {GitHubCheckService.DependencyRegistrationCheckName} validation for '{initialSummaryEndText}'."
            };

            diCheckId = await CheckService.CreateCheckAsync(diCheckArgs, GitHubClient, GitHubCheckService.DependencyRegistrationDetailsUrlForCheck);
        }
        // ARCHGUARD_TEMPLATE_CHECK_CREATION_END

        // ARCHGUARD_INSERTION_POINT_CHECK_CREATION_START
        // New rule CheckExecutionArgs declarations and check creations go here in alphabetical order by rule name

        // ARCHGUARD_GENERATED_RULE_START - ValidateDependencyDirection
        // Generated from template on: 10/7/25
        // DO NOT EDIT - This code will be regenerated
        CheckExecutionArgs dependencyDirectionCheckArgs = new();
        long dependencyDirectionCheckId = 0;
        if (checkNameFromCheckRun is null || checkNameFromCheckRun.Equals(GitHubCheckService.DependencyDirectionCheckName, StringComparison.InvariantCultureIgnoreCase))
        {
            dependencyDirectionCheckArgs = new CheckExecutionArgs
            {
                RepoOwner = repoOwner,
                RepoName = repoName,
                CommitSha = commitSha,
                CheckName = GitHubCheckService.DependencyDirectionCheckName,
                InstallationId = installationId,
                ExistingCheckRunId = checkRunId,
                InitialTitle = GitHubCheckService.DependencyDirectionCheckName,
                InitialSummary = $"Starting {GitHubCheckService.DependencyDirectionCheckName} validation for '{initialSummaryEndText}'."
            };

            dependencyDirectionCheckId = await CheckService.CreateCheckAsync(dependencyDirectionCheckArgs, GitHubClient, GitHubCheckService.DependencyDirectionDetailsUrlForCheck);
        }
        // ARCHGUARD_GENERATED_RULE_END - ValidateDependencyDirection

        // ARCHGUARD_GENERATED_RULE_START - ValidateEntityDtoPropertyMapping
        // Generated from template on: 9/17/25
        // DO NOT EDIT - This code will be regenerated
        CheckExecutionArgs entityDtoPropertyMappingCheckArgs = new();
        long entityDtoPropertyMappingCheckId = 0;
        if (checkNameFromCheckRun is null || checkNameFromCheckRun.Equals(GitHubCheckService.EntityDtoPropertyMappingCheckName, StringComparison.InvariantCultureIgnoreCase))
        {
            entityDtoPropertyMappingCheckArgs = new CheckExecutionArgs
            {
                RepoOwner = repoOwner,
                RepoName = repoName,
                CommitSha = commitSha,
                CheckName = GitHubCheckService.EntityDtoPropertyMappingCheckName,
                InstallationId = installationId,
                ExistingCheckRunId = checkRunId,
                InitialTitle = GitHubCheckService.EntityDtoPropertyMappingCheckName,
                InitialSummary = $"Starting {GitHubCheckService.EntityDtoPropertyMappingCheckName} validation for '{initialSummaryEndText}'."
            };

            entityDtoPropertyMappingCheckId = await CheckService.CreateCheckAsync(entityDtoPropertyMappingCheckArgs, GitHubClient, GitHubCheckService.EntityDtoPropertyMappingDetailsUrlForCheck);
        }
        // ARCHGUARD_GENERATED_RULE_END - ValidateEntityDtoPropertyMapping

        // ARCHGUARD_INSERTION_POINT_CHECK_CREATION_END

        // Phase 2: Execute all checks (fire-and-forget background processing)

        _ = Task.Run(async () =>
        {
            try
            {
                string windowsRoot = "";
                string wslRoot = "";
                ContextFile[] sharedContextFiles = Array.Empty<ContextFile>();

                if (WebhookHandlerBase.SelectedCodingAgent == CodingAgent.ClaudeCode ||
                   WebhookHandlerBase.SelectedCodingAgent == CodingAgent.GeminiCli)
                {
                    // Clone repository for file system agents
                    var cloneResult = await CloneService.CloneRepositoryAsync(
                        cloneUrl,
                        commitSha,
                        repoFullName);

                    if (!cloneResult.Success)
                    {
                        logger.LogError("Failed to clone repository: {ErrorMessage}", cloneResult.ErrorMessage);
                        return;
                    }

                    windowsRoot = cloneResult.WindowsPath;
                    wslRoot = cloneResult.WslPath;
                    logger.LogInformation("Repository cloned for file system agent: {Agent}", WebhookHandlerBase.SelectedCodingAgent);
                }
                else if (WebhookHandlerBase.SelectedCodingAgent == CodingAgent.LocalFoundry || WebhookHandlerBase.SelectedCodingAgent == CodingAgent.GitHubModels)
                {
                    // Extract files for API-based agents (no cloning needed)
                    try
                    {
                        var extractionResult = await this.FileContentService.ExtractFromWebhookAsync(requestBody, GitHubClient);
                        sharedContextFiles = extractionResult.Files;
                        logger.LogInformation("Extracted {FileCount} files for API agent: {Agent} (diffs embedded in ContextFile objects)",
                            sharedContextFiles.Length, WebhookHandlerBase.SelectedCodingAgent);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to extract files for {EventType} event, rules will fall back to individual extraction", eventTypeForLogMessages);
                    }
                }
                else
                {
                    // TBD: Unknown agent type
                    logger.LogWarning("Unknown coding agent type: {Agent}, skipping validation", WebhookHandlerBase.SelectedCodingAgent);
                    return;
                }

                // ARCHGUARD_TEMPLATE_WEBHOOK_RULE_START
                if (checkNameFromCheckRun is null || checkNameFromCheckRun.Equals(GitHubCheckService.DependencyRegistrationCheckName, StringComparison.InvariantCultureIgnoreCase))
                {
                    await CheckService.ExecuteDepInjectionCheckAsync(diCheckArgs, windowsRoot, wslRoot, installationId, GitHubClient, diCheckId, sharedContextFiles, requestBody);
                }
                // ARCHGUARD_TEMPLATE_WEBHOOK_RULE_END

                // ARCHGUARD_INSERTION_POINT_RULE_EXECUTION_START
                // New rule execution calls go here in alphabetical order by rule name

                // ARCHGUARD_GENERATED_RULE_START - ValidateDependencyDirection
                // Generated from template on: 10/7/25
                // DO NOT EDIT - This code will be regenerated
                if (checkNameFromCheckRun is null || checkNameFromCheckRun.Equals(GitHubCheckService.DependencyDirectionCheckName, StringComparison.InvariantCultureIgnoreCase))
                {
                    await CheckService.ExecuteDependencyDirectionCheckAsync(dependencyDirectionCheckArgs, windowsRoot, wslRoot, installationId, GitHubClient, dependencyDirectionCheckId, sharedContextFiles, requestBody);
                }
                // ARCHGUARD_GENERATED_RULE_END - ValidateDependencyDirection

                // ARCHGUARD_GENERATED_RULE_START - ValidateEntityDtoPropertyMapping
                // Generated from template on: 9/17/25
                // DO NOT EDIT - This code will be regenerated
                if (checkNameFromCheckRun is null || checkNameFromCheckRun.Equals(GitHubCheckService.EntityDtoPropertyMappingCheckName, StringComparison.InvariantCultureIgnoreCase))
                {
                    await CheckService.ExecuteEntityDtoPropertyMappingCheckAsync(entityDtoPropertyMappingCheckArgs, windowsRoot, wslRoot, installationId, GitHubClient, entityDtoPropertyMappingCheckId, sharedContextFiles, requestBody);
                }
                // ARCHGUARD_GENERATED_RULE_END - ValidateEntityDtoPropertyMapping

                // ARCHGUARD_INSERTION_POINT_RULE_EXECUTION_END

                // Clean up repository after validation if configured to do so (only for file system agents)
                if (string.IsNullOrEmpty(windowsRoot) is false)
                {
                    var cleanupAfterValidation = this.Configuration.GetValue("RepositoryCloning:CleanupAfterValidation", true);
                    if (cleanupAfterValidation)
                    {
                        logger.LogInformation("Cleaning up repository after validation: {RepoName} at {CommitSha}",
                            repoName, commitSha);

                        await this.CloneService.CleanupRepositoryAsync(windowsRoot);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during validation or cleanup for {RepoName}", repoName);
            }
        });

        return Results.Accepted($"{eventTypeForLogMessages} event checks initiated");
    }

    protected async Task AuthenticateWithGitHubAsync(long installationId)
    {
        var installationToken = await AuthService.GetInstallationTokenAsync(installationId);
        GitHubClient.Connection.Credentials = new Credentials(installationToken, AuthenticationType.Bearer);
    }
}

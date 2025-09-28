using ArchGuard.MCP.Models;
using Octokit;
using System.Text.Json;

namespace ArchGuard.MCP.Services.WebhookHandlers;

//  Different Re-Run Triggers:
//  1. "Re-run all checks" -> Calls check_suite webhook
//  2. "Re-run failed checks" -> Calls check_run webhook for each failed check
//  3. Individual check "Re-run" -> Calls check_run webhook for that specific check

public class CheckSuiteWebhookHandler : WebhookHandlerBase
{
    public override string EventType => "check_suite";
    
    private ILogger<CheckSuiteWebhookHandler> Logger { get; set; }

    public CheckSuiteWebhookHandler(
        GitHubCheckService checkService,
        GitHubAppAuthService authService,
        IGitHubClient githubClient,
        IRepositoryCloneService cloneService,
        IConfiguration configuration,
        IGitHubFileContentService fileContentService,
        ILogger<CheckSuiteWebhookHandler> logger)
        : base(checkService, authService, githubClient, cloneService, configuration, fileContentService)
    {
        this.Logger = logger;
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
            await AuthenticateWithGitHubAsync(installationId);

            // Handle and return for info-only actions, which is anything other than re-requested.
            if(checkSuitePayload.Action.ToLowerInvariant() != "rerequested")
            {
                return HandleInformationalAction(checkSuitePayload);
            }

            this.Logger.LogInformation($"Check suite payload action is '{checkSuitePayload.Action}'");

            this.Logger.LogInformation("Check Suite for commit {CommitSha} was {Action} by user ({SenderLogin}). Triggering all checks...",
                checkSuitePayload.CheckSuite.HeadSha.Substring(0, 7), checkSuitePayload.Action, checkSuitePayload.Sender.Login);

            return await ExecuteAllChecksAsync(
                repoOwner: checkSuitePayload.Repository.Owner.Login,
                repoName: checkSuitePayload.Repository.Name,
                commitSha: checkSuitePayload.CheckSuite.HeadSha,
                installationId: installationId,
                checkRunId: null,
                initialSummaryEndText: "Re-run all checks",
                cloneUrl: checkSuitePayload.Repository.CloneUrl,
                repoFullName: checkSuitePayload.Repository.FullName,
                requestBody: requestBody,
                eventTypeForLogMessages: "Check Suite",
                logger: this.Logger);
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

    private IResult HandleInformationalAction(GitHubCheckSuiteWebhookPayload checkSuitePayload)
    {
        this.Logger.LogInformation("Check Suite for commit {CommitSha} received informational action: '{Action}'. Acknowledging.",
            checkSuitePayload.CheckSuite.HeadSha.Substring(0, 7), checkSuitePayload.Action);

        return Results.Ok("Check suite event acknowledged");
    }
}

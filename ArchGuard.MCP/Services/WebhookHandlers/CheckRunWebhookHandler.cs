using ArchGuard.MCP.Models;
using Octokit;
using System.Text.Json;

namespace ArchGuard.MCP.Services.WebhookHandlers;

//  Different Re-Run Triggers:
//  1. "Re-run all checks" -> Calls check_suite webhook
//  2. "Re-run failed checks" -> Calls check_run webhook for each failed check
//  3. Individual check "Re-run" -> Calls check_run webhook for that specific check
//
// Specifically for Check Run callbacks:
//
// Each rule execution happens in isolation because:
//  - GitHub sends separate webhooks for each check run re-run
//  - Each webhook is handled independently
//  - The handler doesn't know if other checks will be re-run
//
//  Clone sharing would require:
//  - Coordinating multiple separate webhook calls
//  - Complex state management to detect "batch re-runs"
//  - Timing logic to wait for all expected webhooks

public class CheckRunWebhookHandler : WebhookHandlerBase
{
    public override string EventType => "check_run";
    
    private ILogger<CheckRunWebhookHandler> Logger { get; set; }

    public CheckRunWebhookHandler(
        GitHubCheckService checkService,
        GitHubAppAuthService authService,
        IGitHubClient githubClient,
        IRepositoryCloneService cloneService,
        IConfiguration configuration,
        IGitHubFileContentService fileContentService,
        ILogger<CheckRunWebhookHandler> logger)
        : base(checkService, authService, githubClient, cloneService, configuration, fileContentService)
    {
        this.Logger = logger;
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
            await AuthenticateWithGitHubAsync(installationId);

            // Handle and return for info-only actions.
            if (checkRunPayload.Action.ToLowerInvariant() == "requested_action")
            {
                return HandleRequestedAction(checkRunPayload);
            }
            else if (checkRunPayload.Action.ToLowerInvariant() != "rerequested")
            {
                return HandleInformationalAction(checkRunPayload);
            }

            this.Logger.LogInformation("Check run '{CheckName}' (ID: {CheckRunId}) was rerequested by user ({SenderLogin}).",
                checkRunPayload.CheckRun.Name, checkRunPayload.CheckRun.Id, checkRunPayload.Sender.Login);

            return await ExecuteAllChecksAsync(
                repoOwner: checkRunPayload.Repository.Owner.Login,
                repoName: checkRunPayload.Repository.Name,
                commitSha: checkRunPayload.CheckRun.HeadSha,
                installationId: installationId,
                checkRunId: checkRunPayload.CheckRun.Id,
                initialSummaryEndText: "re-validation",
                cloneUrl: checkRunPayload.Repository.CloneUrl,
                repoFullName: checkRunPayload.Repository.FullName,
                requestBody: requestBody,
                eventTypeForLogMessages: "Check Run",
                logger: this.Logger,
                checkNameFromCheckRun: checkRunPayload.CheckRun.Name); // this param forces a single check to be run instead of all checks
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

    private IResult HandleRequestedAction(GitHubCheckRunWebhookPayload checkRunPayload)
    {
        // This is only for custom action buttons. Not used in this project.

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

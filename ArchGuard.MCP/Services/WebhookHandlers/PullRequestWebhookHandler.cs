using ArchGuard.MCP.Models;
using Octokit;
using System.Text.Json;

namespace ArchGuard.MCP.Services.WebhookHandlers;

public class PullRequestWebhookHandler : WebhookHandlerBase
{
    public override string EventType => "pull_request";
    
    private ILogger<PullRequestWebhookHandler> Logger { get; set; }

    public PullRequestWebhookHandler(
        GitHubCheckService checkService,
        GitHubAppAuthService authService,
        IGitHubClient githubClient,
        IRepositoryCloneService cloneService,
        IConfiguration configuration,
        IGitHubFileContentService fileContentService,
        ILogger<PullRequestWebhookHandler> logger)
        : base(checkService, authService, githubClient, cloneService, configuration, fileContentService)
    {
        this.Logger = logger;
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

            // Handle and return for sync action (from a push).
            if (pullRequestPayload.Action.ToLowerInvariant() == "synchronize")
            {
                return HandleInformationalAction(pullRequestPayload);
            }

            return await ExecuteAllChecksAsync(
                repoOwner: pullRequestPayload.Repository.Owner.Login,
                repoName: pullRequestPayload.Repository.Name,
                commitSha: pullRequestPayload.PullRequest.Head.Sha,
                installationId: installationId,
                checkRunId: null,
                initialSummaryEndText: "this pull request",
                cloneUrl: pullRequestPayload.Repository.CloneUrl,
                repoFullName: pullRequestPayload.Repository.FullName,
                requestBody: requestBody,
                eventTypeForLogMessages: "Pull Request",
                logger: this.Logger);
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

    private IResult HandleInformationalAction(GitHubPullRequestWebhookPayload pullRequestPayload)
    {
        this.Logger.LogInformation("Pull Request '{CheckName}' (ID: {CheckRunId}) received informational action: '{Action}'. Acknowledging.",
            pullRequestPayload.Repository.Name, pullRequestPayload.PullRequest.Id, pullRequestPayload.Action);

        return Results.Ok("Check run event acknowledged");
    }
}

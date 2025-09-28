using ArchGuard.MCP.Models;
using Octokit;
using System.Text.Json;

namespace ArchGuard.MCP.Services.WebhookHandlers;

public class PushWebhookHandler : WebhookHandlerBase
{
    public override string EventType => "push";
    
    private ILogger<PushWebhookHandler> Logger { get; set; }

    #region Constructor

    public PushWebhookHandler(
        GitHubCheckService checkService,
        GitHubAppAuthService authService,
        IGitHubClient githubClient,
        IRepositoryCloneService cloneService,
        IConfiguration configuration,
        IGitHubFileContentService fileContentService,
        ILogger<PushWebhookHandler> logger)
        : base(checkService, authService, githubClient, cloneService, configuration, fileContentService)
    {
        this.Logger = logger;
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

            return await ExecuteAllChecksAsync(
                repoOwner: pushPayload.Repository.Owner.Login,
                repoName: pushPayload.Repository.Name,
                commitSha: pushPayload.After,
                installationId: installationId,
                checkRunId: null,
                initialSummaryEndText: "this push",
                cloneUrl: pushPayload.Repository.CloneUrl,
                repoFullName: pushPayload.Repository.FullName,
                requestBody: requestBody,
                eventTypeForLogMessages: "Push",
                logger: this.Logger);
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
}

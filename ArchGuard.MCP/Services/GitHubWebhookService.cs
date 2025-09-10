using System.Text;

namespace ArchGuard.MCP.Services;

#region Interfaces

public interface IGitHubWebhookService
{
    Task<IResult> HandleWebhookAsync(
        HttpRequest request,
        string githubEvent,
        string githubDelivery,
        string? githubSignature,
        long? githubInstallationIdFromHeader);
}

#endregion

#region Service Implementation

public class GitHubWebhookService : IGitHubWebhookService
{
    private IGitHubWebhookAuthenticator Authenticator { get; set; }
    private IGitHubWebhookRouter Router { get; set; }
    private ILogger<GitHubWebhookService> Logger { get; set; }
    private IConfiguration Configuration { get; set; }

    #region Constructor

    public GitHubWebhookService(
        IGitHubWebhookAuthenticator authenticator,
        IGitHubWebhookRouter router,
        ILogger<GitHubWebhookService> logger,
        IConfiguration configuration)
    {
        this.Authenticator = authenticator;
        this.Router = router;
        this.Logger = logger;
        this.Configuration = configuration;
    }

    #endregion

    #region Public Methods

    public async Task<IResult> HandleWebhookAsync(
        HttpRequest request,
        string githubEvent,
        string githubDelivery,
        string? githubSignature,
        long? githubInstallationIdFromHeader)
    {
        this.Logger.LogInformation("Received GitHub webhook. Event: {Event}, Delivery: {DeliveryId}", 
            githubEvent, githubDelivery);

        try
        {
            // Read request body
            string requestBody = await ReadRequestBodyAsync(request);

            // Authenticate webhook
            var secret = this.Configuration["GitHub:WebhookSecret"] ?? "";
            var authResult = this.Authenticator.Authenticate(requestBody, githubSignature, secret);
            
            if (!authResult.IsSuccess)
            {
                this.Logger.LogWarning("Webhook authentication failed for delivery {DeliveryId}: {Error}", 
                    githubDelivery, authResult.ErrorMessage);
                
                return authResult.StatusCode == 401 
                    ? Results.Unauthorized()
                    : Results.BadRequest(authResult.ErrorMessage);
            }

            // Extract installation ID
            long installationId;
            try
            {
                installationId = this.Authenticator.ExtractInstallationId(requestBody, githubInstallationIdFromHeader);
            }
            catch (InvalidOperationException ex)
            {
                this.Logger.LogError("Installation ID extraction failed for delivery {DeliveryId}, event {Event}: {Error}", 
                    githubDelivery, githubEvent, ex.Message);
                return Results.BadRequest("Missing GitHub App installation ID. Required for this event type.");
            }

            // Route to appropriate handler
            return await this.Router.RouteEventAsync(githubEvent, requestBody, installationId, githubDelivery);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Unexpected error processing GitHub webhook for delivery {DeliveryId}, event {Event}", 
                githubDelivery, githubEvent);
            return Results.Problem(
                statusCode: 500,
                title: "Internal Server Error",
                detail: "An unexpected error occurred while processing the webhook."
            );
        }
    }

    #endregion

    #region Private Methods

    private static async Task<string> ReadRequestBodyAsync(HttpRequest request)
    {
        using var reader = new StreamReader(request.Body, Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    #endregion
}

#endregion
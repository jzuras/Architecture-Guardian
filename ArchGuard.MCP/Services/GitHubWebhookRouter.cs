using ArchGuard.MCP.Services.WebhookHandlers;

namespace ArchGuard.MCP.Services;

public interface IGitHubWebhookRouter
{
    Task<IResult> RouteEventAsync(string eventType, string requestBody, long installationId, string deliveryId);
}

public class GitHubWebhookRouter : IGitHubWebhookRouter
{
    private Dictionary<string, IWebhookHandler> Handlers { get; set; }
    private ILogger<GitHubWebhookRouter> Logger { get; set; }

    public GitHubWebhookRouter(
        IEnumerable<IWebhookHandler> handlers,
        ILogger<GitHubWebhookRouter> logger)
    {
        this.Handlers = handlers.ToDictionary(h => h.EventType, h => h, StringComparer.OrdinalIgnoreCase);
        this.Logger = logger;
    }

    public async Task<IResult> RouteEventAsync(string eventType, string requestBody, long installationId, string deliveryId)
    {
        try
        {
            if (this.Handlers.TryGetValue(eventType, out var handler))
            {
                this.Logger.LogInformation("Routing {EventType} event to {HandlerType}", eventType, handler.GetType().Name);
                return await handler.HandleAsync(requestBody, installationId, deliveryId);
            }

            this.Logger.LogInformation("Unhandled GitHub event type: {EventType}", eventType);
            return Results.Ok($"Event {eventType} acknowledged but not processed");
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error routing {EventType} webhook event for delivery {DeliveryId}", eventType, deliveryId);
            return Results.Problem(
                statusCode: 500,
                title: "Internal Server Error",
                detail: $"An error occurred processing {eventType} webhook event"
            );
        }
    }
}
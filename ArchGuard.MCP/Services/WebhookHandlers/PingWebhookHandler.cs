namespace ArchGuard.MCP.Services.WebhookHandlers;

public class PingWebhookHandler : IWebhookHandler
{
    public string EventType => "ping";
    
    private ILogger<PingWebhookHandler> Logger { get; set; }

    public PingWebhookHandler(ILogger<PingWebhookHandler> logger)
    {
        this.Logger = logger;
    }

    public Task<IResult> HandleAsync(string requestBody, long installationId, string deliveryId)
    {
        this.Logger.LogInformation("Received GitHub 'ping' event for delivery {DeliveryId}. Webhook is active.", deliveryId);
        return Task.FromResult(Results.Ok("Pong!"));
    }
}
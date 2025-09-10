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

    protected GitHubCheckService CheckService { get; set; }
    protected GitHubAppAuthService AuthService { get; set; }
    protected IGitHubClient GitHubClient { get; set; }
    protected IRepositoryPathResolver PathResolver { get; set; }

    protected WebhookHandlerBase(
        GitHubCheckService checkService,
        GitHubAppAuthService authService,
        IGitHubClient githubClient,
        IRepositoryPathResolver pathResolver)
    {
        this.CheckService = checkService;
        this.AuthService = authService;
        this.GitHubClient = githubClient;
        this.PathResolver = pathResolver;
    }

    public abstract Task<IResult> HandleAsync(string requestBody, long installationId, string deliveryId);
}

public class WebhookHandlerContext
{
    public string RequestBody { get; set; } = string.Empty;
    public long InstallationId { get; set; }
    public string DeliveryId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public ILogger Logger { get; set; } = default!;
}
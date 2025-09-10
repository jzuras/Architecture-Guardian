using System.Text.Json.Serialization;

namespace ArchGuard.MCP.Models;

public class GitHubCheckRunWebhookPayload
{
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("check_run")]
    public CheckRun CheckRun { get; set; } = new CheckRun();

    [JsonPropertyName("repository")]
    public CheckRunEventRepository Repository { get; set; } = new CheckRunEventRepository();

    [JsonPropertyName("sender")]
    public Sender Sender { get; set; } = new Sender();

    [JsonPropertyName("requested_action")]
    public RequestedActionData? RequestedAction { get; set; }

    [JsonPropertyName("installation")]
    public InstallationData? Installation { get; set; }
}

public class GitHubCheckSuiteWebhookPayload
{
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("check_suite")]
    public CheckSuite CheckSuite { get; set; } = new CheckSuite();

    [JsonPropertyName("repository")]
    public CheckRunEventRepository Repository { get; set; } = new CheckRunEventRepository();

    [JsonPropertyName("sender")]
    public Sender Sender { get; set; } = new Sender();

    [JsonPropertyName("installation")]
    public InstallationData? Installation { get; set; }
}

public class RequestedActionData
{
    [JsonPropertyName("identifier")]
    public string Identifier { get; set; } = string.Empty;
}

public class CheckRun
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("node_id")]
    public string NodeId { get; set; } = string.Empty;

    [JsonPropertyName("head_sha")]
    public string HeadSha { get; set; } = string.Empty;

    [JsonPropertyName("external_id")]
    public string? ExternalId { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("conclusion")]
    public string? Conclusion { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; set; } = string.Empty;

    [JsonPropertyName("details_url")]
    public string? DetailsUrl { get; set; }

    [JsonPropertyName("started_at")]
    public DateTimeOffset StartedAt { get; set; }

    [JsonPropertyName("completed_at")]
    public DateTimeOffset? CompletedAt { get; set; }

    [JsonPropertyName("output")]
    public CheckRunOutput? Output { get; set; }

    [JsonPropertyName("app")]
    public GitHubApp App { get; set; } = new GitHubApp();

    [JsonPropertyName("pull_requests")]
    public List<PullRequestReference> PullRequests { get; set; } = new List<PullRequestReference>();
}

public class CheckRunOutput
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }
}

public class CheckSuite
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("node_id")]
    public string NodeId { get; set; } = string.Empty;

    [JsonPropertyName("head_branch")]
    public string HeadBranch { get; set; } = string.Empty;

    [JsonPropertyName("head_sha")]
    public string HeadSha { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("conclusion")]
    public string? Conclusion { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("before")]
    public string Before { get; set; } = string.Empty;

    [JsonPropertyName("after")]
    public string After { get; set; } = string.Empty;

    [JsonPropertyName("pull_requests")]
    public List<PullRequestReference> PullRequests { get; set; } = new List<PullRequestReference>();

    [JsonPropertyName("app")]
    public GitHubApp App { get; set; } = new GitHubApp();

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }

    [JsonPropertyName("rerequestable")]
    public bool? Rerequestable { get; set; }
}
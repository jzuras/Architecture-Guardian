using System.Text.Json.Serialization;

namespace ArchGuard.MCP.Models;

public class GitHubPushWebhookPayload
{
    [JsonPropertyName("ref")]
    public string Ref { get; set; } = string.Empty;

    [JsonPropertyName("before")]
    public string Before { get; set; } = string.Empty;

    [JsonPropertyName("after")]
    public string After { get; set; } = string.Empty;

    [JsonPropertyName("repository")]
    public Repository Repository { get; set; } = new Repository();

    [JsonPropertyName("pusher")]
    public Pusher Pusher { get; set; } = new Pusher();

    [JsonPropertyName("sender")]
    public Sender Sender { get; set; } = new Sender();

    [JsonPropertyName("created")]
    public bool Created { get; set; }

    [JsonPropertyName("deleted")]
    public bool Deleted { get; set; }

    [JsonPropertyName("forced")]
    public bool Forced { get; set; }

    [JsonPropertyName("base_ref")]
    public string? BaseRef { get; set; }

    [JsonPropertyName("compare")]
    public string Compare { get; set; } = string.Empty;

    [JsonPropertyName("commits")]
    public List<Commit> Commits { get; set; } = new List<Commit>();

    [JsonPropertyName("head_commit")]
    public HeadCommit HeadCommit { get; set; } = new HeadCommit();
}

public class Pusher
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;
}

public class Commit
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("tree_id")]
    public string TreeId { get; set; } = string.Empty;

    [JsonPropertyName("distinct")]
    public bool Distinct { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("author")]
    public CommitUser Author { get; set; } = new CommitUser();

    [JsonPropertyName("committer")]
    public CommitUser Committer { get; set; } = new CommitUser();

    [JsonPropertyName("added")]
    public List<string> Added { get; set; } = new List<string>();

    [JsonPropertyName("removed")]
    public List<string> Removed { get; set; } = new List<string>();

    [JsonPropertyName("modified")]
    public List<string> Modified { get; set; } = new List<string>();
}

public class CommitUser
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;
}

public class HeadCommit : Commit
{
}
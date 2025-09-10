using System.Text.Json.Serialization;

namespace ArchGuard.MCP.Models;

public class GitHubApp
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("slug")]
    public string? Slug { get; set; }

    [JsonPropertyName("node_id")]
    public string NodeId { get; set; } = string.Empty;

    [JsonPropertyName("owner")]
    public Owner Owner { get; set; } = new Owner();

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public class PullRequestReference
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("number")]
    public int Number { get; set; }

    [JsonPropertyName("head")]
    public PullRequestReferenceCommit Head { get; set; } = new PullRequestReferenceCommit();

    [JsonPropertyName("base")]
    public PullRequestReferenceCommit Base { get; set; } = new PullRequestReferenceCommit();
}

public class PullRequestReferenceCommit
{
    [JsonPropertyName("ref")]
    public string Ref { get; set; } = string.Empty;

    [JsonPropertyName("sha")]
    public string Sha { get; set; } = string.Empty;

    [JsonPropertyName("repo")]
    public Repository Repo { get; set; } = new Repository();
}
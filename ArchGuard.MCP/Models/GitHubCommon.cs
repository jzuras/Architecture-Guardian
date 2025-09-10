using System.Text.Json.Serialization;

namespace ArchGuard.MCP.Models;

public class Owner
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("login")]
    public string Login { get; set; } = string.Empty;

    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("node_id")]
    public string NodeId { get; set; } = string.Empty;

    [JsonPropertyName("avatar_url")]
    public string AvatarUrl { get; set; } = string.Empty;

    [JsonPropertyName("gravatar_id")]
    public string GravatarId { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; set; } = string.Empty;

    [JsonPropertyName("followers_url")]
    public string FollowersUrl { get; set; } = string.Empty;

    [JsonPropertyName("following_url")]
    public string FollowingUrl { get; set; } = string.Empty;

    [JsonPropertyName("gists_url")]
    public string GistsUrl { get; set; } = string.Empty;

    [JsonPropertyName("starred_url")]
    public string StarredUrl { get; set; } = string.Empty;

    [JsonPropertyName("subscriptions_url")]
    public string SubscriptionsUrl { get; set; } = string.Empty;

    [JsonPropertyName("organizations_url")]
    public string OrganizationsUrl { get; set; } = string.Empty;

    [JsonPropertyName("repos_url")]
    public string ReposUrl { get; set; } = string.Empty;

    [JsonPropertyName("events_url")]
    public string EventsUrl { get; set; } = string.Empty;

    [JsonPropertyName("received_events_url")]
    public string ReceivedEventsUrl { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("user_view_type")]
    public string? UserViewType { get; set; }

    [JsonPropertyName("site_admin")]
    public bool SiteAdmin { get; set; }
}

public class Sender : Owner
{
}

public class InstallationData
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("node_id")]
    public string NodeId { get; set; } = string.Empty;
}

public class Link
{
    [JsonPropertyName("href")]
    public string Href { get; set; } = string.Empty;
}
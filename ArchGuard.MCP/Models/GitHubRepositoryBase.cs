using System.Text.Json.Serialization;

namespace ArchGuard.MCP.Models;

public abstract class BaseRepository
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("node_id")]
    public string NodeId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("full_name")]
    public string FullName { get; set; } = string.Empty;

    [JsonPropertyName("private")]
    public bool Private { get; set; }

    [JsonPropertyName("owner")]
    public Owner Owner { get; set; } = new Owner();

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("fork")]
    public bool Fork { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("forks_url")]
    public string ForksUrl { get; set; } = string.Empty;

    [JsonPropertyName("keys_url")]
    public string KeysUrl { get; set; } = string.Empty;

    [JsonPropertyName("collaborators_url")]
    public string CollaboratorsUrl { get; set; } = string.Empty;

    [JsonPropertyName("teams_url")]
    public string TeamsUrl { get; set; } = string.Empty;

    [JsonPropertyName("hooks_url")]
    public string HooksUrl { get; set; } = string.Empty;

    [JsonPropertyName("issue_events_url")]
    public string IssueEventsUrl { get; set; } = string.Empty;

    [JsonPropertyName("events_url")]
    public string EventsUrl { get; set; } = string.Empty;

    [JsonPropertyName("assignees_url")]
    public string AssigneesUrl { get; set; } = string.Empty;

    [JsonPropertyName("branches_url")]
    public string BranchesUrl { get; set; } = string.Empty;

    [JsonPropertyName("tags_url")]
    public string TagsUrl { get; set; } = string.Empty;

    [JsonPropertyName("blobs_url")]
    public string BlobsUrl { get; set; } = string.Empty;

    [JsonPropertyName("git_tags_url")]
    public string GitTagsUrl { get; set; } = string.Empty;

    [JsonPropertyName("git_refs_url")]
    public string GitRefsUrl { get; set; } = string.Empty;

    [JsonPropertyName("trees_url")]
    public string TreesUrl { get; set; } = string.Empty;

    [JsonPropertyName("statuses_url")]
    public string StatusesUrl { get; set; } = string.Empty;

    [JsonPropertyName("languages_url")]
    public string LanguagesUrl { get; set; } = string.Empty;

    [JsonPropertyName("stargazers_url")]
    public string StargazersUrl { get; set; } = string.Empty;

    [JsonPropertyName("contributors_url")]
    public string ContributorsUrl { get; set; } = string.Empty;

    [JsonPropertyName("subscribers_url")]
    public string SubscribersUrl { get; set; } = string.Empty;

    [JsonPropertyName("subscription_url")]
    public string SubscriptionUrl { get; set; } = string.Empty;

    [JsonPropertyName("commits_url")]
    public string CommitsUrl { get; set; } = string.Empty;

    [JsonPropertyName("git_commits_url")]
    public string GitCommitsUrl { get; set; } = string.Empty;

    [JsonPropertyName("comments_url")]
    public string CommentsUrl { get; set; } = string.Empty;

    [JsonPropertyName("issue_comment_url")]
    public string IssueCommentUrl { get; set; } = string.Empty;

    [JsonPropertyName("contents_url")]
    public string ContentsUrl { get; set; } = string.Empty;

    [JsonPropertyName("compare_url")]
    public string CompareUrl { get; set; } = string.Empty;

    [JsonPropertyName("merges_url")]
    public string MergesUrl { get; set; } = string.Empty;

    [JsonPropertyName("archive_url")]
    public string ArchiveUrl { get; set; } = string.Empty;

    [JsonPropertyName("downloads_url")]
    public string DownloadsUrl { get; set; } = string.Empty;

    [JsonPropertyName("issues_url")]
    public string IssuesUrl { get; set; } = string.Empty;

    [JsonPropertyName("pulls_url")]
    public string PullsUrl { get; set; } = string.Empty;

    [JsonPropertyName("milestones_url")]
    public string MilestonesUrl { get; set; } = string.Empty;

    [JsonPropertyName("notifications_url")]
    public string NotificationsUrl { get; set; } = string.Empty;

    [JsonPropertyName("labels_url")]
    public string LabelsUrl { get; set; } = string.Empty;

    [JsonPropertyName("releases_url")]
    public string ReleasesUrl { get; set; } = string.Empty;

    [JsonPropertyName("deployments_url")]
    public string DeploymentsUrl { get; set; } = string.Empty;

    [JsonPropertyName("git_url")]
    public string GitUrl { get; set; } = string.Empty;

    [JsonPropertyName("ssh_url")]
    public string SshUrl { get; set; } = string.Empty;

    [JsonPropertyName("clone_url")]
    public string CloneUrl { get; set; } = string.Empty;

    [JsonPropertyName("svn_url")]
    public string SvnUrl { get; set; } = string.Empty;

    [JsonPropertyName("homepage")]
    public string? Homepage { get; set; }

    [JsonPropertyName("size")]
    public int Size { get; set; }

    [JsonPropertyName("stargazers_count")]
    public int StargazersCount { get; set; }

    [JsonPropertyName("watchers_count")]
    public int WatchersCount { get; set; }

    [JsonPropertyName("language")]
    public string Language { get; set; } = string.Empty;

    [JsonPropertyName("has_issues")]
    public bool HasIssues { get; set; }

    [JsonPropertyName("has_projects")]
    public bool HasProjects { get; set; }

    [JsonPropertyName("has_downloads")]
    public bool HasDownloads { get; set; }

    [JsonPropertyName("has_wiki")]
    public bool HasWiki { get; set; }

    [JsonPropertyName("has_pages")]
    public bool HasPages { get; set; }

    [JsonPropertyName("has_discussions")]
    public bool HasDiscussions { get; set; }

    [JsonPropertyName("forks_count")]
    public int ForksCount { get; set; }

    [JsonPropertyName("mirror_url")]
    public string? MirrorUrl { get; set; }

    [JsonPropertyName("archived")]
    public bool Archived { get; set; }

    [JsonPropertyName("disabled")]
    public bool Disabled { get; set; }

    [JsonPropertyName("open_issues_count")]
    public int OpenIssuesCount { get; set; }

    [JsonPropertyName("license")]
    public object? License { get; set; }

    [JsonPropertyName("allow_forking")]
    public bool AllowForking { get; set; }

    [JsonPropertyName("is_template")]
    public bool IsTemplate { get; set; }

    [JsonPropertyName("web_commit_signoff_required")]
    public bool WebCommitSignoffRequired { get; set; }

    [JsonPropertyName("topics")]
    public List<object> Topics { get; set; } = new List<object>();

    [JsonPropertyName("visibility")]
    public string Visibility { get; set; } = string.Empty;

    [JsonPropertyName("forks")]
    public int Forks { get; set; }

    [JsonPropertyName("open_issues")]
    public int OpenIssues { get; set; }

    [JsonPropertyName("watchers")]
    public int Watchers { get; set; }

    [JsonPropertyName("default_branch")]
    public string DefaultBranch { get; set; } = string.Empty;

    [JsonPropertyName("stargazers")]
    public int Stargazers { get; set; }

    [JsonPropertyName("master_branch")]
    public string MasterBranch { get; set; } = string.Empty;
}

public class Repository : BaseRepository
{
    [JsonPropertyName("created_at")]
    public long CreatedAt { get; set; } // Unix timestamp

    [JsonPropertyName("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }

    [JsonPropertyName("pushed_at")]
    public long PushedAt { get; set; } // Unix timestamp
}

public class CheckRunEventRepository : BaseRepository
{
    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }

    [JsonPropertyName("pushed_at")]
    public DateTimeOffset PushedAt { get; set; }
}

public class PullRequestEventRepository : BaseRepository
{
    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }

    [JsonPropertyName("pushed_at")]
    public DateTimeOffset PushedAt { get; set; }

    // Additional properties unique to pull request events
    [JsonPropertyName("allow_squash_merge")]
    public bool AllowSquashMerge { get; set; }

    [JsonPropertyName("allow_merge_commit")]
    public bool AllowMergeCommit { get; set; }

    [JsonPropertyName("allow_rebase_merge")]
    public bool AllowRebaseMerge { get; set; }

    [JsonPropertyName("allow_auto_merge")]
    public bool AllowAutoMerge { get; set; }

    [JsonPropertyName("delete_branch_on_merge")]
    public bool DeleteBranchOnMerge { get; set; }

    [JsonPropertyName("allow_update_branch")]
    public bool AllowUpdateBranch { get; set; }

    [JsonPropertyName("use_squash_pr_title_as_default")]
    public bool UseSquashPrTitleAsDefault { get; set; }

    [JsonPropertyName("squash_merge_commit_message")]
    public string SquashMergeCommitMessage { get; set; } = string.Empty;

    [JsonPropertyName("squash_merge_commit_title")]
    public string SquashMergeCommitTitle { get; set; } = string.Empty;

    [JsonPropertyName("merge_commit_message")]
    public string MergeCommitMessage { get; set; } = string.Empty;

    [JsonPropertyName("merge_commit_title")]
    public string MergeCommitTitle { get; set; } = string.Empty;
}
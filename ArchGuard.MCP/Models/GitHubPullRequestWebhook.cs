using System.Text.Json.Serialization;

namespace ArchGuard.MCP.Models;

public class GitHubPullRequestWebhookPayload
{
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty; // e.g., "opened", "synchronize", "reopened", "closed"

    [JsonPropertyName("number")]
    public int Number { get; set; } // The pull request number

    [JsonPropertyName("pull_request")]
    public PullRequest PullRequest { get; set; } = new PullRequest();

    [JsonPropertyName("repository")]
    public PullRequestEventRepository Repository { get; set; } = new PullRequestEventRepository(); // IMPORTANT: Use a specific repo class for this event

    [JsonPropertyName("sender")]
    public Sender Sender { get; set; } = new Sender();

    [JsonPropertyName("installation")]
    public InstallationData? Installation { get; set; }
}

public class PullRequest
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("node_id")]
    public string NodeId { get; set; } = string.Empty;

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; set; } = string.Empty;

    [JsonPropertyName("diff_url")]
    public string DiffUrl { get; set; } = string.Empty;

    [JsonPropertyName("patch_url")]
    public string PatchUrl { get; set; } = string.Empty;

    [JsonPropertyName("issue_url")]
    public string IssueUrl { get; set; } = string.Empty;

    [JsonPropertyName("number")]
    public int Number { get; set; }

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty; // e.g., "open", "closed"

    [JsonPropertyName("locked")]
    public bool Locked { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("user")]
    public Owner User { get; set; } = new Owner();

    [JsonPropertyName("body")]
    public string? Body { get; set; } // Nullable

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }

    [JsonPropertyName("closed_at")]
    public DateTimeOffset? ClosedAt { get; set; } // Nullable

    [JsonPropertyName("merged_at")]
    public DateTimeOffset? MergedAt { get; set; } // Nullable

    [JsonPropertyName("merge_commit_sha")]
    public string? MergeCommitSha { get; set; } // Nullable

    [JsonPropertyName("assignee")]
    public Owner? Assignee { get; set; }

    [JsonPropertyName("assignees")]
    public List<Owner> Assignees { get; set; } = new List<Owner>();

    [JsonPropertyName("requested_reviewers")]
    public List<Owner> RequestedReviewers { get; set; } = new List<Owner>();

    [JsonPropertyName("requested_teams")]
    public List<object> RequestedTeams { get; set; } = new List<object>(); // Can be Team objects, simpler as object for now

    [JsonPropertyName("labels")]
    public List<object> Labels { get; set; } = new List<object>(); // Can be Label objects, simpler as object for now

    [JsonPropertyName("milestone")]
    public object? Milestone { get; set; } // Nullable, can be Milestone object

    [JsonPropertyName("draft")]
    public bool Draft { get; set; }

    [JsonPropertyName("commits_url")]
    public string CommitsUrl { get; set; } = string.Empty;

    [JsonPropertyName("review_comments_url")]
    public string ReviewCommentsUrl { get; set; } = string.Empty;

    [JsonPropertyName("review_comment_url")]
    public string ReviewCommentUrl { get; set; } = string.Empty;

    [JsonPropertyName("comments_url")]
    public string CommentsUrl { get; set; } = string.Empty;

    [JsonPropertyName("statuses_url")]
    public string StatusesUrl { get; set; } = string.Empty;

    [JsonPropertyName("head")]
    public PullRequestBranch Head { get; set; } = new PullRequestBranch();

    [JsonPropertyName("base")]
    public PullRequestBranch Base { get; set; } = new PullRequestBranch();

    [JsonPropertyName("_links")]
    public PullRequestLinks Links { get; set; } = new PullRequestLinks();

    [JsonPropertyName("author_association")]
    public string AuthorAssociation { get; set; } = string.Empty;

    [JsonPropertyName("auto_merge")]
    public object? AutoMerge { get; set; } // Nullable, can be object

    [JsonPropertyName("active_lock_reason")]
    public string? ActiveLockReason { get; set; } // Nullable

    [JsonPropertyName("merged")]
    public bool Merged { get; set; }

    [JsonPropertyName("mergeable")]
    public bool? Mergeable { get; set; } // Nullable

    [JsonPropertyName("rebaseable")]
    public bool? Rebaseable { get; set; } // Nullable

    [JsonPropertyName("mergeable_state")]
    public string MergeableState { get; set; } = string.Empty;

    [JsonPropertyName("merged_by")]
    public Owner? MergedBy { get; set; }

    [JsonPropertyName("comments")]
    public int Comments { get; set; }

    [JsonPropertyName("review_comments")]
    public int ReviewComments { get; set; }

    [JsonPropertyName("maintainer_can_modify")]
    public bool MaintainerCanModify { get; set; }

    [JsonPropertyName("commits")]
    public int CommitsCount { get; set; } // Renamed to avoid conflict with List<Commit> if it were here

    [JsonPropertyName("additions")]
    public int Additions { get; set; }

    [JsonPropertyName("deletions")]
    public int Deletions { get; set; }

    [JsonPropertyName("changed_files")]
    public int ChangedFiles { get; set; }
}

public class PullRequestBranch
{
    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("ref")]
    public string Ref { get; set; } = string.Empty; // Branch name, e.g., "main", "feature-branch"

    [JsonPropertyName("sha")]
    public string Sha { get; set; } = string.Empty; // Commit SHA of this branch head

    [JsonPropertyName("user")]
    public Owner User { get; set; } = new Owner();

    [JsonPropertyName("repo")]
    public PullRequestEventRepository Repo { get; set; } = new PullRequestEventRepository();
}


public class PullRequestLinks
{
[JsonPropertyName("self")]
public Link Self { get; set; } = new Link();

[JsonPropertyName("html")]
public Link Html { get; set; } = new Link();

[JsonPropertyName("issue")]
public Link Issue { get; set; } = new Link();

[JsonPropertyName("comments")]
public Link Comments { get; set; } = new Link();

[JsonPropertyName("review_comments")]
public Link ReviewComments { get; set; } = new Link();

[JsonPropertyName("review_comment")]
public Link ReviewComment { get; set; } = new Link();

[JsonPropertyName("commits")]
public Link Commits { get; set; } = new Link();

[JsonPropertyName("statuses")]
public Link Statuses { get; set; } = new Link();
}
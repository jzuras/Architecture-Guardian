using ArchGuard.Shared;
using Octokit;
using System.Text;
using System.Text.Json;

namespace ArchGuard.MCP.Services;

/// <summary>
/// Enhanced service for extracting file contents from GitHub repositories via GitHub API
/// Supports both "all files" and "changed files only" modes with diff extraction
/// Handles push events, pull request events, and direct repository access
/// </summary>
public class GitHubFileContentService : IGitHubFileContentService
{
    private ILogger<GitHubFileContentService> Logger { get; set; }
    private IConfiguration Configuration { get; set; }

    public GitHubFileContentService(
        ILogger<GitHubFileContentService> logger,
        IConfiguration configuration)
    {
        this.Logger = logger;
        this.Configuration = configuration;
    }

    /// <summary>
    /// Original method - extracts ALL files from repository at specific commit
    /// </summary>
    public async Task<ContextFile[]> ExtractFileContentsAsync(
        string repoOwner,
        string repoName,
        string commitSha,
        IGitHubClient gitHubClient,
        string[]? fileExtensions = null,
        int maxFiles = 50,
        int maxFileSizeBytes = 100 * 1024)
    {
        try
        {
            // Default to C# files if no extensions specified
            var extensions = fileExtensions ?? new[] { ".cs", ".csproj", ".sln" };

            this.Logger.LogInformation("Extracting ALL file contents for repo {RepoOwner}/{RepoName} at commit {CommitSha}, max files: {MaxFiles}",
                repoOwner, repoName, commitSha.Substring(0, 7), maxFiles);

            // Get the repository tree for the commit
            var tree = await gitHubClient.Git.Tree.GetRecursive(repoOwner, repoName, commitSha);

            if (tree?.Tree is null || tree.Tree.Count == 0)
            {
                this.Logger.LogWarning("No files found in repository tree for {RepoOwner}/{RepoName} at {CommitSha}",
                    repoOwner, repoName, commitSha.Substring(0, 7));
                return Array.Empty<ContextFile>();
            }

            // Filter files by extension and type (blob = file, tree = directory)
            var relevantFiles = tree.Tree
                .Where(item => item.Type == TreeType.Blob) // Only files, not directories
                .Where(item => extensions.Any(ext => item.Path.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                .Where(item => item.Size <= maxFileSizeBytes) // Filter by file size
                .Take(maxFiles) // Limit number of files
                .ToList();

            this.Logger.LogInformation("Found {FileCount} relevant files matching extensions: {Extensions}",
                relevantFiles.Count, string.Join(", ", extensions));

            var contextFiles = new List<ContextFile>();

            // Fetch content for each file
            foreach (var file in relevantFiles)
            {
                try
                {
                    // Get file blob content
                    var blob = await gitHubClient.Git.Blob.Get(repoOwner, repoName, file.Sha);

                    if (blob?.Content is null)
                    {
                        this.Logger.LogWarning("Failed to get content for file {FilePath}", file.Path);
                        continue;
                    }

                    string content;
                    if (blob.Encoding == EncodingType.Base64)
                    {
                        // Decode base64 content
                        var bytes = Convert.FromBase64String(blob.Content);
                        content = Encoding.UTF8.GetString(bytes);
                    }
                    else
                    {
                        // Content is already in text format
                        content = blob.Content;
                    }

                    // Validate content is reasonable (not binary or too large)
                    if (content.Length > maxFileSizeBytes * 2) // Allow for encoding overhead
                    {
                        this.Logger.LogWarning("File {FilePath} content too large ({Size} chars), skipping",
                            file.Path, content.Length);
                        continue;
                    }

                    // Check if content appears to be text (not binary)
                    if (ContainsBinaryData(content))
                    {
                        this.Logger.LogWarning("File {FilePath} appears to contain binary data, skipping", file.Path);
                        continue;
                    }

                    contextFiles.Add(new ContextFile(file.Path, content));

                    this.Logger.LogDebug("Successfully extracted content from {FilePath} ({Size} chars)",
                        file.Path, content.Length);
                }
                catch (Exception ex)
                {
                    this.Logger.LogWarning(ex, "Failed to extract content from file {FilePath}", file.Path);
                    // Continue processing other files
                }
            }

            this.Logger.LogInformation("Successfully extracted content from {ExtractedCount} of {TotalCount} files",
                contextFiles.Count, relevantFiles.Count);

            return contextFiles.ToArray();
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Failed to extract file contents for repo {RepoOwner}/{RepoName} at commit {CommitSha}",
                repoOwner, repoName, commitSha.Substring(0, 7));

            // Return empty array rather than throwing to allow validation to proceed with empty context
            return Array.Empty<ContextFile>();
        }
    }

    /// <summary>
    /// Enhanced method - extracts files and diffs from webhook payload
    /// Supports both "all files" and "changed files only" modes based on app setting
    /// </summary>
    public async Task<FileExtractionResult> ExtractFromWebhookAsync(
        string webhookPayloadJson,
        IGitHubClient gitHubClient,
        string[]? fileExtensions = null,
        int maxFiles = 50,
        int maxFileSizeBytes = 100 * 1024)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(webhookPayloadJson))
            {
                this.Logger.LogWarning("Received empty webhook payload. No files to process.");
                return new FileExtractionResult(Array.Empty<ContextFile>(), Array.Empty<string>());
            }

            // Check app setting to determine extraction mode
            var extractChangedOnly = this.Configuration.GetValue("GitHubFileExtraction:ChangedFilesOnly", true);
            this.Logger.LogInformation("Using file extraction mode: {Mode}",
                extractChangedOnly ? "Changed Files Only" : "All Files");

            JsonDocument payloadDocument;
            try
            {
                payloadDocument = JsonDocument.Parse(webhookPayloadJson);
            }
            catch (JsonException ex)
            {
                this.Logger.LogError(ex, "Failed to parse webhook payload JSON.");
                throw new ArgumentException("Invalid JSON payload.", nameof(webhookPayloadJson), ex);
            }

            // Extract repository information
            var repoInfo = ExtractRepositoryInfo(payloadDocument);
            if (repoInfo is null)
            {
                this.Logger.LogWarning("Could not extract repository information from webhook payload");
                return new FileExtractionResult(Array.Empty<ContextFile>(), Array.Empty<string>());
            }

            this.Logger.LogInformation("Processing webhook for repository {Owner}/{RepoName} (ID: {RepoId})",
                repoInfo.Owner, repoInfo.Name, repoInfo.Id);

            // Determine event type and dispatch
            FileExtractionResult result;
            if (extractChangedOnly)
            {
                result = await ExtractChangedFilesAsync(payloadDocument, repoInfo, gitHubClient, fileExtensions, maxFiles, maxFileSizeBytes);
            }
            else
            {
                result = await ExtractAllFilesFromWebhookAsync(payloadDocument, repoInfo, gitHubClient, fileExtensions, maxFiles, maxFileSizeBytes);
            }

            return result;
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Failed to extract files from webhook payload");
            return new FileExtractionResult(Array.Empty<ContextFile>(), Array.Empty<string>());
        }
    }

    #region Private Helper Methods

    private record RepositoryInfo(long Id, string Owner, string Name, string FullName);

    private RepositoryInfo? ExtractRepositoryInfo(JsonDocument payloadDocument)
    {
        if (!payloadDocument.RootElement.TryGetProperty("repository", out var repoElement))
            return null;

        var fullName = repoElement.TryGetProperty("full_name", out var fullNameElement) ? fullNameElement.GetString() : null;
        var id = repoElement.TryGetProperty("id", out var idElement) && idElement.ValueKind == JsonValueKind.Number
                    ? idElement.GetInt64() : (long?)null;

        if (string.IsNullOrWhiteSpace(fullName) || !id.HasValue)
            return null;

        var parts = fullName.Split('/');
        if (parts.Length != 2)
            return null;

        return new RepositoryInfo(id.Value, parts[0], parts[1], fullName);
    }

    private async Task<FileExtractionResult> ExtractChangedFilesAsync(
        JsonDocument payloadDocument,
        RepositoryInfo repoInfo,
        IGitHubClient gitHubClient,
        string[]? fileExtensions,
        int maxFiles,
        int maxFileSizeBytes)
    {
        // Determine event type based on payload structure
        if (payloadDocument.RootElement.TryGetProperty("ref", out _) &&
            payloadDocument.RootElement.TryGetProperty("after", out _))
        {
            this.Logger.LogDebug("Detected 'push' event for changed files extraction");
            return await HandlePushEventAsync(payloadDocument.RootElement, repoInfo, gitHubClient, fileExtensions, maxFiles, maxFileSizeBytes);
        }
        else if (payloadDocument.RootElement.TryGetProperty("pull_request", out _))
        {
            this.Logger.LogDebug("Detected 'pull_request' event for changed files extraction");
            return await HandlePullRequestEventAsync(payloadDocument.RootElement, repoInfo, gitHubClient, fileExtensions, maxFiles, maxFileSizeBytes);
        }
        else if (payloadDocument.RootElement.TryGetProperty("check_suite", out _))
        {
            this.Logger.LogDebug("Detected 'check_suite' event for changed files extraction");
            return await HandleCheckSuiteEventAsync(payloadDocument.RootElement, repoInfo, gitHubClient, fileExtensions, maxFiles, maxFileSizeBytes);
        }
        else if (payloadDocument.RootElement.TryGetProperty("check_run", out _))
        {
            this.Logger.LogDebug("Detected 'check_run' event for changed files extraction");
            return await HandleCheckRunEventAsync(payloadDocument.RootElement, repoInfo, gitHubClient, fileExtensions, maxFiles, maxFileSizeBytes);
        }
        else
        {
            this.Logger.LogWarning("Unsupported webhook event type for changed files extraction. Event has properties: {Properties}. Falling back to All Files mode.",
                string.Join(", ", payloadDocument.RootElement.EnumerateObject().Select(p => p.Name)));

            // Fall back to all files extraction for unsupported event types
            return await ExtractAllFilesFromWebhookAsync(payloadDocument, repoInfo, gitHubClient, fileExtensions, maxFiles, maxFileSizeBytes);
        }
    }

    private async Task<FileExtractionResult> ExtractAllFilesFromWebhookAsync(
        JsonDocument payloadDocument,
        RepositoryInfo repoInfo,
        IGitHubClient gitHubClient,
        string[]? fileExtensions,
        int maxFiles,
        int maxFileSizeBytes)
    {
        // Extract commit SHA from webhook payload
        string? commitSha = null;

        // Try push event first
        if (payloadDocument.RootElement.TryGetProperty("after", out var afterElement))
        {
            commitSha = afterElement.GetString();
        }
        // Try pull request event
        else if (payloadDocument.RootElement.TryGetProperty("pull_request", out var prElement) &&
                 prElement.TryGetProperty("head", out var headElement) &&
                 headElement.TryGetProperty("sha", out var shaElement))
        {
            commitSha = shaElement.GetString();
        }
        // Try check_suite event
        else if (payloadDocument.RootElement.TryGetProperty("check_suite", out var checkSuiteElement) &&
                 checkSuiteElement.TryGetProperty("head_sha", out var headShaElement))
        {
            commitSha = headShaElement.GetString();
        }
        // Try check_run event
        else if (payloadDocument.RootElement.TryGetProperty("check_run", out var checkRunElement) &&
                 checkRunElement.TryGetProperty("head_sha", out var checkRunHeadShaElement))
        {
            commitSha = checkRunHeadShaElement.GetString();
        }

        if (string.IsNullOrEmpty(commitSha) || commitSha == "0000000000000000000000000000000000000000")
        {
            this.Logger.LogWarning("No valid commit SHA found in webhook payload for all files extraction");
            return new FileExtractionResult(Array.Empty<ContextFile>(), Array.Empty<string>());
        }

        // Use existing ExtractFileContentsAsync method
        var files = await ExtractFileContentsAsync(repoInfo.Owner, repoInfo.Name, commitSha, gitHubClient, fileExtensions, maxFiles, maxFileSizeBytes);

        // Extract diffs if possible
        var diffs = await ExtractDiffsFromWebhookAsync(payloadDocument, repoInfo, gitHubClient);

        return new FileExtractionResult(files, diffs);
    }

    private async Task<FileExtractionResult> HandlePushEventAsync(
        JsonElement payload,
        RepositoryInfo repoInfo,
        IGitHubClient gitHubClient,
        string[]? fileExtensions,
        int maxFiles,
        int maxFileSizeBytes)
    {
        // UPDATED: Now uses embedded diffs pattern for better efficiency and correlation
        // Diffs are embedded directly in ContextFile.Diff properties instead of separate arrays
        var extensions = fileExtensions ?? new[] { ".cs", ".csproj", ".sln" };
        var files = new List<ContextFile>();

        string? afterSha = payload.TryGetProperty("after", out var afterElement) ? afterElement.GetString() : null;
        string? beforeSha = payload.TryGetProperty("before", out var beforeElement) ? beforeElement.GetString() : null;

        if (string.IsNullOrEmpty(afterSha) || afterSha == "0000000000000000000000000000000000000000")
        {
            this.Logger.LogInformation("No 'after' SHA found or initial push/branch deletion detected. No files to process.");
            return new FileExtractionResult(Array.Empty<ContextFile>(), Array.Empty<string>());
        }

        // Collect changed file paths from commits
        var changedFilePaths = new HashSet<string>();
        if (payload.TryGetProperty("commits", out var commitsElement) && commitsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var commitElement in commitsElement.EnumerateArray())
            {
                ExtractFilePathsFromCommit(commitElement, "added", changedFilePaths);
                ExtractFilePathsFromCommit(commitElement, "modified", changedFilePaths);
                ExtractFilePathsFromCommit(commitElement, "removed", changedFilePaths);
            }
        }

        // Filter by file extensions
        var relevantFilePaths = changedFilePaths
            .Where(path => extensions.Any(ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
            .Take(maxFiles)
            .ToList();

        this.Logger.LogInformation("Found {RelevantCount} relevant changed files out of {TotalCount} changed files",
            relevantFilePaths.Count, changedFilePaths.Count);

        // Fetch diffs if before SHA is available
        Dictionary<string, string> fileDiffs = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(beforeSha) && beforeSha != "0000000000000000000000000000000000000000")
        {
            try
            {
                var comparison = await gitHubClient.Repository.Commit.Compare(repoInfo.Owner, repoInfo.Name, beforeSha, afterSha);
                foreach (var file in comparison.Files)
                {
                    if (!string.IsNullOrEmpty(file.Patch) && relevantFilePaths.Contains(file.Filename))
                    {
                        fileDiffs[file.Filename] = file.Patch;
                    }
                }
            }
            catch (Exception ex)
            {
                this.Logger.LogWarning(ex, "Failed to fetch commit comparison for {BeforeSha}...{AfterSha}", beforeSha, afterSha);
            }
        }

        // Fetch content for each relevant file and embed diffs
        foreach (var filePath in relevantFilePaths)
        {
            try
            {
                var content = await GetFileContentAsync(repoInfo.Owner, repoInfo.Name, filePath, afterSha, gitHubClient);
                var diff = fileDiffs.TryGetValue(filePath, out var fileDiff) ? fileDiff : null;
                files.Add(new ContextFile(filePath, content, diff));
            }
            catch (NotFoundException)
            {
                // File was removed - include it with empty content but keep the diff
                this.Logger.LogDebug("File '{FilePath}' not found at {AfterSha} (likely removed)", filePath, afterSha);
                var diff = fileDiffs.TryGetValue(filePath, out var fileDiff) ? fileDiff : null;
                files.Add(new ContextFile(filePath, string.Empty, diff));
            }
            catch (Exception ex)
            {
                this.Logger.LogWarning(ex, "Failed to fetch content for file '{FilePath}'", filePath);
                var diff = fileDiffs.TryGetValue(filePath, out var fileDiff) ? fileDiff : null;
                files.Add(new ContextFile(filePath, string.Empty, diff));
            }
        }

        // Return files with embedded diffs, empty diffs array
        return new FileExtractionResult(files.ToArray(), Array.Empty<string>());
    }

    private async Task<FileExtractionResult> HandlePullRequestEventAsync(
        JsonElement payload,
        RepositoryInfo repoInfo,
        IGitHubClient gitHubClient,
        string[]? fileExtensions,
        int maxFiles,
        int maxFileSizeBytes)
    {
        // UPDATED: Now fully uses embedded diffs pattern for consistency with other optimized events
        // Diffs are embedded directly in ContextFile.Diff properties, no separate diffs array
        var extensions = fileExtensions ?? new[] { ".cs", ".csproj", ".sln" };
        var files = new List<ContextFile>();

        if (!payload.TryGetProperty("pull_request", out var prElement))
        {
            this.Logger.LogWarning("Pull request event payload missing 'pull_request' object");
            return new FileExtractionResult(Array.Empty<ContextFile>(), Array.Empty<string>());
        }

        int pullRequestNumber = prElement.TryGetProperty("number", out var numberElement) && numberElement.ValueKind == JsonValueKind.Number
            ? numberElement.GetInt32()
            : throw new InvalidOperationException("Pull request number not found in payload");

        this.Logger.LogInformation("Processing Pull Request #{Number} files", pullRequestNumber);

        try
        {
            // First, get the PR object to obtain the head SHA
            var pullRequest = await gitHubClient.PullRequest.Get(repoInfo.Id, pullRequestNumber);
            if (pullRequest is null)
            {
                this.Logger.LogWarning("Pull Request #{PullRequestNumber} not found", pullRequestNumber);
                return new FileExtractionResult(Array.Empty<ContextFile>(), Array.Empty<string>());
            }

            string prHeadSha = pullRequest.Head.Sha;
            this.Logger.LogDebug("Using PR head SHA {HeadSha} for content fetching", prHeadSha.Substring(0, 7));

            var prFiles = await gitHubClient.PullRequest.Files(repoInfo.Id, pullRequestNumber);
            var relevantFiles = prFiles
                .Where(f => extensions.Any(ext => f.FileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                .Take(maxFiles)
                .ToList();

            this.Logger.LogInformation("Found {RelevantCount} relevant files out of {TotalCount} changed files in PR #{Number}",
                relevantFiles.Count, prFiles.Count(), pullRequestNumber);

            foreach (var prFile in relevantFiles)
            {
                string content = string.Empty;

                // For removed files, content should be empty
                if (prFile.Status != "removed")
                {
                    try
                    {
                        // Use GetAllContentsByRef with the PR's head SHA and the file path
                        // This leverages the authenticated GitHubClient to fetch content from the API
                        var contents = await gitHubClient.Repository.Content.GetAllContentsByRef(
                            repoInfo.Owner, repoInfo.Name, prFile.FileName, prHeadSha);

                        // We expect a single file entry for a given path
                        var fileItem = contents?.FirstOrDefault(c => c.Type == ContentType.File);

                        if (fileItem is not null && !string.IsNullOrEmpty(fileItem.Content))
                        {
                            content = fileItem.Content; // Octokit automatically decodes Base64
                        }
                        else
                        {
                            this.Logger.LogWarning("File '{FileName}' found in PR files but content could not be retrieved via GetAllContentsByRef for PR #{PullRequestNumber}. Path might not resolve to a file, or content is empty.",
                                prFile.FileName, pullRequestNumber);
                        }
                    }
                    catch (NotFoundException)
                    {
                        this.Logger.LogWarning("File '{FileName}' not found at ref '{PrHeadSha}' (for PR #{PullRequestNumber} content fetch via GetAllContentsByRef). It might have been renamed or transiently unavailable.",
                            prFile.FileName, prHeadSha.Substring(0, 7), pullRequestNumber);
                        content = string.Empty;
                    }
                    catch (Exception ex)
                    {
                        this.Logger.LogError(ex, "Error fetching content for file '{FileName}' (PR #{PullRequestNumber}, ref '{PrHeadSha}') using GetAllContentsByRef.",
                            prFile.FileName, pullRequestNumber, prHeadSha.Substring(0, 7));
                        content = string.Empty;
                    }
                }

                // Embed diff directly in ContextFile with authenticated content
                files.Add(new ContextFile(prFile.FileName, content, prFile.Patch));
            }
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Failed to fetch pull request files for PR #{Number}", pullRequestNumber);
        }

        // Return files with embedded diffs, empty diffs array
        return new FileExtractionResult(files.ToArray(), Array.Empty<string>());
    }

    private async Task<FileExtractionResult> HandleCheckSuiteEventAsync(
        JsonElement payload,
        RepositoryInfo repoInfo,
        IGitHubClient gitHubClient,
        string[]? fileExtensions,
        int maxFiles,
        int maxFileSizeBytes)
    {
        var extensions = fileExtensions ?? new[] { ".cs", ".csproj", ".sln" };

        if (!payload.TryGetProperty("check_suite", out var checkSuiteElement))
        {
            this.Logger.LogWarning("Check suite event payload missing 'check_suite' object");
            return new FileExtractionResult(Array.Empty<ContextFile>(), Array.Empty<string>());
        }

        string? headSha = checkSuiteElement.TryGetProperty("head_sha", out var headShaElement) ? headShaElement.GetString() : null;
        if (string.IsNullOrEmpty(headSha))
        {
            this.Logger.LogWarning("Check suite event missing 'head_sha'");
            return new FileExtractionResult(Array.Empty<ContextFile>(), Array.Empty<string>());
        }

        this.Logger.LogInformation("Processing check_suite for head_sha: {HeadSha}", headSha.Substring(0, 7));

        // Check if the check_suite is explicitly linked to Pull Requests
        if (checkSuiteElement.TryGetProperty("pull_requests", out var prsArray) &&
            prsArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var prRefElement in prsArray.EnumerateArray())
            {
                if (prRefElement.TryGetProperty("number", out var prNumberElement) &&
                    prNumberElement.ValueKind == JsonValueKind.Number)
                {
                    int prNumber = prNumberElement.GetInt32();
                    this.Logger.LogInformation("Check suite for {HeadSha} is associated with Pull Request #{PRNumber}. Processing as PR changed files.",
                        headSha.Substring(0, 7), prNumber);

                    return await GetFilesFromPullRequestAsync(repoInfo, gitHubClient, prNumber, extensions, maxFiles, maxFileSizeBytes);
                }
            }
        }

        // If not explicitly linked to a PR, find associated PR by head SHA
        this.Logger.LogDebug("No explicit PR link in check_suite payload. Searching for PR with head SHA {HeadSha}", headSha.Substring(0, 7));
        try
        {
            var pullRequests = await gitHubClient.Repository.PullRequest.GetAllForRepository(repoInfo.Id,
                new PullRequestRequest { State = ItemStateFilter.Open });

            var associatedPr = pullRequests.FirstOrDefault(pr => pr.Head.Sha == headSha);
            if (associatedPr is not null)
            {
                this.Logger.LogInformation("Found associated PR #{Number} for check_suite with head SHA {HeadSha}. Processing as PR changed files.",
                    associatedPr.Number, headSha.Substring(0, 7));

                return await GetFilesFromPullRequestAsync(repoInfo, gitHubClient, associatedPr.Number, extensions, maxFiles, maxFileSizeBytes);
            }
        }
        catch (Exception ex)
        {
            this.Logger.LogWarning(ex, "Failed to search for associated PR for check_suite head SHA {HeadSha}", headSha.Substring(0, 7));
        }

        // Fallback to commit-level diff if no PR association found
        this.Logger.LogInformation("No PR association found for check_suite head SHA {HeadSha}. Processing as commit-level changes.", headSha.Substring(0, 7));
        return await GetFilesFromCommitDiffAsync(repoInfo, gitHubClient, headSha, extensions, maxFiles, maxFileSizeBytes);
    }

    private async Task<FileExtractionResult> HandleCheckRunEventAsync(
        JsonElement payload,
        RepositoryInfo repoInfo,
        IGitHubClient gitHubClient,
        string[]? fileExtensions,
        int maxFiles,
        int maxFileSizeBytes)
    {
        var extensions = fileExtensions ?? new[] { ".cs", ".csproj", ".sln" };

        if (!payload.TryGetProperty("check_run", out var checkRunElement))
        {
            this.Logger.LogWarning("Check run event payload missing 'check_run' object");
            return new FileExtractionResult(Array.Empty<ContextFile>(), Array.Empty<string>());
        }

        string? headSha = checkRunElement.TryGetProperty("head_sha", out var headShaElement) ? headShaElement.GetString() : null;
        if (string.IsNullOrEmpty(headSha))
        {
            this.Logger.LogWarning("Check run event missing 'head_sha'");
            return new FileExtractionResult(Array.Empty<ContextFile>(), Array.Empty<string>());
        }

        this.Logger.LogInformation("Processing check_run for head_sha: {HeadSha}", headSha.Substring(0, 7));

        // Check if the check_run is explicitly linked to Pull Requests
        if (checkRunElement.TryGetProperty("pull_requests", out var prsArray) &&
            prsArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var prRefElement in prsArray.EnumerateArray())
            {
                if (prRefElement.TryGetProperty("number", out var prNumberElement) &&
                    prNumberElement.ValueKind == JsonValueKind.Number)
                {
                    int prNumber = prNumberElement.GetInt32();
                    this.Logger.LogInformation("Check run for {HeadSha} is associated with Pull Request #{PRNumber}. Processing as PR changed files.",
                        headSha.Substring(0, 7), prNumber);

                    return await GetFilesFromPullRequestAsync(repoInfo, gitHubClient, prNumber, extensions, maxFiles, maxFileSizeBytes);
                }
            }
        }

        // If not explicitly linked to a PR, find associated PR by head SHA
        this.Logger.LogDebug("No explicit PR link in check_run payload. Searching for PR with head SHA {HeadSha}", headSha.Substring(0, 7));
        try
        {
            var pullRequests = await gitHubClient.Repository.PullRequest.GetAllForRepository(repoInfo.Id,
                new PullRequestRequest { State = ItemStateFilter.Open });

            var associatedPr = pullRequests.FirstOrDefault(pr => pr.Head.Sha == headSha);
            if (associatedPr is not null)
            {
                this.Logger.LogInformation("Found associated PR #{Number} for check_run with head SHA {HeadSha}. Processing as PR changed files.",
                    associatedPr.Number, headSha.Substring(0, 7));

                return await GetFilesFromPullRequestAsync(repoInfo, gitHubClient, associatedPr.Number, extensions, maxFiles, maxFileSizeBytes);
            }
        }
        catch (Exception ex)
        {
            this.Logger.LogWarning(ex, "Failed to search for associated PR for check_run head SHA {HeadSha}", headSha.Substring(0, 7));
        }

        // Fallback to commit-level diff if no PR association found
        this.Logger.LogInformation("No PR association found for check_run head SHA {HeadSha}. Processing as commit-level changes.", headSha.Substring(0, 7));
        return await GetFilesFromCommitDiffAsync(repoInfo, gitHubClient, headSha, extensions, maxFiles, maxFileSizeBytes);
    }

    private async Task<FileExtractionResult> GetFilesFromPullRequestAsync(
        RepositoryInfo repoInfo,
        IGitHubClient gitHubClient,
        int pullRequestNumber,
        string[] extensions,
        int maxFiles,
        int maxFileSizeBytes)
    {
        var files = new List<ContextFile>();

        this.Logger.LogInformation("Fetching changed files for Pull Request #{PullRequestNumber}", pullRequestNumber);

        try
        {
            // First, get the PR object to obtain the head SHA
            var pullRequest = await gitHubClient.PullRequest.Get(repoInfo.Id, pullRequestNumber);
            if (pullRequest is null)
            {
                this.Logger.LogWarning("Pull Request #{PullRequestNumber} not found", pullRequestNumber);
                return new FileExtractionResult(Array.Empty<ContextFile>(), Array.Empty<string>());
            }

            string prHeadSha = pullRequest.Head.Sha;
            this.Logger.LogDebug("Using PR head SHA {HeadSha} for content fetching", prHeadSha.Substring(0, 7));

            var prFiles = await gitHubClient.PullRequest.Files(repoInfo.Id, pullRequestNumber);
            var relevantFiles = prFiles
                .Where(f => extensions.Any(ext => f.FileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                .Take(maxFiles)
                .ToList();

            this.Logger.LogInformation("Found {RelevantCount} relevant files out of {TotalCount} changed files in PR #{Number}",
                relevantFiles.Count, prFiles.Count(), pullRequestNumber);

            foreach (var prFile in relevantFiles)
            {
                string content = string.Empty;

                // For removed files, content should be empty
                if (prFile.Status != "removed")
                {
                    try
                    {
                        // Use GetAllContentsByRef with the PR's head SHA and the file path
                        // This leverages the authenticated GitHubClient to fetch content from the API
                        var contents = await gitHubClient.Repository.Content.GetAllContentsByRef(
                            repoInfo.Owner, repoInfo.Name, prFile.FileName, prHeadSha);

                        // We expect a single file entry for a given path
                        var fileItem = contents?.FirstOrDefault(c => c.Type == ContentType.File);

                        if (fileItem is not null && !string.IsNullOrEmpty(fileItem.Content))
                        {
                            content = fileItem.Content; // Octokit automatically decodes Base64
                        }
                        else
                        {
                            this.Logger.LogWarning("File '{FileName}' found in PR files but content could not be retrieved via GetAllContentsByRef for PR #{PullRequestNumber}. Path might not resolve to a file, or content is empty.",
                                prFile.FileName, pullRequestNumber);
                        }
                    }
                    catch (NotFoundException)
                    {
                        this.Logger.LogWarning("File '{FileName}' not found at ref '{PrHeadSha}' (for PR #{PullRequestNumber} content fetch via GetAllContentsByRef). It might have been renamed or transiently unavailable.",
                            prFile.FileName, prHeadSha.Substring(0, 7), pullRequestNumber);
                        content = string.Empty;
                    }
                    catch (Exception ex)
                    {
                        this.Logger.LogError(ex, "Error fetching content for file '{FileName}' (PR #{PullRequestNumber}, ref '{PrHeadSha}') using GetAllContentsByRef.",
                            prFile.FileName, pullRequestNumber, prHeadSha.Substring(0, 7));
                        content = string.Empty;
                    }
                }
                else
                {
                    this.Logger.LogInformation("File '{FileName}' was removed in PR #{PullRequestNumber}. Content will be empty.",
                        prFile.FileName, pullRequestNumber);
                }

                files.Add(new ContextFile(prFile.FileName, content, prFile.Patch));
            }
        }
        catch (NotFoundException)
        {
            this.Logger.LogWarning("Pull Request #{PullRequestNumber} or its files not found", pullRequestNumber);
            return new FileExtractionResult(Array.Empty<ContextFile>(), Array.Empty<string>());
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error fetching pull request details or files for PR #{Number}", pullRequestNumber);
            return new FileExtractionResult(Array.Empty<ContextFile>(), Array.Empty<string>());
        }

        return new FileExtractionResult(files.ToArray(), Array.Empty<string>());
    }

    private async Task<FileExtractionResult> GetFilesFromCommitDiffAsync(
        RepositoryInfo repoInfo,
        IGitHubClient gitHubClient,
        string headSha,
        string[] extensions,
        int maxFiles,
        int maxFileSizeBytes)
    {
        // TODO: ARCHITECTURAL NOTE - Diff Handling Difference
        // This commit diff method uses the legacy pattern of separate Files and Diffs arrays.
        // The GetFilesFromPullRequestAsync method uses the newer pattern where diffs are embedded
        // directly in ContextFile.Diff properties for better efficiency and correlation.
        // See HandlePushEventAsync for migration guidance if updating this method.

        var files = new List<ContextFile>();
        var diffs = new List<string>();

        this.Logger.LogInformation("Attempting to get commit-level diffs for {HeadSha}", headSha.Substring(0, 7));

        try
        {
            var commit = await gitHubClient.Repository.Commit.Get(repoInfo.Owner, repoInfo.Name, headSha);
            if (commit.Parents is null || !commit.Parents.Any())
            {
                this.Logger.LogInformation("Commit {HeadSha} has no parents (initial commit). No diffs to extract.", headSha.Substring(0, 7));
                return new FileExtractionResult(Array.Empty<ContextFile>(), Array.Empty<string>());
            }

            string parentSha = commit.Parents[0].Sha;
            this.Logger.LogDebug("Comparing commit {HeadSha} with its parent {ParentSha}", headSha.Substring(0, 7), parentSha.Substring(0, 7));

            var comparison = await gitHubClient.Repository.Commit.Compare(repoInfo.Owner, repoInfo.Name, parentSha, headSha);
            var relevantFiles = comparison.Files
                .Where(f => extensions.Any(ext => f.Filename.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                .Take(maxFiles)
                .ToList();

            this.Logger.LogInformation("Found {RelevantCount} relevant changed files out of {TotalCount} total changed files",
                relevantFiles.Count, comparison.Files.Count());

            foreach (var file in relevantFiles)
            {
                string content = string.Empty;
                if (file.Status != "removed")
                {
                    try
                    {
                        content = await GetFileContentAsync(repoInfo.Owner, repoInfo.Name, file.Filename, headSha, gitHubClient);
                    }
                    catch (NotFoundException)
                    {
                        this.Logger.LogWarning("File '{FilePath}' not found at {HeadSha} (likely removed)", file.Filename, headSha.Substring(0, 7));
                    }
                    catch (Exception ex)
                    {
                        this.Logger.LogWarning(ex, "Failed to fetch content for file '{FilePath}'", file.Filename);
                    }
                }

                files.Add(new ContextFile(file.Filename, content));

                if (!string.IsNullOrEmpty(file.Patch))
                {
                    diffs.Add(file.Patch);
                }
            }
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error getting commit-level diffs for {HeadSha}", headSha.Substring(0, 7));
            return new FileExtractionResult(Array.Empty<ContextFile>(), Array.Empty<string>());
        }

        return new FileExtractionResult(files.ToArray(), diffs.ToArray());
    }

    private void ExtractFilePathsFromCommit(JsonElement commitElement, string propertyName, HashSet<string> filePaths)
    {
        if (commitElement.TryGetProperty(propertyName, out var filesArray) && filesArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var fileElement in filesArray.EnumerateArray())
            {
                var filePath = fileElement.GetString();
                if (!string.IsNullOrEmpty(filePath))
                {
                    filePaths.Add(filePath);
                }
            }
        }
    }

    private async Task<string[]> ExtractDiffsFromWebhookAsync(JsonDocument payloadDocument, RepositoryInfo repoInfo, IGitHubClient gitHubClient)
    {
        var diffs = new List<string>();

        try
        {
            // Try push event
            if (payloadDocument.RootElement.TryGetProperty("before", out var beforeElement) &&
                payloadDocument.RootElement.TryGetProperty("after", out var afterElement))
            {
                var beforeSha = beforeElement.GetString();
                var afterSha = afterElement.GetString();

                if (!string.IsNullOrEmpty(beforeSha) && !string.IsNullOrEmpty(afterSha) &&
                    beforeSha != "0000000000000000000000000000000000000000")
                {
                    var comparison = await gitHubClient.Repository.Commit.Compare(repoInfo.Owner, repoInfo.Name, beforeSha, afterSha);
                    diffs.AddRange(comparison.Files.Where(f => !string.IsNullOrEmpty(f.Patch)).Select(f => f.Patch));
                }
            }
            // Try pull request event
            else if (payloadDocument.RootElement.TryGetProperty("pull_request", out var prElement) &&
                     prElement.TryGetProperty("number", out var numberElement))
            {
                var prNumber = numberElement.GetInt32();
                var prFiles = await gitHubClient.PullRequest.Files(repoInfo.Id, prNumber);
                diffs.AddRange(prFiles.Where(f => !string.IsNullOrEmpty(f.Patch)).Select(f => f.Patch));
            }
            // Try check_suite event - find associated PR and extract its diffs
            else if (payloadDocument.RootElement.TryGetProperty("check_suite", out var checkSuiteElement))
            {
                this.Logger.LogDebug("Attempting to extract diffs from check_suite event");

                // Get the head SHA from the check suite
                if (checkSuiteElement.TryGetProperty("head_sha", out var headShaElement))
                {
                    var headSha = headShaElement.GetString();
                    if (!string.IsNullOrEmpty(headSha))
                    {
                        // Find PR associated with this commit SHA
                        var pullRequests = await gitHubClient.Repository.PullRequest.GetAllForRepository(repoInfo.Id,
                            new PullRequestRequest { State = ItemStateFilter.Open });

                        var associatedPr = pullRequests.FirstOrDefault(pr => pr.Head.Sha == headSha);
                        if (associatedPr is not null)
                        {
                            this.Logger.LogInformation("Found associated PR #{Number} for check_suite with head SHA {HeadSha}",
                                associatedPr.Number, headSha.Substring(0, 7));

                            var prFiles = await gitHubClient.PullRequest.Files(repoInfo.Id, associatedPr.Number);
                            diffs.AddRange(prFiles.Where(f => !string.IsNullOrEmpty(f.Patch)).Select(f => f.Patch));
                        }
                        else
                        {
                            this.Logger.LogDebug("No open PR found for check_suite head SHA {HeadSha}", headSha.Substring(0, 7));
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            this.Logger.LogWarning(ex, "Failed to extract diffs from webhook payload");
        }

        return diffs.ToArray();
    }

    private async Task<string> GetFileContentAsync(string owner, string repoName, string filePath, string gitRef, IGitHubClient gitHubClient)
    {
        var contents = await gitHubClient.Repository.Content.GetAllContentsByRef(owner, repoName, filePath, gitRef);
        var fileContent = contents.FirstOrDefault(c => c.Type == ContentType.File);

        if (fileContent is null || string.IsNullOrEmpty(fileContent.Content))
        {
            throw new NotFoundException($"File content not found for '{filePath}' at ref '{gitRef}'", System.Net.HttpStatusCode.NotFound);
        }

        return fileContent.Content;
    }

    /// <summary>
    /// Simple heuristic to detect binary data in file content
    /// </summary>
    private static bool ContainsBinaryData(string content)
    {
        // Check for null bytes or high percentage of non-printable characters
        if (content.Contains('\0'))
            return true;

        var nonPrintableCount = content.Count(c => char.IsControl(c) && c != '\r' && c != '\n' && c != '\t');
        var nonPrintableRatio = (double)nonPrintableCount / content.Length;

        // If more than 10% non-printable characters, likely binary
        return nonPrintableRatio > 0.1;
    }

    #endregion
}
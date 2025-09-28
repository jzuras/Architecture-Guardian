using ArchGuard.Shared;
using Octokit;
using System.Text.Json;

namespace ArchGuard.MCP.Services;

public record FileExtractionResult(
    ContextFile[] Files,
    string[] Diffs);

/// <summary>
/// Service for extracting file contents from GitHub repositories via webhook payloads
/// </summary>
public interface IGitHubFileContentService
{
    /// <summary>
    /// Extracts file contents from GitHub repository for LocalFoundry validation
    /// </summary>
    /// <param name="repoOwner">Repository owner</param>
    /// <param name="repoName">Repository name</param>
    /// <param name="commitSha">Commit SHA to fetch files from</param>
    /// <param name="gitHubClient">Authenticated GitHub client</param>
    /// <param name="fileExtensions">File extensions to include (e.g., [".cs", ".csproj"])</param>
    /// <param name="maxFiles">Maximum number of files to fetch (default 50)</param>
    /// <param name="maxFileSizeBytes">Maximum file size in bytes (default 100KB)</param>
    /// <returns>Array of ContextFile objects with file paths and contents</returns>
    Task<ContextFile[]> ExtractFileContentsAsync(
        string repoOwner,
        string repoName,
        string commitSha,
        IGitHubClient gitHubClient,
        string[]? fileExtensions = null,
        int maxFiles = 50,
        int maxFileSizeBytes = 100 * 1024);

    /// <summary>
    /// Extracts changed file contents and diffs from GitHub webhook payload
    /// Supports both "all files" and "changed files only" modes based on configuration
    /// </summary>
    /// <param name="webhookPayloadJson">Raw webhook payload JSON</param>
    /// <param name="gitHubClient">Authenticated GitHub client</param>
    /// <param name="fileExtensions">File extensions to include (e.g., [".cs", ".csproj"])</param>
    /// <param name="maxFiles">Maximum number of files to fetch (default 50)</param>
    /// <param name="maxFileSizeBytes">Maximum file size in bytes (default 100KB)</param>
    /// <returns>FileExtractionResult containing files and diffs</returns>
    Task<FileExtractionResult> ExtractFromWebhookAsync(
        string webhookPayloadJson,
        IGitHubClient gitHubClient,
        string[]? fileExtensions = null,
        int maxFiles = 50,
        int maxFileSizeBytes = 100 * 1024);
}
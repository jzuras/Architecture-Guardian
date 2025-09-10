using ArchGuard.Shared;
using ArchGuard.MCP.Models;
using Octokit;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArchGuard.MCP.Services;

public class GitHubCheckService
{
    public static string DependencyRegistrationCheckName { get; } = "Validate Dependency Registration Check";

    // Note: Annotations do not seem to be clearable according to the web and from what I saw in practice,
    // so I am using a flag here to make it easy to start using them if I need to do so in the future.
    // Also note - these need repo-relative filenames for the GH website to show them in files - not yet tested.
    public bool UseAnnotations { get; set; } = false;

    private ILogger<GitHubCheckService> Logger { get; set; }

    public GitHubCheckService(ILogger<GitHubCheckService> logger)
    {
        this.Logger = logger;
    }

    public async Task ExecuteDepInjectionCheckAsync(CheckExecutionArgs args, string root, long githubInstallationId, IGitHubClient githubClient)
    {
        try
        {
            long checkRunId;

            // --- Step 1: Create or Update Check Run to 'in_progress' ---
            try
            {
                if (args.ExistingCheckRunId.HasValue)
                {
                    // Update existing check run
                    checkRunId = args.ExistingCheckRunId.Value;
                    var updateCheckRun = new CheckRunUpdate()
                    {
                        Status = CheckStatus.InProgress,
                        StartedAt = DateTimeOffset.UtcNow,
                        Output = new NewCheckRunOutput(args.InitialTitle, args.InitialSummary) // Use args for initial output
                        {
                            // --- Explicitly clear annotations and text/images ---
                            Text = "The check has been re-requested and is now in progress. Previous detailed output has been cleared.",
                            Annotations = new List<NewCheckRunAnnotation>(), // Explicitly send an empty list
                            Images = new List<NewCheckRunImage>()          // Explicitly send an empty list
                        }
                    };
                    await githubClient.Check.Run.Update(args.RepoOwner, args.RepoName, checkRunId, updateCheckRun);
                    this.Logger.LogInformation("Updated Check Run '{CheckName}' (ID: {CheckRunId}) to 'in_progress'.", args.CheckName, checkRunId);
                }
                else
                {
                    // Create a new check run
                    var newCheckRun = new NewCheckRun(args.CheckName, args.CommitSha)
                    {
                        Status = CheckStatus.InProgress,
                        StartedAt = DateTimeOffset.UtcNow,
                        DetailsUrl = "https://example.com/details/di-check", // Your custom details URL
                        Output = new NewCheckRunOutput(args.InitialTitle, args.InitialSummary)
                        {
                            // --- Explicitly empty annotations and text/images for new run ---
                            Text = "The check has been queued and is now running...",
                            Annotations = new List<NewCheckRunAnnotation>(),
                            Images = new List<NewCheckRunImage>()
                        }
                    };
                    var createdCheck = await githubClient.Check.Run.Create(args.RepoOwner, args.RepoName, newCheckRun);
                    checkRunId = createdCheck.Id;
                    this.Logger.LogInformation("Created new Check Run '{CheckName}' (ID: {CheckRunId}) for commit {CommitSha}...", args.CheckName, checkRunId, args.CommitSha.Substring(0, 7));
                }
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex, "Failed to create/update Check Run '{CheckName}' for commit {CommitSha} to 'in_progress'.", args.CheckName, args.CommitSha.Substring(0, 7));
                // Consider updating to 'failure' or 'cancelled' if initial setup fails
                return;
            }

            // 2. Perform AI analysis
            var aiResultJsonString = await ValidationService.ValidateDependencyRegistrationAsync(root, Array.Empty<ContextFile>());
            AiCheckResult aiResult;
            Console.WriteLine("aiResultJsonString");
            Console.WriteLine(aiResultJsonString);

            #region Parse Claude Code Result String
            try
            {
                using var doc = JsonDocument.Parse(aiResultJsonString);

                if (doc.RootElement.ValueKind == JsonValueKind.String)
                {
                    // If the root element is a JSON string literal,
                    // its *value* is the actual JSON we want to deserialize.
                    string innerJson = doc.RootElement.GetString() ?? throw new InvalidOperationException("AI result inner JSON string value was null.");
                    aiResult = JsonSerializer.Deserialize<AiCheckResult>(innerJson)
                               ?? throw new InvalidOperationException("Failed to deserialize AI check result from inner JSON string.");
                    this.Logger.LogDebug("Successfully unwrapped and deserialized AI result from a string literal.");
                }
                else if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    // If the root element is directly a JSON object,
                    // proceed with deserialization as normal.
                    aiResult = JsonSerializer.Deserialize<AiCheckResult>(aiResultJsonString)
                               ?? throw new InvalidOperationException("Failed to deserialize AI check result directly from JSON object.");
                    this.Logger.LogDebug("Successfully deserialized AI result directly from JSON object.");
                }
                else
                {
                    // Handle other unexpected JSON root types
                    throw new JsonException($"Unexpected JSON root element type for AI result: {doc.RootElement.ValueKind}. Expected String or Object.");
                }
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex, "Failed to parse AI result JSON for Check Run '{CheckName}' (ID: {CheckRunId}). Raw string: {RawAIResult}", args.CheckName, checkRunId, aiResultJsonString);
                // Fallback to a failure conclusion if AI result parsing fails
                aiResult = new AiCheckResult { Passed = false, Explanation = "Failed to parse AI analysis results due to JSON format error." };
            }

            // --- Step 3: Determine Conclusion and prepare output based on AI result ---
            CheckConclusion checkConclusion = aiResult.Passed ? CheckConclusion.Success : CheckConclusion.Failure;
            string outputTitle = aiResult.Passed ? "Dependency Registration Check Passed" : "Dependency Registration Check Failed";
            string outputSummary = aiResult.Explanation; // Use explanation as summary
            List<NewCheckRunAnnotation> annotations = new List<NewCheckRunAnnotation>();
            string outputText = string.Empty; // Build this separately for detailed text
            #endregion

            this.Logger.LogInformation("Returning check pass to GH (true/false): {result}", aiResult.Passed);

            if (aiResult.Violations.Count != 0 && this.UseAnnotations is true)
            {
                // Create annotations for each violation
                foreach (var violation in aiResult.Violations)
                {
                    string annotationFilePath = GetRelativeRepoPathForAnnotation(violation.File, args.RepoName);

                    annotations.Add(new NewCheckRunAnnotation(
                        path: annotationFilePath,
                        startLine: violation.Line,
                        endLine: violation.Line,
                        annotationLevel: CheckAnnotationLevel.Failure, // Or Warning, depending on severity
                        message: violation.Message)
                    {
                        Title = "DI Registration Violation",
                        RawDetails = $"File: {violation.File}, Line: {violation.Line}\nMessage: {violation.Message}"
                    });
                }

                outputText = "**Detected Violations:**\n\n" +
                             string.Join("\n", aiResult.Violations.Select(v => {
                                 string relativePath = GetRelativeRepoPathForAnnotation(v.File, args.RepoName);
                                 // If the relative path couldn't be determined, fall back to the original full path with a note
                                 string displayPath = string.IsNullOrEmpty(relativePath) ? $"{v.File} (absolute path)" : $"`{relativePath}`";
                                 return $"- **File:** {displayPath} (Line: {v.Line}): {v.Message}";
                             }));
            }

            // Add the overall explanation as part of the text
            outputText = (string.IsNullOrEmpty(outputText) ? "No dependency registration violations were detected." : outputText + "\n\n") + aiResult.Explanation;


            // --- Step 4: Update Check Run to 'completed' with the final conclusion and output ---
            try
            {
                var updateCheckRun = new CheckRunUpdate()
                {
                    Status = CheckStatus.Completed,
                    Conclusion = checkConclusion,
                    CompletedAt = DateTimeOffset.UtcNow,
                    Output = new NewCheckRunOutput(outputTitle, outputSummary)
                    {
                        Text = outputText,
                        Annotations = annotations,
                        // Images = new List<NewCheckRunImage> { ... } // Add images if needed, will also replace previous images
                    }
                };

                await githubClient.Check.Run.Update(args.RepoOwner, args.RepoName, checkRunId, updateCheckRun);
                
                this.Logger.LogInformation("Check run '{CheckName}' (ID: {CheckRunId}) completed with conclusion '{Conclusion}'.", args.CheckName, checkRunId, checkConclusion);
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex, "Failed to update Check Run '{CheckName}' (ID: {CheckRunId}) to 'completed'.", args.CheckName, checkRunId);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("An error occurred .");
            Console.WriteLine(ex.Message);
            return;
        }
    }

    /// <summary>
    /// Converts an absolute file path (from AI results, potentially WSL or Windows format)
    /// into a repository-relative path suitable for GitHub Check Annotations.
    /// </summary>
    /// <param name="absoluteFilePath">The absolute file path reported by the AI tool.</param>
    /// <param name="repositoryName">The name of the repository (e.g., "RulesDemo").</param>
    /// <returns>A repository-relative path (e.g., "src/Folder/File.cs") or empty string if it cannot be determined.</returns>
    private string GetRelativeRepoPathForAnnotation(string absoluteFilePath, string repositoryName)
    {
        if (string.IsNullOrWhiteSpace(absoluteFilePath) || string.IsNullOrWhiteSpace(repositoryName))
        {
            this.Logger.LogWarning("Cannot determine relative path: absoluteFilePath or repositoryName is empty. Path: '{FilePath}', Repo: '{RepoName}'", absoluteFilePath, repositoryName);
            return string.Empty;
        }

        // 1. Normalize path separators to forward slashes for consistency
        string normalizedPath = absoluteFilePath.Replace('\\', '/');
        string normalizedRepoName = repositoryName.Replace('\\', '/'); // Ensure repo name also uses '/' if it came in with '\'

        // 2. Try to find the repository name in the path, ensuring it's followed by a slash.
        //    We look for the *last* occurrence to handle cases where the repo name might appear in parent directories.
        int repoIndex = normalizedPath.LastIndexOf($"{normalizedRepoName}/", StringComparison.OrdinalIgnoreCase);

        if (repoIndex != -1)
        {
            // If "RepoName/" is found, take everything after it.
            // +1 accounts for the trailing slash after the repo name.
            return normalizedPath.Substring(repoIndex + normalizedRepoName.Length + 1);
        }
        else
        {
            // Case: The file itself is the repository folder (e.g. `C:/repo/`) which is not a file
            // Or the file is directly in the repo root without a trailing slash explicitly in the path
            // (e.g., `/path/to/RulesDemo/Program.cs` where `RulesDemo` is not followed by a `/` at `LastIndexOf`).
            //
            // Let's check if the path ends exactly with the repository name (e.g., "/path/to/RulesDemo")
            // This is unlikely for an annotation file path, but good to handle.
            if (normalizedPath.EndsWith(normalizedRepoName, StringComparison.OrdinalIgnoreCase))
            {
                this.Logger.LogWarning("Annotation path '{FilePath}' matches repository root '{RepoName}' exactly. No file path can be extracted for annotation.", absoluteFilePath, repositoryName);
                return string.Empty; // This isn't a file within the repo.
            }
            // Another potential scenario: The file is in the root of the repo, e.g., `RulesDemo/Program.cs`
            // and `normalizedPath` itself is just `Program.cs` without the full absolute path from AI.
            // This method assumes the AI provides absolute paths.

            // Fallback: If the repository name (followed by a slash) wasn't found,
            // it means the path doesn't conform to the expected "repo_root/repo_name/file" structure.
            // We'll log a warning and return an empty string, or the full path (though GitHub might reject full path).
            // Returning empty string is safer as GitHub won't display a bad annotation.
            this.Logger.LogWarning("Could not find repository name '{RepoName}' followed by a slash within the annotation path '{FilePath}'. Returning empty string.", repositoryName, absoluteFilePath);
            return string.Empty;
        }
    }

    public class AiCheckResult
    {
        [JsonPropertyName("passed")]
        public bool Passed { get; set; }

        [JsonPropertyName("violations")]
        public List<AiCheckViolation> Violations { get; set; } = new List<AiCheckViolation>();

        [JsonPropertyName("explanation")]
        public string Explanation { get; set; } = string.Empty;
    }

    public class AiCheckViolation
    {
        [JsonPropertyName("file")]
        public string File { get; set; } = string.Empty;

        [JsonPropertyName("line")]
        public int Line { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;
    }
}
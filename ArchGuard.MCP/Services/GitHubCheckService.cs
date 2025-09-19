using ArchGuard.MCP.Models;
using ArchGuard.Shared;
using Octokit;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArchGuard.MCP.Services;

public class GitHubCheckService
{
    // ARCHGUARD_TEMPLATE_CONSTANT_START
    // TEMPLATE_CHECK_NAME_CONSTANT: DependencyRegistrationDetailsUrlForCheck
    public static string DependencyRegistrationDetailsUrlForCheck = "https://example.com/details/di-check";
    // ARCHGUARD_TEMPLATE_CONSTANT_END

    // ARCHGUARD_INSERTION_POINT_CONSTANTS_START
    // New rule constants go here in alphabetical order by rule name

    // ARCHGUARD_GENERATED_RULE_START - ValidateEntityDtoPropertyMapping
    // Generated from template on: 9/17/25
    // DO NOT EDIT - This code will be regenerated
    public static string EntityDtoPropertyMappingDetailsUrlForCheck = "https://example.com/details/dto-check";
    // ARCHGUARD_GENERATED_RULE_END - ValidateEntityDtoPropertyMapping

    // ARCHGUARD_INSERTION_POINT_CONSTANTS_END

    public bool UseAnnotations { get; set; } = true;

    private ILogger<GitHubCheckService> Logger { get; set; }

    public GitHubCheckService(ILogger<GitHubCheckService> logger)
    {
        this.Logger = logger;
    }

    /// <summary>
    /// Creates a new GitHub check run in "queued" status for immediate visibility.
    /// Used in Phase 1 of webhook handling to show "X of Y checks" in GitHub UI.
    /// </summary>
    public async Task<long> CreateCheckAsync(string checkName, CheckExecutionArgs args, IGitHubClient gitHubClient, string detailsUrl)
    {
        try
        {
            var newCheckRun = new NewCheckRun(checkName, args.CommitSha)
            {
                Status = CheckStatus.Queued,
                DetailsUrl = detailsUrl,
                Output = new NewCheckRunOutput(args.InitialTitle, args.InitialSummary)
                {
                    Text = "Check has been queued and will begin execution shortly...",
                    Annotations = new List<NewCheckRunAnnotation>(),
                    Images = new List<NewCheckRunImage>()
                }
            };

            var createdCheck = await gitHubClient.Check.Run.Create(args.RepoOwner, args.RepoName, newCheckRun);

            this.Logger.LogInformation("Created Check Run '{CheckName}' (ID: {CheckRunId}) for commit {CommitSha}",
                checkName, createdCheck.Id, args.CommitSha.Substring(0, 7));

            return createdCheck.Id;
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Failed to create Check Run '{CheckName}' for commit {CommitSha}",
                checkName, args.CommitSha.Substring(0, 7));
            throw;
        }
    }

    // ARCHGUARD_TEMPLATE_CHECK_METHOD_START
    // TEMPLATE_CHECK_METHOD_NAME: ExecuteDepInjectionCheckAsync
    // TEMPLATE_VALIDATION_SERVICE_METHOD: ValidationService.ValidateDependencyRegistrationAsync
    // TEMPLATE_CHECK_NAME_REFERENCE: DependencyRegistrationCheckName
    public async Task ExecuteDepInjectionCheckAsync(CheckExecutionArgs args, string windowsRoot, string wslRoot, long githubInstallationId, IGitHubClient githubClient, long? existingCheckId = null)
    {
        try
        {
            long checkRunId;

            // --- Step 1: Create Check Run (updating existing does not seem to be reflected on GH GUI) ---
            try
            {
                // Create a new check run
                var newCheckRun = new NewCheckRun(args.CheckName, args.CommitSha)
                {
                    Status = CheckStatus.InProgress,
                    StartedAt = DateTimeOffset.UtcNow,
                    DetailsUrl = GitHubCheckService.DependencyRegistrationDetailsUrlForCheck,
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
            catch (Exception ex)
            {
                this.Logger.LogError(ex, "Failed to create/update Check Run '{CheckName}' for commit {CommitSha} to 'in_progress'.", args.CheckName, args.CommitSha.Substring(0, 7));
                // Consider updating to 'failure' or 'cancelled' if initial setup fails
                return;
            }

            // 2. Perform AI analysis
            var aiResultJsonString = await ValidationService.ValidateDependencyRegistrationAsync(windowsRoot, wslRoot, Array.Empty<ContextFile>());
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
            string outputTitle = aiResult.Passed ? ValidationService.DependencyRegistrationCheckName + " Check Passed" : ValidationService.DependencyRegistrationCheckName + " Check Failed";
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

                    // Skip this annotation if path resolution failed
                    if (string.IsNullOrEmpty(annotationFilePath))
                    {
                        this.Logger.LogWarning("Skipping annotation for violation at '{FilePath}' - could not resolve relative path", violation.File);
                        continue;
                    }

                    annotations.Add(new NewCheckRunAnnotation(
                        path: annotationFilePath,
                        startLine: violation.Line,
                        endLine: violation.Line,
                        annotationLevel: CheckAnnotationLevel.Failure, // Or Warning, depending on severity
                        message: violation.Message)
                    {
                        Title = ValidationService.DependencyRegistrationCheckName + " Violation",
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
            outputText = (string.IsNullOrEmpty(outputText) ? "No " + ValidationService.DependencyRegistrationCheckName + " violations were detected." : outputText + "\n\n") + aiResult.Explanation;


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
    // ARCHGUARD_TEMPLATE_CHECK_METHOD_END

    // ARCHGUARD_INSERTION_POINT_METHODS_START
    // New rule check methods go here in alphabetical order by rule name

    // ARCHGUARD_GENERATED_RULE_START - ValidateEntityDtoPropertyMapping
    // Generated from template on: 9/17/25
    // DO NOT EDIT - This code will be regenerated
    public async Task ExecuteEntityDtoPropertyMappingCheckAsync(CheckExecutionArgs args, string windowsRoot, string wslRoot, long githubInstallationId, IGitHubClient githubClient, long? existingCheckId = null)
    {
        try
        {
            long checkRunId;

            // --- Step 1: Create Check Run (updating existing does not seem to be reflected on GH GUI) ---
            try
            {
                // Create a new check run
                var newCheckRun = new NewCheckRun(args.CheckName, args.CommitSha)
                {
                    Status = CheckStatus.InProgress,
                    StartedAt = DateTimeOffset.UtcNow,
                    DetailsUrl = GitHubCheckService.EntityDtoPropertyMappingDetailsUrlForCheck,
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
            catch (Exception ex)
            {
                this.Logger.LogError(ex, "Failed to create/update Check Run '{CheckName}' for commit {CommitSha} to 'in_progress'.", args.CheckName, args.CommitSha.Substring(0, 7));
                // Consider updating to 'failure' or 'cancelled' if initial setup fails
                return;
            }

            // 2. Perform AI analysis
            var aiResultJsonString = await ValidationService.ValidateEntityDtoPropertyMappingAsync(windowsRoot, wslRoot, Array.Empty<ContextFile>());
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
            string outputTitle = aiResult.Passed ? ValidationService.EntityDtoPropertyMappingCheckName + " Check Passed" : ValidationService.EntityDtoPropertyMappingCheckName + " Check Failed";
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

                    // Skip this annotation if path resolution failed
                    if (string.IsNullOrEmpty(annotationFilePath))
                    {
                        this.Logger.LogWarning("Skipping annotation for violation at '{FilePath}' - could not resolve relative path", violation.File);
                        continue;
                    }

                    annotations.Add(new NewCheckRunAnnotation(
                        path: annotationFilePath,
                        startLine: violation.Line,
                        endLine: violation.Line,
                        annotationLevel: CheckAnnotationLevel.Failure, // Or Warning, depending on severity
                        message: violation.Message)
                    {
                        Title = ValidationService.EntityDtoPropertyMappingCheckName + " Violation",
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
            outputText = (string.IsNullOrEmpty(outputText) ? "No " + ValidationService.EntityDtoPropertyMappingCheckName + " violations were detected." : outputText + "\n\n") + aiResult.Explanation;


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
    // ARCHGUARD_GENERATED_RULE_END - ValidateEntityDtoPropertyMapping

    // ARCHGUARD_INSERTION_POINT_METHODS_END

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

        // 2. Check if the path is already relative (doesn't start with drive letter or root slash)
        if (!Path.IsPathRooted(normalizedPath))
        {
            // Path is already relative, but remove ./ prefix if present
            if (normalizedPath.StartsWith("./"))
            {
                return normalizedPath.Substring(2);
            }
            return normalizedPath;
        }

        // 2. The actual temp directory pattern is: {owner}-{repo-name}-{commit-sha}-{timestamp}
        //    We need to find a directory that contains the repo name and is followed by a slash
        //    Look for pattern: something containing the repo name, followed by a slash
        string[] pathSegments = normalizedPath.Split('/');

        // Find the directory segment that contains the repository name
        int repoSegmentIndex = -1;
        for (int i = 0; i < pathSegments.Length; i++)
        {
            // Check if this segment contains the repository name (case-insensitive)
            // The segment should contain the repo name but may have additional parts (owner prefix, suffixes)
            if (pathSegments[i].Contains(normalizedRepoName, StringComparison.OrdinalIgnoreCase))
            {
                repoSegmentIndex = i;
                break;
            }
        }

        if (repoSegmentIndex != -1 && repoSegmentIndex < pathSegments.Length - 1)
        {
            // Take all segments after the repository directory
            var relativeSegments = pathSegments.Skip(repoSegmentIndex + 1);
            return string.Join("/", relativeSegments);
        }
        else
        {
            // Fallback: If we can't find the repository directory pattern,
            // log a warning and return an empty string (safer than incorrect annotation)
            this.Logger.LogWarning("Could not find repository directory containing '{RepoName}' within the annotation path '{FilePath}'. Returning empty string.", repositoryName, absoluteFilePath);
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
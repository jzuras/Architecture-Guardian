using ArchGuard.MCP.Models;
using ArchGuard.Shared;
using Octokit;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArchGuard.MCP.Services;

#region GH Flow Info
// GitHub UI Action -> Webhook Handler Mapping
//
// Push commit -> push event -> PushWebhookHandler -> Full execution of all checks.
//      Note - above also includes event: -> pull_request (synchronize) -> PullRequestWebhookHandler -> Acknowledge only.
// Open/Close/Reopen PR -> pull_request (opened/closed/reopened) -> PullRequestWebhookHandler -> Full execution of all checks.
// "Re-run all checks" -> check_suite (rerequested) -> CheckSuiteWebhookHandler -> Full execution of all checks.
// "Re-run failed checks" -> check_run (rerequested) -> CheckRunWebhookHandler -> Targeted execution (only the actual failed check or checks).
// "Re-run single check" -> only available for failed checks, and acts exactly the same as above.
//
#endregion

public class GitHubCheckService
{
    public static CodingAgent SelectedCodingAgent { get; set; } = CodingAgent.ClaudeCode;

    // ARCHGUARD_TEMPLATE_CONSTANT_START
    // TEMPLATE_CHECK_NAME_CONSTANT: DependencyRegistrationCheckName
    public static string DependencyRegistrationCheckName { get; } = "Dependency Registration";

    // TEMPLATE_CHECK_NAME_CONSTANT: DependencyRegistrationDetailsUrlForCheck
    public static string DependencyRegistrationDetailsUrlForCheck { get; } = "https://example.com/details/di-check";
    // ARCHGUARD_TEMPLATE_CONSTANT_END

    // ARCHGUARD_INSERTION_POINT_CONSTANTS_START
    // New rule constants go here in alphabetical order by rule name

    // ARCHGUARD_GENERATED_RULE_START - ValidateDependencyDirection
    // Generated from template on: 10/7/25
    // DO NOT EDIT - This code will be regenerated
    public static string DependencyDirectionCheckName { get; } = "DependencyDirection";

    public static string DependencyDirectionDetailsUrlForCheck { get; } = "https://example.com/details/dep-dir-check";
    // ARCHGUARD_GENERATED_RULE_END - ValidateDependencyDirection

    // ARCHGUARD_GENERATED_RULE_START - ValidateEntityDtoPropertyMapping
    // Generated from template on: 9/17/25
    // DO NOT EDIT - This code will be regenerated
    public static string EntityDtoPropertyMappingCheckName { get; } = "EntityDtoPropertyMapping";

    public static string EntityDtoPropertyMappingDetailsUrlForCheck { get; } = "https://example.com/details/dto-check";
    // ARCHGUARD_GENERATED_RULE_END - ValidateEntityDtoPropertyMapping

    // ARCHGUARD_INSERTION_POINT_CONSTANTS_END

    public bool UseAnnotations { get; set; } = true;

    private ILogger<GitHubCheckService> Logger { get; set; }
    private IGitHubFileContentService FileContentService { get; set; }

    public GitHubCheckService(
        ILogger<GitHubCheckService> logger,
        IGitHubFileContentService fileContentService)
    {
        this.Logger = logger;
        this.FileContentService = fileContentService;
    }

    /// <summary>
    /// Creates a new GitHub check run in "queued" status for immediate visibility.
    /// Used in Phase 1 of webhook handling to show "X of Y checks" in GitHub UI.
    /// </summary>
    public async Task<long> CreateCheckAsync(CheckExecutionArgs args, IGitHubClient gitHubClient, string detailsUrl)
    {
        try
        {
            var newCheckRun = new NewCheckRun(args.CheckName, args.CommitSha)
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
                args.CheckName, createdCheck.Id, args.CommitSha.Substring(0, 7));

            return createdCheck.Id;
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Failed to create Check Run '{CheckName}' for commit {CommitSha}",
                args.CheckName, args.CommitSha.Substring(0, 7));
            throw;
        }
    }

    // ARCHGUARD_TEMPLATE_CHECK_METHOD_START
    // TEMPLATE_CHECK_METHOD_NAME: ExecuteDepInjectionCheckAsync
    // TEMPLATE_VALIDATION_SERVICE_METHOD: ValidationService.ValidateDependencyRegistrationAsync
    // TEMPLATE_CHECK_NAME_REFERENCE: DependencyRegistrationCheckName
    public async Task ExecuteDepInjectionCheckAsync(CheckExecutionArgs args, string windowsRoot, string wslRoot, long githubInstallationId, IGitHubClient githubClient, long existingCheckId, ContextFile[]? contextFiles = null, string? webhookPayloadJson = null)
    {
        await this.ExecuteCheckAsync(args, windowsRoot, wslRoot, githubInstallationId, githubClient, existingCheckId,
            ValidationService.ValidateDependencyRegistrationAsync, contextFiles, webhookPayloadJson);
    }
    // ARCHGUARD_TEMPLATE_CHECK_METHOD_END

    // ARCHGUARD_INSERTION_POINT_METHODS_START
    // New rule check methods go here in alphabetical order by rule name

    // ARCHGUARD_GENERATED_RULE_START - ValidateDependencyDirection
    // Generated from template on: 10/7/25
    // DO NOT EDIT - This code will be regenerated
    public async Task ExecuteDependencyDirectionCheckAsync(CheckExecutionArgs args, string windowsRoot, string wslRoot, long githubInstallationId, IGitHubClient githubClient, long existingCheckId, ContextFile[]? contextFiles = null, string? webhookPayloadJson = null)
    {
        await this.ExecuteCheckAsync(args, windowsRoot, wslRoot, githubInstallationId, githubClient, existingCheckId,
            ValidationService.ValidateDependencyDirectionAsync, contextFiles, webhookPayloadJson);
    }
    // ARCHGUARD_GENERATED_RULE_END - ValidateDependencyDirection

    // ARCHGUARD_GENERATED_RULE_START - ValidateEntityDtoPropertyMapping
    // Generated from template on: 9/17/25
    // DO NOT EDIT - This code will be regenerated
    public async Task ExecuteEntityDtoPropertyMappingCheckAsync(CheckExecutionArgs args, string windowsRoot, string wslRoot, long githubInstallationId, IGitHubClient githubClient, long existingCheckId, ContextFile[]? contextFiles = null, string? webhookPayloadJson = null)
    {
        await this.ExecuteCheckAsync(args, windowsRoot, wslRoot, githubInstallationId, githubClient, existingCheckId,
            ValidationService.ValidateEntityDtoPropertyMappingAsync, contextFiles, webhookPayloadJson);
    }
    // ARCHGUARD_GENERATED_RULE_END - ValidateEntityDtoPropertyMapping

    // ARCHGUARD_INSERTION_POINT_METHODS_END

    #region Private Helper Functions
    // This is the entry point from the rule-specific methods above. It uses the other private methods below this one.
    private async Task ExecuteCheckAsync(CheckExecutionArgs args, string windowsRoot, string wslRoot, long githubInstallationId, IGitHubClient githubClient, 
        long existingCheckId, Func<ValidationRequest, Task<string>> validationMethod,
        ContextFile[]? contextFiles = null, string? webhookPayloadJson = null)
    {
        try
        {
            // Update Check Run
            long? checkRunIdOrNull = await this.UpdateCheckRunToInProgressAsync(args, existingCheckId, githubClient);

            if (checkRunIdOrNull is null)
            {
                return;
            }

            long checkRunId = checkRunIdOrNull.Value;

            // Determine context files and diffs based on agent type
            var validationRequest = await this.DetermineContextFilesAndDiffsAsync(contextFiles, webhookPayloadJson, args, githubClient);
            validationRequest.WindowsRoot = windowsRoot;
            validationRequest.WslRoot = wslRoot;
            validationRequest.SelectedCodingAgent = GitHubCheckService.SelectedCodingAgent;

            // Perform AI analysis
            //var aiResultJsonString = await ValidationService.ValidateDependencyRegistrationAsync(validationRequest);
            var aiResultJsonString = await validationMethod(validationRequest);

            var aiResult = this.ParseAiResultJson(aiResultJsonString, args.CheckName, checkRunId);

            this.Logger.LogInformation("Returning check pass to GH (true/false): {result}", aiResult.Passed);

            List<NewCheckRunAnnotation> annotations = new List<NewCheckRunAnnotation>();

            // Determine Conclusion and prepare output based on AI result
            var outputText = this.ProcessViolationsAndCreateAnnotations(annotations, aiResult, args.CheckName, args.RepoName);

            // Update Check Run to 'completed' with the final conclusion and output
            CheckConclusion checkConclusion = aiResult.Passed ? CheckConclusion.Success : CheckConclusion.Failure;
            string outputTitle = aiResult.Passed ? args.CheckName + " Check Passed" : args.CheckName + " Check Failed";
            string outputSummary = aiResult.Explanation; // Use explanation as summary

            await this.CompleteCheckRunAsync(args, checkConclusion, outputTitle, outputSummary, outputText, annotations, githubClient, checkRunId);
        }
        catch (Exception ex)
        {
            Console.WriteLine("An error occurred .");
            Console.WriteLine(ex.Message);
            return;
        }
    }

    private async Task<long?> UpdateCheckRunToInProgressAsync(CheckExecutionArgs args, long existingCheckId, IGitHubClient githubClient)
    {
        long? checkRunId = null;

        try
        {
            var updateToInProgress = new CheckRunUpdate()
            {
                Status = CheckStatus.InProgress,
                StartedAt = DateTimeOffset.UtcNow,
                Output = new NewCheckRunOutput(args.InitialTitle, args.InitialSummary)
                {
                    Text = "The check has been queued and is now running...",
                    // --- Explicitly empty annotations and text/images for new run ---
                    Annotations = new List<NewCheckRunAnnotation>(),
                    Images = new List<NewCheckRunImage>()
                }
            };
            var createdCheck = await githubClient.Check.Run.Update(args.RepoOwner, args.RepoName, existingCheckId, updateToInProgress);

            checkRunId = createdCheck.Id;
            this.Logger.LogInformation("Updated Check Run '{CheckName}' (ID: {CheckRunId}) for commit {CommitSha}...", args.CheckName, checkRunId, args.CommitSha.Substring(0, 7));
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Failed to update Check Run '{CheckName}' for commit {CommitSha} to 'in_progress'.", args.CheckName, args.CommitSha.Substring(0, 7));
            // Consider updating to 'failure' or 'cancelled' if initial setup fails
        }

        return checkRunId;
    }

    private async Task<ValidationRequest> DetermineContextFilesAndDiffsAsync(
          ContextFile[]? contextFiles, string? webhookPayloadJson, CheckExecutionArgs args, IGitHubClient githubClient)
    {
        var validationRequest = new ValidationRequest();

        if (GitHubCheckService.SelectedCodingAgent == CodingAgent.LocalFoundry)
        {
            if (contextFiles is not null)
            {
                // Use pre-extracted context files (from webhook handler to avoid duplication)
                validationRequest.ContextFiles = contextFiles;

                // Extract diffs from the ContextFile objects (no additional API calls needed)
                validationRequest.Diffs = contextFiles
                    .Where(f => !string.IsNullOrEmpty(f.Diff))
                    .Select(f => f.Diff!)
                    .ToArray();
            }
            else if (!string.IsNullOrEmpty(webhookPayloadJson))
            {
                // Enhanced webhook-based extraction with diffs
                var extractionResult = await this.FileContentService.ExtractFromWebhookAsync(
                    webhookPayloadJson, githubClient);
                validationRequest.ContextFiles = extractionResult.Files;
                validationRequest.Diffs = extractionResult.Diffs;
                this.Logger.LogInformation("Extracted {FileCount} files and {DiffCount} diffs from webhook for LocalFoundry validation",
                    validationRequest.ContextFiles.Length, validationRequest.Diffs.Length);
            }
            else
            {
                // Fallback to basic file content extraction
                validationRequest.ContextFiles = await this.FileContentService.ExtractFileContentsAsync(
                    args.RepoOwner, args.RepoName, args.CommitSha, githubClient);
                this.Logger.LogInformation("Extracted {FileCount} files for LocalFoundry validation (no diffs available)", validationRequest.ContextFiles.Length);
            }
        }
        else if (contextFiles is not null)
        {
            // Use provided context files
            validationRequest.ContextFiles = contextFiles;
        }
        else
        {
            // File system agents (ClaudeCode, GeminiCLI) use empty arrays
            validationRequest.ContextFiles = Array.Empty<ContextFile>();
        }

        return validationRequest;
    }

    private AiCheckResult ParseAiResultJson(string aiResultJsonString, string checkName, long checkRunId)
    {
        AiCheckResult aiResult;

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
            this.Logger.LogError(ex, "Failed to parse AI result JSON for Check Run '{CheckName}' (ID: {CheckRunId}). Raw string: {RawAIResult}", checkName, checkRunId, aiResultJsonString);
            // Fallback to a failure conclusion if AI result parsing fails
            aiResult = new AiCheckResult { Passed = false, Explanation = "Failed to parse AI analysis results due to JSON format error." };
        }

        return aiResult;
    }

    private string ProcessViolationsAndCreateAnnotations(List<NewCheckRunAnnotation> annotations, AiCheckResult aiResult, string checkName, string repoName)
    {
        string outputText = string.Empty;

        if (aiResult.Violations.Count != 0 && this.UseAnnotations is true)
        {
            // Create annotations for each violation
            foreach (var violation in aiResult.Violations)
            {
                string annotationFilePath = GetRelativeRepoPathForAnnotation(violation.File, repoName);

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
                    Title = checkName + " Violation",
                    RawDetails = $"File: {violation.File}, Line: {violation.Line}\nMessage: {violation.Message}"
                });
            }

            outputText = "**Detected Violations:**\n\n" +
                         string.Join("\n", aiResult.Violations.Select(v => {
                             string relativePath = GetRelativeRepoPathForAnnotation(v.File, repoName);
                             // If the relative path couldn't be determined, fall back to the original full path with a note
                             string displayPath = string.IsNullOrEmpty(relativePath) ? $"{v.File} (absolute path)" : $"`{relativePath}`";
                             return $"- **File:** {displayPath} (Line: {v.Line}): {v.Message}";
                         }));
        }

        // Add the overall explanation as part of the text
        outputText = (string.IsNullOrEmpty(outputText) ? "No " + checkName + " violations were detected." : outputText + "\n\n") + aiResult.Explanation;

        return outputText;
    }

    private async Task CompleteCheckRunAsync(CheckExecutionArgs args, CheckConclusion checkConclusion, string outputTitle, string outputSummary,
        string outputText, List<NewCheckRunAnnotation> annotations, IGitHubClient githubClient, long checkRunId)
    {
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
    #endregion

    private class AiCheckResult
    {
        [JsonPropertyName("passed")]
        public bool Passed { get; set; }

        [JsonPropertyName("violations")]
        [JsonConverter(typeof(FlexibleViolationsConverter))]
        public List<AiCheckViolation> Violations { get; set; } = new List<AiCheckViolation>();

        [JsonPropertyName("explanation")]
        public string Explanation { get; set; } = string.Empty;
    }

    private class AiCheckViolation
    {
        [JsonPropertyName("file")]
        public string File { get; set; } = string.Empty;

        [JsonPropertyName("line")]
        public int Line { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Custom JSON converter that handles violations as either:
    /// - Array of objects: [{"file": "path", "line": 1, "message": "desc"}]
    /// - Array of strings: ["violation message"]
    /// </summary>
    private class FlexibleViolationsConverter : JsonConverter<List<AiCheckViolation>>
    {
        public override List<AiCheckViolation> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var violations = new List<AiCheckViolation>();

            if (reader.TokenType != JsonTokenType.StartArray)
            {
                throw new JsonException("Expected violations to be an array");
            }

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                {
                    break;
                }

                if (reader.TokenType == JsonTokenType.String)
                {
                    // Handle string violation format: "violation message"
                    var violationMessage = reader.GetString() ?? string.Empty;
                    violations.Add(new AiCheckViolation
                    {
                        File = "unknown", // Default when only message is provided
                        Line = 0,         // Default when line number is not provided
                        Message = violationMessage
                    });
                }
                else if (reader.TokenType == JsonTokenType.StartObject)
                {
                    // Handle object violation format: {"file": "path", "line": 1, "message": "desc"}
                    var violation = JsonSerializer.Deserialize<AiCheckViolation>(ref reader, options);
                    if (violation is not null)
                    {
                        violations.Add(violation);
                    }
                }
                else
                {
                    throw new JsonException($"Unexpected token in violations array: {reader.TokenType}");
                }
            }

            return violations;
        }

        public override void Write(Utf8JsonWriter writer, List<AiCheckViolation> value, JsonSerializerOptions options)
        {
            // Always write in object format for consistency
            JsonSerializer.Serialize(writer, value, options);
        }
    }
}

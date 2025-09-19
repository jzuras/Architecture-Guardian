using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ArchGuard.Shared;

// Concept:
// Validation rule Checks are based on the original template, ValidateDependencyRegistrationAsync(),
// and can be newly created, or entirely re-generated, using these command prompts in the .claude/commands directory:
// Regenerate All Rules: regenerate-all-rules
// Create New Rule: create-new-rule
//
// The Create New Rule command prompt will ask for new values for these strings (some are used in other files):
//      1.Mcp Tool Description (Example: "Validates that all services referenced in constructors are properly registered in the DI container.")
//      2.Rule/Check Name (Example: "Dependency Registration")
//      3.AI Agent Instructions (Example: "Validate Dependency Registration rule: Check that all services referenced in constructors are properly registered in the DI container")
//      4.Details Url For Rule/Check (Example: "https://example.com/details/di-check") (this would be a real website if actually used/needed)
//      5. Method Comment: Optional implementation comment for method body (leave blank for none)
//      6. Generation Date: as a string, with or without time, etc.
//
// The Regenerate All Rules command prompt should also ask for a generation date.


public enum CodingAgent
{
    ClaudeCode,
    GeminiCli
}

public static class ValidationService
{
    private static JsonSerializerOptions WriteIndentedJsonSerializerOptions { get; set; } = new JsonSerializerOptions { WriteIndented = true };
    public static CodingAgent SelectedCodingAgent { get; set; } = CodingAgent.ClaudeCode;

    // ARCHGUARD_TEMPLATE_CONSTANT_START
    // TEMPLATE_CHECK_NAME_CONSTANT: DependencyRegistrationCheckName
    public static string DependencyRegistrationCheckName { get; } = "Dependency Registration";

    // TEMPLATE_CHECK_NAME_CONSTANT: DependencyRegistrationCheckName
    public static string DependencyRegistrationAiAgentInstructions { get; } = "Validate Dependency Registration rule: Check that all services referenced in constructors are properly registered in the DI container.";
    // ARCHGUARD_TEMPLATE_CONSTANT_END

    // ARCHGUARD_INSERTION_POINT_CONSTANTS_START
    // New rule constants go here in alphabetical order by rule name

    // ARCHGUARD_GENERATED_RULE_START - ValidateEntityDtoPropertyMapping
    // Generated from template on: 9/17/25
    // DO NOT EDIT - This code will be regenerated
    public static string EntityDtoPropertyMappingCheckName { get; } = "EntityDtoPropertyMapping";

    public static string EntityDtoPropertyMappingAiAgentInstructions { get; } = "Analyze this codebase for Entity-DTO property mapping violations. Look for DTO classes that have mismatched property names, missing properties, or inconsistent data types compared to their corresponding domain entities. Report any DTOs where properties don't properly align with their entity counterparts (e.g., 'Id' vs 'UserId', missing required properties, or renamed properties that break mapping conventions).";
    // ARCHGUARD_GENERATED_RULE_END - ValidateEntityDtoPropertyMapping

    // ARCHGUARD_INSERTION_POINT_CONSTANTS_END

    // ARCHGUARD_TEMPLATE_RULE_START
    // TEMPLATE_METHOD_NAME: ValidateDependencyRegistrationAsync
    public static async Task<string> ValidateDependencyRegistrationAsync(string windowsRoot, string wslRoot, ContextFile[] contextFiles, string[]? diffs = null)
    {
        try
        {
            // Write inputs to file for debugging.
            var debugData = new
            {
                contextFiles = contextFiles.Select(cf => new
                {
                    cf.FilePath,
                }).ToArray(),
                diffs = diffs ?? Array.Empty<string>(),
                timestamp = DateTime.Now
            };

            string debugJson = JsonSerializer.Serialize(debugData, ValidationService.WriteIndentedJsonSerializerOptions);
            Console.WriteLine(debugJson);
            
            string agentResponse;

            #region Call AI Agent to handle validation.
            // Create prompt content.
            var promptBuilder = new StringBuilder();
            promptBuilder.AppendLine(ValidationService.DependencyRegistrationAiAgentInstructions);
            promptBuilder.AppendLine("Do not include commented-out code in your evaluation. Do not even mention it.");
            promptBuilder.AppendLine("Do not use any external tools, but you may read files as needed.");

            if (diffs is not null && diffs.Length > 0)
            {
                promptBuilder.AppendLine();
                promptBuilder.AppendLine("Pay special attention to these recent changes:");
                foreach (var diff in diffs)
                {
                    promptBuilder.AppendLine($"```diff");
                    promptBuilder.AppendLine(diff);
                    promptBuilder.AppendLine("```");
                }
            }

            promptBuilder.AppendLine();
            if (ValidationService.SelectedCodingAgent == CodingAgent.ClaudeCode)
            {
                promptBuilder.AppendLine("IMPORTANT: You have Write tool permission, but ONLY use it to write the output file 'output.tmp' in the current directory.");
                promptBuilder.AppendLine("DO NOT modify, create, or overwrite any source code files. Only write to 'output.tmp'.");
                promptBuilder.AppendLine("Write your response to a file named 'output.tmp' in the current directory using the Write tool.");
            }
            promptBuilder.AppendLine("Return JSON format: {\"passed\": bool, \"violations\": [{\"file\": \"path\", \"line\": number, \"message\": \"description\"}], \"explanation\": \"detailed explanation\"}");
            promptBuilder.AppendLine("Your entire response must be valid JSON that can be parsed directly. Do not include any text before or after the JSON.");

            // Write prompt to temp file.
            var tempPromptFile = Path.GetTempFileName();
            await File.WriteAllTextAsync(tempPromptFile, promptBuilder.ToString());

            try
            {
                // Call AI Agent.
                string command;
                string workingDirectory;
                string tempPath;

                if (ValidationService.SelectedCodingAgent == CodingAgent.ClaudeCode)
                {
                    tempPath = ConvertToWslPath(tempPromptFile);
                    workingDirectory = wslRoot;
                    command = $"wsl bash -i -c \"cd {workingDirectory} && touch output.tmp && claude --print --allowed-tools 'Read Write Edit' < {tempPath}; echo '__AGENT_COMPLETE__'\"";
                }
                else if (ValidationService.SelectedCodingAgent == CodingAgent.GeminiCli)
                {
                    tempPath = tempPromptFile;
                    workingDirectory = windowsRoot;
                    command = $"cd /d \"{workingDirectory}\" && echo. > output.tmp && gemini --model gemini-2.5-flash --prompt < \"{tempPath}\" > output.tmp && echo '__AGENT_COMPLETE__'";
                }
                else
                {
                    // Default fallback
                    tempPath = tempPromptFile;
                    workingDirectory = windowsRoot;
                    command = $"cmd /c \"cd /d \"{workingDirectory}\" && echo. > output.tmp && echo 'Agent {ValidationService.SelectedCodingAgent} not yet implemented' > output.tmp && echo '__AGENT_COMPLETE__'\"";
                }

                Console.WriteLine("agent command is " + command);

                var processInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {command}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processInfo);
                if (process is null)
                {
                    return JsonSerializer.Serialize(new
                    {
                        passed = false,
                        explanation = "Failed to start agent process"
                    });
                }

                // Wait for process to complete before reading output
                await process.WaitForExitAsync();
                agentResponse = await process.StandardOutput.ReadToEndAsync();
                var errorOutput = await process.StandardError.ReadToEndAsync();

                if (process.ExitCode != 0)
                {
                    return JsonSerializer.Serialize(new
                    {
                        passed = false,
                        explanation = $"Agent error: {errorOutput}"
                    });
                }

                Console.WriteLine();
                Console.WriteLine($"Raw agent response length: {agentResponse?.Length ?? 0}");
                Console.WriteLine($"Raw agent response: '{agentResponse}'");

                // Check for completion marker
                if (agentResponse?.Contains("__AGENT_COMPLETE__") == true)
                {
                    Console.WriteLine("SUCCESS: Found completion marker - agent finished execution");

                    // Read the actual JSON response from the output file
                    var outputPath = Path.Combine(windowsRoot, "output.tmp");
                    if (File.Exists(outputPath))
                    {
                        agentResponse = await File.ReadAllTextAsync(outputPath);
                        Console.WriteLine($"File output length: {agentResponse?.Length ?? 0}");
                        Console.WriteLine($"File output content: '{agentResponse}'");

                        // Clean up the output file
                        File.Delete(outputPath);
                    }
                    else
                    {
                        Console.WriteLine("ERROR: Output file not found - agent may not have written to file");
                        agentResponse = string.Empty;
                    }
                }
                else
                {
                    Console.WriteLine("WARNING: Completion marker not found - agent execution may be incomplete");
                    agentResponse = string.Empty;
                }

                var json = ExtractJsonFromResponse(agentResponse ?? string.Empty);

                Console.WriteLine();
                Console.WriteLine("returning this json: " + json);

                return JsonSerializer.Serialize(json, ValidationService.WriteIndentedJsonSerializerOptions);
            }
            finally
            {
                // Delete temp file.
                if (File.Exists(tempPromptFile))
                {
                    File.Delete(tempPromptFile);
                }
            }            
            #endregion
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
            throw new InvalidOperationException($"Error in ValidateDependencyRegistrationAsync method: {ex.Message}", ex);
        }
    }
    // ARCHGUARD_TEMPLATE_RULE_END

    // ARCHGUARD_INSERTION_POINT_METHODS_START
    // New rule methods go here in alphabetical order by rule name

    // ARCHGUARD_GENERATED_RULE_START - ValidateEntityDtoPropertyMapping
    // Generated from template on: 9/17/25
    // DO NOT EDIT - This code will be regenerated
    public static async Task<string> ValidateEntityDtoPropertyMappingAsync(string windowsRoot, string wslRoot, ContextFile[] contextFiles, string[]? diffs = null)
    {
        try
        {
            // Write inputs to file for debugging.
            var debugData = new
            {
                contextFiles = contextFiles.Select(cf => new
                {
                    cf.FilePath,
                }).ToArray(),
                diffs = diffs ?? Array.Empty<string>(),
                timestamp = DateTime.Now
            };

            string debugJson = JsonSerializer.Serialize(debugData, ValidationService.WriteIndentedJsonSerializerOptions);
            Console.WriteLine(debugJson);

            string agentResponse;

            #region Call AI Agent to handle validation.
            // Create prompt content.
            var promptBuilder = new StringBuilder();
            promptBuilder.AppendLine(ValidationService.EntityDtoPropertyMappingAiAgentInstructions);
            promptBuilder.AppendLine("Do not include commented-out code in your evaluation. Do not even mention it.");
            promptBuilder.AppendLine("Do not use any external tools, but you may read files as needed.");

            if (diffs is not null && diffs.Length > 0)
            {
                promptBuilder.AppendLine();
                promptBuilder.AppendLine("Pay special attention to these recent changes:");
                foreach (var diff in diffs)
                {
                    promptBuilder.AppendLine($"```diff");
                    promptBuilder.AppendLine(diff);
                    promptBuilder.AppendLine("```");
                }
            }

            promptBuilder.AppendLine();
            if (ValidationService.SelectedCodingAgent == CodingAgent.ClaudeCode)
            {
                promptBuilder.AppendLine("IMPORTANT: You have Write tool permission, but ONLY use it to write the output file 'output.tmp' in the current directory.");
                promptBuilder.AppendLine("DO NOT modify, create, or overwrite any source code files. Only write to 'output.tmp'.");
                promptBuilder.AppendLine("Write your response to a file named 'output.tmp' in the current directory using the Write tool.");
            }
            promptBuilder.AppendLine("Return JSON format: {\"passed\": bool, \"violations\": [{\"file\": \"path\", \"line\": number, \"message\": \"description\"}], \"explanation\": \"detailed explanation\"}");
            promptBuilder.AppendLine("Your entire response must be valid JSON that can be parsed directly. Do not include any text before or after the JSON.");

            // Write prompt to temp file.
            var tempPromptFile = Path.GetTempFileName();
            await File.WriteAllTextAsync(tempPromptFile, promptBuilder.ToString());

            try
            {
                // Call AI Agent.
                string command;
                string workingDirectory;
                string tempPath;

                if (ValidationService.SelectedCodingAgent == CodingAgent.ClaudeCode)
                {
                    tempPath = ConvertToWslPath(tempPromptFile);
                    workingDirectory = wslRoot;
                    command = $"wsl bash -i -c \"cd {workingDirectory} && touch output.tmp && claude --print --allowed-tools 'Read Write Edit' < {tempPath}; echo '__AGENT_COMPLETE__'\"";
                }
                else if (ValidationService.SelectedCodingAgent == CodingAgent.GeminiCli)
                {
                    tempPath = tempPromptFile;
                    workingDirectory = windowsRoot;
                    command = $"cd /d \"{workingDirectory}\" && echo. > output.tmp && gemini --model gemini-2.5-flash --prompt < \"{tempPath}\" > output.tmp && echo '__AGENT_COMPLETE__'";
                }
                else
                {
                    // Default fallback
                    tempPath = tempPromptFile;
                    workingDirectory = windowsRoot;
                    command = $"cmd /c \"cd /d \"{workingDirectory}\" && echo. > output.tmp && echo 'Agent {ValidationService.SelectedCodingAgent} not yet implemented' > output.tmp && echo '__AGENT_COMPLETE__'\"";
                }

                Console.WriteLine("agent command is " + command);

                var processInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {command}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processInfo);
                if (process is null)
                {
                    return JsonSerializer.Serialize(new
                    {
                        passed = false,
                        explanation = "Failed to start agent process"
                    });
                }

                // Wait for process to complete before reading output
                await process.WaitForExitAsync();
                agentResponse = await process.StandardOutput.ReadToEndAsync();
                var errorOutput = await process.StandardError.ReadToEndAsync();

                if (process.ExitCode != 0)
                {
                    return JsonSerializer.Serialize(new
                    {
                        passed = false,
                        explanation = $"Agent error: {errorOutput}"
                    });
                }

                Console.WriteLine();
                Console.WriteLine($"Raw agent response length: {agentResponse?.Length ?? 0}");
                Console.WriteLine($"Raw agent response: '{agentResponse}'");

                // Check for completion marker
                if (agentResponse?.Contains("__AGENT_COMPLETE__") == true)
                {
                    Console.WriteLine("SUCCESS: Found completion marker - agent finished execution");

                    // Read the actual JSON response from the output file
                    var outputPath = Path.Combine(windowsRoot, "output.tmp");
                    if (File.Exists(outputPath))
                    {
                        agentResponse = await File.ReadAllTextAsync(outputPath);
                        Console.WriteLine($"File output length: {agentResponse?.Length ?? 0}");
                        Console.WriteLine($"File output content: '{agentResponse}'");

                        // Clean up the output file
                        File.Delete(outputPath);
                    }
                    else
                    {
                        Console.WriteLine("ERROR: Output file not found - agent may not have written to file");
                        agentResponse = string.Empty;
                    }
                }
                else
                {
                    Console.WriteLine("WARNING: Completion marker not found - agent execution may be incomplete");
                    agentResponse = string.Empty;
                }

                var json = ExtractJsonFromResponse(agentResponse ?? string.Empty);

                Console.WriteLine();
                Console.WriteLine("returning this json: " + json);

                return JsonSerializer.Serialize(json, ValidationService.WriteIndentedJsonSerializerOptions);
            }
            finally
            {
                // Delete temp file.
                if (File.Exists(tempPromptFile))
                {
                    File.Delete(tempPromptFile);
                }
            }
            #endregion
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
            throw new InvalidOperationException($"Error in ValidateEntityDtoPropertyMappingAsync method: {ex.Message}", ex);
        }
    }
    // ARCHGUARD_GENERATED_RULE_END - ValidateEntityDtoPropertyMapping

    // ARCHGUARD_INSERTION_POINT_METHODS_END

    private static string ExtractJsonFromResponse(string agentResponse)
    {
        if (string.IsNullOrWhiteSpace(agentResponse) is true)
        {
            Console.WriteLine("ERROR: Agent returned empty or whitespace response");
            return JsonSerializer.Serialize(new
            {
                passed = false,
                explanation = "Agent returned empty response - check agent installation and command execution"
            });
        }

        // Try to find JSON within ```json blocks first
        var jsonBlockMatch = Regex.Match(agentResponse, @"```json\s*\n(.*?)\n```", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (jsonBlockMatch.Success is true)
        {
            return jsonBlockMatch.Groups[1].Value.Trim();
        }

        // Try to find JSON within ``` blocks (without json specifier)
        var codeBlockMatch = Regex.Match(agentResponse, @"```\s*\n(.*?)\n```", RegexOptions.Singleline);
        if (codeBlockMatch.Success is true)
        {
            var blockContent = codeBlockMatch.Groups[1].Value.Trim();
            if (blockContent.StartsWith("{") && blockContent.EndsWith("}"))
            {
                return blockContent;
            }
        }

        // Look for JSON object in the entire response (most permissive)
        var jsonMatch = Regex.Match(agentResponse, @"\{.*\}", RegexOptions.Singleline);
        if (jsonMatch.Success)
        {
            return jsonMatch.Value;
        }

        // Fallback - return error JSON with debug info
        Console.WriteLine($"ERROR: Could not parse JSON from agent response. Response content: '{agentResponse}'");
        return JsonSerializer.Serialize(new
        {
            passed = false,
            explanation = $"Could not parse JSON from agent response. Response length: {agentResponse?.Length ?? 0} characters"
        });
    }

    public static string ConvertToWslPath(string inputPath)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            return string.Empty;
        }

        // Handle file:// URLs first
        if (inputPath.StartsWith("file:///"))
        {
            string pathPart = inputPath.Substring(8); // Remove "file:///"

            // Handle drive letters (e.g., "C:" -> "/mnt/c")
            if (pathPart.Length >= 2 && pathPart[1] == ':')
            {
                char driveLetter = char.ToLower(pathPart[0]);
                string remainingPath = pathPart.Substring(2);
                return $"/mnt/{driveLetter}{remainingPath.Replace("\\", "/")}";
            }

            // Already in WSL format (e.g., "mnt/c/...")
            if (pathPart.StartsWith("mnt/"))
            {
                return "/" + pathPart.Replace("\\", "/");
            }

            // Other cases - just remove file:// and convert slashes
            return pathPart.Replace("\\", "/");
        }

        // Handle regular Windows paths (e.g., "C:\path" -> "/mnt/c/path")
        if (inputPath.Length >= 2 && inputPath[1] == ':')
        {
            char driveLetter = char.ToLower(inputPath[0]);
            string remainingPath = inputPath.Substring(2);
            return $"/mnt/{driveLetter}{remainingPath.Replace("\\", "/")}";
        }

        // Already WSL format or relative path - just convert slashes
        return inputPath.Replace("\\", "/");
    }
}

public record ContextFile(string FilePath);
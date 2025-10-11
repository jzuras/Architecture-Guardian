using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ArchGuard.Shared;

// Strategy for file system based agents (ClaudeCode, GeminiCLI)
public class FileSystemValidationStrategy : IValidationStrategy
{
    // ARCHGUARD_TEMPLATE_RULE_START
    public async Task<string> ValidateDependencyRegistrationAsync(ValidationRequest request)
    {
        return await ExecuteFileSystemValidationAsync(
            request,
            ValidationService.DependencyRegistrationAiAgentInstructions);
    }
    // ARCHGUARD_TEMPLATE_RULE_END

    // ARCHGUARD_INSERTION_POINT_METHODS_START
    // New rule method implementations go here in alphabetical order by rule name

    // ARCHGUARD_GENERATED_RULE_START - ValidateDependencyDirection
    // Generated from template on: 10/7/25
    // DO NOT EDIT - This code will be regenerated
    public async Task<string> ValidateDependencyDirectionAsync(ValidationRequest request)
    {
        return await ExecuteFileSystemValidationAsync(
            request,
            ValidationService.DependencyDirectionAiAgentInstructions);
    }
    // ARCHGUARD_GENERATED_RULE_END - ValidateDependencyDirection

    // ARCHGUARD_GENERATED_RULE_START - ValidateEntityDtoPropertyMapping
    // Generated from template on: 9/17/25
    // DO NOT EDIT - This code will be regenerated
    public async Task<string> ValidateEntityDtoPropertyMappingAsync(ValidationRequest request)
    {
        return await ExecuteFileSystemValidationAsync(
            request,
            ValidationService.EntityDtoPropertyMappingAiAgentInstructions);
    }
    // ARCHGUARD_GENERATED_RULE_END - ValidateEntityDtoPropertyMapping

    // ARCHGUARD_INSERTION_POINT_METHODS_END

    private async Task<string> ExecuteFileSystemValidationAsync(ValidationRequest request, string agentInstructions)
    {
        try
        {
            // Create prompt content
            var promptBuilder = new StringBuilder();
            promptBuilder.AppendLine(agentInstructions);
            promptBuilder.AppendLine("Do not include commented-out code in your evaluation. Do not even mention it.");
            promptBuilder.AppendLine("Do not use any external tools, but you may read files as needed.");

            if (request.Diffs is not null && request.Diffs.Length > 0)
            {
                promptBuilder.AppendLine();
                promptBuilder.AppendLine("Pay special attention to these recent changes:");
                foreach (var diff in request.Diffs)
                {
                    promptBuilder.AppendLine($"```diff");
                    promptBuilder.AppendLine(diff);
                    promptBuilder.AppendLine("```");
                }
            }

            promptBuilder.AppendLine();
            if (request.SelectedCodingAgent == CodingAgent.ClaudeCode)
            {
                promptBuilder.AppendLine("IMPORTANT: You have Write tool permission, but ONLY use it to write the output file 'output.tmp' in the current directory.");
                promptBuilder.AppendLine("DO NOT modify, create, or overwrite any source code files. Only write to 'output.tmp'.");
                promptBuilder.AppendLine("Write your response to a file named 'output.tmp' in the current directory using the Write tool.");
            }
            promptBuilder.AppendLine("Return JSON format: {\"passed\": bool, \"violations\": [{\"file\": \"path\", \"line\": number, \"message\": \"description\"}], \"explanation\": \"detailed explanation\"}");
            promptBuilder.AppendLine("Your entire response must be valid JSON that can be parsed directly. Do not include any text before or after the JSON.");

            // Write prompt to temp file
            var tempPromptFile = Path.GetTempFileName();
            await File.WriteAllTextAsync(tempPromptFile, promptBuilder.ToString());

            try
            {
                // Call AI Agent
                string command;
                string workingDirectory;
                string tempPath;

                if (request.SelectedCodingAgent == CodingAgent.ClaudeCode)
                {
                    tempPath = ValidationService.ConvertToWslPath(tempPromptFile);
                    workingDirectory = request.WslRoot!;
                    command = $"wsl bash -i -c \"cd {workingDirectory} && touch output.tmp && claude --print --allowed-tools 'Read Write Edit' < {tempPath}; echo '__AGENT_COMPLETE__'\"";
                }
                else if (request.SelectedCodingAgent == CodingAgent.GeminiCli)
                {
                    tempPath = tempPromptFile;
                    workingDirectory = request.WindowsRoot!;
                    command = $"cd /d \"{workingDirectory}\" && echo. > output.tmp && gemini --model gemini-2.5-flash --prompt < \"{tempPath}\" > output.tmp && echo '__AGENT_COMPLETE__'";
                }
                else
                {
                    // Default fallback
                    tempPath = tempPromptFile;
                    workingDirectory = request.WindowsRoot!;
                    command = $"cmd /c \"cd /d \"{workingDirectory}\" && echo. > output.tmp && echo 'Agent {request.SelectedCodingAgent} not yet implemented' > output.tmp && echo '__AGENT_COMPLETE__'\"";
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
                var agentResponse = await process.StandardOutput.ReadToEndAsync();
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
                    var outputPath = Path.Combine(request.WindowsRoot!, "output.tmp");
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

                var json = FileSystemValidationStrategy.ExtractJsonFromResponse(agentResponse ?? string.Empty);

                Console.WriteLine();
                Console.WriteLine("returning this json: " + json);

                return json;
            }
            finally
            {
                // Delete temp file
                if (File.Exists(tempPromptFile))
                {
                    File.Delete(tempPromptFile);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
            throw new InvalidOperationException($"Error in FileSystem validation: {ex.Message}", ex);
        }
    }

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
}
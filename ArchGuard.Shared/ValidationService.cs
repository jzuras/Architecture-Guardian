using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ArchGuard.Shared;

public static class ValidationService
{
    public static async Task<string> ValidateDependencyRegistrationAsync(string root, ContextFile[] contextFiles, string[]? diffs = null)
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

            string debugJson = JsonSerializer.Serialize(debugData, new JsonSerializerOptions { WriteIndented = true });
            Console.WriteLine(debugJson);
            
            string claudeResponse;

            #region Call Claude Code to handle validation
            // Create prompt content.
            var promptBuilder = new StringBuilder();
            promptBuilder.AppendLine("Validate dependency registration rule: Check that all services referenced in constructors are properly registered in the DI container.");
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
            promptBuilder.AppendLine("Return JSON format: {\"passed\": bool, \"violations\": [{\"file\": \"path\", \"line\": number, \"message\": \"description\"}], \"explanation\": \"detailed explanation\"}");
            promptBuilder.AppendLine("Your entire response must be valid JSON that can be parsed directly. Do not include any text before or after the JSON.");

            // Write prompt to temp file.
            var tempPromptFile = Path.GetTempFileName();
            await File.WriteAllTextAsync(tempPromptFile, promptBuilder.ToString());

            try
            {
                // Execute Claude Code.
                var wslTempPath = tempPromptFile.Replace("C:\\", "/mnt/c/").Replace("\\", "/");
                string wslRootPath = root.Replace("file:///C:", "/mnt/c").Replace("file:///mnt/c", "/mnt/c").Replace("file:///", "").Replace("\\", "/");

                var command = $"wsl bash -i -c \"cd {wslRootPath} && claude --print < {wslTempPath}\"";
                Console.WriteLine("claude command is " + command);

                // TODO - a more robust version of above replace commands that handle any drive letters:
                //string wslPath = System.Text.RegularExpressions.Regex
                //    .Replace(inputPath, @"^file:///(([A-Za-z]):|(mnt/[a-z]))", match =>
                //    {
                //        if (match.Groups[2].Success) // Drive letter like C:
                //            return "/mnt/" + match.Groups[2].Value.ToLower().Replace(":", "");
                //        else // Already WSL format like mnt/c
                //            return "/" + match.Groups[3].Value;
                //    })
                //    .Replace("\\", "/");

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
                        explanation = "Failed to start Claude Code process"
                    });
                }

                claudeResponse = await process.StandardOutput.ReadToEndAsync();
                var errorOutput = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    return JsonSerializer.Serialize(new
                    {
                        passed = false,
                        explanation = $"Claude Code error: {errorOutput}"
                    });
                }

                var json = ExtractJsonFromResponse(claudeResponse);

                Console.WriteLine();
                Console.WriteLine("returning this json: " + json);

                return JsonSerializer.Serialize(json, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
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

    private static string ExtractJsonFromResponse(string claudeResponse)
    {
        if (string.IsNullOrWhiteSpace(claudeResponse) is true)
        {
            return "{}";
        }

        // Try to find JSON within ```json blocks first
        var jsonBlockMatch = Regex.Match(claudeResponse, @"```json\s*\n(.*?)\n```", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (jsonBlockMatch.Success is true)
        {
            return jsonBlockMatch.Groups[1].Value.Trim();
        }

        // Try to find JSON within ``` blocks (without json specifier)
        var codeBlockMatch = Regex.Match(claudeResponse, @"```\s*\n(.*?)\n```", RegexOptions.Singleline);
        if (codeBlockMatch.Success is true)
        {
            var blockContent = codeBlockMatch.Groups[1].Value.Trim();
            if (blockContent.StartsWith("{") && blockContent.EndsWith("}"))
            {
                return blockContent;
            }
        }

        // Look for JSON object in the entire response (most permissive)
        var jsonMatch = Regex.Match(claudeResponse, @"\{.*\}", RegexOptions.Singleline);
        if (jsonMatch.Success)
        {
            return jsonMatch.Value;
        }

        // Fallback - return error JSON
        return JsonSerializer.Serialize(new
        {
            passed = false,
            explanation = "Could not parse JSON from Claude response"
        });
    }
}

public record ContextFile(string FilePath);
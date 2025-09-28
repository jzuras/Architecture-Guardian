using Microsoft.AI.Foundry.Local;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using System.Text;
using System.Text.Json;

namespace ArchGuard.Shared;

// Strategy for API-based agents (LocalFoundry)
public class ApiValidationStrategy : IValidationStrategy
{
    // Static properties for lazy initialization - now stores manager and model for creating fresh clients
    private static FoundryLocalManager? LocalFoundryManager { get; set; }
    private static ModelInfo? LocalFoundryModel { get; set; }
    private static bool InitializationAttempted { get; set; } = false;
    private static readonly SemaphoreSlim InitializationSemaphore = new(1, 1);

    // LocalFoundry model configuration
    public static string LocalFoundryModelAlias { get; } = "qwen2.5-0.5b";//"phi-4-mini";
    
    // ARCHGUARD_TEMPLATE_RULE_START
    public async Task<string> ValidateDependencyRegistrationAsync(ValidationRequest request)
    {
        return await ExecuteApiValidationAsync(
            request,
            ValidationService.DependencyRegistrationAiAgentInstructions);
    }
    // ARCHGUARD_TEMPLATE_RULE_END

    // ARCHGUARD_INSERTION_POINT_METHODS_START
    // New rule method implementations go here in alphabetical order by rule name

    // ARCHGUARD_GENERATED_RULE_START - ValidateEntityDtoPropertyMapping
    // Generated from template on: 9/17/25
    // DO NOT EDIT - This code will be regenerated
    public async Task<string> ValidateEntityDtoPropertyMappingAsync(ValidationRequest request)
    {
        return await ExecuteApiValidationAsync(
            request,
            ValidationService.EntityDtoPropertyMappingAiAgentInstructions);
    }
    // ARCHGUARD_GENERATED_RULE_END - ValidateEntityDtoPropertyMapping

    // ARCHGUARD_INSERTION_POINT_METHODS_END

    private async Task<string> ExecuteApiValidationAsync(ValidationRequest request, string agentInstructions)
    {
        try
        {
            if (request.SelectedCodingAgent == CodingAgent.LocalFoundry)
            {
                var foundryResult = await CallLocalFoundryWithContentAsync(request.ContextFiles, agentInstructions, request.Diffs);
                return JsonSerializer.Serialize(foundryResult, ValidationService.WriteIndentedJsonSerializerOptions);
            }

            return JsonSerializer.Serialize(new
            {
                passed = false,
                explanation = "Unsupported agent for API-based validation"
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
            return JsonSerializer.Serialize(new
            {
                passed = false,
                explanation = $"Error in API validation: {ex.Message}"
            });
        }
    }

    private async Task<object> CallLocalFoundryWithContentAsync(ContextFile[] contextFiles, string agentInstructions, string[]? diffs)
    {
        // Ensure LocalFoundry is initialized - all threads wait for initialization to complete
        await EnsureLocalFoundryInitializedAsync();

        // After initialization completes, check if it succeeded
        // (No additional semaphore needed here since initialization is now complete)
        if (ApiValidationStrategy.LocalFoundryManager is null)
        {
            return new
            {
                passed = false,
                violations = new object[0],
                explanation = "Local Foundry manager is not available - initialization failed"
            };
        }

        // Build context from file contents
        var contextBuilder = new StringBuilder();

        // Add the agent instructions
        contextBuilder.AppendLine(agentInstructions);
        contextBuilder.AppendLine("Do not include commented-out code in your evaluation. Do not even mention it.");

        // Add diffs if provided
        if (diffs is not null && diffs.Length > 0)
        {
            contextBuilder.AppendLine();
            contextBuilder.AppendLine("Pay special attention to these recent changes:");
            foreach (var diff in diffs)
            {
                contextBuilder.AppendLine($"```diff");
                contextBuilder.AppendLine(diff);
                contextBuilder.AppendLine("```");
            }
        }

        contextBuilder.AppendLine();
        contextBuilder.AppendLine("=== JSON FORMAT REQUIREMENTS ===");
        contextBuilder.AppendLine("Return JSON format: {\"passed\": bool, \"violations\": [{\"file\": \"path\", \"line\": number, \"message\": \"description\"}], \"explanation\": \"detailed explanation\"}");
        contextBuilder.AppendLine();
        contextBuilder.AppendLine("CRITICAL RULES:");
        contextBuilder.AppendLine("- If the rule PASSES: Set \"passed\": true, \"violations\": [], \"explanation\": \"Rule passed - no violations found\"");
        contextBuilder.AppendLine("- If the rule FAILS: Set \"passed\": false, populate violations array with specific issues, provide detailed explanation");
        contextBuilder.AppendLine("- Do NOT include violations when passed=true");
        contextBuilder.AppendLine("- Do NOT provide explanations about potential violations when the rule passes");
        contextBuilder.AppendLine();
        contextBuilder.AppendLine("Your entire response must be valid JSON that can be parsed directly. Do not include any text before or after the JSON.");

        // Add file contents as context
        contextBuilder.AppendLine();
        contextBuilder.AppendLine("Context Files:");
        foreach (var file in contextFiles)
        {
            contextBuilder.AppendLine($"File: {file.FilePath}");
            contextBuilder.AppendLine("```");
            contextBuilder.AppendLine(file.Content);
            contextBuilder.AppendLine("```");
            contextBuilder.AppendLine();
        }

        try
        {
            // Create the complete context for LocalFoundry
            var userPromptContent = contextBuilder.ToString();

            // Write the complete context to a temp file for manual testing and debugging
            var tempContextFile = Path.GetTempFileName();
            var contextFileName = Path.ChangeExtension(tempContextFile, ".txt");

            var fullContextBuilder = new StringBuilder();
            fullContextBuilder.AppendLine("=== SYSTEM MESSAGE ===");
            fullContextBuilder.AppendLine("You are a senior c# developer who can validate code for issues/problems. Important: ignore commented-out code while performing this validation.");
            fullContextBuilder.AppendLine();
            fullContextBuilder.AppendLine("=== USER MESSAGE ===");
            fullContextBuilder.AppendLine(userPromptContent);

            // Un-comment below to write the prompt to a text file that can be copy/pasted into a chat with a model.
            //await File.WriteAllTextAsync(contextFileName, fullContextBuilder.ToString());
            //Console.WriteLine($"LocalFoundry context written to: {contextFileName}");
            //Console.WriteLine($"Context file size: {new FileInfo(contextFileName).Length} bytes");

            // Create fresh chat client for this validation (no conversation history)
            var chatClient = await CreateFreshChatClientAsync();

            // Build structured messages for better AI comprehension
            List<ChatMessage> messages = new List<ChatMessage>()
            {
                new SystemChatMessage("You are a senior c# developer who can validate code for issues/problems. Important: ignore commented-out code while performing this validation.")
            };

            // Add Code Under Test (CUT)-specific instructions about demo code
            messages.Add(new UserChatMessage(
                "=== CUT-SPECIFIC INSTRUCTIONS ===\n" +
                "This is demo/test code designed to showcase violations.\n" +
                "Comments mentioning 'RULE X VIOLATION' are documentation, not active violations.\n" +
                "Only report actual code violations, ignore explanatory comments.\n" +
                "=== END CUT-SPECIFIC INSTRUCTIONS ==="
            ));

            // Add diffs context if present with disclaimer
            if (diffs is not null && diffs.Length > 0)
            {
                var diffContent = "=== RECENT CHANGES (CONTEXT ONLY - MAY BE UNRELATED) ===\n" +
                                "Note: These changes may be unrelated to the current validation rule.\n" +
                                "Analyze the complete files below, not just the changes.\n\n";

                foreach (var diff in diffs)
                {
                    diffContent += $"```diff\n{diff}\n```\n";
                }
                diffContent += "=== END CONTEXT ===";

                messages.Add(new UserChatMessage(diffContent));
            }

            // Add each file as separate message with clear demarcation
            foreach (var file in contextFiles)
            {
                var fileContent = $"=== FILE: {file.FilePath} ===\n" +
                                $"```csharp\n{file.Content}\n```\n" +
                                $"=== END FILE: {file.FilePath} ===";

                messages.Add(new UserChatMessage(fileContent));
            }

            // Add validation task and format requirements
            messages.Add(new UserChatMessage(
                $"{agentInstructions}\n\n" +
                "=== JSON FORMAT REQUIREMENTS ===\n" +
                "Return JSON format: {{\"passed\": bool, \"violations\": [{{\"file\": \"path\", \"line\": number, \"message\": \"description\"}}], \"explanation\": \"detailed explanation\"}}\n\n" +
                "CRITICAL RULES:\n" +
                "- If the rule PASSES: Set \"passed\": true, \"violations\": [], \"explanation\": \"Rule passed - no violations found\"\n" +
                "- If the rule FAILS: Set \"passed\": false, populate violations array with specific issues, provide detailed explanation\n" +
                "- Do NOT include violations when passed=true\n" +
                "- Do NOT provide explanations about potential violations when the rule passes\n\n" +
                "Your entire response must be valid JSON that can be parsed directly. Do not include any text before or after the JSON."
            ));

            // Get response from LocalFoundry with options
            Console.WriteLine("Calling LocalFoundry with validation prompt...");
            var startTime = DateTime.Now;

            var chatCompletionOptions = new ChatCompletionOptions()
            {
                MaxOutputTokenCount = 16000 // Set high token limit for complete responses (local foundry, no cost concern)
            };

            var response = chatClient.CompleteChat(messages, chatCompletionOptions);
            var endTime = DateTime.Now;
            var responseText = response.Value.Content[0].Text ?? string.Empty;

            Console.WriteLine($"LocalFoundry response time: {(endTime - startTime).TotalSeconds:F2} seconds");

            // Parse the JSON response
            var parsedResponse = ExtractAndParseJsonResponse(responseText);

            return parsedResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error calling LocalFoundry: {ex}");
            return new
            {
                passed = false,
                violations = new object[0],
                explanation = $"Error during LocalFoundry validation: {ex.Message}"
            };
        }
    }

    private static async Task EnsureLocalFoundryInitializedAsync()
    {
        // ALL threads must wait for initialization to complete, even if another thread is doing it
        await ApiValidationStrategy.InitializationSemaphore.WaitAsync();
        try
        {
            // If initialization was already completed by another thread, just return
            if (ApiValidationStrategy.InitializationAttempted is true)
            {
                return;
            }

            // This thread will do the initialization
            ApiValidationStrategy.InitializationAttempted = true;

            Console.WriteLine("Initializing Local Foundry model...");

            var alias = ApiValidationStrategy.LocalFoundryModelAlias;
            var manager = await FoundryLocalManager.StartModelAsync(aliasOrModelId: alias);
            var model = await manager.GetModelInfoAsync(aliasOrModelId: alias);

            // Store manager and model for creating fresh chat clients
            ApiValidationStrategy.LocalFoundryManager = manager;
            ApiValidationStrategy.LocalFoundryModel = model;

            Console.WriteLine("Local Foundry model initialized successfully");
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Failed to initialize Local Foundry model: {ex.Message}");
            Console.WriteLine("Local Foundry commands will not be available.");
            Console.ResetColor();
            ApiValidationStrategy.LocalFoundryManager = null;
            ApiValidationStrategy.LocalFoundryModel = null;
        }
        finally
        {
            ApiValidationStrategy.InitializationSemaphore.Release();
        }
    }

    private static async Task<ChatClient> CreateFreshChatClientAsync()
    {
        if (ApiValidationStrategy.LocalFoundryManager is null || ApiValidationStrategy.LocalFoundryModel is null)
        {
            throw new InvalidOperationException("LocalFoundry manager or model not initialized");
        }

        // Create fresh OpenAI client and chat client for this validation only
        ApiKeyCredential key = new ApiKeyCredential(ApiValidationStrategy.LocalFoundryManager.ApiKey);
        OpenAIClient client = new OpenAIClient(key, new OpenAIClientOptions
        {
            Endpoint = ApiValidationStrategy.LocalFoundryManager.Endpoint,
            NetworkTimeout = TimeSpan.FromMinutes(10), // Set 10 minute timeout for LocalFoundry
            RetryPolicy = new System.ClientModel.Primitives.ClientRetryPolicy(maxRetries: 0) // Disable retries for faster failure
        });

        return client.GetChatClient(ApiValidationStrategy.LocalFoundryModel.ModelId);
    }

    private static object ExtractAndParseJsonResponse(string responseText)
    {
        try
        {
            // First try to parse the entire response as JSON
            if (!string.IsNullOrWhiteSpace(responseText))
            {
                // Remove any potential markdown code blocks
                var cleanedResponse = responseText.Trim();

                // Remove ```json and ``` if present
                if (cleanedResponse.StartsWith("```json"))
                {
                    cleanedResponse = cleanedResponse.Substring(7);
                }
                else if (cleanedResponse.StartsWith("```"))
                {
                    cleanedResponse = cleanedResponse.Substring(3);
                }

                if (cleanedResponse.EndsWith("```"))
                {
                    cleanedResponse = cleanedResponse.Substring(0, cleanedResponse.Length - 3);
                }

                cleanedResponse = cleanedResponse.Trim();

                // Try to extract just the JSON portion if response contains extra content
                cleanedResponse = ExtractJsonFromMixedContent(cleanedResponse);

                // Fix invalid JSON string concatenation (+ operators) that some models generate
                cleanedResponse = FixInvalidJsonStringConcatenation(cleanedResponse);

                // Fix missing closing braces that some models generate
                cleanedResponse = FixMissingClosingBraces(cleanedResponse);

                // Try to parse as JSON
                try
                {
                    var parsed = JsonSerializer.Deserialize<object>(cleanedResponse);
                    return parsed ?? CreateErrorResponse("Parsed response was null");
                }
                catch (JsonException ex) when (ex.Message.Contains("']' is invalid without a matching open") ||
                                                ex.Message.Contains("is invalid after a value") ||
                                                ex.Message.Contains("Expected either ',', '}', or ']'"))
                {
                    // Try various fixes for common model JSON errors
                    Console.WriteLine($"Detected JSON parsing error: {ex.Message}");
                    Console.WriteLine("Attempting to fix common model JSON formatting issues...");

                    // Try 1: Fix ending ] to }
                    if (cleanedResponse.EndsWith("]"))
                    {
                        Console.WriteLine("Attempting fix: replacing ending ']' with '}'");
                        var fixedResponse = cleanedResponse.Substring(0, cleanedResponse.Length - 1) + "}";
                        try
                        {
                            var parsed = JsonSerializer.Deserialize<object>(fixedResponse);
                            return parsed ?? CreateErrorResponse("Parsed response was null after ] to } fix");
                        }
                        catch (JsonException)
                        {
                            Console.WriteLine("Ending ] to } fix failed, trying other approaches...");
                        }
                    }

                    // Try 2: Fix any ] that should be } based on brace counting
                    try
                    {
                        Console.WriteLine("Attempting fix: replacing last ']' with '}' based on brace balance");
                        var fixedResponse = FixLastBracketToCloseBrace(cleanedResponse);
                        if (fixedResponse != cleanedResponse)
                        {
                            var parsed = JsonSerializer.Deserialize<object>(fixedResponse);
                            return parsed ?? CreateErrorResponse("Parsed response was null after bracket balance fix");
                        }
                    }
                    catch (JsonException)
                    {
                        Console.WriteLine("Bracket balance fix failed...");
                    }

                    throw; // Re-throw if we couldn't fix it
                }
            }

            return CreateErrorResponse("Empty response from LocalFoundry");
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"Failed to parse LocalFoundry JSON response: {ex.Message}");
            Console.WriteLine($"LocalFoundry raw response length: {responseText.Length}");
            Console.WriteLine($"LocalFoundry raw response: '{responseText}'");

            return CreateErrorResponse($"Failed to parse JSON response: {ex.Message}");
        }
    }

    private static string ExtractJsonFromMixedContent(string responseText)
    {
        // Some models generate valid JSON at the start followed by extra explanatory text
        // Try to find and extract just the JSON portion

        // Look for a JSON object that starts with { and try to find its matching }
        var startIndex = responseText.IndexOf('{');
        if (startIndex == -1)
        {
            return responseText; // No JSON object found, return as-is
        }

        var braceCount = 0;
        var inString = false;
        var escapeNext = false;

        for (int i = startIndex; i < responseText.Length; i++)
        {
            var currentChar = responseText[i];

            if (escapeNext)
            {
                escapeNext = false;
                continue;
            }

            if (currentChar == '\\')
            {
                escapeNext = true;
                continue;
            }

            if (currentChar == '"')
            {
                inString = !inString;
                continue;
            }

            if (!inString)
            {
                if (currentChar == '{')
                {
                    braceCount++;
                }
                else if (currentChar == '}')
                {
                    braceCount--;
                    if (braceCount == 0)
                    {
                        // Found the end of the JSON object
                        return responseText.Substring(startIndex, i - startIndex + 1);
                    }
                }
            }
        }

        // If we couldn't find a complete JSON object, return the original
        return responseText;
    }

    private static string FixInvalidJsonStringConcatenation(string jsonText)
    {
        // Some models generate invalid JSON with string concatenation using '+'
        // Example: "text1" + "text2" instead of "text1text2"
        // This regex finds and fixes those patterns

        // Pattern: "string1" + "string2" -> "string1string2"
        var pattern = @"""([^""]*)""\s*\+\s*""([^""]*)""";

        // Keep applying the fix until no more matches (handles multiple concatenations)
        string previousText;
        do
        {
            previousText = jsonText;
            jsonText = System.Text.RegularExpressions.Regex.Replace(jsonText, pattern, "\"$1$2\"");
        } while (jsonText != previousText);

        return jsonText;
    }

    private static string FixMissingClosingBraces(string jsonText)
    {
        // Some models generate incomplete JSON that ends with ] instead of }
        // This happens when they forget to close the main JSON object

        jsonText = jsonText.Trim();

        // Count opening and closing braces to see if we're missing any
        var openBraces = jsonText.Count(c => c == '{');
        var closeBraces = jsonText.Count(c => c == '}');

        // If we have unmatched opening braces and the text ends with ],
        // it likely should end with } instead
        if (openBraces > closeBraces && jsonText.EndsWith("]"))
        {
            // Replace the trailing ] with the correct number of }
            var missingBraces = openBraces - closeBraces;
            var braces = new string('}', missingBraces);
            jsonText = jsonText.Substring(0, jsonText.Length - 1) + braces;
        }

        return jsonText;
    }

    private static string FixLastBracketToCloseBrace(string jsonText)
    {
        // Models sometimes use ] where they should use } to close JSON objects
        // Find the last ] and see if replacing it with } would balance the braces

        jsonText = jsonText.Trim();

        var openBraces = jsonText.Count(c => c == '{');
        var closeBraces = jsonText.Count(c => c == '}');
        var openBrackets = jsonText.Count(c => c == '[');
        var closeBrackets = jsonText.Count(c => c == ']');

        // If we have more open braces than close braces, and more close brackets than open brackets,
        // try replacing the last ] with }
        if (openBraces > closeBraces && closeBrackets > openBrackets)
        {
            var lastBracketIndex = jsonText.LastIndexOf(']');
            if (lastBracketIndex >= 0)
            {
                var fixedText = jsonText.Substring(0, lastBracketIndex) + '}' + jsonText.Substring(lastBracketIndex + 1);
                Console.WriteLine($"Replaced ] at position {lastBracketIndex} with }}");
                return fixedText;
            }
        }

        return jsonText;
    }

    private static object CreateErrorResponse(string message)
    {
        return new
        {
            passed = false,
            violations = new object[0],
            explanation = message
        };
    }
}
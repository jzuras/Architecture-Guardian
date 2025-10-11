using System.Text.Json;

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
    GeminiCli,
    LocalFoundry,
    GitHubModels
}

public static class ValidationService
{
    internal static JsonSerializerOptions WriteIndentedJsonSerializerOptions { get; set; } = new JsonSerializerOptions { WriteIndented = true };

    internal static string CommonAiAgentInstructions { get; } = "You are a senior c# developer who is highly skilled at performing code reviews with careful attention to detail.";

    // ARCHGUARD_TEMPLATE_CONSTANT_START
    // TEMPLATE_CHECK_NAME_CONSTANT: DependencyRegistrationAiAgentInstructions
    public static string DependencyRegistrationAiAgentInstructions { get; } = CommonAiAgentInstructions + "Validate Dependency Registration rule: Check that all services referenced in constructors exist in the DI container configuration.Look for services.AddScoped<TypeName>, services.AddSingleton<TypeName>, orservices.AddTransient<TypeName> where TypeName matches the constructor parameter type exactly.If found, the service is properly registered. Do not check whether interfaces are used.";
    // ARCHGUARD_TEMPLATE_CONSTANT_END

    // ARCHGUARD_INSERTION_POINT_CONSTANTS_START
    // New rule constants go here in alphabetical order by rule name

    // ARCHGUARD_GENERATED_RULE_START - ValidateDependencyDirection
    // Generated from template on: 10/7/25
    // DO NOT EDIT - This code will be regenerated
    public static string DependencyDirectionAiAgentInstructions { get; } = CommonAiAgentInstructions + "Validate Dependency Direction rule: Check that Domain layer code does not reference or depend on Infrastructure layer code. Domain namespace should not have 'using' statements importing Infrastructure namespace. Dependencies should flow inward: Infrastructure -> Domain, not Domain -> Infrastructure.";
    // ARCHGUARD_GENERATED_RULE_END - ValidateDependencyDirection

    // ARCHGUARD_GENERATED_RULE_START - ValidateEntityDtoPropertyMapping
    // Generated from template on: 9/17/25
    // DO NOT EDIT - This code will be regenerated
    public static string EntityDtoPropertyMappingAiAgentInstructions { get; } = CommonAiAgentInstructions +  "Analyze this codebase for Entity-DTO property mapping violations. Look for DTO classes that have mismatched property names, missing properties, or inconsistent data types compared to their corresponding domain entities. Report any DTOs where properties don't properly align with their entity counterparts (e.g., 'Id' vs 'UserId', missing required properties, or renamed properties that break mapping conventions).";
    // ARCHGUARD_GENERATED_RULE_END - ValidateEntityDtoPropertyMapping

    // ARCHGUARD_INSERTION_POINT_CONSTANTS_END

    // ARCHGUARD_TEMPLATE_RULE_START
    public static async Task<string> ValidateDependencyRegistrationAsync(ValidationRequest request)
    {
        try
        {
            // Use strategy pattern for validation
            var strategy = GetValidationStrategy(request.SelectedCodingAgent);

            return await strategy.ValidateDependencyRegistrationAsync(request);
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

    // ARCHGUARD_GENERATED_RULE_START - ValidateDependencyDirection
    // Generated from template on: 10/7/25
    // DO NOT EDIT - This code will be regenerated
    public static async Task<string> ValidateDependencyDirectionAsync(ValidationRequest request)
    {
        try
        {
            // Use strategy pattern for validation
            var strategy = GetValidationStrategy(request.SelectedCodingAgent);

            return await strategy.ValidateDependencyDirectionAsync(request);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
            throw new InvalidOperationException($"Error in ValidateDependencyDirectionAsync method: {ex.Message}", ex);
        }
    }
    // ARCHGUARD_GENERATED_RULE_END - ValidateDependencyDirection

    // ARCHGUARD_GENERATED_RULE_START - ValidateEntityDtoPropertyMapping
    // Generated from template on: 9/17/25
    // DO NOT EDIT - This code will be regenerated
    public static async Task<string> ValidateEntityDtoPropertyMappingAsync(ValidationRequest request)
    {
        try
        {
            // Use strategy pattern for validation
            var strategy = GetValidationStrategy(request.SelectedCodingAgent);

            return await strategy.ValidateEntityDtoPropertyMappingAsync(request);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
            throw new InvalidOperationException($"Error in ValidateEntityDtoPropertyMappingAsync method: {ex.Message}", ex);
        }
    }
    // ARCHGUARD_GENERATED_RULE_END - ValidateEntityDtoPropertyMapping

    // ARCHGUARD_INSERTION_POINT_METHODS_END

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

    #region Private Helper Methods
    // Strategy factory method
    private static IValidationStrategy GetValidationStrategy(CodingAgent codingAgent)
    {
        return codingAgent switch
        {
            CodingAgent.LocalFoundry => new ApiValidationStrategy(),
            CodingAgent.GitHubModels => new ApiValidationStrategy(),
            CodingAgent.ClaudeCode => new FileSystemValidationStrategy(),
            CodingAgent.GeminiCli => new FileSystemValidationStrategy(),
            _ => new FileSystemValidationStrategy()
        };
    }
    #endregion
}

public record ContextFile(string FilePath, string Content, string? Diff = null);

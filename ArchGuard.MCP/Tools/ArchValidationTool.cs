using ArchGuard.Shared;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace ArchGuard_MCP.Tools;

[McpServerToolType, Description("I am a C# code analyzer, guardian of architectural guidelines.")]
public static class ArchValidationTool
{
    public static CodingAgent SelectedCodingAgent { get; set; } = CodingAgent.ClaudeCode;

    // ARCHGUARD_TEMPLATE_CONSTANT_START
    // TEMPLATE_CHECK_NAME_CONSTANT: DependencyRegistrationMcpToolDescription
    public static string DependencyRegistrationMcpToolDescription = "Validates that all services referenced in constructors are properly registered in the DI container.";
    // ARCHGUARD_TEMPLATE_CONSTANT_END

    // ARCHGUARD_INSERTION_POINT_CONSTANTS_START
    // New rule constants go here in alphabetical order by rule name

    // ARCHGUARD_GENERATED_RULE_START - ValidateEntityDtoPropertyMapping
    // Generated from template on: 9/17/25
    // DO NOT EDIT - This code will be regenerated
    public static string EntityDtoPropertyMappingMcpToolDescription = "Validates that Data Transfer Object (DTO) classes have properties that properly correspond to their associated domain entity properties. Checks for property name mismatches, missing properties, and inconsistent data types between entities and DTOs to ensure proper object mapping and data consistency.";
    // ARCHGUARD_GENERATED_RULE_END - ValidateEntityDtoPropertyMapping

    // ARCHGUARD_INSERTION_POINT_CONSTANTS_END

    // ARCHGUARD_TEMPLATE_RULE_START
    /// <summary>
    /// Validates that all services referenced in constructors are properly registered in the DI container
    /// </summary>
    /// <param name="contextFiles">Array of file contents with their paths for analysis</param>
    /// <param name="diffs">Optional array of git diffs showing recent changes</param>
    /// <returns>Validation result indicating pass/fail with detailed explanation</returns>
    [McpServerTool(UseStructuredContent = true)] // Name, Title, and Desc are set in Program.cs setup code.
    public static async Task<string> ValidateDependencyRegistrationAsync(
        IMcpServer server,
        ContextFile[] contextFiles,
        string[]? diffs = null)
    {
        // TEMPLATE_METHOD_COMMENT: Note - this tool is only checking constructors, for simplicity reasons.

        try
        {
            var root = await ArchValidationTool.GetRootOrThrowExceptionAsync(server);

            var validationRequest = new ValidationRequest();

            // Convert MCP root (file:// URI) to both Windows and WSL paths
            validationRequest.WindowsRoot = ConvertFileUriToWindowsPath(root);
            validationRequest.WslRoot = ConvertFileUriToWslPath(root);
            validationRequest.ContextFiles = contextFiles;
            validationRequest.Diffs = diffs;
            validationRequest.SelectedCodingAgent = ArchValidationTool.SelectedCodingAgent;

            return await ValidationService.ValidateDependencyRegistrationAsync(validationRequest);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
            throw new McpException($"Error in ValidateDependencyRegistrationAsync method: {ex.Message}");
        }
    }
    // ARCHGUARD_TEMPLATE_RULE_END

    // ARCHGUARD_INSERTION_POINT_METHODS_START
    // New rule methods go here in alphabetical order by rule name

    // ARCHGUARD_GENERATED_RULE_START - ValidateEntityDtoPropertyMapping
    // Generated from template on: 9/17/25
    // DO NOT EDIT - This code will be regenerated
    /// <summary>
    /// Validates that Data Transfer Object (DTO) classes have properties that properly correspond to their associated domain entity properties
    /// </summary>
    /// <param name="contextFiles">Array of file contents with their paths for analysis</param>
    /// <param name="diffs">Optional array of git diffs showing recent changes</param>
    /// <returns>Validation result indicating pass/fail with detailed explanation</returns>
    [McpServerTool(UseStructuredContent = true)] // Name, Title, and Desc are set in Program.cs setup code.
    public static async Task<string> ValidateEntityDtoPropertyMappingAsync(
        IMcpServer server,
        ContextFile[] contextFiles,
        string[]? diffs = null)
    {
        // Method comment goes here

        try
        {
            var root = await ArchValidationTool.GetRootOrThrowExceptionAsync(server);

            var validationRequest = new ValidationRequest();

            // Convert MCP root (file:// URI) to both Windows and WSL paths
            validationRequest.WindowsRoot = ConvertFileUriToWindowsPath(root);
            validationRequest.WslRoot = ConvertFileUriToWslPath(root);
            validationRequest.ContextFiles = contextFiles;
            validationRequest.Diffs = diffs;
            validationRequest.SelectedCodingAgent = ArchValidationTool.SelectedCodingAgent;

            return await ValidationService.ValidateEntityDtoPropertyMappingAsync(validationRequest);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
            throw new McpException($"Error in ValidateEntityDtoPropertyMappingAsync method: {ex.Message}");
        }
    }
    // ARCHGUARD_GENERATED_RULE_END - ValidateEntityDtoPropertyMapping

    // ARCHGUARD_INSERTION_POINT_METHODS_END

    #region Helper Functions
    private static async Task<string> GetRootOrThrowExceptionAsync(IMcpServer server)
    {
        var roots = await server.RequestRootsAsync(new ListRootsRequestParams());
        var rootsList = roots.Roots.ToList();

        // IMPORTANT: Not currently handling more than a single root.
        var root = rootsList.FirstOrDefault();
        if (root is null)
        {
            throw new McpException($"No roots available, unable to proceed.");
        }

        Console.WriteLine($"\n[MCP] Using root: {root.Uri}, Name: {root.Name}");

        return root.Uri;
    }

    private static string ConvertFileUriToWindowsPath(string fileUri)
    {
        if (string.IsNullOrEmpty(fileUri))
            return string.Empty;

        // Handle file:// URIs - remove the file:// prefix and convert to Windows path
        if (fileUri.StartsWith("file:///"))
        {
            string pathPart = fileUri.Substring(8); // Remove "file:///"

            // Handle drive letters (e.g., "C:" -> "C:")
            if (pathPart.Length >= 2 && pathPart[1] == ':')
            {
                return pathPart.Replace("/", "\\");
            }
        }

        // If not a file URI or already a Windows path, return as-is
        return fileUri;
    }

    private static string ConvertFileUriToWslPath(string fileUri)
    {
        if (string.IsNullOrEmpty(fileUri))
            return string.Empty;

        // Handle file:// URIs
        if (fileUri.StartsWith("file:///"))
        {
            string pathPart = fileUri.Substring(8); // Remove "file:///"

            // Handle drive letters (e.g., "C:" -> "/mnt/c")
            if (pathPart.Length >= 2 && pathPart[1] == ':')
            {
                char driveLetter = char.ToLower(pathPart[0]);
                string remainingPath = pathPart.Substring(2);
                return $"/mnt/{driveLetter}{remainingPath.Replace("\\", "/")}";
            }
        }

        // If not a recognized format, use ValidationService conversion logic
        return ValidationService.ConvertToWslPath(fileUri);
    }
    #endregion
}


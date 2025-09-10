using ArchGuard.Shared;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace ArchGuard_MCP.Tools;

[McpServerToolType, Description("I am a C# code analyzer, guardian of architectural guidelines.")]
public static class ArchValidationTool
{
    /// <summary>
    /// Validates that all services referenced in constructors are properly registered in the DI container
    /// </summary>
    /// <param name="contextFiles">Array of file contents with their paths for analysis</param>
    /// <param name="diffs">Optional array of git diffs showing recent changes</param>
    /// <returns>Validation result indicating pass/fail with detailed explanation</returns>
    [McpServerTool(UseStructuredContent = true, Title = "Validate Dependency Registration", Name = "ValidateDependencyRegistration"),
        Description("Validates that all services referenced in constructors are properly registered in the DI container.")]
    public static async Task<string> ValidateDependencyRegistrationAsync(
        IMcpServer server,
        ContextFile[] contextFiles,
        string[]? diffs = null,
        string rootFromWebhook = "")
    {
        // Note - this tool is only checking constructors, for simplicity reasons.

        try
        {
            var root = rootFromWebhook;
            if (string.IsNullOrEmpty(root))
            {
                root = await ArchValidationTool.GetRootOrThrowExceptionAsync(server);
            }
     
            return await ValidationService.ValidateDependencyRegistrationAsync(root, contextFiles, diffs);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
            throw new McpException($"Error in ValidateDependencyRegistrationAsync method: {ex.Message}");
        }
    }

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
    #endregion
}


using System.Text.RegularExpressions;

namespace ArchGuard.MCP.Services;

public class PathConverter : IPathConverter
{
    private ILogger<PathConverter> Logger { get; set; }

    public PathConverter(ILogger<PathConverter> logger)
    {
        this.Logger = logger;
    }

    public string ConvertForAgent(string windowsPath, CodingAgentType agentType)
    {
        try
        {
            return agentType switch
            {
                CodingAgentType.ClaudeCode => this.ConvertToWslPath(windowsPath),
                CodingAgentType.WindowsNative => windowsPath,
                _ => throw new ArgumentOutOfRangeException(nameof(agentType), agentType, "Unknown coding agent type")
            };
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Failed to convert path {WindowsPath} for agent type {AgentType}", 
                windowsPath, agentType);
            throw;
        }
    }

    private string ConvertToWslPath(string windowsPath)
    {
        if (string.IsNullOrWhiteSpace(windowsPath))
        {
            return windowsPath;
        }

        var normalizedPath = windowsPath.Replace('\\', '/');

        var driveLetterRegex = new Regex(@"^([A-Za-z]):", RegexOptions.Compiled);
        var match = driveLetterRegex.Match(normalizedPath);

        if (match.Success)
        {
            var driveLetter = match.Groups[1].Value.ToLower();
            var pathWithoutDrive = normalizedPath.Substring(2);
            var wslPath = $"/mnt/{driveLetter}{pathWithoutDrive}";
            
            this.Logger.LogDebug("Converted Windows path {WindowsPath} to WSL path {WslPath}", 
                windowsPath, wslPath);
            
            return wslPath;
        }

        this.Logger.LogWarning("Path {WindowsPath} does not appear to be a Windows drive path, returning as-is", 
            windowsPath);
        return normalizedPath;
    }
}
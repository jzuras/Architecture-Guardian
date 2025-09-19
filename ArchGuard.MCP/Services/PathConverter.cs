using ArchGuard.Shared;

namespace ArchGuard.MCP.Services;

public class PathConverter : IPathConverter
{
    private ILogger<PathConverter> Logger { get; set; }

    public PathConverter(ILogger<PathConverter> logger)
    {
        this.Logger = logger;
    }

    public string ConvertToWslPath(string windowsPath)
    {
        var result = ValidationService.ConvertToWslPath(windowsPath);

        this.Logger.LogDebug("Converted Windows path {WindowsPath} to WSL path {WslPath}",
            windowsPath, result);

        return result;
    }
}
namespace ArchGuard.MCP.Services;

public interface IPathConverter
{
    string ConvertToWslPath(string windowsPath);
}
namespace ArchGuard.MCP.Services;

public enum CodingAgentType 
{
    ClaudeCode,
    WindowsNative
}

public interface IPathConverter
{
    string ConvertForAgent(string windowsPath, CodingAgentType agentType);
}
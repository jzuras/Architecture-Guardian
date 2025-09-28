namespace ArchGuard.Shared;

// Data transfer object for validation strategy pattern
public class ValidationRequest
{
    public string WindowsRoot { get; set; } = string.Empty;
    public string WslRoot { get; set; } = string.Empty;
    public ContextFile[] ContextFiles { get; set; } = Array.Empty<ContextFile>();
    public string[]? Diffs { get; set; }
    public CodingAgent SelectedCodingAgent { get; set; }
}
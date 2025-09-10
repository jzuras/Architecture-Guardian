using ArchGuard.MCP.Models;
using System.Diagnostics;
using System.Text;

namespace ArchGuard.MCP.Services;

public class RepositoryCloneService : IRepositoryCloneService
{
    private GitHubAppAuthService AuthService { get; set; }
    private IPathConverter PathConverter { get; set; }
    private ILogger<RepositoryCloneService> Logger { get; set; }
    private IConfiguration Configuration { get; set; }

    #region Constructor

    public RepositoryCloneService(
        GitHubAppAuthService authService,
        IPathConverter pathConverter,
        ILogger<RepositoryCloneService> logger,
        IConfiguration configuration)
    {
        this.AuthService = authService;
        this.PathConverter = pathConverter;
        this.Logger = logger;
        this.Configuration = configuration;
    }

    #endregion

    #region Public Methods

    public async Task<CloneResult> CloneRepositoryAsync(string cloneUrl, string commitOrBranch, string repoFullName)
    {
        try
        {
            var tempDir = this.CreateTempDirectory(repoFullName, commitOrBranch);
            this.Logger.LogInformation("Cloning repository {RepoFullName} at {CommitOrBranch} to {TempDir}", 
                repoFullName, commitOrBranch, tempDir);

            var cloneResult = await this.CloneWithAuthenticationAsync(cloneUrl, tempDir, commitOrBranch);
            
            if (!cloneResult.Success)
            {
                return cloneResult;
            }

            var codingAgentType = this.GetCodingAgentType();
            var agentPath = this.PathConverter.ConvertForAgent(tempDir, codingAgentType);

            return new CloneResult
            {
                LocalPath = tempDir,
                AgentPath = agentPath,
                CommitSha = commitOrBranch,
                Success = true,
                ErrorMessage = string.Empty,
                ClonedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Failed to clone repository {RepoFullName} at {CommitOrBranch}", 
                repoFullName, commitOrBranch);
            return new CloneResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                ClonedAt = DateTime.UtcNow
            };
        }
    }

    public async Task CleanupRepositoryAsync(string clonePath)
    {
        try
        {
            if (Directory.Exists(clonePath))
            {
                this.Logger.LogInformation("Cleaning up repository at {ClonePath}", clonePath);
                
                await Task.Run(() =>
                {
                    this.RemoveReadOnlyAttributes(clonePath);
                    Directory.Delete(clonePath, recursive: true);
                });
                
                this.Logger.LogInformation("Successfully cleaned up repository at {ClonePath}", clonePath);
            }
            else
            {
                this.Logger.LogWarning("Repository path {ClonePath} does not exist, skipping cleanup", clonePath);
            }
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Failed to cleanup repository at {ClonePath}", clonePath);
            throw;
        }
    }

    private void RemoveReadOnlyAttributes(string directoryPath)
    {
        try
        {
            var dirInfo = new DirectoryInfo(directoryPath);
            
            if (dirInfo.Attributes.HasFlag(FileAttributes.ReadOnly))
            {
                dirInfo.Attributes &= ~FileAttributes.ReadOnly;
            }
            
            foreach (var file in dirInfo.GetFiles("*", SearchOption.AllDirectories))
            {
                if (file.Attributes.HasFlag(FileAttributes.ReadOnly))
                {
                    file.Attributes &= ~FileAttributes.ReadOnly;
                }
            }
            
            foreach (var subDir in dirInfo.GetDirectories("*", SearchOption.AllDirectories))
            {
                if (subDir.Attributes.HasFlag(FileAttributes.ReadOnly))
                {
                    subDir.Attributes &= ~FileAttributes.ReadOnly;
                }
            }
        }
        catch (Exception ex)
        {
            this.Logger.LogWarning(ex, "Failed to remove read-only attributes from {DirectoryPath}", directoryPath);
        }
    }

    #endregion

    #region Private Methods

    private string CreateTempDirectory(string repoFullName, string commitSha)
    {
        var baseTempPath = Path.GetTempPath();
        var sanitizedRepoName = repoFullName.Replace('/', '-').Replace('\\', '-');
        var shortCommitSha = commitSha.Length > 8 ? commitSha.Substring(0, 8) : commitSha;
        var directoryName = $"{sanitizedRepoName}-{shortCommitSha}";
        var tempDir = Path.Combine(baseTempPath, "archguard-clones", directoryName);

        if (Directory.Exists(tempDir))
        {
            this.Logger.LogInformation("Temp directory {TempDir} already exists, cleaning up first", tempDir);
            this.RemoveReadOnlyAttributes(tempDir);
            Directory.Delete(tempDir, recursive: true);
        }

        Directory.CreateDirectory(tempDir);
        return tempDir;
    }

    private async Task<CloneResult> CloneWithAuthenticationAsync(string cloneUrl, string tempDir, string reference)
    {
        var retryAttempts = this.Configuration.GetValue("RepositoryCloning:RetryAttempts", 3);
        var retryDelaySeconds = this.Configuration.GetValue("RepositoryCloning:RetryDelaySeconds", 5);

        for (int attempt = 1; attempt <= retryAttempts; attempt++)
        {
            try
            {
                var cloneResult = await this.ExecuteGitCloneAsync(cloneUrl, tempDir, reference, attempt == retryAttempts);
                
                if (cloneResult.Success)
                {
                    return cloneResult;
                }

                if (attempt < retryAttempts)
                {
                    this.Logger.LogWarning("Clone attempt {Attempt} failed, retrying in {DelaySeconds} seconds", 
                        attempt, retryDelaySeconds);
                    await Task.Delay(TimeSpan.FromSeconds(retryDelaySeconds * attempt));
                }
            }
            catch (Exception ex)
            {
                if (attempt == retryAttempts)
                {
                    this.Logger.LogError(ex, "Final clone attempt {Attempt} failed", attempt);
                    throw;
                }

                this.Logger.LogWarning(ex, "Clone attempt {Attempt} failed, retrying in {DelaySeconds} seconds", 
                    attempt, retryDelaySeconds);
                await Task.Delay(TimeSpan.FromSeconds(retryDelaySeconds * attempt));
            }
        }

        return new CloneResult
        {
            Success = false,
            ErrorMessage = "All clone attempts failed",
            ClonedAt = DateTime.UtcNow
        };
    }

    private async Task<CloneResult> ExecuteGitCloneAsync(string cloneUrl, string tempDir, string reference, bool isLastAttempt)
    {
        try
        {
            var result = await this.TryCloneWithAuthAsync(cloneUrl, tempDir, reference);
            if (result.Success)
            {
                return result;
            }

            if (isLastAttempt)
            {
                this.Logger.LogInformation("Attempting public clone as fallback for {CloneUrl}", cloneUrl);
                return await this.TryPublicCloneAsync(cloneUrl, tempDir, reference);
            }

            return result;
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Git clone execution failed for {CloneUrl}", cloneUrl);
            return new CloneResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                ClonedAt = DateTime.UtcNow
            };
        }
    }

    private async Task<CloneResult> TryCloneWithAuthAsync(string cloneUrl, string tempDir, string reference)
    {
        try
        {
            var authenticatedUrl = this.CreateAuthenticatedUrl(cloneUrl);
            return await this.ExecuteGitCommandAsync(authenticatedUrl, tempDir, reference);
        }
        catch (Exception ex)
        {
            this.Logger.LogWarning(ex, "Authenticated clone failed for {CloneUrl}", cloneUrl);
            return new CloneResult
            {
                Success = false,
                ErrorMessage = $"Authenticated clone failed: {ex.Message}",
                ClonedAt = DateTime.UtcNow
            };
        }
    }

    private async Task<CloneResult> TryPublicCloneAsync(string cloneUrl, string tempDir, string reference)
    {
        try
        {
            this.Logger.LogInformation("Attempting public clone for {CloneUrl}", cloneUrl);
            return await this.ExecuteGitCommandAsync(cloneUrl, tempDir, reference);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Public clone failed for {CloneUrl}", cloneUrl);
            return new CloneResult
            {
                Success = false,
                ErrorMessage = $"Both authenticated and public clone failed: {ex.Message}",
                ClonedAt = DateTime.UtcNow
            };
        }
    }

    private string CreateAuthenticatedUrl(string cloneUrl)
    {
        // For webhook-triggered clones, GitHub provides pre-authenticated CloneUrls
        // No additional authentication needed - use the provided URL directly
        this.Logger.LogInformation("Using provided CloneUrl (GitHub webhook provides authentication for private repos)");
        return cloneUrl;
    }

    private async Task<CloneResult> ExecuteGitCommandAsync(string cloneUrl, string tempDir, string reference)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"clone \"{cloneUrl}\" \"{tempDir}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        var output = new StringBuilder();
        var error = new StringBuilder();

        process.OutputDataReceived += (_, e) => {
            if (e.Data is not null)
            {
                output.AppendLine(e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) => {
            if (e.Data is not null)
            {
                error.AppendLine(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var timeout = this.Configuration.GetValue("RepositoryCloning:TimeoutMinutes", 10);
        var completed = await Task.Run(() => process.WaitForExit(TimeSpan.FromMinutes(timeout)));

        if (!completed)
        {
            process.Kill();
            return new CloneResult
            {
                Success = false,
                ErrorMessage = $"Git clone timed out after {timeout} minutes",
                ClonedAt = DateTime.UtcNow
            };
        }

        if (process.ExitCode != 0)
        {
            var errorMessage = error.ToString();
            this.Logger.LogError("Git clone failed with exit code {ExitCode}: {ErrorMessage}", 
                process.ExitCode, errorMessage);
            return new CloneResult
            {
                Success = false,
                ErrorMessage = errorMessage,
                ClonedAt = DateTime.UtcNow
            };
        }

        if (reference != "HEAD" && !reference.StartsWith("refs/heads/"))
        {
            await this.CheckoutSpecificCommitAsync(tempDir, reference);
        }

        return new CloneResult
        {
            Success = true,
            ErrorMessage = string.Empty,
            ClonedAt = DateTime.UtcNow
        };
    }

    private async Task CheckoutSpecificCommitAsync(string repoPath, string commitSha)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"checkout {commitSha}",
                WorkingDirectory = repoPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        process.Start();
        await Task.Run(() => process.WaitForExit());

        if (process.ExitCode != 0)
        {
            this.Logger.LogWarning("Failed to checkout commit {CommitSha}, repository may be at wrong commit", commitSha);
        }
    }

    private CodingAgentType GetCodingAgentType()
    {
        var agentTypeString = this.Configuration.GetValue("RepositoryCloning:CodingAgentType", "ClaudeCode");
        
        if (Enum.TryParse<CodingAgentType>(agentTypeString, ignoreCase: true, out var agentType))
        {
            return agentType;
        }

        this.Logger.LogWarning("Invalid CodingAgentType configured: {AgentType}, defaulting to ClaudeCode", agentTypeString);
        return CodingAgentType.ClaudeCode;
    }

    #endregion
}
namespace ArchGuard.MCP.Services;

public class RepositoryCleanupService : BackgroundService
{
    private ILogger<RepositoryCleanupService> Logger { get; set; }
    private IConfiguration Configuration { get; set; }

    public RepositoryCleanupService(ILogger<RepositoryCleanupService> logger, IConfiguration configuration)
    {
        this.Logger = logger;
        this.Configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // How often to check for expired repositories (not how long to keep them)
        var intervalMinutes = this.Configuration.GetValue("RepositoryCloning:CleanupIntervalMinutes", 60);
        var interval = TimeSpan.FromMinutes(intervalMinutes);

        this.Logger.LogInformation("Repository cleanup service started with {IntervalMinutes} minute check interval", intervalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Check for and delete repositories older than MaxRetentionHours
                await this.CleanupExpiredRepositoriesAsync();
                // Wait for the configured check interval before checking again
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                this.Logger.LogInformation("Repository cleanup service is stopping");
                break;
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex, "Error during repository cleanup cycle");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }

    private async Task CleanupExpiredRepositoriesAsync()
    {
        try
        {
            var basePath = Path.Combine(Path.GetTempPath(), "archguard-clones");
            
            if (!Directory.Exists(basePath))
            {
                this.Logger.LogDebug("Cleanup base path {BasePath} does not exist, nothing to clean", basePath);
                return;
            }

            // MaxRetentionHours determines how long repositories are kept (separate from check interval)
            var maxRetentionHours = this.Configuration.GetValue("RepositoryCloning:MaxRetentionHours", 2);
            var cutoffTime = DateTime.UtcNow.AddHours(-maxRetentionHours);

            this.Logger.LogDebug("Starting cleanup of repositories older than {CutoffTime} (MaxRetentionHours: {MaxRetentionHours})", cutoffTime, maxRetentionHours);

            var directories = Directory.GetDirectories(basePath);
            var cleanedCount = 0;
            var totalSize = 0L;

            foreach (var directory in directories)
            {
                try
                {
                    var dirInfo = new DirectoryInfo(directory);
                    
                    if (dirInfo.CreationTimeUtc < cutoffTime)
                    {
                        var sizeBefore = await this.GetDirectorySizeAsync(directory);
                        
                        this.Logger.LogInformation("Cleaning up expired repository clone: {Directory} (Created: {CreatedTime}, Size: {SizeMB} MB)", 
                            directory, dirInfo.CreationTimeUtc, sizeBefore / (1024 * 1024));

                        await this.DeleteDirectoryAsync(directory);
                        
                        cleanedCount++;
                        totalSize += sizeBefore;
                    }
                }
                catch (Exception ex)
                {
                    this.Logger.LogError(ex, "Failed to cleanup directory {Directory}", directory);
                }
            }

            if (cleanedCount > 0)
            {
                this.Logger.LogInformation("Cleanup completed: {CleanedCount} directories removed, {TotalSizeMB} MB freed", 
                    cleanedCount, totalSize / (1024 * 1024));
            }
            else
            {
                this.Logger.LogDebug("No expired repositories found for cleanup");
            }

            await this.CheckDiskSpaceAsync(basePath);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error during repository cleanup");
            throw;
        }
    }

    private async Task<long> GetDirectorySizeAsync(string directoryPath)
    {
        return await Task.Run(() =>
        {
            try
            {
                var dirInfo = new DirectoryInfo(directoryPath);
                return this.CalculateDirectorySize(dirInfo);
            }
            catch (Exception ex)
            {
                this.Logger.LogWarning(ex, "Failed to calculate size for directory {DirectoryPath}", directoryPath);
                return 0L;
            }
        });
    }

    private long CalculateDirectorySize(DirectoryInfo directory)
    {
        long size = 0;

        try
        {
            var files = directory.GetFiles();
            foreach (var file in files)
            {
                size += file.Length;
            }

            var subdirectories = directory.GetDirectories();
            foreach (var subdirectory in subdirectories)
            {
                size += this.CalculateDirectorySize(subdirectory);
            }
        }
        catch (UnauthorizedAccessException)
        {
            this.Logger.LogDebug("Access denied calculating size for {DirectoryPath}", directory.FullName);
        }
        catch (Exception ex)
        {
            this.Logger.LogWarning(ex, "Error calculating size for {DirectoryPath}", directory.FullName);
        }

        return size;
    }

    private async Task DeleteDirectoryAsync(string directoryPath)
    {
        await Task.Run(() =>
        {
            try
            {
                if (Directory.Exists(directoryPath))
                {
                    this.RemoveReadOnlyAttributes(directoryPath);
                    Directory.Delete(directoryPath, recursive: true);
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                this.Logger.LogError(ex, "Access denied deleting directory {DirectoryPath}", directoryPath);
                throw;
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex, "Error deleting directory {DirectoryPath}", directoryPath);
                throw;
            }
        });
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

    private async Task CheckDiskSpaceAsync(string basePath)
    {
        try
        {
            var driveInfo = new DriveInfo(Path.GetPathRoot(basePath) ?? "C:");
            var availableSpaceGB = driveInfo.AvailableFreeSpace / (1024 * 1024 * 1024);
            var totalSpaceGB = driveInfo.TotalSize / (1024 * 1024 * 1024);
            var usedSpacePercent = ((double)(driveInfo.TotalSize - driveInfo.AvailableFreeSpace) / driveInfo.TotalSize) * 100;

            this.Logger.LogDebug("Disk space: {AvailableGB} GB available of {TotalGB} GB total ({UsedPercent:F1}% used)", 
                availableSpaceGB, totalSpaceGB, usedSpacePercent);

            if (availableSpaceGB < 1)
            {
                this.Logger.LogWarning("Low disk space detected: {AvailableGB} GB available, initiating emergency cleanup", availableSpaceGB);
                await this.EmergencyCleanupAsync(basePath);
            }
        }
        catch (Exception ex)
        {
            this.Logger.LogWarning(ex, "Failed to check disk space");
        }
    }

    private async Task EmergencyCleanupAsync(string basePath)
    {
        try
        {
            this.Logger.LogWarning("Starting emergency cleanup of all repositories in {BasePath}", basePath);

            var directories = Directory.GetDirectories(basePath);
            var cleanedCount = 0;

            foreach (var directory in directories)
            {
                try
                {
                    this.Logger.LogInformation("Emergency cleanup: removing {Directory}", directory);
                    await this.DeleteDirectoryAsync(directory);
                    cleanedCount++;
                }
                catch (Exception ex)
                {
                    this.Logger.LogError(ex, "Failed to emergency cleanup directory {Directory}", directory);
                }
            }

            this.Logger.LogWarning("Emergency cleanup completed: {CleanedCount} directories removed", cleanedCount);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error during emergency cleanup");
            throw;
        }
    }
}
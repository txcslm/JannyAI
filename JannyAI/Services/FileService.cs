using JannyAI.Configuration;
using JannyAI.Models;
using JannyAI.Services.Interfaces;

namespace JannyAI.Services;

public class FileService : IFileService
{
    private readonly ILogger _logger;

    public FileService(ILogger logger)
    {
        _logger = logger;
    }

    public async Task InitializeDirectoriesAsync()
    {
        Directory.CreateDirectory(ScrapingConfig.DownloadFolderPath);
        Directory.CreateDirectory(ScrapingConfig.TemporaryDownloadFolderPath);
        Directory.CreateDirectory(ScrapingConfig.ChromeProfileFolderPath);
        
        await _logger.LogInfoAsync($"Directories created - Download: {ScrapingConfig.DownloadFolderPath}, Temp: {ScrapingConfig.TemporaryDownloadFolderPath}, Profile: {ScrapingConfig.ChromeProfileFolderPath}");
    }

    public async Task<HashSet<string>> LoadDownloadedCharacterIdsAsync()
    {
        var downloadedIds = new HashSet<string>();
        
        if (File.Exists(ScrapingConfig.CharacterIndexFilePath))
        {
            var characterIdLines = await File.ReadAllLinesAsync(ScrapingConfig.CharacterIndexFilePath);
            foreach (var line in characterIdLines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    downloadedIds.Add(line.Trim());
                }
            }
        }
        
        await _logger.LogInfoAsync($"Character index loaded, found {downloadedIds.Count} existing characters");
        return downloadedIds;
    }

    public async Task SaveDownloadedCharacterIdAsync(string characterId)
    {
        await File.AppendAllTextAsync(ScrapingConfig.CharacterIndexFilePath, characterId + Environment.NewLine);
        await _logger.LogInfoAsync($"Character ID saved to index: {characterId}");
    }

    public async Task<string?> WaitForNewFileDownloadAsync(int filesCountBefore, string characterId)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await _logger.LogInfoAsync($"Waiting for download file for character {characterId}, files before: {filesCountBefore}");

        while (stopwatch.ElapsedMilliseconds < ScrapingConfig.DownloadTimeoutMilliseconds)
        {
            var currentFiles = Directory.GetFiles(ScrapingConfig.TemporaryDownloadFolderPath);
            
            if (currentFiles.Length > filesCountBefore)
            {
                var latestFile = currentFiles.OrderByDescending(filePath => File.GetCreationTime(filePath)).First();
                var fileInfo = new FileInfo(latestFile);

                await _logger.LogInfoAsync($"New file detected for {characterId}: {latestFile}, size: {fileInfo.Length} bytes");

                if (!latestFile.EndsWith(".crdownload") && !latestFile.EndsWith(".tmp") && fileInfo.Length > 0)
                {
                    await Task.Delay(ScrapingConfig.FileStabilityCheckDelayMilliseconds);
                    fileInfo.Refresh();

                    if (fileInfo.Length > 0)
                    {
                        await _logger.LogInfoAsync($"File download confirmed for {characterId} - stable size: {fileInfo.Length} bytes");
                        return latestFile;
                    }
                }
            }

            await Task.Delay(ScrapingConfig.FileCheckIntervalMilliseconds);
        }

        await _logger.LogWarningAsync($"Download timeout for character {characterId} after {ScrapingConfig.DownloadTimeoutMilliseconds}ms");
        return null;
    }

    public async Task<bool> MoveDownloadedFileAsync(string characterId, string sourceFilePath)
    {
        try
        {
            var extension = Path.GetExtension(sourceFilePath).ToLower();
            
            if (extension != ".png" && extension != ".webp" && extension != ".jpg" && extension != ".jpeg")
            {
                await _logger.LogWarningAsync($"Unexpected file extension: {extension}. Will save as PNG.");
            }
            
            var targetFilePath = Path.Combine(ScrapingConfig.DownloadFolderPath, $"{characterId}.png");
            
            if (File.Exists(targetFilePath))
            {
                await _logger.LogWarningAsync($"Target file already exists, overwriting: {targetFilePath}");
                File.Delete(targetFilePath);
            }
            
            File.Move(sourceFilePath, targetFilePath);
            
            if (File.Exists(targetFilePath))
            {
                var targetFileInfo = new FileInfo(targetFilePath);
                await _logger.LogInfoAsync($"File successfully saved for {characterId}, size: {targetFileInfo.Length} bytes");
                return true;
            }
            else
            {
                await _logger.LogErrorAsync($"File move failed for {characterId}");
                return false;
            }
        }
        catch (Exception ex)
        {
            await _logger.LogErrorAsync($"Error moving file for character {characterId}: {ex.Message}", ex);
            return false;
        }
    }

    public void ClearTemporaryDownloadFolder()
    {
        try
        {
            var files = Directory.GetFiles(ScrapingConfig.TemporaryDownloadFolderPath);
            foreach (var file in files)
            {
                try
                {
                    File.Delete(file);
                }
                catch
                {
                    // Ignore individual file deletion errors
                }
            }
        }
        catch
        {
            // Ignore general cleanup errors
        }
    }

    public async Task CleanupTemporaryFilesAsync()
    {
        try
        {
            if (Directory.Exists(ScrapingConfig.TemporaryDownloadFolderPath))
            {
                Directory.Delete(ScrapingConfig.TemporaryDownloadFolderPath, true);
                await _logger.LogInfoAsync("Temporary download folder deleted");
            }
        }
        catch (Exception ex)
        {
            await _logger.LogErrorAsync($"Error deleting temp folder: {ex.Message}", ex);
        }
    }
}
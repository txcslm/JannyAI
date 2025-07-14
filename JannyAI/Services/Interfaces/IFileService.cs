using JannyAI.Models;

namespace JannyAI.Services.Interfaces;

public interface IFileService
{
    Task InitializeDirectoriesAsync();
    Task<HashSet<string>> LoadDownloadedCharacterIdsAsync();
    Task SaveDownloadedCharacterIdAsync(string characterId);
    Task<bool> MoveDownloadedFileAsync(string characterId, string sourceFilePath);
    void ClearTemporaryDownloadFolder();
    Task<string?> WaitForNewFileDownloadAsync(int filesCountBefore, string characterId);
    Task CleanupTemporaryFilesAsync();
}
namespace JannyAI.Services.Interfaces;

public interface IGitHubService
{
    Task<bool> UploadFileAsync(string characterId, byte[] fileContent, string fileName);
    Task<bool> FileExistsAsync(string characterId, string fileName);
    Task<string> GetFileUrlAsync(string characterId, string fileName);
    Task InitializeAsync();
}
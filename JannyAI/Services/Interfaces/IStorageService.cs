namespace JannyAI.Services.Interfaces;

public interface IStorageService
{
    Task<bool> SaveCharacterImageAsync(string characterId, byte[] imageData, string fileName);
    Task<bool> CharacterExistsAsync(string characterId);
    Task<string> GetCharacterImageUrlAsync(string characterId);
    Task<List<string>> GetAllCharacterIdsAsync();
    Task SaveCharacterIndexAsync(string characterId);
    Task InitializeAsync();
}
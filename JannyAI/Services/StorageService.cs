using JannyAI.Configuration;
using JannyAI.Services.Interfaces;

namespace JannyAI.Services;

public class StorageService : IStorageService
{
    private readonly ILogger _logger;
    private readonly IGitHubService? _githubService;
    private readonly IFileService _fileService;
    private readonly HashSet<string> _characterIds;

    public StorageService(ILogger logger, IGitHubService? githubService, IFileService fileService)
    {
        _logger = logger;
        _githubService = githubService;
        _fileService = fileService;
        _characterIds = new HashSet<string>();
    }

    public async Task InitializeAsync()
    {
        if (ScrapingConfig.UseGitHubStorage && _githubService != null)
        {
            await _githubService.InitializeAsync();
            await _logger.LogInfoAsync("Инициализировано GitHub хранилище");
        }
        else
        {
            await _fileService.InitializeDirectoriesAsync();
            var existingIds = await _fileService.LoadDownloadedCharacterIdsAsync();
            foreach (var id in existingIds)
            {
                _characterIds.Add(id);
            }
            await _logger.LogInfoAsync("Инициализировано локальное хранилище");
        }
    }

    public async Task<bool> SaveCharacterImageAsync(string characterId, byte[] imageData, string fileName)
    {
        try
        {
            if (ScrapingConfig.UseGitHubStorage && _githubService != null)
            {
                // Сохраняем в GitHub
                var success = await _githubService.UploadFileAsync(characterId, imageData, fileName);
                if (success)
                {
                    await _logger.LogInfoAsync($"Персонаж {characterId} сохранен в GitHub");
                    return true;
                }
                return false;
            }
            else
            {
                // Сохраняем локально
                var tempFilePath = Path.Combine(ScrapingConfig.TemporaryDownloadFolderPath, fileName);
                await File.WriteAllBytesAsync(tempFilePath, imageData);
                
                var success = await _fileService.MoveDownloadedFileAsync(characterId, tempFilePath);
                if (success)
                {
                    await _logger.LogInfoAsync($"Персонаж {characterId} сохранен локально");
                    return true;
                }
                return false;
            }
        }
        catch (Exception ex)
        {
            await _logger.LogErrorAsync($"Ошибка сохранения персонажа {characterId}: {ex.Message}", ex);
            return false;
        }
    }

    public async Task<bool> CharacterExistsAsync(string characterId)
    {
        try
        {
            if (ScrapingConfig.UseGitHubStorage && _githubService != null)
            {
                // Проверяем в GitHub
                return await _githubService.FileExistsAsync(characterId, $"{characterId}.{ScrapingConfig.DefaultImageExtension}");
            }
            else
            {
                // Проверяем локально
                return _characterIds.Contains(characterId);
            }
        }
        catch (Exception ex)
        {
            await _logger.LogErrorAsync($"Ошибка проверки существования персонажа {characterId}: {ex.Message}", ex);
            return false;
        }
    }

    public async Task<string> GetCharacterImageUrlAsync(string characterId)
    {
        if (ScrapingConfig.UseGitHubStorage && _githubService != null)
        {
            return await _githubService.GetFileUrlAsync(characterId, $"{characterId}.{ScrapingConfig.DefaultImageExtension}");
        }
        else
        {
            var localPath = Path.Combine(ScrapingConfig.DownloadFolderPath, $"{characterId}.{ScrapingConfig.DefaultImageExtension}");
            return $"file://{localPath}";
        }
    }

    public async Task<List<string>> GetAllCharacterIdsAsync()
    {
        if (ScrapingConfig.UseGitHubStorage && _githubService != null)
        {
            // Для GitHub нужно будет реализовать получение списка файлов
            // Пока возвращаем пустой список
            return new List<string>();
        }
        else
        {
            return _characterIds.ToList();
        }
    }

    public async Task SaveCharacterIndexAsync(string characterId)
    {
        if (ScrapingConfig.UseGitHubStorage && _githubService != null)
        {
            // Для GitHub индекс не нужен, так как мы можем проверить существование файла напрямую
            await _logger.LogInfoAsync($"Персонаж {characterId} добавлен в GitHub индекс");
        }
        else
        {
            await _fileService.SaveDownloadedCharacterIdAsync(characterId);
            _characterIds.Add(characterId);
            await _logger.LogInfoAsync($"Персонаж {characterId} добавлен в локальный индекс");
        }
    }
}
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using JannyAI.Configuration;
using JannyAI.Services.Interfaces;

namespace JannyAI.Services;

public class GitHubService : IGitHubService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;
    private readonly string _token;
    private readonly string _owner;
    private readonly string _repository;
    private readonly string _branch;

    public GitHubService(ILogger logger)
    {
        _logger = logger;
        _httpClient = new HttpClient();
        
        // Получаем настройки из переменных окружения или конфигурации
        _token = Environment.GetEnvironmentVariable("GITHUB_TOKEN") ?? "";
        _owner = Environment.GetEnvironmentVariable("GITHUB_OWNER") ?? "";
        _repository = Environment.GetEnvironmentVariable("GITHUB_REPOSITORY") ?? "";
        _branch = Environment.GetEnvironmentVariable("GITHUB_BRANCH") ?? "main";
    }

    public async Task InitializeAsync()
    {
        if (string.IsNullOrEmpty(_token) || string.IsNullOrEmpty(_owner) || string.IsNullOrEmpty(_repository))
        {
            await _logger.LogErrorAsync("GitHub настройки не найдены. Установите переменные окружения: GITHUB_TOKEN, GITHUB_OWNER, GITHUB_REPOSITORY");
            return;
        }

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("token", _token);
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("JannyAI-Scraper", "1.0"));

        await _logger.LogInfoAsync($"GitHub сервис инициализирован для {_owner}/{_repository}");
    }

    public async Task<bool> UploadFileAsync(string characterId, byte[] fileContent, string fileName)
    {
        try
        {
            var filePath = $"characters/{characterId}/{fileName}";
            var base64Content = Convert.ToBase64String(fileContent);
            
            // Проверяем, существует ли файл
            var existingSha = await GetFileShaAsync(filePath);
            
            var requestBody = new
            {
                message = $"Добавлен персонаж {characterId}",
                content = base64Content,
                branch = _branch,
                sha = existingSha // Если файл существует, нужно передать его SHA
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var url = $"https://api.github.com/repos/{_owner}/{_repository}/contents/{filePath}";
            var response = await _httpClient.PutAsync(url, content);

            if (response.IsSuccessStatusCode)
            {
                await _logger.LogInfoAsync($"Файл {fileName} успешно загружен в GitHub для персонажа {characterId}");
                return true;
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                await _logger.LogErrorAsync($"Ошибка загрузки файла в GitHub: {response.StatusCode} - {error}");
                return false;
            }
        }
        catch (Exception ex)
        {
            await _logger.LogErrorAsync($"Исключение при загрузке файла в GitHub: {ex.Message}", ex);
            return false;
        }
    }

    public async Task<bool> FileExistsAsync(string characterId, string fileName)
    {
        try
        {
            var filePath = $"characters/{characterId}/{fileName}";
            var url = $"https://api.github.com/repos/{_owner}/{_repository}/contents/{filePath}";
            
            var response = await _httpClient.GetAsync(url);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            await _logger.LogErrorAsync($"Ошибка проверки существования файла в GitHub: {ex.Message}", ex);
            return false;
        }
    }

    public async Task<string> GetFileUrlAsync(string characterId, string fileName)
    {
        return $"https://raw.githubusercontent.com/{_owner}/{_repository}/{_branch}/characters/{characterId}/{fileName}";
    }

    private async Task<string?> GetFileShaAsync(string filePath)
    {
        try
        {
            var url = $"https://api.github.com/repos/{_owner}/{_repository}/contents/{filePath}";
            var response = await _httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var jsonResponse = await response.Content.ReadAsStringAsync();
                var fileInfo = JsonSerializer.Deserialize<JsonElement>(jsonResponse);
                return fileInfo.GetProperty("sha").GetString();
            }
            
            return null;
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
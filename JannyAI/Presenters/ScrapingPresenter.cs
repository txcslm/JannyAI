using JannyAI.Configuration;
using JannyAI.Models;
using JannyAI.Services.Interfaces;
using JannyAI.Views;

namespace JannyAI.Presenters;

public class ScrapingPresenter
{
    private readonly IConsoleView _view;
    private readonly IWebDriverService _webDriverService;
    private readonly IFileService _fileService;
    private readonly ILogger _logger;
    private readonly HashSet<string> _downloadedCharacterIds;

    public ScrapingPresenter(IConsoleView view, IWebDriverService webDriverService, IFileService fileService, ILogger logger)
    {
        _view = view;
        _webDriverService = webDriverService;
        _fileService = fileService;
        _logger = logger;
        _downloadedCharacterIds = new HashSet<string>();
    }

    public async Task StartScrapingAsync()
    {
        try
        {
            _view.ShowWelcomeMessage();
            
            await InitializeAsync();
            
            var lastPage = await _webDriverService.GetLastPageNumberAsync();
            var startingPage = _view.GetStartingPageFromUser(lastPage);
            
            await _logger.LogAsync($"Запуск парсера. Стартовая страница: {startingPage}, последняя страница: {lastPage}");
            
            await ProcessPagesAsync(startingPage);
        }
        catch (Exception ex)
        {
            _view.ShowError($"Критическая ошибка: {ex.Message}");
            await _logger.LogErrorAsync($"Критическая ошибка: {ex.Message}", ex);
        }
        finally
        {
            await CleanupAsync();
        }
    }

    private async Task InitializeAsync()
    {
        await _fileService.InitializeDirectoriesAsync();
        
        var downloadedIds = await _fileService.LoadDownloadedCharacterIdsAsync();
        foreach (var id in downloadedIds)
        {
            _downloadedCharacterIds.Add(id);
        }
        
        await _webDriverService.InitializeAsync();
        
        _view.ShowInitializationMessage(_downloadedCharacterIds.Count);
    }

    private async Task ProcessPagesAsync(int startingPage)
    {
        int currentPage = startingPage;
        int totalProcessedCharacters = 0;
        int consecutiveErrorCount = 0;

        while (true)
        {
            _view.ShowPageProcessingStart(currentPage);
            
            try
            {
                await _webDriverService.NavigateToPageAsync(currentPage);
                
                var characters = await _webDriverService.GetCharactersFromCurrentPageAsync();
                var processedCount = await ProcessCharactersOnPageAsync(characters);
                
                totalProcessedCharacters += processedCount;
                consecutiveErrorCount = 0;
                
                _view.ShowPageCompleted(currentPage, processedCount, totalProcessedCharacters);
                
                if (!await _webDriverService.NavigateToPreviousPageAsync(currentPage))
                {
                    _view.ShowParsingCompleted();
                    break;
                }
                
                currentPage--;
                
                // Progress report
                if ((startingPage - currentPage) % ScrapingConfig.ProgressReportInterval == 0)
                {
                    _view.ShowProgressReport(startingPage - currentPage, totalProcessedCharacters);
                }
                
                await Task.Delay(ScrapingConfig.PageLoadDelayMilliseconds);
            }
            catch (Exception ex)
            {
                consecutiveErrorCount++;
                _view.ShowError($"Ошибка на странице {currentPage} (попытка {consecutiveErrorCount}/{ScrapingConfig.MaxConsecutiveErrors}): {ex.Message}");
                await _logger.LogErrorAsync($"Ошибка на странице {currentPage}: {ex.Message}", ex);
                
                if (consecutiveErrorCount >= ScrapingConfig.MaxConsecutiveErrors)
                {
                    _view.ShowError($"Слишком много последовательных ошибок ({ScrapingConfig.MaxConsecutiveErrors}). Завершаем парсинг.");
                    break;
                }
                
                await Task.Delay(ScrapingConfig.ErrorRecoveryDelayMilliseconds);
                
                try
                {
                    await _webDriverService.NavigateToPageAsync(currentPage);
                }
                catch (Exception navEx)
                {
                    await _logger.LogErrorAsync($"Не удалось переинициализировать страницу: {navEx.Message}", navEx);
                }
            }
        }
        
        _view.ShowFinalStatistics(totalProcessedCharacters, _downloadedCharacterIds.Count, ScrapingConfig.DownloadFolderPath, ScrapingConfig.LogFilePath);
    }

    private async Task<int> ProcessCharactersOnPageAsync(List<Character> characters)
    {
        int processedCount = 0;
        
        for (int i = 0; i < characters.Count; i++)
        {
            var character = characters[i];
            
            if (_downloadedCharacterIds.Contains(character.Id))
            {
                _view.ShowCharacterSkipped(character.Id);
                continue;
            }
            
            _view.ShowCharacterProcessing(i + 1, characters.Count, character.Id);
            
            _fileService.ClearTemporaryDownloadFolder();
            var filesCountBefore = Directory.GetFiles(ScrapingConfig.TemporaryDownloadFolderPath).Length;
            
            var downloadResult = await _webDriverService.DownloadCharacterAsync(character);
            
            if (downloadResult.Success)
            {
                var downloadedFile = await _fileService.WaitForNewFileDownloadAsync(filesCountBefore, character.Id);
                
                if (downloadedFile != null)
                {
                    var moveSuccess = await _fileService.MoveDownloadedFileAsync(character.Id, downloadedFile);
                    
                    if (moveSuccess)
                    {
                        await _fileService.SaveDownloadedCharacterIdAsync(character.Id);
                        _downloadedCharacterIds.Add(character.Id);
                        _view.ShowCharacterSuccess(character.Id);
                        processedCount++;
                    }
                    else
                    {
                        _view.ShowCharacterFailure(character.Id, "Не удалось переместить файл");
                    }
                }
                else
                {
                    _view.ShowCharacterFailure(character.Id, "Файл не был скачан");
                }
            }
            else
            {
                _view.ShowCharacterFailure(character.Id, downloadResult.ErrorMessage ?? "Неизвестная ошибка");
            }
            
            await Task.Delay(ScrapingConfig.CharacterProcessDelayMilliseconds);
        }
        
        return processedCount;
    }

    private async Task CleanupAsync()
    {
        await _webDriverService.CleanupAsync();
        await _fileService.CleanupTemporaryFilesAsync();
        
        Console.WriteLine("\nПрограмма завершена.");
    }
}
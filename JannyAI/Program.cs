using JannyAI.Presenters;
using JannyAI.Services;
using JannyAI.Services.Interfaces;
using JannyAI.Views;
using JannyAI.Configuration;

namespace JannyAI;

internal static class Program
{
    static async Task Main(string[] args)
    {
        // Dependency injection setup
        ILogger logger = new LoggingService();
        IFileService fileService = new FileService(logger);
        IWebDriverService webDriverService = new WebDriverService(logger);
        IConsoleView consoleView = new ConsoleView();
        
        // GitHub service (optional)
        IGitHubService? githubService = ScrapingConfig.UseGitHubStorage 
            ? new GitHubService(logger) 
            : null;
        
        // Storage service (local or GitHub)
        IStorageService storageService = new StorageService(logger, githubService, fileService);
        
        // Create presenter with all dependencies
        var presenter = new ScrapingPresenter(consoleView, webDriverService, fileService, storageService, logger);
        
        // Show storage mode
        if (ScrapingConfig.UseGitHubStorage)
        {
            Console.WriteLine("🔗 Режим загрузки: GitHub Repository");
        }
        else
        {
            Console.WriteLine("💾 Режим загрузки: Локальное хранилище");
        }
        
        // Start scraping
        await presenter.StartScrapingAsync();
    }
}
using JannyAI.Presenters;
using JannyAI.Services;
using JannyAI.Services.Interfaces;
using JannyAI.Views;

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
        
        // Create presenter with all dependencies
        var presenter = new ScrapingPresenter(consoleView, webDriverService, fileService, logger);
        
        // Start scraping
        await presenter.StartScrapingAsync();
    }
}
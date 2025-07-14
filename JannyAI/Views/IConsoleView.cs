using JannyAI.Models;

namespace JannyAI.Views;

public interface IConsoleView
{
    void ShowWelcomeMessage();
    int GetStartingPageFromUser(int lastPage);
    void ShowInitializationMessage(int existingCharacters);
    void ShowPageProcessingStart(int pageNumber);
    void ShowCharacterProcessing(int currentIndex, int totalCount, string characterId);
    void ShowCharacterSuccess(string characterId);
    void ShowCharacterFailure(string characterId, string error);
    void ShowCharacterSkipped(string characterId);
    void ShowPageCompleted(int pageNumber, int processedCount, int totalCount);
    void ShowProgressReport(int pagesProcessed, int charactersDownloaded);
    void ShowError(string message);
    void ShowWarning(string message);
    void ShowFinalStatistics(int totalProcessed, int totalExisting, string downloadPath, string logPath);
    void ShowNavigationToPreviousPage(int pageNumber);
    void ShowParsingCompleted();
}
using JannyAI.Models;
using OpenQA.Selenium;

namespace JannyAI.Services.Interfaces;

public interface IWebDriverService
{
    Task InitializeAsync();
    Task<int> GetLastPageNumberAsync();
    Task NavigateToPageAsync(int pageNumber);
    Task<List<Character>> GetCharactersFromCurrentPageAsync();
    Task<DownloadResult> DownloadCharacterAsync(Character character);
    Task<bool> NavigateToPreviousPageAsync(int currentPage);
    Task WaitForPageLoadAsync();
    Task CleanupAsync();
    Task<byte[]> TakeScreenshotAsync();
}
using JannyAI.Configuration;
using JannyAI.Services.Interfaces;

namespace JannyAI.Services;

public class LoggingService : ILogger
{
    public async Task LogAsync(string message)
    {
        await WriteLogAsync(message, ConsoleColor.White);
    }

    public async Task LogErrorAsync(string message, Exception? exception = null)
    {
        var errorMessage = exception != null 
            ? $"ERROR: {message}\nStack Trace: {exception.StackTrace}" 
            : $"ERROR: {message}";
        
        await WriteLogAsync(errorMessage, ConsoleColor.Red);
    }

    public async Task LogWarningAsync(string message)
    {
        await WriteLogAsync($"WARNING: {message}", ConsoleColor.Yellow);
    }

    public async Task LogInfoAsync(string message)
    {
        await WriteLogAsync($"INFO: {message}", ConsoleColor.Green);
    }

    private async Task WriteLogAsync(string message, ConsoleColor color = ConsoleColor.White)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var logEntry = $"[{timestamp}] {message}";

        // Выводим в консоль с цветом
        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine($"🔍 {logEntry}");
        Console.ForegroundColor = originalColor;

        try
        {
            await File.AppendAllTextAsync(ScrapingConfig.LogFilePath, logEntry + Environment.NewLine);
        }
        catch
        {
            // Ignore logging errors
        }
    }
}
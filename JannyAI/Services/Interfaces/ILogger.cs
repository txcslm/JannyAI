namespace JannyAI.Services.Interfaces;

public interface ILogger
{
    Task LogAsync(string message);
    Task LogErrorAsync(string message, Exception? exception = null);
    Task LogWarningAsync(string message);
    Task LogInfoAsync(string message);
}
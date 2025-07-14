namespace JannyAI.Models;

public class DownloadResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? FilePath { get; set; }
    public long FileSize { get; set; }
    public int AttemptsUsed { get; set; }
}
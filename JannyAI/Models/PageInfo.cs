namespace JannyAI.Models;

public class PageInfo
{
    public int CurrentPage { get; set; }
    public int LastPage { get; set; }
    public string Url { get; set; } = string.Empty;
    public int CharactersFound { get; set; }
    public int CharactersProcessed { get; set; }
}
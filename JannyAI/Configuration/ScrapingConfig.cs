namespace JannyAI.Configuration;

public static class ScrapingConfig
{
    // URL и XPath селекторы
    public const string BaseUrl = "https://jannyai.com";
    public const string CharactersGridXPath = "/html/body/div/div[2]/div[3]";
    public const string CharacterLinkXPathPrefix = "/html/body/div/div[2]/div[3]/a[";
    public const string DownloadButtonXPath = "/html/body/div/div/div[1]/div[2]/div[2]/astro-island/div/button[1]";
    public const string DownloadButtonContainerXPath = "/html/body/div/div/div[1]/div[2]/div[2]/astro-island/div";
    
    // XPath для навигации по страницам
    public const string PaginationContainer1XPath = "/html/body/div/div[2]/div[2]/nav/ul";
    public const string PaginationContainer2XPath = "/html/body/div/div[2]/div[4]/nav/ul";
    public const string PrevButton1XPath = "/html/body/div/div[2]/div[2]/nav/ul/li[1]/a";
    public const string PrevButton2XPath = "/html/body/div/div[2]/div[4]/nav/ul/li[1]/a";
    
    // Временные задержки (оптимизированы для скорости)
    public const int PageLoadDelayMilliseconds = 400;
    public const int CharacterProcessDelayMilliseconds = 100;
    public const int RetryDelayMilliseconds = 150;
    public const int FileStabilityCheckDelayMilliseconds = 300;
    public const int DownloadTimeoutMilliseconds = 6000;
    public const int FileCheckIntervalMilliseconds = 200;
    public const int ErrorRecoveryDelayMilliseconds = 300;
    
    // Конфигурация парсинга
    public const int CharactersPerPage = 40;
    public const int MaxRetryAttempts = 3;
    public const int MaxConsecutiveErrors = 5;
    public const int ProgressReportInterval = 10;
    
    // Пути к папкам
    public static string DownloadFolderPath => Path.Combine(Environment.CurrentDirectory, "jannyai_characters");
    public static string TemporaryDownloadFolderPath => Path.Combine(Environment.CurrentDirectory, "temp_downloads");
    public static string ChromeProfileFolderPath => Path.Combine(Environment.CurrentDirectory, "ChromeProfile");
    public static string LogFilePath => Path.Combine(Environment.CurrentDirectory, "jannyai_parser.log");
    public static string CharacterIndexFilePath => Path.Combine(DownloadFolderPath, "downloaded_characters.txt");
    
    // GitHub настройки
    public static bool UseGitHubStorage => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_TOKEN"));
    public static string GitHubCharactersPath => "characters";
    public static string DefaultImageExtension => "png";
}
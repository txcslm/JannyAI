using System.Runtime.InteropServices;
using JannyAI.Configuration;
using JannyAI.Models;
using JannyAI.Services.Interfaces;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;

namespace JannyAI.Services;

public class WebDriverService : IWebDriverService
{
    private ChromeDriver? _driver;
    private int _debugPortNumber = 9222;
    private readonly ILogger _logger;

    public WebDriverService(ILogger logger)
    {
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        await _logger.LogInfoAsync("Начало инициализации WebDriverService");
        
        var chromeOptions = new ChromeOptions();
        chromeOptions.AddArgument($"--user-data-dir={ScrapingConfig.ChromeProfileFolderPath}");
        chromeOptions.AddArgument($"--remote-debugging-port={_debugPortNumber++}");
        chromeOptions.AddArgument("--remote-allow-origins=*");
        chromeOptions.AddUserProfilePreference("download.default_directory", ScrapingConfig.TemporaryDownloadFolderPath);
        chromeOptions.AddUserProfilePreference("download.prompt_for_download", false);
        chromeOptions.AddUserProfilePreference("download.directory_upgrade", true);
        chromeOptions.AddUserProfilePreference("safebrowsing.enabled", false);
        chromeOptions.AddUserProfilePreference("safebrowsing.disable_download_protection", true);
        chromeOptions.AddUserProfilePreference("profile.default_content_setting_values.automatic_downloads", 1);
        chromeOptions.AddUserProfilePreference("profile.content_settings.exceptions.automatic_downloads.*.setting", 1);
        chromeOptions.AddArgument("--no-sandbox");
        chromeOptions.AddArgument("--disable-dev-shm-usage");
        chromeOptions.AddArgument("--disable-gpu");
        chromeOptions.AddArgument("--disable-images");
        chromeOptions.AddArgument("--window-size=1280,720");
        chromeOptions.AddArgument("--disable-web-security");
        chromeOptions.AddArgument("--disable-features=VizDisplayCompositor");
        chromeOptions.AddArgument("--allow-running-insecure-content");
        chromeOptions.AddArgument("--disable-blink-features=AutomationControlled");

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            chromeOptions.AddArgument("--wm-window-animations-disabled");
            chromeOptions.AddArgument("--animation-duration-scale=0");
            chromeOptions.AddArgument("--disable-features=RendererCodeIntegrity");
            chromeOptions.AddArgument("--start-minimized");
        }

        _driver = new ChromeDriver(chromeOptions);
        await _logger.LogInfoAsync("WebDriver успешно инициализирован");
    }

    public async Task<int> GetLastPageNumberAsync()
    {
        if (_driver == null) return 21692;
        
        try
        {
            await _logger.LogInfoAsync("Получение номера последней страницы");
            _driver.Navigate().GoToUrl(ScrapingConfig.BaseUrl);
            await WaitForPageLoadAsync();
            
            var paginationElements = _driver.FindElements(By.XPath("//nav//a[contains(@href, '/?page=')]"));
            
            int maxPage = 1;
            foreach (var element in paginationElements)
            {
                try
                {
                    var text = element.Text.Trim();
                    if (int.TryParse(text, out int pageNum) && pageNum > maxPage)
                    {
                        maxPage = pageNum;
                    }
                }
                catch { }
            }
            
            if (maxPage == 1)
            {
                foreach (var element in paginationElements)
                {
                    var href = element.GetAttribute("href");
                    if (!string.IsNullOrEmpty(href))
                    {
                        var match = System.Text.RegularExpressions.Regex.Match(href, @"page=(\d+)");
                        if (match.Success && int.TryParse(match.Groups[1].Value, out int pageNum))
                        {
                            if (pageNum > maxPage)
                                maxPage = pageNum;
                        }
                    }
                }
            }
            
            if (maxPage == 1)
            {
                await _logger.LogWarningAsync("Не удалось определить последнюю страницу, используем значение по умолчанию");
                return 21692;
            }
            
            await _logger.LogInfoAsync($"Последняя страница определена: {maxPage}");
            return maxPage;
        }
        catch (Exception ex)
        {
            await _logger.LogErrorAsync($"Ошибка при определении последней страницы: {ex.Message}", ex);
            return 21692;
        }
    }

    public async Task NavigateToPageAsync(int pageNumber)
    {
        if (_driver == null) return;
        
        var pageUrl = $"{ScrapingConfig.BaseUrl}/?tag_id=&page={pageNumber}";
        _driver.Navigate().GoToUrl(pageUrl);
        await WaitForPageLoadAsync();
        await _logger.LogInfoAsync($"Переход на страницу {pageNumber}");
    }

    public async Task<List<Character>> GetCharactersFromCurrentPageAsync()
    {
        if (_driver == null) return new List<Character>();
        
        var characters = new List<Character>();
        var webDriverWait = new WebDriverWait(_driver, TimeSpan.FromSeconds(2));
        
        try
        {
            webDriverWait.Until(driver => driver.FindElement(By.XPath(ScrapingConfig.CharactersGridXPath)));
            await _logger.LogInfoAsync("Сетка персонажей загружена");
        }
        catch (WebDriverTimeoutException)
        {
            await _logger.LogWarningAsync("Сетка персонажей не найдена");
            return characters;
        }
        
        for (int i = 1; i <= ScrapingConfig.CharactersPerPage; i++)
        {
            try
            {
                var characterXPath = $"{ScrapingConfig.CharacterLinkXPathPrefix}{i}]";
                var characterElement = _driver.FindElement(By.XPath(characterXPath));
                var characterUrl = characterElement.GetAttribute("href");
                
                if (string.IsNullOrEmpty(characterUrl)) continue;
                
                var characterId = ExtractCharacterIdFromUrl(characterUrl);
                if (string.IsNullOrEmpty(characterId)) continue;
                
                characters.Add(new Character
                {
                    Id = characterId,
                    Url = characterUrl
                });
            }
            catch (NoSuchElementException)
            {
                await _logger.LogInfoAsync($"Персонаж {i} не найден (на странице может быть меньше персонажей)");
                break;
            }
        }
        
        await _logger.LogInfoAsync($"Найдено {characters.Count} персонажей на текущей странице");
        return characters;
    }

    public async Task<DownloadResult> DownloadCharacterAsync(Character character)
    {
        if (_driver == null) return new DownloadResult { Success = false, ErrorMessage = "WebDriver не инициализирован" };
        
        var result = new DownloadResult();
        
        for (int attempt = 1; attempt <= ScrapingConfig.MaxRetryAttempts; attempt++)
        {
            try
            {
                await _logger.LogInfoAsync($"Попытка скачивания {attempt}/{ScrapingConfig.MaxRetryAttempts} для персонажа {character.Id}");
                
                _driver.Navigate().GoToUrl(character.Url);
                await WaitForPageLoadAsync();
                await Task.Delay(100);
                
                var downloadButton = await FindDownloadButtonAsync();
                if (downloadButton == null)
                {
                    result.ErrorMessage = $"Кнопка скачивания не найдена для персонажа {character.Id}";
                    if (attempt == ScrapingConfig.MaxRetryAttempts)
                    {
                        await _logger.LogErrorAsync(result.ErrorMessage);
                        return result;
                    }
                    await Task.Delay(ScrapingConfig.RetryDelayMilliseconds);
                    continue;
                }
                
                ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", downloadButton);
                await _logger.LogInfoAsync($"Кнопка скачивания нажата для персонажа {character.Id}");
                
                result.Success = true;
                result.AttemptsUsed = attempt;
                return result;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"Ошибка при скачивании персонажа {character.Id}: {ex.Message}";
                await _logger.LogErrorAsync(result.ErrorMessage, ex);
                
                if (attempt == ScrapingConfig.MaxRetryAttempts) break;
                await Task.Delay(ScrapingConfig.RetryDelayMilliseconds);
            }
        }
        
        return result;
    }

    public async Task<bool> NavigateToPreviousPageAsync(int currentPage)
    {
        if (_driver == null || currentPage <= 1) return false;
        
        try
        {
            await _logger.LogInfoAsync($"Попытка перейти на предыдущую страницу с {currentPage}");
            
            // 1. Самый простой способ - переход по URL
            try
            {
                var previousPageNumber = currentPage - 1;
                var previousPageUrl = $"{ScrapingConfig.BaseUrl}/?tag_id=&page={previousPageNumber}";
                
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"🔄 Переходим на предыдущую страницу через URL: {previousPageNumber}");
                Console.ResetColor();
                await _logger.LogInfoAsync($"Переход на предыдущую страницу через URL: {previousPageUrl}");
                
                _driver.Navigate().GoToUrl(previousPageUrl);
                await WaitForPageLoadAsync();
                
                // Проверяем, что переход прошел успешно
                var currentUrl = _driver.Url;
                if (currentUrl.Contains($"page={previousPageNumber}"))
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"✅ Успешный переход на страницу {previousPageNumber}");
                    Console.ResetColor();
                    await _logger.LogInfoAsync($"Успешный переход на страницу {previousPageNumber}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                await _logger.LogWarningAsync($"Не удалось перейти через URL: {ex.Message}");
            }
            
            // 2. Пробуем найти кнопку "Prev" в первом возможном месте
            try
            {
                var prevButton = _driver.FindElement(By.XPath("/html/body/div/div[2]/div[2]/nav/ul/li[1]/a[contains(text(), 'Prev')]"));
                var href = prevButton.GetAttribute("href");
                if (!string.IsNullOrEmpty(href) && !href.Contains("javascript:void") && !href.Contains("#"))
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("🔙 Найдена кнопка Prev в /div[2]/div[2]/nav/ul");
                    Console.ResetColor();
                    await _logger.LogInfoAsync("Найдена кнопка Prev в первом месте");
                    prevButton.Click();
                    await WaitForPageLoadAsync();
                    return true;
                }
            }
            catch { }
            
            // 3. Пробуем найти кнопку "Prev" во втором возможном месте
            try
            {
                var prevButton = _driver.FindElement(By.XPath("/html/body/div/div[2]/div[4]/nav/ul/li[1]/a[contains(text(), 'Prev')]"));
                var href = prevButton.GetAttribute("href");
                if (!string.IsNullOrEmpty(href) && !href.Contains("javascript:void") && !href.Contains("#"))
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("🔙 Найдена кнопка Prev в /div[2]/div[4]/nav/ul");
                    Console.ResetColor();
                    await _logger.LogInfoAsync("Найдена кнопка Prev во втором месте");
                    prevButton.Click();
                    await WaitForPageLoadAsync();
                    return true;
                }
            }
            catch { }
            
            // 4. Поиск кнопки "Prev" в любом nav контейнере
            try
            {
                var prevButton = _driver.FindElement(By.XPath("//nav//ul//li[1]//a[contains(text(), 'Prev')]"));
                var href = prevButton.GetAttribute("href");
                if (!string.IsNullOrEmpty(href) && !href.Contains("javascript:void") && !href.Contains("#"))
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("🔙 Найдена кнопка Prev в любом nav контейнере");
                    Console.ResetColor();
                    await _logger.LogInfoAsync("Найдена кнопка Prev в nav контейнере");
                    prevButton.Click();
                    await WaitForPageLoadAsync();
                    return true;
                }
            }
            catch { }
            
            // 5. Поиск кнопки "Prev" с классом rounded-l-lg
            try
            {
                var prevButton = _driver.FindElement(By.XPath("//a[contains(@class, 'rounded-l-lg') and contains(text(), 'Prev')]"));
                var href = prevButton.GetAttribute("href");
                if (!string.IsNullOrEmpty(href) && !href.Contains("javascript:void") && !href.Contains("#"))
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("🔙 Найдена кнопка Prev по классу rounded-l-lg");
                    Console.ResetColor();
                    await _logger.LogInfoAsync("Найдена кнопка Prev по классу rounded-l-lg");
                    prevButton.Click();
                    await WaitForPageLoadAsync();
                    return true;
                }
            }
            catch { }
            
            // 6. Поиск ссылки на предыдущую страницу по номеру
            try
            {
                var targetPage = currentPage - 1;
                var pageLinks = _driver.FindElements(By.XPath("//a[contains(@href, '/?page=')]"));
                
                foreach (var link in pageLinks)
                {
                    var href = link.GetAttribute("href");
                    if (!string.IsNullOrEmpty(href) && href.Contains($"page={targetPage}"))
                    {
                        // Проверяем, что это не ссылка на текущую страницу (которая может быть выделена)
                        var parentElement = link.FindElement(By.XPath(".."));
                        if (!parentElement.GetAttribute("class").Contains("text-blue-600"))
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine($"🔙 Найдена ссылка на страницу {targetPage}");
                            Console.ResetColor();
                            await _logger.LogInfoAsync($"Найдена ссылка на страницу {targetPage}");
                            link.Click();
                            await WaitForPageLoadAsync();
                            return true;
                        }
                    }
                }
            }
            catch { }
            
            // 7. Общий поиск любой кнопки "Prev" на странице
            try
            {
                var prevButtons = _driver.FindElements(By.XPath("//a[contains(text(), 'Prev') or contains(text(), 'Previous')]"));
                foreach (var button in prevButtons)
                {
                    var href = button.GetAttribute("href");
                    if (!string.IsNullOrEmpty(href) && !href.Contains("javascript:void") && !href.Contains("#"))
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("🔙 Найдена кнопка Prev общим поиском");
                        Console.ResetColor();
                        await _logger.LogInfoAsync("Найдена кнопка Prev общим поиском");
                        button.Click();
                        await WaitForPageLoadAsync();
                        return true;
                    }
                }
            }
            catch { }
            
            // 8. Поиск по стрелке ← или ‹
            try
            {
                var arrowButton = _driver.FindElement(By.XPath("//a[contains(text(), '←') or contains(text(), '‹')]"));
                var href = arrowButton.GetAttribute("href");
                if (!string.IsNullOrEmpty(href) && !href.Contains("javascript:void") && !href.Contains("#"))
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("🔙 Найдена кнопка стрелка");
                    Console.ResetColor();
                    await _logger.LogInfoAsync("Найдена кнопка стрелка");
                    arrowButton.Click();
                    await WaitForPageLoadAsync();
                    return true;
                }
            }
            catch { }
            
            await _logger.LogWarningAsync("Не удалось найти способ перейти на предыдущую страницу");
            return false;
        }
        catch (Exception ex)
        {
            await _logger.LogErrorAsync($"Ошибка при переходе на предыдущую страницу: {ex.Message}", ex);
            return false;
        }
    }

    public async Task WaitForPageLoadAsync()
    {
        await Task.Delay(ScrapingConfig.PageLoadDelayMilliseconds);
        
        if (_driver == null) return;
        
        var webDriverWait = new WebDriverWait(_driver, TimeSpan.FromSeconds(5));
        try
        {
            webDriverWait.Until(driver => ((IJavaScriptExecutor)driver).ExecuteScript("return document.readyState").Equals("complete"));
        }
        catch
        {
            // Ignore timeouts
        }
    }

    public async Task<byte[]> TakeScreenshotAsync()
    {
        if (_driver == null) return Array.Empty<byte>();
        
        try
        {
            var screenshot = ((ITakesScreenshot)_driver).GetScreenshot();
            return screenshot.AsByteArray;
        }
        catch (Exception ex)
        {
            await _logger.LogErrorAsync($"Не удалось сделать скриншот: {ex.Message}", ex);
            return Array.Empty<byte>();
        }
    }

    public async Task CleanupAsync()
    {
        try
        {
            _driver?.Quit();
            _driver?.Dispose();
            await _logger.LogInfoAsync("WebDriver успешно освобожден");
        }
        catch (Exception ex)
        {
            await _logger.LogErrorAsync($"Ошибка при освобождении WebDriver: {ex.Message}", ex);
        }
    }

    private async Task<IWebElement?> FindDownloadButtonAsync()
    {
        if (_driver == null) return null;
        
        var webDriverWait = new WebDriverWait(_driver, TimeSpan.FromSeconds(7));
        
        try
        {
            return webDriverWait.Until(driver =>
            {
                // 1. Пробуем найти по точному XPath (основной контейнер)
                try
                {
                    var btn = driver.FindElement(By.XPath(ScrapingConfig.DownloadButtonXPath));
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("🎯 Найдена кнопка скачивания по точному XPath");
                    Console.ResetColor();
                    return btn;
                }
                catch { }
                
                // 2. Пробуем найти кнопку в альтернативном контейнере div с классом mt-4
                try
                {
                    var btn = driver.FindElement(By.XPath("/html/body/div/div/div[1]/div[2]/div[2]/astro-island/div/div[contains(@class, 'mt-4')]//button[contains(text(), 'Download')]"));
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("🎯 Найдена кнопка скачивания в контейнере mt-4");
                    Console.ResetColor();
                    return btn;
                }
                catch { }
                
                // 3. Поиск по градиентному фону кнопки в любом месте контейнера
                try
                {
                    var btn = driver.FindElement(By.XPath($"{ScrapingConfig.DownloadButtonContainerXPath}//button[contains(@class, 'bg-gradient-to-br from-purple-600 to-blue-500')]"));
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("🎯 Найдена кнопка скачивания по градиентному классу в контейнере");
                    Console.ResetColor();
                    return btn;
                }
                catch { }
                
                // 4. Поиск кнопки с текстом "Download" в основном контейнере
                try
                {
                    var btn = driver.FindElement(By.XPath($"{ScrapingConfig.DownloadButtonContainerXPath}//button[contains(text(), 'Download')]"));
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("🎯 Найдена кнопка скачивания по тексту в контейнере");
                    Console.ResetColor();
                    return btn;
                }
                catch { }
                
                // 5. Поиск по SVG иконке с path загрузки в контейнере
                try
                {
                    var btn = driver.FindElement(By.XPath($"{ScrapingConfig.DownloadButtonContainerXPath}//button[.//svg//path[contains(@d, 'M3 16.5v2.25')]]"));
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("🎯 Найдена кнопка скачивания по SVG пути в контейнере");
                    Console.ResetColor();
                    return btn;
                }
                catch { }
                
                // 6. Поиск кнопки со специфичными CSS классами
                try
                {
                    var btn = driver.FindElement(By.XPath($"{ScrapingConfig.DownloadButtonContainerXPath}//button[contains(@class, 'inline-flex') and contains(@class, 'items-center') and contains(@class, 'rounded-lg') and contains(text(), 'Download')]"));
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("🎯 Найдена кнопка скачивания по специфичным CSS классам");
                    Console.ResetColor();
                    return btn;
                }
                catch { }
                
                // 7. Поиск в любом div с классом flex внутри контейнера
                try
                {
                    var btn = driver.FindElement(By.XPath($"{ScrapingConfig.DownloadButtonContainerXPath}//div[contains(@class, 'flex')]//button[contains(text(), 'Download')]"));
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("🎯 Найдена кнопка скачивания в flex div");
                    Console.ResetColor();
                    return btn;
                }
                catch { }
                
                // 8. Более общий поиск по тексту "Download" на всей странице
                try
                {
                    var btn = driver.FindElement(By.XPath("//button[contains(text(), 'Download')]"));
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("🎯 Найдена кнопка скачивания по тексту на всей странице");
                    Console.ResetColor();
                    return btn;
                }
                catch { }
                
                // 9. Поиск по SVG иконке загрузки на всей странице
                try
                {
                    var btn = driver.FindElement(By.XPath("//button[.//svg//path[contains(@d, 'M3 16.5v2.25')]]"));
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("🎯 Найдена кнопка скачивания по SVG пути на всей странице");
                    Console.ResetColor();
                    return btn;
                }
                catch { }
                
                // 10. Поиск по CSS селектору с градиентом на всей странице
                try
                {
                    var btn = driver.FindElement(By.XPath("//button[contains(@class, 'bg-gradient-to-br from-purple-600 to-blue-500')]"));
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("🎯 Найдена кнопка скачивания по градиентному классу на всей странице");
                    Console.ResetColor();
                    return btn;
                }
                catch { }
                
                throw new NoSuchElementException("Кнопка скачивания не найдена");
            });
        }
        catch (WebDriverTimeoutException)
        {
            await _logger.LogWarningAsync("Кнопка скачивания не найдена после таймаута");
            return null;
        }
    }

    private string ExtractCharacterIdFromUrl(string characterUrl)
    {
        try
        {
            var parsedUri = new Uri(characterUrl);
            var urlPath = parsedUri.AbsolutePath.TrimEnd('/');
            var lastSlashIndex = urlPath.LastIndexOf('/');
            
            if (lastSlashIndex >= 0 && lastSlashIndex < urlPath.Length - 1)
            {
                var lastUrlSegment = urlPath.Substring(lastSlashIndex + 1);
                var urlSegmentParts = lastUrlSegment.Split('_');
                
                if (urlSegmentParts.Length > 0)
                {
                    var guidPart = urlSegmentParts[0];
                    if (Guid.TryParse(guidPart, out _))
                    {
                        return guidPart;
                    }
                }
                
                return urlSegmentParts.Length > 0 ? urlSegmentParts[0] : lastUrlSegment;
            }
            
            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogErrorAsync($"Ошибка извлечения ID персонажа из URL {characterUrl}: {ex.Message}", ex);
            return string.Empty;
        }
    }
}
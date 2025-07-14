using System.Runtime.InteropServices;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using Directory = System.IO.Directory;
using File = System.IO.File;

namespace JannyAI;

internal static class Program
{
  // Конфигурация URL и XPath селекторов
  private const string BaseUrl = "https://jannyai.com";
  private const string CharactersGridXPath = "/html/body/div/div[2]/div[3]";
  private const string CharacterLinkXPathPrefix = "/html/body/div/div[2]/div[3]/a[";
  private const string JsonDownloadButtonXPath = "/html/body/div/div/div[1]/div[2]/div[2]/astro-island/div/button[1]";
  private const string PreviousPageButtonXPath = "//a[contains(@class, 'hover:bg-gray-100') and contains(@href, 'page=')]";

  // Конфигурация временных задержек (в миллисекундах)
  private const int PageLoadDelayMilliseconds = 800;
  private const int CharacterProcessDelayMilliseconds = 200; 
  private const int RetryDelayMilliseconds = 150;
  private const int FileStabilityCheckDelayMilliseconds = 500;
  private const int DownloadTimeoutMilliseconds = 800;
  private const int FileCheckIntervalMilliseconds = 300;
  private const int ErrorRecoveryDelayMilliseconds = 300;

  // Конфигурация парсинга
  private const int CharactersPerPage = 40; // Полная страница
  private const int MaxRetryAttempts = 3; // Нормальное количество попыток
  private const int MaxConsecutiveErrors = 5;
  private const int ProgressReportInterval = 10;

  // Пути к папкам и файлам
  private static readonly string DownloadFolderPath = Path.Combine(Environment.CurrentDirectory, "jannyai_characters");
  private static readonly string TemporaryDownloadFolderPath = Path.Combine(Environment.CurrentDirectory, "temp_downloads");
  private static readonly string ChromeProfileFolderPath = Path.Combine(Environment.CurrentDirectory, "ChromeProfile");
  private static readonly string LogFilePath = Path.Combine(Environment.CurrentDirectory, "jannyai_parser.log");

  private static ChromeDriver? webDriver;
  private static readonly HashSet<string> downloadedCharacterIds = new();
  private static int startingPageNumber = 1;
  private static int debugPortNumber = 9222;

  static async Task Main(string[] args)
  {
    Console.WriteLine("=== JannyAI Character PNG Downloader ===");

    try
    {
      await InitializeParserAsync();
      
      // Получаем последнюю страницу автоматически
      var lastPageNumber = await GetLastPageNumberAsync();
      
      // Спрашиваем пользователя о начальной странице
      startingPageNumber = GetStartingPageNumberFromUser(lastPageNumber);

      await LogMessageAsync($"Запуск парсера. Стартовая страница: {startingPageNumber}, последняя страница: {lastPageNumber}");
      Console.WriteLine($"Начинаем парсинг со страницы: {startingPageNumber}");

      await StartCharacterParsingAsync();
    }
    catch (Exception exception)
    {
      var errorMessage = $"Критическая ошибка: {exception.Message}\nStack trace: {exception.StackTrace}";
      Console.WriteLine($"❌ {errorMessage}");
      await LogMessageAsync($"CRITICAL ERROR: {errorMessage}");
    }
    finally
    {
      await CleanupResourcesAsync();
    }

    Console.WriteLine("\nПрограмма завершена.");
  }

private static async Task<int> GetLastPageNumberAsync()
{
    if (webDriver == null) return 21692;
    
    try
    {
        Console.WriteLine("🔍 Определяем последнюю страницу...");
        
        // Переходим на главную страницу
        webDriver.Navigate().GoToUrl(BaseUrl);
        await WaitForPageLoadAsync();
        
        // Более специфичный поиск пагинации
        var paginationElements = webDriver.FindElements(By.XPath("//nav//a[contains(@href, '/?page=')]"));
        
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
        
        // Если не нашли через текст, пробуем через href
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
        
        // Если все еще 1, возвращаем известное значение
        if (maxPage == 1)
        {
            Console.WriteLine("⚠️ Не удалось определить последнюю страницу, используем значение по умолчанию");
            return 21692;
        }
        
        Console.WriteLine($"✅ Найдена последняя страница: {maxPage}");
        await LogMessageAsync($"Last page detected: {maxPage}");
        return maxPage;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠️ Не удалось определить последнюю страницу: {ex.Message}");
        await LogMessageAsync($"Failed to detect last page: {ex.Message}");
        return 21692;
    }
}


  private static int GetStartingPageNumberFromUser(int lastPage)
  {
    while (true)
    {
      Console.WriteLine($"\n📄 Последняя страница сайта: {lastPage}");
      Console.WriteLine("\nВыберите режим парсинга:");
      Console.WriteLine("1. Начать с конца (самые старые персонажи)");
      Console.WriteLine("2. Указать номер страницы");
      Console.Write("\nВаш выбор (1 или 2): ");

      var choice = Console.ReadLine()?.Trim();

      if (choice == "1")
      {
        Console.WriteLine($"✅ Начинаем с последней страницы {lastPage}");
        return lastPage;
      }
      else if (choice == "2")
      {
        Console.Write($"\nВведите номер страницы (1-{lastPage}): ");
        var userInput = Console.ReadLine()?.Trim();

        if (!string.IsNullOrEmpty(userInput) && int.TryParse(userInput, out int pageNumber) && pageNumber >= 1 && pageNumber <= lastPage)
        {
          Console.WriteLine($"✅ Начинаем со страницы {pageNumber}");
          return pageNumber;
        }
        else
        {
          Console.WriteLine($"❌ Неверный номер страницы! Введите число от 1 до {lastPage}.");
        }
      }
      else
      {
        Console.WriteLine("❌ Пожалуйста, введите 1 или 2.");
      }
    }
  }

  private static async Task LogMessageAsync(string message)
  {
    var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    var logEntry = $"[{timestamp}] {message}";

    try
    {
      await File.AppendAllTextAsync(LogFilePath, logEntry + Environment.NewLine);
    }
    catch
    {
      // Игнорируем ошибки логирования
    }
  }

  private static async Task InitializeParserAsync()
  {
    Console.WriteLine("🔧 Инициализация...");
    await LogMessageAsync("Initialization started");

    // Создаем директории
    Directory.CreateDirectory(DownloadFolderPath);
    Directory.CreateDirectory(TemporaryDownloadFolderPath);
    Directory.CreateDirectory(ChromeProfileFolderPath);
    await LogMessageAsync($"Directories created - Download: {DownloadFolderPath}, Temp: {TemporaryDownloadFolderPath}, Profile: {ChromeProfileFolderPath}");

    await LoadDownloadedCharacterIndexAsync();
    await LogMessageAsync($"Character index loaded, found {downloadedCharacterIds.Count} existing characters");

    webDriver = CreateWebDriverInstance();
    await LogMessageAsync("WebDriver initialized with Chrome profile");

    Console.WriteLine($"✅ Инициализация завершена. Найдено уже скачанных персонажей: {downloadedCharacterIds.Count}");
    Console.WriteLine($"🔐 Chrome профиль сохранен в: {ChromeProfileFolderPath}");
    await LogMessageAsync("Initialization completed successfully");
  }

  private static ChromeDriver CreateWebDriverInstance()
  {
    var chromeOptions = new ChromeOptions();

    chromeOptions.AddArgument($"--user-data-dir={ChromeProfileFolderPath}");
    chromeOptions.AddArgument($"--remote-debugging-port={debugPortNumber++}");
    chromeOptions.AddArgument("--remote-allow-origins=*");

    chromeOptions.AddUserProfilePreference("download.default_directory", TemporaryDownloadFolderPath);
    chromeOptions.AddUserProfilePreference("download.prompt_for_download", false);
    chromeOptions.AddUserProfilePreference("download.directory_upgrade", true);
    chromeOptions.AddUserProfilePreference("safebrowsing.enabled", false);

    chromeOptions.AddArgument("--no-sandbox");
    chromeOptions.AddArgument("--disable-dev-shm-usage");
    chromeOptions.AddArgument("--disable-gpu");
    chromeOptions.AddArgument("--window-size=1280,720");

    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
    {
      chromeOptions.AddArgument("--wm-window-animations-disabled");
      chromeOptions.AddArgument("--animation-duration-scale=0");
      chromeOptions.AddArgument("--disable-features=RendererCodeIntegrity");
      chromeOptions.AddArgument("--start-minimized");
    }

    return new ChromeDriver(chromeOptions);
  }

  private static async Task LoadDownloadedCharacterIndexAsync()
  {
    var characterIndexFilePath = Path.Combine(DownloadFolderPath, "downloaded_characters.txt");
    if (File.Exists(characterIndexFilePath))
    {
      var characterIdLines = await File.ReadAllLinesAsync(characterIndexFilePath);
      foreach (var characterIdLine in characterIdLines)
      {
        if (!string.IsNullOrWhiteSpace(characterIdLine))
        {
          downloadedCharacterIds.Add(characterIdLine.Trim());
        }
      }
    }
  }

  private static async Task SaveDownloadedCharacterIdAsync(string characterId)
  {
    downloadedCharacterIds.Add(characterId);
    var characterIndexFilePath = Path.Combine(DownloadFolderPath, "downloaded_characters.txt");
    await File.AppendAllTextAsync(characterIndexFilePath, characterId + Environment.NewLine);
  }

  private static async Task StartCharacterParsingAsync()
  {
    Console.WriteLine("🚀 Начинаем парсинг...");
    await LogMessageAsync("Начало парсинга");

    if (webDriver == null)
    {
      throw new InvalidOperationException("WebDriver не инициализирован");
    }

    var startingPageUrl = $"{BaseUrl}/?tag_id=&page={startingPageNumber}";
    webDriver.Navigate().GoToUrl(startingPageUrl);
    await WaitForPageLoadAsync();
    await LogMessageAsync($"Переход на стартовую страницу: {startingPageUrl}");

    Console.WriteLine("✅ Начинаем автоматический парсинг!");
    await LogMessageAsync("Starting parsing");

    int currentPageNumber = startingPageNumber;
    int totalProcessedCharacters = 0;
    int consecutiveErrorCount = 0;

    Console.WriteLine($"📊 Начинаем с страницы {currentPageNumber}. Уже скачано персонажей: {downloadedCharacterIds.Count}");
    await LogMessageAsync($"Стартовая статистика - страница: {currentPageNumber}, уже скачано: {downloadedCharacterIds.Count}");

    while (true)
    {
      Console.WriteLine($"\n📄 === Страница {currentPageNumber} ===");
      await LogMessageAsync($"Начало обработки страницы {currentPageNumber}");

      try
      {
        var processedCharactersOnPage = await ProcessCharactersOnCurrentPageAsync(currentPageNumber);
        totalProcessedCharacters += processedCharactersOnPage;
        consecutiveErrorCount = 0;

        Console.WriteLine($"✅ Страница {currentPageNumber} завершена. Обработано: {processedCharactersOnPage}, всего: {totalProcessedCharacters}");
        await LogMessageAsync($"Страница {currentPageNumber} завершена - обработано: {processedCharactersOnPage}, всего: {totalProcessedCharacters}");

        if (!await NavigateToPreviousPageAsync(currentPageNumber))
        {
          Console.WriteLine("🏁 Достигнута первая страница или кнопка 'Prev' недоступна");
          await LogMessageAsync("Парсинг завершен - достигнута первая страница");
          break;
        }

        currentPageNumber--;

        if ((startingPageNumber - currentPageNumber) % ProgressReportInterval == 0)
        {
          var progressMessage = $"Прогресс: обработано {startingPageNumber - currentPageNumber} страниц, скачано {totalProcessedCharacters} новых персонажей";
          Console.WriteLine($"📈 {progressMessage}");
          await LogMessageAsync(progressMessage);
        }

        await Task.Delay(PageLoadDelayMilliseconds);
      }
      catch (Exception exception)
      {
        consecutiveErrorCount++;
        var errorMessage = $"Ошибка на странице {currentPageNumber} (попытка {consecutiveErrorCount}/{MaxConsecutiveErrors}): {exception.Message}";
        Console.WriteLine($"❌ {errorMessage}");
        await LogMessageAsync($"ERROR: {errorMessage}\nStack Trace: {exception.StackTrace}");

        if (consecutiveErrorCount >= MaxConsecutiveErrors)
        {
          var fatalMessage = $"Слишком много последовательных ошибок ({MaxConsecutiveErrors}). Завершаем парсинг.";
          Console.WriteLine($"💀 {fatalMessage}");
          await LogMessageAsync($"FATAL: {fatalMessage}");
          break;
        }

        var errorDelayMilliseconds = Math.Min(consecutiveErrorCount * ErrorRecoveryDelayMilliseconds, 30000);
                Console.WriteLine($"⏳ Ждем {errorDelayMilliseconds / 1000} секунд перед повтором...");
        await LogMessageAsync($"Ожидание {errorDelayMilliseconds}ms перед повтором из-за ошибки");
        await Task.Delay(errorDelayMilliseconds);

        try
        {
          var currentPageUrl = $"{BaseUrl}/?tag_id=&page={currentPageNumber}";
          webDriver.Navigate().GoToUrl(currentPageUrl);
          await WaitForPageLoadAsync();
          await LogMessageAsync($"Переинициализация страницы: {currentPageUrl}");
        }
        catch (Exception navigationException)
        {
          var navigationErrorMessage = $"Не удалось переинициализировать страницу: {navigationException.Message}";
          Console.WriteLine($"❌ {navigationErrorMessage}");
          await LogMessageAsync($"NAVIGATION ERROR: {navigationErrorMessage}");
        }
      }
    }

    // Финальная статистика
    var finalMessage = $"ПАРСИНГ ЗАВЕРШЕН - Обработано новых персонажей: {totalProcessedCharacters}, Общее количество: {downloadedCharacterIds.Count}";
    Console.WriteLine($"\n🎉 === {finalMessage} ===");
    Console.WriteLine($"💾 Файлы сохранены в папке: {DownloadFolderPath}");
    Console.WriteLine($"📄 Логи сохранены в файле: {LogFilePath}");
    await LogMessageAsync(finalMessage);
  }

  private static async Task<int> ProcessCharactersOnCurrentPageAsync(int pageNumber)
  {
    if (webDriver == null) return 0;

    var processedCharacterCount = 0;

    // Ждем загрузки грида с персонажами
    var webDriverWait = new WebDriverWait(webDriver, TimeSpan.FromSeconds(2));

    try
    {
      webDriverWait.Until(driver => driver.FindElement(By.XPath(CharactersGridXPath)));
      await LogMessageAsync($"Грид персонажей загружен на странице {pageNumber}");
    }
    catch (WebDriverTimeoutException exception)
    {
      var errorMessage = $"Грид персонажей не найден на странице {pageNumber}";
      Console.WriteLine($"⚠️ {errorMessage}");
      await LogMessageAsync($"WARNING: {errorMessage}. Exception: {exception.Message}");
      return 0;
    }

    // Запоминаем URL текущей страницы для возврата
    var currentPageUrl = webDriver.Url;
    await LogMessageAsync($"Сохранен URL страницы для возврата: {currentPageUrl}");

    // Обрабатываем всех персонажей на странице
    for (int characterIndex = 1; characterIndex <= CharactersPerPage; characterIndex++)
    {
      var characterXPath = $"{CharacterLinkXPathPrefix}{characterIndex}]";

      try
      {
        // Возвращаемся на страницу со списком, если мы где-то еще
        if (webDriver.Url != currentPageUrl)
        {
          Console.WriteLine($"🔙 Возвращаемся на страницу списка перед обработкой персонажа {characterIndex}");
          webDriver.Navigate().GoToUrl(currentPageUrl);
          await WaitForPageLoadAsync();
          await LogMessageAsync($"Возврат на страницу списка перед персонажем {characterIndex}");
        }

        var characterElement = webDriver.FindElement(By.XPath(characterXPath));
        var characterUrl = characterElement.GetAttribute("href");

        if (string.IsNullOrEmpty(characterUrl))
        {
          Console.WriteLine($"⚠️ URL персонажа {characterIndex} пуст");
          await LogMessageAsync($"WARNING: Empty URL for character {characterIndex} on page {pageNumber}");
          continue;
        }

        // Извлекаем ID персонажа из URL
        var characterId = ExtractCharacterIdFromUrl(characterUrl);

        if (string.IsNullOrEmpty(characterId))
        {
          var warningMessage = $"Не удалось извлечь ID персонажа из URL: {characterUrl}";
          Console.WriteLine($"⚠️ {warningMessage}");
          await LogMessageAsync($"WARNING: {warningMessage}");
          continue;
        }

        // Проверяем, был ли уже скачан
        if (downloadedCharacterIds.Contains(characterId))
        {
          Console.WriteLine($"⏩ Персонаж {characterId} уже скачан, пропускаем");
          await LogMessageAsync($"SKIPPED: Character {characterId} already downloaded");
          continue;
        }

        Console.WriteLine($"📥 Обрабатываем персонажа {characterIndex}/40: {characterId}");
        await LogMessageAsync($"Processing character {characterIndex}/40: {characterId}, URL: {characterUrl}");

        // Скачиваем PNG файл
        if (await DownloadCharacterPngAsync(characterUrl, characterId, currentPageUrl))
        {
          await SaveDownloadedCharacterIdAsync(characterId);
          processedCharacterCount++;
          Console.WriteLine($"✅ Персонаж {characterId} успешно скачан");
          await LogMessageAsync($"SUCCESS: Character {characterId} downloaded successfully");
        }
        else
        {
          Console.WriteLine($"❌ Не удалось скачать персонажа {characterId}");
          await LogMessageAsync($"FAILED: Could not download character {characterId}");
        }

        // Небольшая задержка между персонажами
        await Task.Delay(CharacterProcessDelayMilliseconds);
      }
      catch (NoSuchElementException exception)
      {
        var message = $"Персонаж {characterIndex} не найден на странице {pageNumber} (возможно, их меньше 40)";
        Console.WriteLine($"⚠️ {message}");
        await LogMessageAsync($"INFO: {message}. Exception: {exception.Message}");
        break;
      }
      catch (Exception exception)
      {
        var errorMessage = $"Ошибка при обработке персонажа {characterIndex} на странице {pageNumber}: {exception.Message}";
        Console.WriteLine($"❌ {errorMessage}");
        await LogMessageAsync($"ERROR: {errorMessage}\nStack Trace: {exception.StackTrace}");
        continue;
      }
    }

    await LogMessageAsync($"Завершена обработка страницы {pageNumber}, обработано персонажей: {processedCharacterCount}");
    return processedCharacterCount;
  }

  private static async Task<bool> DownloadCharacterPngAsync(string characterUrl, string characterId, string returnPageUrl)
{
    if (webDriver == null) return false;

    for (int attemptNumber = 1; attemptNumber <= MaxRetryAttempts; attemptNumber++)
    {
        try
        {
            Console.WriteLine($"🔄 Попытка {attemptNumber}/{MaxRetryAttempts} скачать персонажа {characterId}");
            await LogMessageAsync($"Download attempt {attemptNumber}/{MaxRetryAttempts} for character {characterId}");

            // Переходим на страницу персонажа
            webDriver.Navigate().GoToUrl(characterUrl);
            await WaitForPageLoadAsync();
            await LogMessageAsync($"Navigated to character page: {characterUrl}");

            // Дополнительная пауза для загрузки контента
            await Task.Delay(100);

            // Ищем кнопку скачивания
            var webDriverWait = new WebDriverWait(webDriver, TimeSpan.FromSeconds(7));
            IWebElement? downloadButton = null;

            try
            {
                // Выводим отладочную информацию
                var buttons = webDriver.FindElements(By.TagName("button"));
                Console.WriteLine($"🔍 Найдено кнопок на странице: {buttons.Count}");
                
                // Пробуем найти кнопку
                downloadButton = webDriverWait.Until(driver =>
                {
                    // Сначала пробуем найти по тексту
                    try
                    {
                        var btn = driver.FindElement(By.XPath("//button[contains(., 'Download') or contains(., 'download')]"));
                        Console.WriteLine("✅ Найдена кнопка по тексту 'Download'");
                        return btn;
                    }
                    catch { }
                    
                    // Потом по классу или атрибуту
                    try
                    {
                        var btn = driver.FindElement(By.XPath("//button[contains(@aria-label, 'Download') or contains(@title, 'Download')]"));
                        Console.WriteLine("✅ Найдена кнопка по aria-label/title");
                        return btn;
                    }
                    catch { }
                    
                    // По иконке
                    try
                    {
                        var btn = driver.FindElement(By.XPath("//button[.//svg]"));
                        Console.WriteLine("✅ Найдена кнопка с SVG иконкой");
                        return btn;
                    }
                    catch { }
                    
                    // Последняя попытка - первая кнопка
                    try
                    {
                        var buttons = driver.FindElements(By.TagName("button"));
                        if (buttons.Count > 0)
                        {
                            Console.WriteLine($"⚠️ Используем первую из {buttons.Count} кнопок");
                            return buttons[0];
                        }
                    }
                    catch { }
                    
                    throw new NoSuchElementException("Кнопка не найдена");
                });
            }
            catch (WebDriverTimeoutException exception)
            {
                var errorMessage = $"Кнопка скачивания не найдена для персонажа {characterId} (попытка {attemptNumber})";
                Console.WriteLine($"⚠️ {errorMessage}");
                
                // Делаем скриншот для отладки
                try
                {
                    var screenshot = ((ITakesScreenshot)webDriver).GetScreenshot();
                    var screenshotPath = Path.Combine(TemporaryDownloadFolderPath, $"error_{characterId}_{attemptNumber}.png");
                    screenshot.SaveAsFile(screenshotPath);
                    Console.WriteLine($"📸 Скриншот сохранен: {screenshotPath}");
                }
                catch { }
                
                await LogMessageAsync($"WARNING: {errorMessage}. Exception: {exception.Message}");

                if (attemptNumber == MaxRetryAttempts)
                {
                    webDriver.Navigate().GoToUrl(returnPageUrl);
                    await WaitForPageLoadAsync();
                    return false;
                }
                await Task.Delay(RetryDelayMilliseconds);
                continue;
            }

            await LogMessageAsync($"Download button found for character {characterId}");

            // Очищаем временную папку от старых файлов
            ClearTemporaryDownloadFolder();

            // Получаем количество файлов до скачивания (ищем и PNG и другие форматы)
            var allFilesBefore = Directory.GetFiles(TemporaryDownloadFolderPath).Length;
            await LogMessageAsync($"Files before download: {allFilesBefore} for character {characterId}");

            // Нажимаем кнопку скачивания
            ((IJavaScriptExecutor)webDriver).ExecuteScript("arguments[0].click();", downloadButton);
            await LogMessageAsync($"Download button clicked for character {characterId}");

            // Ждем появления нового файла
            var downloadSuccessful = await WaitForNewFileDownloadAsync(allFilesBefore, characterId);

            if (downloadSuccessful)
            {
                var moveSuccessful = await MoveDownloadedPngFileAsync(characterId);

                Console.WriteLine($"🔙 Возвращаемся на страницу списка после скачивания {characterId}");
                webDriver.Navigate().GoToUrl(returnPageUrl);
                await WaitForPageLoadAsync();
                await LogMessageAsync($"Returned to list page after downloading {characterId}");

                return moveSuccessful;
            }
            else
            {
                var warningMessage = $"Файл не скачался для персонажа {characterId} (попытка {attemptNumber})";
                Console.WriteLine($"⚠️ {warningMessage}");
                await LogMessageAsync($"WARNING: {warningMessage}");

                webDriver.Navigate().GoToUrl(returnPageUrl);
                await WaitForPageLoadAsync();

                if (attemptNumber == MaxRetryAttempts) return false;
                await Task.Delay(RetryDelayMilliseconds);
            }
        }
        catch (Exception exception)
        {
            var errorMessage = $"Ошибка при скачивании персонажа {characterId} (попытка {attemptNumber}): {exception.Message}";
            Console.WriteLine($"❌ {errorMessage}");
            await LogMessageAsync($"ERROR: {errorMessage}\nStack Trace: {exception.StackTrace}");

            try
            {
                webDriver.Navigate().GoToUrl(returnPageUrl);
                await WaitForPageLoadAsync();
                await LogMessageAsync($"Returned to list page after error for {characterId}");
            }
            catch (Exception navigationException)
            {
                await LogMessageAsync($"Failed to return to list page after error: {navigationException.Message}");
            }

            if (attemptNumber == MaxRetryAttempts) return false;
            await Task.Delay(RetryDelayMilliseconds);
        }
    }

    return false;
}

private static async Task<bool> WaitForNewFileDownloadAsync(int filesCountBeforeDownload, string characterId)
{
    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
    await LogMessageAsync($"Waiting for download file for character {characterId}, files before: {filesCountBeforeDownload}");

    while (stopwatch.ElapsedMilliseconds < DownloadTimeoutMilliseconds)
    {
        // Ищем все файлы, не только PNG
        var currentFiles = Directory.GetFiles(TemporaryDownloadFolderPath);
        
        if (currentFiles.Length > filesCountBeforeDownload)
        {
            // Берем последний файл
            var latestFile = currentFiles.OrderByDescending(filePath => File.GetCreationTime(filePath)).First();
            var fileInfo = new FileInfo(latestFile);

            await LogMessageAsync($"New file detected for {characterId}: {latestFile}, size: {fileInfo.Length} bytes");

            // Проверяем, что это не временный файл браузера
            if (!latestFile.EndsWith(".crdownload") && !latestFile.EndsWith(".tmp") && fileInfo.Length > 0)
            {
                await Task.Delay(FileStabilityCheckDelayMilliseconds);
                fileInfo.Refresh();

                if (fileInfo.Length > 0)
                {
                    Console.WriteLine($"✅ Файл скачан для персонажа {characterId}: {Path.GetFileName(latestFile)} ({fileInfo.Length} байт)");
                    await LogMessageAsync($"File download confirmed for {characterId} - stable size: {fileInfo.Length} bytes");
                    return true;
                }
            }
        }

        await Task.Delay(FileCheckIntervalMilliseconds);
    }

    // Выводим список файлов для отладки
    var finalFiles = Directory.GetFiles(TemporaryDownloadFolderPath);
    Console.WriteLine($"📁 Файлы в папке загрузок после таймаута: {finalFiles.Length}");
    foreach (var file in finalFiles)
    {
        Console.WriteLine($"  - {Path.GetFileName(file)}");
    }

    await LogMessageAsync($"Download timeout for character {characterId} after {DownloadTimeoutMilliseconds}ms");
    return false;
}


private static async Task<bool> MoveDownloadedPngFileAsync(string characterId)
{
    try
    {
        // Ищем последний скачанный файл (любого типа)
        var downloadedFiles = Directory.GetFiles(TemporaryDownloadFolderPath);

        if (downloadedFiles.Length == 0)
        {
            var warningMessage = $"Файл не найден в папке загрузок для персонажа {characterId}";
            Console.WriteLine($"⚠️ {warningMessage}");
            await LogMessageAsync($"WARNING: {warningMessage}");
            return false;
        }

        // Берем самый новый файл
        var sourceFile = downloadedFiles.OrderByDescending(filePath => File.GetCreationTime(filePath)).First();
        var extension = Path.GetExtension(sourceFile).ToLower();
        
        // Проверяем расширение
        if (extension != ".png" && extension != ".webp" && extension != ".jpg" && extension != ".jpeg")
        {
            Console.WriteLine($"⚠️ Неожиданное расширение файла: {extension}. Попробуем сохранить как PNG.");
        }
        
        var targetFilePath = Path.Combine(DownloadFolderPath, $"{characterId}.png");

        await LogMessageAsync($"Moving file from {sourceFile} to {targetFilePath}");

        // Проверяем, что целевой файл не существует
        if (File.Exists(targetFilePath))
        {
            await LogMessageAsync($"WARNING: Target file already exists, overwriting: {targetFilePath}");
            File.Delete(targetFilePath);
        }

        // Перемещаем файл
        File.Move(sourceFile, targetFilePath);

        // Проверяем успешность перемещения
        if (File.Exists(targetFilePath))
        {
            var targetFileInfo = new FileInfo(targetFilePath);
            Console.WriteLine($"🖼️ Файл сохранен: {characterId}.png ({targetFileInfo.Length} байт)");
            await LogMessageAsync($"File successfully saved for {characterId}, size: {targetFileInfo.Length} bytes");
            return true;
        }
        else
        {
            await LogMessageAsync($"ERROR: File move failed for {characterId}");
            return false;
        }
    }
    catch (Exception exception)
    {
        var errorMessage = $"Ошибка при перемещении файла для персонажа {characterId}: {exception.Message}";
        Console.WriteLine($"❌ {errorMessage}");
        await LogMessageAsync($"ERROR: {errorMessage}\nStack Trace: {exception.StackTrace}");
        return false;
    }
}


  private static string ExtractCharacterIdFromUrl(string characterUrl)
  {
    try
    {
      // Извлекаем ID из URL вида /characters/d920e577-b2ea-45c5-a80a-82026a1f9e85_character-flowey-the-flower
      var parsedUri = new Uri(characterUrl);
      var urlPath = parsedUri.AbsolutePath.TrimEnd('/');
      var lastSlashIndex = urlPath.LastIndexOf('/');

      if (lastSlashIndex >= 0 && lastSlashIndex < urlPath.Length - 1)
      {
        var lastUrlSegment = urlPath.Substring(lastSlashIndex + 1);

        // Ищем GUID паттерн в начале (8-4-4-4-12 символов через дефис)
        var urlSegmentParts = lastUrlSegment.Split('_');
        if (urlSegmentParts.Length > 0)
        {
          var guidPart = urlSegmentParts[0];
          // Проверяем, что это похоже на GUID
          if (Guid.TryParse(guidPart, out _))
          {
            return guidPart;
          }
        }

        // Если GUID не найден, возвращаем всю часть до первого подчеркивания
        return urlSegmentParts.Length > 0 ? urlSegmentParts[0] : lastUrlSegment;
      }

      return string.Empty;
    }
    catch (Exception exception)
    {
      Console.WriteLine($"⚠️ Ошибка извлечения ID из URL {characterUrl}: {exception.Message}");
      return string.Empty;
    }
  }

  private static async Task<bool> NavigateToPreviousPageAsync(int currentPageNumber)
  {
    if (webDriver == null) return false;

    try
    {
      await LogMessageAsync($"Attempting to go to previous page from {currentPageNumber}");

      // Проверяем, что мы не на первой странице
      if (currentPageNumber <= 1)
      {
        await LogMessageAsync("Already on first page");
        return false;
      }

      // Ищем все ссылки с номерами страниц
      var pageLinks = webDriver.FindElements(By.XPath("//a[contains(@href, '/?page=')]"));
      
      foreach (var link in pageLinks)
      {
        var href = link.GetAttribute("href");
        if (!string.IsNullOrEmpty(href) && href.Contains($"page={currentPageNumber - 1}"))
        {
          Console.WriteLine($"🔙 Переходим на страницу {currentPageNumber - 1}");
          await LogMessageAsync($"Navigating to previous page: {currentPageNumber - 1}");
          
          // Кликаем по ссылке
          link.Click();
          await WaitForPageLoadAsync();
          
          // Проверяем, что переход выполнен успешно
          var currentUrl = webDriver.Url;
          if (currentUrl.Contains($"page={currentPageNumber - 1}"))
          {
            await LogMessageAsync($"Successfully navigated to page {currentPageNumber - 1}");
            return true;
          }
        }
      }

      // Если не нашли прямую ссылку, пробуем найти кнопку "Previous" или стрелку
      try
      {
        var prevButton = webDriver.FindElement(By.XPath("//a[contains(text(), 'Previous') or contains(text(), 'Prev') or contains(text(), '←') or contains(text(), '‹')]"));
        var href = prevButton.GetAttribute("href");
        
        if (!string.IsNullOrEmpty(href) && !href.Contains("javascript:void"))
        {
          Console.WriteLine($"🔙 Используем кнопку Previous для перехода на страницу {currentPageNumber - 1}");
          prevButton.Click();
          await WaitForPageLoadAsync();
          return true;
        }
      }
      catch
      {
        // Кнопка Previous не найдена
      }

      var message = "Не удалось найти способ перехода на предыдущую страницу";
      Console.WriteLine($"⚠️ {message}");
      await LogMessageAsync($"WARNING: {message}");
      return false;
    }
    catch (Exception exception)
    {
      var errorMessage = $"Ошибка при переходе на предыдущую страницу с {currentPageNumber}: {exception.Message}";
      Console.WriteLine($"❌ {errorMessage}");
      await LogMessageAsync($"ERROR: {errorMessage}\nStack Trace: {exception.StackTrace}");
      return false;
    }
  }

  private static async Task WaitForPageLoadAsync()
  {
    await Task.Delay(PageLoadDelayMilliseconds);

    if (webDriver == null) return;

    // Дополнительная проверка готовности страницы
    var webDriverWait = new WebDriverWait(webDriver, TimeSpan.FromSeconds(5));
    try
    {
      webDriverWait.Until(driver => ((IJavaScriptExecutor)driver).ExecuteScript("return document.readyState").Equals("complete"));
    }
    catch
    {
      // Игнорируем таймауты
    }
  }

  private static void ClearTemporaryDownloadFolder()
  {
    try
    {
      var pngFiles = Directory.GetFiles(TemporaryDownloadFolderPath, "*.png");
      foreach (var pngFile in pngFiles)
      {
        try
        {
          File.Delete(pngFile);
        }
        catch
        {
          // Игнорируем ошибки удаления отдельных файлов
        }
      }
    }
    catch
    {
      // Игнорируем общие ошибки очистки
    }
  }

  private static async Task CleanupResourcesAsync()
  {
    Console.WriteLine("🧹 Очистка ресурсов...");
    await LogMessageAsync("Cleanup started");

    try
    {
      webDriver?.Quit();
      webDriver?.Dispose();
      await LogMessageAsync("WebDriver disposed successfully");
    }
    catch (Exception exception)
    {
      await LogMessageAsync($"Error disposing WebDriver: {exception.Message}");
    }

    // Очищаем временную папку (НЕ трогаем профиль Chrome!)
    try
    {
      if (Directory.Exists(TemporaryDownloadFolderPath))
      {
        Directory.Delete(TemporaryDownloadFolderPath, true);
        await LogMessageAsync("Temporary download folder deleted");
      }
    }
    catch (Exception exception)
    {
      await LogMessageAsync($"Error deleting temp folder: {exception.Message}");
    }

    Console.WriteLine("✅ Очистка завершена");
    Console.WriteLine($"🔐 Chrome профиль сохранен в: {ChromeProfileFolderPath}");
    await LogMessageAsync("Cleanup completed");
  }
}

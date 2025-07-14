using JannyAI.Models;

namespace JannyAI.Views;

public class ConsoleView : IConsoleView
{
    public void ShowWelcomeMessage()
    {
        Console.WriteLine("=== JannyAI Character PNG Downloader ===");
    }

    public int GetStartingPageFromUser(int lastPage)
    {
        Console.WriteLine($"\n📄 Последняя страница сайта: {lastPage}");
        Console.WriteLine("\nВыберите режим парсинга:");
        Console.WriteLine("1. Начать с конца (самые старые персонажи)");
        Console.WriteLine("2. Указать номер страницы");
        Console.Write("\nВаш выбор (1 или 2): ");

        var choice = Console.ReadLine()?.Trim();

        // Если пустой ввод или перенаправленный ввод - используем режим по умолчанию
        if (string.IsNullOrEmpty(choice))
        {
            Console.WriteLine("🔄 Автоматический режим: начинаем с последней страницы");
            return lastPage;
        }

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
                Console.WriteLine($"❌ Неверный номер страницы! Используем последнюю страницу по умолчанию.");
                return lastPage;
            }
        }
        else
        {
            Console.WriteLine("❌ Неверный выбор! Используем последнюю страницу по умолчанию.");
            return lastPage;
        }
    }

    public void ShowInitializationMessage(int existingCharacters)
    {
        Console.WriteLine("🔧 Инициализация...");
        Console.WriteLine($"✅ Инициализация завершена. Найдено уже скачанных персонажей: {existingCharacters}");
    }

    public void ShowPageProcessingStart(int pageNumber)
    {
        Console.WriteLine($"\n📄 === Страница {pageNumber} ===");
    }

    public void ShowCharacterProcessing(int currentIndex, int totalCount, string characterId)
    {
        Console.WriteLine($"📥 Обрабатываем персонажа {currentIndex}/{totalCount}: {characterId}");
    }

    public void ShowCharacterSuccess(string characterId)
    {
        Console.WriteLine($"✅ Персонаж {characterId} успешно скачан");
    }

    public void ShowCharacterFailure(string characterId, string error)
    {
        Console.WriteLine($"❌ Не удалось скачать персонажа {characterId}: {error}");
    }

    public void ShowCharacterSkipped(string characterId)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"⏭️ Персонаж {characterId} уже скачан - пропускаем");
        Console.ResetColor();
    }

    public void ShowPageCompleted(int pageNumber, int processedCount, int totalCount)
    {
        Console.WriteLine($"✅ Страница {pageNumber} завершена. Обработано: {processedCount}, всего: {totalCount}");
    }

    public void ShowProgressReport(int pagesProcessed, int charactersDownloaded)
    {
        Console.WriteLine($"📈 Прогресс: обработано {pagesProcessed} страниц, скачано {charactersDownloaded} новых персонажей");
    }

    public void ShowError(string message)
    {
        Console.WriteLine($"❌ {message}");
    }

    public void ShowWarning(string message)
    {
        Console.WriteLine($"⚠️ {message}");
    }

    public void ShowFinalStatistics(int totalProcessed, int totalExisting, string downloadPath, string logPath)
    {
        Console.WriteLine($"\n🎉 === ПАРСИНГ ЗАВЕРШЕН - Обработано новых персонажей: {totalProcessed}, Общее количество: {totalExisting} ===");
        Console.WriteLine($"💾 Файлы сохранены в папке: {downloadPath}");
        Console.WriteLine($"📄 Логи сохранены в файле: {logPath}");
    }

    public void ShowNavigationToPreviousPage(int pageNumber)
    {
        Console.WriteLine($"🔙 Переходим на страницу {pageNumber}");
    }

    public void ShowParsingCompleted()
    {
        Console.WriteLine("🏁 Достигнута первая страница или кнопка 'Prev' недоступна");
    }
}
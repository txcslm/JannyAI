#!/bin/bash

echo "=== Тестирование новой архитектуры JannyAI ==="

echo "1. Сборка проекта..."
dotnet build

if [ $? -eq 0 ]; then
    echo "✅ Сборка прошла успешно"
else
    echo "❌ Ошибка сборки"
    exit 1
fi

echo ""
echo "2. Запуск с автоматическим режимом (5 секунд)..."
echo "" | timeout 5 dotnet run

echo ""
echo "3. Проверка структуры проекта..."
echo "📁 Структура папок:"
find . -type d -name "*" | grep -E "(Models|Views|Services|Presenters|Configuration)" | sort

echo ""
echo "📄 Файлы архитектуры:"
find . -name "*.cs" | grep -v bin | grep -v obj | sort

echo ""
echo "4. Проверка логов..."
if [ -f "jannyai_parser.log" ]; then
    echo "✅ Лог файл создан"
    echo "📊 Последние 5 строк лога:"
    tail -n 5 jannyai_parser.log
else
    echo "❌ Лог файл не найден"
fi

echo ""
echo "5. Проверка папок..."
if [ -d "jannyai_characters" ]; then
    echo "✅ Папка jannyai_characters создана"
else
    echo "❌ Папка jannyai_characters не найдена"
fi

if [ -d "ChromeProfile" ]; then
    echo "✅ Папка ChromeProfile создана"
else
    echo "❌ Папка ChromeProfile не найдена"
fi

echo ""
echo "6. Показать улучшения навигации и поиска кнопок..."
echo "🎯 Улучшения в поиске кнопки Download:"
echo "   • 10 различных стратегий поиска кнопки скачивания"
echo "   • Поддержка альтернативных контейнеров (mt-4, flex)"
echo "   • Поиск по градиентным классам и SVG иконкам"
echo "   • Отладочные сообщения для диагностики"
echo ""
echo "🔙 Улучшения в навигации по страницам:"
echo "   • Поиск кнопки Prev в 2 основных местах"
echo "   • Поиск по тексту 'Prev' и классу 'rounded-l-lg'"
echo "   • Поиск ссылки на предыдущую страницу по номеру"
echo "   • Поиск по стрелкам ← и ‹"
echo "   • Fallback стратегии для максимальной совместимости"
echo ""
echo "=== Тестирование завершено ==="
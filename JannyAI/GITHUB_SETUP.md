# 🚀 Настройка GitHub интеграции

## 📋 Требования

1. **GitHub репозиторий** для хранения картинок
2. **Personal Access Token** с правами на запись в репозиторий

## 🔧 Пошаговая настройка

### 1. Создание Personal Access Token

1. Перейдите в GitHub → Settings → Developer settings → Personal access tokens → Tokens (classic)
2. Нажмите "Generate new token"
3. Выберите нужные права:
   - `repo` (полный доступ к репозиториям)
   - `workflow` (если планируете использовать GitHub Actions)
4. Скопируйте созданный token

### 2. Настройка переменных окружения

Установите следующие переменные окружения:

```bash
# GitHub token для доступа к API
export GITHUB_TOKEN="z``"

# Владелец репозитория (ваш username)
export GITHUB_OWNER="txcslm"

# Название репозитория
export GITHUB_REPOSITORY="JannyAI"

# Ветка для загрузки (по умолчанию main)
export GITHUB_BRANCH="main"
```

### 3. Структура файлов в GitHub

Картинки будут загружаться в следующую структуру:
```
your-repo/
├── characters/
│   ├── character-id-1/
│   │   └── character-id-1.png
│   ├── character-id-2/
│   │   └── character-id-2.png
│   └── ...
```

### 4. Для macOS/Linux (Terminal)

```bash
# Добавьте в ~/.bash_profile или ~/.zshrc
export GITHUB_TOKEN="ghp_your_token_here"
export GITHUB_OWNER="yourusername"
export GITHUB_REPOSITORY="JannyAI"
export GITHUB_BRANCH="main"

# Перезагрузите терминал или выполните:
source ~/.bash_profile
```

### 5. Для Windows (PowerShell)

```powershell
# Установка переменных для текущей сессии
$env:GITHUB_TOKEN="ghp_your_token_here"
$env:GITHUB_OWNER="yourusername"
$env:GITHUB_REPOSITORY="JannyAI"
$env:GITHUB_BRANCH="main"

# Или постоянно через System Properties
```

### 6. Для Windows (Command Prompt)

```cmd
set GITHUB_TOKEN=ghp_your_token_here
set GITHUB_OWNER=yourusername
set GITHUB_REPOSITORY=JannyAI
set GITHUB_BRANCH=main
```

## 🔍 Проверка настройки

Запустите программу - если переменные настроены правильно, вы увидите:
```
🔗 Режим загрузки: GitHub Repository
🔍 GitHub сервис инициализирован для yourusername/JannyAI
```

Если переменные не настроены, программа будет работать в локальном режиме:
```
💾 Режим загрузки: Локальное хранилище
```

## 🌟 Преимущества GitHub хранилища

- ✅ **Автоматическое резервное копирование**
- ✅ **Доступ из любого места**
- ✅ **Версионирование файлов**
- ✅ **Публичный доступ к картинкам**
- ✅ **Неограниченное хранилище** (для публичных репозиториев)

## 🔗 Доступ к картинкам

После загрузки картинки будут доступны по URL:
```
https://raw.githubusercontent.com/yourusername/JannyAI/main/characters/character-id/character-id.png
```

## ⚠️ Важные замечания

1. **Безопасность**: Не коммитьте token в код!
2. **Лимиты**: GitHub API имеет лимиты на количество запросов
3. **Размер файлов**: Максимальный размер файла 100MB
4. **Приватность**: Для приватных репозиториев нужны дополнительные права

## 🚨 Устранение проблем

### Ошибка: "GitHub настройки не найдены"
- Проверьте переменные окружения
- Перезапустите терминал/IDE

### Ошибка: "401 Unauthorized"
- Проверьте правильность token
- Убедитесь, что token имеет нужные права

### Ошибка: "404 Not Found"
- Проверьте правильность имени репозитория
- Убедитесь, что репозиторий существует и доступен
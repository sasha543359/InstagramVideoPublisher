# 🎬 Instagram Video Publisher

Профессиональное консольное приложение для автоматической публикации видео и изображений в Instagram через официальный API.

![.NET](https://img.shields.io/badge/.NET-8.0-purple)
![License](https://img.shields.io/badge/license-MIT-green)

## ✨ Возможности

- 📹 **Публикация видео** - автоматическая загрузка и постинг видео
- 📷 **Публикация изображений** - быстрая публикация фото
- 🎨 **Красивый UI** - интерактивный интерфейс с Spectre.Console
- 🏗️ **Архитектура** - Dependency Injection, сервисы, модели
- 📝 **Логирование** - подробное логирование всех операций
- ⚙️ **Конфигурация** - простая настройка через appsettings.json

## 🚀 Быстрый старт

### 1. Предварительные требования

- ✅ .NET 8.0 SDK или выше
- ✅ Instagram Business/Creator аккаунт
- ✅ Facebook App с Instagram API
- ✅ Access Token для Instagram API

### 2. Установка

```bash
# Распакуйте архив
unzip InstagramVideoPublisher.zip

# Перейдите в папку
cd InstagramVideoPublisher

# Восстановите зависимости
dotnet restore
```

### 3. Настройка

Откройте `appsettings.json` и вставьте ваши данные:

```json
{
  "Instagram": {
    "AccessToken": "ВАSH_ТОКЕН_ОТ_GRAPH_API_EXPLORER",
    "AccountId": "17841474844042448"
  }
}
```

**Как получить Access Token:**

1. Откройте: https://developers.facebook.com/tools/explorer/
2. Выберите ваше приложение **"VideoAutoPost"**
3. Нажмите **"Generate Access Token"**
4. Выберите разрешения:
   - `instagram_basic`
   - `instagram_content_publish`
   - `pages_read_engagement`
   - `pages_show_list`
5. Скопируйте токен (нажмите иконку 📋)
6. Вставьте в `appsettings.json`

### 4. Подготовка видео

Положите видео файл на рабочий стол:
```
C:\Users\Computer\Desktop\video.mp4
```

**Требования к видео:**
- ✅ Формат: MP4, MOV
- ✅ Кодек: H.264
- ✅ Размер: до 100 MB
- ✅ Длительность: 3-60 секунд (для Reels до 90 сек)
- ✅ Соотношение сторон: 9:16, 1:1, или 16:9

### 5. Запуск

```bash
dotnet run
```

## 📖 Использование

### Главное меню

После запуска вы увидите интерактивное меню:

```
╔═══════════════════════════════════════════════╗
║        Instagram Video Publisher              ║
╚═══════════════════════════════════════════════╝

Выберите действие:
❯ 📹 Опубликовать видео с рабочего стола
  📷 Опубликовать изображение (тест)
  📁 Опубликовать видео из другой папки
  ℹ️  Информация о приложении
  ❌ Выход
```

### 1. Публикация видео с рабочего стола

1. Выберите первый пункт меню
2. Приложение автоматически найдёт `video.mp4` на рабочем столе
3. Введите текст для поста (caption)
4. Дождитесь завершения публикации

**Процесс:**
```
📤 Загружаем видео на временный хостинг...
✓ Видео загружено: https://file.io/...

📹 Создаём video container в Instagram...
✓ Video container создан: 17867174784494802

⏳ Ожидаем обработки видео Instagram...
✓ Видео обработано успешно!

📱 Публикуем видео...
✓ Видео успешно опубликовано!

Media ID: 17867174784494802_...
Проверьте ваш Instagram: @0_bimbimbambam_0
```

### 2. Публикация изображения (тест)

Быстрый тест для проверки работоспособности API:
- Публикует случайное тестовое изображение
- Не требует подготовки файлов
- Мгновенная публикация (без ожидания обработки)

### 3. Публикация видео из другой папки

Позволяет указать произвольный путь к видео файлу:
```
Введите полный путь к видео:
> C:\Videos\my-video.mp4
```

## 🏗️ Архитектура

### Структура проекта

```
InstagramVideoPublisher/
├── Models/
│   └── Models.cs              # Модели данных
├── Services/
│   ├── IServices.cs           # Интерфейсы сервисов
│   ├── InstagramService.cs    # Работа с Instagram API
│   └── FileService.cs         # Работа с файлами
├── Program.cs                 # Главный файл приложения
├── appsettings.json          # Конфигурация
└── InstagramVideoPublisher.csproj
```

### Используемые паттерны

- **Dependency Injection** - внедрение зависимостей
- **Service Layer** - слой сервисов
- **Repository Pattern** - работа с данными
- **SOLID принципы** - чистый код

### Технологии

- **.NET 8.0** - платформа
- **Microsoft.Extensions.DependencyInjection** - DI контейнер
- **Microsoft.Extensions.Configuration** - конфигурация
- **Microsoft.Extensions.Logging** - логирование
- **Spectre.Console** - красивый консольный UI
- **Newtonsoft.Json** - работа с JSON
- **Instagram Graph API** - публикация контента

## 📝 API Flow

### Публикация изображения

```
1. CreateImageContainer
   POST /v21.0/{account-id}/media
   {
     "image_url": "https://...",
     "caption": "..."
   }
   → Returns: creation_id

2. PublishContainer
   POST /v21.0/{account-id}/media_publish
   {
     "creation_id": "..."
   }
   → Returns: media_id
```

### Публикация видео

```
1. CreateVideoContainer
   POST /v21.0/{account-id}/media
   {
     "media_type": "VIDEO",
     "video_url": "https://...",
     "caption": "..."
   }
   → Returns: creation_id

2. CheckVideoStatus (циклически)
   GET /v21.0/{creation-id}?fields=status_code
   → Wait until status_code = "FINISHED"

3. PublishContainer
   POST /v21.0/{account-id}/media_publish
   {
     "creation_id": "..."
   }
   → Returns: media_id
```

## ⚙️ Конфигурация

### appsettings.json

```json
{
  "Instagram": {
    "AccessToken": "YOUR_ACCESS_TOKEN",
    "AccountId": "YOUR_INSTAGRAM_BUSINESS_ID"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    }
  }
}
```

## 🔒 Безопасность

- ⚠️ **НЕ коммитьте** `appsettings.json` с реальными токенами в Git
- ⚠️ Access Token живёт ~1 час (краткосрочный)
- ✅ Для продакшена используйте долгосрочные токены (60 дней)
- ✅ Храните токены в переменных окружения или секретных хранилищах

### Получение долгосрочного токена

```bash
curl -X GET "https://graph.facebook.com/v21.0/oauth/access_token?grant_type=fb_exchange_token&client_id=YOUR_APP_ID&client_secret=YOUR_APP_SECRET&fb_exchange_token=YOUR_SHORT_TOKEN"
```

## 🐛 Решение проблем

### "Invalid OAuth access token"

**Проблема:** Токен неправильный или истёк

**Решение:**
1. Получите новый токен в Graph API Explorer
2. Убедитесь, что токен полностью скопирован
3. Проверьте наличие пробелов в начале/конце

### "Only photo or video can be accepted"

**Проблема:** Неправильный URL видео или формат

**Решение:**
1. Убедитесь, что URL публичный (HTTPS)
2. Проверьте формат видео (MP4 с H.264)
3. Размер файла должен быть до 100 MB

### "Media not ready"

**Проблема:** Instagram ещё обрабатывает видео

**Решение:**
- Приложение автоматически ждёт до 2.5 минут
- Если видео большое - может потребоваться больше времени
- Проверьте формат и кодек видео

### Видео не загружается на file.io

**Проблема:** file.io - бесплатный сервис с ограничениями

**Решение для продакшена:**
1. Используйте AWS S3
2. Используйте Azure Blob Storage
3. Используйте Cloudinary
4. Настройте собственный веб-сервер

## 🎯 Следующие шаги

### Расширения функционала

1. **TikTok Integration**
   - Добавить мониторинг TikTok аккаунтов
   - Автоматическое скачивание новых видео
   - API: TikTok API или web scraping

2. **FFmpeg Processing**
   - Обработка видео перед публикацией
   - Добавление водяных знаков
   - Изменение скорости, обрезка
   - Изменение разрешения

3. **Scheduler**
   - Планирование публикаций
   - Автоматический запуск по расписанию
   - Windows Task Scheduler / Cron

4. **Database**
   - Сохранение истории публикаций
   - Отслеживание статистики
   - SQLite / PostgreSQL

5. **Web Interface**
   - ASP.NET Core Web API
   - React / Blazor UI
   - Dashboard с аналитикой

## 📊 Лимиты Instagram API

- **Публикации:** До 25 постов в 24 часа на аккаунт
- **API запросы:** 200 запросов в час (Graph API)
- **Размер видео:** До 100 MB
- **Длительность видео:** 3-60 секунд (Reels до 90 сек)

## 📚 Полезные ссылки

- [Instagram API Documentation](https://developers.facebook.com/docs/instagram-api)
- [Graph API Explorer](https://developers.facebook.com/tools/explorer/)
- [Content Publishing Guide](https://developers.facebook.com/docs/instagram-api/guides/content-publishing)
- [Spectre.Console Docs](https://spectreconsole.net/)

## 📄 Лицензия

MIT License - используйте свободно!

## 🤝 Поддержка

Если возникли вопросы или проблемы - создавайте Issue в репозитории.

---

**Сделано с ❤️ для автоматизации контента**

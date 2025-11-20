namespace InstagramVideoPublisher.Models
{
    /// <summary>
    /// Настройки для ОДНОГО Instagram аккаунта
    /// </summary>
    public class InstagramAccountSettings
    {
        public string AccountName { get; set; } = string.Empty;
        public string AccessToken { get; set; } = string.Empty;
        public string AccountId { get; set; } = string.Empty;
        public List<string> TikTokUsernames { get; set; } = new List<string>();
    }

    /// <summary>
    /// Корневые настройки приложения
    /// </summary>
    public class AppSettings
    {
        public List<InstagramAccountSettings> InstagramAccounts { get; set; } = new();
        public int CheckIntervalMinutes { get; set; } = 5;
        public int AccountCheckDelayMinutes { get; set; } = 1;
    }

    /// <summary>
    /// Настройки для одного Instagram аккаунта (для совместимости со старым кодом)
    /// </summary>
    public class InstagramSettings
    {
        public string AccessToken { get; set; } = string.Empty;
        public string AccountId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Настройки Cloudinary (больше не используется, но оставляем для совместимости)
    /// </summary>
    public class CloudinarySettings
    {
        public string CloudName { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string ApiSecret { get; set; } = string.Empty;
    }

    /// <summary>
    /// Настройки мониторинга TikTok
    /// </summary>
    public class TikTokMonitorSettings
    {
        public string YtDlpPath { get; set; } = string.Empty;
        public string DownloadPath { get; set; } = string.Empty;
    }

    /// <summary>
    /// Настройки обработки видео
    /// </summary>
    public class VideoProcessingSettings
    {
        public string FFmpegPath { get; set; } = "ffmpeg";
        public bool RemoveMetadata { get; set; } = true;
        public bool AddCapCutMetadata { get; set; } = false;
        public int VolumeIncrease { get; set; } = 10; // %
    }

    /// <summary>
    /// Результат публикации
    /// </summary>
    public class PublishResult
    {
        public bool Success { get; set; }
        public string? MediaId { get; set; }
        public string? ErrorMessage { get; set; }
        public string? CreationId { get; set; }
    }

    /// <summary>
    /// Статус обработки видео
    /// </summary>
    public class VideoStatus
    {
        public string StatusCode { get; set; } = string.Empty;
        public bool IsReady => StatusCode == "FINISHED";
        public bool IsError => StatusCode == "ERROR";
    }

    /// <summary>
    /// Информация о видео для публикации
    /// </summary>
    public class VideoPublishInfo
    {
        public string FilePath { get; set; } = string.Empty;
        public string Caption { get; set; } = string.Empty;
        public string? VideoUrl { get; set; }
    }

    /// <summary>
    /// Информация о TikTok видео
    /// </summary>
    public class TikTokVideo
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string UploadDate { get; set; } = string.Empty;
        public int Duration { get; set; }
        public bool IsVideo => Duration > 0;
    }
}
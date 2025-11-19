namespace InstagramVideoPublisher.Models
{
    /// <summary>
    /// Настройки Instagram API
    /// </summary>
    public class InstagramSettings
    {
        public string AccessToken { get; set; } = string.Empty;
        public string AccountId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Настройки Cloudinary
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
        public List<string> TikTokUsernames { get; set; } = new List<string>();
        public bool TestMode { get; set; } = false;
        public int CheckIntervalMinutes { get; set; } = 5;
        public int AccountCheckDelaySeconds { get; set; } = 60;
        public string YtDlpPath { get; set; } = @"C:\Users\Computer\AppData\Local\Programs\Python\Python313\Scripts\yt-dlp.exe";
        public string DownloadPath { get; set; } = @"C:\Users\Computer\Desktop\TikTokVideos";
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
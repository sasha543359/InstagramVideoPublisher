namespace InstagramBulkPublisher.Models
{
    /// <summary>
    /// Настройки Instagram аккаунта
    /// </summary>
    public class InstagramAccount
    {
        public string AccountName { get; set; } = string.Empty;
        public string AccountId { get; set; } = string.Empty;
        public string AccessToken { get; set; } = string.Empty;
    }

    /// <summary>
    /// Главные настройки приложения
    /// </summary>
    public class AppSettings
    {
        /// <summary>
        /// Путь к CSV файлу с аккаунтами
        /// </summary>
        public string AccountsCsvPath { get; set; } = "accounts.csv";

        /// <summary>
        /// Путь к папке с готовыми видео
        /// </summary>
        public string VideosFolder { get; set; } = "/var/www/videos";

        /// <summary>
        /// Путь к JSON файлу с аккаунтами (генерируется из CSV)
        /// </summary>
        public string AccountsJsonPath { get; set; } = "accounts.json";

        /// <summary>
        /// Публичный URL сервера для доступа к видео
        /// </summary>
        public string ServerPublicUrl { get; set; } = "http://YOUR_SERVER_IP";

        /// <summary>
        /// Количество параллельных публикаций
        /// </summary>
        public int ParallelPublishCount { get; set; } = 5;

        /// <summary>
        /// Задержка между пачками публикаций (секунды)
        /// </summary>
        public int DelayBetweenPublishSeconds { get; set; } = 5;

        /// <summary>
        /// Caption для публикаций
        /// </summary>
        public string DefaultCaption { get; set; } = "#reels #viral #trending";

        /// <summary>
        /// Удалять видео после успешной публикации
        /// </summary>
        public bool DeleteAfterPublish { get; set; } = true;
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
    /// Статус обработки видео Instagram
    /// </summary>
    public class VideoStatus
    {
        public string StatusCode { get; set; } = string.Empty;
        public bool IsReady => StatusCode == "FINISHED";
        public bool IsError => StatusCode == "ERROR";
    }

    /// <summary>
    /// Информация для публикации видео
    /// </summary>
    public class VideoPublishInfo
    {
        public string VideoUrl { get; set; } = string.Empty;
        public string Caption { get; set; } = string.Empty;
    }

    /// <summary>
    /// Результат публикации для отчёта
    /// </summary>
    public class PublishReport
    {
        public string AccountName { get; set; } = string.Empty;
        public string VideoFile { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? MediaId { get; set; }
        public string? Error { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}
using System.Collections.Generic;

namespace InstagramVideoPublisher.Models
{
    public class InstagramAccountSettings
    {
        public string AccountName { get; set; } = string.Empty;
        public string AccessToken { get; set; } = string.Empty;
        public string AccountId { get; set; } = string.Empty;
        public List<string> TikTokUsernames { get; set; } = new List<string>();
    }

    public class AppSettings
    {
        public List<InstagramAccountSettings> InstagramAccounts { get; set; } = new();
        public int CheckIntervalMinutes { get; set; } = 10;
        public int AccountCheckDelayMinutes { get; set; } = 1;
        public int TikTokCheckDelaySeconds { get; set; } = 30;
    }

    public class ServerSettings
    {
        public string PublicUrl { get; set; } = string.Empty;
        public string VideoPath { get; set; } = string.Empty;
    }

    public class InstagramSettings
    {
        public string AccessToken { get; set; } = string.Empty;
        public string AccountId { get; set; } = string.Empty;
    }

    public class TikTokMonitorSettings
    {
        public string YtDlpPath { get; set; } = string.Empty;
        public string DownloadPath { get; set; } = string.Empty;
    }

    public class VideoProcessingSettings
    {
        public string FFmpegPath { get; set; } = "ffmpeg";
        public bool RemoveMetadata { get; set; } = true;
        public bool AddCapCutMetadata { get; set; } = false;
        public int VolumeIncrease { get; set; } = 10;
    }

    public class PublishResult
    {
        public bool Success { get; set; }
        public string? MediaId { get; set; }
        public string? ErrorMessage { get; set; }
        public string? CreationId { get; set; }
    }

    public class VideoStatus
    {
        public string StatusCode { get; set; } = string.Empty;
        public bool IsReady => StatusCode == "FINISHED";
        public bool IsError => StatusCode == "ERROR";
    }

    public class VideoPublishInfo
    {
        public string FilePath { get; set; } = string.Empty;
        public string Caption { get; set; } = string.Empty;
        public string? VideoUrl { get; set; }
    }

    public class TikTokVideo
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string UploadDate { get; set; } = string.Empty;
        public int Duration { get; set; }
        public long Timestamp { get; set; } // Unix timestamp
        public bool IsVideo => Duration > 0;
    }

    /// <summary>
    /// Запись о видео в истории
    /// </summary>
    public class VideoHistoryEntry
    {
        public string Id { get; set; } = string.Empty;
        public long Timestamp { get; set; }
    }

    /// <summary>
    /// История видео для одного TikTok аккаунта (последние 5 видео)
    /// </summary>
    public class TikTokAccountHistory
    {
        public List<VideoHistoryEntry> Videos { get; set; } = new List<VideoHistoryEntry>();

        /// <summary>
        /// Проверить, есть ли видео в истории
        /// </summary>
        public bool ContainsVideo(string videoId)
        {
            return Videos.Any(v => v.Id == videoId);
        }

        /// <summary>
        /// Получить самый свежий timestamp из истории
        /// </summary>
        public long GetLatestTimestamp()
        {
            return Videos.Count > 0 ? Videos.Max(v => v.Timestamp) : 0;
        }

        /// <summary>
        /// Добавить новое видео в историю (автоматически удаляет самое старое если > 5)
        /// </summary>
        public void AddVideo(string videoId, long timestamp)
        {
            // Добавляем новое видео в начало
            Videos.Insert(0, new VideoHistoryEntry { Id = videoId, Timestamp = timestamp });

            // Оставляем только последние 5
            if (Videos.Count > 5)
            {
                Videos = Videos.Take(5).ToList();
            }
        }
    }
}
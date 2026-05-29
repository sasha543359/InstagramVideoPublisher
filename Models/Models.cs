using System.Collections.Generic;

namespace InstagramVideoPublisher.Models
{
    public class InstagramAccountSettings
    {
        public string AccountName { get; set; } = string.Empty;
        public string AccessToken { get; set; } = string.Empty;
        public string AccountId { get; set; } = string.Empty;
        public List<string> TikTokUsernames { get; set; } = new List<string>();
        public string? CustomCaption { get; set; }
        // Имя файла обложки в папке /var/www/videos/covers/ на сервере (напр. "cover.jpg").
        // Если задано — используется как обложка Reels. Пусто/нет — обложка как раньше (Instagram сам).
        public string? CoverImage { get; set; }
        // Альтернатива: взять кадр из самого видео (миллисекунды, напр. 1000 = 1-я секунда).
        // Используется только если CoverImage НЕ задан.
        public int? ThumbOffsetMs { get; set; }
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
        public string? CoverUrl { get; set; }
        public int? ThumbOffsetMs { get; set; }
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
    /// ������ � ����� � �������
    /// </summary>
    public class VideoHistoryEntry
    {
        public string Id { get; set; } = string.Empty;
        public long Timestamp { get; set; }
    }

    /// <summary>
    /// ������� ����� ��� ������ TikTok �������� (��������� 5 �����)
    /// </summary>
    public class TikTokAccountHistory
    {
        public List<VideoHistoryEntry> Videos { get; set; } = new List<VideoHistoryEntry>();

        /// <summary>
        /// ���������, ���� �� ����� � �������
        /// </summary>
        public bool ContainsVideo(string videoId)
        {
            return Videos.Any(v => v.Id == videoId);
        }

        /// <summary>
        /// �������� ����� ������ timestamp �� �������
        /// </summary>
        public long GetLatestTimestamp()
        {
            return Videos.Count > 0 ? Videos.Max(v => v.Timestamp) : 0;
        }

        /// <summary>
        /// �������� ����� ����� � ������� (������������� ������� ����� ������ ���� > 5)
        /// </summary>
        public void AddVideo(string videoId, long timestamp)
        {
            // ��������� ����� ����� � ������
            Videos.Insert(0, new VideoHistoryEntry { Id = videoId, Timestamp = timestamp });

            // ��������� ������ ��������� 5
            if (Videos.Count > 5)
            {
                Videos = Videos.Take(5).ToList();
            }
        }
    }
}
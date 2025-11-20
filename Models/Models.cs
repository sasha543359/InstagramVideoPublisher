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
        public bool IsVideo => Duration > 0;
    }
}
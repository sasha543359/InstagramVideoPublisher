using InstagramVideoPublisher.Models;

namespace InstagramVideoPublisher.Services
{
    /// <summary>
    /// Сервис для публикации контента в Instagram
    /// </summary>
    public interface IInstagramService
    {
        /// <summary>
        /// Публикация изображения
        /// </summary>
        Task<PublishResult> PublishImageAsync(string imageUrl, string caption);

        /// <summary>
        /// Публикация видео
        /// </summary>
        Task<PublishResult> PublishVideoAsync(VideoPublishInfo videoInfo);

        /// <summary>
        /// Проверка статуса обработки видео
        /// </summary>
        Task<VideoStatus> CheckVideoStatusAsync(string creationId);
    }

    /// <summary>
    /// Сервис для работы с файлами
    /// </summary>
    public interface IFileService
    {
        /// <summary>
        /// Загрузка видео на временный хостинг
        /// </summary>
        Task<string> UploadVideoAsync(string filePath);

        /// <summary>
        /// Проверка существования файла
        /// </summary>
        bool FileExists(string filePath);

        /// <summary>
        /// Получение информации о файле
        /// </summary>
        FileInfo GetFileInfo(string filePath);
    }

    /// <summary>
    /// Сервис для мониторинга TikTok
    /// </summary>
    public interface ITikTokMonitorService
    {
        /// <summary>
        /// Получить последние видео с TikTok (до 5 штук)
        /// </summary>
        Task<List<TikTokVideo>> GetLatestVideos(string username);

        /// <summary>
        /// Проверить наличие нового видео
        /// </summary>
        Task<TikTokVideo?> CheckForNewVideo(string username, string historyKey);

        /// <summary>
        /// Скачать видео
        /// </summary>
        Task<string> DownloadVideo(TikTokVideo video, string? customPath = null);

        /// <summary>
        /// Отметить видео как обработанное (сохранить ID и timestamp в историю)
        /// </summary>
        void MarkVideoAsProcessed(string historyKey, string videoId, long timestamp);
    }

}
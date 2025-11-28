namespace InstagramBulkPublisher.Services
{
    /// <summary>
    /// Сервис для работы с файлами видео
    /// </summary>
    public interface IVideoFileService
    {
        /// <summary>
        /// Получить список всех видео в папке
        /// </summary>
        List<string> GetAllVideos(string folderPath);

        /// <summary>
        /// Получить публичный URL для видео
        /// </summary>
        string GetPublicUrl(string filePath, string serverUrl, string videosFolder);
    }

    public class VideoFileService : IVideoFileService
    {
        private readonly string[] _videoExtensions = { ".mp4", ".mov", ".avi", ".mkv", ".webm" };

        public List<string> GetAllVideos(string folderPath)
        {
            if (!Directory.Exists(folderPath))
            {
                throw new DirectoryNotFoundException($"Папка с видео не найдена: {folderPath}");
            }

            var videos = Directory.GetFiles(folderPath)
                .Where(f => _videoExtensions.Contains(Path.GetExtension(f).ToLower()))
                .OrderBy(f => f) // Сортировка по имени
                .ToList();

            return videos;
        }

        public string GetPublicUrl(string filePath, string serverUrl, string videosFolder)
        {
            var fileName = Path.GetFileName(filePath);

            // Убираем trailing slash если есть
            serverUrl = serverUrl.TrimEnd('/');

            // Формируем URL
            return $"{serverUrl}/videos/{fileName}";
        }
    }
}
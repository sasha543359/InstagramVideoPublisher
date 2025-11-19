using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using InstagramVideoPublisher.Models;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace InstagramVideoPublisher.Services
{
    public class FileService : IFileService
    {
        private readonly ILogger<FileService> _logger;
        private readonly CloudinarySettings _cloudinarySettings;
        private readonly Cloudinary _cloudinary;

        public FileService(ILogger<FileService> logger, IOptions<CloudinarySettings> cloudinarySettings)
        {
            _logger = logger;
            _cloudinarySettings = cloudinarySettings.Value;

            // Инициализация Cloudinary
            var account = new Account(
                _cloudinarySettings.CloudName,
                _cloudinarySettings.ApiKey,
                _cloudinarySettings.ApiSecret
            );
            _cloudinary = new Cloudinary(account);
        }

        public async Task<string> UploadVideoAsync(string filePath)
        {
            try
            {
                _logger.LogInformation($"Загрузка видео на Cloudinary: {filePath}");

                var uploadParams = new VideoUploadParams()
                {
                    File = new FileDescription(filePath),
                    PublicId = $"instagram_video_{DateTime.Now.Ticks}"
                };

                var uploadResult = await _cloudinary.UploadAsync(uploadParams);

                if (uploadResult.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    throw new Exception($"Ошибка загрузки на Cloudinary: {uploadResult.Error?.Message}");
                }

                var url = uploadResult.SecureUrl.ToString();
                _logger.LogInformation($"Видео загружено на Cloudinary: {url}");

                return url;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке видео на Cloudinary");
                throw;
            }
        }

        public bool FileExists(string filePath)
        {
            return File.Exists(filePath);
        }

        public FileInfo GetFileInfo(string filePath)
        {
            return new FileInfo(filePath);
        }
    }
}
using System.Text;
using InstagramVideoPublisher.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace InstagramVideoPublisher.Services
{
    public class InstagramService : IInstagramService
    {
        private readonly InstagramSettings _settings;
        private readonly ILogger<InstagramService> _logger;
        private readonly HttpClient _httpClient;
        private const string BASE_URL = "https://graph.instagram.com/v24.0";

        public InstagramService(
            IOptions<InstagramSettings> settings,
            ILogger<InstagramService> logger,
            HttpClient httpClient)
        {
            _settings = settings.Value;
            _logger = logger;
            _httpClient = httpClient;
        }

        public async Task<PublishResult> PublishImageAsync(string imageUrl, string caption)
        {
            try
            {
                _logger.LogInformation("Начинаем публикацию изображения...");

                // Шаг 1: Создаём media container
                var creationId = await CreateImageContainerAsync(imageUrl, caption);
                if (string.IsNullOrEmpty(creationId))
                {
                    return new PublishResult
                    {
                        Success = false,
                        ErrorMessage = "Не удалось создать media container"
                    };
                }

                _logger.LogInformation($"Media container создан: {creationId}");

                // Шаг 2: Публикуем
                var mediaId = await PublishContainerAsync(creationId);
                if (string.IsNullOrEmpty(mediaId))
                {
                    return new PublishResult
                    {
                        Success = false,
                        ErrorMessage = "Не удалось опубликовать контент"
                    };
                }

                _logger.LogInformation($"Контент опубликован: {mediaId}");

                return new PublishResult
                {
                    Success = true,
                    MediaId = mediaId,
                    CreationId = creationId
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при публикации изображения");
                return new PublishResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<PublishResult> PublishVideoAsync(VideoPublishInfo videoInfo)
        {
            try
            {
                Console.WriteLine("=== DEBUG: PublishVideoAsync STARTED ===");
                _logger.LogInformation("Начинаем публикацию видео...");

                // Шаг 1: Создаём video container
                var creationId = await CreateVideoContainerAsync(videoInfo.VideoUrl!, videoInfo.Caption);
                if (string.IsNullOrEmpty(creationId))
                {
                    Console.WriteLine("=== DEBUG: CreateVideoContainerAsync returned NULL ===");
                    return new PublishResult
                    {
                        Success = false,
                        ErrorMessage = "Не удалось создать video container"
                    };
                }

                _logger.LogInformation($"Video container создан: {creationId}");

                // Шаг 2: Ждём обработки видео
                _logger.LogInformation("Ожидаем обработки видео Instagram...");
                var isReady = await WaitForVideoProcessingAsync(creationId);

                if (!isReady)
                {
                    return new PublishResult
                    {
                        Success = false,
                        ErrorMessage = "Видео не было обработано Instagram",
                        CreationId = creationId
                    };
                }

                _logger.LogInformation("Видео обработано успешно!");

                // Шаг 3: Публикуем
                var mediaId = await PublishContainerAsync(creationId);
                if (string.IsNullOrEmpty(mediaId))
                {
                    return new PublishResult
                    {
                        Success = false,
                        ErrorMessage = "Не удалось опубликовать видео"
                    };
                }

                _logger.LogInformation($"Видео опубликовано: {mediaId}");

                return new PublishResult
                {
                    Success = true,
                    MediaId = mediaId,
                    CreationId = creationId
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"=== DEBUG: EXCEPTION: {ex.Message} ===");
                _logger.LogError(ex, "Ошибка при публикации видео");
                return new PublishResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<VideoStatus> CheckVideoStatusAsync(string creationId)
        {
            try
            {
                var url = $"{BASE_URL}/{creationId}?fields=status_code&access_token={_settings.AccessToken}";
                var response = await _httpClient.GetAsync(url);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"Не удалось проверить статус: {content}");
                    return new VideoStatus { StatusCode = "UNKNOWN" };
                }

                var result = JObject.Parse(content);
                var statusCode = result["status_code"]?.ToString() ?? "UNKNOWN";

                return new VideoStatus { StatusCode = statusCode };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при проверке статуса видео");
                return new VideoStatus { StatusCode = "ERROR" };
            }
        }

        private async Task<string?> CreateImageContainerAsync(string imageUrl, string caption)
        {
            var url = $"{BASE_URL}/{_settings.AccountId}/media";

            var formData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("image_url", imageUrl),
                new KeyValuePair<string, string>("caption", caption),
                new KeyValuePair<string, string>("access_token", _settings.AccessToken)
            });

            var response = await _httpClient.PostAsync(url, formData);
            var responseText = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"Ошибка создания image container: {responseText}");
                return null;
            }

            var result = JObject.Parse(responseText);
            return result["id"]?.ToString();
        }

        private async Task<string?> CreateVideoContainerAsync(string videoUrl, string caption)
        {
            Console.WriteLine("=== DEBUG: CreateVideoContainerAsync CALLED ===");

            // ПЕРЕДАЁМ ВСЁ В URL, А НЕ В BODY!
            var url = $"{BASE_URL}/{_settings.AccountId}/media?" +
                      $"media_type=REELS&" +
                      $"video_url={Uri.EscapeDataString(videoUrl)}&" +
                      $"caption={Uri.EscapeDataString(caption)}&" +
                      $"access_token={_settings.AccessToken}";

            Console.WriteLine($"=== DEBUG: Full URL (first 150 chars) = {url.Substring(0, Math.Min(150, url.Length))}... ===");

            // Отправляем POST с ПУСТЫМ body
            var response = await _httpClient.PostAsync(url, null);
            Console.WriteLine($"=== DEBUG: Response Status Code = {response.StatusCode} ===");

            var responseText = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"=== DEBUG: Response Body = {responseText} ===");

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine("=== DEBUG: REQUEST FAILED ===");
                _logger.LogError($"Ошибка создания video container. Status: {response.StatusCode}");
                _logger.LogError($"Response: {responseText}");
                return null;
            }

            Console.WriteLine("=== DEBUG: REQUEST SUCCESS ===");
            var result = JObject.Parse(responseText);
            var containerId = result["id"]?.ToString();
            Console.WriteLine($"=== DEBUG: Container ID = {containerId} ===");
            return containerId;
        }

        private async Task<string?> PublishContainerAsync(string creationId)
        {
            var url = $"{BASE_URL}/{_settings.AccountId}/media_publish";

            var formData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("creation_id", creationId),
                new KeyValuePair<string, string>("access_token", _settings.AccessToken)
            });

            var response = await _httpClient.PostAsync(url, formData);
            var responseText = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"Ошибка публикации: {responseText}");
                return null;
            }

            var result = JObject.Parse(responseText);
            return result["id"]?.ToString();
        }

        private async Task<bool> WaitForVideoProcessingAsync(string creationId, int maxAttempts = 30)
        {
            for (int i = 0; i < maxAttempts; i++)
            {
                await Task.Delay(5000); // Ждём 5 секунд между проверками

                var status = await CheckVideoStatusAsync(creationId);

                _logger.LogInformation($"Статус обработки ({i + 1}/{maxAttempts}): {status.StatusCode}");

                if (status.IsReady)
                {
                    return true;
                }

                if (status.IsError)
                {
                    _logger.LogError("Instagram не смог обработать видео");
                    return false;
                }

                // IN_PROGRESS - продолжаем ждать
            }

            _logger.LogWarning("Превышено время ожидания обработки видео");
            return false;
        }
    }
}
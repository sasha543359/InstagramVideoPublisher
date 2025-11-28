using InstagramBulkPublisher.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace InstagramBulkPublisher.Services
{
    /// <summary>
    /// Сервис для публикации в Instagram
    /// </summary>
    public interface IInstagramService
    {
        Task<PublishResult> PublishVideoAsync(string videoUrl, string caption);
        Task<VideoStatus> CheckVideoStatusAsync(string creationId);
    }

    public class InstagramService : IInstagramService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<InstagramService> _logger;
        private readonly string _accessToken;
        private readonly string _accountId;
        private const string BASE_URL = "https://graph.instagram.com/v24.0";

        public InstagramService(
            HttpClient httpClient,
            ILogger<InstagramService> logger,
            string accessToken,
            string accountId)
        {
            _httpClient = httpClient;
            _logger = logger;
            _accessToken = accessToken;
            _accountId = accountId;
        }

        public async Task<PublishResult> PublishVideoAsync(string videoUrl, string caption)
        {
            try
            {
                // Шаг 1: Создаём video container
                var creationId = await CreateVideoContainerAsync(videoUrl, caption);
                if (string.IsNullOrEmpty(creationId))
                {
                    return new PublishResult
                    {
                        Success = false,
                        ErrorMessage = "Не удалось создать video container"
                    };
                }

                _logger.LogDebug($"Video container создан: {creationId}");

                // Шаг 2: Ждём обработки видео
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

                return new PublishResult
                {
                    Success = true,
                    MediaId = mediaId,
                    CreationId = creationId
                };
            }
            catch (Exception ex)
            {
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
                var url = $"{BASE_URL}/{creationId}?fields=status_code&access_token={_accessToken}";
                var response = await _httpClient.GetAsync(url);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return new VideoStatus { StatusCode = "UNKNOWN" };
                }

                var result = JObject.Parse(content);
                var statusCode = result["status_code"]?.ToString() ?? "UNKNOWN";

                return new VideoStatus { StatusCode = statusCode };
            }
            catch
            {
                return new VideoStatus { StatusCode = "ERROR" };
            }
        }

        private async Task<string?> CreateVideoContainerAsync(string videoUrl, string caption)
        {
            var url = $"{BASE_URL}/{_accountId}/media?" +
                      $"media_type=REELS&" +
                      $"video_url={Uri.EscapeDataString(videoUrl)}&" +
                      $"caption={Uri.EscapeDataString(caption)}&" +
                      $"access_token={_accessToken}";

            var response = await _httpClient.PostAsync(url, null);
            var responseText = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"Ошибка создания container: {responseText}");
                return null;
            }

            var result = JObject.Parse(responseText);
            return result["id"]?.ToString();
        }

        private async Task<string?> PublishContainerAsync(string creationId)
        {
            var url = $"{BASE_URL}/{_accountId}/media_publish";

            var formData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("creation_id", creationId),
                new KeyValuePair<string, string>("access_token", _accessToken)
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

        private async Task<bool> WaitForVideoProcessingAsync(string creationId, int maxAttempts = 60)
        {
            for (int i = 0; i < maxAttempts; i++)
            {
                await Task.Delay(3000); // Ждём 3 секунды между проверками

                var status = await CheckVideoStatusAsync(creationId);

                if (status.IsReady)
                {
                    return true;
                }

                if (status.IsError)
                {
                    return false;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Фабрика для создания Instagram сервисов с разными аккаунтами
    /// </summary>
    public interface IInstagramServiceFactory
    {
        IInstagramService Create(string accessToken, string accountId);
    }

    public class InstagramServiceFactory : IInstagramServiceFactory
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<InstagramService> _logger;

        public InstagramServiceFactory(
            IHttpClientFactory httpClientFactory,
            ILogger<InstagramService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public IInstagramService Create(string accessToken, string accountId)
        {
            var httpClient = _httpClientFactory.CreateClient();
            return new InstagramService(httpClient, _logger, accessToken, accountId);
        }
    }
}
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using InstagramVideoPublisher.Models;
using Newtonsoft.Json;

namespace InstagramVideoPublisher.Services
{
    public class TikTokMonitorService : ITikTokMonitorService
    {
        private readonly ILogger<TikTokMonitorService> _logger;
        private readonly TikTokMonitorSettings _settings;
        private readonly string _lastVideoIdFile = "last_video_id.json";
        private Dictionary<string, string> _lastVideoIds = new();
        private HashSet<string> _processedInTestMode = new();

        public TikTokMonitorService(
            ILogger<TikTokMonitorService> logger,
            IOptions<TikTokMonitorSettings> settings)
        {
            _logger = logger;
            _settings = settings.Value;

            // Создаём папку для скачивания если не существует
            if (!Directory.Exists(_settings.DownloadPath))
            {
                Directory.CreateDirectory(_settings.DownloadPath);
            }

            // Загружаем историю последних видео
            LoadLastVideoIds();
        }

        public async Task<List<TikTokVideo>> GetLatestVideos(string username)
        {
            try
            {
                var url = $"https://www.tiktok.com/@{username}";

                var args = $"--flat-playlist " +
                           $"--print \"%(id)s|%(title)s|%(upload_date)s|%(duration)s\" " +
                           $"--playlist-end 10 " +
                           $"\"{url}\"";

                var output = await RunProcessAsync(_settings.YtDlpPath, args);

                var videos = new List<TikTokVideo>();

                foreach (var line in output.Split('\n'))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var parts = line.Split('|');
                    if (parts.Length >= 4)
                    {
                        var videoId = parts[0].Trim();
                        var title = parts[1].Trim();
                        var uploadDate = parts[2].Trim();
                        var durationStr = parts[3].Trim();

                        int.TryParse(durationStr, out int duration);

                        videos.Add(new TikTokVideo
                        {
                            Id = videoId,
                            Title = title,
                            UploadDate = uploadDate,
                            Duration = duration,
                            Url = $"https://www.tiktok.com/@{username}/video/{videoId}"
                        });
                    }
                }

                return videos;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка получения видео с @{username}");
                return new List<TikTokVideo>();
            }
        }

        public async Task<TikTokVideo?> CheckForNewVideo(string username)
        {
            try
            {
                _logger.LogInformation($"Проверяем новые видео у @{username}");

                var videos = await GetLatestVideos(username);

                // Фильтруем только видео (не слайдеры фото)
                var videoList = videos.Where(v => v.IsVideo).ToList();

                if (videoList.Count == 0)
                {
                    _logger.LogInformation("Видео не найдены");
                    return null;
                }

                var latestVideo = videoList.First();

                // ТЕСТОВЫЙ РЕЖИМ: Скачиваем ТОЛЬКО если этот аккаунт ещё НЕ обработан
                if (_settings.TestMode && !_processedInTestMode.Contains(username))
                {
                    _logger.LogInformation($"🧪 ТЕСТОВЫЙ РЕЖИМ: Скачиваем последнее видео с @{username} - {latestVideo.Id}");

                    // Запоминаем ID и добавляем в обработанные
                    _lastVideoIds[username] = latestVideo.Id;
                    _processedInTestMode.Add(username);
                    SaveLastVideoIds();

                    return latestVideo;
                }

                // Проверяем, видели ли мы это видео раньше
                if (_lastVideoIds.ContainsKey(username) && _lastVideoIds[username] == latestVideo.Id)
                {
                    _logger.LogInformation($"Нет новых видео (последнее: {latestVideo.Id})");
                    return null;
                }

                // Если это первый запуск БЕЗ тестового режима - просто сохраняем ID
                if (!_lastVideoIds.ContainsKey(username) && !_settings.TestMode)
                {
                    _logger.LogInformation($"Первый запуск - запоминаем текущее видео: {latestVideo.Id}");
                    _lastVideoIds[username] = latestVideo.Id;
                    SaveLastVideoIds();
                    return null;
                }

                // Новое видео найдено!
                _logger.LogInformation($"🎉 Найдено новое видео: {latestVideo.Title}");
                _lastVideoIds[username] = latestVideo.Id;
                SaveLastVideoIds();

                return latestVideo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка проверки новых видео");
                return null;
            }
        }

        public async Task<string> DownloadVideo(TikTokVideo video, string? customPath = null)
        {
            try
            {
                var outputPath = customPath ?? Path.Combine(_settings.DownloadPath, $"{video.Id}.mp4");

                _logger.LogInformation($"Скачиваем видео: {video.Title}");
                _logger.LogInformation($"URL: {video.Url}");
                _logger.LogInformation($"Путь: {outputPath}");

                var args = $"-o \"{outputPath}\" " +
                           $"--format \"best[ext=mp4]\" " +
                           $"--no-playlist " +
                           $"\"{video.Url}\"";

                await RunProcessAsync(_settings.YtDlpPath, args, showProgress: true);

                if (!File.Exists(outputPath))
                {
                    throw new Exception("Файл не был скачан!");
                }

                _logger.LogInformation($"✅ Видео скачано: {outputPath}");
                return outputPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка скачивания видео: {video.Url}");
                throw;
            }
        }

        private void LoadLastVideoIds()
        {
            try
            {
                if (File.Exists(_lastVideoIdFile))
                {
                    var json = File.ReadAllText(_lastVideoIdFile);
                    _lastVideoIds = JsonConvert.DeserializeObject<Dictionary<string, string>>(json)
                                   ?? new Dictionary<string, string>();
                    _logger.LogInformation($"Загружена история: {_lastVideoIds.Count} аккаунтов");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Не удалось загрузить историю");
                _lastVideoIds = new Dictionary<string, string>();
            }
        }

        private void SaveLastVideoIds()
        {
            try
            {
                var json = JsonConvert.SerializeObject(_lastVideoIds, Formatting.Indented);
                File.WriteAllText(_lastVideoIdFile, json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка сохранения истории");
            }
        }

        private async Task<string> RunProcessAsync(string fileName, string arguments, bool showProgress = false)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = !showProgress,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8
            };

            using var process = new Process { StartInfo = startInfo };

            var outputBuilder = new System.Text.StringBuilder();
            var errorBuilder = new System.Text.StringBuilder();

            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    outputBuilder.AppendLine(e.Data);
                    if (showProgress)
                    {
                        Console.WriteLine(e.Data);
                    }
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    errorBuilder.AppendLine(e.Data);
                    if (showProgress)
                    {
                        Console.WriteLine(e.Data);
                    }
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();

            var output = outputBuilder.ToString();
            var error = errorBuilder.ToString();

            if (process.ExitCode != 0)
            {
                throw new Exception($"yt-dlp завершился с ошибкой (код {process.ExitCode}):\n{error}");
            }

            return output;
        }
    }
}
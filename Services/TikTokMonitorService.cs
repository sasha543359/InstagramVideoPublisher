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
        private readonly string _historyFile = "video_history.json";
        private Dictionary<string, TikTokAccountHistory> _accountHistories = new();

        public TikTokMonitorService(
            ILogger<TikTokMonitorService> logger,
            IOptions<TikTokMonitorSettings> settings)
        {
            _logger = logger;
            _settings = settings.Value;

            if (!Directory.Exists(_settings.DownloadPath))
            {
                Directory.CreateDirectory(_settings.DownloadPath);
            }

            LoadHistory();
        }

        public async Task<List<TikTokVideo>> GetLatestVideos(string username)
        {
            try
            {
                var url = $"https://www.tiktok.com/@{username}";

                // Получаем последние 5 видео с ID, title, upload_date, duration и timestamp
                var args = $"--flat-playlist " +
                           $"--print \"%(id)s|%(title)s|%(upload_date)s|%(duration)s|%(timestamp)s\" " +
                           $"--playlist-end 5 " +
                           $"\"{url}\"";

                var output = await RunProcessAsync(_settings.YtDlpPath, args);

                var videos = new List<TikTokVideo>();

                foreach (var line in output.Split('\n'))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var parts = line.Split('|');
                    if (parts.Length >= 5)
                    {
                        var videoId = parts[0].Trim();
                        var title = parts[1].Trim();
                        var uploadDate = parts[2].Trim();
                        var durationStr = parts[3].Trim();
                        var timestampStr = parts[4].Trim();

                        int.TryParse(durationStr, out int duration);
                        long.TryParse(timestampStr, out long timestamp);

                        videos.Add(new TikTokVideo
                        {
                            Id = videoId,
                            Title = title,
                            UploadDate = uploadDate,
                            Duration = duration,
                            Timestamp = timestamp,
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

        public async Task<TikTokVideo?> CheckForNewVideo(string username, string historyKey)
        {
            try
            {
                _logger.LogInformation($"   Проверяем новые видео у @{username} (история: {historyKey})");

                var videos = await GetLatestVideos(username);

                var videoList = videos.Where(v => v.IsVideo).ToList();

                if (videoList.Count == 0)
                {
                    _logger.LogInformation("   Видео не найдены (аккаунт может не существовать или изменил ник)");
                    return null;
                }

                // Если ключа нет в истории - первый запуск для этой пары
                if (!_accountHistories.ContainsKey(historyKey))
                {
                    _logger.LogInformation($"   🆕 Первый запуск для {historyKey}");
                    _logger.LogInformation($"   Инициализируем историю с последними {videoList.Count} видео");

                    var history = new TikTokAccountHistory();
                    foreach (var video in videoList)
                    {
                        history.AddVideo(video.Id, video.Timestamp);
                        _logger.LogInformation($"      - {video.Id} (timestamp: {video.Timestamp})");
                    }

                    _accountHistories[historyKey] = history;
                    SaveHistory();

                    _logger.LogInformation($"   ✅ История инициализирована, следующие видео будут публиковаться");
                    return null; // НЕ публикуем при первом запуске
                }

                var accountHistory = _accountHistories[historyKey];
                var latestTimestamp = accountHistory.GetLatestTimestamp();

                // Проверяем каждое видео
                foreach (var video in videoList)
                {
                    // Условия для публикации:
                    // 1. ID НЕТ в истории
                    // 2. Timestamp БОЛЬШЕ самого свежего в истории (защита от старых видео)
                    if (!accountHistory.ContainsVideo(video.Id) && video.Timestamp > latestTimestamp)
                    {
                        _logger.LogInformation($"   🎉 Найдено НОВОЕ видео!");
                        _logger.LogInformation($"      Название: {video.Title}");
                        _logger.LogInformation($"      ID: {video.Id}");
                        _logger.LogInformation($"      Timestamp: {video.Timestamp} (последний в истории: {latestTimestamp})");

                        return video;
                    }
                }

                _logger.LogInformation($"   Нет новых видео (последнее: {videoList.First().Id})");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"   ❌ Ошибка проверки @{username}: {ex.Message}");
                _logger.LogInformation("   Пропускаем этот аккаунт и продолжаем...");
                return null;
            }
        }

        public void MarkVideoAsProcessed(string historyKey, string videoId, long timestamp)
        {
            try
            {
                _logger.LogInformation($"   ✅ Сохраняем видео {videoId} в историю [{historyKey}]");

                if (!_accountHistories.ContainsKey(historyKey))
                {
                    _accountHistories[historyKey] = new TikTokAccountHistory();
                }

                _accountHistories[historyKey].AddVideo(videoId, timestamp);
                SaveHistory();

                _logger.LogInformation($"   История обновлена. Всего видео в истории: {_accountHistories[historyKey].Videos.Count}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"   ❌ Ошибка сохранения истории для [{historyKey}]");
            }
        }

        public async Task<string> DownloadVideo(TikTokVideo video, string? customPath = null)
        {
            try
            {
                var outputPath = customPath ?? Path.Combine(_settings.DownloadPath, $"{video.Id}.mp4");

                _logger.LogInformation($"   📥 Скачиваем видео: {video.Title}");
                _logger.LogInformation($"   URL: {video.Url}");

                var args = $"-o \"{outputPath}\" " +
                           $"--format \"best[ext=mp4]\" " +
                           $"--no-playlist " +
                           $"\"{video.Url}\"";

                await RunProcessAsync(_settings.YtDlpPath, args, showProgress: false);

                if (!File.Exists(outputPath))
                {
                    throw new Exception("Файл не был скачан!");
                }

                _logger.LogInformation($"   ✅ Видео скачано: {Path.GetFileName(outputPath)}");
                return outputPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"   ❌ Ошибка скачивания видео: {video.Url}");
                throw;
            }
        }

        private void LoadHistory()
        {
            try
            {
                if (File.Exists(_historyFile))
                {
                    var json = File.ReadAllText(_historyFile);
                    _accountHistories = JsonConvert.DeserializeObject<Dictionary<string, TikTokAccountHistory>>(json)
                                       ?? new Dictionary<string, TikTokAccountHistory>();

                    var totalVideos = _accountHistories.Sum(h => h.Value.Videos.Count);
                    _logger.LogInformation($"📂 Загружена история: {_accountHistories.Count} аккаунтов, {totalVideos} видео");

                    // Показываем историю для каждого аккаунта
                    foreach (var kvp in _accountHistories)
                    {
                        _logger.LogInformation($"   @{kvp.Key}: {kvp.Value.Videos.Count} видео в истории");
                    }
                }
                else
                {
                    _logger.LogWarning("⚠️  Файл video_history.json не найден!");
                    _logger.LogInformation("🆕 Первый запуск - будет создан новый файл");
                    _accountHistories = new Dictionary<string, TikTokAccountHistory>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Не удалось загрузить историю");
                _accountHistories = new Dictionary<string, TikTokAccountHistory>();
            }
        }

        private void SaveHistory()
        {
            try
            {
                var json = JsonConvert.SerializeObject(_accountHistories, Formatting.Indented);
                File.WriteAllText(_historyFile, json);
                _logger.LogDebug($"Файл {_historyFile} обновлён");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Ошибка сохранения истории");
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
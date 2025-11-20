using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using InstagramVideoPublisher.Models;

namespace InstagramVideoPublisher.Services
{
    public class VideoProcessingService : IVideoProcessingService
    {
        private readonly ILogger<VideoProcessingService> _logger;
        private readonly VideoProcessingSettings _settings;

        public VideoProcessingService(
            ILogger<VideoProcessingService> logger,
            IOptions<VideoProcessingSettings> settings)
        {
            _logger = logger;
            _settings = settings.Value;
        }

        public async Task<string> ProcessVideoAsync(string inputPath)
        {
            try
            {
                var outputPath = inputPath.Replace(".mp4", "_processed.mp4");

                _logger.LogInformation($"🎬 Обрабатываем видео: {Path.GetFileName(inputPath)}");

                // Строим фильтры видео
                var videoFilters = new List<string>
        {
            "scale=720:1280:force_original_aspect_ratio=decrease",
            "pad=720:1280:(ow-iw)/2:(oh-ih)/2",
            "setpts=PTS/1.02",  // Ускорение 2%
            "eq=brightness=0.02:saturation=1.03:contrast=1.01"  // Цветокоррекция
        };

                var videoFilter = string.Join(",", videoFilters);

                // Увеличение громкости
                var volumeMultiplier = 1 + (_settings.VolumeIncrease / 100.0);
                var audioFilter = $"volume={volumeMultiplier.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

                // Собираем команду FFmpeg
                var args = $"-i \"{inputPath}\" " +
                           $"-vf \"{videoFilter}\" " +
                           $"-af \"{audioFilter}\" " +
                           $"-c:v libx264 -profile:v high -level 4.0 -preset fast -crf 23 " +
                           $"-maxrate 12000k -bufsize 24000k " +
                           $"-c:a aac -b:a 192k -ar 44100 " +
                           $"-map_metadata -1 " +
                           $"-map_metadata:s:v -1 " +
                           $"-map_metadata:s:a -1 " +
                           $"-fflags +bitexact -flags:v +bitexact -flags:a +bitexact ";

                // Добавляем метаданные CapCut (если включено)
                if (_settings.AddCapCutMetadata)
                {
                    args += $"-metadata Hw=\"1\" " +
                            $"-metadata bitrate=\"12000000\" " +
                            $"-metadata encoder=\"Lavf61.1.100\" ";
                }

                args += $"-y \"{outputPath}\"";

                Console.WriteLine($"\n========================================");
                Console.WriteLine($"FFmpeg команда:");
                Console.WriteLine($"{args}");
                Console.WriteLine($"========================================\n");

                _logger.LogInformation("Запускаем FFmpeg...");
                await RunFFmpegAsync(args);

                if (!File.Exists(outputPath))
                {
                    throw new Exception("FFmpeg не создал выходной файл!");
                }

                var outputSize = new FileInfo(outputPath).Length;
                _logger.LogInformation($"Размер обработанного файла: {outputSize / 1024 / 1024:F2} MB");

                File.Delete(inputPath);
                File.Move(outputPath, inputPath);

                _logger.LogInformation($"✅ Видео обработано: {Path.GetFileName(inputPath)}");

                return inputPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка обработки видео");
                throw;
            }
        }
        private string GenerateCapCutMetadata()
        {
            var videoId = Guid.NewGuid().ToString();
            var musicId = Guid.NewGuid().ToString();

            var metadata = new
            {
                data = new
                {
                    editType = "default",
                    infoStickerId = "",
                    is_ai_lyric = 0,
                    is_aimusic_mv = 0,
                    is_use_ai_image_generation = 0,
                    is_use_ai_video_generation = 0,
                    is_use_aimusic_bgm = 0,
                    is_use_aimusic_vocal = 0,
                    is_use_graph_chart = 0,
                    is_use_jichuang_mode_in_ai_writer = 0,
                    is_use_relight = 0,
                    is_use_vc_sing_clone = 1,
                    is_use_voice_clone = "0",
                    motion_blur_cnt = 0,
                    musicId = musicId,
                    os = "windows",
                    product = "vicut",
                    stickerId = "",
                    videoEffectId = "",
                    videoId = videoId,
                    videoParams = new
                    {
                        be = 0,
                        ef = 0,
                        ft = 0,
                        ma = 0,
                        me = 0,
                        mu = 0,
                        re = 0,
                        sp = 0,
                        st = 0,
                        te = 0,
                        tx = 4,
                        v = 0,
                        vs = 0
                    }
                },
                source_platform = "desktop",
                source_type = "vicut"
            };

            return System.Text.Json.JsonSerializer.Serialize(metadata);
        }

        private string EscapeJson(string json)
        {
            // Экранируем JSON для использования в команде FFmpeg
            return json.Replace("\"", "\\\"");
        }

        private async Task RunFFmpegAsync(string arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _settings.FFmpegPath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };

            var output = new System.Text.StringBuilder();
            var error = new System.Text.StringBuilder();

            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    output.AppendLine(e.Data);
                    _logger.LogDebug($"FFmpeg: {e.Data}");
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    error.AppendLine(e.Data);
                    // FFmpeg пишет прогресс в stderr, это нормально
                    _logger.LogDebug($"FFmpeg: {e.Data}");
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                var errorText = error.ToString();
                _logger.LogError($"FFmpeg завершился с ошибкой:\n{errorText}");
                throw new Exception($"FFmpeg error (код {process.ExitCode}):\n{errorText}");
            }

            _logger.LogInformation("FFmpeg завершил работу успешно");
        }
    }
}
using InstagramBulkPublisher.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace InstagramBulkPublisher.Services
{
    /// <summary>
    /// Главный сервис для массовой публикации видео
    /// </summary>
    public interface IBulkPublishService
    {
        /// <summary>
        /// Запустить массовую публикацию
        /// </summary>
        Task<List<PublishReport>> PublishAllAsync(
            List<InstagramAccount> accounts,
            List<string> videos,
            string serverUrl,
            string videosFolder,
            string caption,
            int parallelCount,
            int delaySeconds,
            bool deleteAfterPublish = true);
    }

    public class BulkPublishService : IBulkPublishService
    {
        private readonly IInstagramServiceFactory _instagramFactory;
        private readonly IVideoFileService _videoFileService;
        private readonly ILogger<BulkPublishService> _logger;

        // Лок для потокобезопасного добавления в отчёт
        private readonly object _reportLock = new object();

        public BulkPublishService(
            IInstagramServiceFactory instagramFactory,
            IVideoFileService videoFileService,
            ILogger<BulkPublishService> logger)
        {
            _instagramFactory = instagramFactory;
            _videoFileService = videoFileService;
            _logger = logger;
        }

        public async Task<List<PublishReport>> PublishAllAsync(
            List<InstagramAccount> accounts,
            List<string> videos,
            string serverUrl,
            string videosFolder,
            string caption,
            int parallelCount,
            int delaySeconds,
            bool deleteAfterPublish = true)
        {
            var reports = new List<PublishReport>();
            var totalTasks = Math.Min(accounts.Count, videos.Count);

            _logger.LogInformation($"========================================");
            _logger.LogInformation($"СТАРТ ПУБЛИКАЦИИ");
            _logger.LogInformation($"Всего пар (видео + аккаунт): {totalTasks}");
            _logger.LogInformation($"Параллельных потоков: {parallelCount}");
            _logger.LogInformation($"Удалять после публикации: {deleteAfterPublish}");
            _logger.LogInformation($"========================================");

            // Создаём список задач — ФИКСИРОВАННЫЕ пары: видео[i] → аккаунт[i]
            var publishQueue = new List<PublishTask>();
            for (int i = 0; i < totalTasks; i++)
            {
                publishQueue.Add(new PublishTask
                {
                    Index = i,
                    Account = accounts[i],
                    VideoPath = videos[i]
                });
            }

            // Обрабатываем ПАЧКАМИ по parallelCount
            int batchNumber = 0;

            for (int i = 0; i < publishQueue.Count; i += parallelCount)
            {
                batchNumber++;

                // Берём следующую пачку
                var batch = publishQueue
                    .Skip(i)
                    .Take(parallelCount)
                    .ToList();

                _logger.LogInformation($"");
                _logger.LogInformation($"--- ПАЧКА #{batchNumber}: позиции {i + 1}-{i + batch.Count} из {totalTasks} ---");

                // Запускаем ВСЕ задачи в пачке параллельно
                var batchTasks = batch.Select(task =>
                    PublishSingleAsync(task, serverUrl, videosFolder, caption, deleteAfterPublish)
                ).ToList();

                // Ждём завершения ВСЕЙ пачки
                var batchResults = await Task.WhenAll(batchTasks);

                // Добавляем результаты в отчёт
                lock (_reportLock)
                {
                    reports.AddRange(batchResults);
                }

                // Показываем статистику пачки
                var successInBatch = batchResults.Count(r => r.Success);
                var failedInBatch = batchResults.Count(r => !r.Success);
                _logger.LogInformation($"--- Пачка #{batchNumber} завершена: ✅ {successInBatch} / ❌ {failedInBatch} ---");

                // Задержка между пачками (кроме последней)
                if (i + parallelCount < publishQueue.Count)
                {
                    _logger.LogInformation($"⏳ Задержка {delaySeconds} сек перед следующей пачкой...");
                    await Task.Delay(delaySeconds * 1000);
                }
            }

            // Финальная статистика
            var totalSuccess = reports.Count(r => r.Success);
            var totalFailed = reports.Count(r => !r.Success);

            _logger.LogInformation($"");
            _logger.LogInformation($"========================================");
            _logger.LogInformation($"ЗАВЕРШЕНО");
            _logger.LogInformation($"Успешно: {totalSuccess}");
            _logger.LogInformation($"Ошибок: {totalFailed}");
            _logger.LogInformation($"========================================");

            // Сохраняем отчёт
            await SaveReportAsync(reports);

            return reports;
        }

        private async Task<PublishReport> PublishSingleAsync(
            PublishTask task,
            string serverUrl,
            string videosFolder,
            string caption,
            bool deleteAfterPublish)
        {
            var report = new PublishReport
            {
                AccountName = task.Account.AccountName,
                VideoFile = Path.GetFileName(task.VideoPath),
                Timestamp = DateTime.Now
            };

            var logPrefix = $"[{task.Index + 1}]";

            try
            {
                _logger.LogInformation($"{logPrefix} 📤 {report.VideoFile} → @{task.Account.AccountName}");

                // Проверяем что видео существует
                if (!File.Exists(task.VideoPath))
                {
                    throw new FileNotFoundException($"Видео не найдено: {task.VideoPath}");
                }

                // Создаём Instagram сервис для этого аккаунта
                var instagramService = _instagramFactory.Create(
                    task.Account.AccessToken,
                    task.Account.AccountId);

                // Получаем публичный URL
                var videoUrl = _videoFileService.GetPublicUrl(task.VideoPath, serverUrl, videosFolder);

                // Публикуем
                var result = await instagramService.PublishVideoAsync(videoUrl, caption);

                report.Success = result.Success;
                report.MediaId = result.MediaId;
                report.Error = result.ErrorMessage;

                if (result.Success)
                {
                    _logger.LogInformation($"{logPrefix} ✅ @{task.Account.AccountName} - MediaID: {result.MediaId}");

                    // УДАЛЯЕМ ВИДЕО после успешной публикации
                    if (deleteAfterPublish)
                    {
                        await DeleteVideoSafeAsync(task.VideoPath, logPrefix);
                    }
                }
                else
                {
                    _logger.LogWarning($"{logPrefix} ❌ @{task.Account.AccountName} - {result.ErrorMessage}");
                    // При ошибке НЕ удаляем видео — можно попробовать позже
                }
            }
            catch (Exception ex)
            {
                report.Success = false;
                report.Error = ex.Message;
                _logger.LogError($"{logPrefix} ❌ ИСКЛЮЧЕНИЕ @{task.Account.AccountName}: {ex.Message}");
                // При исключении НЕ удаляем видео
            }

            return report;
        }

        private async Task DeleteVideoSafeAsync(string videoPath, string logPrefix)
        {
            try
            {
                // Небольшая задержка чтобы Instagram успел скачать
                await Task.Delay(5000);

                if (File.Exists(videoPath))
                {
                    File.Delete(videoPath);
                    _logger.LogInformation($"{logPrefix} 🗑️ Удалено: {Path.GetFileName(videoPath)}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"{logPrefix} ⚠️ Не удалось удалить {Path.GetFileName(videoPath)}: {ex.Message}");
                // Не критично — продолжаем работу
            }
        }

        private async Task SaveReportAsync(List<PublishReport> reports)
        {
            try
            {
                var fileName = $"publish_report_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                var json = JsonConvert.SerializeObject(reports, Formatting.Indented);
                await File.WriteAllTextAsync(fileName, json);
                _logger.LogInformation($"📄 Отчёт сохранён: {fileName}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Ошибка сохранения отчёта: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Задача на публикацию — связка видео + аккаунт
    /// </summary>
    internal class PublishTask
    {
        public int Index { get; set; }
        public InstagramAccount Account { get; set; } = null!;
        public string VideoPath { get; set; } = string.Empty;
    }
}
using InstagramVideoPublisher.Models;
using InstagramVideoPublisher.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace InstagramVideoPublisher
{
    class TestProcessor
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("🧪 ТЕСТОВЫЙ РЕЖИМ - Обработка видео\n");

            // Настройка DI
            var services = new ServiceCollection();

            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            services.AddSingleton<IConfiguration>(configuration);
            services.Configure<VideoProcessingSettings>(configuration.GetSection("VideoProcessing"));
            services.AddLogging(builder => builder.AddConsole());
            services.AddSingleton<IVideoProcessingService, VideoProcessingService>();

            var serviceProvider = services.BuildServiceProvider();
            var videoProcessing = serviceProvider.GetRequiredService<IVideoProcessingService>();

            // Путь к тестовому файлу
            var testFile = @"C:\Users\Computer\Desktop\hm.mp4";

            if (!File.Exists(testFile))
            {
                Console.WriteLine($"❌ Файл не найден: {testFile}");
                Console.WriteLine("Положите тестовое видео с именем 'test_output.mp4' на рабочий стол");
                Console.ReadKey();
                return;
            }

            var originalSize = new FileInfo(testFile).Length / 1024.0 / 1024.0;
            Console.WriteLine($"Исходный файл: {testFile}");
            Console.WriteLine($"Размер: {originalSize:F2} MB\n");

            try
            {
                Console.WriteLine("⏳ Обрабатываем видео FFmpeg...\n");

                await videoProcessing.ProcessVideoAsync(testFile);

                var outputSize = new FileInfo(testFile).Length / 1024.0 / 1024.0;

                Console.WriteLine($"\n✅ Видео успешно обработано!");
                Console.WriteLine($"Файл: {testFile}");
                Console.WriteLine($"Размер: {outputSize:F2} MB");
                Console.WriteLine($"Изменение: {((outputSize - originalSize) / originalSize * 100):F1}%\n");

                Console.WriteLine("📊 Проверьте метаданные:");
                Console.WriteLine("1. Откройте MediaInfo");
                Console.WriteLine($"2. Загрузите файл: {testFile}");
                Console.WriteLine("3. View → JSON");
                Console.WriteLine("4. Проверьте что нет TikTok метаданных\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка: {ex.Message}");
                Console.WriteLine($"\nПодробности:\n{ex.StackTrace}");
            }

            Console.WriteLine("\nНажмите любую клавишу для выхода...");
            Console.ReadKey();
        }
    }
}
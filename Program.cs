using InstagramVideoPublisher.Models;
using InstagramVideoPublisher.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace InstagramVideoPublisher
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Красивый заголовок
            AnsiConsole.Write(
                new FigletText("TikTok -> Instagram")
                    .Centered()
                    .Color(Color.Purple));

            AnsiConsole.MarkupLine("[grey]Автоматическая публикация видео из TikTok в Instagram[/]\n");

            // Настройка DI контейнера
            var services = ConfigureServices();
            var serviceProvider = services.BuildServiceProvider();

            // Получаем сервисы
            var tiktokMonitor = serviceProvider.GetRequiredService<ITikTokMonitorService>();
            var fileService = serviceProvider.GetRequiredService<IFileService>();
            var instagramService = serviceProvider.GetRequiredService<IInstagramService>();
            var config = serviceProvider.GetRequiredService<IConfiguration>();

            // Проверка настроек
            var accessToken = config["Instagram:AccessToken"];
            var tiktokUsernames = config.GetSection("TikTokMonitor:TikTokUsernames").Get<List<string>>() ?? new List<string>();
            var checkInterval = int.Parse(config["TikTokMonitor:CheckIntervalMinutes"] ?? "5");
            var accountDelay = int.Parse(config["TikTokMonitor:AccountCheckDelaySeconds"] ?? "60");
            var testMode = bool.Parse(config["TikTokMonitor:TestMode"] ?? "false");

            if (string.IsNullOrEmpty(accessToken))
            {
                AnsiConsole.MarkupLine("[red]❌ ОШИБКА: Access Token не настроен![/]");
                AnsiConsole.MarkupLine("[yellow]Откройте appsettings.json и вставьте ваш Access Token[/]");
                Console.ReadKey();
                return;
            }

            if (tiktokUsernames.Count == 0)
            {
                AnsiConsole.MarkupLine("[red]❌ ОШИБКА: TikTok аккаунты не настроены![/]");
                AnsiConsole.MarkupLine("[yellow]Откройте appsettings.json и укажите TikTokUsernames[/]");
                Console.ReadKey();
                return;
            }

            // Показываем настройки
            var settingsTable = new Table();
            settingsTable.Border = TableBorder.Rounded;
            settingsTable.AddColumn("[yellow]Параметр[/]");
            settingsTable.AddColumn("[green]Значение[/]");
            settingsTable.AddRow("📱 TikTok аккаунты", string.Join(", ", tiktokUsernames.Select(u => $"@{u}")));
            settingsTable.AddRow("⏱️  Интервал проверки", $"{checkInterval} минут");
            settingsTable.AddRow("⏲️  Задержка между аккаунтами", $"{accountDelay} секунд");
            settingsTable.AddRow("📸 Instagram аккаунт", "@0_bimbimbambam_0");

            if (testMode)
            {
                settingsTable.AddRow("🧪 Режим", "[red]ТЕСТОВЫЙ (скачает последнее видео ОДИН РАЗ)[/]");
            }

            AnsiConsole.Write(settingsTable);
            AnsiConsole.WriteLine();

            if (testMode)
            {
                AnsiConsole.MarkupLine("[red]⚠️  ТЕСТОВЫЙ РЕЖИМ ВКЛЮЧЁН![/]");
                AnsiConsole.MarkupLine("[yellow]   При первом запуске скачает последнее видео с каждого аккаунта[/]");
                AnsiConsole.MarkupLine("[yellow]   После первого цикла будет работать как обычно (только новые видео)[/]");
                AnsiConsole.MarkupLine("[yellow]   Для обычного режима установите TestMode: false в appsettings.json[/]\n");
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]ℹ️  При первом запуске видео НЕ скачиваются - только запоминается последнее видео[/]");
                AnsiConsole.MarkupLine("[yellow]   Новые видео будут автоматически публиковаться в Instagram[/]\n");
            }

            // Главный цикл мониторинга
            int cycleCount = 0;

            while (true)
            {
                try
                {
                    cycleCount++;

                    var rule = new Rule($"[cyan]Цикл #{cycleCount} - {DateTime.Now:HH:mm:ss}[/]");
                    AnsiConsole.Write(rule);
                    AnsiConsole.WriteLine();

                    // Проверяем каждый TikTok аккаунт
                    for (int i = 0; i < tiktokUsernames.Count; i++)
                    {
                        var username = tiktokUsernames[i];

                        AnsiConsole.MarkupLine($"[grey]🔍 Проверяем @{username} ({i + 1}/{tiktokUsernames.Count})...[/]");

                        // Проверяем новое видео
                        var newVideo = await tiktokMonitor.CheckForNewVideo(username);

                        if (newVideo == null)
                        {
                            AnsiConsole.MarkupLine("[grey]   Нет новых видео[/]\n");

                            // Задержка перед следующим аккаунтом (но не после последнего)
                            if (i < tiktokUsernames.Count - 1)
                            {
                                AnsiConsole.MarkupLine($"[grey]⏳ Ждём {accountDelay} секунд перед проверкой следующего аккаунта...[/]\n");
                                await Task.Delay(TimeSpan.FromSeconds(accountDelay));
                            }
                            continue;
                        }

                        // Новое видео найдено!
                        AnsiConsole.MarkupLine($"[green]🎉 Найдено новое видео на @{username}![/]");
                        AnsiConsole.MarkupLine($"[grey]   Название:[/] {newVideo.Title}");
                        AnsiConsole.MarkupLine($"[grey]   ID:[/] {newVideo.Id}");
                        AnsiConsole.MarkupLine($"[grey]   Длина:[/] {newVideo.Duration} секунд\n");

                        await AnsiConsole.Status()
                            .Spinner(Spinner.Known.Dots)
                            .SpinnerStyle(Style.Parse("green bold"))
                            .StartAsync("Обрабатываем видео...", async ctx =>
                            {
                                // Шаг 1: Скачиваем видео
                                ctx.Status("📥 Скачиваем видео с TikTok...");
                                string localPath = await tiktokMonitor.DownloadVideo(newVideo);
                                AnsiConsole.MarkupLine($"[green]✓[/] Видео скачано: [grey]{Path.GetFileName(localPath)}[/]");
                                await Task.Delay(1000);

                                // Шаг 2: Загружаем на Cloudinary
                                ctx.Status("☁️  Загружаем на Cloudinary...");
                                string cloudinaryUrl = await fileService.UploadVideoAsync(localPath);
                                AnsiConsole.MarkupLine($"[green]✓[/] Загружено на Cloudinary");
                                await Task.Delay(1000);

                                // Шаг 3: Публикуем в Instagram
                                ctx.Status("📸 Публикуем в Instagram...");

                                var caption = GenerateCaption(newVideo);

                                var result = await instagramService.PublishVideoAsync(new VideoPublishInfo
                                {
                                    FilePath = localPath,
                                    VideoUrl = cloudinaryUrl,
                                    Caption = caption
                                });

                                AnsiConsole.WriteLine();

                                if (result.Success)
                                {
                                    var successPanel = new Panel(
                                        new Markup($"[green]✓ Видео успешно опубликовано в Instagram![/]\n\n" +
                                                 $"[grey]Media ID:[/] [yellow]{result.MediaId}[/]\n" +
                                                 $"[grey]TikTok:[/] [cyan]@{username}[/]\n" +
                                                 $"[grey]Video ID:[/] [cyan]{newVideo.Id}[/]\n" +
                                                 $"[grey]Caption:[/] {caption}"))
                                    {
                                        Border = BoxBorder.Double,
                                        BorderStyle = new Style(Color.Green)
                                    };
                                    AnsiConsole.Write(successPanel);
                                }
                                else
                                {
                                    var errorPanel = new Panel(
                                        new Markup($"[red]❌ Ошибка публикации в Instagram[/]\n\n" +
                                                 $"[grey]{result.ErrorMessage}[/]"))
                                    {
                                        Border = BoxBorder.Double,
                                        BorderStyle = new Style(Color.Red)
                                    };
                                    AnsiConsole.Write(errorPanel);
                                }

                                // Удаляем локальный файл после публикации
                                try
                                {
                                    File.Delete(localPath);
                                    AnsiConsole.MarkupLine($"\n[grey]🗑️  Удалён локальный файл[/]");
                                }
                                catch { }
                            });

                        AnsiConsole.WriteLine();

                        // Задержка перед следующим аккаунтом (но не после последнего)
                        if (i < tiktokUsernames.Count - 1)
                        {
                            AnsiConsole.MarkupLine($"[grey]⏳ Ждём {accountDelay} секунд перед проверкой следующего аккаунта...[/]\n");
                            await Task.Delay(TimeSpan.FromSeconds(accountDelay));
                        }
                    }

                    AnsiConsole.MarkupLine($"[grey]💤 Следующая проверка всех аккаунтов через {checkInterval} минут...[/]\n");
                    await Task.Delay(TimeSpan.FromMinutes(checkInterval));
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]❌ Ошибка:[/] {ex.Message}");
                    AnsiConsole.MarkupLine($"\n[grey]⏱️  Повторная попытка через 1 минуту...[/]\n");
                    await Task.Delay(TimeSpan.FromMinutes(1));
                }
            }
        }

        static string GenerateCaption(TikTokVideo video)
        {
            // Генерируем caption для Instagram
            var caption = video.Title;

            // Добавляем хештеги если их нет
            if (!caption.Contains("#"))
            {
                caption += " #glavstroy #tiktok #reels";
            }

            return caption;
        }

        static IServiceCollection ConfigureServices()
        {
            var services = new ServiceCollection();

            // Конфигурация
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            services.AddSingleton<IConfiguration>(configuration);

            // Настройки
            services.Configure<InstagramSettings>(configuration.GetSection("Instagram"));
            services.Configure<CloudinarySettings>(configuration.GetSection("Cloudinary"));
            services.Configure<TikTokMonitorSettings>(configuration.GetSection("TikTokMonitor"));

            // Логирование
            services.AddLogging(builder =>
            {
                builder.AddConfiguration(configuration.GetSection("Logging"));
                builder.AddConsole();
            });

            // HTTP Client
            services.AddHttpClient<IInstagramService, InstagramService>();

            // Сервисы
            services.AddSingleton<ITikTokMonitorService, TikTokMonitorService>();
            services.AddSingleton<IFileService, FileService>();
            services.AddSingleton<IInstagramService, InstagramService>();

            return services;
        }
    }
}
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
            var videoProcessing = serviceProvider.GetRequiredService<IVideoProcessingService>();
            var config = serviceProvider.GetRequiredService<IConfiguration>();

            // Загружаем настройки
            var appSettings = new AppSettings();
            config.Bind(appSettings);

            var instagramAccounts = appSettings.InstagramAccounts;
            var checkInterval = appSettings.CheckIntervalMinutes;
            var accountDelay = appSettings.AccountCheckDelayMinutes;

            // Проверка настроек
            if (instagramAccounts.Count == 0)
            {
                AnsiConsole.MarkupLine("[red]❌ ОШИБКА: Instagram аккаунты не настроены![/]");
                AnsiConsole.MarkupLine("[yellow]Откройте appsettings.json и настройте InstagramAccounts[/]");
                Console.ReadKey();
                return;
            }

            // Показываем настройки
            var settingsTable = new Table();
            settingsTable.Border = TableBorder.Rounded;
            settingsTable.AddColumn("[yellow]Instagram аккаунт[/]");
            settingsTable.AddColumn("[green]TikTok источники[/]");
            settingsTable.AddColumn("[cyan]Статус[/]");

            foreach (var account in instagramAccounts)
            {
                var status = string.IsNullOrEmpty(account.AccessToken) ? "[red]❌ Нет токена[/]" : "[green]✓ Активен[/]";
                var tiktoks = string.Join(", ", account.TikTokUsernames.Select(u => $"@{u}"));
                settingsTable.AddRow($"@{account.AccountName}", tiktoks, status);
            }

            AnsiConsole.Write(settingsTable);
            AnsiConsole.WriteLine();

            var configTable = new Table();
            configTable.Border = TableBorder.Rounded;
            configTable.AddColumn("[yellow]Параметр[/]");
            configTable.AddColumn("[green]Значение[/]");
            configTable.AddRow("⏱️  Интервал проверки", $"{checkInterval} минут");
            configTable.AddRow("⏲️  Задержка между аккаунтами", $"{accountDelay} минута");
            configTable.AddRow("📹 FFmpeg обработка", "[green]✓ Включена[/]");
            configTable.AddRow("🔊 Увеличение громкости", "+10%");
            configTable.AddRow("🎨 Разрешение", "720x1280 (HD вертикальное)");

            AnsiConsole.Write(configTable);
            AnsiConsole.WriteLine();

            AnsiConsole.MarkupLine("[yellow]ℹ️  При первом запуске видео НЕ скачиваются - только запоминается последнее видео[/]");
            AnsiConsole.MarkupLine("[yellow]   Новые видео будут автоматически публиковаться в Instagram[/]\n");

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

                    // Проходим по каждому Instagram аккаунту
                    for (int accountIndex = 0; accountIndex < instagramAccounts.Count; accountIndex++)
                    {
                        var instagramAccount = instagramAccounts[accountIndex];

                        // Пропускаем если нет токена
                        if (string.IsNullOrEmpty(instagramAccount.AccessToken))
                        {
                            AnsiConsole.MarkupLine($"[red]⚠️  @{instagramAccount.AccountName} - нет токена, пропускаем[/]\n");
                            continue;
                        }

                        AnsiConsole.MarkupLine($"[blue]═══ Instagram: @{instagramAccount.AccountName} ({accountIndex + 1}/{instagramAccounts.Count}) ═══[/]\n");

                        // Создаём Instagram сервис для этого аккаунта
                        var instagramService = CreateInstagramService(
                            serviceProvider,
                            instagramAccount.AccessToken,
                            instagramAccount.AccountId);

                        // Проверяем каждый TikTok аккаунт
                        foreach (var tiktokUsername in instagramAccount.TikTokUsernames)
                        {
                            AnsiConsole.MarkupLine($"[grey]🔍 Проверяем @{tiktokUsername}...[/]");

                            // Проверяем новое видео
                            var newVideo = await tiktokMonitor.CheckForNewVideo(tiktokUsername);

                            if (newVideo == null)
                            {
                                AnsiConsole.MarkupLine("[grey]   Нет новых видео[/]\n");
                                continue;
                            }

                            // Новое видео найдено!
                            AnsiConsole.MarkupLine($"[green]🎉 Найдено новое видео на @{tiktokUsername}![/]");
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

                                    // Шаг 2: Обрабатываем FFmpeg
                                    ctx.Status("🎬 Обрабатываем видео (FFmpeg)...");
                                    await videoProcessing.ProcessVideoAsync(localPath);
                                    AnsiConsole.MarkupLine($"[green]✓[/] Видео обработано (уникализация, метаданные очищены)");
                                    await Task.Delay(1000);

                                    // Шаг 3: Генерируем публичный URL
                                    // TODO: Здесь будет Nginx URL после настройки на сервере
                                    // Пока используем заглушку
                                    var publicUrl = $"http://your-server-ip/videos/{Path.GetFileName(localPath)}";
                                    AnsiConsole.MarkupLine($"[green]✓[/] URL для Instagram: [grey]{publicUrl}[/]");

                                    // Шаг 4: Публикуем в Instagram
                                    ctx.Status($"📸 Публикуем в Instagram (@{instagramAccount.AccountName})...");

                                    var caption = GenerateCaption(newVideo, tiktokUsername);

                                    var result = await instagramService.PublishVideoAsync(new VideoPublishInfo
                                    {
                                        FilePath = localPath,
                                        VideoUrl = publicUrl,
                                        Caption = caption
                                    });

                                    AnsiConsole.WriteLine();

                                    if (result.Success)
                                    {
                                        var successPanel = new Panel(
                                            new Markup($"[green]✓ Видео успешно опубликовано![/]\n\n" +
                                                     $"[grey]Instagram:[/] [cyan]@{instagramAccount.AccountName}[/]\n" +
                                                     $"[grey]Media ID:[/] [yellow]{result.MediaId}[/]\n" +
                                                     $"[grey]TikTok:[/] [cyan]@{tiktokUsername}[/]\n" +
                                                     $"[grey]Video ID:[/] [cyan]{newVideo.Id}[/]"))
                                        {
                                            Border = BoxBorder.Double,
                                            BorderStyle = new Style(Color.Green)
                                        };
                                        AnsiConsole.Write(successPanel);
                                    }
                                    else
                                    {
                                        var errorPanel = new Panel(
                                            new Markup($"[red]❌ Ошибка публикации[/]\n\n" +
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
                        }

                        // Задержка перед следующим Instagram аккаунтом (но не после последнего)
                        if (accountIndex < instagramAccounts.Count - 1)
                        {
                            AnsiConsole.MarkupLine($"[grey]⏳ Ждём {accountDelay} минуту перед следующим аккаунтом...[/]\n");
                            await Task.Delay(TimeSpan.FromMinutes(accountDelay));
                        }
                    }

                    AnsiConsole.MarkupLine($"[grey]💤 Следующая проверка через {checkInterval} минут...[/]\n");
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

        static string GenerateCaption(TikTokVideo video, string tiktokUsername)
        {
            var caption = video.Title;

            // Добавляем хештеги если их нет
            if (!caption.Contains("#"))
            {
                caption += "\n\n#reels #viral #trending";
            }

            return caption;
        }

        static IInstagramService CreateInstagramService(
            IServiceProvider serviceProvider,
            string accessToken,
            string accountId)
        {
            var logger = serviceProvider.GetRequiredService<ILogger<InstagramService>>();
            var httpClient = serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient();

            var settings = Microsoft.Extensions.Options.Options.Create(new InstagramSettings
            {
                AccessToken = accessToken,
                AccountId = accountId
            });

            return new InstagramService(settings, logger, httpClient);
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
            services.Configure<TikTokMonitorSettings>(configuration.GetSection("TikTokMonitor"));
            services.Configure<VideoProcessingSettings>(configuration.GetSection("VideoProcessing"));

            // Логирование
            services.AddLogging(builder =>
            {
                builder.AddConfiguration(configuration.GetSection("Logging"));
                builder.AddConsole();
            });

            // HTTP Client
            services.AddHttpClient();

            // Сервисы
            services.AddSingleton<ITikTokMonitorService, TikTokMonitorService>();
            services.AddSingleton<IVideoProcessingService, VideoProcessingService>();

            return services;
        }
    }
}
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
            AnsiConsole.Write(
                new FigletText("TikTok -> Instagram")
                    .Centered()
                    .Color(Color.Purple));

            AnsiConsole.MarkupLine("[grey]Автоматическая публикация видео из TikTok в Instagram[/]\n");

            var services = ConfigureServices();
            var serviceProvider = services.BuildServiceProvider();

            var tiktokMonitor = serviceProvider.GetRequiredService<ITikTokMonitorService>();
            var config = serviceProvider.GetRequiredService<IConfiguration>();

            var appSettings = new AppSettings();
            config.Bind(appSettings);

            var instagramAccounts = appSettings.InstagramAccounts;
            var checkInterval = appSettings.CheckIntervalMinutes;
            var tiktokDelay = appSettings.TikTokCheckDelaySeconds;
            var serverUrl = config["Server:PublicUrl"] ?? "http://localhost:8080";

            if (instagramAccounts.Count == 0)
            {
                AnsiConsole.MarkupLine("[red]❌ ОШИБКА: Instagram аккаунты не настроены![/]");
                AnsiConsole.MarkupLine("[yellow]Откройте appsettings.json и настройте InstagramAccounts[/]");
                Console.ReadKey();
                return;
            }

            // Показываем настройки
            DisplaySettings(instagramAccounts, checkInterval, tiktokDelay, serverUrl);

            int cycleCount = 0;

            while (true)
            {
                try
                {
                    cycleCount++;

                    var rule = new Rule($"[cyan]Цикл #{cycleCount} - {DateTime.Now:HH:mm:ss}[/]");
                    AnsiConsole.Write(rule);
                    AnsiConsole.WriteLine();

                    // Находим максимальное количество TikTok аккаунтов
                    int maxTikTokAccounts = instagramAccounts.Max(acc => acc.TikTokUsernames.Count);

                    AnsiConsole.MarkupLine($"[grey]🔄 Алгоритм: проходим по {maxTikTokAccounts} позициям в массивах[/]\n");

                    // АЛГОРИТМ "КАРУСЕЛЬ" - проходим по позициям в массивах
                    for (int tiktokIndex = 0; tiktokIndex < maxTikTokAccounts; tiktokIndex++)
                    {
                        AnsiConsole.MarkupLine($"[yellow]═══ Позиция #{tiktokIndex + 1} в массивах TikTok ═══[/]\n");

                        // Проходим по всем Instagram аккаунтам для текущей позиции
                        for (int accountIndex = 0; accountIndex < instagramAccounts.Count; accountIndex++)
                        {
                            var instagramAccount = instagramAccounts[accountIndex];

                            // Проверяем есть ли токен
                            if (string.IsNullOrEmpty(instagramAccount.AccessToken))
                            {
                                AnsiConsole.MarkupLine($"[red]⚠️  @{instagramAccount.AccountName} - нет токена, пропускаем[/]\n");
                                continue;
                            }

                            // Проверяем есть ли TikTok аккаунт на этой позиции
                            if (tiktokIndex >= instagramAccount.TikTokUsernames.Count)
                            {
                                AnsiConsole.MarkupLine($"[grey]⏭️  @{instagramAccount.AccountName} - нет TikTok на позиции {tiktokIndex + 1}, пропускаем[/]");
                                continue;
                            }

                            var tiktokUsername = instagramAccount.TikTokUsernames[tiktokIndex];
                            var historyKey = $"{instagramAccount.AccountName}:{tiktokUsername}";

                            AnsiConsole.MarkupLine($"[blue]📍 Instagram: @{instagramAccount.AccountName} → TikTok: @{tiktokUsername}[/]");

                            try
                            {
                                // Проверяем новое видео
                                var newVideo = await tiktokMonitor.CheckForNewVideo(tiktokUsername, historyKey);

                                if (newVideo == null)
                                {
                                    AnsiConsole.MarkupLine("[grey]   Нет новых видео[/]\n");

                                    // Задержка перед следующей проверкой (кроме последней)
                                    if (accountIndex < instagramAccounts.Count - 1 || tiktokIndex < maxTikTokAccounts - 1)
                                    {
                                        AnsiConsole.MarkupLine($"[grey]   ⏳ Задержка {tiktokDelay}с перед следующей проверкой...[/]");
                                        await Task.Delay(TimeSpan.FromSeconds(tiktokDelay));
                                    }

                                    continue;
                                }

                                // НАШЛИ НОВОЕ ВИДЕО!
                                AnsiConsole.MarkupLine($"[green]🎉 Найдено новое видео на @{tiktokUsername}![/]");
                                AnsiConsole.MarkupLine($"[grey]   Название:[/] {newVideo.Title}");
                                AnsiConsole.MarkupLine($"[grey]   ID:[/] {newVideo.Id}");
                                AnsiConsole.MarkupLine($"[grey]   Timestamp:[/] {newVideo.Timestamp}");
                                AnsiConsole.MarkupLine($"[grey]   Длина:[/] {newVideo.Duration} секунд\n");

                                // Создаем Instagram сервис для этого аккаунта
                                var instagramService = CreateInstagramService(
                                    serviceProvider,
                                    instagramAccount.AccessToken,
                                    instagramAccount.AccountId);

                                // Скачиваем и публикуем видео (без обработки)
                                await DownloadAndPublishVideo(
                                    newVideo,
                                    tiktokUsername,
                                    historyKey,
                                    instagramAccount,
                                    tiktokMonitor,
                                    instagramService,
                                    serverUrl);

                            }
                            catch (Exception ex)
                            {
                                AnsiConsole.MarkupLine($"[red]❌ Ошибка обработки @{tiktokUsername}: {ex.Message}[/]");
                                AnsiConsole.MarkupLine("[yellow]   Продолжаем проверку других аккаунтов...[/]\n");
                            }

                            // Задержка перед следующей проверкой (кроме последней)
                            if (accountIndex < instagramAccounts.Count - 1 || tiktokIndex < maxTikTokAccounts - 1)
                            {
                                AnsiConsole.MarkupLine($"[grey]   ⏳ Задержка {tiktokDelay}с перед следующей проверкой...[/]");
                                await Task.Delay(TimeSpan.FromSeconds(tiktokDelay));
                            }
                        }

                        AnsiConsole.WriteLine();
                    }

                    AnsiConsole.MarkupLine($"[grey]💤 Цикл завершен. Следующая проверка через {checkInterval} минут...[/]\n");
                    await Task.Delay(TimeSpan.FromMinutes(checkInterval));
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]❌ Критическая ошибка в цикле:[/]");
                    AnsiConsole.WriteException(ex);
                    AnsiConsole.MarkupLine($"\n[grey]⏱️  Повторная попытка через 1 минуту...[/]\n");
                    await Task.Delay(TimeSpan.FromMinutes(1));
                }
            }
        }

        static async Task DownloadAndPublishVideo(
            TikTokVideo newVideo,
            string tiktokUsername,
            string historyKey,
            InstagramAccountSettings instagramAccount,
            ITikTokMonitorService tiktokMonitor,
            IInstagramService instagramService,
            string serverUrl)
        {
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("green bold"))
                .StartAsync("Скачиваем и публикуем видео...", async ctx =>
                {
                    string? localPath = null;
                    var instagramAccountName = instagramAccount.AccountName;

                    try
                    {
                        ctx.Status("📥 Скачиваем видео с TikTok...");
                        localPath = await tiktokMonitor.DownloadVideo(newVideo);

                        var fileSize = new FileInfo(localPath).Length / 1024.0 / 1024.0;
                        AnsiConsole.MarkupLine($"[green]✓[/] Видео скачано: [grey]{Path.GetFileName(localPath)}[/]");
                        AnsiConsole.MarkupLine($"[grey]   Размер:[/] {fileSize:F2} MB");

                        var fileName = Path.GetFileName(localPath);
                        var publicUrl = $"{serverUrl}/videos/{fileName}";

                        AnsiConsole.MarkupLine($"[green]✓[/] URL для Instagram: [grey]{publicUrl}[/]");

                        ctx.Status($"📸 Публикуем в Instagram (@{instagramAccountName})...");

                        var caption = GenerateCaption(newVideo, tiktokUsername, instagramAccount.CustomCaption);

                        // ОБЛОЖКА (опционально): если у аккаунта задан файл обложки в appsettings,
                        // строим публичный URL — её раздаёт тот же nginx, что и видео.
                        // Если CoverImage не задан, coverUrl остаётся null и публикация идёт как раньше.
                        string? coverUrl = null;
                        if (!string.IsNullOrWhiteSpace(instagramAccount.CoverImage))
                        {
                            coverUrl = $"{serverUrl}/videos/covers/{instagramAccount.CoverImage}";
                            AnsiConsole.MarkupLine($"[grey]   🖼️  Обложка:[/] {coverUrl}");
                        }

                        var result = await instagramService.PublishVideoAsync(new VideoPublishInfo
                        {
                            FilePath = localPath,
                            VideoUrl = publicUrl,
                            Caption = caption,
                            CoverUrl = coverUrl,
                            ThumbOffsetMs = instagramAccount.ThumbOffsetMs
                        });

                        AnsiConsole.WriteLine();

                        if (result.Success)
                        {
                            tiktokMonitor.MarkVideoAsProcessed(historyKey, newVideo.Id, newVideo.Timestamp);

                            var successPanel = new Panel(
                                new Markup($"[green]✓ Видео успешно опубликовано![/]\n\n" +
                                         $"[grey]Instagram:[/] [cyan]@{instagramAccountName}[/]\n" +
                                         $"[grey]Media ID:[/] [yellow]{result.MediaId}[/]\n" +
                                         $"[grey]TikTok:[/] [cyan]@{tiktokUsername}[/]\n" +
                                         $"[grey]Video ID:[/] [cyan]{newVideo.Id}[/]\n" +
                                         $"[grey]Timestamp:[/] [cyan]{newVideo.Timestamp}[/]"))
                            {
                                Border = BoxBorder.Double,
                                BorderStyle = new Style(Color.Green)
                            };
                            AnsiConsole.Write(successPanel);

                            AnsiConsole.MarkupLine("\n[grey]⏳ Ждём 30 секунд чтобы Instagram скачал видео...[/]");
                            await Task.Delay(TimeSpan.FromSeconds(30));
                        }
                        else
                        {
                            var errorPanel = new Panel(
                                new Markup($"[red]❌ Ошибка публикации[/]\n\n" +
                                         $"[grey]{result.ErrorMessage}[/]\n\n" +
                                         $"[yellow]ID НЕ сохранён - попробуем снова в следующем цикле[/]"))
                            {
                                Border = BoxBorder.Double,
                                BorderStyle = new Style(Color.Red)
                            };
                            AnsiConsole.Write(errorPanel);
                        }

                        // Удаляем локальный файл
                        if (localPath != null && File.Exists(localPath))
                        {
                            try
                            {
                                File.Delete(localPath);
                                AnsiConsole.MarkupLine($"\n[grey]🗑️  Удалён локальный файл: {Path.GetFileName(localPath)}[/]");
                            }
                            catch (Exception ex)
                            {
                                AnsiConsole.MarkupLine($"[yellow]⚠️  Не удалось удалить файл: {ex.Message}[/]");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        AnsiConsole.WriteLine();
                        AnsiConsole.MarkupLine($"[red]❌ Ошибка: {ex.Message}[/]");
                        AnsiConsole.MarkupLine("[yellow]ID НЕ сохранён - попробуем снова в следующем цикле[/]");

                        if (localPath != null && File.Exists(localPath))
                        {
                            try
                            {
                                File.Delete(localPath);
                            }
                            catch { }
                        }

                        throw;
                    }
                });
        }

        static void DisplaySettings(
            List<InstagramAccountSettings> instagramAccounts,
            int checkInterval,
            int tiktokDelay,
            string serverUrl)
        {
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
            configTable.AddRow("⏱️  Интервал проверки (полный цикл)", $"{checkInterval} минут");
            configTable.AddRow("⏳ Задержка между проверками", $"{tiktokDelay} секунд");
            configTable.AddRow("🌐 Сервер", serverUrl);
            configTable.AddRow("💾 История", "Последние 5 видео с timestamp");

            AnsiConsole.Write(configTable);
            AnsiConsole.WriteLine();

            AnsiConsole.MarkupLine("[yellow]ℹ️  Алгоритм проверки: \"карусель\" - проходим по позициям в массивах[/]");
            AnsiConsole.MarkupLine("[yellow]   Пример: Instagram#1→TikTok[[0]], Instagram#2→TikTok[[0]], ..., Instagram#1→TikTok[[1]], ...[/]");
            AnsiConsole.MarkupLine("[yellow]   Защита: история последних 5 видео с timestamp предотвращает дубликаты[/]\n");
        }

        static string GenerateCaption(TikTokVideo video, string tiktokUsername, string? customCaption)
        {
            if (!string.IsNullOrWhiteSpace(customCaption))
                return customCaption;

            var caption = video.Title;

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

            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true)
                .Build();

            services.AddSingleton<IConfiguration>(configuration);

            services.Configure<TikTokMonitorSettings>(configuration.GetSection("TikTokMonitor"));

            services.AddLogging(builder =>
            {
                builder.AddConfiguration(configuration.GetSection("Logging"));
                builder.AddConsole();
            });

            services.AddHttpClient();

            services.AddSingleton<ITikTokMonitorService, TikTokMonitorService>();

            return services;
        }
    }
}
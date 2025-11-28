using InstagramBulkPublisher.Models;
using InstagramBulkPublisher.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace InstagramBulkPublisher
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Красивый заголовок
            AnsiConsole.Write(
                new FigletText("Instagram Bulk")
                    .Centered()
                    .Color(Color.Purple));
            AnsiConsole.MarkupLine("[grey]Массовая публикация видео в Instagram[/]\n");

            // Настраиваем DI
            var services = ConfigureServices();
            var serviceProvider = services.BuildServiceProvider();

            var accountService = serviceProvider.GetRequiredService<IAccountService>();
            var videoFileService = serviceProvider.GetRequiredService<IVideoFileService>();
            var bulkPublishService = serviceProvider.GetRequiredService<IBulkPublishService>();
            var config = serviceProvider.GetRequiredService<IConfiguration>();

            // Загружаем настройки
            var settings = new AppSettings();
            config.GetSection("App").Bind(settings);

            // Показываем меню
            while (true)
            {
                var choice = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("[yellow]Выберите действие:[/]")
                        .AddChoices(new[]
                        {
                            "1. Импортировать аккаунты из CSV",
                            "2. Показать загруженные аккаунты",
                            "3. Показать доступные видео",
                            "4. Запустить публикацию",
                            "5. Настройки",
                            "0. Выход"
                        }));

                AnsiConsole.WriteLine();

                switch (choice)
                {
                    case "1. Импортировать аккаунты из CSV":
                        await ImportAccountsFromCsv(accountService, settings);
                        break;

                    case "2. Показать загруженные аккаунты":
                        await ShowAccounts(accountService, settings);
                        break;

                    case "3. Показать доступные видео":
                        ShowVideos(videoFileService, settings);
                        break;

                    case "4. Запустить публикацию":
                        await RunPublishing(accountService, videoFileService, bulkPublishService, settings);
                        break;

                    case "5. Настройки":
                        ShowSettings(settings);
                        break;

                    case "0. Выход":
                        AnsiConsole.MarkupLine("[green]До свидания![/]");
                        return;
                }

                AnsiConsole.WriteLine();
            }
        }

        static async Task ImportAccountsFromCsv(IAccountService accountService, AppSettings settings)
        {
            try
            {
                // Спрашиваем путь к CSV если не задан или хотят изменить
                var csvPath = AnsiConsole.Ask<string>(
                    $"[cyan]Путь к CSV файлу[/] (Enter = {settings.AccountsCsvPath}):",
                    settings.AccountsCsvPath);

                if (!File.Exists(csvPath))
                {
                    AnsiConsole.MarkupLine($"[red]❌ Файл не найден: {csvPath}[/]");
                    AnsiConsole.MarkupLine("[yellow]Создайте CSV файл в формате: AccountName,AccountId,AccessToken[/]");
                    return;
                }

                await AnsiConsole.Status()
                    .StartAsync("Импортируем аккаунты...", async ctx =>
                    {
                        var accounts = await accountService.LoadAccountsFromCsvAsync(csvPath, settings.AccountsJsonPath);

                        AnsiConsole.MarkupLine($"[green]✅ Импортировано {accounts.Count} аккаунтов[/]");
                        AnsiConsole.MarkupLine($"[grey]Сохранено в: {settings.AccountsJsonPath}[/]");
                    });
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]❌ Ошибка: {ex.Message}[/]");
            }
        }

        static async Task ShowAccounts(IAccountService accountService, AppSettings settings)
        {
            try
            {
                if (!accountService.JsonExists(settings.AccountsJsonPath))
                {
                    AnsiConsole.MarkupLine("[yellow]⚠️ Аккаунты не загружены. Сначала импортируйте CSV.[/]");
                    return;
                }

                var accounts = await accountService.LoadAccountsFromJsonAsync(settings.AccountsJsonPath);

                var table = new Table();
                table.Border = TableBorder.Rounded;
                table.AddColumn("[yellow]#[/]");
                table.AddColumn("[cyan]Имя аккаунта[/]");
                table.AddColumn("[green]Account ID[/]");
                table.AddColumn("[grey]Token (первые 20 символов)[/]");

                for (int i = 0; i < accounts.Count; i++)
                {
                    var acc = accounts[i];
                    var tokenPreview = acc.AccessToken.Length > 20
                        ? acc.AccessToken.Substring(0, 20) + "..."
                        : acc.AccessToken;

                    table.AddRow(
                        (i + 1).ToString(),
                        acc.AccountName,
                        acc.AccountId,
                        tokenPreview
                    );
                }

                AnsiConsole.Write(table);
                AnsiConsole.MarkupLine($"\n[grey]Всего аккаунтов: {accounts.Count}[/]");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]❌ Ошибка: {ex.Message}[/]");
            }
        }

        static void ShowVideos(IVideoFileService videoFileService, AppSettings settings)
        {
            try
            {
                var videos = videoFileService.GetAllVideos(settings.VideosFolder);

                AnsiConsole.MarkupLine($"[cyan]Папка:[/] {settings.VideosFolder}");
                AnsiConsole.MarkupLine($"[green]Найдено видео:[/] {videos.Count}\n");

                if (videos.Count == 0)
                {
                    AnsiConsole.MarkupLine("[yellow]⚠️ Видео не найдены[/]");
                    return;
                }

                // Показываем первые 20
                var showCount = Math.Min(20, videos.Count);
                for (int i = 0; i < showCount; i++)
                {
                    var fileName = Path.GetFileName(videos[i]);
                    var fileSize = new FileInfo(videos[i]).Length / 1024.0 / 1024.0;
                    AnsiConsole.MarkupLine($"  [grey]{i + 1}.[/] {fileName} [grey]({fileSize:F2} MB)[/]");
                }

                if (videos.Count > 20)
                {
                    AnsiConsole.MarkupLine($"\n  [grey]... и ещё {videos.Count - 20} видео[/]");
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]❌ Ошибка: {ex.Message}[/]");
            }
        }

        static async Task RunPublishing(
            IAccountService accountService,
            IVideoFileService videoFileService,
            IBulkPublishService bulkPublishService,
            AppSettings settings)
        {
            try
            {
                // Проверяем аккаунты
                if (!accountService.JsonExists(settings.AccountsJsonPath))
                {
                    AnsiConsole.MarkupLine("[red]❌ Сначала импортируйте аккаунты из CSV![/]");
                    return;
                }

                var accounts = await accountService.LoadAccountsFromJsonAsync(settings.AccountsJsonPath);
                if (accounts.Count == 0)
                {
                    AnsiConsole.MarkupLine("[red]❌ Нет загруженных аккаунтов![/]");
                    return;
                }

                // Проверяем видео
                var videos = videoFileService.GetAllVideos(settings.VideosFolder);
                if (videos.Count == 0)
                {
                    AnsiConsole.MarkupLine("[red]❌ Нет видео в папке![/]");
                    return;
                }

                var totalTasks = Math.Min(accounts.Count, videos.Count);

                // Показываем план
                var planTable = new Table();
                planTable.Border = TableBorder.Rounded;
                planTable.AddColumn("[yellow]Параметр[/]");
                planTable.AddColumn("[green]Значение[/]");
                planTable.AddRow("Аккаунтов", accounts.Count.ToString());
                planTable.AddRow("Видео", videos.Count.ToString());
                planTable.AddRow("Будет опубликовано", totalTasks.ToString());
                planTable.AddRow("Параллельных потоков", settings.ParallelPublishCount.ToString());
                planTable.AddRow("Задержка между пачками", $"{settings.DelayBetweenPublishSeconds} сек");
                planTable.AddRow("Удалять после публикации", settings.DeleteAfterPublish ? "[green]Да[/]" : "[red]Нет[/]");
                planTable.AddRow("Caption", settings.DefaultCaption);
                planTable.AddRow("Сервер", settings.ServerPublicUrl);

                AnsiConsole.Write(planTable);
                AnsiConsole.WriteLine();

                // Подтверждение
                var confirm = AnsiConsole.Confirm("[yellow]Запустить публикацию?[/]");
                if (!confirm)
                {
                    AnsiConsole.MarkupLine("[grey]Отменено[/]");
                    return;
                }

                AnsiConsole.WriteLine();

                // Запускаем
                var reports = await bulkPublishService.PublishAllAsync(
                    accounts,
                    videos,
                    settings.ServerPublicUrl,
                    settings.VideosFolder,
                    settings.DefaultCaption,
                    settings.ParallelPublishCount,
                    settings.DelayBetweenPublishSeconds,
                    settings.DeleteAfterPublish);

                // Показываем результаты
                AnsiConsole.WriteLine();
                var successCount = reports.Count(r => r.Success);
                var failCount = reports.Count(r => !r.Success);

                var resultPanel = new Panel(
                    new Markup($"[green]✅ Успешно: {successCount}[/]\n" +
                              $"[red]❌ Ошибок: {failCount}[/]\n\n" +
                              $"[grey]Отчёт сохранён в текущую папку[/]"))
                {
                    Border = BoxBorder.Double,
                    BorderStyle = new Style(successCount > failCount ? Color.Green : Color.Red)
                };
                AnsiConsole.Write(resultPanel);
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]❌ Критическая ошибка: {ex.Message}[/]");
            }
        }

        static void ShowSettings(AppSettings settings)
        {
            var table = new Table();
            table.Border = TableBorder.Rounded;
            table.AddColumn("[yellow]Параметр[/]");
            table.AddColumn("[green]Значение[/]");

            table.AddRow("CSV файл аккаунтов", settings.AccountsCsvPath);
            table.AddRow("JSON файл аккаунтов", settings.AccountsJsonPath);
            table.AddRow("Папка с видео", settings.VideosFolder);
            table.AddRow("Публичный URL сервера", settings.ServerPublicUrl);
            table.AddRow("Параллельных публикаций", settings.ParallelPublishCount.ToString());
            table.AddRow("Задержка между пачками", $"{settings.DelayBetweenPublishSeconds} сек");
            table.AddRow("Caption по умолчанию", settings.DefaultCaption);
            table.AddRow("Удалять видео после публикации", settings.DeleteAfterPublish ? "[green]Да[/]" : "[red]Нет[/]");

            AnsiConsole.Write(table);
            AnsiConsole.MarkupLine("\n[grey]Для изменения отредактируйте appsettings.json[/]");
        }

        static IServiceCollection ConfigureServices()
        {
            var services = new ServiceCollection();

            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            services.AddSingleton<IConfiguration>(configuration);

            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Information);
            });

            services.AddHttpClient();

            // Регистрируем сервисы
            services.AddSingleton<IAccountService, AccountService>();
            services.AddSingleton<IVideoFileService, VideoFileService>();
            services.AddSingleton<IInstagramServiceFactory, InstagramServiceFactory>();
            services.AddSingleton<IBulkPublishService, BulkPublishService>();

            return services;
        }
    }
}
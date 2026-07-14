using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MediaSorter.Models;
using MediaSorter.Services.Logging;
using MediaSorter.Services.Metadata;
using MediaSorter.Services.Organization;
using MediaSorter.Services.Scanning;
using MediaSorter.Services.Settings;
using MediaSorter.Services.System;
using MediaSorter.ViewModels;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace MediaSorter;

public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            SetupGlobalExceptionHandlers();

            _host = CreateHostBuilder().Build();
            await _host.StartAsync();

            var mainVm = _host.Services.GetRequiredService<MainViewModel>();
            var mainWindow = new MainWindow { DataContext = mainVm };
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            ShowErrorDialog("Ошибка запуска", ex);
            Shutdown(1);
        }
    }

    private void SetupGlobalExceptionHandlers()
    {
        DispatcherUnhandledException += (s, args) =>
        {
            var logger = _host?.Services.GetService<ILogger<App>>();
            logger?.LogCritical(args.Exception, "�������������� ���������� � UI ������");
            args.Handled = true;
            ShowErrorDialog("������ UI ������", args.Exception);
        };

        TaskScheduler.UnobservedTaskException += (s, args) =>
        {
            var logger = _host?.Services.GetService<ILogger<App>>();
            logger?.LogCritical(args.Exception, "�������������� ���������� � ������� ������");
            args.SetObserved();
            ShowErrorDialog("������ ������� ������", args.Exception);
        };

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                var logger = _host?.Services.GetService<ILogger<App>>();
                logger?.LogCritical(ex, "����������� �������������� ���������� ������");
                ShowErrorDialog("����������� ������", ex);
            }
        };
    }

    private void ShowErrorDialog(string title, Exception ex)
    {
        try
        {
            LogStartupCrash(ex);
            MessageBox.Show($"{title}\n\nПроизошла непредвиденная ошибка.\n\nПодробности записаны в лог-файл:\n{GetLogDirPath()}",
                "MediaSorter - Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch
        {
            MessageBox.Show($"Произошла критическая ошибка", 
                "MediaSorter - Критическая ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        
        Shutdown(1);
    }

    private static string GetLogDirPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MediaSorter", "logs");
    }

    private void LogStartupCrash(Exception ex)
    {
        try
        {
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MediaSorter", "logs");
            Directory.CreateDirectory(logDir);
            var logFile = Path.Combine(logDir, $"crash_{DateTime.Now:yyyyMMdd_HHmmss}.log");
            File.WriteAllText(logFile, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} STARTUP CRASH\n{ex}");
        }
        catch (Exception logEx)
        {
            Debug.WriteLine($"Failed to write crash log: {logEx.Message}");
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host != null)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(5));
            _host.Dispose();
        }
        base.OnExit(e);
    }

    private static IHostBuilder CreateHostBuilder()
    {
        return Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // 1. FIRST: Configure logging BEFORE any services that need ILogger<T>
                services.AddLogging(builder =>
                {
                    builder.ClearProviders();
                    builder.AddDebug();
                    builder.AddConsole();
                });

                // 2. Settings
                services.AddSingleton<ISettingsService, SettingsService>();
                services.AddSingleton(sp => sp.GetRequiredService<ISettingsService>().Load());

                // 3. Core services (after logging is configured)
                services.AddSingleton<FileSystemHelper>();
                services.AddSingleton<IDateExtractor, DateExtractor>();
                services.AddSingleton<FileNameDateParser>();
                services.AddSingleton<IPhotoScanner, PhotoScanner>();
                services.AddSingleton<ICollisionResolver, CollisionResolver>();
                services.AddSingleton<IMoveExecutor, MoveExecutor>();
                services.AddSingleton<IFileOrganizer, FileOrganizer>();
                services.AddSingleton<IPanoramaDetector, PanoramaDetector>();

                // 4. Logging services
                services.AddSingleton<ILoggerService, FileLoggerService>();

                // 5. ViewModels
                services.AddTransient<MainViewModel>();
                services.AddTransient<PreviewViewModel>();

                // 6. Views
                services.AddTransient<MainWindow>();
                services.AddTransient<Views.PreviewWindow>();
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddDebug();
                logging.AddConsole();
            });
    }
}
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using MediaSorter.Models;
using MediaSorter.Services.Logging;
using MediaSorter.Services.Scanning;
using MediaSorter.Services.Organization;
using MediaSorter.Services.Settings;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Windows;

namespace MediaSorter.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IPhotoScanner _scanner;
    private readonly IFileOrganizer _organizer;
    private readonly ISettingsService _settings;
    private readonly ILoggerService _uiLogger;
    private readonly ILogger<MainViewModel> _logger;
    private readonly SemaphoreSlim _operationLock = new(1, 1);
    private CancellationTokenSource _cts = new();

    [ObservableProperty]
    private string _sourceFolder = "";

    [ObservableProperty]
    private bool _isBusy = false;

    [ObservableProperty]
    private bool _canScan = false;

    [ObservableProperty]
    private bool _hasScanResults = false;

    [ObservableProperty]
    private bool _canStartMove = false;

    [ObservableProperty]
    private int _progressValue = 0;

    [ObservableProperty]
    private int _progressMaximum = 100;

    [ObservableProperty]
    private string _progressText = "Готов к работе";

    [ObservableProperty]
    private string _statusText = "Выберите папку с медиа";

    public ObservableCollection<PhotoItemViewModel> LogItems { get; } = [];

    public IAsyncRelayCommand BrowseCommand { get; }
    public IAsyncRelayCommand ScanCommand { get; }
    public IAsyncRelayCommand PreviewCommand { get; }
    public IAsyncRelayCommand StartMoveCommand { get; }
    public IRelayCommand CancelCommand { get; }
    public IAsyncRelayCommand CleanEmptyFoldersCommand { get; }

    public MainViewModel(
        IPhotoScanner scanner,
        IFileOrganizer organizer,
        ISettingsService settings,
        ILoggerService uiLogger,
        ILogger<MainViewModel> logger)
    {
        _scanner = scanner;
        _organizer = organizer;
        _settings = settings;
        _uiLogger = uiLogger;
        _logger = logger;

        BrowseCommand = new AsyncRelayCommand(BrowseAsync);
        ScanCommand = new AsyncRelayCommand(ScanAsync, () => CanScan && !IsBusy);
        PreviewCommand = new AsyncRelayCommand(PreviewAsync, () => HasScanResults && !IsBusy);
        StartMoveCommand = new AsyncRelayCommand(StartMoveAsync, () => CanStartMove && !IsBusy);
        CancelCommand = new RelayCommand(Cancel, () => IsBusy);
        CleanEmptyFoldersCommand = new AsyncRelayCommand(CleanEmptyFoldersAsync, () => CanScan && !IsBusy);

        SourceFolder = _settings.Current.LastSourceFolder;
        CanScan = !string.IsNullOrWhiteSpace(SourceFolder) && Directory.Exists(SourceFolder);
    }

    partial void OnSourceFolderChanged(string value)
    {
        CanScan = !string.IsNullOrWhiteSpace(value) && Directory.Exists(value);
        if (CanScan)
        {
            _settings.Update(s => s.LastSourceFolder = value);
        }
        ScanCommand.NotifyCanExecuteChanged();
        CleanEmptyFoldersCommand.NotifyCanExecuteChanged();
    }

    private async Task BrowseAsync()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Выберите папку с медиа",
            InitialDirectory = !string.IsNullOrWhiteSpace(SourceFolder) ? SourceFolder : Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
        };

        if (dialog.ShowDialog() == true)
        {
            SourceFolder = dialog.FolderName;
        }
    }

    private async Task ScanAsync()
    {
        if (!await _operationLock.WaitAsync(0)) return;
        try
        {
            if (IsBusy || !CanScan) return;

            IsBusy = true;
            StatusText = "Сканирование...";
            ProgressValue = 0;
            ProgressText = "Подготовка...";
            LogItems.Clear();
            HasScanResults = false;
            CanStartMove = false;

            _cts.Cancel();
            _cts.Dispose();
            _cts = new CancellationTokenSource();

            try
            {
                _uiLogger.Log("Начало сканирования: " + SourceFolder);

                var result = await _scanner.ScanAsync(SourceFolder, _cts.Token, 
                    new Progress<ScanProgress>(p =>
                    {
                        ProgressValue = p.ProcessedFiles;
                        ProgressMaximum = p.TotalFiles;
                        ProgressText = $"Обработано {p.ProcessedFiles} из {p.TotalFiles}";
                        StatusText = $"Сканирование: {p.CurrentFile}";
                    }));

                foreach (var photo in result.Photos)
                {
                    LogItems.Add(new PhotoItemViewModel(photo));
                }

                HasScanResults = result.NewPhotos > 0;
                CanStartMove = result.NewPhotos > 0;
                StatusText = $"Готово: {result.WithDate} с датой, {result.WithoutDate} без даты, {result.AlreadySorted} уже отсортированы";
                ProgressText = $"Сканировано {result.TotalScanned} файлов за {result.Duration.TotalSeconds:F1} сек";

                _uiLogger.Log($"Сканирование завершено: {result.TotalScanned} файлов, {result.WithDate} с датой, {result.WithoutDate} без даты");
            }
            catch (OperationCanceledException)
            {
                StatusText = "Сканирование отменено";
                _uiLogger.Log("Сканирование отменено пользователем");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка сканирования");
                _uiLogger.LogError("Ошибка сканирования: " + ex.Message);
                StatusText = "Ошибка сканирования";
            }
            finally
            {
                IsBusy = false;
                ScanCommand.NotifyCanExecuteChanged();
                PreviewCommand.NotifyCanExecuteChanged();
                StartMoveCommand.NotifyCanExecuteChanged();
                CancelCommand.NotifyCanExecuteChanged();
                CleanEmptyFoldersCommand.NotifyCanExecuteChanged();
            }
        }
        finally
        {
            _operationLock.Release();
        }
    }

    private async Task PreviewAsync()
    {
        if (!HasScanResults) return;

        var plans = LogItems
            .Where(x => x.Status == FileStatus.Scanned && x.HasDate)
            .Select(x => x.ToMovePlan())
            .ToList();

        var previewWindow = new Views.PreviewWindow(plans);
        previewWindow.Owner = Application.Current.MainWindow;
        previewWindow.ShowDialog();
    }

    private async Task StartMoveAsync()
    {
        if (!await _operationLock.WaitAsync(0)) return;
        try
        {
            if (IsBusy || !CanStartMove) return;

            IsBusy = true;
            StatusText = "Перемещение файлов...";
            ProgressValue = 0;
            
            _cts.Cancel();
            _cts.Dispose();
            _cts = new CancellationTokenSource();

            try
            {
                _uiLogger.Log("Начало перемещения файлов");

                var plans = LogItems
                    .Where(x => x.Status == FileStatus.Scanned && x.HasDate)
                    .Select(x => x.ToMovePlan())
                    .ToList();

                var result = await _organizer.OrganizeAsync(plans, SourceFolder, _cts.Token,
                    new Progress<MediaSorter.Services.Organization.OrganizeProgress>(p =>
                    {
                        ProgressValue = p.Current;
                        ProgressMaximum = p.Total;
                        ProgressText = $"Перемещено {p.Moved}, пропущено {p.Skipped}, ошибок {p.Errors}";
                        StatusText = !string.IsNullOrEmpty(p.CurrentFile) ? $"Перемещение: {p.CurrentFile}" : "Перемещение...";
                    }));

                StatusText = $"Завершено: перемещено {result.Moved}, пропущено {result.Skipped}, дубликатов {result.SkippedDuplicates}, ошибок {result.Errors}";
                ProgressText = $"Готово за {result.Duration.TotalSeconds:F1} сек";

                _uiLogger.Log($"Перемещение завершено: {result.Moved} файлов, {result.Errors} ошибок");
            }
            catch (OperationCanceledException)
            {
                StatusText = "Перемещение отменено";
                _uiLogger.Log("Перемещение отменено пользователем");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка перемещения");
                _uiLogger.LogError("Ошибка перемещения: " + ex.Message);
                StatusText = "Ошибка перемещения";
            }
            finally
            {
                IsBusy = false;
                ScanCommand.NotifyCanExecuteChanged();
                PreviewCommand.NotifyCanExecuteChanged();
                StartMoveCommand.NotifyCanExecuteChanged();
                CancelCommand.NotifyCanExecuteChanged();
                CleanEmptyFoldersCommand.NotifyCanExecuteChanged();
            }
        }
        finally
        {
            _operationLock.Release();
        }
    }

    private void Cancel()
    {
        _cts.Cancel();
        StatusText = "Отмена...";
        _uiLogger.Log("Пользователь запросил отмену операции");
    }

    private async Task CleanEmptyFoldersAsync()
    {
        if (!await _operationLock.WaitAsync(0)) return;
        try
        {
            if (IsBusy || !CanScan) return;

            IsBusy = true;
            StatusText = "Поиск пустых папок...";
            ProgressValue = 0;
            ProgressText = "";

            try
            {
                _uiLogger.Log("Поиск пустых папок в: " + SourceFolder);

                var removed = await Task.Run(() =>
                {
                    var count = 0;
                    var dirs = Directory.GetDirectories(SourceFolder, "*", SearchOption.AllDirectories)
                        .OrderByDescending(d => d.Length);

                    foreach (var dir in dirs)
                    {
                        _cts.Token.ThrowIfCancellationRequested();

                        if (!Directory.EnumerateFileSystemEntries(dir).Any())
                        {
                            Directory.Delete(dir);
                            count++;
                        }
                    }

                    return count;
                }, _cts.Token);

                var msg = removed > 0
                    ? $"Очищено пустых папок: {removed}"
                    : "Пустых папок не найдено";
                StatusText = msg;
                _uiLogger.Log(msg);
            }
            catch (OperationCanceledException)
            {
                StatusText = "Очистка отменена";
                _uiLogger.Log("Очистка пустых папок отменена");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка очистки пустых папок");
                _uiLogger.LogError("Ошибка очистки: " + ex.Message);
                StatusText = "Ошибка очистки";
            }
            finally
            {
                IsBusy = false;
                CleanEmptyFoldersCommand.NotifyCanExecuteChanged();
            }
        }
        finally
        {
            _operationLock.Release();
        }
    }
}
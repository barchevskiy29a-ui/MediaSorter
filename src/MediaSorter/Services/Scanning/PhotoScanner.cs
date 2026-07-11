using Microsoft.Extensions.Logging;
using MediaSorter.Models;
using MediaSorter.Helpers;
using MediaSorter.Services.Metadata;
using MediaSorter.Services.System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MediaSorter.Services.Scanning;

public interface IPhotoScanner
{
    Task<ScanResult> ScanAsync(string rootPath, CancellationToken ct, IProgress<ScanProgress>? progress = null);
}

public class PhotoScanner : IPhotoScanner
{
    private readonly IDateExtractor _dateExtractor;
    private readonly FileSystemHelper _fileSystem;
    private readonly ILogger<PhotoScanner> _logger;
    private readonly AppSettings _settings;

    public PhotoScanner(
        IDateExtractor dateExtractor,
        FileSystemHelper fileSystem,
        ILogger<PhotoScanner> logger,
        AppSettings settings)
    {
        _dateExtractor = dateExtractor;
        _fileSystem = fileSystem;
        _logger = logger;
        _settings = settings;
    }

    public async Task<ScanResult> ScanAsync(string rootPath, CancellationToken ct, IProgress<ScanProgress>? progress = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var photos = new List<PhotoFile>();
        var totalFiles = 0;
        var withDate = 0;
        var withoutDate = 0;
        var alreadySorted = 0;
        var errors = 0;

        _logger.LogInformation("Начинаю сканирование: {RootPath}", rootPath);

        var files = GetImageFiles(rootPath, _settings.ScanRecursively).ToList();
        totalFiles = files.Count;

        _logger.LogInformation("Найдено файлов для обработки: {Count}", totalFiles);

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Clamp(_settings.MaxParallelism > 0 ? _settings.MaxParallelism : Environment.ProcessorCount, 1, 64),
            CancellationToken = ct
        };

        var lockObj = new object();
        var processedCount = 0;

        await Task.Run(() => Parallel.ForEach(files, parallelOptions, filePath =>
        {
            ct.ThrowIfCancellationRequested();
            
            try
            {
                var photo = ProcessFile(filePath, rootPath);
                
                lock (lockObj)
                {
                    photos.Add(photo);
                    processedCount++;
                    
                    if (photo.HasDate) withDate++;
                    else withoutDate++;
                    
                    if (photo.Status == FileStatus.SkippedAlreadySorted) alreadySorted++;
                    if (photo.Status == FileStatus.Error) errors++;
                    
                    if (processedCount % 50 == 0)
                    {
                        progress?.Report(new ScanProgress(
                            processedCount, totalFiles, photo.FileName, photo.DateSource));
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ошибка при обработке файла {File}", filePath);
                lock (lockObj)
                {
                    errors++;
                    photos.Add(new PhotoFile(
                        filePath, null, DateSource.Unknown, 0, Path.GetFileName(filePath), Path.GetExtension(filePath), "", 
                        Status: FileStatus.Error, ErrorMessage: ex.Message));
                    processedCount++;
                }
            }
        }), ct);

        stopwatch.Stop();

        var result = new ScanResult(
            photos.OrderBy(p => p.SourcePath).ToList(),
            totalFiles,
            withDate,
            withoutDate,
            alreadySorted,
            errors,
            stopwatch.Elapsed
        );

        _logger.LogInformation("Сканирование завершено: {Total} файлов, {WithDate} с датой, {WithoutDate} без даты, {AlreadySorted} уже отсортированы, {Errors} ошибок за {Duration}",
            totalFiles, withDate, withoutDate, alreadySorted, errors, stopwatch.Elapsed);

        progress?.Report(new ScanProgress(totalFiles, totalFiles, "Готово", null));
        
        return result;
    }

    private PhotoFile ProcessFile(string filePath, string rootPath)
    {
        var relativePath = Path.GetRelativePath(rootPath, filePath);
        var parentFolder = Path.GetDirectoryName(relativePath)?.Split(Path.DirectorySeparatorChar).FirstOrDefault() ?? "";
        
        if (_settings.SkipAlreadySorted && RegexPatterns.DateFolderPattern.IsMatch(parentFolder))
        {
            if (DateOnly.TryParseExact(parentFolder, "dd.MM.yyyy", out var folderDate))
            {
                var extracted = _dateExtractor.ExtractAsync(filePath).GetAwaiter().GetResult();
                if (extracted.HasValue && extracted.Value == folderDate)
                {
                    return new PhotoFile(filePath, folderDate, DateSource.ExifDateTimeOriginal, 
                        new FileInfo(filePath).Length, Path.GetFileName(filePath), Path.GetExtension(filePath), 
                        relativePath, Status: FileStatus.SkippedAlreadySorted);
                }
            }
        }

        var (date, source) = _dateExtractor.ExtractWithSourceAsync(filePath).GetAwaiter().GetResult();
        
        var status = date.HasValue ? FileStatus.Scanned : FileStatus.SkippedNoDate;
        
        return new PhotoFile(
            SourcePath: filePath,
            DateTaken: date,
            DateSource: source,
            FileSize: new FileInfo(filePath).Length,
            FileName: Path.GetFileName(filePath),
            Extension: Path.GetExtension(filePath),
            RelativePath: relativePath,
            Status: status
        );
    }

    private static IEnumerable<string> GetImageFiles(string rootPath, bool recursive)
    {
        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var extSet = RegexPatterns.SupportedExtensions
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var resolvedRoot = Path.GetFullPath(rootPath).TrimEnd('\\') + '\\';

        foreach (var file in Directory.EnumerateFiles(rootPath, "*", searchOption))
        {
            var ext = Path.GetExtension(file);
            if (extSet.Contains(ext))
            {
                var resolvedFile = Path.GetFullPath(file);
                if (resolvedFile.StartsWith(resolvedRoot, StringComparison.OrdinalIgnoreCase))
                    yield return file;
            }
        }
    }
}
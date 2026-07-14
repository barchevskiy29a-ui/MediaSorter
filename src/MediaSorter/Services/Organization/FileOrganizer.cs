using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MediaSorter.Models;
using MediaSorter.Services.System;

namespace MediaSorter.Services.Organization;

public interface IFileOrganizer
{
    Task<OrganizeResult> OrganizeAsync(
        IReadOnlyList<MovePlan> plans, 
        string rootPath, 
        CancellationToken ct,
        IProgress<OrganizeProgress>? progress = null);
}

public record OrganizeProgress(
    int Current,
    int Total,
    int Moved = 0,
    int Skipped = 0,
    int Errors = 0,
    string? CurrentFile = null
);

public class FileOrganizer : IFileOrganizer
{
    private readonly ILogger<FileOrganizer> _logger;
    private readonly IMoveExecutor _moveExecutor;
    private readonly FileSystemHelper _fileSystem;
    private readonly AppSettings _settings;
    private readonly List<MoveRecord> _rollbackJournal = new();

    public FileOrganizer(
        ILogger<FileOrganizer> logger,
        IMoveExecutor moveExecutor,
        FileSystemHelper fileSystem,
        AppSettings settings)
    {
        _logger = logger;
        _moveExecutor = moveExecutor;
        _fileSystem = fileSystem;
        _settings = settings;
    }

    public async Task<OrganizeResult> OrganizeAsync(
        IReadOnlyList<MovePlan> plans, 
        string rootPath, 
        CancellationToken ct,
        IProgress<OrganizeProgress>? progress = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var moved = 0;
        var skipped = 0;
        var skippedDuplicates = 0;
        var skippedAlreadySorted = 0;
        var errors = 0;
        var accessDenied = 0;
        var fileInUse = 0;
        var pathTooLong = 0;
        var errorDetails = new List<string>();
        _rollbackJournal.Clear();
        var createdDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var toProcess = plans.Where(p => !p.SkipBecauseAlreadySorted).ToList();
        var total = toProcess.Count;
        var processed = 0;

        _logger.LogInformation("Начинаю перемещение {Count} файлов", total);

        foreach (var plan in toProcess)
        {
            ct.ThrowIfCancellationRequested();

            var targetDir = plan.TargetDirectory;
            
            _fileSystem.EnsureDirectoryExists(targetDir);
            createdDirs.Add(targetDir);

            progress?.Report(new OrganizeProgress(processed, total, moved, skipped + skippedDuplicates, errors, plan.FileName));

            var moveResult = await _moveExecutor.ExecuteAsync(plan, ct);

            if (moveResult.Success)
            {
                if (moveResult.ActualAction == ConflictResolution.SkipDuplicate)
                {
                    skippedDuplicates++;
                    skipped++;
                }
                else
                {
                    moved++;
                    _rollbackJournal.Add(new MoveRecord(plan.SourcePath, plan.FinalTargetPath, DateTime.UtcNow));
                }
            }
            else
            {
                errors++;
                errorDetails.Add($"{plan.FileName}: {moveResult.ErrorMessage}");
                
                if (moveResult.ErrorMessage?.Contains("доступ") == true) accessDenied++;
                else if (moveResult.ErrorMessage?.Contains("занят") == true) fileInUse++;
                else if (moveResult.ErrorMessage?.Contains("длинн") == true) pathTooLong++;
                
                _logger.LogWarning("Ошибка перемещения {File}: {Error}", plan.FileName, moveResult.ErrorMessage);
            }

            processed++;
            
            if (processed % 50 == 0)
            {
                progress?.Report(new OrganizeProgress(processed, total, moved, skipped + skippedDuplicates, errors));
            }
        }

        var emptyFoldersRemoved = 0;
        foreach (var dir in createdDirs.OrderByDescending(d => d.Length))
        {
            if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
            {
                Directory.Delete(dir);
                _logger.LogInformation("Удалена пустая папка: {Dir}", dir);
                emptyFoldersRemoved++;
            }
        }

        stopwatch.Stop();

        progress?.Report(new OrganizeProgress(total, total, moved, skipped + skippedDuplicates, errors));

        if (emptyFoldersRemoved > 0)
            _logger.LogInformation("Удалено пустых папок: {Count}", emptyFoldersRemoved);

        var result = new OrganizeResult(
            moved,
            skipped,
            skippedDuplicates,
            skippedAlreadySorted,
            errors,
            errorDetails,
            stopwatch.Elapsed,
            _settings.LogToFile ? SaveRollbackJournal() : null,
            emptyFoldersRemoved
        );

        _logger.LogInformation("Перемещение завершено: {Moved} перемещено, {Skipped} пропущено, {Duplicates} дубликатов, {Errors} ошибок за {Duration}",
            moved, skipped, skippedDuplicates, errors, stopwatch.Elapsed);

        return result;
    }

    private string? SaveRollbackJournal()
    {
        try
        {
            var tempPath = Path.GetTempPath();
            var journalPath = Path.Combine(tempPath, $"MediaSorter_Rollback_{Guid.NewGuid():N}.jsonl");
            
            using var writer = File.CreateText(journalPath);
            foreach (var record in _rollbackJournal)
            {
                var json = global::System.Text.Json.JsonSerializer.Serialize(record);
                writer.WriteLine(json);
            }
            
            return journalPath;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось сохранить журнал отката");
            return null;
        }
    }
}
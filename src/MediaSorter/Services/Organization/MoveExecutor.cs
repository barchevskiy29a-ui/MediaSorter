using System.IO;
using Microsoft.Extensions.Logging;
using MediaSorter.Helpers;
using MediaSorter.Models;
using MediaSorter.Services.System;

namespace MediaSorter.Services.Organization;

public interface IMoveExecutor
{
    Task<MoveResult> ExecuteAsync(MovePlan plan, CancellationToken ct = default);
}

public record MoveResult(
    bool Success,
    string? ErrorMessage = null,
    ConflictResolution ActualAction = ConflictResolution.Move
);

public class MoveExecutor : IMoveExecutor
{
    private readonly ILogger<MoveExecutor> _logger;
    private readonly FileSystemHelper _fileSystem;
    private readonly ICollisionResolver _collisionResolver;
    private readonly int _retryCount;
    private readonly int _retryDelayMs;
    private readonly bool _requestElevation;

    public MoveExecutor(
        ILogger<MoveExecutor> logger,
        FileSystemHelper fileSystem,
        ICollisionResolver collisionResolver,
        int retryCount = 3,
        int retryDelayMs = 500,
        bool requestElevation = true)
    {
        _logger = logger;
        _fileSystem = fileSystem;
        _collisionResolver = collisionResolver;
        _retryCount = retryCount;
        _retryDelayMs = retryDelayMs;
        _requestElevation = requestElevation;
    }

    public async Task<MoveResult> ExecuteAsync(MovePlan plan, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var sourcePath = LongPathHelper.ToExtendedLengthPath(plan.SourcePath);
        var targetDir = LongPathHelper.ToExtendedLengthPath(plan.TargetDirectory);
        var targetPath = LongPathHelper.ToExtendedLengthPath(plan.TargetPath);

        var finalTargetPath = targetPath;
        var finalAction = ConflictResolution.Move;
        string? newFileName = null;

        if (_fileSystem.FileExists(targetPath))
        {
            var resolution = await _collisionResolver.ResolveAsync(sourcePath, targetPath, ConflictAction.RenameWithHash, ct);
            
            switch (resolution)
            {
                case ConflictResolution.SkipDuplicate:
                    return new MoveResult(true, null, ConflictResolution.SkipDuplicate);
                case ConflictResolution.Overwrite:
                    finalAction = ConflictResolution.Overwrite;
                    break;
                case ConflictResolution.RenameAndMove:
                    finalAction = ConflictResolution.RenameAndMove;
                    var sourceHash = plan.SourceHash is not null ? Convert.ToUInt64(plan.SourceHash, 16) : 0UL;
                    newFileName = _collisionResolver.GenerateUniqueName(targetPath, sourceHash, ConflictAction.RenameWithHash);
                    finalTargetPath = LongPathHelper.ToExtendedLengthPath(Path.Combine(targetDir, newFileName));
                    break;
            }
        }

        _fileSystem.EnsureDirectoryExists(targetDir);

        for (int attempt = 1; attempt <= _retryCount; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var canWrite = _fileSystem.CanWrite(targetDir, out var errorMsg);
                if (!canWrite)
                {
                    if (_requestElevation && attempt == _retryCount)
                    {
                        _logger.LogWarning("Нет прав на запись в {Dir}, запрос повышения прав...", targetDir);
                        var elevated = await _fileSystem.RequestElevationAsync(targetDir);
                        if (!elevated)
                        {
                            return new MoveResult(false, $"Нет прав на запись в эту папку. Запустите программу от имени администратора и повторите попытку.", ConflictResolution.Move);
                        }
                    }
                    else
                    {
                        return new MoveResult(false, errorMsg ?? "Нет прав на запись", ConflictResolution.Move);
                    }
                }

                if (finalAction == ConflictResolution.Overwrite && _fileSystem.FileExists(finalTargetPath))
                {
                    _fileSystem.DeleteFile(finalTargetPath);
                }

                _fileSystem.MoveFile(sourcePath, finalTargetPath, overwrite: false);
                
                _logger.LogInformation("Перемещен: {Source} -> {Target}", Path.GetFileName(sourcePath), Path.GetFileName(finalTargetPath));
                return new MoveResult(true, null, finalAction);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Попытка {Attempt}/{Max}: Нет доступа к {File}", attempt, _retryCount, Path.GetFileName(sourcePath));
                
                if (attempt == _retryCount)
                {
                    if (_requestElevation)
                    {
                        var elevated = await _fileSystem.RequestElevationAsync(targetDir);
                        if (elevated) continue;
                    }
                    return new MoveResult(false, "Отказано в доступе (нужны права администратора)", ConflictResolution.Move);
                }
            }
            catch (IOException ex) when (ex.HResult == unchecked((int)0x80070020))
            {
                _logger.LogWarning(ex, "Попытка {Attempt}/{Max}: Файл занят {File}", attempt, _retryCount, Path.GetFileName(sourcePath));
                if (attempt == _retryCount)
                {
                    return new MoveResult(false, "Файл занят другой программой", ConflictResolution.Move);
                }
            }
            catch (IOException ex) when (ex.HResult == unchecked((int)0x80070057))
            {
                _logger.LogError(ex, "Путь слишком длинный: {Source} -> {Target}", sourcePath, finalTargetPath);
                return new MoveResult(false, "Путь превышает максимальную длину", ConflictResolution.Move);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка перемещения {Source} -> {Target}", sourcePath, finalTargetPath);
                if (attempt == _retryCount)
                {
                    return new MoveResult(false, ex.Message, ConflictResolution.Move);
                }
            }

            await Task.Delay(_retryDelayMs * attempt, ct);
        }

        return new MoveResult(false, "Превышено количество попыток", ConflictResolution.Move);
    }
}
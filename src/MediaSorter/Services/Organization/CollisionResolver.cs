using System.IO;
using Microsoft.Extensions.Logging;
using MediaSorter.Helpers;
using MediaSorter.Models;
using MediaSorter.Services.System;

namespace MediaSorter.Services.Organization;

public interface ICollisionResolver
{
    ConflictResolution Resolve(string sourcePath, string targetPath, ConflictAction defaultAction);
    Task<ConflictResolution> ResolveAsync(string sourcePath, string targetPath, ConflictAction defaultAction, CancellationToken ct);
    string GenerateUniqueName(string targetPath, ulong sourceHash, ConflictAction action);
}

public class CollisionResolver : ICollisionResolver
{
    private readonly ILogger<CollisionResolver> _logger;
    private readonly FileSystemHelper _fileSystem;

    public CollisionResolver(ILogger<CollisionResolver> logger, FileSystemHelper fileSystem)
    {
        _logger = logger;
        _fileSystem = fileSystem;
    }

    public ConflictResolution Resolve(string sourcePath, string targetPath, ConflictAction defaultAction)
    {
        return ResolveAsync(sourcePath, targetPath, defaultAction, CancellationToken.None).GetAwaiter().GetResult();
    }

    public async Task<ConflictResolution> ResolveAsync(string sourcePath, string targetPath, ConflictAction defaultAction, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (!_fileSystem.FileExists(targetPath))
            return ConflictResolution.Move;

        try
        {
            var sourceHash = await HashHelper.ComputeXxHash3Async(sourcePath, ct);
            var targetHash = await HashHelper.ComputeXxHash3Async(targetPath, ct);

            if (sourceHash == targetHash)
            {
                _logger.LogDebug("Дубликат найден (идентичный хеш): {File}", Path.GetFileName(sourcePath));
                return ConflictResolution.SkipDuplicate;
            }

            switch (defaultAction)
            {
                case ConflictAction.Skip:
                    return ConflictResolution.SkipDuplicate;
                case ConflictAction.Overwrite:
                    return ConflictResolution.Overwrite;
                case ConflictAction.RenameWithNumber:
                    return ConflictResolution.RenameAndMove;
                case ConflictAction.RenameWithHash:
                default:
                    return ConflictResolution.RenameAndMove;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ошибка при сравнении файлов {Source} и {Target}", sourcePath, targetPath);
            return ConflictResolution.RenameAndMove;
        }
    }

    public string GenerateUniqueName(string targetPath, ulong sourceHash, ConflictAction action)
    {
        var directory = Path.GetDirectoryName(targetPath)!;
        var fileName = Path.GetFileNameWithoutExtension(targetPath);
        var extension = Path.GetExtension(targetPath);

        if (action == ConflictAction.RenameWithHash)
        {
            var shortHash = HashHelper.ShortHash(sourceHash);
            var newName = $"{fileName}_{shortHash}{extension}";
            var fullPath = Path.Combine(directory, newName);
            
            var counter = 1;
            while (_fileSystem.FileExists(fullPath))
            {
                newName = $"{fileName}_{shortHash}_{counter}{extension}";
                fullPath = Path.Combine(directory, newName);
                counter++;
            }
            return newName;
        }
        else
        {
            var counter = 1;
            var newName = $"{fileName}_{counter}{extension}";
            var fullPath = Path.Combine(directory, newName);
            
            while (_fileSystem.FileExists(fullPath))
            {
                counter++;
                newName = $"{fileName}_{counter}{extension}";
                fullPath = Path.Combine(directory, newName);
            }
            return newName;
        }
    }
}
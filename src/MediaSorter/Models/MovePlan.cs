using System.IO;

namespace MediaSorter.Models;

public record MovePlan(
    string SourcePath,
    string TargetPath,
    string FileName,
    DateOnly? DateTaken,
    DateSource DateSource,
    ConflictResolution ConflictAction,
    string? SourceHash = null,
    string? TargetHash = null,
    bool IsPanoramaPart = false,
    int? PanoramaGroupId = null,
    bool SkipBecauseAlreadySorted = false
)
{
    public string TargetDirectory => Path.GetDirectoryName(TargetPath) ?? string.Empty;
    public string TargetFolderName => Path.GetDirectoryName(TargetPath)?.Split(Path.DirectorySeparatorChar).LastOrDefault() ?? string.Empty;
    public string FinalTargetPath => TargetPath;
    public bool IsConflict => ConflictAction is ConflictResolution.RenameAndMove or ConflictResolution.Overwrite;
    public bool IsDuplicate => ConflictAction == ConflictResolution.SkipDuplicate;
}

public record MoveRecord(string SourcePath, string TargetPath, DateTime Timestamp);

public record OrganizeResult(
    int Moved,
    int Skipped,
    int SkippedDuplicates,
    int SkippedAlreadySorted,
    int Errors,
    IReadOnlyList<string> ErrorMessages,
    TimeSpan Duration,
    string? RollbackJournalPath = null,
    int EmptyFoldersRemoved = 0
)
{
    public static OrganizeResult Empty => new(0, 0, 0, 0, 0, Array.Empty<string>(), TimeSpan.Zero);
    public int TotalProcessed => Moved + Skipped + SkippedDuplicates + SkippedAlreadySorted + Errors;
}
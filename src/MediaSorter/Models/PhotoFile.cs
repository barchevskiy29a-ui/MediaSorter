using System.IO;

namespace MediaSorter.Models;

public record PhotoFile(
    string SourcePath,
    DateOnly? DateTaken,
    DateSource DateSource,
    long FileSize,
    string FileName,
    string Extension,
    string RelativePath,
    string? Hash = null,
    bool IsPanoramaPart = false,
    int? PanoramaGroupId = null,
    FileStatus Status = FileStatus.Pending,
    string? ErrorMessage = null
)
{
    public string SourceDirectory => Path.GetDirectoryName(SourcePath) ?? string.Empty;
    public string ParentFolderName => Path.GetDirectoryName(RelativePath)?.Split(Path.DirectorySeparatorChar).LastOrDefault() ?? string.Empty;
    public bool HasDate => DateTaken.HasValue;
    
    public string GetTargetFolderName(string format, string unknownFolderName)
    {
        if (DateTaken.HasValue)
            return DateTaken.Value.ToString(format);
        return unknownFolderName;
    }
}

public record ScanResult(
    IReadOnlyList<PhotoFile> Photos,
    int TotalScanned,
    int WithDate,
    int WithoutDate,
    int AlreadySorted,
    int Errors,
    TimeSpan Duration
)
{
    public static ScanResult Empty => new(
        Array.Empty<PhotoFile>(), 0, 0, 0, 0, 0, TimeSpan.Zero);
    
    public int NewPhotos => Photos.Count(p => p.HasDate && p.Status != FileStatus.SkippedAlreadySorted);
}
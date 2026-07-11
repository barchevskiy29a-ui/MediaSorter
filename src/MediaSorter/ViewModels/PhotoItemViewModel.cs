using CommunityToolkit.Mvvm.ComponentModel;
using MediaSorter.Models;
using MediaSorter.Services.Organization;
using System.IO;

namespace MediaSorter.ViewModels;

public partial class PhotoItemViewModel : ObservableObject
{
    private readonly PhotoFile _photo;

    public PhotoItemViewModel(PhotoFile photo)
    {
        _photo = photo;
    }

    public string FileName => _photo.FileName;
    public string RelativePath => _photo.RelativePath;
    public DateOnly? DateTaken => _photo.DateTaken;
    public DateSource DateSource => _photo.DateSource;
    public FileStatus Status => _photo.Status;
    public string StatusText => Status.ToString();
    public string DateSourceText => DateSource.ToString();
    public long FileSize => _photo.FileSize;
    public bool HasDate => _photo.HasDate;

    [ObservableProperty]
    private string _errorMessage = "";

    public string SizeFormatted => FormatSize(FileSize);

    public MovePlan ToMovePlan()
{
    var targetFolder = _photo.GetTargetFolderName("dd.MM.yyyy", "дата съемки неопределена");
    var targetPath = Path.Combine(_photo.SourceDirectory, targetFolder, _photo.FileName);
    
    return new MovePlan(
        SourcePath: _photo.SourcePath,
        TargetPath: targetPath,
        FileName: _photo.FileName,
        DateTaken: _photo.DateTaken ?? DateOnly.MinValue,
        DateSource: _photo.DateSource,
        ConflictAction: ConflictResolution.Move,
        SourceHash: _photo.Hash
    );
}

    private static string FormatSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }
}
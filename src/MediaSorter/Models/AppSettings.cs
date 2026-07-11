namespace MediaSorter.Models;

public record AppSettings
{
    public string LastSourceFolder { get; set; } = string.Empty;
    public string DateFolderFormat { get; set; } = "dd.MM.yyyy";
    public bool ScanRecursively { get; set; } = true;
    public ConflictAction DefaultConflictAction { get; set; } = ConflictAction.RenameWithHash;
    public bool LogToFile { get; set; } = true;
    public string UnknownDateFolderName { get; set; } = "дата съемки неопределена";
    public bool SkipAlreadySorted { get; set; } = true;
    public bool GroupPanoramas { get; set; } = true;
    public int MaxParallelism { get; set; } = 4;
}
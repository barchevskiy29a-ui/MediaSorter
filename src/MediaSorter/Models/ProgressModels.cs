namespace MediaSorter.Models;

public record ScanProgress(
    int ProcessedFiles,
    int TotalFiles,
    string CurrentFile,
    DateSource? CurrentDateSource = null
);


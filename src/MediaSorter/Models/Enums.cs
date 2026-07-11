namespace MediaSorter.Models;

public enum DateSource
{
    Unknown,
    ExifDateTimeOriginal,
    ExifDateTimeDigitized,
    ExifDateTime,
    QuickTimeCreationDate,
    XmpCreateDate,
    XmpModifyDate,
    FileName,
    FileSystemCreationTime,
    FileSystemLastWriteTime
}

public enum ConflictAction
{
    Skip,
    Overwrite,
    RenameWithHash,
    RenameWithNumber
}

public enum ConflictResolution
{
    Move,
    SkipDuplicate,
    RenameAndMove,
    Overwrite
}

public enum FileStatus
{
    Pending,
    Scanning,
    Scanned,
    Moving,
    Moved,
    Skipped,
    SkippedDuplicate,
    SkippedAlreadySorted,
    SkippedNoDate,
    Error,
    ErrorAccessDenied,
    ErrorFileInUse,
    ErrorPathTooLong
}
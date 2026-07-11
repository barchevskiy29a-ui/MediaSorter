using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.Xmp;
using Microsoft.Extensions.Logging;
using MediaSorter.Helpers;
using MediaSorter.Models;
using MetadataDirectory = MetadataExtractor.Directory;
using System.IO;

namespace MediaSorter.Services.Metadata;

public interface IDateExtractor
{
    Task<DateOnly?> ExtractAsync(string filePath, CancellationToken ct = default);
    Task<(DateOnly? Date, DateSource Source)> ExtractWithSourceAsync(string filePath, CancellationToken ct = default);
}

public class DateExtractor : IDateExtractor
{
    private readonly FileNameDateParser _fileNameParser;
    private readonly ILogger<DateExtractor> _logger;

    // XMP tag constants
    private const int XmpCreateDateTag = 1001;
    private const int XmpModifyDateTag = 1002;
    // QuickTime creation time tag
    private const int QuickTimeCreationDateTag = 1001;

    public DateExtractor(FileNameDateParser fileNameParser, ILogger<DateExtractor> logger)
    {
        _fileNameParser = fileNameParser;
        _logger = logger;
    }

    public async Task<DateOnly?> ExtractAsync(string filePath, CancellationToken ct = default)
    {
        var result = await ExtractWithSourceAsync(filePath, ct);
        return result.Date;
    }

    public Task<(DateOnly? Date, DateSource Source)> ExtractWithSourceAsync(string filePath, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var directories = ReadDirectories(filePath);

        // 1. EXIF DateTimeOriginal (most accurate - when photo was taken)
        var date = TryExtractExifDateTimeOriginal(directories);
        if (date.HasValue) return Task.FromResult<(DateOnly?, DateSource)>((date.Value, Source: DateSource.ExifDateTimeOriginal));

        // 2. EXIF DateTimeDigitized (when digitized)
        date = TryExtractExifDateTimeDigitized(directories);
        if (date.HasValue) return Task.FromResult<(DateOnly?, DateSource)>((date.Value, Source: DateSource.ExifDateTimeDigitized));

        // 3. EXIF DateTime (file modification)
        date = TryExtractExifDateTime(directories);
        if (date.HasValue) return Task.FromResult<(DateOnly?, DateSource)>((date.Value, Source: DateSource.ExifDateTime));

        // 4. QuickTime CreationDate (HEIC, MOV, MP4) - try using generic directory
        date = TryExtractQuickTimeCreationDate(directories);
        if (date.HasValue) return Task.FromResult<(DateOnly?, DateSource)>((date.Value, Source: DateSource.QuickTimeCreationDate));

        // 5. XMP CreateDate
        date = TryExtractXmpCreateDate(directories);
        if (date.HasValue) return Task.FromResult<(DateOnly?, DateSource)>((date.Value, Source: DateSource.XmpCreateDate));

        // 6. XMP ModifyDate
        date = TryExtractXmpModifyDate(directories);
        if (date.HasValue) return Task.FromResult<(DateOnly?, DateSource)>((date.Value, Source: DateSource.XmpModifyDate));

        // 7. File name patterns
        date = _fileNameParser.ParseFromFileName(filePath);
        if (date.HasValue) return Task.FromResult<(DateOnly?, DateSource)>((date.Value, Source: DateSource.FileName));

        // 8. File system creation time
        date = TryGetFileSystemCreationTime(filePath);
        if (date.HasValue) return Task.FromResult<(DateOnly?, DateSource)>((date.Value, Source: DateSource.FileSystemCreationTime));

        // 9. File system last write time
        date = TryGetFileSystemLastWriteTime(filePath);
        if (date.HasValue) return Task.FromResult<(DateOnly?, DateSource)>((date.Value, Source: DateSource.FileSystemLastWriteTime));

        return Task.FromResult<(DateOnly? Date, DateSource Source)>((null, DateSource.Unknown));
    }

    private IReadOnlyList<MetadataDirectory> ReadDirectories(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            return ImageMetadataReader.ReadMetadata(stream);
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Failed to read metadata for {File}", filePath);
            return Array.Empty<MetadataDirectory>();
        }
    }

    private DateOnly? TryExtractExifDateTimeOriginal(IReadOnlyList<MetadataDirectory> directories)
    {
        try
        {
            var exifDir = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
            if (exifDir?.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out var dateTime) == true)
                return DateOnly.FromDateTime(dateTime);
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "EXIF DateTimeOriginal extraction failed");
        }
        return null;
    }

    private DateOnly? TryExtractExifDateTimeDigitized(IReadOnlyList<MetadataDirectory> directories)
    {
        try
        {
            var exifDir = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
            if (exifDir?.TryGetDateTime(ExifDirectoryBase.TagDateTimeDigitized, out var dateTime) == true)
                return DateOnly.FromDateTime(dateTime);
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "EXIF DateTimeDigitized extraction failed");
        }
        return null;
    }

    private DateOnly? TryExtractExifDateTime(IReadOnlyList<MetadataDirectory> directories)
    {
        try
        {
            var exifDir = directories.OfType<ExifDirectoryBase>().FirstOrDefault();
            if (exifDir?.TryGetDateTime(ExifDirectoryBase.TagDateTime, out var dateTime) == true)
                return DateOnly.FromDateTime(dateTime);
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "EXIF DateTime extraction failed");
        }
        return null;
    }

    private DateOnly? TryExtractQuickTimeCreationDate(IReadOnlyList<MetadataDirectory> directories)
    {
        try
        {
            var qtDir = directories.FirstOrDefault(d => d.Name.Contains("QuickTime", StringComparison.OrdinalIgnoreCase));
            if (qtDir?.TryGetDateTime(QuickTimeCreationDateTag, out var dateTime) == true)
                return DateOnly.FromDateTime(dateTime);
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "QuickTime CreationDate extraction failed");
        }
        return null;
    }

    private DateOnly? TryExtractXmpCreateDate(IReadOnlyList<MetadataDirectory> directories)
    {
        try
        {
            var xmpDir = directories.OfType<XmpDirectory>().FirstOrDefault();
            if (xmpDir != null)
            {
                var createDate = xmpDir.GetDescription(XmpCreateDateTag);
                if (DateTime.TryParse(createDate, out var dt))
                    return DateOnly.FromDateTime(dt);
            }
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "XMP CreateDate extraction failed");
        }
        return null;
    }

    private DateOnly? TryExtractXmpModifyDate(IReadOnlyList<MetadataDirectory> directories)
    {
        try
        {
            var xmpDir = directories.OfType<XmpDirectory>().FirstOrDefault();
            if (xmpDir != null)
            {
                var modifyDate = xmpDir.GetDescription(XmpModifyDateTag);
                if (DateTime.TryParse(modifyDate, out var dt))
                    return DateOnly.FromDateTime(dt);
            }
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "XMP ModifyDate extraction failed");
        }
        return null;
    }

    private DateOnly? TryGetFileSystemCreationTime(string filePath)
    {
        try
        {
            var creationTime = File.GetCreationTimeUtc(filePath);
            if (creationTime > DateTime.MinValue && creationTime < DateTime.MaxValue)
                return DateOnly.FromDateTime(creationTime);
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "File system creation time failed for {File}", filePath);
        }
        return null;
    }

    private DateOnly? TryGetFileSystemLastWriteTime(string filePath)
    {
        try
        {
            var lastWrite = File.GetLastWriteTimeUtc(filePath);
            if (lastWrite > DateTime.MinValue && lastWrite < DateTime.MaxValue)
                return DateOnly.FromDateTime(lastWrite);
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "File system last write time failed for {File}", filePath);
        }
        return null;
    }
}
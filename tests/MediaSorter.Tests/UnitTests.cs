using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using MediaSorter.Models;
using MediaSorter.Services.Metadata;
using MediaSorter.Services.Organization;
using MediaSorter.Helpers;

namespace MediaSorter.Tests;

public class FileNameDateParserTests
{
    private readonly FileNameDateParser _parser;

    public FileNameDateParserTests()
    {
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<FileNameDateParser>.Instance;
        _parser = new FileNameDateParser(logger);
    }

    [Theory]
    [InlineData("IMG_20240315_123456.jpg", 2024, 3, 15)]
    [InlineData("DSC_20240315_123456.jpg", 2024, 3, 15)]
    [InlineData("VID_20240315_123456.mp4", 2024, 3, 15)]
    [InlineData("20240315_123456.jpg", 2024, 3, 15)]
    [InlineData("IMG_20240315.jpg", 2024, 3, 15)]
    [InlineData("PANO_20240315_001.jpg", 2024, 3, 15)]
    [InlineData("HDR_20240315_001.jpg", 2024, 3, 15)]
    [InlineData("Screenshot_20240315-123456.png", 2024, 3, 15)]
    [InlineData("Снимок экрана 2024-03-15 12.34.56.png", 2024, 3, 15)]
    [InlineData("WhatsApp Image 2024-03-15 at 12.34.56.jpg", 2024, 3, 15)]
    [InlineData("Photo_2024-03-15_12-34-56.jpg", 2024, 3, 15)]
    [InlineData("Video_2024-03-15_12-34-56.mp4", 2024, 3, 15)]
    public void ParseFromFileName_ShouldExtractDate_FromVariousFormats(string fileName, int year, int month, int day)
    {
        var result = _parser.ParseFromFileName(fileName);
        
        result.Should().NotBeNull();
        result!.Value.Year.Should().Be(year);
        result.Value.Month.Should().Be(month);
        result.Value.Day.Should().Be(day);
    }

    [Theory]
    [InlineData("IMG_123456.jpg")]
    [InlineData("DSC001.jpg")]
    [InlineData("random_name.jpg")]
    [InlineData("image.png")]
    [InlineData("photo")]
    [InlineData("")]
    [InlineData(null)]
    public void ParseFromFileName_ShouldReturnNull_WhenNoDatePattern(string? fileName)
    {
        var result = _parser.ParseFromFileName(fileName);
        
        result.Should().BeNull();
    }

    [Theory]
    [InlineData("15.03.2024.jpg", 2024, 3, 15)]
    [InlineData("15-03-2024.jpg", 2024, 3, 15)]
    [InlineData("2024_03_15.jpg", 2024, 3, 15)]
    public void ParseFromFileName_ShouldHandleDDMMYYYYFormat(string fileName, int year, int month, int day)
    {
        var result = _parser.ParseFromFileName(fileName);
        
        result.Should().NotBeNull();
        result!.Value.Year.Should().Be(year);
        result.Value.Month.Should().Be(month);
        result.Value.Day.Should().Be(day);
    }

    [Theory]
    [InlineData("20241332.jpg")] // Invalid month
    [InlineData("20240230.jpg")] // Invalid day
    public void ParseFromFileName_ShouldReturnNull_ForInvalidDates(string fileName)
    {
        var result = _parser.ParseFromFileName(fileName);
        
        result.Should().BeNull();
    }

[Theory]
    [InlineData("20240315.jpg", 2024, 3, 15)]
    [InlineData("19000315.jpg", 1900, 3, 15)]
    public void ParseFromFileName_ShouldHandleBoundaryYears(string fileName, int year, int month, int day)
    {
        var result = _parser.ParseFromFileName(fileName);
        
        result.Should().NotBeNull();
        result!.Value.Year.Should().Be(year);
        result.Value.Month.Should().Be(month);
        result.Value.Day.Should().Be(day);
    }
}

public class RegexPatternsTests
{
    [Theory]
    [InlineData("12.06.2010", true)]
    [InlineData("01.01.2000", true)]
    [InlineData("31.12.2099", true)]
    [InlineData("2024-03-15", false)]
    [InlineData("15-06-2010", false)]
    [InlineData("12/06/2010", false)]
    [InlineData("12.06.10", false)]
    [InlineData("1.6.2010", false)]
    [InlineData("", false)]
    public void DateFolderPattern_ShouldMatchDDMMYYYYFormat(string input, bool expectedMatch)
    {
        var match = RegexPatterns.DateFolderPattern.IsMatch(input);
        match.Should().Be(expectedMatch);
    }

    [Theory]
    [InlineData("PANO_20240315.jpg")]
    [InlineData("PANORAMA_20240315.jpg")]
    [InlineData("HDR_20240315.jpg")]
    [InlineData("BRACKET_20240315.jpg")]
    [InlineData("AEB_20240315.jpg")]
    [InlineData("DJI_20240315.jpg")]
    [InlineData("IMG_20240315_PANO_001.jpg")]
    [InlineData("IMG_20240315_HDR_001.jpg")]
    public void PanoramaPatterns_ShouldDetectPanoramaKeywords(string fileName)
    {
        var match = RegexPatterns.PanoramaPatterns.IsMatch(fileName);
        match.Should().BeTrue();
    }

    [Theory]
    [InlineData("IMG_001.jpg")]
    [InlineData("DSC_002.jpg")]
    [InlineData("VID_003.mp4")]
    [InlineData("random.jpg")]
    public void PanoramaPatterns_ShouldNotMatchRegularFiles(string fileName)
    {
        var match = RegexPatterns.PanoramaPatterns.IsMatch(fileName);
        match.Should().BeFalse();
    }

    [Theory]
    [InlineData(".jpg")]
    [InlineData(".JPG")]
    [InlineData(".jpeg")]
    [InlineData(".png")]
    [InlineData(".heic")]
    [InlineData(".heif")]
    [InlineData(".tiff")]
    [InlineData(".bmp")]
    [InlineData(".webp")]
    public void SupportedExtensions_ShouldContainCommonImageFormats(string ext)
    {
        RegexPatterns.SupportedExtensions.Should().Contain(e => e.Equals(ext, StringComparison.OrdinalIgnoreCase));
    }
}

public class HashHelperTests
{
    [Fact]
    public void ComputeXxHash3_ShouldReturnSameHash_ForIdenticalContent()
    {
        var content1 = "test content"u8.ToArray();
        var content2 = "test content"u8.ToArray();

        var hash1 = HashHelper.ComputeXxHash3(content1);
        var hash2 = HashHelper.ComputeXxHash3(content2);

        hash1.Should().Be(hash2);
    }

    [Fact]
    public void ComputeXxHash3_ShouldReturnDifferentHash_ForDifferentContent()
    {
        var content1 = "content 1"u8.ToArray();
        var content2 = "content 2"u8.ToArray();

        var hash1 = HashHelper.ComputeXxHash3(content1);
        var hash2 = HashHelper.ComputeXxHash3(content2);

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void ShortHash_ShouldBe16HexChars()
    {
        var hash = 0x12345678ABCDEF01ul;
        var shortHash = HashHelper.ShortHash(hash);
        
        shortHash.Should().HaveLength(16);
        shortHash.Should().MatchRegex("^[0-9A-F]{16}$");
    }

    [Fact]
    public async Task ComputeXxHash3Async_ShouldWorkWithStream()
    {
        using var stream = new MemoryStream("async test content"u8.ToArray());
        var hash = await HashHelper.ComputeXxHash3Async(stream);
        
        hash.Should().BeGreaterThan(0);
    }
}

public class LongPathHelperTests
{
    [Fact]
    public void ToExtendedLengthPath_ShouldAddPrefix_ForLongPaths()
    {
        var longPath = new string('a', 300);
        var result = LongPathHelper.ToExtendedLengthPath(longPath);
        
        result.Should().StartWith(@"\\?\");
    }

    [Fact]
    public void ToExtendedLengthPath_ShouldNotModify_ShortPaths()
    {
        var shortPath = @"C:\short\path";
        var result = LongPathHelper.ToExtendedLengthPath(shortPath);
        
        result.Should().NotStartWith(@"\\?\");
    }

    [Fact]
    public void ToExtendedLengthPath_ShouldHandleUNCPaths()
    {
        var uncPath = @"\\server\share\path";
        var result = LongPathHelper.ToExtendedLengthPath(uncPath);
        
        result.Should().StartWith(@"\\?\UNC\");
    }
}

public class PhotoFileTests
{
    [Fact]
    public void PhotoFile_ShouldCalculatePropertiesCorrectly()
    {
        var photo = new PhotoFile(
            SourcePath: @"C:\Photos\2024\03\15\IMG_1234.jpg",
            DateTaken: new DateOnly(2024, 3, 15),
            DateSource: DateSource.ExifDateTimeOriginal,
            FileSize: 1024000,
            FileName: "IMG_1234.jpg",
            Extension: ".jpg",
            RelativePath: @"2024\03\15\IMG_1234.jpg"
        );

        photo.HasDate.Should().BeTrue();
        photo.GetTargetFolderName("dd.MM.yyyy", "unknown").Should().Be("15.03.2024");
        photo.SourceDirectory.Should().Be(@"C:\Photos\2024\03\15");
    }

    [Fact]
    public void PhotoFile_ShouldHandleMissingDate()
    {
        var photo = new PhotoFile(
            SourcePath: @"C:\Photos\IMG_1234.jpg",
            DateTaken: null,
            DateSource: DateSource.Unknown,
            FileSize: 1024000,
            FileName: "IMG_1234.jpg",
            Extension: ".jpg",
            RelativePath: "IMG_1234.jpg"
        );

        photo.HasDate.Should().BeFalse();
        photo.GetTargetFolderName("dd.MM.yyyy", "unknown").Should().Be("unknown");
    }
}

public class ScanResultTests
{
    [Fact]
    public void ScanResult_ShouldCalculateNewPhotosCorrectly()
    {
        var result = new ScanResult(
            Photos: new List<PhotoFile>
            {
                new("", new DateOnly(2024, 3, 15), DateSource.ExifDateTimeOriginal, 100, "1.jpg", ".jpg", ""),
                new("", new DateOnly(2024, 3, 15), DateSource.ExifDateTimeOriginal, 100, "2.jpg", ".jpg", ""),
                new("", null, DateSource.Unknown, 100, "3.jpg", ".jpg", ""),
                new("", new DateOnly(2024, 3, 15), DateSource.ExifDateTimeOriginal, 100, "4.jpg", ".jpg", "", Status: FileStatus.SkippedAlreadySorted)
            },
            TotalScanned: 4,
            WithDate: 3,
            WithoutDate: 1,
            AlreadySorted: 1,
            Errors: 0,
            Duration: TimeSpan.FromSeconds(1)
        );

        // NewPhotos = photos with date that are NOT already sorted
        // Items 1, 2 have dates and are not skipped = 2
        // Item 3 has no date = excluded
        // Item 4 has date but is SkippedAlreadySorted = excluded
        // Result: 2
        result.NewPhotos.Should().Be(2);
    }
}

public class OrganizeProgressTests
{
    [Fact]
    public void OrganizeProgress_ShouldTrackAllCounters()
    {
        var progress = new OrganizeProgress(
            Current: 50,
            Total: 100,
            Moved: 45,
            Skipped: 3,
            Errors: 2,
            CurrentFile: "test.jpg"
        );

        progress.Current.Should().Be(50);
        progress.Total.Should().Be(100);
        progress.Moved.Should().Be(45);
        progress.Skipped.Should().Be(3);
        progress.Errors.Should().Be(2);
        progress.CurrentFile.Should().Be("test.jpg");
    }
}

public class ScanProgressTests
{
    [Fact]
    public void ScanProgress_ShouldTrackProgress()
    {
        var progress = new ScanProgress(
            ProcessedFiles: 50,
            TotalFiles: 100,
            CurrentFile: "test.jpg",
            CurrentDateSource: DateSource.ExifDateTimeOriginal
        );

        progress.ProcessedFiles.Should().Be(50);
        progress.TotalFiles.Should().Be(100);
        progress.CurrentFile.Should().Be("test.jpg");
        progress.CurrentDateSource.Should().Be(DateSource.ExifDateTimeOriginal);
    }
}

public class OrganizeResultTests
{
    [Fact]
    public void OrganizeResult_ShouldCalculateTotalProcessed()
    {
        var result = new OrganizeResult(
            Moved: 50,
            Skipped: 5,
            SkippedDuplicates: 3,
            SkippedAlreadySorted: 2,
            Errors: 1,
            ErrorMessages: new List<string> { "Error 1" },
            Duration: TimeSpan.FromSeconds(10),
            RollbackJournalPath: null
        );

        result.TotalProcessed.Should().Be(50 + 5 + 3 + 2 + 1);
    }
}
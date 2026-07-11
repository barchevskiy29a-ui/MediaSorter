using System.Text.RegularExpressions;

namespace MediaSorter.Helpers;

public static class RegexPatterns
{
    public static readonly string[] SupportedExtensions = 
    [
        ".jpg", ".jpeg", ".jpe", ".jfif",
        ".png",
        ".heic", ".heif",
        ".tiff", ".tif",
        ".bmp",
        ".webp",
        ".mp4", ".m4v",
        ".mov", ".qt",
        ".avi",
        ".mkv",
        ".wmv",
        ".webm",
        ".mts", ".m2ts",
        ".mpeg", ".mpg",
        ".3gp"
    ];

    // Folder name pattern: DD.MM.YYYY
    public static readonly Regex DateFolderPattern = new(@"^\d{2}\.\d{2}\.\d{4}$", RegexOptions.Compiled);
    
    // Unknown date folder
    public const string UnknownDateFolderName = "дата съемки неопределена";

    // File name date patterns
    public static readonly Regex[] FileNameDatePatterns =
    [
        // YYYYMMDD
        new(@"^(?:IMG|DSC|VID|PANO|HDR|Screenshot)?_?(\d{4})(\d{2})(\d{2})", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        
        // YYYY-MM-DD
        new(@"(\d{4})[-](\d{2})[-](\d{2})", RegexOptions.Compiled),
        
        // DD.MM.YYYY
        new(@"(\d{2})[.](\d{2})[.](\d{4})", RegexOptions.Compiled),
        
        // DD-MM-YYYY
        new(@"(\d{2})[-](\d{2})[-](\d{4})", RegexOptions.Compiled),
        
        // YYYY_MM_DD
        new(@"(\d{4})[_](\d{2})[_](\d{2})", RegexOptions.Compiled),
        
        // WhatsApp: "WhatsApp Image 2024-03-15 at 12.34.56"
        new(@"WhatsApp\s+Image\s+(\d{4})[-](\d{2})[-](\d{2})", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        
        // Telegram: "Photo_2024-03-15_12-34-56"
        new(@"(?:Photo|Video|Document)_(\d{4})[-](\d{2})[-](\d{2})", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        
        // Screenshot: "Screenshot_20240315-123456" or "Снимок экрана 2024-03-15"
        new(@"(?:Screenshot|Снимок\s+экрана|Снимок_экрана)[_\s-]?(\d{4})[-_]?(\d{2})[-_]?(\d{2})", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    ];

    // Panorama/HDR detection
    public static readonly Regex PanoramaPatterns = new(
        @"(PANO|PANORAMA|HDR|BRACKET|AEB|DJI_|_PANO_|_HDR_)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Sequence number for panoramas
    public static readonly Regex SequenceNumber = new(
        @"[_\-](?<seq>\d{3,4})(?=[_\-\.]|$)",
        RegexOptions.Compiled);
}
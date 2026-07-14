using System.IO;

namespace MediaSorter.Helpers;

public static class LongPathHelper
{
    private const string LongPathPrefix = @"\\?\";

    /// <summary>
    /// Converts a path to extended-length path format if needed (>260 chars)
    /// </summary>
    public static string ToExtendedLengthPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return path;

        // Already has prefix
        if (path.StartsWith(LongPathPrefix, StringComparison.Ordinal))
            return path;

        // Get full path
        var fullPath = Path.GetFullPath(path);
        
        // Check if UNC path
        var isUnc = fullPath.StartsWith(@"\\", StringComparison.Ordinal);
        
        // Only add prefix if path is long enough, contains problematic chars, or is UNC
        if (fullPath.Length >= 260 || fullPath.Contains(' ') || fullPath.Contains('.') || isUnc)
        {
            // For UNC paths, need different handling
            if (isUnc)
            {
                return @"\\?\UNC\" + fullPath.Substring(2);
            }
            return LongPathPrefix + fullPath;
        }

        return fullPath;
    }


}
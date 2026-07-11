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

    /// <summary>
    /// Ensures directory exists with long path support
    /// </summary>
    public static void EnsureDirectoryExists(string path)
    {
        var extendedPath = ToExtendedLengthPath(path);
        if (!Directory.Exists(extendedPath))
        {
            Directory.CreateDirectory(extendedPath);
        }
    }

    /// <summary>
    /// Checks if file exists with long path support
    /// </summary>
    public static bool FileExists(string path)
    {
        return File.Exists(ToExtendedLengthPath(path));
    }

    /// <summary>
    /// Checks if directory exists with long path support
    /// </summary>
    public static bool DirectoryExists(string path)
    {
        return Directory.Exists(ToExtendedLengthPath(path));
    }

    /// <summary>
    /// Moves file with long path support
    /// </summary>
    public static void MoveFile(string source, string dest, bool overwrite = false)
    {
        var extSource = ToExtendedLengthPath(source);
        var extDest = ToExtendedLengthPath(dest);
        
        if (overwrite)
        {
            try
            {
                if (File.Exists(extDest))
                    File.Delete(extDest);
            }
            catch (IOException)
            {
            }
        }
        
        File.Move(extSource, extDest, overwrite);
    }

    /// <summary>
    /// Opens file for reading with long path support
    /// </summary>
    public static FileStream OpenRead(string path)
    {
        return new FileStream(ToExtendedLengthPath(path), FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    /// <summary>
    /// Gets file info with long path support
    /// </summary>
    public static FileInfo GetFileInfo(string path)
    {
        return new FileInfo(ToExtendedLengthPath(path));
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace MediaSorter.Services.System;

public class FileSystemHelper
{
    private readonly ILogger<FileSystemHelper> _logger;

    public FileSystemHelper(ILogger<FileSystemHelper> logger)
    {
        _logger = logger;
    }

    public List<string> EnumerateFiles(string rootPath, IEnumerable<string> extensions, CancellationToken ct)
    {
        var result = new List<string>();
        var extSet = new HashSet<string>(extensions, StringComparer.OrdinalIgnoreCase);
        var longRoot = ToLongPath(rootPath);
        
        if (!Directory.Exists(longRoot))
        {
            _logger.LogWarning("Root folder does not exist: {Path}", rootPath);
            return result;
        }

        var stack = new Stack<string>();
        stack.Push(longRoot);

        while (stack.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var currentDir = stack.Pop();

            string[] files;
            try
            {
                files = Directory.GetFiles(currentDir);
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error reading directory: {Dir}", currentDir);
                continue;
            }

            foreach (var file in files)
            {
                ct.ThrowIfCancellationRequested();
                var ext = Path.GetExtension(file);
                if (extSet.Contains(ext))
                    result.Add(file);
            }

            try
            {
                foreach (var subDir in Directory.GetDirectories(currentDir))
                {
                    stack.Push(subDir);
                }
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
        
        return result;
    }

    public string ToLongPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;
        var fullPath = Path.GetFullPath(path);
        if (fullPath.Length >= 260 && !fullPath.StartsWith(@"\\?\"))
        {
            if (fullPath.StartsWith(@"\\", StringComparison.Ordinal))
                return @"\\?\UNC\" + fullPath.Substring(2);
            return @"\\?\" + fullPath;
        }
        return fullPath;
    }

    public bool CanRead(string path)
    {
        try { using var s = File.OpenRead(ToLongPath(path)); return true; }
        catch { return false; }
    }

    public bool CanWrite(string path, out string errorMessage)
    {
        errorMessage = string.Empty;
        try
        {
            var longPath = ToLongPath(path);
            var dir = Path.GetDirectoryName(longPath) ?? longPath;
            var dirInfo = new DirectoryInfo(dir);
            var security = dirInfo.GetAccessControl();
            var rules = security.GetAccessRules(true, true, typeof(SecurityIdentifier));
            var currentUser = WindowsIdentity.GetCurrent().User;
            var currentGroups = WindowsIdentity.GetCurrent().Groups?.Select(g => g.Value).ToHashSet() ?? new HashSet<string>();
            bool hasWrite = false;
            foreach (FileSystemAccessRule rule in rules)
            {
                if ((rule.FileSystemRights & FileSystemRights.WriteData) == 0) continue;
                if (rule.IdentityReference.Value == currentUser?.Value || currentGroups.Contains(rule.IdentityReference.Value))
                {
                    if (rule.AccessControlType == AccessControlType.Allow) hasWrite = true;
                    else if (rule.AccessControlType == AccessControlType.Deny) { errorMessage = "Access denied"; return false; }
                }
            }
            if (!hasWrite) { errorMessage = "No write permission"; return false; }
            var testFile = Path.Combine(dir, $".MediaSorter_test_{Guid.NewGuid():N}.tmp");
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);
            return true;
        }
        catch (UnauthorizedAccessException) { errorMessage = "Access denied"; return false; }
        catch (Exception ex) { errorMessage = ex.Message; return false; }
    }

    public bool CanWrite(string path) => CanWrite(path, out _);

    public DateTime GetCreationTime(string path) { try { return File.GetCreationTimeUtc(ToLongPath(path)).ToLocalTime(); } catch { return DateTime.MinValue; } }
    public DateTime GetLastWriteTime(string path) { try { return File.GetLastWriteTimeUtc(ToLongPath(path)).ToLocalTime(); } catch { return DateTime.MinValue; } }
    public void EnsureDirectoryExists(string path) { var longPath = ToLongPath(path); if (!Directory.Exists(longPath)) Directory.CreateDirectory(longPath); }
    public string GetImmediateParentFolder(string filePath, string rootPath) { var longRoot = ToLongPath(rootPath); var longFile = ToLongPath(filePath); if (!longFile.StartsWith(longRoot, StringComparison.OrdinalIgnoreCase)) return string.Empty; var relative = longFile[longRoot.Length..].TrimStart('\\', '/'); var parts = relative.Split('\\', '/'); return parts.Length > 1 ? parts[0] : string.Empty; }
    public async Task<bool> RequestElevationAsync(string targetPath)
    {
        try
        {
            if (IsRunningAsAdmin())
            {
                var longPath = ToLongPath(targetPath);
                Directory.CreateDirectory(longPath);
                return Directory.Exists(longPath);
            }

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -Command \"New-Item -ItemType Directory -Path '{targetPath}' -Force | Out-Null\"",
                Verb = "runas",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            if (process == null) return false;
            await process.WaitForExitAsync();
            return process.ExitCode == 0 && Directory.Exists(targetPath);
        }
        catch
        {
            return false;
        }
    }
    public bool IsRunningAsAdmin() { using var identity = WindowsIdentity.GetCurrent(); var principal = new WindowsPrincipal(identity); return principal.IsInRole(WindowsBuiltInRole.Administrator); }
    public string GetRelativePath(string rootPath, string filePath) => Path.GetRelativePath(rootPath, filePath);
    public bool FileExists(string path) => File.Exists(ToLongPath(path));
    public bool DirectoryExists(string path) => Directory.Exists(ToLongPath(path));
    public void MoveFile(string source, string dest, bool overwrite = false) => File.Move(ToLongPath(source), ToLongPath(dest), overwrite);
    public void DeleteFile(string path) => File.Delete(ToLongPath(path));
}
using System.IO;
using Microsoft.Extensions.Logging;
using MediaSorter.Helpers;
using MediaSorter.Models;

namespace MediaSorter.Services.Metadata;

public class FileNameDateParser
{
    private readonly ILogger<FileNameDateParser> _logger;

    public FileNameDateParser(ILogger<FileNameDateParser> logger)
    {
        _logger = logger;
    }

    public DateOnly? ParseFromFileName(string filePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        return ParseFromString(fileName);
    }

    public DateOnly? ParseFromString(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        foreach (var pattern in RegexPatterns.FileNameDatePatterns)
        {
            var match = pattern.Match(input);
            if (match.Success)
            {
                try
                {
                    var groups = match.Groups;
                    int year, month, day;

                    // Check if pattern is DD.MM.YYYY or DD-MM-YYYY (day first)
                    var patternStr = pattern.ToString();
                    if (patternStr.Contains(@"(\d{2})[.](\d{2})[.](\d{4})") || 
                        patternStr.Contains(@"(\d{2})[-](\d{2})[-](\d{4})"))
                    {
                        // DD.MM.YYYY or DD-MM-YYYY
                        day = int.Parse(groups[1].Value);
                        month = int.Parse(groups[2].Value);
                        year = int.Parse(groups[3].Value);
                    }
                    else
                    {
                        // YYYY first formats
                        year = int.Parse(groups[1].Value);
                        month = int.Parse(groups[2].Value);
                        day = int.Parse(groups[3].Value);
                    }

                    if (ValidateDate(year, month, day))
                    {
                        return new DateOnly(year, month, day);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogTrace(ex, "Error parsing date from {Input} with pattern {Pattern}", input, pattern);
                }
            }
        }

        return null;
    }

private bool ValidateDate(int year, int month, int day)
        {
            if (year < 1900 || year > 2099) return false;
            if (month < 1 || month > 12) return false;
            if (day < 1 || day > 31) return false;
            
            try
            {
                new DateOnly(year, month, day);
                return true;
            }
            catch
            {
                return false;
            }
        }
}
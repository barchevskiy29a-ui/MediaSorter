using Microsoft.Extensions.Logging;
using MediaSorter.Models;
using MediaSorter.Helpers;
using System.IO;
using System.Text.RegularExpressions;

namespace MediaSorter.Services.Organization;

public interface IPanoramaDetector
{
    List<PhotoFile> DetectAndGroup(List<PhotoFile> photos);
}

public class PanoramaDetector : IPanoramaDetector
{
    private readonly ILogger<PanoramaDetector> _logger;
    private readonly Regex _sequencePattern = new(@"[_\-\s](?<seq>\d{3,4})(?=[_\-\.]|$)", RegexOptions.Compiled);
    private readonly Regex _panoramaKeywords = RegexPatterns.PanoramaPatterns;

    public PanoramaDetector(ILogger<PanoramaDetector> logger)
    {
        _logger = logger;
    }

public List<PhotoFile> DetectAndGroup(List<PhotoFile> photos)
        {
            var byDirectory = photos.GroupBy(p => p.SourceDirectory);

            foreach (var dirGroup in byDirectory)
            {
                var sorted = dirGroup.OrderBy(p => p.FileName).ToList();
                
                for (int i = 0; i < sorted.Count; i++)
                {
                    var current = sorted[i];
                    if (current.IsPanoramaPart) continue;

                    var isPano = IsPanoramaFile(current.FileName);
                    var isHdr = IsHdrFile(current.FileName);
                    
                    if (!isPano && !isHdr) continue;

                    var sequence = new List<PhotoFile> { current };
                    var baseName = GetBaseName(current.FileName);
                    
                    for (int j = i + 1; j < sorted.Count; j++)
                    {
                        var next = sorted[j];
                        if (IsSequenceMatch(baseName, next.FileName))
                        {
                            sequence.Add(next);
                        }
                        else if (sequence.Count == 1)
                        {
                            break;
                        }
                        else
                        {
                            break;
                        }
                    }

                    if (sequence.Count >= 2)
                    {
                        var groupId = sorted.IndexOf(current) + 1;
                        foreach (var p in sequence)
                        {
                            var index = photos.FindIndex(x => x.SourcePath == p.SourcePath);
                            if (index >= 0)
                            {
                                photos[index] = photos[index] with 
                                { 
                                    IsPanoramaPart = true, 
                                    PanoramaGroupId = sorted.IndexOf(current) + 1 
                                };
                            }
                        }
                        _logger.LogDebug("Обнаружена панорама/HDR группа: {Count} файлов в {Dir}", 
                            sequence.Count, dirGroup.Key);
                    }
                }
            }
            
            return photos;
        }

    private bool IsPanoramaFile(string fileName)
    {
        return _panoramaKeywords.IsMatch(fileName);
    }

    private bool IsHdrFile(string fileName)
    {
        return fileName.Contains("HDR", StringComparison.OrdinalIgnoreCase) ||
               fileName.Contains("BRACKET", StringComparison.OrdinalIgnoreCase) ||
               fileName.Contains("AEB", StringComparison.OrdinalIgnoreCase);
    }

    private string GetBaseName(string fileName)
    {
        var match = _sequencePattern.Match(fileName);
        if (match.Success)
        {
            return fileName[..match.Index];
        }
        return Path.GetFileNameWithoutExtension(fileName);
    }

    private bool IsSequenceMatch(string baseName, string nextFileName)
    {
        if (!nextFileName.StartsWith(baseName, StringComparison.OrdinalIgnoreCase))
            return false;

        var suffix = nextFileName[baseName.Length..];
        var match = _sequencePattern.Match(suffix);
        return match.Success;
    }
}
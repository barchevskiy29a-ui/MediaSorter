using System.IO;
using System.IO.Hashing;

namespace MediaSorter.Helpers;

public static class HashHelper
{
    private const long MaxHashFileSize = 10L * 1024 * 1024 * 1024; // 10 GB

    /// <summary>
    /// Computes xxHash3 (64-bit) hash of a file using streaming (memory efficient)
    /// </summary>
    public static ulong ComputeXxHash3(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        if (stream.Length > MaxHashFileSize)
            throw new InvalidOperationException($"File too large for hashing: {stream.Length} bytes");
        return ComputeXxHash3(stream);
    }

    /// <summary>
    /// Computes xxHash3 (64-bit) hash of a stream
    /// </summary>
    public static ulong ComputeXxHash3(Stream stream)
    {
        var hasher = new XxHash3();
        var buffer = new byte[81920]; // 80KB buffer
        int bytesRead;
        
        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            hasher.Append(buffer.AsSpan(0, bytesRead));
        }
        
        return hasher.GetCurrentHashAsUInt64();
    }

    /// <summary>
    /// Computes xxHash3 (64-bit) hash of a byte array
    /// </summary>
    public static ulong ComputeXxHash3(ReadOnlySpan<byte> data)
    {
        var hasher = new XxHash3();
        hasher.Append(data);
        return hasher.GetCurrentHashAsUInt64();
    }

    /// <summary>
    /// Short hash for file name suffix (16 hex chars - full 64-bit)
    /// </summary>
    public static string ShortHash(ulong hash)
    {
        return hash.ToString("X16");
    }

    /// <summary>
    /// Computes xxHash3 (64-bit) hash of a file asynchronously
    /// </summary>
    public static async Task<ulong> ComputeXxHash3Async(string filePath, CancellationToken ct = default)
    {
        await using var stream = File.OpenRead(filePath);
        if (stream.Length > MaxHashFileSize)
            throw new InvalidOperationException($"File too large for hashing: {stream.Length} bytes");
        return await ComputeXxHash3Async(stream, ct);
    }

    /// <summary>
    /// Computes xxHash3 (64-bit) hash of a stream asynchronously
    /// </summary>
    public static async Task<ulong> ComputeXxHash3Async(Stream stream, CancellationToken ct = default)
    {
        var hasher = new XxHash3();
        var buffer = new byte[81920]; // 80KB buffer
        int bytesRead;
        
        while ((bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
        {
            hasher.Append(buffer.AsSpan(0, bytesRead));
        }
        
        return hasher.GetCurrentHashAsUInt64();
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ZstdSharp;

namespace ExtCompress;

public class FileChunk
{
    public string RelativePath { get; set; }
    public bool IsCompressed { get; set; }
    public long OriginalSize { get; set; }
    public long CompressedSize { get; set; }
    public byte[] MemoryData { get; set; }
    public string DirectFilePath { get; set; }
}

public class ExtCompressEngine
{
    private static readonly byte[] MAGIC = { (byte)'E', (byte)'X', (byte)'T', (byte)'C' };
    private const byte VERSION = 1;
    private const string DEVELOPER_SIGNATURE = "Soluciones Digitales Camargo";

    private static readonly HashSet<string> BypassExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".zip", ".rar", ".7z", ".mp4", ".mkv", ".avi", ".jpg", ".png", ".pak", ".ff", ".gz", ".tar"
    };

    private static bool IsMatch(string text, string pattern)
    {
        if (string.IsNullOrEmpty(pattern)) return false;
        var parts = pattern.Split('|', StringSplitOptions.RemoveEmptyEntries);
        foreach (var p in parts)
        {
            string regex = "^" + Regex.Escape(p.Trim()).Replace("\\*", ".*").Replace("\\?", ".") + "$";
            if (Regex.IsMatch(text, regex, RegexOptions.IgnoreCase) || Regex.IsMatch(Path.GetFileName(text), regex, RegexOptions.IgnoreCase)) return true;
        }
        return false;
    }

    public static async Task CompressAsync(
        string[] inputFiles, 
        string outputPath, 
        ExtCompressOptions options,
        IProgress<long> progress, 
        CancellationToken ct)
    {
        var filesToProcess = new List<(string absolute, string relative)>();
        foreach (var path in inputFiles)
        {
            if (File.GetAttributes(path).HasFlag(FileAttributes.Directory))
            {
                // Limpiamos los separadores al final del directorio para asegurar el nombre correcto del relativo
                string cleanPath = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var parentDir = Path.GetDirectoryName(cleanPath);
                var allFiles = Directory.GetFiles(cleanPath, "*.*", SearchOption.AllDirectories);
                foreach (var f in allFiles)
                {
                    string rel = Path.GetRelativePath(parentDir, f);
                    if (!string.IsNullOrEmpty(options.Exclude) && IsMatch(rel, options.Exclude)) continue;
                    if (!string.IsNullOrEmpty(options.Include) && !IsMatch(rel, options.Include)) continue;
                    filesToProcess.Add((f, rel));
                }
            }
            else
            {
                string rel = Path.GetFileName(path);
                if (!string.IsNullOrEmpty(options.Exclude) && IsMatch(rel, options.Exclude)) continue;
                if (!string.IsNullOrEmpty(options.Include) && !IsMatch(rel, options.Include)) continue;
                filesToProcess.Add((path, rel));
            }
        }

        int maxDegree = options.Threads > 0 ? options.Threads : Math.Max(1, Environment.ProcessorCount - 2);

        using Stream fs = options.Benchmark ? Stream.Null : new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 1024 * 1024, useAsync: true);
        
        // Escribimos la firma del formato y metadatos de autoría
        await fs.WriteAsync(MAGIC, 0, MAGIC.Length, ct);
        fs.WriteByte(VERSION);

        byte[] devBytes = Encoding.UTF8.GetBytes(DEVELOPER_SIGNATURE);
        fs.WriteByte((byte)devBytes.Length);
        await fs.WriteAsync(devBytes, 0, devBytes.Length, ct);

        long bytesProcessedForProgress = 0;
        var writeLock = new SemaphoreSlim(1, 1);

        await Parallel.ForEachAsync(filesToProcess, new ParallelOptions { MaxDegreeOfParallelism = maxDegree, CancellationToken = ct }, async (fileInfo, token) =>
        {
            var chunk = await ProcessFileAsync(fileInfo.absolute, fileInfo.relative, options, token);

            byte[] pathBytes = Encoding.UTF8.GetBytes(chunk.RelativePath);
            byte[] header = new byte[4 + pathBytes.Length + 1 + 8 + 8];
            
            int offset = 0;
            BitConverter.GetBytes(pathBytes.Length).CopyTo(header, offset); offset += 4;
            pathBytes.CopyTo(header, offset); offset += pathBytes.Length;
            header[offset] = chunk.IsCompressed ? (byte)1 : (byte)0; offset += 1;
            BitConverter.GetBytes(chunk.OriginalSize).CopyTo(header, offset); offset += 8;
            BitConverter.GetBytes(chunk.CompressedSize).CopyTo(header, offset); offset += 8;

            await writeLock.WaitAsync(token);
            try
            {
                await fs.WriteAsync(header, 0, header.Length, token);

                if (chunk.MemoryData != null)
                {
                    await fs.WriteAsync(chunk.MemoryData, 0, chunk.MemoryData.Length, token);
                }
                else if (!string.IsNullOrEmpty(chunk.DirectFilePath))
                {
                    using var sourceStream = new FileStream(chunk.DirectFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 1024 * 1024, useAsync: true);
                    await sourceStream.CopyToAsync(fs, token);
                }

                long currentProgress = Interlocked.Add(ref bytesProcessedForProgress, chunk.OriginalSize);
                progress?.Report(currentProgress);
            }
            finally
            {
                writeLock.Release();
            }
        });
    }

    private static async Task<FileChunk> ProcessFileAsync(string absolutePath, string relativePath, ExtCompressOptions options, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var fileInfo = new FileInfo(absolutePath);
        long originalSize = fileInfo.Length;
        string ext = fileInfo.Extension;

        bool bypass = options.CompressionLevel <= 1 || BypassExtensions.Contains(ext) || originalSize > 100 * 1024 * 1024;

        if (bypass)
        {
            if (originalSize > 10 * 1024 * 1024)
            {
                return new FileChunk { RelativePath = relativePath, IsCompressed = false, OriginalSize = originalSize, CompressedSize = originalSize, DirectFilePath = absolutePath };
            }
            else
            {
                byte[] memData = await File.ReadAllBytesAsync(absolutePath, ct);
                return new FileChunk { RelativePath = relativePath, IsCompressed = false, OriginalSize = originalSize, CompressedSize = originalSize, MemoryData = memData };
            }
        }
        else
        {
            byte[] originalData = await File.ReadAllBytesAsync(absolutePath, ct);
            byte[] compressedData = await Task.Run(() => 
            {
                using var compressor = new Compressor(options.CompressionLevel);
                return compressor.Wrap(originalData).ToArray();
            }, ct);
            
            return new FileChunk { RelativePath = relativePath, IsCompressed = true, OriginalSize = originalSize, CompressedSize = compressedData.Length, MemoryData = compressedData };
        }
    }

    public static async Task DecompressAsync(string inputPath, string outputDir, string oldRootName, string newRootName, ExtCompressOptions options, IProgress<long> progress, CancellationToken ct)
    {
        using var fs = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 1024 * 1024, useAsync: true);
        
        byte[] magicBuffer = new byte[4];
        await fs.ReadExactlyAsync(magicBuffer, 0, 4, ct);
        if (!magicBuffer.SequenceEqual(MAGIC)) throw new Exception(LocalizationManager.Get("Engine_NotExtc"));
        
        int version = fs.ReadByte();
        if (version != VERSION) throw new Exception(LocalizationManager.Get("Engine_UnsupportedVersion"));

        // Verificamos metadatos de autoría
        int devSigLen = fs.ReadByte();
        if (devSigLen > 0)
        {
            byte[] devSigBytes = new byte[devSigLen];
            await fs.ReadExactlyAsync(devSigBytes, 0, devSigLen, ct);
            string devSig = Encoding.UTF8.GetString(devSigBytes);
            if (!devSig.Equals(DEVELOPER_SIGNATURE, StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception(LocalizationManager.Get("Engine_InvalidSignature"));
            }
        }
        else
        {
            throw new Exception(LocalizationManager.Get("Engine_MissingSignature"));
        }

        long totalSize = fs.Length;
        
        byte[] intBuffer = new byte[4];
        byte[] longBuffer = new byte[8];

        int maxDegree = Math.Max(1, Environment.ProcessorCount - 2);
        var semaphore = new SemaphoreSlim(maxDegree, maxDegree);
        var tasks = new List<Task>();
        long bytesProcessedForProgress = fs.Position;

        while (fs.Position < totalSize)
        {
            ct.ThrowIfCancellationRequested();

            await fs.ReadExactlyAsync(intBuffer, 0, 4, ct);
            int pathLen = BitConverter.ToInt32(intBuffer, 0);

            byte[] pathBytes = new byte[pathLen];
            await fs.ReadExactlyAsync(pathBytes, 0, pathLen, ct);
            string relativePath = Encoding.UTF8.GetString(pathBytes);

            if (!string.IsNullOrEmpty(oldRootName) && !string.IsNullOrEmpty(newRootName))
            {
                if (relativePath.StartsWith(oldRootName + "/", StringComparison.OrdinalIgnoreCase) || 
                    relativePath.StartsWith(oldRootName + "\\", StringComparison.OrdinalIgnoreCase))
                {
                    relativePath = newRootName + relativePath.Substring(oldRootName.Length);
                }
                else if (relativePath.Equals(oldRootName, StringComparison.OrdinalIgnoreCase))
                {
                    relativePath = newRootName;
                }
            }

            int isCompressedByte = fs.ReadByte();
            bool isCompressed = isCompressedByte == 1;

            await fs.ReadExactlyAsync(longBuffer, 0, 8, ct);
            long originalSize = BitConverter.ToInt64(longBuffer, 0);

            await fs.ReadExactlyAsync(longBuffer, 0, 8, ct);
            long compressedSize = BitConverter.ToInt64(longBuffer, 0);

            string fullDestPath = Path.Combine(outputDir, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullDestPath));

            long bytesToReport = 4 + pathLen + 1 + 16 + compressedSize;

            if (isCompressed)
            {
                byte[] compressedData = new byte[compressedSize];
                await fs.ReadExactlyAsync(compressedData, 0, (int)compressedSize, ct);
                
                await semaphore.WaitAsync(ct);
                var t = Task.Run(async () =>
                {
                    try
                    {
                        using var decompressor = new Decompressor();
                        byte[] uncompressedData = decompressor.Unwrap(compressedData).ToArray();
                        if (!options.Verify)
                        {
                            await File.WriteAllBytesAsync(fullDestPath, uncompressedData, ct);
                        }
                        
                        long currentProgress = Interlocked.Add(ref bytesProcessedForProgress, bytesToReport);
                        progress?.Report(currentProgress);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, ct);
                tasks.Add(t);
            }
            else
            {
                using Stream destStream = options.Verify ? Stream.Null : new FileStream(fullDestPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 1024 * 1024, useAsync: true);
                byte[] buffer = new byte[81920];
                long remaining = compressedSize;
                while (remaining > 0)
                {
                    ct.ThrowIfCancellationRequested();
                    int toRead = (int)Math.Min(buffer.Length, remaining);
                    int read = await fs.ReadAsync(buffer, 0, toRead, ct);
                    if (read == 0) break;
                    await destStream.WriteAsync(buffer, 0, read, ct);
                    remaining -= read;
                }

                long currentProgress = Interlocked.Add(ref bytesProcessedForProgress, bytesToReport);
                progress?.Report(currentProgress);
            }
        }

        await Task.WhenAll(tasks);
        progress?.Report(totalSize);
    }

    public static async Task<string> AnalyzeArchiveRootFolderAsync(string inputPath)
    {
        try
        {
            using var fs = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 256 * 1024, useAsync: true);
            
            byte[] magicBuffer = new byte[4];
            await fs.ReadExactlyAsync(magicBuffer, 0, 4);
            if (!magicBuffer.SequenceEqual(MAGIC)) return null;
            
            int version = fs.ReadByte();
            if (version != VERSION) return null;

            int devSigLen = fs.ReadByte();
            if (devSigLen > 0)
            {
                byte[] devSigBytes = new byte[devSigLen];
                await fs.ReadExactlyAsync(devSigBytes, 0, devSigLen);
            }

            long totalSize = fs.Length;
            byte[] intBuffer = new byte[4];
            byte[] longBuffer = new byte[8];

            string commonRoot = null;
            bool isFirst = true;

            while (fs.Position < totalSize)
            {
                await fs.ReadExactlyAsync(intBuffer, 0, 4);
                int pathLen = BitConverter.ToInt32(intBuffer, 0);

                byte[] pathBytes = new byte[pathLen];
                await fs.ReadExactlyAsync(pathBytes, 0, pathLen);
                string relativePath = Encoding.UTF8.GetString(pathBytes);

                fs.Seek(9, SeekOrigin.Current); // 1 byte isCompressed + 8 bytes originalSize

                await fs.ReadExactlyAsync(longBuffer, 0, 8);
                long compressedSize = BitConverter.ToInt64(longBuffer, 0);

                fs.Seek(compressedSize, SeekOrigin.Current);

                var parts = relativePath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 1)
                {
                    string root = parts[0];
                    if (isFirst)
                    {
                        commonRoot = root;
                        isFirst = false;
                    }
                    else if (commonRoot != null && !commonRoot.Equals(root, StringComparison.OrdinalIgnoreCase))
                    {
                        commonRoot = null;
                    }
                }
                else
                {
                    commonRoot = null;
                }
            }

            return commonRoot;
        }
        catch
        {
            return null;
        }
    }
}

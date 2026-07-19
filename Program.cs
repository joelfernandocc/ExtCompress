using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Text;
using System.Threading.Tasks;

namespace ExtCompress;

public class ExtCompressOptions
{
    public bool Silent { get; set; }
    public int CompressionLevel { get; set; } = 3;
    public int Threads { get; set; } = 0;
    public string Exclude { get; set; } = "";
    public string Include { get; set; } = "";
    public string Password { get; set; } = "";
    public bool Verify { get; set; }
    public bool Benchmark { get; set; }
    public string Format { get; set; } = "text";
    public string DictPath { get; set; } = "";
    public bool TrainDict { get; set; }
}

static class Program
{
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool AttachConsole(int dwProcessId);

    private const int ATTACH_PARENT_PROCESS = -1;

    [STAThread]
    static void Main(string[] args)
    {
        if (args.Length > 0)
        {
            AttachConsole(ATTACH_PARENT_PROCESS);
        }

        ApplicationConfiguration.Initialize();

        if (args.Length == 0)
        {
            PrintHelp();
            return;
        }

        var options = new ExtCompressOptions();
        var inputs = new List<string>();
        string action = null;
        string outputPath = null;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i].ToLowerInvariant();
            
            if (i == 0 && (arg == "compress" || arg == "extract" || arg == "help" || arg == "/compress" || arg == "/decompress" || arg == "/lightning"))
            {
                if (arg == "/compress") action = "compress";
                else if (arg == "/decompress") action = "extract";
                else if (arg == "/lightning") { action = "compress"; options.CompressionLevel = 1; }
                else action = arg;
                continue;
            }
            // For backward compatibility (unpack context menu)
            if (i == 0 && arg == "unpack")
            {
                action = "extract";
                continue;
            }

            if (arg == "--silent" || arg == "-s") options.Silent = true;
            else if (arg == "--verify") options.Verify = true;
            else if (arg == "--benchmark") options.Benchmark = true;
            else if (arg == "--train-dict") options.TrainDict = true;
            else if (arg == "--threads" && i + 1 < args.Length) { options.Threads = int.Parse(args[++i]); }
            else if (arg == "--level" && i + 1 < args.Length) { options.CompressionLevel = int.Parse(args[++i]); }
            else if (arg == "--exclude" && i + 1 < args.Length) { options.Exclude = args[++i]; }
            else if (arg == "--include" && i + 1 < args.Length) { options.Include = args[++i]; }
            else if (arg == "--password" && i + 1 < args.Length) { options.Password = args[++i]; }
            else if (arg == "--dict" && i + 1 < args.Length) { options.DictPath = args[++i]; }
            else if (arg == "--format" && i + 1 < args.Length) { options.Format = args[++i]; }
            else if ((arg == "--out" || arg == "/out") && i + 1 < args.Length) { outputPath = args[++i]; }
            else if (!arg.StartsWith("-"))
            {
                inputs.Add(args[i]);
            }
        }

        // Implicit action if a single file is passed
        if (action == null && inputs.Count > 0)
        {
            if (inputs[0].EndsWith(".extc", StringComparison.OrdinalIgnoreCase)) action = "extract";
            else action = "compress";
        }

        if (action == "help")
        {
            PrintHelp();
            return;
        }
        else if (action == "compress")
        {
            RunCompression(inputs, outputPath, options);
        }
        else if (action == "extract")
        {
            RunDecompression(inputs, outputPath, options);
        }
        else
        {
            Console.WriteLine(LocalizationManager.Get("CLI_InvalidAction", args[0]));
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine("\n" + LocalizationManager.Get("CLI_HelpTitle"));
        Console.WriteLine("Usage: extcompress <action> [inputs...] [options]");
        Console.WriteLine("\nActions:");
        Console.WriteLine("  compress   Compress files/directories");
        Console.WriteLine("  extract    Extract .extc archives");
        Console.WriteLine("  help       Show this help");
        Console.WriteLine("\nOptions:");
        Console.WriteLine("  --out <path>       Specify output file/directory");
        Console.WriteLine("  --level <1-22>     Compression level (default: 3, lower is faster)");
        Console.WriteLine("  --threads <N>      Number of threads (0 = auto)");
        Console.WriteLine("  --silent, -s       Run without UI/console output");
        Console.WriteLine("  --format <type>    Output format for console (text, json)");
        Console.WriteLine("  --exclude <pat>    Exclude files matching pattern");
        Console.WriteLine("  --include <pat>    Include only files matching pattern");
        Console.WriteLine("  --password <pwd>   Encrypt/Decrypt using AES-256");
        Console.WriteLine("  --verify           Verify archive integrity");
        Console.WriteLine("  --benchmark        Benchmark compression speed");
        Console.WriteLine("  --train-dict       Train Zstandard dictionary from inputs");
        Console.WriteLine("  --dict <path>      Use custom Zstandard dictionary");
        Console.WriteLine();
    }

    private static void RunCompression(List<string> inputList, string outputPath, ExtCompressOptions options)
    {
        if (inputList.Count == 0) return;

        bool isPrimary = false;
        Mutex mutex = new Mutex(true, "ExtCompress_Compression_Mutex_v2", out isPrimary);

        if (!isPrimary && !options.Silent && options.Format != "json")
        {
            try
            {
                using var client = new NamedPipeClientStream(".", "ExtCompress_Pipe_v2", PipeDirection.Out);
                client.Connect(500); 
                using var writer = new StreamWriter(client, Encoding.UTF8);
                foreach (var file in inputList) writer.WriteLine(file);
                writer.Flush();
            }
            catch { }
            return;
        }

        if (isPrimary && !options.Silent && options.Format != "json")
        {
            var cts = new CancellationTokenSource();
            Task.Run(() =>
            {
                try
                {
                    using var server = new NamedPipeServerStream("ExtCompress_Pipe_v2", PipeDirection.In, 1, PipeTransmissionMode.Message, PipeOptions.Asynchronous);
                    while (!cts.IsCancellationRequested)
                    {
                        var connectTask = server.WaitForConnectionAsync(cts.Token);
                        connectTask.Wait(cts.Token);
                        using var reader = new StreamReader(server, Encoding.UTF8, false, 1024, leaveOpen: true);
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            lock (inputList)
                            {
                                if (!inputList.Contains(line) && (File.Exists(line) || Directory.Exists(line)))
                                    inputList.Add(line);
                            }
                        }
                        server.Disconnect();
                    }
                }
                catch { }
            });
            Thread.Sleep(300);
            cts.Cancel();
        }

        if (isPrimary) { mutex.ReleaseMutex(); mutex.Dispose(); }

        if (inputList.Count > 1)
        {
            string parent = Path.GetDirectoryName(inputList[0].TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) ?? "";
            if (string.IsNullOrEmpty(outputPath)) outputPath = Path.Combine(parent, "Archivo.extc");
        }
        else if (string.IsNullOrEmpty(outputPath))
        {
            outputPath = inputList[0].TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + ".extc";
        }

        if (outputPath != null && outputPath.Contains($"{Path.DirectorySeparatorChar}.extc")) 
            outputPath = outputPath.Replace($"{Path.DirectorySeparatorChar}.extc", ".extc");

        outputPath = GetUniqueFilePath(outputPath);

        if (options.Silent || options.Format == "json")
        {
            RunHeadlessCompressionAsync(inputList, outputPath, options).GetAwaiter().GetResult();
        }
        else
        {
            var form = new ProgressForm();
            form.Shown += (s, e) => form.RunCompression(inputList.ToArray(), outputPath, options);
            Application.Run(form);
        }
    }

    private static async Task RunHeadlessCompressionAsync(List<string> inputList, string outputPath, ExtCompressOptions options)
    {
        var cts = new CancellationTokenSource();
        var progress = new Progress<long>(bytes => 
        {
            if (options.Format == "json") Console.WriteLine($"{{\"progress\": {bytes}}}");
        });

        try
        {
            await ExtCompressEngine.CompressAsync(inputList.ToArray(), outputPath, options, progress, cts.Token);
            if (options.Format == "json") Console.WriteLine("{\"status\": \"success\"}");
        }
        catch (Exception ex)
        {
            if (options.Format == "json") Console.WriteLine($"{{\"status\": \"error\", \"message\": \"{ex.Message}\"}}");
            else Console.WriteLine(LocalizationManager.Get("UI_Error") + ": " + ex.Message);
        }
    }

    private static string GetUniqueFilePath(string path)
    {
        if (!File.Exists(path)) return path;
        string dir = Path.GetDirectoryName(path);
        string name = Path.GetFileNameWithoutExtension(path);
        string ext = Path.GetExtension(path);
        int count = 1;
        string newPath;
        do
        {
            newPath = Path.Combine(dir, $"{name} ({count}){ext}");
            count++;
        } while (File.Exists(newPath));
        return newPath;
    }

    private static void RunDecompression(List<string> inputs, string outputDir, ExtCompressOptions options)
    {
        string inputPath = inputs.FirstOrDefault(a => a.EndsWith(".extc", StringComparison.OrdinalIgnoreCase));
        
        if (string.IsNullOrEmpty(inputPath))
        {
            Console.WriteLine(LocalizationManager.Get("Engine_NotExtc"));
            return;
        }

        string oldRoot = null;
        string newRoot = null;

        if (string.IsNullOrEmpty(outputDir))
        {
            string parentDir = Path.GetDirectoryName(inputPath) ?? "";
            string commonRoot = ExtCompressEngine.AnalyzeArchiveRootFolderAsync(inputPath).Result;
            if (!string.IsNullOrEmpty(commonRoot))
            {
                outputDir = parentDir;
                string potentialRootPath = Path.Combine(parentDir, commonRoot);
                string uniqueRootPath = GetUniqueDirectoryPath(potentialRootPath);
                
                if (uniqueRootPath != potentialRootPath)
                {
                    oldRoot = commonRoot;
                    newRoot = Path.GetFileName(uniqueRootPath);
                }
            }
            else
            {
                outputDir = Path.Combine(parentDir, Path.GetFileNameWithoutExtension(inputPath) + "_Extraido");
                outputDir = GetUniqueDirectoryPath(outputDir);
            }
        }

        if (options.Silent || options.Format == "json")
        {
            RunHeadlessDecompressionAsync(inputPath, outputDir, oldRoot, newRoot, options).GetAwaiter().GetResult();
        }
        else
        {
            var form = new ProgressForm();
            form.Shown += (s, e) => form.RunDecompression(inputPath, outputDir, oldRoot, newRoot, options);
            Application.Run(form);
        }
    }

    private static async Task RunHeadlessDecompressionAsync(string inputPath, string outputDir, string oldRoot, string newRoot, ExtCompressOptions options)
    {
        var cts = new CancellationTokenSource();
        var progress = new Progress<long>(bytes => 
        {
            if (options.Format == "json") Console.WriteLine($"{{\"progress\": {bytes}}}");
        });

        try
        {
            await ExtCompressEngine.DecompressAsync(inputPath, outputDir, oldRoot, newRoot, options, progress, cts.Token);
            if (options.Format == "json") Console.WriteLine("{\"status\": \"success\"}");
        }
        catch (Exception ex)
        {
            if (options.Format == "json") Console.WriteLine($"{{\"status\": \"error\", \"message\": \"{ex.Message}\"}}");
            else Console.WriteLine(LocalizationManager.Get("UI_Error") + ": " + ex.Message);
        }
    }

    private static string GetUniqueDirectoryPath(string path)
    {
        if (!Directory.Exists(path)) return path;
        string dir = Path.GetDirectoryName(path);
        string name = Path.GetFileName(path);
        int count = 1;
        string newPath;
        do
        {
            newPath = Path.Combine(dir, $"{name} ({count})");
            count++;
        } while (Directory.Exists(newPath));
        return newPath;
    }
}
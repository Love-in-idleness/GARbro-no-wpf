using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GARbro.Application;
using GameRes;

internal static class Program
{
    static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    static void Emit(object value) => Console.WriteLine(JsonSerializer.Serialize(value, Json));
    static async Task<int> Main(string[] args)
    {
        bool json = args.Contains("--json");
        using var cancel = new CancellationTokenSource();
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            cancel.Cancel();
        };
        try
        {
            if (args.Length == 0 || args[0] is "help" or "--help" or "-h")
            {
                Console.WriteLine("GARbro portable\n  formats [--type archive|image|audio|script] [--json]\n  probe FILE [--format TAG] [--json]\n  list FILE [--format TAG] [--json]\n  extract FILE... -o DIRECTORY [--overwrite fail|skip|replace] [--json]\n  convert FILE -o FILE [--to PNG|BMP|JPEG|TIFF|TGA] [--json]\nOptions: --options FILE, --scheme NAME, --recursive, --max-depth N, --max-entry BYTES, --max-total BYTES, --max-ratio N, --timeout SECONDS");
                return 0;
            }

            var values = new Dictionary<string, string>();
            var files = new List<string>();
            bool recursive = false;
            var allowed = new HashSet<string>
            {
                "-o",
                "--output",
                "--format",
                "--type",
                "--to",
                "--overwrite",
                "--options",
                "--scheme",
                "--max-depth",
                "--max-entry",
                "--max-total",
                "--max-ratio",
                "--timeout",
                "--schemes"
            };
            for (int i = 1; i < args.Length; i++)
            {
                if (args[i] == "--json")
                    continue;
                if (args[i] == "--recursive")
                {
                    recursive = true;
                    continue;
                }

                if (args[i].StartsWith('-'))
                {
                    if (!allowed.Contains(args[i]) || i + 1 >= args.Length)
                        throw new ArgumentException("Invalid or incomplete option: " + args[i]);
                    string name = args[i];
                    values[name] = args[++i];
                }
                else
                    files.Add(args[i]);
            }

            string Get(string key, string fallback = null) => values.TryGetValue(key, out var value) ? value : fallback;
            long Number(string key, long fallback) => values.TryGetValue(key, out var value) ? long.Parse(value, CultureInfo.InvariantCulture) : fallback;
            string output = Get("-o", Get("--output"));
            var overwrite = Get("--overwrite", "fail") switch
            {
                "fail" => OverwritePolicy.Fail,
                "skip" => OverwritePolicy.Skip,
                "replace" => OverwritePolicy.Replace,
                _ => throw new ArgumentException("Invalid overwrite policy.")};
            if (values.ContainsKey("--timeout"))
                cancel.CancelAfter(TimeSpan.FromSeconds(Number("--timeout", 0)));
            if (Get("--schemes")is string schemes)
                ArchiveService.LoadSchemes(schemes);
            var provider = new JsonOptionsProvider(Get("--options")is string config ? File.ReadAllText(config) : null, Get("--scheme"));
            if (args[0] == "formats")
            {
                var formats = ArchiveService.Formats().Where(f => Get("--type") == null || f.Type == Get("--type")).ToArray();
                if (json)
                    Emit(formats);
                else
                    foreach (var format in formats)
                        Console.WriteLine($"{format.Type}\t{format.Tag}\t{format.Description}");
                return 0;
            }

            if (files.Count == 0)
                throw new ArgumentException("At least one input file is required.");
            if (args[0] == "convert")
            {
                if (files.Count != 1 || output == null)
                    throw new ArgumentException("convert requires one input and an output file.");
                var target = Get("--to", Path.GetExtension(output).TrimStart('.').ToUpperInvariant());
                if (target == "WAV")
                {
                    ArchiveService.ConvertAudio(files[0], output, overwrite, cancel.Token, Number("--max-entry", 2L << 30));
                    if (json)
                        Emit(new { input = files[0], output, format = "WAV" });
                    else
                        Console.WriteLine(output);
                }
                else
                {
                    var image = ArchiveService.ReadImage(files[0], Get("--format"));
                    if (target == "JPG")
                        target = "JPEG";
                    if (target == "TIF")
                        target = "TIFF";
                    ArchiveService.WriteImage(image, output, target, overwrite);
                    if (json)
                        Emit(new { input = files[0], output, width = image.Width, height = image.Height, image.OffsetX, image.OffsetY });
                    else
                        Console.WriteLine(output);
                }

                return 0;
            }

            var inputs = new List<string>();
            foreach (var file in files)
            {
                if (Directory.Exists(file))
                {
                    if (!recursive)
                        throw new ArgumentException("A directory input requires --recursive.");
                    inputs.AddRange(Directory.EnumerateFiles(file, "*", new EnumerationOptions { RecurseSubdirectories = true, MaxRecursionDepth = checked((int)Number("--max-depth", 8)), AttributesToSkip = FileAttributes.ReparsePoint }).OrderBy(x => x, StringComparer.Ordinal));
                }
                else
                    inputs.Add(file);
            }

            if (inputs.Count == 0)
                throw new ArgumentException("No input files were found.");
            var results = new List<object>();
            var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var limits = new ExtractionLimits(Number("--max-entry", 2L << 30), Number("--max-total", 16L << 30), double.Parse(Get("--max-ratio", "10000"), CultureInfo.InvariantCulture));
            foreach (var file in inputs)
            {
                cancel.Token.ThrowIfCancellationRequested();
                if (args[0] == "probe")
                {
                    try
                    {
                        using var probe = ArchiveService.Open(file, Get("--format"), provider);
                        var result = new
                        {
                            input = file,
                            type = "archive",
                            format = probe.Tag,
                            count = probe.Entries.Count
                        };
                        results.Add(result);
                        if (!json)
                            Console.WriteLine($"{file}: {probe.Tag}, {probe.Entries.Count} entries");
                    }
                    catch (UnknownFormatException)
                    {
                        var result = ArchiveService.ProbeResource(file);
                        results.Add(result);
                        if (!json)
                            Console.WriteLine(JsonSerializer.Serialize(result, Json));
                    }

                    continue;
                }

                using var archive = ArchiveService.Open(file, Get("--format"), provider);
                switch (args[0])
                {
                    case "probe":
                        results.Add(new { input = file, type = "archive", format = archive.Tag, count = archive.Entries.Count });
                        if (!json)
                            Console.WriteLine($"{file}: {archive.Tag}, {archive.Entries.Count} entries");
                        break;
                    case "list":
                        results.Add(new { input = file, format = archive.Tag, entries = archive.Entries });
                        if (!json)
                            foreach (var entry in archive.Entries)
                                Console.WriteLine($"{entry.Size, 12}\t{entry.Name}");
                        break;
                    case "extract":
                        if (output == null)
                            throw new ArgumentException("extract requires -o DIRECTORY.");
                        var root = inputs.Count == 1 ? output : Path.Combine(output, Path.GetFileName(file));
                        if (!roots.Add(Path.GetFullPath(root)))
                            throw new IOException("Batch output directories collide: " + root);
                        var extracted = await archive.ExtractAsync(root, overwrite, limits, cancellation: cancel.Token);
                        results.Add(new { input = file, format = archive.Tag, output = root, files = extracted });
                        if (!json)
                            Console.WriteLine($"{file}: {extracted.Count} entries processed → {root}");
                        break;
                    default:
                        throw new ArgumentException("Unknown command: " + args[0]);
                }
            }

            if (json)
                Emit(results);
            return 0;
        }
        catch (Exception error)
        {
            int code = error switch
            {
                OperationCanceledException => 130,
                MissingOptionsException or UnknownEncryptionScheme or InvalidEncryptionScheme => 4,
                PlatformNotSupportedException or DllNotFoundException => 6,
                UnknownFormatException => 1,
                ArgumentException or FormatException or OverflowException => 2,
                InvalidDataException or InvalidFormatException or EndOfStreamException => 3,
                IOException or UnauthorizedAccessException => 5,
                _ => 7
            };
            if (json)
                Emit(new { error = error.GetType().Name, message = error.Message, exitCode = code });
            else
                Console.Error.WriteLine(error.Message);
            return code;
        }
    }
}

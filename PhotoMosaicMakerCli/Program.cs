using PhotoMosaicMaker.Core.Video;
using PhotoMosaicMaker.Core.Engine;
using PhotoMosaicMaker.Core.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace PhotoMosaicMakerCli
{
    internal class Program
    {
        static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                PrintUsage();
                return 2;
            }

            string cmd = args[0].ToLowerInvariant();

            if (cmd == "extract")
            {
                // extract --out "C:\temp\frames" --fps 1 --max 0 --video "a.mp4" --video "b.mp4"
                string? outFolder = GetArg(args, "--out");
                double fps = GetArgDouble(args, "--fps", 1.0);
                int max = GetArgInt(args, "--max", 0);

                var videos = GetArgsMulti(args, "--video");
                if (string.IsNullOrWhiteSpace(outFolder) == true || videos.Count == 0)
                {
                    PrintUsage();
                    return 2;
                }

                var extractor = new FfmpegFrameExtractor("ffmpeg");
                var opt = new VideoFrameExtractionOptions
                {
                    FramesPerSecond = fps,
                    MaxFramesPerVideo = max,
                    JpegQuality = 3
                };

                var progress = new Progress<VideoExtractionProgress>(p =>
                {
                    Console.WriteLine($"{p.Stage} {p.CurrentVideo}/{p.TotalVideos} : {p.VideoPath}");
                });

                extractor.ExtractFramesAsync(videos, outFolder, opt, progress, CancellationToken.None).GetAwaiter().GetResult();
                Console.WriteLine("OK");
                return 0;
            }

            if (cmd == "render")
            {
                // render --target "target.jpg" --sources "C:\imgs" --out "out.png" [--tile 24] [--width 1920] [--height 1080] [--grid 2] [--blur 0.0]
                string? target = GetArg(args, "--target");
                string? sources = GetArg(args, "--sources");
                string? outPath = GetArg(args, "--out");
                if (string.IsNullOrWhiteSpace(target) || string.IsNullOrWhiteSpace(sources) || string.IsNullOrWhiteSpace(outPath))
                {
                    PrintUsage();
                    return 2;
                }

                int tile = GetArgInt(args, "--tile", new MosaicSettings().TileSize);
                int width = GetArgInt(args, "--width", new MosaicSettings().OutputWidth);
                int height = GetArgInt(args, "--height", new MosaicSettings().OutputHeight);
                int grid = GetArgInt(args, "--grid", new MosaicSettings().MatchingGridSize);
                double blur = GetArgDouble(args, "--blur", 0.0);

                if (File.Exists(target) == false)
                {
                    Console.WriteLine($"Target not found: {target}");
                    return 2;
                }

                if (Directory.Exists(sources) == false)
                {
                    Console.WriteLine($"Sources folder not found: {sources}");
                    return 2;
                }

                var files = Directory.EnumerateFiles(sources)
                    .Where(p => p.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                             || p.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                             || p.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
                             || p.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase)
                             || p.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (files.Count == 0)
                {
                    Console.WriteLine("No source images found in sources folder.");
                    return 2;
                }

                var engine = new MosaicEngine();
                var settings = new MosaicSettings
                {
                    OutputWidth = width,
                    OutputHeight = height,
                    TileSize = tile,
                    ColorAdjustStrength = 0.35f,
                    UseSourcePatches = false,
                    MaxPatchReuse = 0,
                    MatchingGridSize = grid,
                    OutputBlurRadius = (float)blur
                };

                var prog = new Progress<MosaicProgress>(p =>
                {
                    if (p.Total > 0)
                    {
                        Console.WriteLine($"{p.Stage}: {p.Current}/{p.Total}");
                    }
                    else
                    {
                        Console.WriteLine($"{p.Stage}");
                    }
                });

                Console.WriteLine("Building patch library...");
                PatchLibrary lib;
                try
                {
                    lib = engine.BuildPatchLibrary(files, settings, grid, prog, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to build library: {ex.Message}");
                    return 2;
                }

                Console.WriteLine($"Library ready. Patches: {lib.Patches.Count}");
                Console.WriteLine("Rendering...");
                Image<Rgba32> result;
                try
                {
                    result = engine.Render(target, lib, settings, prog, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    lib.Dispose();
                    Console.WriteLine($"Render failed: {ex.Message}");
                    return 2;
                }

                try
                {
                    string ext = Path.GetExtension(outPath).ToLowerInvariant();
                    if (ext == ".jpg" || ext == ".jpeg")
                    {
                        result.SaveAsJpeg(outPath);
                    }
                    else
                    {
                        result.SaveAsPng(outPath);
                    }

                    Console.WriteLine($"Saved: {outPath}");
                }
                finally
                {
                    result.Dispose();
                    lib.Dispose();
                }

                return 0;
            }

            PrintUsage();
            return 2;
        }

        static List<string> GetArgsMulti(string[] args, string name)
        {
            var list = new List<string>();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase) == true)
                {
                    list.Add(args[i + 1]);
                }
            }
            return list;
        }

        static string? GetArg(string[] args, string name)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase) == true)
                {
                    return args[i + 1];
                }
            }
            return null;
        }

        static int GetArgInt(string[] args, string name, int defaultValue)
        {
            string? v = GetArg(args, name);
            if (int.TryParse(v, out int n) == true) return n;
            return defaultValue;
        }

        static double GetArgDouble(string[] args, string name, double defaultValue)
        {
            string? v = GetArg(args, name);
            if (double.TryParse(v, out double n) == true) return n;
            return defaultValue;
        }

        static void PrintUsage()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  extract --out <folder> --fps <double> --max <int> --video <file> [--video <file> ...]");
            Console.WriteLine("  render --target <file> --sources <folder> --out <file> [--tile <int>] [--width <int>] [--height <int>] [--grid <int>] [--blur <float>]");
            Console.WriteLine("Example:");
            Console.WriteLine("  extract --out \"C:\\temp\\frames\" --fps 1 --max 0 --video \"a.mp4\" --video \"b.mp4\"");
            Console.WriteLine("  render --target \"t.jpg\" --sources \"C:\\imgs\" --out \"out.png\" --tile 24 --width 1920 --height 1080 --blur 1.5");
        }
    }
}

using PhotoMosaicMaker.Core.Video;

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
            Console.WriteLine("Example:");
            Console.WriteLine("  extract --out \"C:\\temp\\frames\" --fps 1 --max 0 --video \"a.mp4\" --video \"b.mp4\"");
        }
    }
}

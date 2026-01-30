using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PhotoMosaicMaker.Core.Video
{
    public sealed record VideoInfo(int Width, int Height, double DurationSeconds);

    public static class VideoInfoReader
    {
        public static async Task<VideoInfo> ReadAsync(
            string ffprobePath,
            string inputMp4Path,
            CancellationToken cancellationToken)
        {
            // width/height + duration만 MVP에 충분
            string args =
                "-v error " +
                "-select_streams v:0 " +
                "-show_entries stream=width,height " +
                "-show_entries format=duration " +
                "-of json " +
                $"\"{inputMp4Path}\"";

            var psi = new ProcessStartInfo
            {
                FileName = ffprobePath,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var p = Process.Start(psi) ?? throw new InvalidOperationException("ffprobe 실행 실패");
            string stdout = await p.StandardOutput.ReadToEndAsync();
            string stderr = await p.StandardError.ReadToEndAsync();

            await p.WaitForExitAsync(cancellationToken);

            if (p.ExitCode != 0)
            {
                throw new InvalidOperationException($"ffprobe 실패: {stderr}");
            }

            using var doc = JsonDocument.Parse(stdout);

            int width = doc.RootElement.GetProperty("streams")[0].GetProperty("width").GetInt32();
            int height = doc.RootElement.GetProperty("streams")[0].GetProperty("height").GetInt32();

            string durStr = doc.RootElement.GetProperty("format").GetProperty("duration").GetString() ?? "0";
            double duration = double.Parse(durStr, CultureInfo.InvariantCulture);

            return new VideoInfo(width, height, duration);
        }
    }
}

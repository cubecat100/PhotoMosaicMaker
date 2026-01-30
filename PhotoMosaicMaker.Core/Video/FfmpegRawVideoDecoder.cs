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
    public sealed record VideoMeta(int Width, int Height, double DurationSeconds);

    public sealed class FfmpegRawVideoDecoder : IAsyncDisposable
    {
        private readonly Process _process;
        private readonly Task _stderrPump;
        private readonly StringBuilder _stderr = new();

        public int Width { get; }
        public int Height { get; }
        public int FrameSizeBytes { get; }

        public FfmpegRawVideoDecoder(
            string ffmpegPath,
            string inputMp4Path,
            int outWidth,
            int outHeight,
            int outFps)
        {
            Width = outWidth;
            Height = outHeight;
            FrameSizeBytes = checked(outWidth * outHeight * 4);

            // -vf: scale + fps + format=rgba
            // -an: 오디오 디코딩 불필요
            string args =
                "-hide_banner -loglevel error " +
                $"-i \"{inputMp4Path}\" " +
                "-an " +
                $"-vf \"scale={outWidth}:{outHeight}:flags=fast_bilinear,fps={outFps},format=rgba\" " +
                "-f rawvideo -pix_fmt rgba -";

            var psi = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            _process = Process.Start(psi) ?? throw new InvalidOperationException("ffmpeg(디코더) 실행 실패");

            _stderrPump = Task.Run(async () =>
            {
                while (_process.HasExited == false)
                {
                    string? line = await _process.StandardError.ReadLineAsync();
                    if (line == null)
                    {
                        break;
                    }
                    _stderr.AppendLine(line);
                }
            });
        }

        public async Task<bool> ReadFrameAsync(byte[] buffer, CancellationToken cancellationToken)
        {
            if (buffer.Length < FrameSizeBytes)
            {
                throw new ArgumentException("buffer가 프레임 크기보다 작습니다.", nameof(buffer));
            }

            int read = await ReadExactlyOrEofAsync(_process.StandardOutput.BaseStream, buffer, 0, FrameSizeBytes, cancellationToken);
            if (read == 0)
            {
                return false; // EOF
            }

            if (read != FrameSizeBytes)
            {
                throw new InvalidOperationException("프레임 읽기 중간에 EOF가 발생했습니다.");
            }

            return true;
        }

        private static async Task<int> ReadExactlyOrEofAsync(
            System.IO.Stream stream,
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            int total = 0;
            while (total < count)
            {
                int n = await stream.ReadAsync(buffer, offset + total, count - total, cancellationToken);
                if (n == 0)
                {
                    return total; // EOF
                }
                total += n;
            }
            return total;
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (_process.HasExited == false)
                {
                    _process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // ignore
            }

            try
            {
                await _stderrPump;
            }
            catch
            {
                // ignore
            }

            _process.Dispose();
        }

        public static async Task<VideoMeta> ReadAsync(string ffprobePath, string videoPath, CancellationToken token)
        {
            string args =
                "-v error " +
                "-select_streams v:0 " +
                "-show_entries stream=width,height " +
                "-show_entries format=duration " +
                "-of json " +
                $"\"{videoPath}\"";

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

            await p.WaitForExitAsync(token);

            if (p.ExitCode != 0)
            {
                throw new InvalidOperationException(stderr.Length > 0 ? stderr : "ffprobe 실패");
            }

            using var doc = JsonDocument.Parse(stdout);

            int w = doc.RootElement.GetProperty("streams")[0].GetProperty("width").GetInt32();
            int h = doc.RootElement.GetProperty("streams")[0].GetProperty("height").GetInt32();

            double dur = 0;
            if (doc.RootElement.TryGetProperty("format", out var fmt) &&
                fmt.TryGetProperty("duration", out var d) &&
                d.ValueKind == JsonValueKind.String)
            {
                _ = double.TryParse(d.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out dur);
            }

            return new VideoMeta(w, h, dur);
        }

        public string GetStderr() => _stderr.ToString();
    }
}

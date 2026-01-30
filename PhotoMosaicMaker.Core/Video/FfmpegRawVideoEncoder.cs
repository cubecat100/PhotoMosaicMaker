using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace PhotoMosaicMaker.Core.Video
{
    public sealed class FfmpegRawVideoEncoder : IAsyncDisposable
    {
        private readonly Process _process;
        private readonly Task _stderrPump;
        private readonly StringBuilder _stderr = new();

        private readonly int _frameSizeBytes;
        private readonly byte[] _fallbackCopyBuffer;

        public FfmpegRawVideoEncoder(
            string ffmpegPath,
            string outputMp4Path,
            int width,
            int height,
            int fps)
        {
            _frameSizeBytes = checked(width * height * 4);
            _fallbackCopyBuffer = new byte[_frameSizeBytes];

            // rawvideo stdin -> h264 mp4
            // preset/crf는 MVP 기본값(속도/품질 균형)
            string args =
                "-hide_banner -loglevel error " +
                $"-f rawvideo -pix_fmt rgba -s {width}x{height} -r {fps} -i - " +
                "-an " +
                "-c:v libx264 -preset veryfast -crf 20 -pix_fmt yuv420p " +
                $"\"{outputMp4Path}\"";

            var psi = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            _process = Process.Start(psi) ?? throw new InvalidOperationException("ffmpeg(인코더) 실행 실패");

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

        public ValueTask WriteFrameAsync(Image<Rgba32> frame, CancellationToken cancellationToken)
        {
            if (frame.DangerousTryGetSinglePixelMemory(out Memory<Rgba32> mem) == true)
            {
                ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(mem.Span);
                _process.StandardInput.BaseStream.Write(bytes);
                return ValueTask.CompletedTask;
            }

            frame.CopyPixelDataTo(_fallbackCopyBuffer);
            _process.StandardInput.BaseStream.Write(_fallbackCopyBuffer, 0, _fallbackCopyBuffer.Length);
            return ValueTask.CompletedTask;
        }

        public async Task FinishAsync(CancellationToken cancellationToken)
        {
            _process.StandardInput.Close();
            await _process.WaitForExitAsync(cancellationToken);

            if (_process.ExitCode != 0)
            {
                throw new InvalidOperationException($"ffmpeg 인코딩 실패: {_stderr}");
            }
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

        public string GetStderr() => _stderr.ToString();
    }
}

using PhotoMosaicMaker.Core.Engine;
using PhotoMosaicMaker.Core.Models;
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
    public sealed record VideoRenderProgress(string Stage, int CurrentFrame, int TotalFrames, double? InnerProgress);

    public sealed class VideoMosaicRenderer
    {
        private readonly MosaicEngine _engine;

        public VideoMosaicRenderer(MosaicEngine engine)
        {
            _engine = engine;
        }

        public async Task RenderAsync(
            string ffmpegPath,
            string ffprobePath,
            string inputMp4Path,
            string outputMp4Path,
            PatchLibrary library,
            MosaicSettings settings,
            int outputFps,
            IProgress<VideoRenderProgress>? progress,
            IProgress<MosaicProgress>? frameProgress,
            CancellationToken cancellationToken)
        {
            progress?.Report(new VideoRenderProgress("Probing", 0, 0, null));

            VideoInfo info = await VideoInfoReader.ReadAsync(ffprobePath, inputMp4Path, cancellationToken);

            // 진행률용 총 프레임(대략치): duration * outFps
            int totalFrames = (int)Math.Ceiling(info.DurationSeconds * outputFps);
            if (totalFrames < 1)
            {
                totalFrames = 1;
            }

            // 임시: 영상만 먼저 만든 다음, 마지막에 오디오 mux
            string tempVideoOnlyPath = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(outputMp4Path) ?? "",
                System.IO.Path.GetFileNameWithoutExtension(outputMp4Path) + ".video_only.mp4");

            // 프레임 raw 버퍼(재사용) + 입력 프레임 Image(재사용)
            int frameBytes = checked(settings.OutputWidth * settings.OutputHeight * 4);
            var raw = new byte[frameBytes];
            using var inputFrame = new Image<Rgba32>(settings.OutputWidth, settings.OutputHeight);

            await using var decoder = new FfmpegRawVideoDecoder(ffmpegPath, inputMp4Path, settings.OutputWidth, settings.OutputHeight, outputFps);
            await using var encoder = new FfmpegRawVideoEncoder(ffmpegPath, tempVideoOnlyPath, settings.OutputWidth, settings.OutputHeight, outputFps);

            int frameIndex = 0;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                progress?.Report(new VideoRenderProgress("Decoding", frameIndex, totalFrames, null));

                bool ok = await decoder.ReadFrameAsync(raw, cancellationToken);
                if (ok == false)
                {
                    break; // EOF
                }

                // raw bytes -> inputFrame 픽셀 메모리에 복사(이미지 객체 재사용)
                if (inputFrame.DangerousTryGetSinglePixelMemory(out Memory<Rgba32> mem) == false)
                {
                    throw new InvalidOperationException("inputFrame pixel memory 접근 실패");
                }

                CopyRawToImageRgba(raw, inputFrame, frameBytes);

                // 프레임 내부 진행률을 전체 진행률에 합치기
                double inner = 0;

                var wrappedFrameProgress = frameProgress == null ? null : new Progress<MosaicProgress>(p =>
                {
                    frameProgress.Report(p);
                    if (p.Total > 0)
                    {
                        inner = (double)p.Current / p.Total;
                        progress?.Report(new VideoRenderProgress("Rendering", frameIndex + 1, totalFrames, inner));
                    }
                    else
                    {
                        progress?.Report(new VideoRenderProgress("Rendering", frameIndex + 1, totalFrames, null));
                    }
                });

                progress?.Report(new VideoRenderProgress("Rendering", frameIndex + 1, totalFrames, null));

                using Image<Rgba32> mosaic = _engine.Render(inputFrame, library, settings, wrappedFrameProgress, cancellationToken);

                progress?.Report(new VideoRenderProgress("Encoding", frameIndex + 1, totalFrames, inner));
                await encoder.WriteFrameAsync(mosaic, cancellationToken);

                frameIndex++;
                progress?.Report(new VideoRenderProgress("Encoding", frameIndex, totalFrames, null));
            }

            progress?.Report(new VideoRenderProgress("Finishing", frameIndex, totalFrames, null));
            await encoder.FinishAsync(cancellationToken);

            // 오디오 mux (기본 ON: "알아서" → 있으면 포함, 없으면 스킵)
            progress?.Report(new VideoRenderProgress("MuxingAudio", frameIndex, totalFrames, null));
            await MuxAudioIfAnyAsync(ffmpegPath, tempVideoOnlyPath, inputMp4Path, outputMp4Path, cancellationToken);

            // 정리
            try
            {
                if (System.IO.File.Exists(tempVideoOnlyPath) == true)
                {
                    System.IO.File.Delete(tempVideoOnlyPath);
                }
            }
            catch
            {
                // ignore
            }

            progress?.Report(new VideoRenderProgress("Done", frameIndex, totalFrames, 1));
        }

        static void CopyRawToImageRgba(byte[] raw, Image<Rgba32> img, int frameBytes)
        {
            if (img.DangerousTryGetSinglePixelMemory(out Memory<Rgba32> mem) == false)
                throw new InvalidOperationException("pixel memory 접근 실패");

            Span<byte> dst = MemoryMarshal.AsBytes(mem.Span);
            raw.AsSpan(0, frameBytes).CopyTo(dst);
        }

        private static async Task MuxAudioIfAnyAsync(
            string ffmpegPath,
            string videoOnlyPath,
            string inputPath,
            string outputPath,
            CancellationToken cancellationToken)
        {
            // -map 1:a? : 오디오가 있으면 포함, 없으면 무시
            string args =
                "-hide_banner -loglevel error " +
                $"-i \"{videoOnlyPath}\" -i \"{inputPath}\" " +
                "-c copy -map 0:v:0 -map 1:a? -shortest " +
                $"\"{outputPath}\"";

            var psi = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var p = Process.Start(psi) ?? throw new InvalidOperationException("ffmpeg(mux) 실행 실패");
            string stderr = await p.StandardError.ReadToEndAsync();
            await p.WaitForExitAsync(cancellationToken);

            if (p.ExitCode != 0)
            {
                throw new InvalidOperationException($"오디오 mux 실패: {stderr}");
            }
        }
    }
}

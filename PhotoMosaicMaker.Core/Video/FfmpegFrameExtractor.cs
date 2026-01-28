using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhotoMosaicMaker.Core.Video
{
    public sealed class FfmpegFrameExtractor
    {
        private readonly string _ffmpegExe;

        // PATH에 ffmpeg가 잡혀 있으면 기본값 "ffmpeg"로 충분
        public FfmpegFrameExtractor(string ffmpegExe = "ffmpeg")
        {
            _ffmpegExe = ffmpegExe;
        }

        public async Task ExtractFramesAsync(
            IReadOnlyList<string> videoPaths,
            string outputFolder,
            VideoFrameExtractionOptions options,
            IProgress<VideoExtractionProgress>? progress,
            CancellationToken cancellationToken)
        {
            if (videoPaths == null || videoPaths.Count == 0)
            {
                throw new ArgumentException("동영상 파일이 없습니다.", nameof(videoPaths));
            }

            if (string.IsNullOrWhiteSpace(outputFolder) == true)
            {
                throw new ArgumentException("출력 폴더가 비어 있습니다.", nameof(outputFolder));
            }

            if (options.FramesPerSecond <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(options.FramesPerSecond));
            }

            Directory.CreateDirectory(outputFolder);

            for (int i = 0; i < videoPaths.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string videoPath = videoPaths[i];
                if (File.Exists(videoPath) == false)
                {
                    throw new FileNotFoundException("동영상 파일을 찾을 수 없습니다.", videoPath);
                }

                progress?.Report(new VideoExtractionProgress(VideoExtractionStage.StartingVideo, i + 1, videoPaths.Count, videoPath));

                string baseName = MakeSafeFileName(Path.GetFileNameWithoutExtension(videoPath));
                string outputPattern = Path.Combine(outputFolder, $"{baseName}_%06d.jpg");

                var psi = new ProcessStartInfo
                {
                    FileName = _ffmpegExe,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                psi.ArgumentList.Add("-hide_banner");
                psi.ArgumentList.Add("-loglevel");
                psi.ArgumentList.Add("error");

                psi.ArgumentList.Add("-i");
                psi.ArgumentList.Add(videoPath);

                psi.ArgumentList.Add("-vf");
                psi.ArgumentList.Add($"fps={options.FramesPerSecond.ToString(System.Globalization.CultureInfo.InvariantCulture)}");

                if (options.MaxFramesPerVideo > 0)
                {
                    psi.ArgumentList.Add("-frames:v");
                    psi.ArgumentList.Add(options.MaxFramesPerVideo.ToString());
                }

                psi.ArgumentList.Add("-q:v");
                psi.ArgumentList.Add(options.JpegQuality.ToString());

                psi.ArgumentList.Add(outputPattern);

                try
                {
                    using var process = Process.Start(psi);
                    if (process == null)
                    {
                        throw new InvalidOperationException("ffmpeg 실행에 실패했습니다.");
                    }

                    Task<string> errTask = process.StandardError.ReadToEndAsync(cancellationToken);

                    try
                    {
                        await process.WaitForExitAsync(cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        TryKill(process);
                        throw;
                    }

                    string err = await errTask;

                    if (process.ExitCode != 0)
                    {
                        throw new InvalidOperationException($"ffmpeg 실패(ExitCode={process.ExitCode}). {err}");
                    }
                }
                catch (Win32Exception)
                {
                    throw new InvalidOperationException("ffmpeg를 찾을 수 없습니다. FFmpeg 설치 후 PATH에 추가하거나 ffmpeg.exe 경로를 확인하세요.");
                }

                progress?.Report(new VideoExtractionProgress(VideoExtractionStage.FinishedVideo, i + 1, videoPaths.Count, videoPath));
            }
        }

        private static void TryKill(Process p)
        {
            try
            {
                if (p.HasExited == false)
                {
                    p.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // ignore
            }
        }

        private static string MakeSafeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return name;
        }
    }
}

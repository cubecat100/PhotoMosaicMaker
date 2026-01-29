using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhotoMosaicMaker.Core.Video
{
    public sealed class YoutubeVideoDownloader
    {
        public async Task<string> DownloadAsync(
            string url,
            string cacheFolder,
            YoutubeDownloadOptions options,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(url) == true)
            {
                throw new ArgumentException("YouTube URL이 비어 있습니다.", nameof(url));
            }

            Directory.CreateDirectory(cacheFolder);

            string baseName = $"yt_{Guid.NewGuid():N}";
            string outputTemplate = Path.Combine(cacheFolder, baseName + ".%(ext)s");

            var psi = new ProcessStartInfo
            {
                FileName = options.DownloaderExePath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            // 안정적으로 동작하는 최소 인자 세트
            // -o : 출력 템플릿
            // --no-playlist : 플레이리스트면 다 받아버리는 걸 방지
            psi.ArgumentList.Add("-o");
            psi.ArgumentList.Add(outputTemplate);

            if (options.NoPlaylist == true)
            {
                psi.ArgumentList.Add("--no-playlist");
            }

            // 최대 해상도 제한: 720p 이하에서 가능한 최선 선택
            if (options.MaxResolution > 0)
            {
                psi.ArgumentList.Add("-S");
                psi.ArgumentList.Add($"res:{options.MaxResolution}");
            }

            psi.ArgumentList.Add("--extractor-args");
            psi.ArgumentList.Add("youtube:player-client=tv_simply");

            psi.ArgumentList.Add(url);

            try
            {
                using var process = Process.Start(psi);
                if (process == null)
                {
                    throw new InvalidOperationException("yt-dlp 실행에 실패했습니다.");
                }

                Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
                Task<string> stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

                try
                {
                    await process.WaitForExitAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    TryKill(process);
                    throw;
                }

                string stdout = await stdoutTask;
                string stderr = await stderrTask;

                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException($"yt-dlp 실패(ExitCode={process.ExitCode}). {stderr}".Trim());
                }
            }
            catch (Win32Exception)
            {
                throw new InvalidOperationException("yt-dlp를 찾을 수 없습니다. yt-dlp 설치 후 PATH에 추가하거나 exe 경로를 지정하세요.");
            }

            // 결과 파일 찾기: baseName.* 중 가장 큰 파일을 채택
            var candidates = Directory.GetFiles(cacheFolder, baseName + ".*", SearchOption.TopDirectoryOnly)
                .Where(p => p.EndsWith(".part", StringComparison.OrdinalIgnoreCase) == false)
                .ToList();

            if (candidates.Count == 0)
            {
                throw new InvalidOperationException("다운로드는 성공했지만 결과 파일을 찾지 못했습니다.");
            }

            string best = candidates
                .Select(p => new FileInfo(p))
                .OrderByDescending(fi => fi.Length)
                .First()
                .FullName;

            return best;
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
    }
}

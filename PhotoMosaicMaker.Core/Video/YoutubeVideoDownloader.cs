using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PhotoMosaicMaker.Core.Video
{
    public sealed class YoutubeVideoDownloader
    {
        private static readonly HttpClient VersionCheckClient = CreateVersionCheckClient();

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

            try
            {
                var firstAttempt = await RunYtDlpAsync(
                    url, outputTemplate, options, useTvSimply: true, cancellationToken);

                if (firstAttempt.ExitCode != 0)
                {
                    var retry = await RunYtDlpAsync(
                        url, outputTemplate, options, useTvSimply: false, cancellationToken);

                    if (retry.ExitCode != 0)
                    {
                        string updateStatus = await GetUpdateStatusAsync(options.DownloaderExePath, cancellationToken);
                        throw new InvalidOperationException(
                            $"yt-dlp 강제 옵션 시도 실패(ExitCode={firstAttempt.ExitCode}). {firstAttempt.Error}\n\n" +
                            $"강제 옵션 없는 재시도 실패(ExitCode={retry.ExitCode}). {retry.Error}\n\n" +
                            updateStatus);
                    }
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

        private static async Task<(int ExitCode, string Error)> RunYtDlpAsync(
            string url,
            string outputTemplate,
            YoutubeDownloadOptions options,
            bool useTvSimply,
            CancellationToken cancellationToken)
        {
            var psi = new ProcessStartInfo
            {
                FileName = options.DownloaderExePath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            psi.ArgumentList.Add("-o");
            psi.ArgumentList.Add(outputTemplate);

            if (options.NoPlaylist == true)
            {
                psi.ArgumentList.Add("--no-playlist");
            }

            if (options.MaxResolution > 0)
            {
                psi.ArgumentList.Add("-S");
                psi.ArgumentList.Add($"res:{options.MaxResolution}");
            }

            if (useTvSimply == true)
            {
                psi.ArgumentList.Add("--extractor-args");
                psi.ArgumentList.Add("youtube:player-client=tv_simply");
            }

            psi.ArgumentList.Add(url);

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

            await stdoutTask;
            string stderr = await stderrTask;
            return (process.ExitCode, stderr);
        }

        private static async Task<string> GetUpdateStatusAsync(
            string downloaderExePath,
            CancellationToken cancellationToken)
        {
            try
            {
                string installedVersion = await GetInstalledVersionAsync(downloaderExePath, cancellationToken);
                string latestVersion = await GetLatestStableVersionAsync(cancellationToken);

                if (TryParseVersionDate(installedVersion, out DateTime installedDate) == false ||
                    TryParseVersionDate(latestVersion, out DateTime latestDate) == false)
                {
                    return $"yt-dlp 버전 판정 불가: 설치 {installedVersion}, 최신 stable {latestVersion}";
                }

                if (installedDate < latestDate)
                {
                    return $"yt-dlp 업데이트 필요: 설치 {installedVersion}, 최신 stable {latestVersion}";
                }

                return $"yt-dlp는 최신 버전입니다: 설치 {installedVersion}, 최신 stable {latestVersion}";
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                return "yt-dlp 최신 버전 여부를 확인하지 못했습니다.";
            }
        }

        private static async Task<string> GetInstalledVersionAsync(
            string downloaderExePath,
            CancellationToken cancellationToken)
        {
            var psi = new ProcessStartInfo
            {
                FileName = downloaderExePath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add("--version");

            using var process = Process.Start(psi);
            if (process == null)
            {
                throw new InvalidOperationException("yt-dlp 버전을 확인할 수 없습니다.");
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
            await stderrTask;

            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(stdout) == true)
            {
                throw new InvalidOperationException("yt-dlp 버전을 확인할 수 없습니다.");
            }

            return stdout.Trim();
        }

        private static async Task<string> GetLatestStableVersionAsync(CancellationToken cancellationToken)
        {
            using HttpResponseMessage response = await VersionCheckClient.GetAsync(
                "https://api.github.com/repos/yt-dlp/yt-dlp/releases/latest", cancellationToken);
            response.EnsureSuccessStatusCode();

            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            return document.RootElement.GetProperty("tag_name").GetString()
                ?? throw new InvalidOperationException("최신 yt-dlp 버전을 확인할 수 없습니다.");
        }

        private static bool TryParseVersionDate(string version, out DateTime date)
        {
            string datePart = version.Length >= 10 ? version[..10] : version;
            return DateTime.TryParseExact(
                datePart,
                "yyyy.MM.dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out date);
        }

        private static HttpClient CreateVersionCheckClient()
        {
            var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(5)
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("PhotoMosaicMaker/1.0");
            return client;
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

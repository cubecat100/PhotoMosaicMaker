using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhotoMosaicMaker.Core.Video
{
    public sealed class YoutubeDownloadOptions
    {
        // PATH에 잡혀 있으면 "yt-dlp"로 충분
        public string DownloaderExePath { get; init; } = "yt-dlp";

        // 플레이리스트 방지(기본 true)
        public bool NoPlaylist { get; init; } = true;

        // 다운로드 후 임시 영상 파일을 지울지
        public bool DeleteDownloadedVideo { get; init; } = true;

        // 최대 해상도 설정 (예: 720 = 720p 이하, 0 = 최대 해상도)
        //타일 크기는 작으므로 360p 정도로도 충분
        public int MaxResolution { get; set; } = 360;
    }
}

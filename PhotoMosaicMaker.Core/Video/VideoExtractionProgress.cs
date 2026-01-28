using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhotoMosaicMaker.Core.Video
{
    public enum VideoExtractionStage
    {
        StartingVideo = 1,
        FinishedVideo = 2
    }

    public readonly struct VideoExtractionProgress
    {
        public VideoExtractionStage Stage { get; }
        public int CurrentVideo { get; }
        public int TotalVideos { get; }
        public string VideoPath { get; }

        public VideoExtractionProgress(VideoExtractionStage stage, int currentVideo, int totalVideos, string videoPath)
        {
            Stage = stage;
            CurrentVideo = currentVideo;
            TotalVideos = totalVideos;
            VideoPath = videoPath;
        }
    }
}

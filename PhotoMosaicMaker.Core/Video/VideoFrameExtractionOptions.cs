using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhotoMosaicMaker.Core.Video
{
    public sealed class VideoFrameExtractionOptions
    {
        // 1.0 = 초당 1장
        public double FramesPerSecond { get; init; } = 1.0;

        // 0이면 제한 없음
        public int MaxFramesPerVideo { get; init; } = 0;

        // ffmpeg -q:v 값 (2~5 권장, 낮을수록 고품질)
        public int JpegQuality { get; init; } = 3;
    }
}

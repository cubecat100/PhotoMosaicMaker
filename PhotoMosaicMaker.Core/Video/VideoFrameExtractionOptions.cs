using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhotoMosaicMaker.Core.Video
{
    public sealed class VideoFrameExtractionOptions
    {
        // 초당 프레임 샘플링 횟수
        public double FramesPerSecond { get; init; } = 1.0;

        // 최대 프레임 수, 0이면 제한 없음
        public int MaxFramesPerVideo { get; init; } = 0;

        // ffmpeg -q:v 값 (2~5 권장, 낮을수록 고품질)
        public int JpegQuality { get; init; } = 3;

        //중복 검사 경계값 (0~64 권장, 낮을수록 엄격)
        public int DHashHammingThreshold { get; init; } = 12;

        //경계값 최대치
        public int MaxThresholdValue { get;  init; } = 32;
    }
}

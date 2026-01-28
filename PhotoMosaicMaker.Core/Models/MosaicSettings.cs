using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhotoMosaicMaker.Core.Models
{
    public sealed class MosaicSettings
    {
        public int OutputWidth { get; init; } = 3840;
        public int OutputHeight { get; init; } = 2160;

        public int TileSize { get; init; } = 32;

        // 0이면 제한 없음
        public int MaxPatchReuse { get; init; } = 5;

        // 0=off ~ 1=strong
        public float ColorAdjustStrength { get; init; } = 0.35f;

        // true: 소스 이미지를 tileSize 단위로 "분할(패치)"해서 후보 생성
        // false: 소스 이미지 "1장당 1개 타일" 후보 생성(비율 유지 + cover 크롭으로 tileSize 맞춤)
        public bool UseSourcePatches { get; init; } = true;
    }
}

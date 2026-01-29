using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhotoMosaicMaker.Core.Models
{
    public sealed class MosaicSettings
    {
        public int OutputWidth { get; init; } = 1920;
        public int OutputHeight { get; init; } = 1080;

        public int TileSize { get; init; } = 24;

        // 0이면 제한 없음
        public int MaxPatchReuse { get; init; } = 5;

        // 0=off ~ 1=strong
        public float ColorAdjustStrength { get; init; } = 0.35f;

        public bool UseSourcePatches { get; init; } = false;
    }
}

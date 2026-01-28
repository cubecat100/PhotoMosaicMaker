using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhotoMosaicMaker.Core.Models
{
    public enum MosaicStage
    {
        None = 0,
        LoadingSources = 1,
        BuildingPatches = 2,
        Rendering = 3
    }

    public readonly struct MosaicProgress
    {
        public MosaicStage Stage { get; }
        public int Current { get; }
        public int Total { get; }

        public MosaicProgress(MosaicStage stage, int current, int total)
        {
            Stage = stage;
            Current = current;
            Total = total;
        }
    }
}

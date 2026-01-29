using PhotoMosaicMaker.Core.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhotoMosaicMaker.Core.Engine
{
    public sealed class PatchRecord : IDisposable
    {
        public int Id { get; }
        public RgbFeature Mean { get; }
        public Image<Rgba32> Image { get; }
        public int GridSize { get; init; } = 2;
        public float[] GridFeature { get; init; } = Array.Empty<float>();

        public PatchRecord(int id, RgbFeature mean, Image<Rgba32> image)
            : this(id, mean, image, 0, Array.Empty<float>())
        {
        }

        public PatchRecord(int id, RgbFeature mean, Image<Rgba32> image, int gridSize, float[] gridFeature)
        {
            Id = id;
            Mean = mean;
            Image = image;

            GridSize = gridSize;
            GridFeature = gridFeature ?? Array.Empty<float>();
        }

        public void Dispose()
        {
            Image.Dispose();
        }
    }
}

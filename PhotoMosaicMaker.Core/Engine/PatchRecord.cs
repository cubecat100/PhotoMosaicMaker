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

        public PatchRecord(int id, RgbFeature mean, Image<Rgba32> image)
        {
            Id = id;
            Mean = mean;
            Image = image;
        }

        public void Dispose()
        {
            Image.Dispose();
        }
    }
}

using PhotoMosaicMaker.Core.Imaging;
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
    public sealed class PatchLibraryBuilder
    {
        public PatchLibrary BuildFromImageFiles(
            IReadOnlyList<string> imagePaths,
            int tileWidth,
            int tileHeight,
            bool useSourcePatches,
            int matchingGridSize,
            IProgress<MosaicProgress>? progress,
            CancellationToken cancellationToken)
        {
            if (tileWidth <= 0 || tileHeight <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tileWidth));
            }

            var patches = new List<PatchRecord>();
            int id = 1;

            progress?.Report(new MosaicProgress(MosaicStage.LoadingSources, 0, imagePaths.Count));

            for (int i = 0; i < imagePaths.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new MosaicProgress(MosaicStage.LoadingSources, i + 1, imagePaths.Count));

                using Image<Rgba32> img = ImageOps.Load(imagePaths[i]);

                if (useSourcePatches == true)
                {
                    // (A) 분할 모드: 이미지 1장을 tileWidth x tileHeight 격자로 잘라 패치 후보를 많이 만든다
                    int usableW = (img.Width / tileWidth) * tileWidth;
                    int usableH = (img.Height / tileHeight) * tileHeight;

                    if (usableW <= 0 || usableH <= 0)
                    {
                        continue;
                    }

                    for (int y = 0; y < usableH; y += tileHeight)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        for (int x = 0; x < usableW; x += tileWidth)
                        {
                            int gridSize = matchingGridSize;

                            var patch = ImageOps.Crop(img, x, y, tileWidth, tileHeight);
                            var mean = ImageOps.ComputeMeanRgb(patch);
                            var grid = ImageOps.ComputeGridMeanRgb(patch, gridSize);
                            patches.Add(new PatchRecord(id, mean, patch, gridSize, grid));
                            id++;
                        }
                    }
                }
                else
                {
                    // (B) 원본 1장 모드: 이미지 1장당 tile 1개 후보만 만든다
                    // 비율 유지 + cover(중앙) 크롭으로 왜곡 없이 타일 크기 맞춤
                    int gridSize = matchingGridSize;

                    var tile = ImageOps.CreateTileCoverCrop(img, tileWidth, tileHeight);
                    var mean = ImageOps.ComputeMeanRgb(tile);
                    var grid = ImageOps.ComputeGridMeanRgb(tile, gridSize);
                    patches.Add(new PatchRecord(id, mean, tile, gridSize, grid));
                    id++;
                }
            }

            return new PatchLibrary(patches);
        }
    }
}

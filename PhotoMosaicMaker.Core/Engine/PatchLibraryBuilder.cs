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
            int tileSize,
            bool useSourcePatches,
            IProgress<MosaicProgress>? progress,
            CancellationToken cancellationToken)
        {
            if (tileSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tileSize));
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
                    // (A) 분할 모드: 이미지 1장을 tileSize 격자로 잘라 패치 후보를 많이 만든다
                    int usableW = (img.Width / tileSize) * tileSize;
                    int usableH = (img.Height / tileSize) * tileSize;

                    if (usableW <= 0 || usableH <= 0)
                    {
                        continue;
                    }

                    for (int y = 0; y < usableH; y += tileSize)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        for (int x = 0; x < usableW; x += tileSize)
                        {
                            var patch = ImageOps.Crop(img, x, y, tileSize, tileSize);
                            var mean = ImageOps.ComputeMeanRgb(patch);
                            patches.Add(new PatchRecord(id, mean, patch));
                            id++;
                        }
                    }
                }
                else
                {
                    // (B) 원본 1장 모드: 이미지 1장당 tileSize 타일 1개 후보만 만든다
                    // 비율 유지 + cover(중앙) 크롭으로 왜곡 없이 타일 크기 맞춤
                    var tile = ImageOps.CreateTileCoverCrop(img, tileSize);
                    var mean = ImageOps.ComputeMeanRgb(tile);
                    patches.Add(new PatchRecord(id, mean, tile));
                    id++;
                }
            }

            return new PatchLibrary(patches);
        }
    }
}

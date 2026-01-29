using PhotoMosaicMaker.Core.Imaging;
using PhotoMosaicMaker.Core.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhotoMosaicMaker.Core.Engine
{
    public sealed class MosaicEngine
    {
        public PatchLibrary BuildPatchLibrary(
            IReadOnlyList<string> sourceImagePaths,
            MosaicSettings settings,
            int gridSize,
            IProgress<MosaicProgress>? progress,
            CancellationToken cancellationToken)
        {
            var builder = new PatchLibraryBuilder();
            return builder.BuildFromImageFiles(
                sourceImagePaths,
                settings.TileSize,
                settings.UseSourcePatches,
                gridSize,
                progress,
                cancellationToken);
        }

        public Image<Rgba32> Render(
            string targetImagePath,
            PatchLibrary library,
            MosaicSettings settings,
            IProgress<MosaicProgress>? progress,
            CancellationToken cancellationToken)
        {
            if (library.Patches.Count == 0)
            {
                throw new InvalidOperationException("패치 라이브러리가 비어 있습니다.");
            }

            using Image<Rgba32> targetOriginal = Image.Load<Rgba32>(targetImagePath);
            using Image<Rgba32> target = PrepareTargetToOutput(targetOriginal, settings.OutputWidth, settings.OutputHeight);

            int gridW = settings.OutputWidth / settings.TileSize;
            int gridH = settings.OutputHeight / settings.TileSize;

            var result = new Image<Rgba32>(settings.OutputWidth, settings.OutputHeight);

            var useCount = new Dictionary<int, int>();
            progress?.Report(new MosaicProgress(MosaicStage.Rendering, 0, gridW * gridH));

            int done = 0;

            for (int ty = 0; ty < gridH; ty++)
            {
                for (int tx = 0; tx < gridW; tx++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    int x = tx * settings.TileSize;
                    int y = ty * settings.TileSize;

                    var tileMean = ImageOps.ComputeMeanRgbRegion(target, x, y, settings.TileSize, settings.TileSize);
                    var tileGrid = ImageOps.ComputeGridMeanRgbRegion(target, x, y, settings.TileSize, settings.TileSize, settings.MatchingGridSize);

                    PatchRecord best = FindBestPatch(library.Patches, tileMean, tileGrid, settings.MatchingGridSize, useCount, settings.MaxPatchReuse);

                    if (settings.ColorAdjustStrength > 0f)
                    {
                        float dr = (tileMean.R - best.Mean.R) * settings.ColorAdjustStrength;
                        float dg = (tileMean.G - best.Mean.G) * settings.ColorAdjustStrength;
                        float db = (tileMean.B - best.Mean.B) * settings.ColorAdjustStrength;

                        ImageOps.BlitWithColorOffset(result, best.Image, x, y, dr, dg, db);
                    }
                    else
                    {
                        ImageOps.Blit(result, best.Image, x, y);
                    }

                    if (useCount.TryGetValue(best.Id, out int used) == true)
                    {
                        useCount[best.Id] = used + 1;
                    }
                    else
                    {
                        useCount[best.Id] = 1;
                    }

                    done++;
                    progress?.Report(new MosaicProgress(MosaicStage.Rendering, done, gridW * gridH));
                }
            }

            return result;
        }

        private static PatchRecord FindBestPatch(
            IReadOnlyList<PatchRecord> patches,
            in RgbFeature tileMean,
            float[] tileGrid,
            int gridSize,
            Dictionary<int, int> useCount,
            int maxReuse)
        {
            PatchRecord? best = null;
            float bestDist = float.MaxValue;

            for (int i = 0; i < patches.Count; i++)
            {
                var p = patches[i];

                if (maxReuse > 0 &&
                    useCount.TryGetValue(p.Id, out int used) == true &&
                    used >= maxReuse)
                {
                    continue;
                }

                float d = ComputeDistance(p, tileMean, tileGrid, gridSize);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = p;
                }
            }

            if (best == null)
            {
                for (int i = 0; i < patches.Count; i++)
                {
                    var p = patches[i];
                    float d = ComputeDistance(p, tileMean, tileGrid, gridSize);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        best = p;
                    }
                }
            }

            return best ?? patches[0];
        }

        private static float ComputeDistance(in PatchRecord p, in RgbFeature tileMean, float[] tileGrid, int gridSize)
        {
            if (gridSize > 1 &&
                p.GridSize == gridSize &&
                p.GridFeature.Length == tileGrid.Length &&
                tileGrid.Length > 0)
            {
                return ImageOps.DistanceSquared(p.GridFeature, tileGrid);
            }

            return p.Mean.DistanceSquared(tileMean);
        }

        private static Image<Rgba32> PrepareTargetToOutput(Image<Rgba32> src, int outputWidth, int outputHeight)
        {
            return src.Clone(ctx =>
            {
                ctx.BackgroundColor(Color.White);       //투명 백그라운드 흰색 처리
                ctx.Resize(outputWidth, outputHeight);
            });
        }
    }
}

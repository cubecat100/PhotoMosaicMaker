using PhotoMosaicMaker.Core.Imaging;
using PhotoMosaicMaker.Core.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
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

            // TileSize는 가로(width)로 해석. 세로는 16:9 비율로 계산
            int tileWidth = settings.TileSize;
            int tileHeight = Math.Max(1, tileWidth * 9 / 16);

            return builder.BuildFromImageFiles(
                sourceImagePaths,
                tileWidth,
                tileHeight,
                settings.UseSourcePatches,
                gridSize,
                progress,
                cancellationToken);
        }

        // 이미지 파일 입력용 오버로드
        public Image<Rgba32> Render(
            string targetImagePath,
            PatchLibrary library,
            MosaicSettings settings,
            IProgress<MosaicProgress>? progress,
            CancellationToken cancellationToken)
        {
            using Image<Rgba32> targetOriginal = Image.Load<Rgba32>(targetImagePath);
            return Render(targetOriginal, library, settings, progress, cancellationToken);
        }

        // 비디오 프레임(메모리) 입력용 오버로드
        public Image<Rgba32> Render(
            Image<Rgba32> targetFrame,
            PatchLibrary library,
            MosaicSettings settings,
            IProgress<MosaicProgress>? progress,
            CancellationToken cancellationToken)
        {
            if (library.Patches.Count == 0)
            {
                throw new InvalidOperationException("패치 라이브러리가 비어 있습니다.");
            }

            Image<Rgba32>? prepared = null;

            // TileSize는 가로(폭). 세로는 16:9 비율로 계산
            int tileWidth = settings.TileSize;
            int tileHeight = Math.Max(1, tileWidth * 9 / 16);

            // 성능 포인트:
            // - ffmpeg에서 이미 settings.OutputWidth/Height로 스케일해서 넘기면 여기서 Resize/Clone을 피할 수 있음
            Image<Rgba32> target;
            if (targetFrame.Width != settings.OutputWidth || targetFrame.Height != settings.OutputHeight)
            {
                prepared = PrepareTargetToOutput(targetFrame, settings.OutputWidth, settings.OutputHeight);
                target = prepared;
            }
            else
            {
                target = targetFrame;
            }

            try
            {
                // 남는 여백이 있어도 마지막 타일을 잘라서 채우도록 ceil 방식으로 계산
                int gridW = (settings.OutputWidth + tileWidth - 1) / tileWidth;
                int gridH = (settings.OutputHeight + tileHeight - 1) / tileHeight;

                var result = new Image<Rgba32>(settings.OutputWidth, settings.OutputHeight);

                var useCount = new Dictionary<int, int>();
                progress?.Report(new MosaicProgress(MosaicStage.Rendering, 0, gridW * gridH));

                int done = 0;

                for (int ty = 0; ty < gridH; ty++)
                {
                    for (int tx = 0; tx < gridW; tx++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        int x = tx * tileWidth;
                        int y = ty * tileHeight;

                        // 남은 영역 크기에 맞춰 실제 타일 크기 결정 (마지막 행/열은 작을 수 있음)
                        int actualW = Math.Min(tileWidth, settings.OutputWidth - x);
                        int actualH = Math.Min(tileHeight, settings.OutputHeight - y);

                        if (actualW <= 0 || actualH <= 0)
                        {
                            // 패딩 없이 넘어감
                            done++;
                            progress?.Report(new MosaicProgress(MosaicStage.Rendering, done, gridW * gridH));
                            continue;
                        }

                        var tileMean = ImageOps.ComputeMeanRgbRegion(target, x, y, actualW, actualH);
                        var tileGrid = ImageOps.ComputeGridMeanRgbRegion(target, x, y, actualW, actualH, settings.MatchingGridSize);

                        PatchRecord best = FindBestPatch(library.Patches, tileMean, tileGrid, settings.MatchingGridSize, useCount, settings.MaxPatchReuse);

                        if (settings.ColorAdjustStrength > 0f)
                        {
                            float dr = (tileMean.R - best.Mean.R) * settings.ColorAdjustStrength;
                            float dg = (tileMean.G - best.Mean.G) * settings.ColorAdjustStrength;
                            float db = (tileMean.B - best.Mean.B) * settings.ColorAdjustStrength;

                            ImageOps.BlitWithColorOffset(result, best.Image, x, y, actualW, actualH, dr, dg, db);
                        }
                        else
                        {
                            ImageOps.Blit(result, best.Image, x, y, actualW, actualH);
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
            finally
            {
                prepared?.Dispose();
            }
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

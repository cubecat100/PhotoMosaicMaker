using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PhotoMosaicMaker.Core.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Advanced;

namespace PhotoMosaicMaker.Core.Imaging
{
    public static class ImageOps
    {
        public static Image<Rgba32> Load(string path)
        {
            return Image.Load<Rgba32>(path);
        }

        public static Image<Rgba32> ResizeTo(Image<Rgba32> src, int width, int height)
        {
            return src.Clone(ctx => ctx.Resize(width, height));
        }

        // 원본 1장을 "타일 1장"으로: 비율 유지 + cover(중앙) 크롭
        public static Image<Rgba32> CreateTileCoverCrop(Image<Rgba32> src, int tileSize)
        {
            return src.Clone(ctx => ctx.Resize(new ResizeOptions
            {
                Size = new Size(tileSize, tileSize),
                Mode = ResizeMode.Crop,
                Position = AnchorPositionMode.Center
            }));
        }

        public static Image<Rgba32> Crop(Image<Rgba32> src, int x, int y, int w, int h)
        {
            return src.Clone(ctx => ctx.Crop(new Rectangle(x, y, w, h)));
        }

        public static RgbFeature ComputeMeanRgb(Image<Rgba32> img)
        {
            return ComputeMeanRgbRegion(img, 0, 0, img.Width, img.Height);
        }

        public static RgbFeature ComputeMeanRgbRegion(Image<Rgba32> img, int x, int y, int w, int h)
        {
            double sumR = 0, sumG = 0, sumB = 0;
            long count = (long)w * h;

            img.ProcessPixelRows(accessor =>
            {
                for (int yy = y; yy < y + h; yy++)
                {
                    var row = accessor.GetRowSpan(yy).Slice(x, w);
                    for (int xx = 0; xx < row.Length; xx++)
                    {
                        sumR += row[xx].R;
                        sumG += row[xx].G;
                        sumB += row[xx].B;
                    }
                }
            });

            float r = (float)(sumR / (255.0 * count));
            float g = (float)(sumG / (255.0 * count));
            float b = (float)(sumB / (255.0 * count));
            return new RgbFeature(r, g, b);
        }

        public static void Blit(Image<Rgba32> dest, Image<Rgba32> patch, int destX, int destY)
        {
            dest.ProcessPixelRows(destAcc =>
            {
                for (int y = 0; y < patch.Height; y++)
                {
                    Span<Rgba32> dstRow = destAcc.GetRowSpan(destY + y).Slice(destX, patch.Width);
                    Span<Rgba32> srcRow = patch.DangerousGetPixelRowMemory(y).Span.Slice(0, patch.Width);

                    srcRow.CopyTo(dstRow);
                }
            });
        }

        public static void BlitWithColorOffset(
            Image<Rgba32> dest,
            Image<Rgba32> patch,
            int destX,
            int destY,
            float dr,
            float dg,
            float db)
        {
            int rOff = (int)(dr * 255f);
            int gOff = (int)(dg * 255f);
            int bOff = (int)(db * 255f);

            dest.ProcessPixelRows(destAcc =>
            {
                for (int y = 0; y < patch.Height; y++)
                {
                    Span<Rgba32> dstRow = destAcc.GetRowSpan(destY + y).Slice(destX, patch.Width);
                    Span<Rgba32> srcRow = patch.DangerousGetPixelRowMemory(y).Span.Slice(0, patch.Width);

                    for (int x = 0; x < srcRow.Length; x++)
                    {
                        var s = srcRow[x];

                        int rr = s.R + rOff;
                        int gg = s.G + gOff;
                        int bb = s.B + bOff;

                        dstRow[x] = new Rgba32(
                            (byte)Clamp255(rr),
                            (byte)Clamp255(gg),
                            (byte)Clamp255(bb),
                            s.A);
                    }
                }
            });
        }

        private static int Clamp255(int v)
        {
            if (v < 0) return 0;
            if (v > 255) return 255;
            return v;
        }
    }
}

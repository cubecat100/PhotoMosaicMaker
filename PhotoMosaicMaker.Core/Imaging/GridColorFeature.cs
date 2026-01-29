using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhotoMosaicMaker.Core.Imaging
{
    public static class GridColorFeature
    {
        // region을 gridSize x gridSize로 나누어, 셀별 평균색(R,G,B)을 float[gridSize*gridSize*3]로 반환
        public static float[] Compute(Image<Rgba32> img, Rectangle region, int gridSize)
        {
            int cellCount = gridSize * gridSize;
            float[] feat = new float[cellCount * 3];

            for (int gy = 0; gy < gridSize; gy++)
            {
                int y0 = region.Y + (gy * region.Height) / gridSize;
                int y1 = region.Y + ((gy + 1) * region.Height) / gridSize;

                for (int gx = 0; gx < gridSize; gx++)
                {
                    int x0 = region.X + (gx * region.Width) / gridSize;
                    int x1 = region.X + ((gx + 1) * region.Width) / gridSize;

                    long sumR = 0, sumG = 0, sumB = 0;
                    long count = 0;

                    for (int y = y0; y < y1; y++)
                    {
                        for (int x = x0; x < x1; x++)
                        {
                            Rgba32 p = img[x, y];
                            sumR += p.R;
                            sumG += p.G;
                            sumB += p.B;
                            count++;
                        }
                    }

                    int idx = (gy * gridSize + gx) * 3;
                    if (count > 0)
                    {
                        feat[idx + 0] = (float)sumR / count;
                        feat[idx + 1] = (float)sumG / count;
                        feat[idx + 2] = (float)sumB / count;
                    }
                }
            }

            return feat;
        }

        // 두 feature 간 거리(작을수록 유사)
        public static float Distance(float[] a, float[] b)
        {
            float d = 0;
            int n = a.Length;
            for (int i = 0; i < n; i++)
            {
                float diff = a[i] - b[i];
                d += diff * diff;
            }
            return d;
        }
    }
}

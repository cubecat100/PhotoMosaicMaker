using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhotoMosaicMaker.Core.Models
{
    public readonly struct RgbFeature
    {
        public readonly float R; // 0..1
        public readonly float G; // 0..1
        public readonly float B; // 0..1

        public RgbFeature(float r, float g, float b)
        {
            R = r; G = g; B = b;
        }

        public float DistanceSquared(in RgbFeature other)
        {
            float dr = R - other.R;
            float dg = G - other.G;
            float db = B - other.B;
            return (dr * dr) + (dg * dg) + (db * db);
        }
    }
}

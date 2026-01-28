using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace PhotoMosaicMaker.Core.Imaging
{
    public static class ImageDeduplicator
    {
        public sealed class DedupResult
        {
            public int Total { get; init; }
            public int Kept { get; init; }
            public int MovedToDuplicates { get; init; }
        }

        public static DedupResult DedupFolderByDHash(
            string folder,
            int hammingThreshold,
            bool moveToDuplicatesFolder,
            CancellationToken cancellationToken)
        {
            if (Directory.Exists(folder) == false)
            {
                return new DedupResult { Total = 0, Kept = 0, MovedToDuplicates = 0 };
            }

            string duplicatesFolder = Path.Combine(folder, "_duplicates");
            if (moveToDuplicatesFolder == true)
            {
                Directory.CreateDirectory(duplicatesFolder);
            }

            // 파일 생성 순서대로 처리(연속 프레임 중복 제거에 가장 유리)
            var files = Directory.EnumerateFiles(folder)
                .Where(IsImageFile)
                .Select(p => new FileInfo(p))
                .OrderBy(fi => fi.CreationTimeUtc)
                .ThenBy(fi => fi.Name)
                .ToList();

            int total = files.Count;
            if (total == 0)
            {
                return new DedupResult { Total = 0, Kept = 0, MovedToDuplicates = 0 };
            }

            ulong? lastKeptHash = null;
            int kept = 0;
            int moved = 0;

            foreach (var fi in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // 이미 duplicates 폴더로 들어간 건 스킵
                if (fi.DirectoryName != null && fi.DirectoryName.EndsWith("_duplicates", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                ulong hash;
                try
                {
                    hash = ComputeDHash64(fi.FullName);
                }
                catch
                {
                    // 읽기 실패 파일은 유지(삭제/이동하지 않음)
                    kept++;
                    lastKeptHash = null;
                    continue;
                }

                if (lastKeptHash.HasValue == true)
                {
                    int dist = HammingDistance(lastKeptHash.Value, hash);
                    if (dist <= hammingThreshold)
                    {
                        if (moveToDuplicatesFolder == true)
                        {
                            string dst = Path.Combine(duplicatesFolder, fi.Name);
                            dst = MakeUniquePath(dst);
                            File.Move(fi.FullName, dst);
                            moved++;
                        }
                        else
                        {
                            File.Delete(fi.FullName);
                            moved++;
                        }

                        continue;
                    }
                }

                // keep
                kept++;
                lastKeptHash = hash;
            }

            return new DedupResult
            {
                Total = total,
                Kept = kept,
                MovedToDuplicates = moved
            };
        }

        private static bool IsImageFile(string path)
        {
            return path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".webp", StringComparison.OrdinalIgnoreCase);
        }

        // dHash 64bit: 9x8로 축소 후 가로 방향 밝기 비교
        private static ulong ComputeDHash64(string path)
        {
            using Image<Rgba32> img = Image.Load<Rgba32>(path);

            img.Mutate(c =>
            {
                c.Resize(9, 8);
                c.Grayscale();
            });

            ulong bits = 0;
            int bitIndex = 0;

            for (int y = 0; y < 8; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    // GetPixelRowSpan 대신 인덱서로 접근
                    byte left = img[x, y].R;       // grayscale 후 R=G=B
                    byte right = img[x + 1, y].R;

                    if (left < right)
                    {
                        bits |= 1UL << bitIndex;
                    }

                    bitIndex++;
                }
            }

            return bits;
        }

        private static int HammingDistance(ulong a, ulong b)
        {
            return BitOperations.PopCount(a ^ b);
        }

        private static string MakeUniquePath(string path)
        {
            if (File.Exists(path) == false) return path;

            string dir = Path.GetDirectoryName(path) ?? "";
            string name = Path.GetFileNameWithoutExtension(path);
            string ext = Path.GetExtension(path);

            for (int i = 1; i < 10000; i++)
            {
                string p = Path.Combine(dir, $"{name} ({i}){ext}");
                if (File.Exists(p) == false) return p;
            }

            return Path.Combine(dir, $"{name}_{Guid.NewGuid():N}{ext}");
        }
    }
}

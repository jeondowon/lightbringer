using System;
using System.Collections.Generic;
using System.IO;
using Lightbringer.UI;
using UnityEditor;
using UnityEngine;

namespace Lightbringer.EditorTools
{
    // Cuts the kneeling lord out of the Retreat painting by comparing it with the clean plate, so the escape
    // can pull the painted lord himself into the portal. Output: the lord's pixels with a feathered alpha mask,
    // cropped to BossPortalEscape.LordRect.
    [InitializeOnLoad]
    public static class BossRetreatMaskBake
    {
        private const string Folder = "Assets/UI/Resources/BossStory/";
        private const string OutputPath = Folder + "Retreat_Lord_v1.png";
        private const string ReportPath = "Docs/Validation/BossRetreatMaskChecks.txt";

        static BossRetreatMaskBake()
        {
            EditorRequests.Register("Docs/Validation/BossRetreatMask.request", Bake, duringPlay: true);
        }

        [MenuItem("Lightbringer/Boss Story/Bake Retreat Lord Cutout")]
        public static void Bake()
        {
            Texture2D painting = Load(Folder + "Retreat_v1.png"), plate = Load(Folder + "Retreat_Empty_v1.png");
            try
            {
                if (painting.width != plate.width || painting.height != plate.height)
                    throw new InvalidOperationException("Retreat painting and clean plate differ in size");
                RectInt rect = BossPortalEscape.LordRect;
                int w = rect.width, h = rect.height;
                Color32[] a = painting.GetPixels32(), b = plate.GetPixels32();
                var diff = new float[w * h];
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = Source(painting, rect, x, y);
                    diff[y * w + x] = Mathf.Abs(a[i].r - b[i].r) + Mathf.Abs(a[i].g - b[i].g) + Mathf.Abs(a[i].b - b[i].b);
                }
                // The plate is a repaint: faint noise everywhere, strong differences only where the lord stood.
                float[] smooth = Blur(diff, w, h, 3);
                var mask = new bool[w * h];
                for (int i = 0; i < mask.Length; i++) mask[i] = smooth[i] > 40f;
                // Open first so thin repainted lines (stair edges) cannot bridge the silhouette, then close gaps.
                mask = Dilate(Erode(mask, w, h, 6), w, h, 6);
                mask = Erode(Dilate(mask, w, h, 6), w, h, 6);
                mask = Largest(mask, w, h);
                FillHoles(mask, w, h);
                mask = Dilate(mask, w, h, 2);

                var solid = new float[w * h];
                for (int i = 0; i < solid.Length; i++) solid[i] = mask[i] ? 1f : 0f;
                float[] alpha = Blur(solid, w, h, 2);
                var output = new Texture2D(w, h, TextureFormat.RGBA32, false);
                var pixels = new Color32[w * h];
                int count = 0, minX = w, maxX = 0, minY = h, maxY = 0;
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    Color32 c = a[Source(painting, rect, x, y)];
                    c.a = (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha[y * w + x]) * 255f);
                    pixels[(h - 1 - y) * w + x] = c;
                    if (!mask[y * w + x]) continue;
                    count++;
                    minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                    minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                }
                int margin = Mathf.Min(Mathf.Min(minX, w - 1 - maxX), Mathf.Min(minY, h - 1 - maxY));
                WritePreview(painting, rect, mask);
                // The clean plate patch feathers its outer 18 px, so the lord must sit clear of that band.
                if (count < 20000 || margin < BossPortalEscape.PlateFeather + 2)
                    throw new InvalidOperationException($"Lord mask looks wrong: {count} px, crop bounds x {minX}-{maxX}, y {minY}-{maxY}");
                output.SetPixels32(pixels);
                File.WriteAllBytes(OutputPath, output.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(output);
                AssetDatabase.ImportAsset(OutputPath, ImportAssetOptions.ForceUpdate);
                var importer = (TextureImporter)AssetImporter.GetAtPath(OutputPath);
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
                File.WriteAllText(ReportPath, $"PASS | {DateTime.UtcNow:O} | lord {count} px, artwork bounds "
                    + $"x {rect.x + minX}-{rect.x + maxX}, y {rect.y + minY}-{rect.y + maxY}, margin {margin} px\n");
                Debug.Log("Boss retreat lord cutout baked: " + OutputPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(painting);
                UnityEngine.Object.DestroyImmediate(plate);
            }
        }

        // Reads the PNG bytes directly, so the paintings need not be import-readable.
        private static Texture2D Load(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(File.ReadAllBytes(path))) throw new InvalidOperationException("Cannot read " + path);
            return texture;
        }

        // Crop coordinates are top-down like the artwork; texture rows run bottom-up.
        private static int Source(Texture2D texture, RectInt rect, int x, int y) =>
            (texture.height - 1 - (rect.y + y)) * texture.width + rect.x + x;

        private static float[] Blur(float[] source, int w, int h, int radius)
        {
            var row = new float[source.Length];
            var result = new float[source.Length];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float sum = 0; int n = 0;
                for (int k = Mathf.Max(0, x - radius); k <= Mathf.Min(w - 1, x + radius); k++) { sum += source[y * w + k]; n++; }
                row[y * w + x] = sum / n;
            }
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float sum = 0; int n = 0;
                for (int k = Mathf.Max(0, y - radius); k <= Mathf.Min(h - 1, y + radius); k++) { sum += row[k * w + x]; n++; }
                result[y * w + x] = sum / n;
            }
            return result;
        }

        private static bool[] Dilate(bool[] mask, int w, int h, int radius) => Morph(mask, w, h, radius, true);
        private static bool[] Erode(bool[] mask, int w, int h, int radius) => Morph(mask, w, h, radius, false);

        // Separable square structuring element; outside the crop counts as empty.
        private static bool[] Morph(bool[] mask, int w, int h, int radius, bool grow)
        {
            var row = new bool[mask.Length];
            var result = new bool[mask.Length];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                bool value = !grow;
                for (int k = x - radius; k <= x + radius; k++)
                {
                    bool inside = k >= 0 && k < w && mask[y * w + k];
                    if (grow ? inside : !inside) { value = grow; break; }
                }
                row[y * w + x] = value;
            }
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                bool value = !grow;
                for (int k = y - radius; k <= y + radius; k++)
                {
                    bool inside = k >= 0 && k < h && row[k * w + x];
                    if (grow ? inside : !inside) { value = grow; break; }
                }
                result[y * w + x] = value;
            }
            return result;
        }

        private static bool[] Largest(bool[] mask, int w, int h)
        {
            var label = new int[mask.Length];
            var queue = new Queue<int>();
            int best = 0, bestSize = 0, next = 0;
            for (int start = 0; start < mask.Length; start++)
            {
                if (!mask[start] || label[start] != 0) continue;
                int size = 0; next++;
                label[start] = next; queue.Enqueue(start);
                while (queue.Count > 0)
                {
                    int i = queue.Dequeue(); size++;
                    int x = i % w, y = i / w;
                    if (x > 0) Visit(i - 1); if (x < w - 1) Visit(i + 1);
                    if (y > 0) Visit(i - w); if (y < h - 1) Visit(i + w);
                }
                if (size > bestSize) { bestSize = size; best = next; }
            }
            var result = new bool[mask.Length];
            for (int i = 0; i < mask.Length; i++) result[i] = label[i] == best;
            return result;

            void Visit(int j)
            {
                if (!mask[j] || label[j] != 0) return;
                label[j] = next; queue.Enqueue(j);
            }
        }

        // Dark armour plates match the plate's dark portal; anything enclosed by the silhouette is the lord.
        private static void FillHoles(bool[] mask, int w, int h)
        {
            var outside = new bool[mask.Length];
            var queue = new Queue<int>();
            for (int x = 0; x < w; x++) { Seed(x); Seed((h - 1) * w + x); }
            for (int y = 0; y < h; y++) { Seed(y * w); Seed(y * w + w - 1); }
            while (queue.Count > 0)
            {
                int i = queue.Dequeue(), x = i % w, y = i / w;
                if (x > 0) Seed(i - 1); if (x < w - 1) Seed(i + 1);
                if (y > 0) Seed(i - w); if (y < h - 1) Seed(i + w);
            }
            for (int i = 0; i < mask.Length; i++) if (!outside[i]) mask[i] = true;

            void Seed(int j)
            {
                if (mask[j] || outside[j]) return;
                outside[j] = true; queue.Enqueue(j);
            }
        }

        private static void WritePreview(Texture2D painting, RectInt rect, bool[] mask)
        {
            int w = rect.width, h = rect.height;
            Color32[] source = painting.GetPixels32();
            var preview = new Texture2D(w, h, TextureFormat.RGB24, false);
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Color32 c = source[Source(painting, rect, x, y)];
                if (!mask[y * w + x]) c = new Color32((byte)(c.r / 3), (byte)(c.g / 3 + 60), (byte)(c.b / 3), 255);
                pixels[(h - 1 - y) * w + x] = c;
            }
            preview.SetPixels32(pixels);
            File.WriteAllBytes("Docs/Validation/BossRetreatMaskPreview.png", preview.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(preview);
        }
    }
}

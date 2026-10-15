using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Lightbringer.EditorTools
{
    // Builds unit portraits and shared HUD textures in Assets/UI/Art (growth icons are authored illustrations).
    // Character illustrations are keyed out of white-background sources. Re-run after source art changes.
    [InitializeOnLoad]
    public static partial class PreparationArtBuilder
    {
        public const string Folder = "Assets/UI/Art";
        private const string Characters = "Assets/Art/Characters/";
        private const string Request = "Docs/Validation/BuildPreparationArt.request";
        private static readonly List<(string path, bool compressed)> Written = new List<(string, bool)>();

        static PreparationArtBuilder()
        {
            EditorRequests.Register(Request, Build);
            EditorRequests.Register("Docs/Validation/BuildUpdatedUnitPortraits.request", BuildUpdatedUnitPortraits);
            EditorRequests.Register(SkillRequest, BuildSkillIcons);
        }

        [MenuItem("Lightbringer/UI/Build Updated Unit Portraits")]
        public static void BuildUpdatedUnitPortraits()
        {
            Directory.CreateDirectory(Full(Folder));
            Written.Clear();
            SaveUpdatedUnitPortraits();
            ImportWritten();
            Debug.Log("Updated unit portraits built: Archer, Mage, Priest, Spearman.");
        }

        private static void SaveUpdatedUnitPortraits()
        {
            foreach (string unit in new[] { "Archer", "Mage", "Priest", "Spearman" })
            {
                string variant = unit == "Spearman" ? "ClosedHelmet" : "ShadowFace";
                Illustration("Docs/ArtReference/Characters/" + unit + "_Concept_" + variant + "_v2.png",
                    "Unit_" + unit, 640, false, false, true);
            }
        }

        [MenuItem("Lightbringer/UI/Build Preparation Art")]
        public static void Build()
        {
            Directory.CreateDirectory(Full(Folder));
            Written.Clear();

            Illustration("FemaleHero/Source/FemaleHero_View_v2_front.png", "Hero_Commander", 1400, false, false);
            string[] units = { "Swordsman", "Shieldbearer", "Knight" };
            foreach (string unit in units)
                Illustration(unit + "/Source/" + unit + "_1_Front.png", "Unit_" + unit, 640, false, false);
            // The wings enclose pockets of background the border flood fill cannot reach.
            Illustration("Dragon/Source/Dragon_1_ThreeQuarter.png", "Unit_Dragon", 640, false, true);
            SaveUpdatedUnitPortraits();

            Save("Prep_Glow", Glow(128), false);
            Save("Prep_Fade", Fade(4, 128), false);

            ImportWritten();
            Debug.Log("Preparation art built: " + Written.Count + " textures in " + Folder);
        }

        private static void ImportWritten()
        {
            AssetDatabase.Refresh();
            foreach ((string path, bool compressed) in Written)
            {
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) continue;
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = 2048;
                importer.textureCompression = compressed ? TextureImporterCompression.CompressedHQ : TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            // The style sheet resolves url() references at import; reimport it so newly created textures bind.
            AssetDatabase.ImportAsset(Lightbringer.UI.CampaignHUD.StyleSheetPath, ImportAssetOptions.ForceUpdate);
        }

        private static string Full(string projectPath) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", projectPath));

        // ---------- Illustrations ----------

        private static void Illustration(string source, string name, int maxHeight, bool silhouette, bool pockets, bool projectRelative = false)
        {
            string path = Full(projectRelative ? source : Characters + source);
            if (!File.Exists(path)) { Debug.LogWarning("Missing illustration source " + path); return; }
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.LoadImage(File.ReadAllBytes(path));
            int w = texture.width, h = texture.height;
            Color32[] pixels = texture.GetPixels32();
            UnityEngine.Object.DestroyImmediate(texture);

            Color[] keyed = Key(pixels, w, h, pockets);
            RectInt bounds = Bounds(keyed, w, h, 6);
            Color[] cropped = Crop(keyed, w, bounds);
            int height = Mathf.Min(maxHeight, bounds.height);
            int width = Mathf.Max(1, Mathf.RoundToInt(bounds.width * (height / (float)bounds.height)));
            Color[] result = Resize(cropped, bounds.width, bounds.height, width, height);
            if (silhouette)
                for (int i = 0; i < result.Length; i++) result[i] = new Color(0.17f, 0.22f, 0.36f, result[i].a);
            Save(name, result, width, height, !silhouette);
        }

        private static bool IsBackground(Color32 c)
        {
            int min = Mathf.Min(c.r, Mathf.Min(c.g, c.b)), max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            return c.a < 16 || (min > 222 && max - min < 20);
        }

        // Flood fill from the border so white cloth inside the outline survives; edge pixels get soft alpha.
        private static Color[] Key(Color32[] source, int w, int h, bool pockets)
        {
            bool[] background = new bool[w * h];
            int[] stack = new int[w * h];
            int top = 0;
            void Seed(int i) { if (!background[i] && IsBackground(source[i])) { background[i] = true; stack[top++] = i; } }
            for (int x = 0; x < w; x++) { Seed(x); Seed((h - 1) * w + x); }
            for (int y = 0; y < h; y++) { Seed(y * w); Seed(y * w + w - 1); }
            while (top > 0)
            {
                int i = stack[--top], x = i % w;
                if (x > 0) Seed(i - 1);
                if (x < w - 1) Seed(i + 1);
                if (i >= w) Seed(i - w);
                if (i < w * (h - 1)) Seed(i + w);
            }
            if (pockets) RemovePockets(source, background, w, h, stack);

            Color[] result = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (background[i]) { result[i] = Color.clear; continue; }
                    Color c = source[i];
                    float alpha = c.a;
                    if (TouchesBackground(background, w, h, x, y))
                    {
                        // Near-white fringe pixels become translucent and lose their white contamination.
                        float whiteness = Mathf.Clamp01((Mathf.Min(c.r, Mathf.Min(c.g, c.b)) - 0.62f) / 0.3f);
                        alpha *= 1f - whiteness * 0.85f;
                        if (alpha > 0.01f)
                        {
                            float blend = 1f - alpha;
                            c.r = Mathf.Clamp01((c.r - blend) / alpha);
                            c.g = Mathf.Clamp01((c.g - blend) / alpha);
                            c.b = Mathf.Clamp01((c.b - blend) / alpha);
                        }
                    }
                    c.a = alpha;
                    result[i] = c;
                }
            return result;
        }

        // Large flat pure-white regions are background; shaded white scales and cloth stay.
        private static void RemovePockets(Color32[] source, bool[] background, int w, int h, int[] stack)
        {
            bool[] visited = new bool[w * h];
            List<int> region = new List<int>();
            int minArea = w * h / 600;
            bool Flat(Color32 c) => Mathf.Min(c.r, Mathf.Min(c.g, c.b)) > 244 && Mathf.Max(c.r, Mathf.Max(c.g, c.b)) - Mathf.Min(c.r, Mathf.Min(c.g, c.b)) < 8;
            for (int start = 0; start < w * h; start++)
            {
                if (visited[start] || background[start] || !Flat(source[start])) continue;
                region.Clear();
                int top = 0;
                stack[top++] = start;
                visited[start] = true;
                while (top > 0)
                {
                    int i = stack[--top], x = i % w;
                    region.Add(i);
                    void Visit(int n) { if (!visited[n] && !background[n] && Flat(source[n])) { visited[n] = true; stack[top++] = n; } }
                    if (x > 0) Visit(i - 1);
                    if (x < w - 1) Visit(i + 1);
                    if (i >= w) Visit(i - w);
                    if (i < w * (h - 1)) Visit(i + w);
                }
                if (region.Count >= minArea) foreach (int i in region) background[i] = true;
            }
        }

        private static bool TouchesBackground(bool[] background, int w, int h, int x, int y)
        {
            for (int dy = -2; dy <= 2; dy++)
                for (int dx = -2; dx <= 2; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx >= 0 && ny >= 0 && nx < w && ny < h && background[ny * w + nx]) return true;
                }
            return false;
        }

        private static RectInt Bounds(Color[] pixels, int w, int h, int margin)
        {
            int minX = w, minY = h, maxX = -1, maxY = -1;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (pixels[y * w + x].a > 0.1f)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
            if (maxX < 0) return new RectInt(0, 0, w, h);
            minX = Mathf.Max(0, minX - margin); minY = Mathf.Max(0, minY - margin);
            maxX = Mathf.Min(w - 1, maxX + margin); maxY = Mathf.Min(h - 1, maxY + margin);
            return new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        private static Color[] Crop(Color[] pixels, int w, RectInt r)
        {
            Color[] result = new Color[r.width * r.height];
            for (int y = 0; y < r.height; y++)
                Array.Copy(pixels, (r.y + y) * w + r.x, result, y * r.width, r.width);
            return result;
        }

        // Area average in premultiplied space so transparent pixels do not darken edges.
        private static Color[] Resize(Color[] source, int sw, int sh, int dw, int dh)
        {
            if (sw == dw && sh == dh) return source;
            Color[] result = new Color[dw * dh];
            float sx = sw / (float)dw, sy = sh / (float)dh;
            for (int y = 0; y < dh; y++)
            {
                int y0 = (int)(y * sy), y1 = Mathf.Min(sh, Mathf.Max(y0 + 1, (int)((y + 1) * sy)));
                for (int x = 0; x < dw; x++)
                {
                    int x0 = (int)(x * sx), x1 = Mathf.Min(sw, Mathf.Max(x0 + 1, (int)((x + 1) * sx)));
                    float r = 0f, g = 0f, b = 0f, a = 0f;
                    for (int yy = y0; yy < y1; yy++)
                        for (int xx = x0; xx < x1; xx++)
                        {
                            Color c = source[yy * sw + xx];
                            r += c.r * c.a; g += c.g * c.a; b += c.b * c.a; a += c.a;
                        }
                    int count = (y1 - y0) * (x1 - x0);
                    result[y * dw + x] = a > 0f ? new Color(r / a, g / a, b / a, a / count) : Color.clear;
                }
            }
            return result;
        }

        // ---------- Procedural textures ----------

        private static Canvas Glow(int size)
        {
            Canvas canvas = new Canvas(size, size);
            float c = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float r = Len(x + 0.5f - c, y + 0.5f - c) / c;
                    canvas.Set(x, y, new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp01(1f - r), 2.2f)));
                }
            return canvas;
        }

        private static Canvas Fade(int w, int h)
        {
            Canvas canvas = new Canvas(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float t = (y + 0.5f) / h;
                    canvas.Set(x, y, new Color(0.03f, 0.05f, 0.1f, Mathf.SmoothStep(0f, 0.96f, t)));
                }
            return canvas;
        }

        // ---------- Raster helpers (y grows downward while drawing) ----------

        private static float Len(float x, float y) => Mathf.Sqrt(x * x + y * y);

        private static void Save(string name, Canvas canvas, bool compressed) => Save(name, canvas.Pixels, canvas.Width, canvas.Height, compressed);

        private static void Save(string name, Color[] pixels, int width, int height, bool compressed)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            texture.Apply();
            string path = Folder + "/" + name + ".png";
            File.WriteAllBytes(Full(path), texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            Written.Add((path, compressed));
        }

        private sealed class Canvas
        {
            public readonly int Width, Height;
            public readonly Color[] Pixels;

            public Canvas(int width, int height) { Width = width; Height = height; Pixels = new Color[width * height]; }

            public void Set(int x, int y, Color c) => Pixels[(Height - 1 - y) * Width + x] = c;
        }
    }
}

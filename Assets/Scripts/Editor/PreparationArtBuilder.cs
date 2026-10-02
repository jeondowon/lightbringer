using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Lightbringer.EditorTools
{
    // Builds the preparation screen art in Assets/UI/Art: character illustrations keyed out of the white-background
    // concept sources, plus procedural frames, rune circle, glows and item icons. Re-run after source art changes.
    [InitializeOnLoad]
    public static partial class PreparationArtBuilder
    {
        public const string Folder = "Assets/UI/Art";
        private const string Characters = "Assets/Art/Characters/";
        private const string Request = "Docs/Validation/BuildPreparationArt.request";
        private static readonly Color Gold = new Color(0.86f, 0.69f, 0.36f), BrightGold = new Color(1f, 0.88f, 0.6f);
        private static readonly List<(string path, bool compressed)> Written = new List<(string, bool)>();

        static PreparationArtBuilder()
        {
            EditorRequests.Register(Request, Build);
            EditorRequests.Register("Docs/Validation/BuildUpdatedUnitPortraits.request", BuildUpdatedUnitPortraits);
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

        private static void SaveGrowthFallback(string name, Func<Canvas> create)
        {
            if (!File.Exists(Full(Folder + "/" + name + ".png")))
                Save(name, create(), false);
        }

        [MenuItem("Lightbringer/UI/Build Preparation Art")]
        public static void Build()
        {
            Directory.CreateDirectory(Full(Folder));
            Written.Clear();

            Illustration("FemaleHero/Source/FemaleHero_View_v2_front.png", "Hero_Commander", 1400, false, false);
            string[] units = { "Swordsman", "Shieldbearer", "Knight" };
            foreach (string unit in units)
                if (unit != null) Illustration(unit + "/Source/" + unit + "_1_Front.png", "Unit_" + unit, 640, false, false);
            // The wings enclose pockets of background the border flood fill cannot reach.
            Illustration("Dragon/Source/Dragon_1_ThreeQuarter.png", "Unit_Dragon", 640, false, true);
            SaveUpdatedUnitPortraits();

            Save("Prep_Background", Background(960, 540), false);
            Save("Prep_Rune", Rune(1024), false);
            Save("Prep_Glow", Glow(128), false);
            Save("Prep_Halo", Halo(96), false);
            Save("Prep_Frame", Frame(128), false);
            Save("Prep_Fade", Fade(4, 128), false);
            Save("Prep_Star", Star(128), false);
            Save("Item_LightStaff", Staff(new Color(1f, 0.9f, 0.6f), false), false);
            Save("Item_HealingStaff", Staff(new Color(0.5f, 0.95f, 0.6f), false), false);
            Save("Item_RuneStaff", Staff(new Color(0.62f, 0.62f, 1f), true), false);
            Save("Item_FoodRing", Ring(new Color(1f, 0.68f, 0.22f)), false);
            Save("Item_ManaRing", Ring(new Color(0.35f, 0.6f, 1f)), false);
            Save("Item_VitalityRing", Ring(new Color(0.95f, 0.3f, 0.35f)), false);
            Save("LevelUp_Card", CardFrame(192), false);
            // Preserve authored blessing illustrations when rebuilding procedural UI art.
            // Original shapes are only a fallback for missing assets.
            SaveGrowthFallback("Growth_AuraSize", AuraSizeIcon);
            SaveGrowthFallback("Growth_FoodProduction", HarvestIcon);
            SaveGrowthFallback("Growth_ManaRecovery", WellspringIcon);
            SaveGrowthFallback("Growth_Capacity", ReservesIcon);
            SaveGrowthFallback("Growth_Leadership", BannerIcon);
            SaveGrowthFallback("Growth_AuraBuff", BlessedBladeIcon);
            SaveGrowthFallback("Growth_HeroPower", MightIcon);
            SaveGrowthFallback("Growth_HeroVitality", HeartIcon);
            SaveBattleHudArt();

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

        private static Canvas Background(int w, int h)
        {
            Canvas canvas = new Canvas(w, h);
            float aspect = w / (float)h;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w, v = (y + 0.5f) / h;
                    Color c = Color.Lerp(new Color(0.07f, 0.1f, 0.19f), new Color(0.02f, 0.03f, 0.07f), Mathf.Pow(v, 0.8f));
                    c += new Color(0.55f, 0.4f, 0.18f) * 0.32f * Falloff((u - 0.2f) * aspect, v - 0.55f, 0.55f);
                    c += new Color(0.16f, 0.27f, 0.55f) * 0.3f * Falloff((u - 0.62f) * aspect, v + 0.05f, 0.7f);
                    float angle = Mathf.Atan2(v + 0.25f, u - 0.05f);
                    float ray = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(angle * 23f + Mathf.Sin(angle * 7f) * 2f), 6f);
                    c += new Color(0.9f, 0.8f, 0.55f) * 0.045f * ray * Mathf.Clamp01(1f - v * 1.2f) * Mathf.Clamp01(1.2f - u);
                    float edge = Len((u - 0.5f) * 1.2f, v - 0.5f) / 0.78f;
                    c *= 1f - 0.6f * Mathf.Pow(Mathf.Clamp01(edge), 2.2f);
                    float noise = (Hash(x, y) - 0.5f) / 255f * 2f;
                    canvas.Set(x, y, new Color(c.r + noise, c.g + noise, c.b + noise, 1f));
                }
            return canvas;
        }

        private static float Falloff(float dx, float dy, float radius) => Mathf.Exp(-(dx * dx + dy * dy) / (radius * radius));

        private static Canvas Rune(int size)
        {
            Canvas canvas = new Canvas(size, size);
            float c = size * 0.5f, s = size / 1024f;
            Color white = Color.white;
            const float glow = 6f, glowAlpha = 0.3f;
            canvas.Ring(c, c, 500 * s, 2.2f * s, white, glow, glowAlpha);
            canvas.Ring(c, c, 486 * s, 1f * s, white, 0f, 0f);
            canvas.Ring(c, c, 420 * s, 1.6f * s, white, glow, glowAlpha);
            canvas.Ring(c, c, 404 * s, 1f * s, white, 0f, 0f);
            for (int i = 0; i < 72; i++)
            {
                float a = i * Mathf.PI * 2f / 72f;
                bool major = i % 6 == 0;
                canvas.Line(c, c, a, (major ? 466 : 488) * s, 498 * s, (major ? 1.7f : 1f) * s, white, major ? glow : 0f, glowAlpha);
            }
            System.Random random = new System.Random(7);
            for (int i = 0; i < 24; i++)
            {
                float a = (i + 0.5f) * Mathf.PI * 2f / 24f;
                Vector2 n = new Vector2(Mathf.Cos(a), Mathf.Sin(a)), t = new Vector2(-n.y, n.x);
                Vector2 centre = new Vector2(c, c) + n * 445f * s;
                Vector2 Point() => centre + t * ((float)random.NextDouble() - 0.5f) * 26f * s + n * ((float)random.NextDouble() - 0.5f) * 30f * s;
                for (int k = 0; k < 3; k++) canvas.Segment(Point(), Point(), 1.4f * s, white, 3f, 0.25f);
            }
            for (int i = 0; i < 8; i++)
            {
                float a0 = i * Mathf.PI / 2f + (i >= 4 ? Mathf.PI / 4f : 0f), a1 = a0 + Mathf.PI / 2f;
                Vector2 p0 = new Vector2(c + Mathf.Cos(a0) * 400f * s, c + Mathf.Sin(a0) * 400f * s);
                Vector2 p1 = new Vector2(c + Mathf.Cos(a1) * 400f * s, c + Mathf.Sin(a1) * 400f * s);
                canvas.Segment(p0, p1, 1.4f * s, white, glow, glowAlpha);
            }
            canvas.Ring(c, c, 283 * s, 1.2f * s, white, 0f, 0f);
            canvas.Ring(c, c, 200 * s, 2f * s, white, glow, glowAlpha);
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI / 6f;
                canvas.Ring(c + Mathf.Cos(a) * 240f * s, c + Mathf.Sin(a) * 240f * s, 10f * s, 1f * s, white, 3f, 0.25f);
            }
            canvas.Shape(c - 80 * s, c - 80 * s, c + 80 * s, c + 80 * s, (x, y) => StarSdf(x, y, c, c, 70 * s, 12 * s, 4, 0f),
                (x, y) => white, 10f * s, 0.6f);
            return canvas;
        }

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

        // 9-slice halo: transparent inside the inner rectangle so it only glows around the element.
        private static Canvas Halo(int size)
        {
            Canvas canvas = new Canvas(size, size);
            float c = size * 0.5f, inner = c - 36f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = BoxSdf(x + 0.5f, y + 0.5f, c, c, inner, inner, 2f);
                    float a = d <= 0f ? 0f : 0.9f * Mathf.Exp(-d / 9f) * Mathf.Clamp01(1f - d / 34f);
                    canvas.Set(x, y, new Color(1f, 1f, 1f, a));
                }
            return canvas;
        }

        // 9-slice panel frame (slice 26): navy fill, double gold line, corner brackets and diamonds.
        private static Canvas Frame(int size)
        {
            Canvas canvas = new Canvas(size, size);
            float c = size * 0.5f, half = c - 4f;
            Color edge = new Color(0.03f, 0.05f, 0.1f), centre = new Color(0.06f, 0.09f, 0.17f);
            canvas.Shape(0, 0, size, size, (x, y) => BoxSdf(x, y, c, c, half, half, 6f), (x, y) =>
            {
                Color fill = Color.Lerp(edge, centre, Mathf.Clamp01(-BoxSdf(x, y, c, c, half, half, 6f) / 14f));
                fill.a = 0.93f;
                return fill;
            });
            canvas.Shape(0, 0, size, size, (x, y) => Mathf.Abs(BoxSdf(x, y, c, c, half, half, 6f)) - 0.9f, (x, y) => Gold);
            canvas.Shape(0, 0, size, size, (x, y) => Mathf.Abs(BoxSdf(x, y, c, c, half - 5f, half - 5f, 3f)) - 0.55f,
                (x, y) => new Color(Gold.r, Gold.g, Gold.b, 0.35f));
            canvas.Segment(new Vector2(14f, 6.5f), new Vector2(size - 14f, 6.5f), 0.5f, new Color(1f, 1f, 1f, 0.12f), 0f, 0f);
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                {
                    Vector2 corner = new Vector2(c + sx * half, c + sy * half);
                    canvas.Segment(corner + new Vector2(-sx * 2f, 0f), corner + new Vector2(-sx * 20f, 0f), 1.6f, BrightGold, 0f, 0f);
                    canvas.Segment(corner + new Vector2(0f, -sy * 2f), corner + new Vector2(0f, -sy * 20f), 1.6f, BrightGold, 0f, 0f);
                    Vector2 gem = new Vector2(c + sx * (half - 5f), c + sy * (half - 5f));
                    canvas.Shape(gem.x - 8, gem.y - 8, gem.x + 8, gem.y + 8, (x, y) => DiamondSdf(x, y, gem.x, gem.y, 4.5f, 4.5f),
                        (x, y) => BrightGold, 3f, 0.5f);
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

        private static Canvas Star(int size)
        {
            Canvas canvas = new Canvas(size, size);
            float c = size * 0.5f;
            canvas.Shape(0, 0, size, size, (x, y) => StarSdf(x, y, c, c, size * 0.47f, size * 0.07f, 4, 0f), (x, y) => Color.white, 7f, 0.5f);
            canvas.Shape(0, 0, size, size, (x, y) => StarSdf(x, y, c, c, size * 0.27f, size * 0.05f, 4, Mathf.PI / 4f),
                (x, y) => new Color(1f, 1f, 1f, 0.75f), 0f, 0f);
            return canvas;
        }

        private static Canvas Staff(Color orb, bool rune)
        {
            const int size = 128;
            Canvas canvas = new Canvas(size, size);
            Vector2 butt = new Vector2(24f, 108f), head = new Vector2(84f, 46f), centre = new Vector2(92f, 36f);
            canvas.Segment(butt, head, 4.2f, new Color(0.4f, 0.27f, 0.16f), 0f, 0f);
            canvas.Segment(butt, head, 1.2f, new Color(0.62f, 0.45f, 0.28f), 0f, 0f);
            foreach (float t in new[] { 0.3f, 0.72f, 0.95f })
            {
                Vector2 p = Vector2.Lerp(butt, head, t), d = (head - butt).normalized * 2.5f;
                canvas.Segment(p - d, p + d, 5.6f, Gold, 0f, 0f);
            }
            canvas.Ring(centre.x, centre.y, 19f, 2.6f, Gold, 0f, 0f);
            canvas.Shape(centre.x - 40, centre.y - 40, centre.x + 40, centre.y + 40, (x, y) => Len(x - centre.x, y - centre.y) - 13f,
                (x, y) => Color.Lerp(Color.white, orb, Mathf.Clamp01(Len(x - centre.x + 4f, y - centre.y + 4f) / 16f)), 11f, 0.85f);
            if (rune)
            {
                canvas.Ring(centre.x, centre.y, 27f, 1f, new Color(orb.r, orb.g, orb.b, 0.8f), 0f, 0f);
                for (int i = 0; i < 4; i++)
                {
                    float a = i * Mathf.PI / 2f + Mathf.PI / 4f;
                    float gx = centre.x + Mathf.Cos(a) * 27f, gy = centre.y + Mathf.Sin(a) * 27f;
                    canvas.Shape(gx - 6, gy - 6, gx + 6, gy + 6, (x, y) => DiamondSdf(x, y, gx, gy, 3.5f, 3.5f), (x, y) => orb, 3f, 0.6f);
                }
            }
            canvas.Shape(centre.x - 30, centre.y - 30, centre.x + 30, centre.y + 30,
                (x, y) => StarSdf(x, y, centre.x, centre.y, 24f, 1.5f, 4, 0f), (x, y) => new Color(1f, 1f, 1f, 0.9f), 3f, 0.4f);
            return canvas;
        }

        private static Canvas Ring(Color gem)
        {
            const int size = 128;
            Canvas canvas = new Canvas(size, size);
            const float cx = 64f, cy = 78f, rx = 38f, ry = 28f;
            canvas.Shape(0, 0, size, size, (x, y) => Mathf.Abs(EllipseSdf(x, y, cx, cy, rx, ry)) - 7f,
                (x, y) => Color.Lerp(new Color(1f, 0.88f, 0.55f), new Color(0.5f, 0.34f, 0.12f), Mathf.Clamp01((y - cy + ry) / (2f * ry))));
            canvas.Shape(0, 0, size, size, (x, y) => Mathf.Abs(EllipseSdf(x, y, cx, cy - 3f, rx - 2f, ry - 2f)) - 1.2f,
                (x, y) => new Color(1f, 1f, 1f, y < cy - 6f ? 0.45f : 0f));
            canvas.Shape(40, 30, 88, 60, (x, y) => BoxSdf(x, y, 64f, 50f, 13f, 7f, 3f), (x, y) => new Color(0.62f, 0.45f, 0.18f));
            canvas.Shape(30, 0, 98, 76, (x, y) => DiamondSdf(x, y, 64f, 38f, 16f, 20f),
                (x, y) =>
                {
                    Color facet = Color.Lerp(gem, Color.white, x < 64f ? 0.3f : 0f) * (y > 38f ? 0.8f : 1f);
                    facet.a = 1f;
                    return facet;
                }, 9f, 0.7f);
            canvas.Shape(48, 20, 70, 44, (x, y) => DiamondSdf(x, y, 59f, 32f, 4f, 6f), (x, y) => new Color(1f, 1f, 1f, 0.85f));
            return canvas;
        }

        // ---------- Level-up cards ----------

        // 9-slice card frame (slice 48): navy fill, triple gold line, long corner brackets with filigree arcs and gems.
        private static Canvas CardFrame(int size)
        {
            Canvas canvas = new Canvas(size, size);
            float c = size * 0.5f, half = c - 4f;
            Color edge = new Color(0.025f, 0.04f, 0.085f), centre = new Color(0.06f, 0.09f, 0.18f);
            canvas.Shape(0, 0, size, size, (x, y) => BoxSdf(x, y, c, c, half, half, 10f), (x, y) =>
            {
                Color fill = Color.Lerp(edge, centre, Mathf.Clamp01(-BoxSdf(x, y, c, c, half, half, 10f) / 30f));
                fill.a = 0.96f;
                return fill;
            });
            canvas.Shape(0, 0, size, size, (x, y) => Mathf.Abs(BoxSdf(x, y, c, c, half, half, 10f)) - 1.3f, (x, y) => Gold);
            canvas.Shape(0, 0, size, size, (x, y) => Mathf.Abs(BoxSdf(x, y, c, c, half - 8f, half - 8f, 6f)) - 0.7f,
                (x, y) => new Color(Gold.r, Gold.g, Gold.b, 0.55f));
            canvas.Shape(0, 0, size, size, (x, y) => Mathf.Abs(BoxSdf(x, y, c, c, half - 14f, half - 14f, 3f)) - 0.5f,
                (x, y) => new Color(Gold.r, Gold.g, Gold.b, 0.18f));
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                {
                    Vector2 corner = new Vector2(c + sx * (half - 8f), c + sy * (half - 8f));
                    canvas.Segment(corner, corner + new Vector2(-sx * 34f, 0f), 1.5f, BrightGold, 2f, 0.3f);
                    canvas.Segment(corner, corner + new Vector2(0f, -sy * 34f), 1.5f, BrightGold, 2f, 0.3f);
                    // Quarter-circle filigree opening toward the card centre.
                    float fx = corner.x - sx * 18f, fy = corner.y - sy * 18f;
                    canvas.Shape(fx - 14, fy - 14, fx + 14, fy + 14, (x, y) => Mathf.Abs(Len(x - fx, y - fy) - 10f) - 0.8f,
                        (x, y) => (x - fx) * sx > 0f || (y - fy) * sy > 0f ? new Color(BrightGold.r, BrightGold.g, BrightGold.b, 0.85f) : Color.clear);
                    canvas.Shape(fx - 5, fy - 5, fx + 5, fy + 5, (x, y) => DiamondSdf(x, y, fx, fy, 2.5f, 2.5f), (x, y) => BrightGold);
                    Vector2 gem = new Vector2(c + sx * half, c + sy * half);
                    canvas.Shape(gem.x - 10, gem.y - 10, gem.x + 10, gem.y + 10, (x, y) => DiamondSdf(x, y, gem.x, gem.y, 6f, 6f),
                        (x, y) => BrightGold, 4f, 0.55f);
                }
            return canvas;
        }

        private const int IconSize = 192;
        private static readonly Color DarkGold = new Color(0.5f, 0.34f, 0.12f);

        private static Color Vertical(Color top, Color bottom, float y, float y0, float y1) => Color.Lerp(top, bottom, Mathf.Clamp01((y - y0) / (y1 - y0)));

        private static Canvas AuraSizeIcon()
        {
            Canvas canvas = new Canvas(IconSize, IconSize);
            const float c = 96f;
            Color aura = new Color(1f, 0.84f, 0.5f);
            canvas.Ring(c, c, 80f, 1.4f, new Color(aura.r, aura.g, aura.b, 0.45f), 4f, 0.25f);
            canvas.Ring(c, c, 58f, 2.4f, new Color(aura.r, aura.g, aura.b, 0.75f), 5f, 0.35f);
            canvas.Ring(c, c, 36f, 3f, aura, 6f, 0.5f);
            for (int i = 0; i < 4; i++)
            {
                // Outward chevrons: the aura grows.
                float a = i * Mathf.PI / 2f + Mathf.PI / 4f;
                Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a)), side = new Vector2(-dir.y, dir.x);
                Vector2 tip = new Vector2(c, c) + dir * 90f, back = new Vector2(c, c) + dir * 74f;
                canvas.Segment(back + side * 10f, tip, 2.4f, BrightGold, 3f, 0.4f);
                canvas.Segment(back - side * 10f, tip, 2.4f, BrightGold, 3f, 0.4f);
            }
            canvas.Shape(c - 34, c - 34, c + 34, c + 34, (x, y) => StarSdf(x, y, c, c, 30f, 6f, 4, 0f),
                (x, y) => Color.Lerp(Color.white, aura, Len(x - c, y - c) / 30f), 8f, 0.7f);
            return canvas;
        }

        private static Canvas HarvestIcon()
        {
            Canvas canvas = new Canvas(IconSize, IconSize);
            Color wheat = new Color(1f, 0.86f, 0.46f), deep = new Color(0.8f, 0.55f, 0.18f);
            Vector2 tie = new Vector2(96f, 150f);
            foreach (float tilt in new[] { -0.36f, 0f, 0.36f })
            {
                Vector2 dir = new Vector2(Mathf.Sin(tilt), -Mathf.Cos(tilt)), side = new Vector2(-dir.y, dir.x);
                Vector2 top = tie + dir * 112f;
                canvas.Segment(tie - dir * 28f, top, 2f, new Color(0.62f, 0.72f, 0.3f), 0f, 0f);
                for (int k = 0; k < 5; k++)
                    for (int s = -1; s <= 1; s += 2)
                    {
                        Vector2 p = tie + dir * (48f + k * 13f) + side * s * 7f;
                        float angle = Mathf.Atan2(dir.y, dir.x) + s * 0.45f;
                        canvas.Shape(p.x - 12, p.y - 12, p.x + 12, p.y + 12, (x, y) => RotatedEllipseSdf(x, y, p.x, p.y, 9f, 4.6f, angle),
                            (x, y) => Vertical(wheat, deep, y, 30f, 130f), 3f, 0.3f);
                    }
                canvas.Shape(top.x - 12, top.y - 12, top.x + 12, top.y + 12,
                    (x, y) => RotatedEllipseSdf(x, y, top.x, top.y, 9f, 4.6f, Mathf.Atan2(dir.y, dir.x)), (x, y) => wheat, 3f, 0.3f);
            }
            canvas.Shape(70, 140, 122, 160, (x, y) => BoxSdf(x, y, 96f, 150f, 20f, 6f, 3f), (x, y) => Vertical(BrightGold, DarkGold, y, 144f, 156f));
            canvas.Shape(88, 142, 104, 158, (x, y) => DiamondSdf(x, y, 96f, 150f, 5f, 5f), (x, y) => new Color(0.5f, 0.85f, 0.4f));
            return canvas;
        }

        private static Canvas WellspringIcon()
        {
            Canvas canvas = new Canvas(IconSize, IconSize);
            Color mana = new Color(0.4f, 0.62f, 1f);
            Vector2 centre = new Vector2(96f, 116f), apex = new Vector2(96f, 26f);
            const float r = 44f;
            float tangent = Mathf.Asin(r / (centre - apex).magnitude);
            Vector2 left = centre + new Vector2(-Mathf.Cos(tangent), -Mathf.Sin(tangent)) * r;
            Vector2 right = centre + new Vector2(Mathf.Cos(tangent), -Mathf.Sin(tangent)) * r;
            float Drop(float x, float y) => Mathf.Min(Len(x - centre.x, y - centre.y) - r, ConvexSdf(x, y, apex, right, centre, left));
            // Recovery arc around the drop with an arrowhead.
            canvas.Shape(0, 0, IconSize, IconSize, (x, y) => Mathf.Abs(Len(x - 96f, y - 106f) - 74f) - 2.2f,
                (x, y) => y > 100f ? new Color(mana.r, mana.g, mana.b, 0.8f) : Color.clear, 4f, 0.35f);
            canvas.Shape(150, 82, 186, 118, (x, y) => ConvexSdf(x, y, new Vector2(170f, 84f), new Vector2(182f, 106f), new Vector2(158f, 106f)),
                (x, y) => mana, 4f, 0.35f);
            canvas.Shape(0, 0, IconSize, IconSize, Drop,
                (x, y) => Color.Lerp(Color.white, mana, Mathf.Clamp01(Len(x - 82f, y - 96f) / 52f)), 9f, 0.6f);
            canvas.Shape(0, 0, IconSize, IconSize, (x, y) => Mathf.Abs(Drop(x, y)) - 1.6f, (x, y) => Gold);
            canvas.Shape(64, 84, 98, 132, (x, y) => RotatedEllipseSdf(x, y, 80f, 108f, 7f, 15f, 0.35f), (x, y) => new Color(1f, 1f, 1f, 0.75f));
            canvas.Shape(120, 30, 160, 70, (x, y) => StarSdf(x, y, 140f, 50f, 16f, 2.5f, 4, 0f), (x, y) => Color.white, 4f, 0.5f);
            return canvas;
        }

        private static Canvas ReservesIcon()
        {
            Canvas canvas = new Canvas(IconSize, IconSize);
            Color food = new Color(1f, 0.68f, 0.24f), mana = new Color(0.4f, 0.62f, 1f);
            float Body(float x, float y) => Mathf.Min(EllipseSdf(x, y, 96f, 122f, 50f, 48f),
                Mathf.Min(BoxSdf(x, y, 96f, 70f, 20f, 14f, 2f), BoxSdf(x, y, 96f, 54f, 30f, 6f, 3f)));
            canvas.Shape(0, 0, IconSize, IconSize, (x, y) => Body(x, y) - 1f, (x, y) =>
            {
                if (y < 98f) return new Color(0.08f, 0.12f, 0.24f);
                return y < 124f ? Vertical(new Color(0.75f, 0.88f, 1f), mana, y, 98f, 124f) : Vertical(BrightGold, food, y, 124f, 168f);
            }, 8f, 0.4f);
            canvas.Shape(0, 0, IconSize, IconSize, (x, y) => Mathf.Abs(Body(x, y)) - 2.6f, (x, y) => Vertical(BrightGold, DarkGold, y, 50f, 170f));
            canvas.Segment(new Vector2(52f, 98f), new Vector2(140f, 98f), 1f, new Color(1f, 1f, 1f, 0.7f), 0f, 0f);
            canvas.Shape(56, 100, 80, 150, (x, y) => RotatedEllipseSdf(x, y, 68f, 124f, 5f, 18f, -0.2f), (x, y) => new Color(1f, 1f, 1f, 0.4f));
            canvas.Segment(new Vector2(150f, 34f), new Vector2(150f, 66f), 3f, BrightGold, 3f, 0.45f);
            canvas.Segment(new Vector2(134f, 50f), new Vector2(166f, 50f), 3f, BrightGold, 3f, 0.45f);
            return canvas;
        }

        private static Canvas BannerIcon()
        {
            Canvas canvas = new Canvas(IconSize, IconSize);
            Color violet = new Color(0.62f, 0.46f, 1f), deep = new Color(0.3f, 0.18f, 0.6f);
            float Flag(float x, float y) => Mathf.Max(BoxSdf(x, y, 106f, 98f, 38f, 58f, 2f),
                -ConvexSdf(x, y, new Vector2(106f, 128f), new Vector2(150f, 160f), new Vector2(62f, 160f)));
            canvas.Segment(new Vector2(40f, 34f), new Vector2(172f, 34f), 3.2f, Vertical(BrightGold, DarkGold, 0.5f, 0f, 1f), 0f, 0f);
            canvas.Shape(0, 0, IconSize, IconSize, Flag, (x, y) => Vertical(violet, deep, y, 40f, 160f), 7f, 0.4f);
            canvas.Shape(0, 0, IconSize, IconSize, (x, y) => Mathf.Abs(Flag(x, y) + 5f) - 1.2f, (x, y) => Gold);
            canvas.Shape(76, 64, 136, 124, (x, y) => StarSdf(x, y, 106f, 92f, 26f, 6f, 4, 0f), (x, y) => BrightGold, 5f, 0.5f);
            foreach (float px in new[] { 36f, 176f })
                canvas.Shape(px - 9, 25, px + 9, 43, (x, y) => DiamondSdf(x, y, px, 34f, 7f, 7f), (x, y) => BrightGold, 3f, 0.4f);
            for (int i = 0; i < 3; i++)
                canvas.Segment(new Vector2(80f + i * 26f, 38f), new Vector2(80f + i * 26f, 44f), 2.2f, Gold, 0f, 0f);
            return canvas;
        }

        private static Canvas BlessedBladeIcon()
        {
            Canvas canvas = new Canvas(IconSize, IconSize);
            const float c = 96f;
            Color glow = new Color(1f, 0.78f, 0.42f), steel = new Color(0.86f, 0.92f, 1f);
            canvas.Ring(c, c, 72f, 2.4f, glow, 6f, 0.4f);
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI / 8f;
                canvas.Line(c, c, a, 78f, i % 2 == 0 ? 92f : 86f, 1.6f, glow, 2f, 0.3f);
            }
            canvas.Shape(70, 14, 122, 150, (x, y) => ConvexSdf(x, y, new Vector2(96f, 22f), new Vector2(108f, 44f), new Vector2(106f, 132f),
                new Vector2(86f, 132f), new Vector2(84f, 44f)), (x, y) => x < 96f ? Color.white : steel, 8f, 0.6f);
            canvas.Segment(new Vector2(96f, 40f), new Vector2(96f, 126f), 0.8f, new Color(0.55f, 0.65f, 0.85f), 0f, 0f);
            canvas.Shape(56, 126, 136, 146, (x, y) => BoxSdf(x, y, 96f, 136f, 36f, 6f, 3f), (x, y) => Vertical(BrightGold, DarkGold, y, 130f, 142f));
            canvas.Shape(86, 140, 106, 172, (x, y) => BoxSdf(x, y, 96f, 156f, 5f, 15f, 2f), (x, y) => new Color(0.42f, 0.28f, 0.16f));
            canvas.Ring(96f, 176f, 6f, 2.6f, Gold, 0f, 0f);
            return canvas;
        }

        private static Canvas MightIcon()
        {
            Canvas canvas = new Canvas(IconSize, IconSize);
            const float c = 96f;
            Color ember = new Color(1f, 0.5f, 0.3f), flare = new Color(1f, 0.86f, 0.5f);
            canvas.Shape(0, 0, IconSize, IconSize, (x, y) => StarSdf(x, y, c, c, 88f, 30f, 8, Mathf.PI / 8f),
                (x, y) => Color.Lerp(flare, ember, Len(x - c, y - c) / 88f), 8f, 0.5f);
            canvas.Shape(0, 0, IconSize, IconSize, (x, y) => Mathf.Abs(StarSdf(x, y, c, c, 88f, 30f, 8, Mathf.PI / 8f)) - 1.2f,
                (x, y) => new Color(BrightGold.r, BrightGold.g, BrightGold.b, 0.8f));
            canvas.Shape(c - 60, c - 60, c + 60, c + 60, (x, y) => StarSdf(x, y, c, c, 56f, 8f, 4, 0f), (x, y) => Color.white, 8f, 0.7f);
            canvas.Shape(c - 20, c - 20, c + 20, c + 20, (x, y) => Len(x - c, y - c) - 12f, (x, y) => Color.white, 10f, 0.8f);
            return canvas;
        }

        private static Canvas HeartIcon()
        {
            Canvas canvas = new Canvas(IconSize, IconSize);
            Color rose = new Color(1f, 0.45f, 0.5f), crimson = new Color(0.62f, 0.08f, 0.18f);
            float Heart(float x, float y) => Mathf.Min(Mathf.Min(Len(x - 72f, y - 82f) - 32f, Len(x - 120f, y - 82f) - 32f),
                ConvexSdf(x, y, new Vector2(43f, 96f), new Vector2(149f, 96f), new Vector2(96f, 162f)));
            canvas.Shape(0, 0, IconSize, IconSize, Heart, (x, y) => Vertical(rose, crimson, y, 50f, 160f), 9f, 0.5f);
            canvas.Shape(0, 0, IconSize, IconSize, (x, y) => Mathf.Abs(Heart(x, y)) - 2.4f, (x, y) => Gold);
            canvas.Shape(52, 58, 84, 94, (x, y) => RotatedEllipseSdf(x, y, 68f, 76f, 7f, 13f, -0.6f), (x, y) => new Color(1f, 1f, 1f, 0.6f));
            canvas.Segment(new Vector2(150f, 30f), new Vector2(150f, 64f), 4f, Color.white, 4f, 0.5f);
            canvas.Segment(new Vector2(133f, 47f), new Vector2(167f, 47f), 4f, Color.white, 4f, 0.5f);
            return canvas;
        }

        // ---------- Raster helpers (y grows downward while drawing) ----------

        private static float Len(float x, float y) => Mathf.Sqrt(x * x + y * y);

        private static float Hash(int x, int y)
        {
            uint h = (uint)(x * 374761393 + y * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return (h ^ (h >> 16)) / (float)uint.MaxValue;
        }

        private static float BoxSdf(float x, float y, float cx, float cy, float hx, float hy, float r)
        {
            float qx = Mathf.Abs(x - cx) - hx + r, qy = Mathf.Abs(y - cy) - hy + r;
            return Len(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        private static float DiamondSdf(float x, float y, float cx, float cy, float rx, float ry) =>
            (Mathf.Abs(x - cx) / rx + Mathf.Abs(y - cy) / ry - 1f) * Mathf.Min(rx, ry) * 0.7071f;

        private static float EllipseSdf(float x, float y, float cx, float cy, float rx, float ry)
        {
            float nx = (x - cx) / rx, ny = (y - cy) / ry;
            return (Mathf.Sqrt(nx * nx + ny * ny) - 1f) * Mathf.Min(rx, ry);
        }

        private static float RotatedEllipseSdf(float x, float y, float cx, float cy, float rx, float ry, float angle)
        {
            float dx = x - cx, dy = y - cy, cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
            return EllipseSdf(cx + dx * cos + dy * sin, cy - dx * sin + dy * cos, cx, cy, rx, ry);
        }

        // Convex polygon (any winding): exact inside, slightly rounded outside corners, which suits anti-aliasing.
        private static float ConvexSdf(float x, float y, params Vector2[] points)
        {
            float area = 0f;
            for (int i = 0; i < points.Length; i++)
            {
                Vector2 a = points[i], b = points[(i + 1) % points.Length];
                area += a.x * b.y - b.x * a.y;
            }
            float sign = area > 0f ? 1f : -1f, distance = float.MinValue;
            for (int i = 0; i < points.Length; i++)
            {
                Vector2 a = points[i], edge = points[(i + 1) % points.Length] - a;
                Vector2 normal = new Vector2(edge.y, -edge.x).normalized * sign;
                distance = Mathf.Max(distance, (x - a.x) * normal.x + (y - a.y) * normal.y);
            }
            return distance;
        }

        private static float StarSdf(float x, float y, float cx, float cy, float outer, float inner, int points, float rotation)
        {
            float dx = x - cx, dy = y - cy;
            float k = Mathf.Abs(Mathf.Cos((Mathf.Atan2(dy, dx) - rotation) * points * 0.5f));
            return (Len(dx, dy) - Mathf.Lerp(inner, outer, Mathf.Pow(k, 6f))) * 0.8f;
        }

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

            private void Over(int x, int y, Color c, float a)
            {
                int i = (Height - 1 - y) * Width + x;
                Color d = Pixels[i];
                float outA = a + d.a * (1f - a);
                if (outA <= 0f) return;
                float keep = d.a * (1f - a);
                Pixels[i] = new Color((c.r * a + d.r * keep) / outA, (c.g * a + d.g * keep) / outA, (c.b * a + d.b * keep) / outA, outA);
            }

            // Signed distance shape (negative inside) with one-pixel anti-aliasing and an optional outer glow.
            public void Shape(float x0, float y0, float x1, float y1, Func<float, float, float> sdf, Func<float, float, Color> paint,
                float glow = 0f, float glowAlpha = 0f)
            {
                float pad = glow * 3f + 2f;
                int ix0 = Mathf.Max(0, (int)(x0 - pad)), iy0 = Mathf.Max(0, (int)(y0 - pad));
                int ix1 = Mathf.Min(Width, (int)(x1 + pad) + 1), iy1 = Mathf.Min(Height, (int)(y1 + pad) + 1);
                for (int y = iy0; y < iy1; y++)
                    for (int x = ix0; x < ix1; x++)
                    {
                        float px = x + 0.5f, py = y + 0.5f, d = sdf(px, py);
                        float a = Mathf.Clamp01(0.5f - d);
                        if (glow > 0f && d > 0f) a = Mathf.Max(a, glowAlpha * Mathf.Exp(-d / glow));
                        if (a <= 0.001f) continue;
                        Color c = paint(px, py);
                        Over(x, y, c, a * c.a);
                    }
            }

            public void Ring(float cx, float cy, float r, float halfWidth, Color color, float glow, float glowAlpha) =>
                Shape(cx - r - halfWidth, cy - r - halfWidth, cx + r + halfWidth, cy + r + halfWidth,
                    (x, y) => Mathf.Abs(Len(x - cx, y - cy) - r) - halfWidth, (x, y) => color, glow, glowAlpha);

            public void Line(float cx, float cy, float angle, float r0, float r1, float halfWidth, Color color, float glow, float glowAlpha)
            {
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Segment(new Vector2(cx, cy) + dir * r0, new Vector2(cx, cy) + dir * r1, halfWidth, color, glow, glowAlpha);
            }

            public void Segment(Vector2 a, Vector2 b, float halfWidth, Color color, float glow, float glowAlpha)
            {
                Vector2 ba = b - a;
                float lengthSq = Mathf.Max(ba.sqrMagnitude, 1e-4f);
                Shape(Mathf.Min(a.x, b.x) - halfWidth, Mathf.Min(a.y, b.y) - halfWidth, Mathf.Max(a.x, b.x) + halfWidth, Mathf.Max(a.y, b.y) + halfWidth,
                    (x, y) =>
                    {
                        float px = x - a.x, py = y - a.y;
                        float h = Mathf.Clamp01((px * ba.x + py * ba.y) / lengthSq);
                        return Len(px - ba.x * h, py - ba.y * h) - halfWidth;
                    }, (x, y) => color, glow, glowAlpha);
            }
        }
    }
}

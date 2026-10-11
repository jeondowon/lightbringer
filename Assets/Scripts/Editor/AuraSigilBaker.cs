using System.IO;
using UnityEditor;
using UnityEngine;

namespace Lightbringer.EditorTools
{
    // Bakes a linear RGB mask: blue-white sigils, gold ornaments, fine inner filigree.
    // Generated once in the Editor; no texture generation or allocation during gameplay.
    [InitializeOnLoad]
    public static class AuraSigilBaker
    {
        public const string TexturePath = "Assets/Art/Textures/LB_AuraSigil.png";
        private const int Size = 2048;
        private static Color32[] pixels;

        static AuraSigilBaker() => EditorApplication.delayCall += Ensure;
        private static void Ensure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!File.Exists(TexturePath)) Bake();
        }

        [MenuItem("Lightbringer/Art/Rebuild Aura Sigil Texture")]
        public static void Bake()
        {
            pixels = new Color32[Size * Size];
            // Double rune bands, with a repeated authored alphabet of angular strokes.
            for (int i = 0; i < 48; i++) Rune(i * 360f / 48f, 0.866f, 0.040f, i, 0);
            for (int i = 0; i < 32; i++) Rune(i * 360f / 32f + 3f, 0.659f, 0.027f, i + 5, 2);
            for (int i = 0; i < 96; i++)
            {
                float a = i * 360f / 96f;
                Stroke(Polar(a, 0.944f), Polar(a, i % 4 == 0 ? 0.969f : 0.955f), 0.0014f, 1);
            }
            for (int i = 0; i < 12; i++)
            {
                float a = i * 30f;
                // Small compass diamonds interrupt the outer band.
                Diamond(a, 0.755f, 0.018f, 0.031f, 1);
                Stroke(Polar(a, 0.695f), Polar(a, 0.729f), 0.0012f, 1);
                // Interlaced curved petals in the inner mandala.
                Vector2 previous = Polar(a, 0.20f);
                for (int j = 1; j <= 48; j++)
                {
                    float t = j / 48f;
                    Vector2 p = Polar(a + Mathf.Sin(t * Mathf.PI) * 18f, 0.20f + 0.38f * t);
                    Stroke(previous, p, 0.0014f, 2); previous = p;
                }
                previous = Polar(a, 0.20f);
                for (int j = 1; j <= 48; j++)
                {
                    float t = j / 48f;
                    Vector2 p = Polar(a - Mathf.Sin(t * Mathf.PI) * 18f, 0.20f + 0.38f * t);
                    Stroke(previous, p, 0.0014f, 2); previous = p;
                }
            }
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f;
                Stroke(Polar(a, 0.32f), Polar(a + 135f, 0.32f), 0.002f, 0);
                Diamond(a, 0.595f, 0.013f, 0.021f, 0);
            }
            Circle(0.20f, 0.0015f, 1);
            Circle(0.325f, 0.001f, 0);
            Circle(0.605f, 0.0013f, 2);
            Circle(0.622f, 0.001f, 2);
            Circle(0.697f, 0.001f, 1);
            var texture = new Texture2D(Size, Size, TextureFormat.RGB24, true, true);
            texture.SetPixels32(pixels);
            texture.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(TexturePath));
            File.WriteAllBytes(TexturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            pixels = null;
            AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
            importer.sRGBTexture = false;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 8;
            importer.maxTextureSize = Size;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/LB_AuraRunes.mat");
            if (material != null)
            {
                material.SetTexture("_SigilTex", AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath));
                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssetIfDirty(material);
            }
        }

        private static Vector2 Polar(float angle, float radius) => new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * radius;
        private static void Circle(float radius, float width, int channel)
        {
            for (int i = 0; i < 720; i++) Stroke(Polar(i * 0.5f, radius), Polar((i + 1) * 0.5f, radius), width, channel);
        }
        private static void Diamond(float angle, float radius, float w, float h, int channel)
        {
            Vector2 c = Polar(angle, radius), up = Polar(angle, h), side = Polar(angle + 90f, w);
            Stroke(c + up, c + side, 0.0015f, channel); Stroke(c + side, c - up, 0.0015f, channel);
            Stroke(c - up, c - side, 0.0015f, channel); Stroke(c - side, c + up, 0.0015f, channel);
        }
        private static void Rune(float angle, float radius, float h, int index, int channel)
        {
            Vector2 c = Polar(angle, radius), up = Polar(angle, h), side = Polar(angle + 90f, h * 0.46f);
            Stroke(c - up * 0.8f, c + up * 0.8f, 0.0018f, channel);
            Stroke(c + up * 0.7f, c + side + up * 0.2f, 0.0016f, channel);
            Stroke(c + side + up * 0.2f, c + up * -0.15f, 0.0016f, channel);
            if ((index & 1) == 0) Stroke(c - up * 0.45f, c - side + up * 0.1f, 0.0016f, channel);
            else Stroke(c - side - up * 0.4f, c + side - up * 0.4f, 0.0016f, channel);
            if ((index & 2) == 0) Stroke(c + up * 0.65f, c - side + up * 0.2f, 0.0016f, channel);
            if ((index & 4) != 0) Stroke(c - side - up * 0.8f, c + side - up * 0.8f, 0.0016f, channel);
        }
        private static void Stroke(Vector2 a, Vector2 b, float width, int channel)
        {
            a = (a * 0.5f + Vector2.one * 0.5f) * (Size - 1);
            b = (b * 0.5f + Vector2.one * 0.5f) * (Size - 1);
            float half = width * Size * 0.25f;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x) - half - 1));
            int x1 = Mathf.Min(Size - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x) + half + 1));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y) - half - 1));
            int y1 = Mathf.Min(Size - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y) + half + 1));
            Vector2 ab = b - a;
            for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++)
            {
                Vector2 p = new Vector2(x, y);
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
                byte value = (byte)(Mathf.Clamp01(half + 0.75f - Vector2.Distance(p, a + t * ab)) * 255f);
                int n = y * Size + x;
                Color32 old = pixels[n];
                if (channel == 0) old.r = (byte)Mathf.Max(old.r, value);
                if (channel == 1) old.g = (byte)Mathf.Max(old.g, value);
                if (channel == 2) old.b = (byte)Mathf.Max(old.b, value);
                pixels[n] = old;
            }
        }
    }
}

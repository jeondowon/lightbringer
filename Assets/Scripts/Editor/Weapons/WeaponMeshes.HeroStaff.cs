using System.IO;
using UnityEditor;
using UnityEngine;

namespace Lightbringer.EditorTools
{
    // Surfaces for the hero staff: aged engraved gold (albedo, normal and a metallic/smoothness mask) and glass.
    internal static partial class WeaponMeshes
    {
        // Brushed, mottled gold with a diamond engraving, tiny pits and darker patina in the hollows. The mask
        // carries metallic in R and smoothness in A (URP Lit reads them that way), so wear shows as duller spots.
        public static Material AgedGold(string folder, string name)
        {
            string albedo = $"{TextureFolder}/AgedGold_Albedo.png", normal = $"{TextureFolder}/AgedGold_Normal.png", mask = $"{TextureFolder}/AgedGold_Mask.png";
            if (!File.Exists(albedo) || !File.Exists(normal) || !File.Exists(mask)) GenerateAgedGold();
            Material material = RigBuildUtility.MakeMaterial(folder, name, Color.white, 1f, 1f);
            material.SetTexture("_BaseMap", Load(albedo));
            material.SetTexture("_BumpMap", Load(normal));
            material.SetTexture("_MetallicGlossMap", Load(mask));
            material.SetFloat("_BumpScale", 1f);
            material.SetFloat("_Metallic", 1f);
            material.SetFloat("_Smoothness", 1f); // scales the mask's alpha
            material.EnableKeyword("_NORMALMAP");
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            EditorUtility.SetDirty(material);
            return material;
        }

        // One solid, deep sapphire: dark blue body with cloudy inclusions and bright veins, opaque and glossy. The
        // emission follows the veins at a low level, so the facets still shade and the stone stays rich in the dark.
        public static Material Sapphire(string folder, string name)
        {
            EnsureSapphire();
            Material material = RigBuildUtility.MakeGlowMaterial(folder, name, new Color(.05f, .12f, .6f), 1f);
            material.SetTexture("_BaseMap", Load($"{TextureFolder}/Sapphire_Albedo.png"));
            material.SetTexture("_BumpMap", Load($"{TextureFolder}/Sapphire_Normal.png"));
            material.SetTexture("_EmissionMap", Load($"{TextureFolder}/Sapphire_Glow.png"));
            material.SetColor("_BaseColor", new Color(.7f, .8f, 1f));
            material.SetColor("_EmissionColor", new Color(.2f, .4f, 1f) * 1.1f);
            material.SetFloat("_BumpScale", .8f);
            material.SetFloat("_Smoothness", .9f);
            material.EnableKeyword("_NORMALMAP");
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsureSapphire()
        {
            if (!File.Exists($"{TextureFolder}/Sapphire_Albedo.png") || !File.Exists($"{TextureFolder}/Sapphire_Normal.png")
                || !File.Exists($"{TextureFolder}/Sapphire_Glow.png")) GenerateSapphire();
        }

        private static void GenerateSapphire()
        {
            const int size = 256;
            var height = new float[size, size];
            var albedo = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var glow = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var bump = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color deep = new Color(.015f, .04f, .3f), mid = new Color(.04f, .14f, .72f), bright = new Color(.35f, .62f, 1f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size, v = y / (float)size;
                    // Warp the lookup so clouds and veins swirl instead of lining up with the lattice.
                    float uu = Mathf.Repeat(u + .18f * (Fbm(u, v, 3, 10) - .5f), 1f), vv = Mathf.Repeat(v + .18f * (Fbm(u, v, 3, 20) - .5f), 1f);
                    float cloud = Fbm(uu, vv, 4, 30);
                    float vein = Mathf.Pow(1f - Mathf.Abs(2f * Fbm(uu, vv, 3, 40) - 1f), 7f);
                    float depth = Mathf.Clamp01(cloud * 1.3f - .1f);
                    Color c = Color.Lerp(Color.Lerp(deep, mid, depth), bright, vein * .8f);
                    c.a = Mathf.Clamp(.5f + .3f * cloud + .25f * vein, .45f, .9f);
                    albedo.SetPixel(x, y, c);
                    Color g = c * (.35f + 1.2f * vein + .5f * cloud);
                    g.a = 1f;
                    glow.SetPixel(x, y, g);
                    height[x, y] = cloud * .15f + vein * .1f;
                }
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = height[(x + 1) % size, y] - height[(x + size - 1) % size, y];
                    float dy = height[x, (y + 1) % size] - height[x, (y + size - 1) % size];
                    Vector3 n = new Vector3(-dx * 5f, -dy * 5f, 1f).normalized;
                    bump.SetPixel(x, y, new Color(n.x * .5f + .5f, n.y * .5f + .5f, n.z * .5f + .5f, 1f));
                }
            SaveTextures("Sapphire", albedo, bump);
            SaveLinearTexture("Sapphire_Glow", glow, true);
        }

        private static float Fbm(float u, float v, int cells, int seed)
        {
            float sum = 0f, amplitude = .5f, total = 0f;
            for (int octave = 0; octave < 4; octave++)
            {
                int c = cells << octave;
                sum += amplitude * TileNoise(u, v, c, c, seed + octave);
                total += amplitude;
                amplitude *= .5f;
            }
            return sum / total;
        }

        private static Texture2D Load(string path) => AssetDatabase.LoadAssetAtPath<Texture2D>(path)
            ?? throw new System.InvalidOperationException(path + " could not be imported.");

        private static void GenerateAgedGold()
        {
            const int size = 512;
            var height = new float[size, size];
            var albedo = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var mask = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var bump = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color gold = new Color(.88f, .67f, .3f), patina = new Color(.42f, .29f, .12f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size, v = y / (float)size;
                    float brushed = TileNoise(u, v, 3, 160, 1);                                        // streaks along u
                    float mottle = TileNoise(u, v, 5, 5, 2) * .6f + TileNoise(u, v, 18, 18, 3) * .4f;   // slow tone changes
                    float wear = TileNoise(u, v, 40, 40, 4);
                    float pit = wear > .82f ? (wear - .82f) / .18f : 0f;
                    // Diamond engraving: two sets of diagonal grooves that tile exactly.
                    float d1 = Mathf.Abs(Mathf.Repeat((u + v) * 4f, 1f) - .5f), d2 = Mathf.Abs(Mathf.Repeat((u - v) * 4f, 1f) - .5f);
                    float groove = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Min(d1, d2) * 4f / .06f - 0.9f);
                    groove = Mathf.Clamp01(groove);
                    float hollow = Mathf.Clamp01((.45f - mottle) / .25f);

                    height[x, y] = (brushed - .5f) * .12f + (wear - .5f) * .06f - pit * .5f - groove * .45f;

                    Color c = Color.Lerp(gold, patina, hollow * .7f + groove * .45f + pit * .6f);
                    c *= .88f + .24f * mottle + .06f * (brushed - .5f);
                    c.a = 1f;
                    albedo.SetPixel(x, y, c);

                    float metallic = Mathf.Clamp01(.97f - .35f * pit - .25f * groove - .2f * hollow);
                    float smooth = Mathf.Clamp01(.78f - .28f * hollow - .2f * Mathf.Abs(brushed - .5f) * 2f - .3f * pit - .15f * groove);
                    mask.SetPixel(x, y, new Color(metallic, 0f, 0f, smooth));
                }
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = height[(x + 1) % size, y] - height[(x + size - 1) % size, y];
                    float dy = height[x, (y + 1) % size] - height[x, (y + size - 1) % size];
                    Vector3 n = new Vector3(-dx * 6f, -dy * 6f, 1f).normalized;
                    bump.SetPixel(x, y, new Color(n.x * .5f + .5f, n.y * .5f + .5f, n.z * .5f + .5f, 1f));
                }
            SaveTextures("AgedGold", albedo, bump);
            SaveLinearTexture("AgedGold_Mask", mask);
        }

        private static void SaveLinearTexture(string name, Texture2D texture, bool colour = false)
        {
            string path = $"{TextureFolder}/{name}.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = colour;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
        }

        // Value noise on a lattice of `px` by `py` cells that wraps, so the texture tiles without seams.
        private static float TileNoise(float u, float v, int px, int py, int seed)
        {
            float x = u * px, y = v * py;
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = Mathf.SmoothStep(0f, 1f, x - x0), fy = Mathf.SmoothStep(0f, 1f, y - y0);
            float a = Lattice(x0, y0, px, py, seed), b = Lattice(x0 + 1, y0, px, py, seed);
            float c = Lattice(x0, y0 + 1, px, py, seed), d = Lattice(x0 + 1, y0 + 1, px, py, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        private static float Lattice(int ix, int iy, int px, int py, int seed)
        {
            ix = (ix % px + px) % px;
            iy = (iy % py + py) % py;
            unchecked
            {
                uint h = (uint)(ix * 374761393 + iy * 668265263 + seed * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFF) / 65535f;
            }
        }
    }
}

using System.IO;
using UnityEditor;
using UnityEngine;

namespace Lightbringer.EditorTools
{
    // Surfaces for the allied weapons: brushed steel, blue enamel plate and pebbled leather. Each has an albedo, a normal
    // map and, for the metals, a mask (metallic in R, smoothness in A) so scratches and wear show as duller spots.
    internal static partial class WeaponMeshes
    {
        // Satin steel: fine brushed streaks, scattered scratches and dark patina in the hollows. Smoothness stays
        // moderate so it reads as metal without mirroring the sky.
        public static Material BrushedSteel(string folder, string name)
        {
            string albedo = $"{TextureFolder}/BrushedSteel_Albedo.png", normal = $"{TextureFolder}/BrushedSteel_Normal.png", mask = $"{TextureFolder}/BrushedSteel_Mask.png";
            GenerateBrushedSteel();
            return MaskedMaterial(folder, name, albedo, normal, mask, 1f, 1f, 1f);
        }

        // Painted steel plate: royal blue enamel over hammered metal, with scratches that expose bright steel and
        // slightly duller, mottled paint.
        public static Material BlueEnamel(string folder, string name)
        {
            string albedo = $"{TextureFolder}/BlueEnamel_Albedo.png", normal = $"{TextureFolder}/BlueEnamel_Normal.png", mask = $"{TextureFolder}/BlueEnamel_Mask.png";
            GenerateBlueEnamel();
            return MaskedMaterial(folder, name, albedo, normal, mask, 1f, 1f, 1f);
        }

        // Dark pebbled leather with creases.
        public static Material PebbledLeather(string folder, string name, Color tint)
        {
            string albedo = $"{TextureFolder}/Leather_Albedo.png", normal = $"{TextureFolder}/Leather_Normal.png";
            if (!File.Exists(albedo) || !File.Exists(normal)) GenerateLeather();
            Material material = RigBuildUtility.MakeMaterial(folder, name, tint, 0f, .32f);
            material.SetTexture("_BaseMap", Load(albedo));
            material.SetTexture("_BumpMap", Load(normal));
            material.SetFloat("_BumpScale", 1.2f);
            material.EnableKeyword("_NORMALMAP");
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material MaskedMaterial(string folder, string name, string albedo, string normal, string mask, float bump, float metallic, float smoothness)
        {
            Material material = RigBuildUtility.MakeMaterial(folder, name, Color.white, metallic, smoothness);
            material.SetTexture("_BaseMap", Load(albedo));
            material.SetTexture("_BumpMap", Load(normal));
            material.SetTexture("_MetallicGlossMap", Load(mask));
            material.SetFloat("_BumpScale", bump);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            material.EnableKeyword("_NORMALMAP");
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Texture2D[] NewMaps(int size) => new[]
        {
            new Texture2D(size, size, TextureFormat.RGBA32, false), new Texture2D(size, size, TextureFormat.RGBA32, false),
            new Texture2D(size, size, TextureFormat.RGBA32, false),
        };

        private static void FillNormal(Texture2D bump, float[,] height, float strength)
        {
            int size = height.GetLength(0);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = height[(x + 1) % size, y] - height[(x + size - 1) % size, y];
                    float dy = height[x, (y + 1) % size] - height[x, (y + size - 1) % size];
                    Vector3 n = new Vector3(-dx * strength, -dy * strength, 1f).normalized;
                    bump.SetPixel(x, y, new Color(n.x * .5f + .5f, n.y * .5f + .5f, n.z * .5f + .5f, 1f));
                }
        }

        // Thin scratch lines at a handful of fixed angles; returns 0..1 coverage. Tiles because the lattice wraps.
        private static float Scratches(float u, float v, int seed)
        {
            // Integer wave vectors keep the lines continuous across the texture edge.
            int[,] waves = { { 7, 3 }, { 4, -6 }, { 2, 9 } };
            float best = 0f;
            for (int k = 0; k < 3; k++)
            {
                float along = u * waves[k, 0] + v * waves[k, 1];
                float across = Mathf.Round(along);
                if (TileNoise(u, v, 4, 4, seed * 7 + k) < .74f) continue; // scratches come in patches, not everywhere
                float d = Mathf.Abs(along - across);
                best = Mathf.Max(best, 1f - Mathf.SmoothStep(0f, 1f, d / .035f));
            }
            return best;
        }

        private static void GenerateBrushedSteel()
        {
            const int size = 512;
            var height = new float[size, size];
            Texture2D[] maps = NewMaps(size);
            Texture2D albedo = maps[0], bump = maps[1], mask = maps[2];
            Color steel = new Color(.74f, .76f, .8f), dark = new Color(.3f, .31f, .34f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size, v = y / (float)size;
                    float brushed = TileNoise(u, v, 2, 220, 5) * .6f + TileNoise(u, v, 4, 90, 6) * .4f;
                    float mottle = TileNoise(u, v, 4, 4, 7) * .6f + TileNoise(u, v, 14, 14, 8) * .4f;
                    float scratch = Scratches(u, v, 3);
                    float pit = TileNoise(u, v, 48, 48, 9);
                    pit = pit > .86f ? (pit - .86f) / .14f : 0f;
                    height[x, y] = (brushed - .5f) * .1f - scratch * .35f - pit * .25f;
                    float hollow = Mathf.Clamp01((.42f - mottle) / .25f);
                    Color c = Color.Lerp(steel, dark, hollow * .16f + pit * .45f);
                    c *= .9f + .2f * brushed;
                    c = Color.Lerp(c, new Color(.92f, .93f, .96f), scratch * .5f);
                    c.a = 1f;
                    albedo.SetPixel(x, y, c);
                    float metallic = Mathf.Clamp01(.95f - .3f * pit);
                    float smooth = Mathf.Clamp01(.62f - .08f * hollow - .18f * Mathf.Abs(brushed - .5f) * 2f - .3f * pit + .1f * scratch);
                    mask.SetPixel(x, y, new Color(metallic, 0f, 0f, smooth));
                }
            FillNormal(bump, height, 5f);
            SaveTextures("BrushedSteel", albedo, bump);
            SaveLinearTexture("BrushedSteel_Mask", mask);
        }

        private static void GenerateBlueEnamel()
        {
            const int size = 512;
            var height = new float[size, size];
            Texture2D[] maps = NewMaps(size);
            Texture2D albedo = maps[0], bump = maps[1], mask = maps[2];
            Color deep = new Color(.025f, .06f, .2f), paint = new Color(.06f, .15f, .46f), light = new Color(.12f, .26f, .64f), bare = new Color(.62f, .64f, .68f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size, v = y / (float)size;
                    // Hammered plate: broad soft dents plus a fine orange-peel of the paint.
                    float dent = TileNoise(u, v, 7, 7, 11) * .6f + TileNoise(u, v, 19, 19, 12) * .4f;
                    float peel = TileNoise(u, v, 90, 90, 13);
                    float mottle = TileNoise(u, v, 3, 3, 14) * .6f + TileNoise(u, v, 11, 11, 15) * .4f;
                    float scratch = Scratches(u, v, 5);
                    float chip = TileNoise(u, v, 26, 26, 16);
                    chip = chip > .93f ? Mathf.Clamp01((chip - .93f) / .04f) : 0f;
                    float wear = Mathf.Max(scratch * .45f, chip * .8f);
                    height[x, y] = (dent - .5f) * .35f + (peel - .5f) * .05f - wear * .25f;
                    Color c = Color.Lerp(Color.Lerp(deep, paint, dent), light, Mathf.Clamp01((mottle - .55f) * 2f) * .55f);
                    c *= .94f + .12f * peel;
                    c = Color.Lerp(c, bare, wear);
                    c.a = 1f;
                    albedo.SetPixel(x, y, c);
                    // Paint is a dielectric; bare steel shows through scratches and chips.
                    float metallic = Mathf.Clamp01(.12f + wear * .85f);
                    float smooth = Mathf.Clamp01(.62f - .22f * mottle - .2f * (peel - .5f) * 2f + wear * .05f);
                    mask.SetPixel(x, y, new Color(metallic, 0f, 0f, smooth));
                }
            FillNormal(bump, height, 4f);
            SaveTextures("BlueEnamel", albedo, bump);
            SaveLinearTexture("BlueEnamel_Mask", mask);
        }

        private static void GenerateLeather()
        {
            const int size = 256;
            var height = new float[size, size];
            Texture2D[] maps = NewMaps(size);
            Texture2D albedo = maps[0], bump = maps[1];
            Object.DestroyImmediate(maps[2]);
            Color dark = new Color(.55f, .5f, .48f), light = new Color(1f, .95f, .9f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size, v = y / (float)size;
                    float pebble = TileNoise(u, v, 48, 48, 21) * .6f + TileNoise(u, v, 96, 96, 22) * .4f;
                    float crease = TileNoise(u, v, 6, 20, 23);
                    crease = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Abs(crease - .5f) / .04f);
                    float wear = TileNoise(u, v, 5, 5, 24);
                    height[x, y] = pebble * .4f - crease * .5f;
                    Color c = Color.Lerp(dark, light, .45f + .4f * pebble + .25f * (wear - .5f));
                    albedo.SetPixel(x, y, Color.Lerp(c, dark * .6f, crease * .7f));
                }
            FillNormal(bump, height, 3f);
            SaveTextures("Leather", albedo, bump);
        }
    }
}

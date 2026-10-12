using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Lightbringer.EditorTools
{
    // Shared PBR texture import for characters, strongholds and battlefield props.
    internal static class ModelSurfaceImport
    {
        internal static void ConfigureLit(Material material, Texture2D color, Texture2D normal,
            Texture2D metallicSmoothness, float bumpScale = 1f, float smoothness = 1f)
        {
            material.SetTexture("_BaseMap", color);
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BumpMap", normal);
            material.SetFloat("_BumpScale", bumpScale);
            material.EnableKeyword("_NORMALMAP");
            material.SetTexture("_MetallicGlossMap", metallicSmoothness);
            material.SetFloat("_Metallic", 1f);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_SmoothnessTextureChannel", 0f);
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            EditorUtility.SetDirty(material);
        }

        internal static Texture2D ImportTexture(string path, bool normal)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) throw new FileNotFoundException(path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normal;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // URP Lit: metallic in R, smoothness (1 - roughness) in A.
        internal static void PackMetallicSmoothness(string metallicPath, string roughnessPath, string packedPath)
        {
            var metallic = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            var roughness = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            Texture2D packed = null;
            try
            {
                if (!metallic.LoadImage(File.ReadAllBytes(metallicPath)) || !roughness.LoadImage(File.ReadAllBytes(roughnessPath)))
                    throw new InvalidOperationException("Could not decode surface textures for " + packedPath);
                if (metallic.width != roughness.width || metallic.height != roughness.height)
                    throw new InvalidOperationException("Metallic/roughness texture dimensions differ for " + packedPath);
                Color32[] pixels = metallic.GetPixels32();
                Color32[] rough = roughness.GetPixels32();
                for (int i = 0; i < pixels.Length; i++)
                    pixels[i] = new Color32(pixels[i].r, 0, 0, (byte)(255 - rough[i].r));
                packed = new Texture2D(metallic.width, metallic.height, TextureFormat.RGBA32, false, true);
                packed.SetPixels32(pixels);
                packed.Apply();
                File.WriteAllBytes(packedPath, packed.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(metallic);
                UnityEngine.Object.DestroyImmediate(roughness);
                if (packed != null) UnityEngine.Object.DestroyImmediate(packed);
            }
            AssetDatabase.ImportAsset(packedPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(packedPath);
            importer.sRGBTexture = false;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
            importer.SaveAndReimport();
        }

    }
}

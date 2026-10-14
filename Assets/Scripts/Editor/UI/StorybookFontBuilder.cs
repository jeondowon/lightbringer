using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;

namespace Lightbringer.EditorTools
{
    // Builds the UI Toolkit font assets for the storybook UI (Docs/Art/ArtDirection.md):
    // Gowun Batang for text, with its Bold face wired into the weight table so -unity-font-style: bold uses the real
    // bold instead of a faux one; Song Myung for display titles. Both fall back to Nanum Gothic for symbols.
    [InitializeOnLoad]
    public static class StorybookFontBuilder
    {
        private const string Folder = "Assets/UI/Fonts/";
        public const string TextFontPath = Folder + "GowunBatang.asset";
        public const string DisplayFontPath = Folder + "SongMyung.asset";
        private const string Request = "Docs/Validation/BuildStorybookFonts.request";

        static StorybookFontBuilder()
        {
            EditorRequests.Register(Request, Build);
        }

        [MenuItem("Lightbringer/UI/Build Storybook Fonts")]
        public static void Build()
        {
            FontAsset fallback = Create("NanumGothic-Regular.ttf", Folder + "NanumGothic.asset", null);
            FontAsset bold = Create("GowunBatang-Bold.ttf", Folder + "GowunBatang-Bold.asset", fallback);
            FontAsset text = Create("GowunBatang-Regular.ttf", TextFontPath, fallback);
            FontAsset display = Create("SongMyung-Regular.ttf", DisplayFontPath, fallback);

            // FontWeight.Bold is index 7 of the weight table.
            text.fontWeightTable[7].regularTypeface = bold;
            EditorUtility.SetDirty(text);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(Lightbringer.UI.CampaignHUD.StyleSheetPath, ImportAssetOptions.ForceUpdate);
            Debug.Log("Storybook fonts built: " + text.name + ", " + bold.name + ", " + display.name + ", " + fallback.name);
        }

        // Dynamic SDF font asset with its atlas and material stored inside the asset; rebuilt in place so the
        // GUID referenced by the style sheet never changes.
        private static FontAsset Create(string sourceFile, string path, FontAsset fallback)
        {
            Font source = AssetDatabase.LoadAssetAtPath<Font>(Folder + sourceFile);
            if (source == null) throw new System.IO.FileNotFoundException("Missing font " + Folder + sourceFile);

            FontAsset fresh = FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
            fresh.name = System.IO.Path.GetFileNameWithoutExtension(path);
            FontAsset asset = AssetDatabase.LoadAssetAtPath<FontAsset>(path);
            if (asset == null)
            {
                AssetDatabase.CreateAsset(fresh, path);
                asset = fresh;
            }
            else
            {
                foreach (Object sub in AssetDatabase.LoadAllAssetRepresentationsAtPath(path)) Object.DestroyImmediate(sub, true);
                EditorUtility.CopySerialized(fresh, asset);
                asset.name = fresh.name;
            }
            Texture2D atlas = asset.atlasTextures[0];
            atlas.name = asset.name + " Atlas";
            AssetDatabase.AddObjectToAsset(atlas, asset);
            asset.material.name = asset.name + " Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            asset.fallbackFontAssetTable = fallback != null ? new System.Collections.Generic.List<FontAsset> { fallback } : new System.Collections.Generic.List<FontAsset>();
            EditorUtility.SetDirty(asset);
            return asset;
        }
    }
}

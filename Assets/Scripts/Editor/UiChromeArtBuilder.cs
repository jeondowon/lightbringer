using System.IO;
using UnityEditor;
using UnityEngine;

namespace Lightbringer.EditorTools
{
    // Writes the UI chrome and the minimap parchment (UiChromePainter) to Assets/UI/Art/Chrome.
    [InitializeOnLoad]
    public static class UiChromeArtBuilder
    {
        public const string Folder = "Assets/UI/Art/Chrome";
        private const string Request = "Docs/Validation/BuildUiChromeArt.request";

        static UiChromeArtBuilder()
        {
            EditorRequests.Register(Request, Build);
            // Picks up changed scripts and assets while the Editor window is in the background.
            EditorRequests.Register("Docs/Validation/RefreshAssets.request", () => AssetDatabase.Refresh());
        }

        [MenuItem("Lightbringer/UI/Build UI Chrome Art")]
        public static void Build()
        {
            Directory.CreateDirectory(Full(Folder));
            var written = new System.Collections.Generic.List<string>();
            UiChromePainter.PaintAll((name, image) =>
            {
                string path = Folder + "/" + name + ".png";
                File.WriteAllBytes(Full(path), Encode(image));
                written.Add(path);
            });
            AssetDatabase.Refresh();
            // Unchanged bytes skip the import; force it so the import settings below always apply.
            foreach (string path in written) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(Lightbringer.UI.CampaignHUD.StyleSheetPath, ImportAssetOptions.ForceUpdate);
            Debug.Log("UI chrome art built: " + written.Count + " textures in " + Folder);
        }

        private static byte[] Encode(UiImage image)
        {
            int w = image.Width, h = image.Height;
            Color[] pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    Rgba c = image.Get(x, y);
                    pixels[(h - 1 - y) * w + x] = new Color(c.R, c.G, c.B, c.A);
                }
            Texture2D texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            texture.Apply();
            byte[] png = texture.EncodeToPNG();
            Object.DestroyImmediate(texture);
            return png;
        }

        private static string Full(string projectPath) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", projectPath));
    }

    // Chrome textures are drawn 1:1 in UI Toolkit: no mipmaps, clamped and uncompressed (block compression bands
    // the soft gradients and muddies the paper grain).
    internal sealed class UiChromeTextureImport : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(UiChromeArtBuilder.Folder + "/")) return;
            TextureImporter importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
        }
    }
}

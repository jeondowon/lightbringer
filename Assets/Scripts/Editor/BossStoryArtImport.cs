using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Lightbringer.EditorTools
{
    // Story paintings are screen artwork: never resize each axis to a power of two.
    [InitializeOnLoad]
    public sealed class BossStoryArtImport : AssetPostprocessor
    {
        private const string Folder = "Assets/UI/Resources/BossStory";
        private const string ReportPath = "Docs/Validation/BossStoryArtChecks.txt";

        static BossStoryArtImport()
        {
            EditorRequests.Register("Docs/Validation/ImportBossStoryArt.request", RepairAndValidate);
        }

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder + "/", StringComparison.Ordinal)) return;
            Configure((TextureImporter)assetImporter);
        }

        private static void Configure(TextureImporter importer)
        {
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            // Avoid block-compression rounding of paintings whose height is not divisible by four.
            importer.textureCompression = TextureImporterCompression.Uncompressed;
        }

        [MenuItem("Lightbringer/Boss Story/Import and Validate Artwork")]
        public static void RepairAndValidate()
        {
            var report = new StringBuilder();
            Directory.CreateDirectory("Docs/Validation");
            try
            {
                string[] paths = Directory.GetFiles(Folder, "*.png");
                if (paths.Length == 0) throw new InvalidOperationException("No boss story artwork found.");
                foreach (string file in paths)
                {
                    string path = file.Replace('\\', '/');
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    Configure(importer);
                    importer.SaveAndReimport();
                    importer.GetSourceTextureWidthAndHeight(out int width, out int height);
                    Texture2D imported = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (imported == null || imported.width != width || imported.height != height)
                        throw new InvalidOperationException("Imported dimensions differ from source: " + path);
                    report.AppendLine($"PASS | {Path.GetFileName(path)} | source/imported {width}x{height}");
                }
                File.WriteAllText(ReportPath, "PASS | " + DateTime.UtcNow.ToString("O") + "\n" + report);
                Debug.Log("Boss story artwork: all imported dimensions match their source images.");
            }
            catch (Exception error)
            {
                File.WriteAllText(ReportPath, "FAIL | " + DateTime.UtcNow.ToString("O") + "\n" + report + error);
                throw;
            }
        }
    }
}

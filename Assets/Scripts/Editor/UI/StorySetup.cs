using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Lightbringer.Core;
using UnityEditor;
using UnityEngine;

namespace Lightbringer.EditorTools
{
    // Copies the story illustrations (Docs/ArtReference/Story/<Act>/) that StoryCatalog uses into
    // Assets/UI/Resources/Story and checks every scene still resolves. Planned pages may be missing; they borrow a fallback.
    [InitializeOnLoad]
    public static class StorySetup
    {
        public const string SourceFolder = "Docs/ArtReference/Story";
        public const string TargetFolder = "Assets/UI/Resources/Story";
        public const string RequestPath = "Docs/Validation/BuildStory.request";
        public const string ReportPath = "Docs/Validation/StoryChecks.txt";

        static StorySetup()
        {
            EditorRequests.Register(RequestPath, BuildAndValidate);
        }

        [MenuItem("Lightbringer/Story/Build and Validate")]
        public static void BuildAndValidate()
        {
            var report = new StringBuilder();
            try
            {
                Build(report);
                Validate(report);
                Write("PASS", report);
                Debug.Log("Story build and validation passed.\n" + report);
            }
            catch (Exception error)
            {
                report.AppendLine(error.ToString());
                Write("FAIL", report);
                throw;
            }
        }

        private static void Write(string status, StringBuilder report)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, status + " | " + DateTime.UtcNow.ToString("O") + "\n" + report);
        }

        // Source PNG for a Resources path like "Story/ActOne_01_..."; searched across the act folders.
        private static string Source(string resource)
        {
            string file = Path.GetFileName(resource) + ".png";
            return Directory.Exists(SourceFolder)
                ? Directory.GetFiles(SourceFolder, file, SearchOption.AllDirectories).Select(p => p.Replace('\\', '/')).FirstOrDefault()
                : null;
        }

        private static IEnumerable<string> StoryArtwork() =>
            StoryCatalog.Artwork().Where(path => path.StartsWith(StoryCatalog.Folder, StringComparison.Ordinal));

        public static void Build(StringBuilder report)
        {
            Directory.CreateDirectory(TargetFolder);
            var copied = new List<string>();
            foreach (string resource in StoryArtwork())
            {
                string source = Source(resource);
                if (source == null) { report.AppendLine("planned (no artwork yet): " + resource); continue; }
                string target = TargetFolder + "/" + Path.GetFileName(source);
                File.Copy(source, target, true);
                copied.Add(target);
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (string path in copied)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Default;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.mipmapEnabled = false;
                importer.isReadable = false;
                importer.sRGBTexture = true;
                importer.alphaSource = TextureImporterAlphaSource.None;
                importer.maxTextureSize = 2048;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
                report.AppendLine("imported: " + path);
            }
        }

        public static void Validate(StringBuilder report)
        {
            foreach (string id in StoryCatalog.Scenes)
            {
                int pages = 0;
                foreach (StoryBeat beat in StoryCatalog.Scene(id))
                {
                    bool own = UnityEngine.Resources.Load<Texture2D>(beat.Image) != null;
                    bool borrowed = !own && beat.Fallback != null && UnityEngine.Resources.Load<Texture2D>(beat.Fallback) != null;
                    if (!own && !borrowed && beat.Fallback == null && !beat.IsSilent)
                        throw new InvalidOperationException($"{id}: page with a line has no artwork: {beat.Image}");
                    if (!own && beat.Fallback != null && !borrowed)
                        throw new InvalidOperationException($"{id}: fallback artwork missing: {beat.Fallback}");
                    if (beat.Line.Length > 0 && string.IsNullOrWhiteSpace(beat.Speaker))
                        throw new InvalidOperationException($"{id}: spoken line without a speaker: {beat.Line}");
                    if (own || borrowed) pages++;
                }
                if (pages == 0) throw new InvalidOperationException(id + " has no playable page.");
                report.AppendLine($"{id}: {pages} pages playable");
            }
            foreach (string picture in new[] { StoryCatalog.SwordCloseUp, StoryCatalog.HeroRecognition, StoryCatalog.WarlordDissolution })
                if (UnityEngine.Resources.Load<Texture2D>(picture) == null)
                    throw new InvalidOperationException("In-battle story panel missing: " + picture);
            // Act 2 keeps the Warlord unnamed; only his fall in Act 3 lets the teacher's voice speak.
            foreach (string said in BossEncounter.RematchLines.Concat(BossEncounter.RematchRetreatLines))
                if (said.Contains("용사")) throw new InvalidOperationException("Act 2 line reveals the Warlord: " + said);
            report.AppendLine("in-battle panels resolve; Act 2 lines keep the Warlord unnamed");
        }
    }
}

using UnityEditor;
using UnityEngine;

namespace Lightbringer.EditorTools
{
    // Keep long music clips streamed rather than decompressing the whole track into memory.
    public sealed class BattleMusicImport : AssetPostprocessor
    {
        public const string BattlePath = "Assets/Audio/Resources/Music/TheTreadOfWar.mp3";
        public const string ProloguePath = "Assets/Audio/Music/Prologue/Legionnaire2022.mp3";

        private void OnPreprocessAudio()
        {
            if (assetPath != BattlePath && assetPath != ProloguePath) return;
            AudioImporter importer = (AudioImporter)assetImporter;
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.Streaming;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.85f;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = false;
            importer.loadInBackground = true;
        }

        [InitializeOnLoadMethod]
        private static void Register()
        {
            EditorRequests.Register("Docs/Validation/ImportBattleMusic.request", () =>
            {
                AssetDatabase.ImportAsset(BattlePath, ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset(ProloguePath, ImportAssetOptions.ForceUpdate);
                AudioClip battle = AssetDatabase.LoadAssetAtPath<AudioClip>(BattlePath);
                AudioClip prologue = AssetDatabase.LoadAssetAtPath<AudioClip>(ProloguePath);
                if (battle == null || prologue == null) throw new System.InvalidOperationException("Music import failed.");
                System.IO.File.WriteAllText("Docs/Validation/MusicImportChecks.txt",
                    $"PASS | {System.DateTime.UtcNow:O}\nBattle: {battle.length:F3}s, {battle.channels}ch, {battle.frequency}Hz, {battle.loadType}\n" +
                    $"Prologue: {prologue.length:F3}s, {prologue.channels}ch, {prologue.frequency}Hz, {prologue.loadType}\n");
            });
        }
    }
}

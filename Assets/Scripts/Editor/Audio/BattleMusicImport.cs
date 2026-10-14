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
    }
}

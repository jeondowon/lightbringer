using UnityEditor;
using UnityEngine;

namespace Lightbringer.EditorTools
{
    // Short one-shots are mono PCM: cheap to decode even in crowded battles, and spatialize correctly.
    public sealed class SoundEffectsImport : AssetPostprocessor
    {
        private void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith("Assets/Audio/Resources/SoundEffects/", System.StringComparison.Ordinal)
                && !assetPath.StartsWith("Assets/Audio/Sources/Foley/", System.StringComparison.Ordinal)) return;
            AudioImporter importer = (AudioImporter)assetImporter;
            importer.forceToMono = true;
            importer.loadInBackground = false;
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.preloadAudioData = true;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings = settings;
        }
    }
}

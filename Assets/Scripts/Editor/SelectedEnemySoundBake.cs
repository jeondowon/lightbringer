using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Lightbringer.EditorTools
{
    public static partial class FoleySoundBake
    {
        [InitializeOnLoadMethod]
        private static void RegisterSelectedAdditionalSounds() => EditorRequests.Register(
            "Docs/Validation/ImportSelectedAdditionalSounds.request", RebuildNormalizedSounds);

        [MenuItem("Lightbringer/Audio/Import Selected Enemy and Hero Healing Sounds")]
        public static void BakeSelectedAdditionalSounds()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before importing audio.");
            cache.Clear();
            Directory.CreateDirectory(Output);
            string[] files = { "ShamanWaterUser.mp3", "EnemyDeathUser.mp3", "BossSummonUser.mp3", "HeroHealingUser.mp3" };
            string[] names = { "ShamanWater", "EnemyDeath", "BossSummon", "HeroHeal" };
            var report = new StringBuilder("PASS | " + DateTime.UtcNow.ToString("O") + "\n");
            for (int i = 0; i < files.Length; i++)
            {
                AssetDatabase.ImportAsset(Sources + files[i], ImportAssetOptions.ForceUpdate);
                float seconds = Read(files[i]).Length / (float)Rate;
                // The Shaman repeatedly casts; keep the water attack, not the eight-second tail.
                if (names[i] == "ShamanWater") seconds = Mathf.Min(seconds, 1.4f);
                Write(names[i], seconds, new Layer(files[i], lowPass: 22000));
                AssetDatabase.ImportAsset(Output + names[i] + ".wav", ImportAssetOptions.ForceUpdate);
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Output + names[i] + ".wav");
                if (clip == null || clip.channels != 1 || Mathf.Abs(clip.length - seconds) > .002f)
                    throw new InvalidOperationException("Invalid selected enemy sound: " + names[i]);
                report.AppendLine(FormattableString.Invariant(
                    $"{names[i]}: {files[i]} -> {clip.length:F3}s, mono {clip.frequency}Hz, max100ms RMS -20 dBFS, original pitch/speed."));
            }
            cache.Clear();
            File.WriteAllText("Docs/Validation/SelectedAdditionalAudioChecks.txt", report.ToString());
        }
    }
}

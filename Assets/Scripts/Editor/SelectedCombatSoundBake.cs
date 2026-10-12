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
        private static void RegisterSelectedCombatSounds() => EditorRequests.Register("Docs/Validation/ImportSelectedCombatSounds.request", BakeSelectedCombatSounds);

        [InitializeOnLoadMethod]
        private static void RegisterTroopVoices() => EditorRequests.Register("Docs/Validation/ImportTroopVoices.request", BakeSelectedCombatSounds);

        [InitializeOnLoadMethod]
        private static void RegisterDeathAndHeroSounds() => EditorRequests.Register("Docs/Validation/ImportDeathAndHeroSounds.request", BakeSelectedCombatSounds);

        [MenuItem("Lightbringer/Audio/Import Selected Combat Sounds")]
        public static void BakeSelectedCombatSounds()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before importing audio.");
            cache.Clear();
            Directory.CreateDirectory(Output);
            foreach (string file in new[] { "ArrowImpactUser.mp3", "ArrowReleaseUser.mp3", "BossImpactUser.mp3", "HeroPunchImpactUser.mp3",
                "TroopGruntFemaleUser.mp3", "TroopGruntMaleUser.mp3" })
                AssetDatabase.ImportAsset(Sources + file, ImportAssetOptions.ForceUpdate);

            // Envelope inspection: first hit peaks at 0.29s; second starts at 0.78s after leading-silence trim.
            // Include only the second transient and its decay. Never include the first hit or the gap.
            float[] original = Read("ArrowImpactUser.mp3");
            int start = Mathf.RoundToInt(.775f * Rate), count = Mathf.RoundToInt(.335f * Rate);
            if (original.Length < start + count) throw new InvalidOperationException("Arrow source is shorter than the selected second hit.");
            float[] second = new float[count];
            Array.Copy(original, start, second, 0, count);
            cache["ArrowImpactUser.mp3"] = second;
            Write("ArrowHit", .335f, new Layer("ArrowImpactUser.mp3", lowPass: 22000));
            Write("ArrowRelease", Read("ArrowReleaseUser.mp3").Length / (float)Rate, new Layer("ArrowReleaseUser.mp3", lowPass: 22000));
            Write("HeroHurt", Read("HeroPunchImpactUser.mp3").Length / (float)Rate, new Layer("HeroPunchImpactUser.mp3", lowPass: 22000));
            Write("BossImpact", Read("BossImpactUser.mp3").Length / (float)Rate, new Layer("BossImpactUser.mp3", lowPass: 22000));
            Write("TroopDeathFemale", Read("TroopGruntFemaleUser.mp3").Length / (float)Rate, new Layer("TroopGruntFemaleUser.mp3", lowPass: 22000));
            Write("TroopDeathMale", Read("TroopGruntMaleUser.mp3").Length / (float)Rate, new Layer("TroopGruntMaleUser.mp3", lowPass: 22000));

            var report = new StringBuilder("PASS | " + DateTime.UtcNow.ToString("O") + "\n");
            report.AppendLine("ArrowHit: selected second hit, 0.775-1.110s relative to the leading-silence-trimmed source.");
            report.AppendLine("HeroHurt: user-selected Classic Punch Impact 352711, original pitch/speed.");
            foreach (string name in new[] { "ArrowHit", "ArrowRelease", "HeroHurt", "BossImpact", "TroopDeathFemale", "TroopDeathMale" })
            {
                AssetDatabase.ImportAsset(Output + name + ".wav", ImportAssetOptions.ForceUpdate);
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Output + name + ".wav");
                if (clip == null || clip.channels != 1 || clip.samples == 0) throw new InvalidOperationException("Invalid audio: " + name);
                float[] data = new float[clip.samples];
                if (!clip.GetData(data, 0)) throw new InvalidOperationException("Cannot decode " + name);
                float peak = 0;
                foreach (float sample in data)
                {
                    if (float.IsNaN(sample) || Mathf.Abs(sample) >= .99f) throw new InvalidOperationException("Clipped audio: " + name);
                    peak = Mathf.Max(peak, Mathf.Abs(sample));
                }
                if (peak < .1f) throw new InvalidOperationException("Silent audio: " + name);
                report.AppendLine($"{name}: {clip.length:F3}s, mono {clip.frequency}Hz, peak {peak:F3}");
            }
            cache.Clear();
            File.WriteAllText("Docs/Validation/SelectedCombatAudioChecks.txt", report.ToString());
        }

        [InitializeOnLoadMethod]
        private static void RegisterCombatAudioInspection() => EditorRequests.Register("Docs/Validation/InspectCombatAudio.request", InspectCombatAudio);

        private static void InspectCombatAudio()
        {
            cache.Clear();
            AssetDatabase.ImportAsset(Sources + "ArrowImpactUser.mp3", ImportAssetOptions.ForceUpdate);
            float[] samples = Read("ArrowImpactUser.mp3");
            var report = new StringBuilder("Seconds after leading-silence trim, RMS, peak\n");
            for (int start = 0; start < samples.Length; start += 441)
            {
                float sum = 0, peak = 0;
                int end = Mathf.Min(start + 441, samples.Length);
                for (int i = start; i < end; i++) { sum += samples[i] * samples[i]; peak = Mathf.Max(peak, Mathf.Abs(samples[i])); }
                report.AppendLine(FormattableString.Invariant($"{start / (float)Rate:F3},{Mathf.Sqrt(sum / (end - start)):F4},{peak:F4}"));
            }
            File.WriteAllText("Docs/Validation/ArrowSourceEnvelope.csv", report.ToString());
            cache.Clear();
        }
    }
}

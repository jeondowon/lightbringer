using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Lightbringer.EditorTools
{
    public static partial class FoleySoundBake
    {
        // Unweighted, strongest sliding 100ms RMS, not integrated LUFS or whole-file RMS.
        // A common reference avoids boosting long tails simply because a clip has a long decay.
        private const float SoundRmsTarget = .1f; // -20 dBFS before the user's volume slider.
        private const float SoundPeakCeiling = .8912509f; // -1 dBFS sample peak.

        [InitializeOnLoadMethod]
        private static void RegisterSoundLevelBake() => EditorRequests.Register(
            "Docs/Validation/NormalizeSoundLevels.request", RebuildNormalizedSounds);

        [MenuItem("Lightbringer/Audio/Rebuild and Validate Normalized Sound Effects")]
        public static void RebuildNormalizedSounds()
        {
            Bake();
            var report = new StringBuilder("PASS | " + DateTime.UtcNow.ToString("O") + "\n");
            report.AppendLine("27 clips: max sliding 100ms RMS -20 dBFS +/- 0.1 dB; sample peak <= -1 dBFS.");
            string[] paths = Directory.GetFiles(Output, "*.wav");
            if (paths.Length != 27) throw new InvalidOperationException("Expected 27 SFX WAVs.");
            foreach (string path in paths)
            {
                string assetPath = path.Replace('\\', '/');
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
                if (clip == null || clip.channels != 1 || clip.frequency != Rate)
                    throw new InvalidOperationException("Invalid normalized clip: " + path);
                var samples = new float[clip.samples];
                if (!clip.GetData(samples, 0)) throw new InvalidOperationException("Cannot read: " + path);
                float peak = 0;
                foreach (float sample in samples)
                {
                    if (float.IsNaN(sample) || float.IsInfinity(sample)) throw new InvalidOperationException("Invalid sample: " + path);
                    peak = Mathf.Max(peak, Mathf.Abs(sample));
                }
                float rmsDb = 20 * Mathf.Log10(MaxWindowRms(samples));
                if (Mathf.Abs(rmsDb + 20) > .1f || peak > SoundPeakCeiling + .0001f)
                    throw new InvalidOperationException($"Level out of range: {path}, RMS {rmsDb}, peak {peak}");
                report.AppendLine(FormattableString.Invariant(
                    $"{clip.name}: {clip.length:F3}s, max100ms RMS {rmsDb:F2} dBFS, peak {20 * Mathf.Log10(peak):F2} dBFS"));
            }
            File.WriteAllText("Docs/Validation/SoundLevelChecks.txt", report.ToString());
        }

        private static float MaxWindowRms(float[] samples)
        {
            int window = Math.Min(samples.Length, Rate / 10);
            if (window == 0) throw new InvalidOperationException("Empty sound.");
            double sum = 0, largest = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                sum += (double)samples[i] * samples[i];
                if (i >= window) sum -= (double)samples[i - window] * samples[i - window];
                if (i >= window - 1) largest = Math.Max(largest, sum);
            }
            return (float)Math.Sqrt(largest / window);
        }

        private static float[] NormalizeSoundLevel(float[] original)
        {
            float rms = MaxWindowRms(original);
            if (rms < .000001f) throw new InvalidOperationException("Cannot normalize a silent sound.");
            float gain = SoundRmsTarget / rms;
            var output = new float[original.Length];
            var envelope = new float[original.Length];
            for (int pass = 0; pass < 24; pass++)
            {
                // Each pass starts from the unprocessed waveform; no cumulative compression.
                for (int i = 0; i < original.Length; i++)
                {
                    output[i] = original[i] * gain;
                    envelope[i] = Mathf.Min(1, SoundPeakCeiling / Mathf.Max(.000001f, Mathf.Abs(output[i])));
                }
                // Offline look-ahead gain ramps retain timing and pitch, avoiding hard clipping.
                float attack = 1f - Mathf.Exp(-1f / (.001f * Rate));
                float release = 1f - Mathf.Exp(-1f / (.010f * Rate));
                for (int i = envelope.Length - 2; i >= 0; i--)
                    envelope[i] = Mathf.Min(envelope[i], envelope[i + 1] + (1 - envelope[i + 1]) * attack);
                for (int i = 1; i < envelope.Length; i++)
                    envelope[i] = Mathf.Min(envelope[i], envelope[i - 1] + (1 - envelope[i - 1]) * release);
                for (int i = 0; i < output.Length; i++) output[i] *= envelope[i];
                rms = MaxWindowRms(output);
                if (Mathf.Abs(20 * Mathf.Log10(rms / SoundRmsTarget)) < .02f) return output;
                gain *= SoundRmsTarget / rms;
            }
            throw new InvalidOperationException("Unable to reach the SFX RMS target within the peak ceiling.");
        }
    }
}

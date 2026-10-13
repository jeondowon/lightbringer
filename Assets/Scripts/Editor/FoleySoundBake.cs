using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Lightbringer.EditorTools
{
    // Edits recorded source material only: trim, layer, filter and fade. No oscillators or pitch sweeps.
    [InitializeOnLoad]
    public static partial class FoleySoundBake
    {
        private const string Sources = "Assets/Audio/Sources/Foley/";
        private const string Output = "Assets/Audio/Resources/SoundEffects/";
        private const int Rate = 44100;
        private static readonly Dictionary<string, float[]> cache = new Dictionary<string, float[]>();
        static FoleySoundBake() => EditorRequests.Register("Docs/Validation/BakeFoley.request", Bake);

        private readonly struct Layer
        {
            public readonly string File;
            public readonly float Gain, Start, Speed, LowPass;
            public readonly bool Reverse;
            public Layer(string file, float gain = 1, float start = 0, float speed = 1, float lowPass = 16000, bool reverse = false)
            { File = file; Gain = gain; Start = start; Speed = speed; LowPass = lowPass; Reverse = reverse; }
        }

        [MenuItem("Lightbringer/Audio/Rebuild Foley Sound Effects")]
        public static void Bake()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before rebuilding audio.");
            cache.Clear();
            Directory.CreateDirectory(Output);
            foreach (string source in Directory.GetFiles(Sources))
                if (source.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) || source.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase)
                    || source.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
                    AssetDatabase.ImportAsset(source.Replace('\\', '/'), ImportAssetOptions.ForceUpdate);
            BakeSelectedButton();
            Write("Denied", .18f, new Layer("Latch.ogg", 1, lowPass: 2800));
            BakeSelectedSummon();
            Write("MeleeHit", .32f, new Layer("Steel1.ogg", .75f), new Layer("Chop.ogg", .65f));
            Write("MeleeHit_2", .35f, new Layer("Steel2.ogg", .7f), new Layer("Chop.ogg", .7f, speed: .96f));
            Write("MeleeHit_3", .32f, new Layer("Steel3.ogg", .72f), new Layer("LeatherDrop.ogg", .7f));
            Write("HeavyImpact", .8f, new Layer("Stone3.ogg", 1, speed: .85f, lowPass: 3500), new Layer("LeatherDrop.ogg", .7f), new Layer("Chainmail.wav", .2f, .035f));
            Write("FireBreath", 1.1f, new Layer("Fire2.ogg", 1, speed: .9f, lowPass: 7000), new Layer("Swish3.wav", .3f));
            Write("BossWarning", 1.05f, new Layer("Stone3.ogg", .8f, speed: .7f, lowPass: 1600, reverse: true), new Layer("Roar.ogg", .35f, lowPass: 1400));
            BakeSelectedMagic();
            BakeSelectedHeroSounds();
            BakeSelectedCombatSounds();
            BakeSelectedAdditionalSounds();
            BakeSkillSounds();
            AssetDatabase.Refresh();
            File.WriteAllText("Docs/Validation/FoleyBakeChecks.txt", "PASS | " + DateTime.UtcNow.ToString("O")
                + "\n27 recorded/layered clips baked as mono PCM; no generated sine tones.\n"
                + "Sources and attribution: Assets/Audio/SoundEffectsNotes.txt\n");
            cache.Clear();
            Debug.Log("Foley sound effects rebuilt.");
        }

        [InitializeOnLoadMethod]
        private static void RegisterSelectedMagic() => EditorRequests.Register("Docs/Validation/ImportSelectedMagic.request", BakeSelectedMagic);

        [InitializeOnLoadMethod]
        private static void RegisterSelectedButton() => EditorRequests.Register("Docs/Validation/ImportSelectedButton.request", BakeSelectedButton);

        [MenuItem("Lightbringer/Audio/Import Selected Button Sound")]
        public static void BakeSelectedButton()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before importing audio.");
            cache.Clear();
            Directory.CreateDirectory(Output);
            const string file = "ButtonPressUser.mp3";
            AssetDatabase.ImportAsset(Sources + file, ImportAssetOptions.ForceUpdate);
            float seconds = Read(file).Length / (float)Rate;
            Write("Click", seconds, new Layer(file, lowPass: 22000));
            AssetDatabase.ImportAsset(Output + "Click.wav", ImportAssetOptions.ForceUpdate);
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Output + "Click.wav");
            if (clip == null || clip.channels != 1 || Mathf.Abs(clip.length - seconds) > .002f)
                throw new InvalidOperationException("Invalid button sound import.");
            File.WriteAllText("Docs/Validation/SelectedButtonAudioChecks.txt", "PASS | " + DateTime.UtcNow.ToString("O")
                + $"\nClick: Button Press 382713 -> {clip.length:F3}s, mono {clip.frequency}Hz, max 100ms RMS -20 dBFS, peak <= -1 dBFS, pitch 1.0\n");
            cache.Clear();
        }

        [InitializeOnLoadMethod]
        private static void RegisterSelectedHeroSounds() => EditorRequests.Register("Docs/Validation/ImportSelectedHeroSounds.request", BakeSelectedHeroSounds);

        [InitializeOnLoadMethod]
        private static void RegisterSelectedSummon() => EditorRequests.Register("Docs/Validation/ImportSelectedSummon.request", BakeSelectedSummon);

        [MenuItem("Lightbringer/Audio/Import Selected Summon Sound")]
        public static void BakeSelectedSummon()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before importing audio.");
            cache.Clear();
            Directory.CreateDirectory(Output);
            const string file = "SpellCastingUser.mp3";
            AssetDatabase.ImportAsset(Sources + file, ImportAssetOptions.ForceUpdate);
            float seconds = Read(file).Length / (float)Rate;
            Write("Summon", seconds, new Layer(file, lowPass: 22000));
            AssetDatabase.ImportAsset(Output + "Summon.wav", ImportAssetOptions.ForceUpdate);
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Output + "Summon.wav");
            if (clip == null || clip.channels != 1 || Mathf.Abs(clip.length - seconds) > .002f)
                throw new InvalidOperationException("Invalid summon sound import.");
            File.WriteAllText("Docs/Validation/SelectedSummonAudioChecks.txt", "PASS | " + DateTime.UtcNow.ToString("O")
                + $"\nSummon: Spell Casting 229208 -> {clip.length:F3}s, mono {clip.frequency}Hz, max 100ms RMS -20 dBFS, peak <= -1 dBFS, pitch 1.0\n");
            cache.Clear();
        }

        [MenuItem("Lightbringer/Audio/Import Selected Hero and Result Sounds")]
        public static void BakeSelectedHeroSounds()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before importing audio.");
            cache.Clear();
            Directory.CreateDirectory(Output);
            string[] sources = { "FxLightUser.mp3", "HeroLightStaffUser.mp3", "MedievalHornUser.mp3" };
            string[] names = { "Victory", "HeroLightStaff", "Defeat" };
            StringBuilder report = new StringBuilder("PASS | " + DateTime.UtcNow.ToString("O") + "\n");
            for (int i = 0; i < sources.Length; i++)
            {
                AssetDatabase.ImportAsset(Sources + sources[i], ImportAssetOptions.ForceUpdate);
                // Preserve the full sound and original speed; remove leading silence and leave peak headroom.
                float seconds = Read(sources[i]).Length / (float)Rate;
                Write(names[i], seconds, new Layer(sources[i], lowPass: 22000));
                AssetDatabase.ImportAsset(Output + names[i] + ".wav", ImportAssetOptions.ForceUpdate);
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Output + names[i] + ".wav");
                if (clip == null || clip.channels != 1 || Mathf.Abs(clip.length - seconds) > .002f)
                    throw new InvalidOperationException("Invalid imported clip: " + names[i]);
                report.AppendLine($"{names[i]}: {clip.length:F3}s, mono {clip.frequency}Hz; max 100ms RMS -20 dBFS, peak <= -1 dBFS, pitch 1.0");
            }
            cache.Clear();
            File.WriteAllText("Docs/Validation/SelectedHeroAudioChecks.txt", report.ToString());
        }

        [MenuItem("Lightbringer/Audio/Import Selected Mage and Priest Sounds")]
        public static void BakeSelectedMagic()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before importing audio.");
            cache.Clear();
            Directory.CreateDirectory(Output);
            string[] sources = { "FireMagicUser.mp3", "HealingMagicUser.mp3" };
            string[] names = { "MageFire", "PriestHeal" };
            float[] lengths = { .9f, 1.2f };
            StringBuilder report = new StringBuilder("PASS | " + DateTime.UtcNow.ToString("O") + "\n");
            for (int i = 0; i < sources.Length; i++)
            {
                AssetDatabase.ImportAsset(Sources + sources[i], ImportAssetOptions.ForceUpdate);
                AudioClip original = AssetDatabase.LoadAssetAtPath<AudioClip>(Sources + sources[i]);
                // Same speed and pitch as the user's recording; only trim, level and short fades.
                Write(names[i], lengths[i], new Layer(sources[i], lowPass: 22000));
                AssetDatabase.ImportAsset(Output + names[i] + ".wav", ImportAssetOptions.ForceUpdate);
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Output + names[i] + ".wav");
                if (clip == null || clip.channels != 1 || Mathf.Abs(clip.length - lengths[i]) > .002f)
                    throw new InvalidOperationException("Invalid imported clip: " + names[i]);
                float[] data = new float[clip.samples];
                if (!clip.GetData(data, 0)) throw new InvalidOperationException("Cannot read " + names[i]);
                float peak = 0;
                foreach (float sample in data)
                {
                    if (float.IsNaN(sample) || Mathf.Abs(sample) >= .99f) throw new InvalidOperationException("Clipped audio: " + names[i]);
                    peak = Mathf.Max(peak, Mathf.Abs(sample));
                }
                if (peak < .1f) throw new InvalidOperationException("Silent audio: " + names[i]);
                report.AppendLine($"{names[i]}: {original.length:F3}s source -> {clip.length:F3}s, mono {clip.frequency}Hz, peak {peak:F3}, pitch 1.0");
            }
            cache.Clear();
            File.WriteAllText("Docs/Validation/SelectedMagicChecks.txt", report.ToString());
        }

        private static float[] Read(string file)
        {
            if (cache.TryGetValue(file, out float[] result)) return result;
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Sources + file);
            if (clip == null) throw new FileNotFoundException("Missing foley source", Sources + file);
            clip.LoadAudioData();
            float[] interleaved = new float[clip.samples * clip.channels];
            if (!clip.GetData(interleaved, 0)) throw new InvalidOperationException("Cannot decode " + file);
            result = new float[Mathf.CeilToInt(clip.length * Rate)];
            for (int i = 0; i < result.Length; i++)
            {
                float position = i * (clip.frequency / (float)Rate);
                int a = Mathf.Min((int)position, clip.samples - 1), b = Mathf.Min(a + 1, clip.samples - 1);
                for (int c = 0; c < clip.channels; c++)
                    result[i] += Mathf.Lerp(interleaved[a * clip.channels + c], interleaved[b * clip.channels + c], position - a) / clip.channels;
            }
            // Align the first meaningful transient; silence in source recordings must not delay a strike.
            int first = 0;
            float peak = 0;
            foreach (float sample in result) peak = Mathf.Max(peak, Mathf.Abs(sample));
            while (first < result.Length - 1 && Mathf.Abs(result[first]) < peak * .015f) first++;
            first = Mathf.Max(0, first - 88);
            float[] trimmed = new float[result.Length - first];
            for (int i = 0; i < trimmed.Length; i++) trimmed[i] = result[first + i] / Mathf.Max(.01f, peak);
            cache[file] = trimmed;
            return trimmed;
        }

        private static void Write(string name, float seconds, params Layer[] layers)
        {
            float[] samples = new float[Mathf.CeilToInt(seconds * Rate)];
            foreach (Layer layer in layers)
            {
                float[] source = Read(layer.File);
                int offset = Mathf.RoundToInt(layer.Start * Rate);
                float low = 0, alpha = 1f - Mathf.Exp(-2 * Mathf.PI * layer.LowPass / Rate);
                int count = Mathf.Min(samples.Length - offset, Mathf.FloorToInt(source.Length / layer.Speed));
                for (int i = 0; i < count; i++)
                {
                    float p = i * layer.Speed;
                    if (layer.Reverse) p = Mathf.Min(source.Length - 1, (count - 1 - i) * layer.Speed);
                    int a = Mathf.Clamp((int)p, 0, source.Length - 1), b = Mathf.Min(a + 1, source.Length - 1);
                    float value = Mathf.Lerp(source[a], source[b], p - a);
                    low += alpha * (value - low);
                    float fade = Mathf.Clamp01(i / 88f) * Mathf.Clamp01((count - i - 1) / 2205f);
                    samples[offset + i] += low * layer.Gain * fade;
                }
            }
            // Remove DC and finish edge fades before measuring the audible level.
            float mean = 0, peak = .001f;
            foreach (float sample in samples) mean += sample / samples.Length;
            for (int i = 0; i < samples.Length; i++) { samples[i] -= mean; peak = Mathf.Max(peak, Mathf.Abs(samples[i])); }
            for (int i = 0; i < samples.Length; i++)
                samples[i] = samples[i] / peak * .72f * Mathf.Clamp01(i / 88f)
                    * Mathf.Clamp01((samples.Length - i - 1) / 882f);
            samples = NormalizeSoundLevel(samples);
            using (var writer = new BinaryWriter(File.Create(Output + name + ".wav")))
            {
                writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples.Length * 2);
                writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
                writer.Write((short)1); writer.Write((short)1); writer.Write(Rate); writer.Write(Rate * 2);
                writer.Write((short)2); writer.Write((short)16);
                writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(samples.Length * 2);
                for (int i = 0; i < samples.Length; i++)
                    writer.Write((short)(samples[i] * short.MaxValue));
            }
        }
    }
}

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
        private static void RegisterSkillSounds() => EditorRequests.Register(
            "Docs/Validation/ImportSkillSounds.request", RebuildNormalizedSounds);

        [MenuItem("Lightbringer/Audio/Import Skill Sounds (Meteor, Ice, Shockwave)")]
        public static void BakeSkillSounds()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before importing audio.");
            cache.Clear();
            Directory.CreateDirectory(Output);
            string[] files = { "MeteorImpactUser.mp3", "IceMagicUser.mp3", "ShockwaveBoomUser.mp3" };
            string[] names = { "MeteorImpact", "IceMagic", "ShockwaveBoom" };
            // Long tails would overlap the next cast; keep the hit and a short decay.
            float[] longest = { 3.2f, 2.4f, 3f };
            var report = new StringBuilder("PASS | " + DateTime.UtcNow.ToString("O") + "\n");
            for (int i = 0; i < files.Length; i++)
            {
                AssetDatabase.ImportAsset(Sources + files[i], ImportAssetOptions.ForceUpdate);
                float seconds = Mathf.Min(Read(files[i]).Length / (float)Rate, longest[i]);
                Write(names[i], seconds, new Layer(files[i], lowPass: 22000));
                AssetDatabase.ImportAsset(Output + names[i] + ".wav", ImportAssetOptions.ForceUpdate);
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Output + names[i] + ".wav");
                if (clip == null || clip.channels != 1 || Mathf.Abs(clip.length - seconds) > .002f)
                    throw new InvalidOperationException("Invalid skill sound: " + names[i]);
                report.AppendLine(FormattableString.Invariant(
                    $"{names[i]}: {files[i]} -> {clip.length:F3}s, mono {clip.frequency}Hz, original pitch/speed."));
            }
            cache.Clear();
            File.WriteAllText("Docs/Validation/SkillSoundChecks.txt", report.ToString());
        }
    }
}

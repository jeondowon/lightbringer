using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Lightbringer.EditorTools
{
    public static class BossArrivalAudioValidation
    {
        [InitializeOnLoadMethod]
        private static void Register() => EditorRequests.Register(
            "Docs/Validation/BossArrivalAudio.request", Validate);

        [MenuItem("Lightbringer/Audio/Validate Boss Arrival Audio")]
        public static void Validate()
        {
            var report = new StringBuilder();
            foreach (string name in new[] { "BossRuptureBoom", "StrongholdExplosion" })
            {
                string path = "Assets/Audio/Resources/SoundEffects/" + name + ".mp3";
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                AudioClip clip = UnityEngine.Resources.Load<AudioClip>("SoundEffects/" + name);
                if (clip == null || clip.length <= 0f)
                    throw new InvalidOperationException("Missing or empty boss arrival clip: " + name);
                if (name == "BossRuptureBoom" && clip.length < Core.BossEncounter.WaveSeconds)
                    throw new InvalidOperationException("Boom is shorter than the shockwave.");
                report.AppendLine(FormattableString.Invariant($"{name}: {clip.length:F3}s, {clip.channels} channels, {clip.frequency}Hz"));
            }
            File.WriteAllText("Docs/Validation/BossArrivalAudioChecks.txt",
                "PASS | " + DateTime.UtcNow.ToString("O") + "\n" + report);
        }
    }
}

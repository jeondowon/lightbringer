using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using Lightbringer.Audio;

namespace Lightbringer.EditorTools
{
    // Measures how loud each music track and sound effect actually is, so music/SFX gains can be balanced by
    // numbers instead of by ear. Effects are read from their imported clips (before SoundEffects levels them to
    // the listed target); streamed music cannot be read back, so it is decoded from the source file.
    // Output: Docs/Diagnostics/AudioLevels.txt. Also runs when Docs/Diagnostics/audio-levels.request exists.
    [InitializeOnLoad]
    public static class AudioLevelReport
    {
        private const string EffectsFolder = "Assets/Audio/Resources/SoundEffects";
        private static string Folder => Path.Combine(Path.GetDirectoryName(Application.dataPath), "Docs", "Diagnostics");
        private static readonly List<(string name, UnityWebRequest request)> pending = new List<(string, UnityWebRequest)>();
        private static StringBuilder report;

        static AudioLevelReport() => EditorRequests.Register(Path.Combine(Folder, "audio-levels.request"), Run, true);

        [MenuItem("Lightbringer/Audio/Measure Audio Levels")]
        public static void Run()
        {
            if (pending.Count > 0) return;
            report = new StringBuilder("Audio levels | " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "\n");
            report.AppendLine("dBFS as imported. rms = gated average, loud = loudest 400 ms window, -> = the clip after SoundEffects leveling.\n");
            report.AppendLine("[Effects]");
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { EffectsFolder }))
            {
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid));
                if (clip == null) continue;
                if (clip.loadState != AudioDataLoadState.Loaded) clip.LoadAudioData();
                float[] data = new float[clip.samples * clip.channels];
                if (!clip.GetData(data, 0)) { report.AppendLine(clip.name + ": unreadable"); continue; }
                string cue = clip.name.Split('_')[0];
                float target = System.Enum.TryParse(cue, out SoundCue parsed) ? SoundEffects.TargetLoudness(parsed) : float.NaN;
                string line = Describe(clip.name, data, clip.channels, clip.frequency);
                if (!float.IsNaN(target))
                {
                    AudioClip leveled = SoundEffects.Level(clip, target);
                    float[] output = new float[leveled.samples * leveled.channels];
                    float peak = 0f;
                    if (leveled.GetData(output, 0)) foreach (float sample in output) peak = Mathf.Max(peak, Mathf.Abs(sample));
                    line += string.Format("  -> target {0,5:0.0}: loud {1,6:0.0}  peak {2,6:0.0}", target,
                        SoundEffects.LoudestWindowDb(output, leveled.channels, leveled.frequency), 20 * Mathf.Log10(Mathf.Max(peak, 1e-6f)));
                    if (leveled != clip) Object.DestroyImmediate(leveled);
                }
                report.AppendLine(line);
            }
            report.AppendLine("\n[Music]");
            Queue(BattleMusicImport.BattlePath);
            Queue(BattleMusicImport.ProloguePath);
            EditorApplication.update += Poll;
        }

        private static void Queue(string assetPath)
        {
            string url = new System.Uri(Path.GetFullPath(assetPath)).AbsoluteUri;
            UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.MPEG);
            var handler = (DownloadHandlerAudioClip)request.downloadHandler;
            handler.streamAudio = false;
            handler.compressed = false;
            request.SendWebRequest();
            pending.Add((Path.GetFileNameWithoutExtension(assetPath), request));
        }

        private static void Poll()
        {
            foreach ((string _, UnityWebRequest request) in pending) if (!request.isDone) return;
            EditorApplication.update -= Poll;
            foreach ((string name, UnityWebRequest request) in pending)
            {
                AudioClip clip = request.result == UnityWebRequest.Result.Success ? DownloadHandlerAudioClip.GetContent(request) : null;
                float[] data = clip != null ? new float[clip.samples * clip.channels] : null;
                if (clip == null || !clip.GetData(data, 0)) report.AppendLine(name + ": unreadable " + request.error);
                else
                {
                    report.AppendLine(Describe(name, data, clip.channels, clip.frequency));
                    Object.DestroyImmediate(clip);
                }
                request.Dispose();
            }
            pending.Clear();
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Path.Combine(Folder, "AudioLevels.txt"), report.ToString());
            Debug.Log("Audio level report written: Docs/Diagnostics/AudioLevels.txt");
        }

        private static string Describe(string name, float[] data, int channels, int frequency)
        {
            int frames = data.Length / Mathf.Max(1, channels);
            int window = Mathf.Max(1, frequency / 20);
            double gatedSum = 0, peak = 0;
            long gatedFrames = 0;
            var squares = new double[frames];
            for (int f = 0; f < frames; f++)
            {
                double sum = 0;
                for (int c = 0; c < channels; c++)
                {
                    double sample = data[f * channels + c];
                    sum += sample * sample;
                    peak = System.Math.Max(peak, System.Math.Abs(sample));
                }
                squares[f] = sum / channels;
            }
            const double gate = 0.0000316; // -45 dBFS power
            for (int start = 0; start < frames; start += window)
            {
                int end = Mathf.Min(frames, start + window);
                double sum = 0;
                for (int f = start; f < end; f++) sum += squares[f];
                if (sum / (end - start) < gate) continue;
                gatedSum += sum;
                gatedFrames += end - start;
            }
            return string.Format("{0,-22} len {1,6:0.00}s  peak {2,6:0.0}  rms {3,6:0.0}  loud {4,6:0.0}",
                name, frames / (float)frequency, Db(peak * peak), Db(gatedFrames > 0 ? gatedSum / gatedFrames : 0),
                SoundEffects.LoudestWindowDb(data, channels, frequency));
        }

        private static double Db(double power) => power <= 1e-12 ? -120 : 10 * System.Math.Log10(power);
    }
}

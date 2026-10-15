using UnityEngine;
using Lightbringer.Core;

namespace Lightbringer.Audio
{
    public enum SoundCue
    {
        Click, Denied, Summon, MeleeHit, HeavyImpact, FireBreath, BossWarning,
        MageFire, PriestHeal, Victory, HeroLightStaff, ArrowHit, HeroHurt, BossImpact, ArrowRelease, Defeat,
        TroopDeathFemale, TroopDeathMale, ShamanWater, EnemyDeath, BossSummon, HeroHeal,
        MeteorImpact, IceMagic, ShockwaveBoom
    }

    // One bounded pool per campaign, independent of music. No AudioSources on individual troops.
    [DisallowMultipleComponent]
    public sealed class SoundEffects : MonoBehaviour
    {
        public const int WorldVoices = 20, InterfaceVoices = 4;
        public const string ResourceFolder = "SoundEffects/";
        private const string VolumeKey = "Lightbringer.SoundEffectsVolume";
        private static SoundEffects current;
        private readonly AudioSource[] sources = new AudioSource[WorldVoices + InterfaceVoices];
        private readonly float[] sourceGains = new float[WorldVoices + InterfaceVoices];
        private static readonly int CueCount = System.Enum.GetValues(typeof(SoundCue)).Length;
        private readonly float[] nextAllowed = new float[CueCount];
        private readonly AudioClip[][] clips = new AudioClip[CueCount][];
        private readonly System.Random variation = new System.Random(713);
        private CampaignSession session;
        private bool worldPaused;
        private float volume;

        public static float Volume
        {
            get => Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 0.7f));
            set
            {
                PlayerPrefs.SetFloat(VolumeKey, float.IsNaN(value) ? 0f : Mathf.Clamp01(value));
                if (current != null) current.ApplyVolume();
            }
        }

        public static void UI(SoundCue cue)
        {
            if (Application.isPlaying && current != null) current.Play(cue, Vector3.zero, true);
        }

        public static void World(SoundCue cue, Vector3 position)
        {
            if (Application.isPlaying && current != null) current.Play(cue, position);
        }

        private void Awake()
        {
            if (!Application.isPlaying) return;
            current = this;
            session = GetComponent<CampaignSession>();
            volume = Volume;
            for (int i = 0; i < clips.Length; i++)
            {
                if (Leveled[i] != null && System.Array.TrueForAll(Leveled[i], clip => clip != null))
                {
                    clips[i] = Leveled[i];
                    continue;
                }
                var variants = new System.Collections.Generic.List<AudioClip>();
                AudioClip main = UnityEngine.Resources.Load<AudioClip>(ResourceFolder + (SoundCue)i);
                if (main != null) variants.Add(Level(main, TargetLoudness((SoundCue)i)));
                else Debug.LogWarning("효과음 누락: " + (SoundCue)i, this);
                for (int take = 2; take <= 3; take++)
                {
                    AudioClip alternate = UnityEngine.Resources.Load<AudioClip>(ResourceFolder + (SoundCue)i + "_" + take);
                    if (alternate != null) variants.Add(Level(alternate, TargetLoudness((SoundCue)i)));
                }
                clips[i] = Leveled[i] = variants.ToArray();
            }
            for (int i = 0; i < sources.Length; i++)
            {
                GameObject voice = new GameObject("SFX Voice " + i);
                voice.transform.SetParent(transform, false);
                AudioSource source = sources[i] = voice.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = i < WorldVoices ? 0.85f : 0f;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = 7f;
                source.maxDistance = 55f;
                source.dopplerLevel = 0f;
                source.reverbZoneMix = 0f;
            }
        }

        public bool Play(SoundCue cue, Vector3 position, bool interfaceSound = false)
        {
            int id = (int)cue;
            if (!Application.isPlaying || !isActiveAndEnabled || id < 0 || id >= clips.Length
                || clips[id] == null || clips[id].Length == 0 || volume <= 0f) return false;
            if (!interfaceSound)
            {
                if (session == null || session.Battle == null || session.Battle.Objective.HasEnded
                    || session.IsUserPaused || session.IsChoosing || session.IsBattlePaused) return false;
                UnityEngine.Camera camera = session.Battle.Camera;
                if (camera == null || (position - camera.transform.position).sqrMagnitude > 55f * 55f) return false;
            }
            float now = Time.unscaledTime;
            if (now < nextAllowed[id]) return false;
            int begin = interfaceSound ? WorldVoices : 0;
            int end = interfaceSound ? sources.Length : WorldVoices;
            int priority = Priority(cue, interfaceSound), slot = -1;
            for (int i = begin; i < end; i++)
            {
                if (!sources[i].isPlaying) { slot = i; break; }
                if (sources[i].priority > priority && (slot < 0 || sources[i].priority > sources[slot].priority)) slot = i;
            }
            if (slot < 0) return false;
            nextAllowed[id] = now + Cooldown(cue);
            AudioSource source = sources[slot];
            source.Stop();
            source.transform.position = position;
            source.clip = clips[id][variation.Next(clips[id].Length)];
            source.priority = priority;
            source.pitch = interfaceSound || cue == SoundCue.MageFire || cue == SoundCue.PriestHeal
                || cue == SoundCue.ArrowHit || cue == SoundCue.BossImpact || cue == SoundCue.ArrowRelease
                || cue == SoundCue.TroopDeathFemale || cue == SoundCue.TroopDeathMale
                || cue == SoundCue.ShamanWater || cue == SoundCue.EnemyDeath || cue == SoundCue.BossSummon
                ? 1f : 0.98f + (float)variation.NextDouble() * 0.04f;
            // Keep clicks softer while preserving the user's effects volume setting.
            // Boss attacks stay at full level and mostly ignore distance falloff so they read as the loudest sound; everything else sits lower.
            bool boss = cue == SoundCue.BossWarning || cue == SoundCue.BossImpact || cue == SoundCue.BossSummon;
            // The hero's big spells are deep booms that vanish on small speakers at 3D falloff: full level, mostly 2D.
            bool heroBoom = cue == SoundCue.MeteorImpact || cue == SoundCue.ShockwaveBoom;
            bool heroSpell = heroBoom || cue == SoundCue.IceMagic || cue == SoundCue.HeroLightStaff || cue == SoundCue.HeroHeal;
            sourceGains[slot] = boss || heroSpell ? 1f : cue == SoundCue.Click ? 0.35f : 0.7f;
            if (!interfaceSound) source.spatialBlend = boss || heroBoom ? 0.15f : heroSpell ? 0.35f : 0.85f;
            source.volume = volume * sourceGains[slot];
            source.Play();
            // Both booms open with ~0.25 s of near silence; start at the hit so the sound meets the visual.
            if (heroBoom && source.clip.length > 0.5f) source.time = 0.22f;
            return true;
        }

        public void StopWorld()
        {
            for (int i = 0; i < WorldVoices; i++) if (sources[i] != null) sources[i].Stop();
            worldPaused = false;
        }

        private void Update()
        {
            if (session == null || session.Battle == null || session.Battle.Objective.HasEnded) { StopWorld(); return; }
            bool paused = session.IsUserPaused || session.IsChoosing || session.IsBattlePaused;
            if (paused == worldPaused) return;
            worldPaused = paused;
            for (int i = 0; i < WorldVoices; i++)
                if (paused) sources[i].Pause(); else sources[i].UnPause();
        }

        private void ApplyVolume()
        {
            volume = Volume;
            for (int i = 0; i < sources.Length; i++)
                if (sources[i] != null) sources[i].volume = volume * sourceGains[i];
        }

        private void OnDisable()
        {
            foreach (AudioSource source in sources) if (source != null) source.Stop();
            worldPaused = false;
        }
        private void OnDestroy() { if (current == this) current = null; }

        // Source files arrive at very different levels (loudest 400 ms from -23 to -14 dBFS) and peak far below
        // full scale, so the voice gain alone (capped at 1) left spells under the music. Each clip is raised once
        // per run to its group's loudness; a look-ahead limiter keeps the boosted transients under the ceiling.
        // Measure the sources with Lightbringer/Audio/Measure Audio Levels.
        private static readonly AudioClip[][] Leveled = new AudioClip[CueCount][];
        private const float MaxBoostDb = 15f, Ceiling = 0.89f;

        // The window measures raw power, which overrates deep booms and underrates the bright beam by ~4-5 dB of
        // perceived (K-weighted) loudness, so those cues get their own targets.
        public static float TargetLoudness(SoundCue cue) =>
            // Meteor and shockwave are the hero's big spells: they must read at least as loud as the boss.
            cue == SoundCue.MeteorImpact || cue == SoundCue.ShockwaveBoom ? -8f
            : cue == SoundCue.IceMagic || cue == SoundCue.HeroHeal
            || cue == SoundCue.BossWarning || cue == SoundCue.BossImpact || cue == SoundCue.BossSummon ? -10f
            : cue == SoundCue.Victory || cue == SoundCue.Defeat ? -12f
            // The beam is the hero's repeated main attack and bright: well under the big spells so it never tires the ear.
            : cue == SoundCue.HeroLightStaff ? -17f
            : -15f;

        // Power (dBFS) of the loudest 400 ms window, or of the whole clip when it is shorter.
        public static float LoudestWindowDb(float[] data, int channels, int frequency)
        {
            int frames = data.Length / Mathf.Max(1, channels), window = Mathf.Max(1, frequency * 2 / 5);
            double running = 0, loudest = 0;
            for (int f = 0; f < frames; f++)
            {
                running += FramePower(data, f, channels);
                if (f >= window) running -= FramePower(data, f - window, channels);
                if (f >= window - 1 || f == frames - 1) loudest = System.Math.Max(loudest, running / Mathf.Min(f + 1, window));
            }
            return loudest <= 1e-12 ? -120f : (float)(10 * System.Math.Log10(loudest));
        }

        private static double FramePower(float[] data, int frame, int channels)
        {
            double sum = 0;
            for (int c = 0; c < channels; c++) sum += data[frame * channels + c] * (double)data[frame * channels + c];
            return sum / channels;
        }

        public static AudioClip Level(AudioClip source, float targetDb)
        {
            int channels = source.channels, frames = source.samples;
            float[] data = new float[frames * channels];
            if (frames == 0 || !source.GetData(data, 0)) return source;
            float gain = Mathf.Pow(10f, Mathf.Clamp(targetDb - LoudestWindowDb(data, channels, source.frequency), -12f, MaxBoostDb) / 20f);
            // Gain reduction reaches each over-ceiling peak ~3 ms early and recovers over ~60 ms, so no sample clips
            // and the limiter does not pump.
            float[] envelope = new float[frames];
            for (int f = 0; f < frames; f++)
            {
                float peak = 0f;
                for (int c = 0; c < channels; c++) peak = Mathf.Max(peak, Mathf.Abs(data[f * channels + c]) * gain);
                envelope[f] = peak > Ceiling ? Ceiling / peak : 1f;
            }
            float attack = 1f / Mathf.Max(1, source.frequency / 330), release = 1f / Mathf.Max(1, source.frequency / 16);
            for (int f = frames - 2; f >= 0; f--) envelope[f] = Mathf.Min(envelope[f], envelope[f + 1] + attack);
            for (int f = 1; f < frames; f++) envelope[f] = Mathf.Min(envelope[f], envelope[f - 1] + release);
            for (int f = 0; f < frames; f++)
                for (int c = 0; c < channels; c++) data[f * channels + c] *= gain * envelope[f];
            AudioClip leveled = AudioClip.Create(source.name, frames, channels, source.frequency, false);
            leveled.SetData(data, 0);
            leveled.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return leveled;
        }

        private static int Priority(SoundCue cue, bool ui) => cue == SoundCue.Victory || cue == SoundCue.Defeat ? 10
            : cue == SoundCue.BossWarning || cue == SoundCue.BossImpact || cue == SoundCue.BossSummon || cue == SoundCue.HeroHurt ? 30 : ui ? 50 : 150;
        private static float Cooldown(SoundCue cue) => cue == SoundCue.PriestHeal || cue == SoundCue.ShamanWater || cue == SoundCue.Denied ? 0.25f
            : cue == SoundCue.TroopDeathFemale || cue == SoundCue.TroopDeathMale || cue == SoundCue.EnemyDeath ? .35f
            : cue == SoundCue.MageFire ? 0.12f : cue == SoundCue.HeroHurt ? 0.2f : 0.065f;
    }
}

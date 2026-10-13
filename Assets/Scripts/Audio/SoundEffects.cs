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
                var variants = new System.Collections.Generic.List<AudioClip>();
                AudioClip main = UnityEngine.Resources.Load<AudioClip>(ResourceFolder + (SoundCue)i);
                if (main != null) variants.Add(main);
                else Debug.LogWarning("효과음 누락: " + (SoundCue)i, this);
                for (int take = 2; take <= 3; take++)
                {
                    AudioClip alternate = UnityEngine.Resources.Load<AudioClip>(ResourceFolder + (SoundCue)i + "_" + take);
                    if (alternate != null) variants.Add(alternate);
                }
                clips[i] = variants.ToArray();
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
            sourceGains[slot] = boss ? 1f : cue == SoundCue.Click ? 0.35f : 0.7f;
            if (!interfaceSound) source.spatialBlend = boss ? 0.15f : 0.85f;
            source.volume = volume * sourceGains[slot];
            source.Play();
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

        private static int Priority(SoundCue cue, bool ui) => cue == SoundCue.Victory || cue == SoundCue.Defeat ? 10
            : cue == SoundCue.BossWarning || cue == SoundCue.BossImpact || cue == SoundCue.BossSummon || cue == SoundCue.HeroHurt ? 30 : ui ? 50 : 150;
        private static float Cooldown(SoundCue cue) => cue == SoundCue.PriestHeal || cue == SoundCue.ShamanWater || cue == SoundCue.Denied ? 0.25f
            : cue == SoundCue.TroopDeathFemale || cue == SoundCue.TroopDeathMale || cue == SoundCue.EnemyDeath ? .35f
            : cue == SoundCue.MageFire ? 0.12f : cue == SoundCue.HeroHurt ? 0.2f : 0.065f;
    }
}

using UnityEngine;

namespace Lightbringer.Core
{
    // Owned by one battlefield so retries and scene changes cannot leave music behind.
    [DisallowMultipleComponent]
    public sealed class BattleMusic : MonoBehaviour
    {
        public const string ResourcePath = "Music/TheTreadOfWar";
        public const string Credit = "전투 음악: The Tread of War — Jonathan Shaw\nwww.jshaw.co.uk · CC BY 3.0\nhttps://creativecommons.org/licenses/by/3.0/";
        private const string VolumeKey = "Lightbringer.MusicVolume";
        private CampaignSession session;
        private AudioSource source;
        private float envelope;

        public static float Volume
        {
            get => Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 0.5f));
            set => PlayerPrefs.SetFloat(VolumeKey, Mathf.Clamp01(value));
        }

        // Music sits under the effects: at a full slider its loudest passages land ~7 dB below the hero's spells.
        public const float Mix = 0.5f;
        // Measured with Lightbringer/Audio/Measure Audio Levels: the battle track averages -15.2 dBFS (loudest
        // 400 ms -10.1) against the prologue's -16.3 (-12.0); it is trimmed by 1.5 dB so both sit level.
        public const float BattleTrim = 0.84f, PrologueTrim = 1f;

        // Every music player (title, prologue, battle) goes through here so the tracks stay level with each other.
        public static float Output(float trim) => Volume * Mix * trim;

        public void Configure(CampaignSession owner)
        {
            session = owner;
            if (source == null) source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.priority = 0;
            source.loop = true;
            source.volume = 0f;
            envelope = 0f;
            source.clip = UnityEngine.Resources.Load<AudioClip>(ResourcePath);
            if (source.clip == null)
            {
                Debug.LogWarning("전투 음악을 불러오지 못했습니다: " + ResourcePath, this);
                return;
            }
            source.Play();
        }

        private void Update()
        {
            if (source == null || source.clip == null || session == null) return;
            bool ended = session.Battle == null || session.Battle.Objective.HasEnded;
            float target = ended ? 0f : session.IsUserPaused || session.IsChoosing ? 0.35f : 1f;
            // Pause uses timeScale=0: transitions must continue on unscaled time.
            envelope = Mathf.MoveTowards(envelope, target, Time.unscaledDeltaTime / 1.2f);
            source.volume = Output(BattleTrim) * envelope;
            if (ended && envelope == 0f && source.isPlaying) source.Stop();
        }

        private void OnDisable()
        {
            if (source != null) source.Stop();
        }
    }
}

using Lightbringer.Core;
using UnityEngine;

namespace Lightbringer.Audio
{
    // Owned by the encounter, so leaving/restarting the battle also removes its sound.
    public sealed class BossArrivalAudio : MonoBehaviour
    {
        private AudioSource boom, explosion;
        private float elapsed;
        private bool paused;

        public static void Begin(Transform encounter)
        {
            if (!Application.isPlaying) return;
            var root = new GameObject("Boss Arrival Audio");
            root.transform.SetParent(encounter, false);
            var audio = root.AddComponent<BossArrivalAudio>();
            audio.boom = audio.CreateVoice("BossRuptureBoom");
            audio.explosion = audio.CreateVoice("StrongholdExplosion");
        }

        private AudioSource CreateVoice(string clipName)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.reverbZoneMix = 0f;
            source.priority = 20;
            source.volume = SoundEffects.Volume * .7f;
            source.clip = UnityEngine.Resources.Load<AudioClip>(SoundEffects.ResourceFolder + clipName);
            if (source.clip != null) source.Play();
            else Debug.LogWarning("군주 등장 효과음 누락: " + clipName, this);
            return source;
        }

        private void Update()
        {
            // The encounter suspends combat components, but its sound continues with the cinematic.
            // A real user pause freezes both the camera/VFX and these voices.
            bool shouldPause = Time.timeScale <= 0f;
            if (paused != shouldPause)
            {
                paused = shouldPause;
                if (paused) { boom.Pause(); explosion.Pause(); }
                else { boom.UnPause(); explosion.UnPause(); }
            }
            elapsed += Time.deltaTime;
            float fade = Mathf.Clamp01((BossEncounter.WaveSeconds - elapsed) / .5f);
            boom.volume = explosion.volume = SoundEffects.Volume * .7f * fade;
            if (elapsed >= BossEncounter.WaveSeconds) Destroy(gameObject);
        }

        private void OnDisable()
        {
            if (boom != null) boom.Stop();
            if (explosion != null) explosion.Stop();
        }
    }
}

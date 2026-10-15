using System;
using System.Linq;
using Lightbringer.Audio;
using Lightbringer.Combat;
using Lightbringer.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Lightbringer.EditorTools
{
    public static partial class CampaignPlayValidation
    {
        private static void ValidateSoundEffects(PrototypeBattle battle, Combatant summoned)
        {
            foreach (SoundCue cue in Enum.GetValues(typeof(SoundCue)))
            {
                AudioClip clip = UnityEngine.Resources.Load<AudioClip>(SoundEffects.ResourceFolder + cue);
                Assert(clip != null && clip.channels == 1 && clip.samples > 0 && clip.loadState == AudioDataLoadState.Loaded,
                    "SFX loads as ready mono PCM: " + cue);
                float[] samples = new float[clip.samples];
                Assert(clip.GetData(samples, 0) && samples.Any(sample => Mathf.Abs(sample) > .1f)
                    && samples.All(sample => !float.IsNaN(sample) && Mathf.Abs(sample) < .99f),
                    "SFX contains non-silent, unclipped audio: " + cue);
            }
            SoundEffects pool = session.GetComponent<SoundEffects>();
            Assert(pool != null && summoned.GetComponent<UnitAudio>() != null && battle.Root.GetComponent<BattleAudio>() != null,
                "Campaign and newly summoned troop receive audio automatically without scene setup");
            AudioSource[] sources = pool.GetComponentsInChildren<AudioSource>().Where(s => s.name.StartsWith("SFX Voice ")).ToArray();
            Assert(sources.Length == SoundEffects.WorldVoices + SoundEffects.InterfaceVoices,
                "Campaign owns exactly 20 world voices and 4 reserved interface voices");
            bool hadVolume = PlayerPrefs.HasKey("Lightbringer.SoundEffectsVolume");
            float previous = SoundEffects.Volume;
            float music = BattleMusic.Volume;
            try
            {
                SoundEffects.Volume = .7f;
                Vector3 point = battle.Camera.transform.position + battle.Camera.transform.forward * 8f;
                Assert(!pool.Play(SoundCue.HeavyImpact, point + Vector3.right * 1000f), "Inaudible distant sounds are culled before occupying a voice");
                int accepted = 0;
                for (int i = 0; i < 1000; i++) if (pool.Play(SoundCue.HeavyImpact, point)) accepted++;
                Assert(accepted == 1 && sources.Count(s => s.isPlaying && s.clip.name == "HeavyImpact") == 1,
                    "A thousand simultaneous impacts yield one rate-limited sound without adding sources");
                Assert(sources.Where(s => s.isPlaying && s.clip.name == "HeavyImpact").All(s => s.spatialBlend > 0 && s.dopplerLevel == 0),
                    "Battle sounds spatialize at impact positions without Doppler pitch shifts");
                Assert(sources.Where(s => s.isPlaying).All(s => Mathf.Approximately(s.volume, .7f * .7f)),
                    "Leveled battle SFX follow the user's slider at the combat mix gain");
                SoundEffects.Volume = 1f;
                Assert(sources.Where(s => s.isPlaying).All(s => Mathf.Approximately(s.volume, .7f)),
                    "SFX slider maximum raises existing voices to their mix gain");
                SoundEffects.Volume = 0;
                Assert(sources.All(s => s.volume == 0f) && !pool.Play(SoundCue.MageFire, point)
                    && Mathf.Approximately(BattleMusic.Volume, music), "SFX mute affects existing and new voices without changing music");
                SoundEffects.Volume = .7f;
                Assert(pool.Play(SoundCue.Click, Vector3.zero, true), "Interface feedback uses the reserved interface pool");
                Assert(sources.Any(s => s.isPlaying && s.clip.name == "Click" && s.spatialBlend == 0),
                    "Interface feedback remains audible independently of camera distance");
                Assert(sources.Where(s => s.isPlaying && s.clip.name == "Click").All(s => Mathf.Approximately(s.volume, .7f * .35f)),
                    "Button feedback sits under battlefield sounds");
                ValidateEnemyDeathAudio(pool, sources, point);
                pool.StopWorld();
                Assert(sources.Where(s => s.spatialBlend > 0).All(s => !s.isPlaying), "Battle cleanup stops every world voice");
            }
            finally
            {
                SoundEffects.Volume = previous;
                if (!hadVolume) PlayerPrefs.DeleteKey("Lightbringer.SoundEffectsVolume");
            }
        }

        private static void ValidateEnemyDeathAudio(SoundEffects pool, AudioSource[] sources, Vector3 point)
        {
            int DeathVoices() => sources.Count(source => source.isPlaying && source.clip.name == "EnemyDeath");
            pool.StopWorld();
            // Real Combatant damage/death events, including the disable-before-Died lifecycle.
            foreach (Units.EnemyKind? kind in new Units.EnemyKind?[] { null, Units.EnemyKind.Boss })
            {
                var entity = new GameObject("Silent death validation");
                entity.transform.position = point;
                Combatant health = entity.AddComponent<Combatant>();
                health.Configure(Faction.Enemy, 10);
                UnitAudio audio = entity.AddComponent<UnitAudio>();
                audio.Configure(pool);
                if (kind.HasValue) audio.SetEnemyKind(kind.Value);
                health.TakeDamage(20);
            }
            Assert(DeathVoices() == 0, "Stronghold destruction and Warlord retreat do not play the troop death grunt");
            var troop = new GameObject("Enemy death validation");
            troop.transform.position = point;
            Combatant enemy = troop.AddComponent<Combatant>();
            enemy.Configure(Faction.Enemy, 10);
            UnitAudio voice = troop.AddComponent<UnitAudio>();
            voice.Configure(pool);
            voice.SetEnemyKind(Units.EnemyKind.Brute);
            enemy.TakeDamage(1);
            Assert(DeathVoices() == 0, "Nonlethal enemy hits do not play Monster Death Grunt");
            enemy.TakeDamage(20);
            Assert(DeathVoices() == 1, "An enemy death plays Monster Death Grunt after the unit is disabled");
            Assert(!pool.Play(SoundCue.EnemyDeath, point), "Simultaneous enemy deaths share the death voice repetition limit");
        }

        private static void ValidatePausedSoundEffects()
        {
            SoundEffects pool = session.GetComponent<SoundEffects>();
            bool hadVolume = PlayerPrefs.HasKey("Lightbringer.SoundEffectsVolume");
            float previous = SoundEffects.Volume;
            try
            {
                SoundEffects.Volume = .7f;
                Assert(!pool.Play(SoundCue.MeleeHit, session.Battle.Camera.transform.position), "Pause rejects new battlefield sounds");
                Assert(pool.Play(SoundCue.Click, Vector3.zero, true), "Menu sounds still play while timeScale is zero");
                var view = session.GetComponent<Lightbringer.UI.CampaignHUD>().Pause;
                view.Refresh(session);
                Assert(view.Root.Query<UnityEngine.UIElements.Slider>().ToList().Count == 2,
                    "Korean pause screen provides independent music and SFX sliders");
            }
            finally
            {
                SoundEffects.Volume = previous;
                if (!hadVolume) PlayerPrefs.DeleteKey("Lightbringer.SoundEffectsVolume");
            }
        }
    }
}

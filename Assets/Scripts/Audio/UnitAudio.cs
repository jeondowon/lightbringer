using Lightbringer.Combat;
using Lightbringer.Units;
using UnityEngine;

namespace Lightbringer.Audio
{
    // No Update or AudioSource per soldier.
    public sealed class UnitAudio : MonoBehaviour
    {
        private SoundEffects soundEffects;
        private Combatant health;
        private UnitCombat combat;
        private UnitSupport support;
        private SoundCue? deathVoice;
        private EnemyKind? enemyKind;
        public void Configure(SoundEffects owner)
        {
            soundEffects = owner;
            health = GetComponent<Combatant>();
            combat = GetComponent<UnitCombat>();
            support = GetComponent<UnitSupport>();
            health.Died += Died;
            if (combat != null)
            {
                combat.StrikeLanded += Strike;
                combat.ProjectileLaunched += Launch;
                combat.ChargeLanded += Charge;
            }
            if (support != null) support.Pulsed += Heal;
        }
        public void SetTroopDeathVoice(UnitKind kind)
        {
            enemyKind = null;
            switch (kind)
            {
                case UnitKind.Archer:
                case UnitKind.Priest:
                case UnitKind.Mage:
                    deathVoice = SoundCue.TroopDeathFemale;
                    break;
                case UnitKind.Swordsman:
                case UnitKind.Shieldbearer:
                case UnitKind.Spearman:
                case UnitKind.Knight:
                    deathVoice = SoundCue.TroopDeathMale;
                    break;
                default:
                    deathVoice = null;
                    break;
            }
        }
        public void SetEnemyKind(EnemyKind kind)
        {
            enemyKind = kind;
            // The Warlord retreats in a story sequence; strongholds never receive a roster kind.
            deathVoice = kind == EnemyKind.Boss ? (SoundCue?)null : SoundCue.EnemyDeath;
        }
        private void Died(Combatant unit)
        {
            // Combatant raises Died after disabling the soldier; the campaign pool remains active.
            if (deathVoice.HasValue && soundEffects != null)
                soundEffects.Play(deathVoice.Value, transform.position);
        }
        private void Strike(Combatant target, Vector3 point)
        {
            SoundCue cue = combat.SplashRadius >= 4f && health.Faction == Faction.Allied ? SoundCue.FireBreath
                : health.IsHeavy ? SoundCue.HeavyImpact : SoundCue.MeleeHit;
            if (soundEffects != null) soundEffects.Play(cue, point);
        }
        private void Launch(Projectile shot)
        {
            bool mage = shot.Kind == ProjectileKind.Bolt && health.Faction == Faction.Allied;
            if (mage && soundEffects != null) soundEffects.Play(SoundCue.MageFire, shot.transform.position);
            if (shot.Kind == ProjectileKind.Bolt && health.Faction == Faction.Enemy
                && enemyKind == EnemyKind.Shaman && soundEffects != null)
                soundEffects.Play(SoundCue.ShamanWater, shot.transform.position);
            if (shot.Kind == ProjectileKind.Arrow)
            {
                if (soundEffects != null) soundEffects.Play(SoundCue.ArrowRelease, shot.transform.position);
                // The projectile can outlive its shooter; retain only the campaign pool.
                SoundEffects pool = soundEffects;
                shot.Landed += (projectile, point, target) =>
                {
                    if (target != null && pool != null) pool.Play(SoundCue.ArrowHit, point);
                };
            }
        }
        private void Charge() { if (soundEffects != null) soundEffects.Play(SoundCue.HeavyImpact, transform.position); }
        private void Heal()
        {
            if (health.Faction == Faction.Allied && soundEffects != null)
                soundEffects.Play(SoundCue.PriestHeal, transform.position);
            else if (health.Faction == Faction.Enemy && enemyKind == EnemyKind.Shaman && soundEffects != null)
                soundEffects.Play(SoundCue.ShamanWater, transform.position);
        }
        private void OnDestroy()
        {
            if (health != null) health.Died -= Died;
            if (combat != null)
            {
                combat.StrikeLanded -= Strike;
                combat.ProjectileLaunched -= Launch;
                combat.ChargeLanded -= Charge;
            }
            if (support != null) support.Pulsed -= Heal;
        }
    }
}

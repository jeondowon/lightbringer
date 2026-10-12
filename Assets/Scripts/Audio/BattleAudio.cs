using Lightbringer.Combat;
using Lightbringer.Core;
using Lightbringer.Player;
using UnityEngine;

namespace Lightbringer.Audio
{
    // Presentation observes existing gameplay events; it never controls damage, resources or progression.
    public sealed class BattleAudio : MonoBehaviour
    {
        private PrototypeBattle battle;
        private SoundEffects soundEffects;

        public void Configure(CampaignSession owner)
        {
            battle = owner.Battle;
            soundEffects = owner.GetComponent<SoundEffects>();
            battle.Summoner.Summoned += Summoned;
            battle.Waves.Spawned += Register;
            battle.Abilities.Casted += Cast;
            battle.Hero.Damaged += HeroHit;
            battle.Objective.Completed += Completed;
            foreach (Combatant unit in battle.Root.GetComponentsInChildren<Combatant>()) Register(unit);
        }

        private void Register(Combatant unit)
        {
            if (unit == null) return;
            if (!unit.TryGetComponent<UnitAudio>(out var audio))
            {
                audio = unit.gameObject.AddComponent<UnitAudio>();
                audio.Configure(soundEffects);
            }
            if (battle.Waves.TryGetKind(unit, out var kind)) audio.SetEnemyKind(kind);
        }
        private void Summoned(Combatant unit)
        {
            Register(unit);
            if (unit != null && unit.TryGetComponent<UnitAudio>(out var audio))
                audio.SetTroopDeathVoice(battle.Summoner.SelectedUnit);
            SoundEffects.UI(SoundCue.Summon);
        }
        private void HeroHit(Combatant unit, float amount, Combatant attacker) => SoundEffects.UI(SoundCue.HeroHurt);
        private void Cast(int slot)
        {
            if (battle.Abilities.Equipped(slot) == (int)EquipmentKind.LightStaff)
                SoundEffects.UI(SoundCue.HeroLightStaff);
            else if (battle.Abilities.Equipped(slot) == (int)EquipmentKind.HealingStaff)
                SoundEffects.UI(SoundCue.HeroHeal);
        }
        private void Completed(bool victory)
        {
            soundEffects.StopWorld();
            SoundEffects.UI(victory ? SoundCue.Victory : SoundCue.Defeat);
        }
        private void OnDestroy()
        {
            if (battle == null) return;
            if (battle.Summoner != null) battle.Summoner.Summoned -= Summoned;
            if (battle.Waves != null) battle.Waves.Spawned -= Register;
            if (battle.Abilities != null) battle.Abilities.Casted -= Cast;
            if (battle.Hero != null) battle.Hero.Damaged -= HeroHit;
            if (battle.Objective != null) battle.Objective.Completed -= Completed;
            if (soundEffects != null) soundEffects.StopWorld();
        }
    }
}

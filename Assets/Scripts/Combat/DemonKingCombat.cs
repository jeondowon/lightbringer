using System.Collections.Generic;
using Lightbringer.Core;
using Lightbringer.Visuals;
using UnityEngine;

namespace Lightbringer.Combat
{
    public enum DemonKingAttack { DarkLightning, AbyssRain }

    // The Demon King's sky magic in the final battle (FinalBattle.md, first candidates). Every strike is read
    // from its ground warning; nobody has to look up. Its health is the abyss core's. Big attacks alternate with the
    // Warlord's: it waits while he casts, charges or chains, and he waits while it casts (BossCombat.HoldSpecials).
    [DefaultExecutionOrder(-60)]
    public sealed class DemonKingCombat : MonoBehaviour
    {
        public const float LightningRadius = 3.4f, LightningWindup = 1.8f, LightningDamage = 34f;
        public const float RainRadius = 2.6f, RainWindup = 1.6f, RainStagger = .35f, RainDamage = 24f, RainSpacing = 4.5f;
        // Quiet time after either boss's special, so warnings never stack into an unavoidable overlap.
        public const float Gap = 1.1f;
        private const float FirstCast = 3.5f, Cooldown = 5.5f, BereavedCooldown = 3.6f;

        private struct Strike
        {
            public Vector3 Point;
            public float Delay;
            public BossVfx Warning;
            public bool Resolved;
        }

        private readonly List<Strike> strikes = new List<Strike>();
        private PrototypeBattle battle;
        private ArtStyleLibrary art;
        private Combatant core;
        private DemonKingSky sky;
        private BossCombat warlord;
        private float cooldown = FirstCast, gap, timer;
        private int next;
        public DemonKingAttack CurrentAttack { get; private set; }
        public bool IsAttacking { get; private set; }
        public int SpecialsResolved { get; private set; }
        // The Warlord fell first: shorter waits and a longer rain, without restoring any health.
        public bool Bereaved { get; set; }
        // The Warlord wards the core while he stands (BossEncounter): it takes no damage, but the sky keeps casting.
        public bool Warded { get; set; }
        public bool HoldsWarlord => IsAttacking || gap > 0;
        public Combatant Core => core;

        public void Configure(PrototypeBattle value, Combatant abyssCore, DemonKingSky darkness, BossCombat partner, ArtStyleLibrary style)
        {
            battle = value; core = abyssCore; sky = darkness; warlord = partner; art = style;
            if (warlord != null) warlord.HoldSpecials = () => HoldsWarlord && core != null && core.IsAlive;
            if (core != null)
            {
                core.Damaged += OnCoreDamaged;
                core.Died += OnCoreDied;
            }
        }

        private void Update() => Tick(Time.deltaTime);

        public void Tick(float delta)
        {
            if (!isActiveAndEnabled || delta <= 0 || battle == null || core == null || !core.IsAlive || (core.Invulnerable && !Warded)
                || battle.Objective.HasEnded || battle.Hero == null || !battle.Hero.IsAlive) return;
            if (IsAttacking)
            {
                timer += delta;
                bool pending = false;
                float charge = 0;
                for (int i = 0; i < strikes.Count; i++)
                {
                    Strike strike = strikes[i];
                    if (strike.Resolved) continue;
                    float windup = Windup;
                    float progress = (timer - strike.Delay) / windup;
                    if (strike.Warning != null) strike.Warning.SetProgress(progress);
                    charge = Mathf.Max(charge, progress);
                    if (progress < 1) { pending = true; continue; }
                    Resolve(ref strike);
                    // A strike can end the battle, which disables this component and clears the list.
                    if (i >= strikes.Count || battle.Objective.HasEnded) return;
                    strikes[i] = strike;
                }
                if (sky != null) sky.SetCharge(charge);
                if (!pending) Finish();
                return;
            }
            // The Warlord's specials and his crescent-to-charge link hold the sky back, followed by a short gap.
            if (warlord != null && warlord.isActiveAndEnabled && warlord.IsBusy) { gap = Gap; return; }
            gap = Mathf.Max(0, gap - delta);
            cooldown -= delta;
            if (cooldown > 0 || gap > 0) return;
            Begin(next == 1 && FrontTarget(out _) ? DemonKingAttack.AbyssRain : DemonKingAttack.DarkLightning);
        }

        private float Windup => CurrentAttack == DemonKingAttack.DarkLightning ? LightningWindup : RainWindup;
        private float Radius => CurrentAttack == DemonKingAttack.DarkLightning ? LightningRadius : RainRadius;

        private void Begin(DemonKingAttack attack)
        {
            CurrentAttack = attack;
            IsAttacking = true;
            timer = 0;
            strikes.Clear();
            next = (next + 1) % 2;
            if (attack == DemonKingAttack.DarkLightning)
                AddStrike(battle.Hero.transform.position, 0);
            else if (FrontTarget(out Vector3 front))
            {
                // A short line of drops pressed onto one front, each landing in turn; the gaps between them are safe.
                int count = Bereaved ? 6 : 4;
                var random = new System.Random(SpecialsResolved * 7919 + 17);
                for (int i = 0; i < count; i++)
                {
                    Vector3 point = front + Vector3.back * (RainSpacing * (i - 1)) + Vector3.right * ((float)random.NextDouble() - .5f) * 3f;
                    AddStrike(point, i * RainStagger);
                }
            }
            if (Application.isPlaying)
                Lightbringer.Audio.SoundEffects.World(Lightbringer.Audio.SoundCue.BossWarning, strikes.Count > 0 ? strikes[0].Point : core.transform.position);
        }

        private void AddStrike(Vector3 point, float delay)
        {
            point.y = battle.Root.transform.position.y + .08f;
            BossVfx warning = Application.isPlaying ? BossVfx.Spawn(battle.Root.transform, point, BossVfx.Shape.WarningCircle, Radius, -1, art) : null;
            // Dark lightning gathers in a lance under a storm eye; each abyss drop is seen falling from the sky.
            if (warning != null && CurrentAttack == DemonKingAttack.DarkLightning) warning.AddSkyLance(38, .8f);
            else if (warning != null) warning.AddFallingOrb(30, 1.3f);
            strikes.Add(new Strike { Point = point, Delay = delay, Warning = warning });
        }

        // The most advanced living allied troop: the sky presses the front that threatens it most.
        private bool FrontTarget(out Vector3 point)
        {
            point = default;
            float best = float.NegativeInfinity;
            foreach (Combatant unit in battle.Root.GetComponentsInChildren<Combatant>())
            {
                if (unit == null || !unit.IsAlive || unit.Faction != Faction.Allied || unit == battle.Hero || unit == battle.AlliedBase
                    || unit.GetComponent<UnitCombat>() == null) continue;
                float z = battle.Root.transform.InverseTransformPoint(unit.transform.position).z;
                if (z <= best) continue;
                best = z; point = unit.transform.position;
            }
            return best > float.NegativeInfinity;
        }

        private void Resolve(ref Strike strike)
        {
            strike.Resolved = true;
            if (strike.Warning != null) strike.Warning.Remove();
            strike.Warning = null;
            float radius = Radius, damage = CurrentAttack == DemonKingAttack.DarkLightning ? LightningDamage : RainDamage;
            if (Application.isPlaying)
            {
                Vector3 ground = strike.Point; ground.y = battle.Root.transform.position.y + .02f;
                BossStrikeVfx.Spawn(art, battle.Root.transform, ground, Vector3.back,
                    CurrentAttack == DemonKingAttack.DarkLightning ? BossStrikeVfx.Kind.DarkLightning : BossStrikeVfx.Kind.AbyssDrop, radius);
                CombatEffects.DustRing(ground, Mathf.Min(radius * .5f, 3), 10);
                Lightbringer.Audio.SoundEffects.World(Lightbringer.Audio.SoundCue.BossImpact, ground);
                CameraSystem.ThirdPersonCamera.Shake(ground, CurrentAttack == DemonKingAttack.DarkLightning ? .55f : .3f);
            }
            foreach (Combatant target in battle.Root.GetComponentsInChildren<Combatant>())
            {
                if (target == null || !target.IsAlive || target.Faction != Faction.Allied) continue;
                Vector3 offset = target.GetAimPoint(strike.Point) - strike.Point; offset.y = 0;
                if (offset.sqrMagnitude > radius * radius) continue;
                target.TakeDamage(damage, core);
                if (battle.Objective.HasEnded) return;
            }
        }

        private void Finish()
        {
            IsAttacking = false;
            SpecialsResolved++;
            strikes.Clear();
            gap = Gap;
            cooldown = Bereaved ? BereavedCooldown : Cooldown;
            if (sky != null) sky.SetCharge(0);
        }

        private void ClearWarnings()
        {
            foreach (Strike strike in strikes) if (strike.Warning != null) strike.Warning.Remove();
            strikes.Clear();
            IsAttacking = false;
            if (sky != null) sky.SetCharge(0);
        }

        private void OnCoreDamaged(Combatant unit, float amount, Combatant attacker) { if (sky != null && amount > 0) sky.Flinch(); }

        private void OnCoreDied(Combatant unit)
        {
            ClearWarnings();
            if (sky != null) sky.Dissipate();
            if (warlord != null) warlord.HoldSpecials = null;
        }

        private void OnDisable()
        {
            // A level-up or menu pause preserves the wind-up. Death or the end of the battle cancels it.
            if (core == null || !core.IsAlive || (battle != null && battle.Objective.HasEnded)) ClearWarnings();
        }

        private void OnDestroy()
        {
            ClearWarnings();
            if (core == null) return;
            core.Damaged -= OnCoreDamaged;
            core.Died -= OnCoreDied;
        }
    }
}

using System.Collections.Generic;
using Lightbringer.Core;
using Lightbringer.Units;
using Lightbringer.Visuals;
using UnityEngine;

namespace Lightbringer.Combat
{
    public enum BossAttack { Crescent, Cleave, Lightning, Charge, Shockwave, Summon }

    // Six readable specials alternate with the existing melee AI. Telegraphs lock their aim at cast start.
    [DefaultExecutionOrder(-60)]
    public sealed class BossCombat : MonoBehaviour
    {
        private PrototypeBattle battle;
        private ArtStyleLibrary art;
        private Combatant self;
        private UnitCombat melee;
        private UnitPathFollower movement;
        private CharacterController body;
        private BossVfx warning;
        private float cooldown = 1.5f, timer, chargeRemaining;
        private float chargeVfxTimer;
        private int nextAttack;
        private bool followupCharge;
        private Vector3 origin, aim, direction;
        private readonly HashSet<Combatant> hit = new HashSet<Combatant>();
        public BossAttack CurrentAttack { get; private set; }
        public bool IsAttacking { get; private set; }
        public bool IsEnraged => Bereaved || (self != null && self.CurrentHealth <= self.MaximumHealth * 0.5f);
        // Final battle: no special starts while this returns true (the Demon King is casting), so big attacks alternate.
        public System.Func<bool> HoldSpecials { get; set; }
        // Final battle: the Demon King fell first; the Warlord fights on at the enraged pace without healing.
        public bool Bereaved { get; set; }
        // Casting, charging or about to chain the crescent into a charge.
        public bool IsBusy => IsAttacking || followupCharge;
        public int SpecialsResolved { get; private set; }
        public float Windup => CurrentAttack == BossAttack.Lightning ? 1.6f : CurrentAttack == BossAttack.Shockwave ? 1.5f : 1.25f;

        public void Configure(PrototypeBattle value, ArtStyleLibrary style)
        {
            battle = value; art = style;
            self = GetComponent<Combatant>();
            melee = GetComponent<UnitCombat>();
            movement = GetComponent<UnitPathFollower>();
            body = GetComponent<CharacterController>();
            // The Warlord hunts the commander instead of marching on the allied gate.
            melee.Quarry = battle.Hero;
        }

        // How far the hero may stand for a special to be worth casting; close strikes are skipped at range.
        private static float Reach(BossAttack attack) =>
            attack == BossAttack.Cleave ? 6 : attack == BossAttack.Shockwave ? 8 : attack == BossAttack.Charge ? 18
            : attack == BossAttack.Lightning ? 30 : attack == BossAttack.Crescent ? 35 : MaxReach;
        private const float MaxReach = 42;

        private void Update() => Tick(Time.deltaTime);

        public void Tick(float delta)
        {
            if (!isActiveAndEnabled || delta <= 0 || self == null || !self.IsAlive || self.Invulnerable
                || battle == null || battle.Objective.HasEnded || battle.Hero == null || !battle.Hero.IsAlive) return;
            if (chargeRemaining > 0)
            {
                Vector3 before = transform.position;
                float step = Mathf.Min(chargeRemaining, delta);
                body.Move(direction * (18f / 0.7f * step));
                if (Application.isPlaying)
                {
                    chargeVfxTimer -= step;
                    if (chargeVfxTimer <= 0)
                    {
                        // A burning furrow of scorched ground with violet sparks streaming off the boss.
                        chargeVfxTimer = .09f;
                        CombatEffects.Hit(art, transform.position + Vector3.up * .5f, -direction, BossStrikeVfx.Violet, 1.2f, true);
                        CombatEffects.DustRing(HeroSpellVfx.GroundBelow(transform.position), .7f, 4);
                        BossStrikeVfx.Spawn(art, battle.Root.transform, GroundPoint(transform.position), direction,
                            BossStrikeVfx.Kind.ChargeTrail, 1.7f);
                    }
                }
                DamageSegment(before, transform.position, 2.2f, 42);
                chargeRemaining -= step;
                if (chargeRemaining <= 0)
                {
                    Impact(transform.position, 3f, BossStrikeVfx.Kind.ChargeImpact);
                    FinishSpecial();
                }
                return;
            }
            if (IsAttacking)
            {
                timer += delta;
                if (warning != null) warning.SetProgress(timer / Windup);
                if (timer >= Windup) ResolveSpecial();
                return;
            }
            cooldown -= delta;
            if (cooldown > 0 || melee.IsStrikePending || (HoldSpecials != null && HoldSpecials())) return;
            Vector3 offset = battle.Hero.transform.position - transform.position; offset.y = 0;
            float distance = offset.magnitude;
            if (followupCharge)
            {
                followupCharge = false;
                if (distance <= Reach(BossAttack.Charge))
                {
                    BeginSpecial(BossAttack.Charge);
                    return;
                }
            }
            // Keep the rotation order, but skip strikes that cannot reach the hero so the boss never swings at air.
            for (int i = 0; i < 6; i++)
            {
                int attack = (nextAttack + i) % 6;
                if (distance > Reach((BossAttack)attack)) continue;
                BeginSpecial((BossAttack)attack);
                nextAttack = (attack + 1) % 6;
                return;
            }
        }

        private void BeginSpecial(BossAttack attack)
        {
            CurrentAttack = attack;
            IsAttacking = true; timer = 0; hit.Clear();
            origin = transform.position;
            aim = battle.Hero.transform.position;
            aim.y = origin.y;
            direction = aim - origin; direction.y = 0;
            direction = direction.sqrMagnitude < 0.01f ? transform.forward : direction.normalized;
            transform.rotation = Quaternion.LookRotation(direction);
            melee.enabled = false;
            movement.enabled = false;
            if (!Application.isPlaying) return;
            Lightbringer.Audio.SoundEffects.World(Lightbringer.Audio.SoundCue.BossWarning, origin);
            BossVfx.Shape shape = attack == BossAttack.Cleave ? BossVfx.Shape.WarningCone
                : attack == BossAttack.Charge || attack == BossAttack.Crescent ? BossVfx.Shape.WarningLane : BossVfx.Shape.WarningCircle;
            float radius = attack == BossAttack.Lightning ? 3.2f : attack == BossAttack.Shockwave ? 8
                : attack == BossAttack.Charge ? 18 : attack == BossAttack.Crescent ? 35 : 6;
            Vector3 point = attack == BossAttack.Lightning ? aim : origin;
            point.y = battle.Root.transform.position.y + 0.08f;
            warning = BossVfx.Spawn(battle.Root.transform, point, shape, radius, -1, art);
            if (warning != null)
            {
                warning.transform.rotation = Quaternion.LookRotation(direction);
                if (attack == BossAttack.Lightning) warning.AddSkyLance(18, .45f);
            }
            foreach (ModelAnimationDriver driver in GetComponentsInChildren<ModelAnimationDriver>())
                driver.PlayAction(Windup / Mathf.Max(0.1f, EnemyCatalog.StrikePhase(EnemyKind.Boss)));
        }

        private void ResolveSpecial()
        {
            ClearWarning();
            SpecialsResolved++;
            // The summon is also a sword slammed into the ground, so it gets the same impact thud as the other strikes.
            Lightbringer.Audio.SoundEffects.World(Lightbringer.Audio.SoundCue.BossImpact,
                CurrentAttack == BossAttack.Lightning ? aim : origin);
            switch (CurrentAttack)
            {
                case BossAttack.Crescent:
                    DarkCrescent.Launch(battle, self, origin + Vector3.up * 0.5f, direction, art);
                    break;
                case BossAttack.Cleave:
                    DamageArea(origin, 6, 45, true);
                    Impact(origin, 6, BossStrikeVfx.Kind.Cleave);
                    break;
                case BossAttack.Lightning:
                    DamageArea(aim, 3.2f, 36);
                    Impact(aim, 3.2f, BossStrikeVfx.Kind.Lightning);
                    break;
                case BossAttack.Shockwave:
                    DamageArea(origin, 8, 32);
                    Impact(origin, 8, BossStrikeVfx.Kind.Shockwave);
                    break;
                case BossAttack.Charge:
                    chargeRemaining = 0.7f;
                    chargeVfxTimer = 0;
                    Impact(origin, 2.5f, BossStrikeVfx.Kind.ChargeImpact);
                    return;
                case BossAttack.Summon:
                    if (battle.Waves.SummonBossGuard() > 0)
                        Lightbringer.Audio.SoundEffects.World(Lightbringer.Audio.SoundCue.BossSummon, origin);
                    Impact(origin, 6, BossStrikeVfx.Kind.Summon);
                    break;
            }
            FinishSpecial();
        }

        private void Impact(Vector3 point, float radius, BossStrikeVfx.Kind kind)
        {
            if (!Application.isPlaying) return;
            point = GroundPoint(point);
            BossStrikeVfx.Spawn(art, battle.Root.transform, point, direction, kind, radius);
            if (kind == BossStrikeVfx.Kind.Summon)
            {
                BossVfx portal = BossVfx.Spawn(battle.Root.transform, point, BossVfx.Shape.Portal, radius, 0.65f);
                if (portal != null) portal.transform.rotation = Quaternion.LookRotation(direction);
            }
            // The cleave lands in front of the boss; every other strike is centred on its point.
            Vector3 centre = kind == BossStrikeVfx.Kind.Cleave ? point + direction * radius * .5f : point;
            CombatEffects.Burst(art, centre + Vector3.up * .25f, BossStrikeVfx.Violet, Mathf.Min(radius, 3.2f));
            CombatEffects.DustRing(centre, Mathf.Min(radius * .5f, 3), 12);
            bool heavy = kind == BossStrikeVfx.Kind.Lightning || kind == BossStrikeVfx.Kind.Shockwave;
            CameraSystem.ThirdPersonCamera.Shake(point, heavy ? 0.6f : 0.45f);
        }

        private Vector3 GroundPoint(Vector3 point)
        {
            point.y = battle.Root.transform.position.y + 0.02f;
            return point;
        }

        private void DamageArea(Vector3 point, float radius, float damage, bool cone = false)
        {
            foreach (Combatant target in battle.Root.GetComponentsInChildren<Combatant>())
            {
                if (!IsEnemyTarget(target)) continue;
                Vector3 offset = target.GetAimPoint(point) - point; offset.y = 0;
                if (offset.sqrMagnitude > radius * radius || (cone && offset.sqrMagnitude > 0.01f
                    && Vector3.Dot(offset.normalized, direction) < Mathf.Cos(55 * Mathf.Deg2Rad))) continue;
                target.TakeDamage(damage, self);
                if (battle.Objective.HasEnded) return;
            }
        }

        private void DamageSegment(Vector3 from, Vector3 to, float radius, float damage)
        {
            foreach (Combatant target in battle.Root.GetComponentsInChildren<Combatant>())
            {
                if (!IsEnemyTarget(target) || hit.Contains(target)) continue;
                if (DarkCrescent.DistanceToSegment(target.GetAimPoint(to), from, to) > radius) continue;
                hit.Add(target); target.TakeDamage(damage, self);
                if (battle.Objective.HasEnded) return;
            }
        }

        private bool IsEnemyTarget(Combatant target) => target != null && target.IsAlive && target.Faction != self.Faction;

        private void FinishSpecial()
        {
            IsAttacking = false; chargeRemaining = 0;
            // From the rematch on, the crescent links into a charge.
            followupCharge = battle.Stage != EnemyCatalog.BossStage && CurrentAttack == BossAttack.Crescent;
            // The follow-up retains its full warning and locks a fresh aim point.
            cooldown = followupCharge ? .65f : IsEnraged ? 1.6f : 2.5f;
            if (self != null && self.IsAlive && !battle.Objective.HasEnded)
            { melee.enabled = true; movement.enabled = true; }
        }

        private void ClearWarning()
        {
            if (warning != null) warning.Remove();
            warning = null;
        }

        private void OnDisable()
        {
            // A level-up or menu pause preserves wind-up. Death/victory cancels it.
            if (self == null || !self.IsAlive || (battle != null && battle.Objective.HasEnded))
            { ClearWarning(); IsAttacking = false; chargeRemaining = 0; }
        }
        private void OnDestroy() => ClearWarning();
    }
}

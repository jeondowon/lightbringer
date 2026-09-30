using Lightbringer.Units;
using Lightbringer.Aura;
using System.Collections.Generic;
using UnityEngine;

namespace Lightbringer.Combat
{
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Combatant), typeof(UnitPathFollower))]
    public sealed class UnitCombat : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float detectionRadius = 8f;
        [Tooltip("Distance between unit centres, not collider surfaces.")]
        [SerializeField, Min(0.1f)] private float attackRange = 1.25f;
        [SerializeField, Min(0f)] private float attackDamage = 10f;
        [SerializeField, Min(0.05f)] private float attackInterval = 0.8f;
        [SerializeField] private LayerMask detectionMask = ~0;
        [SerializeField] private LayerMask obstructionMask = ~0;

        public Combatant Target { get; private set; }
        public float BaseDamage => attackDamage;
        public float EffectiveDamage
        {
            get
            {
                float bonus = 0f;
                foreach (HeroAura aura in auras)
                    if (aura != null && aura.Affects(this))
                        bonus = Mathf.Max(bonus, aura.AttackBonus);
                return attackDamage * (1f + bonus);
            }
        }
        private readonly HashSet<HeroAura> auras = new HashSet<HeroAura>();

        // Repeated registration is harmless; overlapping hero auras use only the strongest bonus.
        public void AddAura(HeroAura aura)
        {
            if (aura != null)
                auras.Add(aura);
        }

        public void RemoveAura(HeroAura aura) => auras.Remove(aura);
        private Combatant self;
        private UnitPathFollower movement;
        private float cooldown;
        private readonly PhysicsQueryBuffer candidates = new PhysicsQueryBuffer();
        private readonly PhysicsQueryBuffer splashQuery = new PhysicsQueryBuffer();
        private float searchCooldown;
        public int TargetSearches { get; private set; }
        public Combatant Health => self != null ? self : GetComponent<Combatant>();
        private float splashRadius;
        private readonly HashSet<Combatant> splashTargets = new HashSet<Combatant>();
        public void ConfigureSplash(float radius) => splashRadius = Mathf.Max(0, radius);

        public void Configure(float damage, float range, float interval)
        {
            attackDamage = Mathf.Max(0f, damage);
            attackRange = Mathf.Max(0.1f, range);
            attackInterval = Mathf.Max(0.05f, interval);
            detectionRadius = Mathf.Max(attackRange + 2f, detectionRadius);
        }

        private void Awake()
        {
            self = GetComponent<Combatant>();
            movement = GetComponent<UnitPathFollower>();
        }

        private void OnDisable()
        {
            Target = null;
            auras.Clear();
            if (movement != null)
                movement.ClearSteeringOverride();
        }

        private void Update() => Tick(Time.deltaTime);

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f || !isActiveAndEnabled)
                return;
            if (self == null || movement == null)
                Awake();
            if (!self.IsAlive)
                return;
            cooldown = Mathf.Max(0f, cooldown - deltaTime);
            searchCooldown -= deltaTime;
            if (!IsValidTarget(Target))
            {
                bool lostTarget = Target != null;
                Target = null;
                if (lostTarget || searchCooldown <= 0f)
                {
                    Target = FindTarget();
                    // Stagger per-unit scans while retaining immediate reacquisition after a lost target.
                    searchCooldown = 0.1f + (GetInstanceID() & 7) * 0.005f;
                }
            }
            if (Target == null)
            {
                movement.ClearSteeringOverride();
                return;
            }

            Vector3 aimPoint = Target.GetAimPoint(transform.position);
            movement.SetSteeringOverride(aimPoint, attackRange * 0.9f);
            if ((aimPoint - transform.position).sqrMagnitude > attackRange * attackRange
                || cooldown > 0f || !HasLineOfSight(Target))
                return;

            Combatant primary = Target;
            Vector3 impact = primary.transform.position;
            float damage = EffectiveDamage;
            primary.TakeDamage(damage, self);
            if (splashRadius > 0 && isActiveAndEnabled)
            {
                splashTargets.Clear();
                int count = splashQuery.Overlap(impact, splashRadius, detectionMask);
                for (int i = 0; i < count; i++)
                {
                    Combatant other = splashQuery.Items[i].GetComponentInParent<Combatant>();
                    if (other != primary && IsValidTarget(other) && splashTargets.Add(other) && HasLineOfSight(other))
                        other.TakeDamage(damage, self);
                }
            }
            cooldown = attackInterval;
            // A death listener can end the stage and disable this component during TakeDamage.
            if (Target == null || !Target.IsAlive)
            {
                Target = null;
                movement.ClearSteeringOverride();
            }
        }

        private bool IsValidTarget(Combatant candidate)
        {
            return candidate != null && candidate != self && candidate.IsAlive
                && candidate.Faction != self.Faction && candidate.gameObject.scene == gameObject.scene
                && (candidate.GetAimPoint(transform.position) - transform.position).sqrMagnitude <= detectionRadius * detectionRadius;
        }

        private Combatant FindTarget()
        {
            TargetSearches++;
            int count = candidates.Overlap(transform.position, detectionRadius, detectionMask);
            Combatant nearest = null;
            float nearestDistance = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                Combatant candidate = candidates.Items[i].GetComponentInParent<Combatant>();
                if (!IsValidTarget(candidate))
                    continue;
                float distance = (candidate.GetAimPoint(transform.position) - transform.position).sqrMagnitude;
                if (distance < nearestDistance && HasLineOfSight(candidate))
                {
                    nearest = candidate;
                    nearestDistance = distance;
                }
            }
            return nearest;
        }

        private bool HasLineOfSight(Combatant candidate)
        {
            Vector3 offset = candidate.GetAimPoint(transform.position) - transform.position;
            if (offset.sqrMagnitude < 0.0001f)
                return true;
            // Rays originate inside this unit's controller, which does not block its own ray.
            return !Physics.Raycast(transform.position, offset.normalized, out RaycastHit hit,
                    offset.magnitude, obstructionMask, QueryTriggerInteraction.Ignore)
                || hit.collider.GetComponentInParent<Combatant>() == candidate;
        }

        private void OnValidate()
        {
            attackRange = Mathf.Max(0.1f, attackRange);
            detectionRadius = Mathf.Max(attackRange, detectionRadius);
            attackDamage = Mathf.Max(0f, attackDamage);
            attackInterval = Mathf.Max(0.05f, attackInterval);
        }
    }
}

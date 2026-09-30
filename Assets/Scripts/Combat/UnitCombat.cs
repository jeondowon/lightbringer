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
        private readonly Collider[] candidates = new Collider[32];

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

        private void Tick(float deltaTime)
        {
            if (deltaTime <= 0f || !isActiveAndEnabled)
                return;
            if (self == null || movement == null)
                Awake();
            if (!self.IsAlive)
                return;
            cooldown = Mathf.Max(0f, cooldown - deltaTime);
            if (!IsValidTarget(Target))
                Target = FindTarget();
            if (Target == null)
            {
                movement.ClearSteeringOverride();
                return;
            }

            movement.SetSteeringOverride(Target.transform.position, attackRange * 0.9f);
            if ((Target.transform.position - transform.position).sqrMagnitude > attackRange * attackRange
                || cooldown > 0f || !HasLineOfSight(Target))
                return;

            Target.TakeDamage(EffectiveDamage);
            cooldown = attackInterval;
            if (!Target.IsAlive)
            {
                Target = null;
                movement.ClearSteeringOverride();
            }
        }

        private bool IsValidTarget(Combatant candidate)
        {
            return candidate != null && candidate != self && candidate.IsAlive
                && candidate.Faction != self.Faction && candidate.gameObject.scene == gameObject.scene
                && (candidate.transform.position - transform.position).sqrMagnitude <= detectionRadius * detectionRadius;
        }

        private Combatant FindTarget()
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, detectionRadius,
                candidates, detectionMask, QueryTriggerInteraction.Ignore);
            // Preserve correctness if more than 32 colliders surround this prototype unit.
            Collider[] hits = count == candidates.Length
                ? Physics.OverlapSphere(transform.position, detectionRadius, detectionMask, QueryTriggerInteraction.Ignore)
                : candidates;
            if (hits != candidates)
                count = hits.Length;
            Combatant nearest = null;
            float nearestDistance = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                Combatant candidate = hits[i].GetComponentInParent<Combatant>();
                if (!IsValidTarget(candidate))
                    continue;
                float distance = (candidate.transform.position - transform.position).sqrMagnitude;
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
            Vector3 offset = candidate.transform.position - transform.position;
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

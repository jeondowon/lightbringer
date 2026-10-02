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
        public float AttackRange => attackRange;
        public float BaseDamage => attackDamage;
        public float AttackInterval => attackInterval;
        public event System.Action Attacked;
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
        // Damage multiplier against Heavy targets (Spearman: 2x). 1 = no bonus.
        public float HeavyMultiplier { get; private set; } = 1f;
        public void ConfigureHeavyBonus(float multiplier) => HeavyMultiplier = Mathf.Max(1f, multiplier);
        private float DamageAgainst(Combatant target, float damage) => target.IsHeavy ? damage * HeavyMultiplier : damage;

        // Charge (Knight): ground covered since the last attack arms a stronger first strike.
        private float chargeDistance;
        private float chargeMultiplier = 1f;
        private float chargeRadius;
        private float chargeKnockback;
        private float travelled;
        private Vector3 lastPosition;
        private bool hasLastPosition;
        public bool CanCharge => chargeMultiplier > 1f;
        public bool IsChargeReady => CanCharge && travelled >= chargeDistance;
        public event System.Action ChargeLanded;

        public void ConfigureCharge(float distance, float multiplier, float radius, float knockback)
        {
            chargeDistance = Mathf.Max(0f, distance);
            chargeMultiplier = Mathf.Max(1f, multiplier);
            chargeRadius = Mathf.Max(0f, radius);
            chargeKnockback = Mathf.Max(0f, knockback);
        }

        // Heavy enemies and objectives (no CharacterController) stand their ground.
        private void Knockback(Combatant target, Vector3 from)
        {
            if (chargeKnockback <= 0f || target == null || !target.IsAlive || target.IsHeavy
                || !target.TryGetComponent(out CharacterController body) || !body.enabled)
                return;
            Vector3 push = target.transform.position - from;
            push.y = 0f;
            if (push.sqrMagnitude < 0.0001f) push = transform.forward;
            body.Move(push.normalized * chargeKnockback);
        }

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
            if (CanCharge)
            {
                Vector3 position = transform.position;
                if (hasLastPosition)
                {
                    Vector3 moved = position - lastPosition;
                    moved.y = 0f;
                    travelled = Mathf.Min(chargeDistance, travelled + moved.magnitude);
                }
                lastPosition = position;
                hasLastPosition = true;
            }
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
            // Steering stops on the ground plane, so account for height (flying targets) when choosing
            // the stop distance; otherwise a unit can halt just outside its 3D attack range forever.
            float reach = attackRange * 0.9f;
            float height = aimPoint.y - transform.position.y;
            movement.SetSteeringOverride(aimPoint, Mathf.Sqrt(Mathf.Max(0.01f, reach * reach - height * height)));
            // Simple rule: an enemy ahead is approached and attacked; with no enemy the unit keeps advancing.
            if ((aimPoint - transform.position).sqrMagnitude > attackRange * attackRange
                || cooldown > 0f || !HasLineOfSight(Target))
                return;

            Combatant primary = Target;
            Vector3 impact = primary.transform.position;
            float damage = EffectiveDamage;
            bool charging = IsChargeReady;
            travelled = 0f;
            primary.TakeDamage(DamageAgainst(primary, charging ? damage * chargeMultiplier : damage), self);
            if (charging) Knockback(primary, transform.position);
            float radius = charging ? Mathf.Max(splashRadius, chargeRadius) : splashRadius;
            if (radius > 0 && isActiveAndEnabled)
            {
                splashTargets.Clear();
                int count = splashQuery.Overlap(impact, radius, detectionMask);
                for (int i = 0; i < count; i++)
                {
                    Combatant other = splashQuery.Items[i].GetComponentInParent<Combatant>();
                    if (other != primary && IsValidTarget(other) && splashTargets.Add(other) && HasLineOfSight(other))
                    {
                        other.TakeDamage(DamageAgainst(other, damage), self);
                        if (charging) Knockback(other, impact);
                    }
                }
            }
            cooldown = attackInterval;
            Attacked?.Invoke();
            if (charging) ChargeLanded?.Invoke();
            // A death listener can end the stage and disable this component during TakeDamage.
            if (Target == null || !Target.IsAlive)
            {
                Target = null;
                movement.ClearSteeringOverride();
            }
        }

        private bool IsValidTarget(Combatant candidate)
        {
            if (candidate == null || candidate == self || !candidate.IsAlive || candidate.Invulnerable
                || candidate.Faction == self.Faction || candidate.gameObject.scene != gameObject.scene)
                return false;
            Vector3 offset = candidate.GetAimPoint(transform.position) - transform.position;
            // Units cannot strike targets higher above them than their reach (melee vs the flying Dragon).
            return Mathf.Abs(offset.y) < attackRange * 0.95f && offset.sqrMagnitude <= detectionRadius * detectionRadius;
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

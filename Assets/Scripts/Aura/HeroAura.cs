using System.Collections.Generic;
using Lightbringer.Combat;
using UnityEngine;

namespace Lightbringer.Aura
{
    [DefaultExecutionOrder(-75)]
    [DisallowMultipleComponent]
    public sealed class HeroAura : MonoBehaviour
    {
        // The radius is the visible rune circle; buffs reach exactly as far as it is drawn.
        [SerializeField, Min(0f)] private float radius = 3f;
        [Tooltip("0.25 means +25% attack damage. This does not change the base damage.")]
        [SerializeField, Min(0f)] private float attackBonus = 0.25f;
        [SerializeField] private LayerMask unitMask = ~0;

        public float Radius => radius;
        public float AttackBonus => attackBonus;
        public void Configure(float newRadius, float bonus)
        {
            radius = Mathf.Max(0f, newRadius);
            attackBonus = Mathf.Max(0f, bonus);
        }
        private readonly PhysicsQueryBuffer candidates = new PhysicsQueryBuffer(64);
        private float scanCooldown;
        private HashSet<UnitCombat> affected = new HashSet<UnitCombat>();
        private HashSet<UnitCombat> detected = new HashSet<UnitCombat>();

        private static readonly List<HeroAura> active = new List<HeroAura>();

        public bool Affects(UnitCombat unit)
        {
            if (unit == null || !unit.isActiveAndEnabled) return false;
            return Covers(unit.Health);
        }

        // Any living ally whose pivot stands inside the ground circle, including non-fighters such as the Priest.
        public bool Covers(Combatant ally)
        {
            if (!isActiveAndEnabled || radius <= 0f || ally == null || !ally.IsAlive || ally.Faction != Faction.Allied
                || ally.gameObject.scene != gameObject.scene)
                return false;
            // Ground-plane distance (the hero and troop pivots sit at different heights above the circle) inside a
            // cylinder as tall as the radius, so nothing far above or below is reached.
            Vector3 offset = ally.transform.position - transform.position;
            if (Mathf.Abs(offset.y) > radius) return false;
            offset.y = 0f;
            return offset.sqrMagnitude <= radius * radius;
        }

        public static bool AnyCovers(Combatant ally)
        {
            foreach (HeroAura aura in active)
                if (aura != null && aura.Covers(ally)) return true;
            return false;
        }

        private void OnEnable()
        {
            if (!active.Contains(this)) active.Add(this);
        }

        private void Update()
        {
            scanCooldown -= Time.deltaTime;
            if (scanCooldown > 0) return;
            scanCooldown = 0.1f;
            RefreshRecipients();
        }

        private void RefreshRecipients()
        {
            if (!isActiveAndEnabled)
                return;
            detected.Clear();
            int count = candidates.Overlap(transform.position, radius, unitMask);
            for (int i = 0; i < count; i++)
            {
                UnitCombat unit = candidates.Items[i].GetComponentInParent<UnitCombat>();
                if (Affects(unit) && detected.Add(unit))
                    unit.AddAura(this);
            }
            foreach (UnitCombat unit in affected)
                if (unit != null && !detected.Contains(unit))
                    unit.RemoveAura(this);
            HashSet<UnitCombat> previous = affected;
            affected = detected;
            detected = previous;
        }

        private void OnDisable()
        {
            active.Remove(this);
            foreach (UnitCombat unit in affected)
                if (unit != null)
                    unit.RemoveAura(this);
            affected.Clear();
            detected.Clear();
        }

        private void OnValidate()
        {
            radius = Mathf.Max(0f, radius);
            attackBonus = Mathf.Max(0f, attackBonus);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}

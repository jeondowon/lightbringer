using System.Collections.Generic;
using Lightbringer.Combat;
using UnityEngine;

namespace Lightbringer.Aura
{
    [DefaultExecutionOrder(-75)]
    [DisallowMultipleComponent]
    public sealed class HeroAura : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float radius = 6f;
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

        public bool Affects(UnitCombat unit)
        {
            if (!isActiveAndEnabled || radius <= 0f || unit == null || !unit.isActiveAndEnabled
                || unit.gameObject.scene != gameObject.scene)
                return false;
            Combatant health = unit.Health;
            return health != null && health.IsAlive && health.Faction == Faction.Allied
                && (unit.transform.position - transform.position).sqrMagnitude <= radius * radius;
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

using System.Collections.Generic;
using Lightbringer.Combat;
using UnityEngine;

namespace Lightbringer.Units
{
    // Periodic heal pulse for same-faction units nearby (allied Priest, enemy Shaman).
    [DisallowMultipleComponent]
    public sealed class UnitSupport : MonoBehaviour
    {
        private float cooldown;
        private float amount = 10f;
        private float interval = 1.5f;
        private float radius = 6f;
        private Combatant self;
        private readonly HashSet<Combatant> healed = new HashSet<Combatant>();
        private readonly PhysicsQueryBuffer query = new PhysicsQueryBuffer();
        public float HealAmount => amount;
        public float Interval => interval;
        // Raised after a pulse that restored health to at least one wounded unit (drives the cast animation).
        public event System.Action Pulsed;

        public void Configure(float healAmount, float pulseInterval, float pulseRadius)
        {
            amount = Mathf.Max(0f, healAmount);
            interval = Mathf.Max(0.1f, pulseInterval);
            radius = Mathf.Max(0.5f, pulseRadius);
        }

        private void Update() => Tick(Time.deltaTime);
        public void Tick(float delta)
        {
            if (!isActiveAndEnabled || delta <= 0) return;
            if (self == null) self = GetComponent<Combatant>();
            if (self == null || !self.IsAlive) return;
            cooldown -= delta;
            if (cooldown > 0) return;
            cooldown = interval;
            healed.Clear();
            bool restored = false;
            int count = query.Overlap(transform.position, radius);
            for (int i = 0; i < count; i++)
            {
                Combatant unit = query.Items[i].GetComponentInParent<Combatant>();
                if (unit != null && unit.gameObject.scene == gameObject.scene && unit.Faction == self.Faction && healed.Add(unit))
                {
                    bool wounded = unit.CurrentHealth < unit.MaximumHealth;
                    restored |= unit.Heal(amount) && wounded;
                }
            }
            if (restored) Pulsed?.Invoke();
        }
    }
}

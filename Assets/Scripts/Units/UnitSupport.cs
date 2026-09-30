using System.Collections.Generic;
using Lightbringer.Combat;
using UnityEngine;

namespace Lightbringer.Units
{
    [DisallowMultipleComponent]
    public sealed class UnitSupport : MonoBehaviour
    {
        private float cooldown;
        private Combatant self;
        private readonly HashSet<Combatant> healed = new HashSet<Combatant>();
        private readonly PhysicsQueryBuffer query = new PhysicsQueryBuffer();
        private void Update() => Tick(Time.deltaTime);
        public void Tick(float delta)
        {
            if (!isActiveAndEnabled || delta <= 0) return;
            if (self == null) self = GetComponent<Combatant>();
            if (self == null || !self.IsAlive) return;
            cooldown -= delta;
            if (cooldown > 0) return;
            cooldown = 1.5f;
            healed.Clear();
            int count = query.Overlap(transform.position, 6f);
            for (int i = 0; i < count; i++)
            {
                Combatant unit = query.Items[i].GetComponentInParent<Combatant>();
                if (unit != null && unit.gameObject.scene == gameObject.scene && unit.Faction == self.Faction && healed.Add(unit))
                    unit.Heal(10);
            }
        }
    }
}

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

        // Rear guard (allied Priest, which has no UnitCombat to halt it): wait until a fighter on the same Path
        // is this far ahead, and stop short of enemies, instead of walking the Path into the enemy line.
        private float followGap;
        private float keepAway;
        private float positionTimer;
        private UnitPathFollower movement;
        private readonly PhysicsQueryBuffer threats = new PhysicsQueryBuffer();
        public bool IsHolding { get; private set; }

        public void Configure(float healAmount, float pulseInterval, float pulseRadius)
        {
            amount = Mathf.Max(0f, healAmount);
            interval = Mathf.Max(0.1f, pulseInterval);
            radius = Mathf.Max(0.5f, pulseRadius);
        }

        public void ConfigureRearGuard(float gapBehindFront, float enemyDistance)
        {
            followGap = Mathf.Max(0f, gapBehindFront);
            keepAway = Mathf.Max(0f, enemyDistance);
        }

        private void OnDisable()
        {
            positionTimer = 0f;
            if (IsHolding && movement != null) movement.ClearSteeringOverride();
            IsHolding = false;
        }

        private void Update() => Tick(Time.deltaTime);
        public void Tick(float delta)
        {
            if (!isActiveAndEnabled || delta <= 0) return;
            if (self == null) self = GetComponent<Combatant>();
            if (self == null || !self.IsAlive) return;
            if (followGap > 0f || keepAway > 0f) UpdatePosition(delta);
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

        private void UpdatePosition(float delta)
        {
            positionTimer -= delta;
            if (positionTimer > 0f) return;
            positionTimer = 0.2f;
            if (movement == null) movement = GetComponent<UnitPathFollower>();
            if (movement == null || movement.AssignedPath == null || !movement.AssignedPath.IsValid) return;
            IsHolding = EnemyWithin(keepAway) || !FighterAhead(movement.AssignedPath);
            if (IsHolding) movement.SetSteeringOverride(transform.position, 0f);
            else movement.ClearSteeringOverride();
        }

        private bool EnemyWithin(float distance)
        {
            if (distance <= 0f) return false;
            int count = threats.Overlap(transform.position, distance);
            for (int i = 0; i < count; i++)
            {
                Combatant unit = threats.Items[i].GetComponentInParent<Combatant>();
                if (unit != null && unit.IsAlive && unit.Faction != self.Faction && unit.gameObject.scene == gameObject.scene)
                    return true;
            }
            return false;
        }

        // Fighters are the other troops deployed under the same parent on this Path (healers do not count).
        private bool FighterAhead(Pathing.WaypointPath path)
        {
            Transform group = transform.parent;
            if (group == null) return false;
            float mine = path.DistanceAlong(transform.position);
            for (int i = 0; i < group.childCount; i++)
            {
                Transform child = group.GetChild(i);
                if (child == transform || !child.gameObject.activeInHierarchy || child.TryGetComponent(out UnitSupport _)
                    || !child.TryGetComponent(out UnitPathFollower follower) || follower.AssignedPath != path
                    || !child.TryGetComponent(out Combatant unit) || !unit.IsAlive || unit.Faction != self.Faction)
                    continue;
                if (path.DistanceAlong(child.position) - followGap >= mine) return true;
            }
            return false;
        }
    }
}

using System.Collections.Generic;
using Lightbringer.Combat;
using Lightbringer.Core;
using UnityEngine;

namespace Lightbringer.UI
{
    public sealed class BattlefieldReadability : MonoBehaviour
    {
        private PrototypeBattle battle;
        private Combatant[] units = new Combatant[0];
        private float nextScan;
        private readonly List<Combatant> visible = new List<Combatant>();
        private int[] alliesPerPath;
        private int[] enemiesPerPath;

        // Roads mark the battlefield routes; selection remains in the HUD and minimap.
        public void Configure(PrototypeBattle value)
        {
            battle = value;
            alliesPerPath = new int[battle.Paths.Length];
            enemiesPerPath = new int[battle.Paths.Length];
        }

        // Per-front troop counts and damaged units (nearest first) for the HUD; refreshed four times a second.
        public int FrontCount => alliesPerPath != null ? alliesPerPath.Length : 0;
        public int AlliesOn(int front) => alliesPerPath[front];
        public int EnemiesOn(int front) => enemiesPerPath[front];
        public IReadOnlyList<Combatant> DamagedUnits => visible;
        // Every Combatant found by the last scan, including dead or destroyed ones; callers filter.
        public IReadOnlyList<Combatant> Units => units;

        private void Update()
        {
            if (battle == null || battle.Camera == null) return;
            if (Time.unscaledTime < nextScan) return;
            nextScan = Time.unscaledTime + 0.25f;
            Scan();
        }

        public void Scan()
        {
            if (battle == null || battle.Camera == null) return;
            units = battle.Root.GetComponentsInChildren<Combatant>();
            System.Array.Clear(alliesPerPath, 0, alliesPerPath.Length);
            System.Array.Clear(enemiesPerPath, 0, enemiesPerPath.Length);
            visible.Clear();
            foreach (Combatant unit in units)
            {
                // Hero and both strongholds have their own HUD bars.
                if (!unit.IsAlive || unit == battle.Hero || unit == battle.Objective.EnemyBase || unit == battle.AlliedBase) continue;
                var follower = unit.GetComponent<Units.UnitPathFollower>();
                if (follower != null && follower.AssignedPath != null)
                {
                    for (int i = 0; i < alliesPerPath.Length; i++)
                        if (follower.AssignedPath == battle.Paths[i]) alliesPerPath[i]++;
                    if (unit.Faction == Faction.Enemy)
                    {
                        int nearest = 0;
                        float distance = float.PositiveInfinity;
                        for (int i = 0; i < enemiesPerPath.Length; i++)
                        {
                            float candidate = Mathf.Abs(unit.transform.position.x - battle.Paths[i].GetPosition(1).x);
                            if (candidate < distance) { distance = candidate; nearest = i; }
                        }
                        enemiesPerPath[nearest]++;
                    }
                }
                if (unit.CurrentHealth < unit.MaximumHealth) visible.Add(unit);
            }
            Vector3 cameraPosition = battle.Camera.transform.position;
            visible.Sort((a, b) => (a.transform.position - cameraPosition).sqrMagnitude.CompareTo((b.transform.position - cameraPosition).sqrMagnitude));
        }

    }
}

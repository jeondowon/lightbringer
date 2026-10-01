using System;
using Lightbringer.Combat;
using Lightbringer.Pathing;
using Lightbringer.Units;
using UnityEngine;

namespace Lightbringer.Core
{
    public sealed class EnemyWaveSpawner : MonoBehaviour
    {
        private CharacterController template;
        private WaypointPath[] routes;
        private Transform parent;
        private int stage;
        private int waves;
        private float timer;
        public int WavesSpawned => waves;
        public int TotalWaves => 3 + stage / 2;
        public event Action<Combatant> Spawned;

        public void Configure(CharacterController unitTemplate, Transform unitsParent, WaypointPath[] paths, int stageNumber)
        { template = unitTemplate; parent = unitsParent; routes = paths; stage = stageNumber; }

        private void Update() => Tick(Time.deltaTime);
        public void Tick(float delta)
        {
            if (!isActiveAndEnabled || delta <= 0 || template == null || routes == null || waves >= TotalWaves) return;
            timer -= delta;
            if (timer > 0) return;
            timer = 10f;
            for (int lane = 0; lane < routes.Length; lane++)
                for (int i = 0; i < 2 + stage / 3; i++)
                {
                    Vector3 position = routes[lane].GetPosition(0) + new Vector3((i % 2) * 1.2f, 0.85f, -(i / 2) * 1.2f);
                    // Delay crowded spawns rather than stacking overlapping colliders.
                    if (Physics.CheckCapsule(position + Vector3.up * 0.4f, position - Vector3.up * 0.4f, 0.35f,
                        ~0, QueryTriggerInteraction.Ignore)) continue;
                    CharacterController unit = Instantiate(template, position, Quaternion.Euler(0, 180, 0), parent);
                    unit.name = stage >= 3 && i == 1 ? "Enemy Archer" : "Enemy Raider";
                    Combatant health = unit.GetComponent<Combatant>();
                    health.Configure(Faction.Enemy, 25 + stage * 5);
                    unit.GetComponent<UnitCombat>().Configure(3 + stage, stage >= 3 && i == 1 ? 6f : 1.25f, 1.2f);
                    unit.GetComponent<UnitPathFollower>().TryAssignPath(routes[lane]);
                    unit.GetComponent<UnitPathFollower>().ConfigureCrowdAvoidance(true);
                    if (unit.TryGetComponent(out Lightbringer.Visuals.UnitAppearance appearance))
                        appearance.Apply(stage >= 3 && i == 1 ? Lightbringer.Visuals.VisualId.EnemyArcher : Lightbringer.Visuals.VisualId.EnemyRaider);
                    Spawned?.Invoke(health);
                    unit.gameObject.SetActive(true);
                    Physics.SyncTransforms();
                }
            waves++;
        }
    }
}

using System;
using System.Collections.Generic;
using Lightbringer.Combat;
using Lightbringer.Pathing;
using Lightbringer.Units;
using Lightbringer.Visuals;
using UnityEngine;

namespace Lightbringer.Core
{
    // Queues one squad per enemy route every wave (composition from EnemyCatalog) and spawns it at the
    // route start. Units that do not fit yet stay queued and retry, so crowded gates delay rather than drop.
    public sealed class EnemyWaveSpawner : MonoBehaviour
    {
        private const float WaveInterval = 10f;
        private const float RetryInterval = 0.5f;
        private const float SlotSpacing = 1.3f;

        private CharacterController template;
        private WaypointPath[] routes;
        private Transform parent;
        private int stage;
        private int waves;
        private float timer;
        private float retry;
        private List<EnemyKind>[] pending = new List<EnemyKind>[0];
        private readonly Dictionary<Combatant, EnemyKind> kinds = new Dictionary<Combatant, EnemyKind>();

        public int WavesSpawned => waves;
        public int TotalWaves => 3 + stage / 2;
        public bool HasBoss => EnemyCatalog.HasBoss(stage);
        public Combatant Boss { get; private set; }
        public bool BossDefeated { get; private set; }
        public int PendingCount { get { int count = 0; foreach (List<EnemyKind> lane in pending) count += lane.Count; return count; } }
        public event Action<Combatant> Spawned;
        public event Action BossFell;

        public void Configure(CharacterController unitTemplate, Transform unitsParent, WaypointPath[] paths, int stageNumber)
        {
            template = unitTemplate; parent = unitsParent; routes = paths; stage = stageNumber;
            pending = new List<EnemyKind>[paths != null ? paths.Length : 0];
            for (int i = 0; i < pending.Length; i++) pending[i] = new List<EnemyKind>();
        }

        public bool TryGetKind(Combatant unit, out EnemyKind kind) => kinds.TryGetValue(unit, out kind);

        public int ExperienceFor(Combatant enemy) =>
            EnemyCatalog.ExperienceReward(TryGetKind(enemy, out EnemyKind kind) ? kind : EnemyKind.Raider, stage);

        private void Update() => Tick(Time.deltaTime);
        public void Tick(float delta)
        {
            if (!isActiveAndEnabled || delta <= 0 || template == null || routes == null) return;
            if (waves < TotalWaves)
            {
                timer -= delta;
                if (timer <= 0)
                {
                    timer = WaveInterval;
                    QueueWave();
                    waves++;
                    retry = 0f;
                }
            }
            retry -= delta;
            if (retry > 0f) return;
            retry = RetryInterval;
            for (int lane = 0; lane < routes.Length; lane++) SpawnPending(lane);
        }

        private void QueueWave()
        {
            for (int lane = 0; lane < routes.Length; lane++)
                pending[lane].AddRange(EnemyCatalog.Squad(stage, waves, lane));
            // The boss leads the final wave down the central route.
            if (HasBoss && waves == TotalWaves - 1)
                pending[routes.Length / 2].Insert(0, EnemyKind.Boss);
        }

        private void SpawnPending(int lane)
        {
            List<EnemyKind> queue = pending[lane];
            Vector3 start = routes[lane].GetPosition(0);
            while (queue.Count > 0)
            {
                EnemyKind kind = queue[0];
                if (!TryFindSlot(start, EnemyCatalog.Radius(kind), out Vector3 position)) return;
                queue.RemoveAt(0);
                Spawn(kind, lane, position);
            }
        }

        // Three columns by three rows behind the route start; the first free slot wins.
        private static bool TryFindSlot(Vector3 start, float radius, out Vector3 position)
        {
            // Probe the 1.6 m unit body while keeping the capsule clear of the ground, whatever the radius.
            float low = radius + 0.1f, high = Mathf.Max(low, 1.6f - radius);
            for (int i = 0; i < 9; i++)
            {
                Vector3 ground = start + new Vector3((i % 3 - 1) * SlotSpacing, 0f, (i / 3) * SlotSpacing);
                if (!Physics.CheckCapsule(ground + Vector3.up * low, ground + Vector3.up * high, radius,
                    ~0, QueryTriggerInteraction.Ignore))
                {
                    position = ground + Vector3.up * 0.85f;
                    return true;
                }
            }
            position = default;
            return false;
        }

        private void Spawn(EnemyKind kind, int lane, Vector3 position)
        {
            CharacterController unit = Instantiate(template, position, Quaternion.Euler(0, 180, 0), parent);
            unit.name = EnemyCatalog.Names[(int)kind];
            unit.radius = EnemyCatalog.Radius(kind);
            Combatant health = unit.GetComponent<Combatant>();
            health.Configure(Faction.Enemy, EnemyCatalog.MaxHealth(kind, stage));
            health.IsHeavy = EnemyCatalog.IsHeavy(kind);
            UnitCombat combat = unit.GetComponent<UnitCombat>();
            combat.Configure(EnemyCatalog.AttackDamage(kind, stage), EnemyCatalog.Range(kind), EnemyCatalog.AttackInterval(kind));
            combat.ConfigureSplash(EnemyCatalog.SplashRadius(kind));
            UnitPathFollower follower = unit.GetComponent<UnitPathFollower>();
            follower.ConfigureSpeed(EnemyCatalog.Speed(kind));
            follower.TryAssignPath(routes[lane]);
            follower.ConfigureCrowdAvoidance(true);
            if (kind == EnemyKind.Shaman)
                unit.gameObject.AddComponent<UnitSupport>().Configure(EnemyCatalog.HealAmount(stage), EnemyCatalog.HealInterval, EnemyCatalog.HealRadius);
            if (!unit.TryGetComponent(out UnitAppearance appearance) || !appearance.Apply(ArtStyleLibrary.ForEnemy(kind)))
                ScaleGreybox(unit.transform, kind);
            kinds[health] = kind;
            if (kind == EnemyKind.Boss)
            {
                Boss = health;
                health.Died += OnBossDied;
            }
            Spawned?.Invoke(health);
            unit.gameObject.SetActive(true);
            Physics.SyncTransforms();
        }

        // Greybox capsules read size by role; the capsule is lifted so it still stands on the ground.
        private static void ScaleGreybox(Transform unit, EnemyKind kind)
        {
            Transform visual = unit.Find(UnitAppearance.GreyboxName);
            if (visual == null) return;
            Vector3 scale = kind == EnemyKind.Swarm ? new Vector3(0.5f, 0.55f, 0.5f)
                : kind == EnemyKind.Brute ? new Vector3(1.05f, 1.05f, 1.05f)
                : kind == EnemyKind.Boss ? new Vector3(1.6f, 1.5f, 1.6f)
                : visual.localScale;
            visual.localScale = scale;
            visual.localPosition = new Vector3(0f, scale.y - 0.8f, 0f);
        }

        private void OnBossDied(Combatant boss)
        {
            boss.Died -= OnBossDied;
            if (BossDefeated) return;
            BossDefeated = true;
            BossFell?.Invoke();
        }
    }
}

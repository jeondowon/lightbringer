using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Lightbringer.CameraSystem;
using Lightbringer.Combat;
using Lightbringer.Units;
using UnityEngine;

namespace Lightbringer.EditorTools
{
    public static partial class GreyboxValidation
    {
        private static void ValidateCombatScale()
        {
            List<string> report = new List<string> {
                "Unity Editor component/physics benchmark | " + DateTime.UtcNow.ToString("O"),
                "120 simulated 1/60-second steps after warmup. No rendering, assets, animation, or shipping-build frame-rate claim.",
                "Hardware: " + SystemInfo.processorType + " | " + SystemInfo.graphicsDeviceName };
            string directory = Path.Combine(Application.dataPath, "../Docs/Validation");
            Directory.CreateDirectory(directory);
            try
            {
                BenchmarkBattle(100, report);
                BenchmarkBattle(300, report);
                ValidateCameraAndSeparation();
            }
            finally { File.WriteAllLines(Path.Combine(directory, "CombatScaleChecks.txt"), report); }
        }

        private static void BenchmarkBattle(int count, List<string> report)
        {
            GameObject root = new GameObject("Scale validation " + count);
            Vector3 origin = new Vector3(9000, 0.85f, 9000);
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.SetParent(root.transform);
            ground.transform.position = origin - Vector3.up * 0.85f;
            ground.transform.localScale = Vector3.one * 12;
            UnitCombat[] combat = new UnitCombat[count];
            UnitPathFollower[] movement = new UnitPathFollower[count];
            try
            {
                for (int i = 0; i < count; i++)
                {
                    bool allied = i < count / 2;
                    int index = i % (count / 2);
                    Vector3 position = origin + new Vector3((index % 15 - 7) * 1.1f, 0, (allied ? -1 : 1) * (2 + index / 15 * 1.1f));
                    Combatant unit = CreateCombatUnit(position, allied ? Faction.Allied : Faction.Enemy);
                    unit.transform.SetParent(root.transform);
                    unit.Configure(allied ? Faction.Allied : Faction.Enemy, 100000);
                    combat[i] = unit.GetComponent<UnitCombat>();
                    movement[i] = unit.GetComponent<UnitPathFollower>();
                    movement[i].ConfigureCrowdAvoidance(true);
                }
                Physics.SyncTransforms();
                PhysicsQueryBuffer query = new PhysicsQueryBuffer(8);
                Check(query.Overlap(origin, 45) >= count, count + "-unit broad-phase query grows without losing crowded targets");
                Collider[] buffer = query.Items;
                query.Overlap(origin, 45);
                Check(ReferenceEquals(buffer, query.Items), "Repeated " + count + "-unit query reuses its saturated buffer");
                for (int warmup = 0; warmup < 15; warmup++) StepBattle(combat, movement);
                int searches = combat.Sum(unit => unit.TargetSearches);
                long allocationsBefore = GC.GetAllocatedBytesForCurrentThread();
                double[] milliseconds = new double[120];
                Stopwatch stopwatch = new Stopwatch();
                double combatTime = 0, movementTime = 0;
                for (int frame = 0; frame < milliseconds.Length; frame++)
                {
                    stopwatch.Restart();
                    Physics.SyncTransforms();
                    foreach (UnitCombat unit in combat) unit.Tick(1f / 60f);
                    double combatEnd = stopwatch.Elapsed.TotalMilliseconds;
                    foreach (UnitPathFollower unit in movement) unit.Tick(1f / 60f);
                    combatTime += combatEnd;
                    movementTime += stopwatch.Elapsed.TotalMilliseconds - combatEnd;
                    stopwatch.Stop();
                    milliseconds[frame] = stopwatch.Elapsed.TotalMilliseconds;
                }
                long bytes = GC.GetAllocatedBytesForCurrentThread() - allocationsBefore;
                int scans = combat.Sum(unit => unit.TargetSearches) - searches;
                Array.Sort(milliseconds);
                double average = milliseconds.Average();
                double p95 = milliseconds[(int)(milliseconds.Length * 0.95f)];
                report.Add($"{count} units: mean {average:F3} ms/step, p95 {p95:F3} ms/step; managed bytes {bytes}; target scans {scans} vs {count * 120} unthrottled opportunities.");
                report.Add($"Mean combat {combatTime / 120:F3} ms, movement {movementTime / 120:F3} ms. Auto sync transforms: {Physics.autoSyncTransforms}.");
                Check(scans < count * 25, count + "-unit target scans are throttled substantially below per-frame polling");
                Check(combat.All(unit => unit.Health.IsAlive) && combat.Any(unit => unit.Health.CurrentHealth < 100000),
                    count + "-unit simulation exchanges damage without invalid/dead-target state");
                Check(p95 < 16.67, count + "-unit prototype simulation stays below 16.67 ms p95 on this Editor run");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); Physics.SyncTransforms(); }
        }

        private static void StepBattle(UnitCombat[] combat, UnitPathFollower[] movement)
        {
            Physics.SyncTransforms();
            foreach (UnitCombat unit in combat) unit.Tick(1f / 60f);
            foreach (UnitPathFollower unit in movement) unit.Tick(1f / 60f);
        }

        private static void ValidateCameraAndSeparation()
        {
            Vector3 origin = new Vector3(9500, 1, 9500);
            Combatant left = CreateCombatUnit(origin - Vector3.right * 0.4f, Faction.Allied);
            Combatant right = CreateCombatUnit(origin + Vector3.right * 0.4f, Faction.Allied);
            UnitPathFollower a = left.GetComponent<UnitPathFollower>();
            UnitPathFollower b = right.GetComponent<UnitPathFollower>();
            a.ConfigureCrowdAvoidance(true); b.ConfigureCrowdAvoidance(true);
            a.SetSteeringOverride(origin + Vector3.forward * 5, 0);
            b.SetSteeringOverride(origin + Vector3.forward * 5, 0);
            Physics.SyncTransforms();
            a.Tick(0.1f); b.Tick(0.1f);
            Check(right.transform.position.x - left.transform.position.x > 0.8f,
                "Local crowd avoidance separates neighbours while they advance automatically");
            GameObject cameraObject = new GameObject("Collision Camera");
            cameraObject.AddComponent<UnityEngine.Camera>();
            ThirdPersonCamera camera = cameraObject.AddComponent<ThirdPersonCamera>();
            camera.Configure(left.transform, null);
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = left.transform.position + Vector3.up + Vector3.back * 4;
            wall.transform.localScale = new Vector3(5, 5, 1);
            Physics.SyncTransforms();
            Invoke(camera, "UpdatePose");
            Check(Vector3.Distance(camera.transform.position, left.transform.position + Vector3.up) < 4,
                "Third-person camera pulls in before an obstructing wall");
            wall.SetActive(false);
            Physics.SyncTransforms();
            Invoke(camera, "UpdatePose");
            Check(Mathf.Abs(Vector3.Distance(camera.transform.position, left.transform.position + Vector3.up) - 9) < 0.01f,
                "Camera restores its chosen distance once the obstruction clears");
            UnityEngine.Object.DestroyImmediate(cameraObject);
        }
    }
}

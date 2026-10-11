using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Lightbringer.Combat;
using Lightbringer.Core;
using Lightbringer.Progression;
using Lightbringer.Resources;
using Lightbringer.UI;
using Lightbringer.Units;
using Lightbringer.Visuals;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lightbringer.EditorTools
{
    // Rendered Play-mode benchmark of a late-campaign battle with the Art Pass look: stage 8 (three Paths),
    // a mixed army of every allied troop against every regular enemy, all immortal so the fight never ends.
    // Each quality level is measured in a typical and a heavy crowd. Results are appended to
    // Docs/Diagnostics/Performance.txt so runs before and after an optimisation can be compared.
    // Triggered by Lightbringer/Diagnostics/Benchmark Performance or Docs/Diagnostics/performance.request.
    // Editor numbers include Editor overhead: compare runs with each other, not with a shipping build.
    [InitializeOnLoad]
    public static class PerformanceBenchmark
    {
        private const string RunningKey = "Lightbringer.PerformanceBenchmark.Running";
        private const string PreviousSceneKey = "Lightbringer.PerformanceBenchmark.PreviousStartScene";
        private const string PreviousBackgroundKey = "Lightbringer.PerformanceBenchmark.PreviousBackground";
        private const string TestScenePath = "Assets/Scenes/CampaignValidation.unity";
        private const float Settle = 2f;
        private const int Samples = 240;
        private static readonly (string name, int allies, int enemies)[] Crowds = { ("typical", 30, 30), ("heavy", 110, 110) };

        private static string Folder => Path.GetFullPath(Path.Combine(Application.dataPath, "../Docs/Diagnostics"));
        private static string RequestPath => Path.Combine(Folder, "performance.request");
        private static string ReportPath => Path.Combine(Folder, "Performance.txt");

        private static CampaignSession session;
        private static Material material;
        private static readonly List<GameObject> spawned = new List<GameObject>();
        private static readonly List<string> lines = new List<string>();
        private static int level, crowd, originalLevel;
        private static float phaseStart;
        private static bool sampling, finishing;
        private static int lastFrame;
        private static double started;
        private static double nextProbe;
        private static string runtimeError;
        private static readonly List<double> frame = new List<double>(), main = new List<double>(), render = new List<double>(), gpu = new List<double>();
        private static readonly List<long> batches = new List<long>(), setPass = new List<long>(), triangles = new List<long>(), shadowCasters = new List<long>();
        private static ProfilerRecorder batchesRecorder, setPassRecorder, trianglesRecorder, shadowRecorder;
        private static readonly FrameTiming[] timing = new FrameTiming[1];
        // Main-thread breakdown: every PlayerLoop step plus the Editor loop. (Per-script markers are not
        // emitted in Editor Play mode without the Profiler window recording, so they are not listed.)
        private static readonly List<(string name, ProfilerRecorder recorder)> markers = new List<(string, ProfilerRecorder)>();
        private static double[] markerTotals = new double[0];

        private static void StartMarkers()
        {
            foreach ((string _, ProfilerRecorder recorder) in markers) recorder.Dispose();
            markers.Clear();
            List<string> names = new List<string> { "PlayerLoop", "EditorLoop", "RenderPipelineManager.DoRenderLoop_Internal()" };
            foreach (UnityEngine.LowLevel.PlayerLoopSystem system in UnityEngine.LowLevel.PlayerLoop.GetCurrentPlayerLoop().subSystemList)
                if (system.subSystemList != null)
                    foreach (UnityEngine.LowLevel.PlayerLoopSystem step in system.subSystemList)
                        if (step.type != null) names.Add(system.type.Name + "." + step.type.Name);
            foreach (string name in names.Distinct())
            {
                ProfilerRecorder recorder = new ProfilerRecorder(name);
                recorder.Start();
                markers.Add((name, recorder));
            }
            markerTotals = new double[markers.Count];
        }

        static PerformanceBenchmark()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += StateChanged;
        }

        [MenuItem("Lightbringer/Diagnostics/Benchmark Performance")]
        public static void Request()
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(RequestPath, "Run rendered performance benchmark");
        }

        private static void Update()
        {
            if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode && !SessionState.GetBool(RunningKey, false))
            {
                if (EditorApplication.timeSinceStartup < nextProbe) return;
                nextProbe = EditorApplication.timeSinceStartup + 1;
                if (!File.Exists(RequestPath) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
                File.Delete(RequestPath);
                Begin();
                return;
            }
            if (!EditorApplication.isPlaying || !SessionState.GetBool(RunningKey, false) || finishing) return;
            if (Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;
            try
            {
                if (runtimeError != null) throw new InvalidOperationException(runtimeError);
                if (session == null) Initialize();
                if (EditorApplication.timeSinceStartup - started > 240) throw new TimeoutException("Benchmark exceeded 240 seconds.");
                Step();
            }
            catch (Exception exception) { Finish(exception.ToString()); }
        }

        private static void Begin()
        {
            if (!CampaignSceneSetup.EnsureScene()) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TestScenePath) == null)
            {
                Scene original = SceneManager.GetActiveScene();
                Scene empty = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                EditorSceneManager.SaveScene(empty, TestScenePath);
                EditorSceneManager.CloseScene(empty, true);
                if (original.IsValid()) SceneManager.SetActiveScene(original);
            }
            SessionState.SetString(PreviousSceneKey, AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            SessionState.SetBool(PreviousBackgroundKey, Application.runInBackground);
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(TestScenePath);
            SessionState.SetBool(RunningKey, true);
            EditorApplication.ExecuteMenuItem("Window/General/Game");
            EditorApplication.isPlaying = true;
        }

        private static void StateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(RunningKey, false)) return;
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(PreviousSceneKey, ""));
            SessionState.SetBool(RunningKey, false);
            Application.runInBackground = SessionState.GetBool(PreviousBackgroundKey, false);
            session = null;
        }

        private static void Initialize()
        {
            lines.Clear(); spawned.Clear(); runtimeError = null; finishing = false; sampling = false;
            level = 0; crowd = 0; originalLevel = QualitySettings.GetQualityLevel();
            Application.logMessageReceived += CaptureError;
            Application.runInBackground = true;
            Application.targetFrameRate = -1;
            QualitySettings.vSyncCount = 0;
            started = EditorApplication.timeSinceStartup;
            GameObject host = new GameObject("Performance Benchmark Session"); host.SetActive(false);
            session = host.AddComponent<CampaignSession>();
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            EditorAssets.ConfigureSession(session, material);
            session.ConfigureArt(AssetDatabase.LoadAssetAtPath<ArtStyleLibrary>(ArtStyleSetup.LibraryPath));
            CampaignProgress profile = new CampaignProgress();
            for (int stage = 1; stage < CampaignProgress.StageCount; stage++) profile.CompleteStage(stage);
            session.InitializeForValidation(profile);
            host.AddComponent<CampaignHUD>().Configure(HudSetup.EnsureAssets(),
                AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.StyleSheet>(CampaignHUD.StyleSheetPath));
            host.SetActive(true);
            StartMarkers();

            batchesRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            setPassRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            trianglesRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            shadowRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Shadow Casters Count");
            lines.Add($"== Performance benchmark | {DateTime.Now:yyyy-MM-dd HH:mm} | {SystemInfo.processorType} | {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType})");
            lines.Add($"Game view {Screen.width}x{Screen.height}. Stage 8, art pass, immortal armies. Editor numbers; compare runs with each other.");
            BeginLevel();
        }

        private static void CaptureError(string condition, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) runtimeError = condition + "\n" + trace; }

        // A fresh stage-8 battle whenever none is running (first level, or the previous one ended).
        private static void EnsureBattle()
        {
            PrototypeBattle current = session.Battle;
            if (current != null && current.Root != null && current.Summoner != null && !current.Objective.HasEnded) return;
            if (current != null)
            {
                lines.Add($"  (battle restarted: ended={current.Objective != null && current.Objective.HasEnded}, won={current.Objective != null && current.Objective.HasWon})");
                while (session.IsChoosing) session.ChooseGrowth(0);
                session.ReturnToPreparation();
            }
            spawned.Clear();
            session.SelectStage(8);
            if (!session.StartBattle()) throw new InvalidOperationException("Benchmark battle did not start.");
            PrototypeBattle battle = session.Battle;
            battle.Waves.enabled = false;
            battle.Hero.SetMaximumHealth(1000000f); battle.Hero.Heal(1000000f);
            battle.Objective.EnemyBase.SetMaximumHealth(10000000f); battle.Objective.EnemyBase.Heal(10000000f);
            battle.AlliedBase.SetMaximumHealth(10000000f); battle.AlliedBase.Heal(10000000f);
            battle.Hero.GetComponent<FoodResource>().Configure(100000f, 1000f);
            battle.Hero.transform.position = new Vector3(0f, 1.05f, -4f);
            Physics.SyncTransforms();
        }

        private static void BeginLevel()
        {
            GraphicsQuality.AutoAdjust = false;
            GraphicsQuality.Apply((GraphicsQuality.Tier)(2 - level), false, false);
            EnsureBattle();
            SpawnCrowd(Crowds[crowd].allies, Crowds[crowd].enemies);
            phaseStart = Time.realtimeSinceStartup;
            sampling = false;
        }

        private static void Step()
        {
            if (!sampling)
            {
                if (Time.realtimeSinceStartup - phaseStart < Settle) return;
                sampling = true;
                foreach (IList<double> list in new IList<double>[] { frame, main, render, gpu }) list.Clear();
                foreach (IList<long> list in new IList<long>[] { batches, setPass, triangles, shadowCasters }) list.Clear();
                // Script markers only exist once their first Update ran, so bind recorders after spawning.
                StartMarkers();
                FrameTimingManager.CaptureFrameTimings();
                return;
            }
            frame.Add(Time.unscaledDeltaTime * 1000.0);
            for (int i = 0; i < markers.Count; i++)
                if (markers[i].recorder.Valid) markerTotals[i] += markers[i].recorder.LastValue * 1e-6;
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, timing) > 0)
            {
                main.Add(timing[0].cpuMainThreadFrameTime);
                render.Add(timing[0].cpuRenderThreadFrameTime);
                gpu.Add(timing[0].gpuFrameTime);
            }
            batches.Add(batchesRecorder.LastValue); setPass.Add(setPassRecorder.LastValue);
            triangles.Add(trianglesRecorder.LastValue); shadowCasters.Add(shadowRecorder.LastValue);
            if (frame.Count < Samples) return;
            Report();
            if (++crowd >= Crowds.Length)
            {
                crowd = 0;
                if (++level >= 3) { Finish(null); return; }
            }
            BeginLevel();
        }

        private static void Report()
        {
            int allies = spawned.Count(item => item != null && item.activeInHierarchy && item.GetComponent<Combatant>().Faction == Faction.Allied);
            int enemies = spawned.Count(item => item != null && item.activeInHierarchy) - allies;
            lines.Add($"[{GraphicsQuality.Current} ({QualitySettings.names[QualitySettings.GetQualityLevel()]}) | {Crowds[crowd].name}: {allies} allies + {enemies} enemies | render {RenderDescription()}]");
            lines.Add($"  frame mean {frame.Average():F2} ms ({1000.0 / frame.Average():F0} fps), p95 {Percentile(frame, 0.95):F2} ms"
                + $" | main {Mean(main)} | render thread {Mean(render)} | GPU {Mean(gpu)}");
            lines.Add($"  batches {batches.Average():F0}, set-pass {setPass.Average():F0}, triangles {triangles.Average() / 1000.0:F0}k, shadow casters {shadowCasters.Average():F0}");
            IEnumerable<string> top = Enumerable.Range(0, markers.Count)
                .Where(i => markerTotals[i] > 0.0)
                .OrderByDescending(i => markerTotals[i]).Take(22)
                .Select(i => $"{markers[i].name} {markerTotals[i] / frame.Count:F2}");
            lines.Add("  main-thread ms/frame: " + string.Join(" | ", top));
        }

        private static string RenderDescription()
        {
            if (!(QualitySettings.renderPipeline is UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset urp)) return "n/a";
            return $"scale {urp.renderScale:0.##} ({Mathf.RoundToInt(Screen.width * urp.renderScale)}x{Mathf.RoundToInt(Screen.height * urp.renderScale)}), "
                + $"MSAA {urp.msaaSampleCount}x, shadows {urp.mainLightShadowmapResolution}/{urp.shadowCascadeCount} cascades/{urp.shadowDistance:0} m";
        }

        private static string Mean(List<double> values)
        {
            List<double> valid = values.Where(value => value > 0.0).ToList();
            return valid.Count == 0 ? "n/a" : $"{valid.Average():F2} ms";
        }

        private static double Percentile(List<double> values, double fraction)
        {
            List<double> sorted = values.OrderBy(value => value).ToList();
            return sorted[Mathf.Clamp((int)(sorted.Count * fraction), 0, sorted.Count - 1)];
        }

        // Allies deploy through the real summoner from anchors spread over the three fronts; enemies enter
        // through the wave spawner's own setup. Both armies are made immortal so the melee keeps going.
        private static void SpawnCrowd(int allies, int enemies)
        {
            PrototypeBattle battle = session.Battle;
            // The previous crowd leaves only after the new one stands, so the field never empties mid-battle.
            List<GameObject> previous = new List<GameObject>(spawned);
            spawned.Clear();
            foreach (Projectile shot in battle.Root.GetComponentsInChildren<Projectile>()) UnityEngine.Object.Destroy(shot.gameObject);
            Vector3 gate = battle.DeployPoint.position;
            PropertyInfo food = typeof(FoodResource).GetProperty(nameof(FoodResource.CurrentFood));
            CombatantSink sink = new CombatantSink();
            battle.Summoner.Summoned += sink.Add;
            try
            {
                for (int i = 0; i < allies; i++)
                {
                    int lane = i % 3, row = i / 3;
                    float x = (lane - 1) * 22f + (row % 5 - 2) * 1.7f;
                    battle.DeployPoint.position = new Vector3(x, 0f, 4f - row / 5 * 1.8f);
                    battle.Summoner.TrySelectPath(lane);
                    battle.Summoner.TrySelectUnit(i % UnitCatalog.Count);
                    // Food starts empty and only trickles in; top it up so every deployment is affordable.
                    food.SetValue(battle.Hero.GetComponent<FoodResource>(), 100000f);
                    if (!battle.Summoner.TrySummon() || sink.Last == null) continue;
                    sink.Last.Configure(Faction.Allied, 100000f);
                    spawned.Add(sink.Last.gameObject);
                    sink.Last = null;
                }
            }
            finally
            {
                battle.Summoner.Summoned -= sink.Add;
                battle.DeployPoint.position = gate;
            }
            MethodInfo spawn = typeof(EnemyWaveSpawner).GetMethod("Spawn", BindingFlags.Instance | BindingFlags.NonPublic);
            Transform enemyParent = battle.Root.transform.Find("Enemy Units");
            for (int i = 0; i < enemies; i++)
            {
                int lane = i % 3, row = i / 3;
                float x = (lane - 1) * 22f + (row % 5 - 2) * 1.7f;
                int before = enemyParent.childCount;
                spawn.Invoke(battle.Waves, new object[] { (EnemyKind)(i % (int)EnemyKind.Boss), lane, new Vector3(x, 0.85f, 15f + row / 5 * 1.8f) });
                if (enemyParent.childCount == before) continue;
                Transform unit = enemyParent.GetChild(enemyParent.childCount - 1);
                unit.GetComponent<Combatant>().Configure(Faction.Enemy, 100000f);
                spawned.Add(unit.gameObject);
            }
            foreach (GameObject item in previous) if (item != null) UnityEngine.Object.Destroy(item);
            Physics.SyncTransforms();
        }

        private sealed class CombatantSink
        {
            public Combatant Last;
            public void Add(Combatant unit) => Last = unit;
        }

        private static void Finish(string error)
        {
            finishing = true;
            Application.logMessageReceived -= CaptureError;
            batchesRecorder.Dispose(); setPassRecorder.Dispose(); trianglesRecorder.Dispose(); shadowRecorder.Dispose();
            foreach ((string _, ProfilerRecorder recorder) in markers) recorder.Dispose();
            markers.Clear();
            GraphicsQuality.AutoAdjust = true;
            if (error != null) lines.Add("FAIL: " + error);
            Directory.CreateDirectory(Folder);
            File.AppendAllText(ReportPath, string.Join("\n", lines) + "\n\n", Encoding.UTF8);
            if (material != null) UnityEngine.Object.Destroy(material);
            QualitySettings.SetQualityLevel(originalLevel, true);
            EditorApplication.isPlaying = false;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using Lightbringer.Combat;
using Lightbringer.Core;
using Lightbringer.Progression;
using Lightbringer.Resources;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Lightbringer.EditorTools
{
    // Plays the stage-8 citadel collapse in an isolated Play-mode scene with allies spread over the field and
    // captures the Warlord's shockwave and smoke reveal at fixed moments (Docs/Validation/BossShockwave_*.png).
    // Create Docs/Validation/BossShockwave.request to run it. Uses a memory-only campaign profile.
    [InitializeOnLoad]
    public static class BossShockwavePreview
    {
        private const string RunningKey = "Lightbringer.BossShockwavePreview.Running";
        private const string PreviousSceneKey = "Lightbringer.BossShockwavePreview.PreviousStartScene";
        private const string TestScenePath = "Assets/Scenes/CampaignValidation.unity";
        private static string DirectoryPath => Path.GetFullPath(Path.Combine(Application.dataPath, "../Docs/Validation"));
        private static string RequestPath => Path.Combine(DirectoryPath, "BossShockwave.request");
        // Seconds after the citadel falls (the rupture lasts BossEncounter.WaveSeconds, then the arrival begins).
        private static readonly float[] Shots = { 0.45f, 1.2f, 2.0f, 2.9f, 3.9f, 4.6f, 5.5f, 6.6f };
        private static CampaignSession session;
        private static Material material;
        private static int step, shot, lastFrame, deployed;
        private static readonly HashSet<Combatant> placed = new HashSet<Combatant>();
        private static System.Random random;
        private static float stepTime, collapseTime;
        private static double started, nextProbe;
        private static readonly List<string> log = new List<string>();

        static BossShockwavePreview()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(RunningKey, false)) return;
                EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(PreviousSceneKey, ""));
                SessionState.SetBool(RunningKey, false);
            };
        }

        private static void Update()
        {
            if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode && !SessionState.GetBool(RunningKey, false))
            {
                if (EditorApplication.timeSinceStartup < nextProbe) return;
                nextProbe = EditorApplication.timeSinceStartup + 1;
                if (!File.Exists(RequestPath) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
                File.Delete(RequestPath);
                if (!CampaignSceneSetup.EnsureScene() || AssetDatabase.LoadAssetAtPath<SceneAsset>(TestScenePath) == null)
                {
                    Debug.LogError("Boss shockwave preview needs the campaign validation scene; run the campaign play validation once.");
                    return;
                }
                SessionState.SetString(PreviousSceneKey, AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
                EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(TestScenePath);
                SessionState.SetBool(RunningKey, true);
                session = null;
                EditorApplication.ExecuteMenuItem("Window/General/Game");
                EditorApplication.isPlaying = true;
                return;
            }
            if (!EditorApplication.isPlaying || !SessionState.GetBool(RunningKey, false) || Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;
            try
            {
                if (session == null) Begin();
                else if (EditorApplication.timeSinceStartup - started > 90) throw new TimeoutException("Preview exceeded 90 seconds.");
                else Step();
            }
            catch (Exception exception) { Finish(exception.ToString()); }
        }

        private static void Begin()
        {
            log.Clear();
            started = EditorApplication.timeSinceStartup;
            Application.runInBackground = true;
            GameObject host = new GameObject("Boss Shockwave Preview"); host.SetActive(false);
            session = host.AddComponent<CampaignSession>();
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            EditorAssets.ConfigureSession(session, material);
            session.ConfigureArt(AssetDatabase.LoadAssetAtPath<Lightbringer.Visuals.ArtStyleLibrary>(ArtStyleSetup.LibraryPath));
            var progress = new CampaignProgress { unlockedStage = CampaignProgress.StageCount };
            session.InitializeForValidation(progress);
            host.AddComponent<Lightbringer.UI.CampaignHUD>();
            host.SetActive(true);
            if (!session.SelectStage(Units.EnemyCatalog.BossStage) || !session.StartBattle())
                throw new InvalidOperationException("Stage 8 did not start.");
            step = 1; stepTime = Time.realtimeSinceStartup; shot = 0; deployed = 0;
            placed.Clear();
            random = new System.Random(8);
        }

        private static void Step()
        {
            PrototypeBattle battle = session.Battle;
            float elapsed = Time.realtimeSinceStartup - stepTime;
            switch (step)
            {
                case 1:
                    battle.Hero.GetComponent<FoodResource>().Configure(1000, 1000);
                    if (elapsed < 1.5f) return;
                    // One deployment per frame (Food refills meanwhile), each moved out over the field so the
                    // deploy point stays clear. Every troop type, across the Paths.
                    if (deployed < 36 && elapsed < 12f)
                    {
                        if (deployed % 4 == 3) battle.Summoner.TryCyclePath();
                        if (battle.Summoner.TrySelectUnit(deployed % Units.UnitCatalog.Count) && battle.Summoner.TrySummon())
                        {
                            deployed++;
                            foreach (Combatant unit in battle.Root.GetComponentsInChildren<Combatant>())
                            {
                                if (unit.Faction != Faction.Allied || unit == battle.Hero || unit == battle.AlliedBase || !placed.Add(unit)) continue;
                                Vector3 local = new Vector3((float)(random.NextDouble() * 44 - 22), 1f, (float)(random.NextDouble() * 76 - 6));
                                unit.transform.position = battle.Root.transform.TransformPoint(local);
                            }
                            Physics.SyncTransforms();
                        }
                        return;
                    }
                    log.Add("Deployed allies: " + deployed);
                    step = 2; stepTime = Time.realtimeSinceStartup; break;
                case 2:
                    if (elapsed < 0.6f) return;
                    for (int attempt = 0; attempt < 100 && !battle.Waves.AssaultCleared; attempt++)
                    {
                        battle.Waves.Tick(10);
                        foreach (Combatant enemy in battle.Root.GetComponentsInChildren<Combatant>())
                            if (enemy.Faction == Faction.Enemy && enemy != battle.Objective.EnemyBase)
                                enemy.TakeDamage(enemy.MaximumHealth * 10);
                        Physics.SyncTransforms();
                    }
                    int alive = 0;
                    foreach (Combatant unit in battle.Root.GetComponentsInChildren<Combatant>())
                        if (unit.Faction == Faction.Allied && unit != battle.Hero && unit != battle.AlliedBase && unit.IsAlive) alive++;
                    log.Add("Allies standing at collapse: " + alive);
                    battle.Objective.EnemyBase.TakeDamage(100000, battle.Hero);
                    collapseTime = Time.time;
                    step = 3; break;
                case 3:
                    if (shot < Shots.Length && Time.time - collapseTime >= Shots[shot])
                    {
                        string name = $"BossShockwave_{shot + 1}_{Shots[shot]:0.0}s.png";
                        ScreenCapture.CaptureScreenshot(Path.Combine(DirectoryPath, name));
                        log.Add($"{name}: phase={battle.Encounter.Phase}, fallen={battle.Encounter.FallenAllies}");
                        shot++;
                    }
                    if (shot >= Shots.Length && Time.time - collapseTime > Shots[Shots.Length - 1] + 0.5f) Finish(null);
                    break;
            }
        }

        private static void Finish(string error)
        {
            if (error != null) log.Add("FAIL: " + error);
            Directory.CreateDirectory(DirectoryPath);
            File.WriteAllText(Path.Combine(DirectoryPath, "BossShockwavePreview.txt"),
                (error == null ? "DONE" : "FAIL") + " | " + DateTime.Now.ToString("O") + "\n" + string.Join("\n", log));
            if (material != null) UnityEngine.Object.Destroy(material);
            session = null;
            EditorApplication.isPlaying = false;
        }
    }
}

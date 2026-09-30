using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lightbringer.Combat;
using Lightbringer.Core;
using Lightbringer.Progression;
using Lightbringer.Resources;
using Lightbringer.Units;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Lightbringer.EditorTools
{
    // An isolated Play-mode scene. Unity restores the user's edit-mode scenes after exit.
    // Test profiles are memory-only and never touch the real campaign save.
    [InitializeOnLoad]
    public static class CampaignPlayValidation
    {
        private const string RunningKey = "Lightbringer.CampaignPlayValidation.Running";
        private const string PreviousSceneKey = "Lightbringer.CampaignPlayValidation.PreviousStartScene";
        private const string PreviousBackgroundKey = "Lightbringer.CampaignPlayValidation.PreviousBackground";
        private const string TestScenePath = "Assets/Scenes/CampaignValidation.unity";
        private static string DirectoryPath => Path.GetFullPath(Path.Combine(Application.dataPath, "../Docs/Validation"));
        private static string RequestPath => Path.Combine(DirectoryPath, "RunCampaignPlayValidation.request");
        private static CampaignSession session;
        private static Material material;
        private static int phase;
        private static float phaseTime;
        private static double started;
        private static double nextProbe;
        private static int lastFrame;
        private static Vector3 soldierStart;
        private static Combatant soldier;
        private static int chosenRank;
        private static float pausedFood;
        private static Vector3 pausedPosition;
        private static GameObject stressRoot;
        private static int stressCount;
        private static readonly List<double> frames = new List<double>();
        private static readonly List<string> results = new List<string>();
        private static string runtimeError;
        private static bool finishing;
        private static readonly string LoadedScriptStamp = GreyboxScriptAutoRefresh.GetStamp();

        static CampaignPlayValidation()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += StateChanged;
        }

        [MenuItem("Lightbringer/Validate Campaign in Play Mode")]
        public static void Request()
        {
            Directory.CreateDirectory(DirectoryPath);
            File.WriteAllText(RequestPath, "Run isolated campaign validation");
        }

        private static void Update()
        {
            if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode && !SessionState.GetBool(RunningKey, false))
            {
                if (EditorApplication.timeSinceStartup < nextProbe) return;
                nextProbe = EditorApplication.timeSinceStartup + 1;
                if (!File.Exists(RequestPath) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
                // A request may arrive before Unity's script watcher imports the accompanying edits.
                if (LoadedScriptStamp != GreyboxScriptAutoRefresh.GetStamp())
                {
                    AssetDatabase.Refresh();
                    return;
                }
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
                if (EditorApplication.timeSinceStartup - started > 150) throw new TimeoutException("Play validation exceeded 150 seconds.");
                Step();
            }
            catch (Exception exception) { Finish(false, exception.ToString()); }
        }

        private static void Begin()
        {
            if (!GreyboxValidation.RunChecks()) return;
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
            if (!finishing && !File.Exists(Path.Combine(DirectoryPath, "CampaignPlayChecks.txt")))
                File.WriteAllText(Path.Combine(DirectoryPath, "CampaignPlayChecks.txt"), "INCOMPLETE: Play mode exited before the probe finished.");
        }

        private static void Initialize()
        {
            results.Clear(); frames.Clear(); runtimeError = null; finishing = false;
            Application.logMessageReceived += CaptureError;
            Application.runInBackground = true;
            started = EditorApplication.timeSinceStartup;
            GameObject host = new GameObject("Isolated Campaign Probe"); host.SetActive(false);
            session = host.AddComponent<CampaignSession>();
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            session.Configure(AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions"),
                material, Shader.Find("Universal Render Pipeline/Unlit"));
            session.InitializeForValidation(new CampaignProgress());
            host.AddComponent<Lightbringer.UI.CampaignHUD>();
            host.SetActive(true);
            Assert(session.StartBattle(), "Actual Play mode can start a campaign with isolated progress");
            phase = 1; phaseTime = Time.realtimeSinceStartup;
        }

        private static void CaptureError(string condition, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) runtimeError = condition + "\n" + trace; }

        private static void Step()
        {
            float elapsed = Time.realtimeSinceStartup - phaseTime;
            PrototypeBattle battle = session.Battle;
            switch (phase)
            {
                case 1:
                    if (elapsed < 2.2f) return;
                    Assert(battle.Hero.IsAlive && battle.Hero.CurrentHealth == 120
                        && battle.Hero.GetComponent<FoodResource>().CurrentFood >= 10,
                        "Awake/Start initialize hero stats and live Food generation");
                    battle.Summoner.Summoned += unit => soldier = unit;
                    Assert(battle.Summoner.TrySummon(), "Actual runtime summon initializes and activates one allied soldier");
                    soldierStart = soldier.transform.position;
                    Screenshot("CampaignBattle.png");
                    Advance(); break;
                case 2:
                    if (elapsed < 1.2f) return;
                    Assert(soldier != null && soldier.transform.position.z > soldierStart.z + 1,
                        "Summoned soldier advances automatically across real rendered frames");
                    Combatant victim = battle.Root.GetComponentsInChildren<Combatant>().First(x => x.Faction == Faction.Enemy && x != battle.Objective.EnemyBase);
                    victim.transform.position = battle.Hero.transform.position + Vector3.forward * 4;
                    Physics.SyncTransforms();
                    float hp = victim.CurrentHealth;
                    Assert(battle.Abilities.TryCast(0, victim) && victim.CurrentHealth < hp, "Hero staff deals damage during actual Play mode");
                    victim.TakeDamage(10000, battle.Hero);
                    session.Progress.GainExperience(60);
                    Advance(); break;
                case 3:
                    if (elapsed < 0.3f) return;
                    Assert(session.IsChoosing && session.IsBattlePaused && !battle.Summoner.enabled,
                        "Actual level-up suspends battle input and simulation");
                    pausedFood = battle.Hero.GetComponent<FoodResource>().CurrentFood;
                    pausedPosition = battle.Hero.transform.position;
                    chosenRank = session.Progress.choices[0];
                    Advance(); break;
                case 4:
                    if (elapsed < 0.4f) return;
                    Assert(battle.Hero.GetComponent<FoodResource>().CurrentFood == pausedFood && battle.Hero.transform.position == pausedPosition,
                        "Food and movement remain frozen while growth choices are displayed");
                    Assert(session.ChooseGrowth(0) && session.Progress.ranks[chosenRank] == 1 && battle.Summoner.enabled,
                        "Choosing permanent growth resumes enabled runtime components");
                    battle.Objective.EnemyBase.TakeDamage(10000, battle.Hero);
                    Advance(); break;
                case 5:
                    if (elapsed < 0.2f) return;
                    while (session.IsChoosing) session.ChooseGrowth(0);
                    Assert(battle.Objective.HasWon && session.Progress.unlockedStage == 2 && session.Progress.gold == 100,
                        "Runtime base destruction grants victory and persistent-profile rewards");
                    Screenshot("CampaignVictory.png");
                    Advance(); break;
                case 6:
                    if (elapsed < 0.3f) return;
                    Assert(session.ReturnToPreparation() && session.StartBattle(), "Result screen can return and start the next unlocked stage");
                    Advance(); break;
                case 7:
                    if (elapsed < 0.5f) return;
                    battle.Hero.TakeDamage(10000);
                    Advance(); break;
                case 8:
                    if (elapsed < 0.3f) return;
                    Assert(battle.Objective.HasLost && session.Progress.ranks[chosenRank] >= 1 && session.Progress.gold == 100,
                        "Runtime hero death keeps permanent growth and grants no victory reward");
                    Assert(session.ReturnToPreparation(), "Defeat can return to preparation without reloading Unity");
                    for (int stage = 2; stage < 8; stage++) session.Progress.CompleteStage(stage);
                    session.SelectStage(8);
                    Assert(session.StartBattle() && session.Battle.Paths.Length == 3, "Late campaign runtime builds three independent fronts");
                    session.Battle.Summoner.Summoned += unit => soldier = unit;
                    session.Battle.Hero.GetComponent<FoodResource>().Configure(1000, 1000);
                    Advance(); break;
                case 9:
                    // Apply probe resources after the hero's real Start has applied its saved growth/loadout.
                    battle.Hero.GetComponent<FoodResource>().Configure(1000, 1000);
                    if (elapsed < 1.2f) return;
                    for (int i = 0; i < UnitCatalog.Count; i++)
                    {
                        bool selected = battle.Summoner.TrySelectUnit(i);
                        bool summoned = selected && battle.Summoner.TrySummon();
                        Assert(summoned, "Runtime deployment: " + UnitCatalog.Names[i] + (summoned ? "" :
                            $" [selected={selected}, enabled={battle.Summoner.enabled}, food={battle.Hero.GetComponent<FoodResource>().CurrentFood}, paused={session.IsBattlePaused}, pending={session.Progress.pendingLevels}, feedback={battle.Summoner.LastFeedback}, position={battle.Hero.transform.position}]") );
                    }
                    Advance(); break;
                case 10:
                    if (elapsed < 1.5f) return;
                    Assert(soldier != null && soldier.name.StartsWith("Dragon") && soldier.transform.position.y > 2,
                        "Runtime Dragon rises above ground troops");
                    Screenshot("CampaignThreeFronts.png");
                    phase = 16; phaseTime = Time.realtimeSinceStartup; break;
                case 11:
                case 13:
                    if (elapsed < 1f) return;
                    frames.Clear(); Advance(); break;
                case 12:
                case 14:
                    frames.Add(Time.unscaledDeltaTime * 1000.0);
                    if (frames.Count < 180) return;
                    frames.Sort();
                    results.Add($"MEASURE: {stressCount} rendered stress units (+hero/base): frame mean {frames.Average():F3} ms, p95 {frames[171]:F3} ms. Unity Editor, not a shipping-build benchmark.");
                    Assert(stressRoot.GetComponentsInChildren<Combatant>().Length == stressCount,
                        stressCount + " rendered stress units stay active through sustained real-time combat");
                    Assert(stressRoot.GetComponentsInChildren<Combatant>().Any(x => x.CurrentHealth < 100000),
                        stressCount + " rendered stress units exchange damage");
                    if (phase == 12) { StartStress(300); Advance(); }
                    else { Screenshot("Campaign300Units.png"); Advance(); }
                    break;
                case 15:
                    if (elapsed < 0.5f) return;
                    battle.Objective.EnemyBase.TakeDamage(10000, battle.Hero);
                    while (session.IsChoosing) session.ChooseGrowth(0);
                    Assert(session.ReturnToPreparation(), "Stress battle can clean up and return to campaign preparation");
                    Screenshot("CampaignPreparation.png");
                    phase = 17; phaseTime = Time.realtimeSinceStartup; break;
                case 16:
                    if (elapsed < 0.3f) return;
                    StartStress(100);
                    phase = 11; phaseTime = Time.realtimeSinceStartup; break;
                case 17:
                    if (elapsed < 0.5f) return;
                    Finish(true, null); break;
            }
        }

        private static void StartStress(int count)
        {
            PrototypeBattle battle = session.Battle;
            battle.Waves.enabled = false;
            foreach (Combatant unit in battle.Root.GetComponentsInChildren<Combatant>())
                if (unit != battle.Hero && unit != battle.Objective.EnemyBase) unit.gameObject.SetActive(false);
            if (stressRoot != null) UnityEngine.Object.Destroy(stressRoot);
            stressRoot = new GameObject("Rendered stress " + count); stressRoot.transform.SetParent(battle.Root.transform);
            CharacterController template = battle.Root.transform.Find("Unit Template").GetComponent<CharacterController>();
            for (int i = 0; i < count; i++)
            {
                bool allied = i < count / 2;
                int index = i % (count / 2);
                Vector3 position = new Vector3((index % 15 - 7) * 1.1f, 0.85f, allied ? 3 - index / 15 * 1.1f : 7 + index / 15 * 1.1f);
                CharacterController unit = UnityEngine.Object.Instantiate(template, position, Quaternion.identity, stressRoot.transform);
                unit.GetComponent<Combatant>().Configure(allied ? Faction.Allied : Faction.Enemy, 100000);
                unit.GetComponent<UnitPathFollower>().ConfigureCrowdAvoidance(true);
                unit.gameObject.SetActive(true);
            }
            Physics.SyncTransforms();
            stressCount = count;
        }

        private static void Advance() { phase++; phaseTime = Time.realtimeSinceStartup; }
        private static void Assert(bool condition, string description)
        { if (!condition) throw new InvalidOperationException(description); results.Add("PASS: " + description); }
        private static void Screenshot(string name) => ScreenCapture.CaptureScreenshot(Path.Combine(DirectoryPath, name));
        private static void Finish(bool passed, string error)
        {
            finishing = true;
            Application.logMessageReceived -= CaptureError;
            if (error != null) results.Add("FAIL: " + error);
            Directory.CreateDirectory(DirectoryPath);
            File.WriteAllText(Path.Combine(DirectoryPath, "CampaignPlayChecks.txt"),
                (passed ? "PASS" : "FAIL") + " | CampaignPlay.v1 | " + DateTime.UtcNow.ToString("O") + "\n" + string.Join("\n", results));
            if (material != null) UnityEngine.Object.Destroy(material);
            EditorApplication.isPlaying = false;
        }
    }
}

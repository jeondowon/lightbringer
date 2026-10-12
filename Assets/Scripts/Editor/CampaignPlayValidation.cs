using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lightbringer.Combat;
using Lightbringer.Core;
using Lightbringer.Player;
using Lightbringer.Progression;
using Lightbringer.Resources;
using Lightbringer.Units;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lightbringer.EditorTools
{
    // An isolated Play-mode scene. Unity restores the user's edit-mode scenes after exit.
    // Test profiles are memory-only and never touch the real campaign save.
    [InitializeOnLoad]
    public static partial class CampaignPlayValidation
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
        private static float battleStartTime;
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
        private static bool storyShotCaptured;
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
            // An explicit validation request may arrive while a manual play session is still running.
            // Leave it before launching the isolated scene; never toggle an active validation run.
            if (EditorApplication.isPlaying && !SessionState.GetBool(RunningKey, false) && File.Exists(RequestPath))
            {
                EditorApplication.isPlaying = false;
                return;
            }
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
                // Let the requested audio import finish in Edit mode before testing its runtime cues.
                if (File.Exists(Path.Combine(DirectoryPath, "ImportSelectedMagic.request"))
                    || File.Exists(Path.Combine(DirectoryPath, "ImportSelectedHeroSounds.request"))
                    || File.Exists(Path.Combine(DirectoryPath, "InspectCombatAudio.request"))
                    || File.Exists(Path.Combine(DirectoryPath, "ImportSelectedCombatSounds.request"))
                    || File.Exists(Path.Combine(DirectoryPath, "ImportTroopVoices.request"))
                    || File.Exists(Path.Combine(DirectoryPath, "ImportSelectedButton.request"))
                    || File.Exists(Path.Combine(DirectoryPath, "ImportSelectedSummon.request"))
                    || File.Exists(Path.Combine(DirectoryPath, "ImportDeathAndHeroSounds.request"))
                    || File.Exists(Path.Combine(DirectoryPath, "NormalizeSoundLevels.request"))
                    || File.Exists(Path.Combine(DirectoryPath, "ImportSelectedAdditionalSounds.request"))) return;
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
            results.Clear(); frames.Clear(); runtimeError = null; finishing = false; storyShotCaptured = false;
            Application.logMessageReceived += CaptureError;
            Application.runInBackground = true;
            started = EditorApplication.timeSinceStartup;
            GameObject host = new GameObject("Isolated Campaign Probe"); host.SetActive(false);
            session = host.AddComponent<CampaignSession>();
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            EditorAssets.ConfigureSession(session, material);
            session.ConfigureArt(AssetDatabase.LoadAssetAtPath<Lightbringer.Visuals.ArtStyleLibrary>(ArtStyleSetup.LibraryPath));
            session.InitializeForValidation(new CampaignProgress());
            host.AddComponent<Lightbringer.UI.CampaignHUD>();
            host.SetActive(true);
            Assert(Cursor.lockState == CursorLockMode.None && Cursor.visible, "Campaign preparation releases menu input");
            Assert(session.StartBattle(), "Actual Play mode can start a campaign with isolated progress");
            battleStartTime = Time.time;
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
                    // Food advances on scaled simulation time; asset loading and shader compilation
                    // can consume wall time without giving Update enough time to produce it.
                    if (Time.time - battleStartTime < 2.2f) return;
                    AudioSource battleMusic = battle.Root.GetComponent<AudioSource>();
                    Assert(battle.Root.GetComponent<BattleMusic>() != null && battleMusic != null
                        && battleMusic.clip == UnityEngine.Resources.Load<AudioClip>(BattleMusic.ResourcePath)
                        && battleMusic.isPlaying && battleMusic.timeSamples > 0 && battleMusic.loop && battleMusic.spatialBlend == 0f,
                        "The Tread of War loads and advances as looping 2D battle music");
                    if (session.ArtStyle != null && session.ArtStyle.forestEnvironment != null)
                    {
                        Lightbringer.Visuals.ForestEnvironmentDressing forest = battle.Root.GetComponentInChildren<Lightbringer.Visuals.ForestEnvironmentDressing>();
                        Assert(forest != null && forest.TreeCount >= 40 && forest.PropCount >= 400,
                            "Stage 1 renders the imported forest profile");
                        Assert(forest.GetComponentsInChildren<Collider>(true).Length == 0,
                            "Imported forest decoration adds no gameplay colliders");
                        Assert(forest.GetComponentsInChildren<MeshRenderer>(true).All(r =>
                            r.sharedMaterials.All(m => m != null && m.shader != null && m.shader.isSupported)),
                            "Imported forest renderers have supported materials");
                        Lightbringer.Visuals.BattlefieldEnvironment terrain = forest.GetComponentInParent<Lightbringer.Visuals.BattlefieldEnvironment>();
                        Assert(terrain.GrassChunks == 0, "Imported forest replaces the procedural grass carpet");
                        foreach (Transform tree in forest.transform)
                        {
                            if (tree.name != session.ArtStyle.forestEnvironment.tree.name) continue;
                            float lowest = float.PositiveInfinity;
                            foreach (MeshFilter mesh in tree.GetComponentsInChildren<MeshFilter>(true))
                                foreach (Vector3 vertex in mesh.sharedMesh.vertices)
                                    lowest = Mathf.Min(lowest, terrain.transform.InverseTransformPoint(mesh.transform.TransformPoint(vertex)).y);
                            Vector3 foot = terrain.transform.InverseTransformPoint(tree.position);
                            Assert(lowest <= terrain.SurfaceHeight(foot.x, foot.z) + 0.02f,
                                $"Forest tree mesh reaches the terrain at ({foot.x:F1}, {foot.z:F1})");
                        }
                    }
                    Assert(battle.Hero.IsAlive && battle.Hero.CurrentHealth == 120
                        && battle.Hero.GetComponent<FoodResource>().CurrentFood >= 10,
                        $"Awake/Start initialize hero stats and live Food generation [health={battle.Hero.CurrentHealth}, food={battle.Hero.GetComponent<FoodResource>().CurrentFood:F2}]");
                    battle.Summoner.Summoned += unit => soldier = unit;
                    Assert(battle.Summoner.TrySummon(), "Actual runtime summon initializes and activates one allied soldier");
                    ValidateSoundEffects(battle, soldier);
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
                    Assert(battle.Abilities.TryCast(0, victim) && victim.CurrentHealth == hp, "Hero staff defers damage until light impact");
                    battle.Abilities.Tick(HeroAbilities.LightStaffImpactDelay + 0.001f);
                    Assert(victim.CurrentHealth < hp, "Hero staff deals damage after advancing to light impact in Play mode");
                    victim.TakeDamage(10000, battle.Hero);
                    session.Progress.GainExperience(session.Progress.ExperienceToNext - session.Progress.experience);
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
                    Assert(battle.Objective.HasWon && session.Progress.unlockedStage == 2 && session.Progress.cleared[0],
                        "Runtime base destruction grants victory and persistent-profile rewards");
                    Screenshot("CampaignVictory.png");
                    Advance(); break;
                case 6:
                    if (elapsed < 1.5f) return;
                    Assert(!battle.Root.GetComponent<AudioSource>().isPlaying,
                        "Victory fades out and stops battle music");
                    Assert(session.ReturnToPreparation() && session.StartBattle(), "Result screen can return and start the next unlocked stage");
                    Advance(); break;
                case 7:
                    if (elapsed < 0.5f) return;
                    Assert(battle.Root.GetComponents<AudioSource>().Length == 1 && battle.Root.GetComponent<AudioSource>().isPlaying,
                        "Starting the next battle creates exactly one playing music source");
                    Assert(session.SetPaused(true), "Battle music pause probe pauses the battle");
                    ValidatePausedSoundEffects();
                    phase = 18; phaseTime = Time.realtimeSinceStartup; break;
                case 18:
                    if (elapsed < 1.3f) return;
                    AudioSource pausedMusic = battle.Root.GetComponent<AudioSource>();
                    Assert(Time.timeScale == 0f && pausedMusic.isPlaying
                        && Mathf.Abs(pausedMusic.volume - BattleMusic.Volume * 0.35f) < 0.01f,
                        "Pause keeps music playing and ducks volume using unscaled time");
                    Assert(session.SetPaused(false), "Music pause probe resumes the battle");
                    battle.Hero.TakeDamage(10000);
                    phase = 8; phaseTime = Time.realtimeSinceStartup; break;
                case 8:
                    if (elapsed < 1.5f) return;
                    Assert(!battle.Root.GetComponent<AudioSource>().isPlaying,
                        "Defeat fades out and stops battle music");
                    Assert(battle.Objective.HasLost && session.Progress.ranks[chosenRank] >= 1 && session.Progress.cleared[0],
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
                    // The rigged Dragon became a ground unit (Docs/Roadmap.md, 2026-10-03).
                    Assert(soldier != null && soldier.name.StartsWith(UnitCatalog.Names[(int)UnitKind.Dragon], StringComparison.Ordinal)
                        && soldier.GetComponent<CharacterController>().isGrounded
                        && soldier.GetComponent<UnitPathFollower>().AssignedPath != null,
                        "Runtime Dragon stays grounded on its assigned Path");
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
                    // Stress setup deliberately bypasses the assault. End it as a defeat, then test a fresh boss encounter.
                    battle.Hero.TakeDamage(10000);
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
                    Assert(session.SelectStage(8) && session.StartBattle(), "A fresh final-stage encounter starts after the stress run");
                    session.Battle.Hero.GetComponent<FoodResource>().Configure(1000, 1000);
                    phase = 20; phaseTime = Time.realtimeSinceStartup; break;
                case 20:
                    battle.Hero.GetComponent<FoodResource>().Configure(1000, 1000);
                    if (elapsed < 1f) return;
                    // Use the live spawner and real death events; no shield bypass or early boss injection.
                    for (int attempt = 0; attempt < 100 && !battle.Waves.AssaultCleared; attempt++)
                    {
                        battle.Waves.Tick(10);
                        foreach (Combatant enemy in battle.Root.GetComponentsInChildren<Combatant>())
                            if (enemy.Faction == Faction.Enemy && enemy != battle.Objective.EnemyBase)
                                enemy.TakeDamage(enemy.MaximumHealth * 10);
                        Physics.SyncTransforms();
                    }
                    Assert(battle.Waves.AssaultCleared && battle.Waves.Boss == null && !battle.Objective.EnemyBase.Invulnerable,
                        "Runtime final assault ends without spawning the boss and exposes the stronghold");
                    Assert(battle.Summoner.TrySelectUnit(0) && battle.Summoner.TrySummon(), "An ally is present for the cinematic shockwave");
                    battle.Objective.EnemyBase.TakeDamage(10000, battle.Hero);
                    Assert(session.IsBossCinematic && !battle.Objective.HasEnded && !battle.Summoner.enabled,
                        "Runtime base death enters the cinematic and blocks summons without victory");
                    phase = 21; phaseTime = Time.realtimeSinceStartup; break;
                case 21:
                    if (elapsed < 1.2f) return;
                    Assert(battle.Root.GetComponentsInChildren<Lightbringer.Visuals.BossVfx>().Length > 0,
                        "Runtime rupture creates the procedural aurora and collapse effects");
                    Screenshot("BossRupture.png");
                    phase = 22; phaseTime = Time.realtimeSinceStartup; break;
                case 22:
                    if (battle.Encounter.Phase != BossEncounterPhase.Dialogue) return;
                    Assert(battle.Encounter.FallenAllies >= 1 && battle.Hero.IsAlive && battle.AlliedBase.IsAlive,
                        "Runtime aurora eliminates allies while the hero and base survive");
                    Assert(battle.Waves.Boss != null && battle.Waves.Boss.Invulnerable && !session.IsChoosing,
                        "Runtime boss reveal protects both speakers and defers growth");
                    phase = 28; phaseTime = Time.realtimeSinceStartup; break;
                case 28:
                    if (elapsed < .7f) return;
                    var storyHud = session.GetComponent<Lightbringer.UI.CampaignHUD>();
                    Assert(storyHud.BossDialogue.StoryVisible && storyHud.BossDialogue.StoryImage != null,
                        "Runtime boss dialogue displays the bundled storybook illustration");
                    Screenshot("BossDialogue.png");
                    phase = 23; phaseTime = Time.realtimeSinceStartup; break;
                case 23:
                    if (elapsed < 0.7f) return;
                    if (battle.Encounter.Phase == BossEncounterPhase.Dialogue)
                    {
                        if (!storyShotCaptured)
                        {
                            var openingView = session.GetComponent<Lightbringer.UI.CampaignHUD>().BossDialogue;
                            Assert(openingView.StoryImage != null && openingView.LineText == BossEncounter.Lines[battle.Encounter.DialogueIndex],
                                "Runtime opening shot has the matching illustration and dialogue");
                            if (battle.Encounter.DialogueIndex != 1) Screenshot($"BossDialogue_{battle.Encounter.DialogueIndex + 1:00}.png");
                            storyShotCaptured = true;
                            return; // ScreenCapture writes at the end of this frame, before advancing the story.
                        }
                        storyShotCaptured = false;
                        battle.Encounter.AdvanceDialogue(); phaseTime = Time.realtimeSinceStartup; return;
                    }
                    while (session.IsChoosing) session.ChooseGrowth(0);
                    Assert(battle.Summoner.enabled && !battle.Waves.Boss.Invulnerable && !battle.Hero.Invulnerable,
                        "Finishing the dialogue restores the fight and normal vulnerability");
                    battle.Hero.transform.position = new Vector3(0, 1.05f, 8);
                    battle.Waves.Boss.transform.position = new Vector3(0, .85f, 18);
                    battle.Hero.Invulnerable = true; // Observe the projectile without losing the validation hero.
                    Physics.SyncTransforms();
                    phase = 24; phaseTime = Time.realtimeSinceStartup; break;
                case 24:
                    if (!battle.Waves.Boss.GetComponent<BossCombat>().IsAttacking) return;
                    Screenshot("BossAttackWarning.png");
                    phase = 25; phaseTime = Time.realtimeSinceStartup; break;
                case 25:
                    if (battle.Root.GetComponentInChildren<DarkCrescent>() == null) return;
                    Assert(battle.Root.GetComponentInChildren<DarkCrescent>().enabled,
                        "Runtime boss animation releases a moving dark crescent");
                    phase = 26; phaseTime = Time.realtimeSinceStartup; break;
                case 26:
                    if (elapsed < .2f) return;
                    Screenshot("BossCrescent.png");
                    phase = 27; phaseTime = Time.realtimeSinceStartup; break;
                case 27:
                    if (elapsed < .3f) return;
                    session.Progress.experience = session.Progress.ExperienceToNext - 1;
                    battle.Waves.Boss.TakeDamage(100000, battle.Hero);
                    Assert(!battle.Objective.HasEnded && session.IsBossCinematic && !session.IsChoosing
                        && !battle.Summoner.enabled && !session.ReturnToPreparation(),
                        "Runtime boss defeat begins the retreat story and withholds victory and growth");
                    phase = 29; phaseTime = Time.realtimeSinceStartup; break;
                case 29:
                    if (elapsed < 1f) return;
                    var retreatHud = session.GetComponent<Lightbringer.UI.CampaignHUD>();
                    Assert(battle.Waves.Boss == null && retreatHud.BossDialogue.StoryVisible
                        && retreatHud.BossDialogue.StoryImage != null && !retreatHud.Result.Visible
                        && retreatHud.BossDialogue.LineText == BossEncounter.RetreatLines[0],
                        "The retreat remains visible after the runtime boss object is destroyed");
                    Screenshot("BossRetreat.png");
                    phase = 30; phaseTime = Time.realtimeSinceStartup; break;
                case 30:
                    if (elapsed < .9f) return;
                    if (!storyShotCaptured)
                    {
                        var retreatView = session.GetComponent<Lightbringer.UI.CampaignHUD>().BossDialogue;
                        Assert(retreatView.StoryImage != null && retreatView.LineText == BossEncounter.RetreatLines[battle.Encounter.DialogueIndex],
                            "Runtime retreat shot has the matching illustration and dialogue");
                        Screenshot($"BossRetreat_{battle.Encounter.DialogueIndex + 1:00}.png");
                        storyShotCaptured = true;
                        return;
                    }
                    storyShotCaptured = false;
                    battle.Encounter.AdvanceDialogue();
                    Assert(!battle.Objective.HasEnded, "Reading the retreat cannot immediately expose victory");
                    phaseTime = Time.realtimeSinceStartup;
                    if (battle.Encounter.Phase == BossEncounterPhase.Retreat) phase = 31;
                    break;
                case 31:
                    // Screenshot encoding can stall the Editor; Unity caps deltaTime on those frames.
                    // Wait for the in-game fade to finish, with a bounded wall-clock timeout.
                    if (!battle.Objective.HasEnded && elapsed < 8f) return;
                    while (session.IsChoosing) session.ChooseGrowth(0);
                    Assert(battle.Objective.HasWon && session.Progress.IsComplete,
                        "Runtime victory is awarded only after the retreat story and its closing fade");
                    Assert(session.ReturnToPreparation(), "Final boss victory cleans up and returns to preparation");
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

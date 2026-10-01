using System.IO;
using Lightbringer.Combat;
using Lightbringer.Progression;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lightbringer.Core
{
    [DefaultExecutionOrder(-200)]
    public sealed class CampaignSession : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private Material prototypeMaterial;
        [SerializeField] private Shader auraShader;
        [Tooltip("Art Pass look. Leave empty to keep greybox capsules.")]
        [SerializeField] private Lightbringer.Visuals.ArtStyleLibrary artStyle;
        private CampaignSaveStore store;
        private readonly BattleSimulation simulation = new BattleSimulation();
        private bool dirty;
        private float saveTimer;
        private GameObject preparationCamera;
        private PlaytestRecorder playtest = new PlaytestRecorder(null);
        public CampaignProgress Progress { get; private set; }
        public PrototypeBattle Battle { get; private set; }
        public int SelectedStage { get; private set; } = 1;
        public string LastResult { get; private set; } = "";
        public string SaveStatus => store == null ? "Validation session" : store.Error;
        public bool IsChoosing => Progress != null && Progress.pendingLevels > 0;
        public bool IsBattlePaused => simulation.IsPaused;
        public PlaytestRecord PlaytestRecord => playtest.Current;
        public string PlaytestStatus { get; private set; } = "";

        public void Configure(InputActionAsset actions, Material material, Shader ring)
        { inputActions = actions; prototypeMaterial = material; auraShader = ring; }

        public Lightbringer.Visuals.ArtStyleLibrary ArtStyle => artStyle;
        public void ConfigureArt(Lightbringer.Visuals.ArtStyleLibrary style) => artStyle = style;

        public void InitializeForValidation(CampaignProgress profile)
        { Progress = profile; store = null; SelectedStage = profile.unlockedStage; playtest = new PlaytestRecorder(null); }

        private void Awake()
        {
            DisableStandaloneGreybox();
            if (Progress == null)
            {
                store = new CampaignSaveStore(Path.Combine(Application.persistentDataPath, "Lightbringer", "campaign-v1.json"));
                Progress = store.Load();
                SelectedStage = Progress.unlockedStage;
                playtest = new PlaytestRecorder(PlaytestRecorder.DefaultLogPath);
            }
            preparationCamera = new GameObject("Preparation Camera");
            preparationCamera.transform.SetParent(transform, false);
            UnityEngine.Camera camera = preparationCamera.AddComponent<UnityEngine.Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.07f, 0.09f, 0.12f);
            camera.cullingMask = 0;
            preparationCamera.AddComponent<AudioListener>();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // The standalone setup can exist in older campaign scenes. Its camera would
        // recapture menu clicks and its simulation would run behind preparation.
        private void DisableStandaloneGreybox()
        {
            GameObject[] roots = gameObject.scene.GetRootGameObjects();
            foreach (GameObject root in roots)
            {
                if (root.GetComponent<Lightbringer.UI.GreyboxHUD>() == null
                    || transform.IsChildOf(root.transform)) continue;
                foreach (GameObject cameraRoot in roots)
                    foreach (Lightbringer.CameraSystem.ThirdPersonCamera follow in
                        cameraRoot.GetComponentsInChildren<Lightbringer.CameraSystem.ThirdPersonCamera>(true))
                    {
                        if (follow.Target == null || !follow.Target.IsChildOf(root.transform)) continue;
                        follow.enabled = false;
                        follow.GetComponent<UnityEngine.Camera>().enabled = false;
                        AudioListener listener = follow.GetComponent<AudioListener>();
                        if (listener != null) listener.enabled = false;
                    }
                root.SetActive(false);
            }
        }

        public bool SelectStage(int stage)
        {
            if (Progress == null || Battle != null || stage < 1 || stage > Progress.unlockedStage) return false;
            SelectedStage = stage;
            return true;
        }

        public bool TryEquip(int slot, int equipment)
        {
            if (Battle != null || Progress == null || !Progress.TryEquip(slot, equipment)) return false;
            dirty = true; Flush(); return true;
        }

        public bool StartBattle()
        {
            if (Progress == null || Battle != null || IsChoosing || inputActions == null || prototypeMaterial == null || auraShader == null)
                return false;
            Battle = PrototypeBattleBuilder.Build(SelectedStage, Progress, inputActions, prototypeMaterial, auraShader, transform, artStyle);
            Battle.Objective.Completed += OnCompleted;
            Battle.Waves.Spawned += RegisterEnemy;
            RegisterEnemy(Battle.Objective.EnemyBase);
            if (preparationCamera != null) preparationCamera.SetActive(false);
            Battle.Root.SetActive(true);
            LastResult = "";
            playtest.Begin(Battle, SelectedStage, Progress);
            return true;
        }

        private void RegisterEnemy(Combatant enemy) => enemy.Died += OnEnemyDefeated;

        private void OnEnemyDefeated(Combatant enemy)
        {
            if (Battle == null || enemy.LastAttacker == null || enemy.LastAttacker.Faction != Faction.Allied) return;
            int before = Progress.pendingLevels;
            Progress.GainExperience(enemy == Battle.Objective.EnemyBase ? 60 : 10 + SelectedStage * 2);
            dirty = true;
            if (before != Progress.pendingLevels) Flush();
        }

        private void OnCompleted(bool victory)
        {
            playtest.Finish(victory ? "victory" : "defeat");
            bool rewarded = victory && Progress.CompleteStage(SelectedStage);
            LastResult = victory
                ? rewarded ? "Victory! Earned first-clear gold, equipment and campaign unlocks." : "Victory! First-clear rewards were already claimed."
                : "Defeat. Your permanent growth and equipment are retained.";
            dirty = true;
            Flush();
        }

        public void SetPlaytestFeedback(int rating, string note) => playtest.SetFeedback(rating, note);

        public bool ChooseGrowth(int index)
        {
            if (Progress == null || !Progress.ChooseGrowth(index)) return false;
            if (Battle != null && Battle.Hero != null && Battle.Hero.IsAlive)
                Battle.Abilities.ApplyProgression(Progress.ranks);
            dirty = true; Flush();
            UpdatePause();
            return true;
        }

        public bool ReturnToPreparation()
        {
            if (Battle == null || !Battle.Objective.HasEnded || IsChoosing) return false;
            PlaytestStatus = playtest.Commit();
            simulation.Resume(true);
            Battle.Root.SetActive(false);
            if (Application.isPlaying) Destroy(Battle.Root); else DestroyImmediate(Battle.Root);
            Battle = null;
            if (preparationCamera != null) preparationCamera.SetActive(true);
            SelectedStage = Progress.unlockedStage;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            Flush();
            return true;
        }

        private void Update()
        {
            UpdatePause();
            playtest.Tick(Time.deltaTime, Battle == null || IsChoosing || simulation.IsPaused);
            saveTimer += Time.unscaledDeltaTime;
            if (saveTimer >= 2f) { saveTimer = 0; Flush(); }
        }

        public void UpdatePause()
        {
            if (Battle == null) return;
            if (IsChoosing) simulation.Pause(Battle.Root.transform);
            else if (simulation.IsPaused) simulation.Resume(Battle.Objective.HasEnded);
        }

        public void Flush()
        {
            if (dirty && store != null && store.Save(Progress)) dirty = false;
        }
        private void OnApplicationPause(bool paused) { if (paused) Flush(); }
        private void OnApplicationQuit() { playtest.Commit(); Flush(); }
        private void OnDestroy() { playtest.Commit(); Flush(); }
    }
}

using System.IO;
using Lightbringer.Combat;
using Lightbringer.Player;
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
        private bool userPaused;
        private float resumeTimeScale = 1f;
        private float saveTimer;
        private GameObject preparationCamera;
        private PlaytestRecorder playtest = new PlaytestRecorder(null);
        public CampaignProgress Progress { get; private set; }
        public PrototypeBattle Battle { get; private set; }
        public int SelectedStage { get; private set; } = 1;
        public string LastResult { get; private set; } = "";
        public int BattleGold { get; private set; }
        public bool TryUpgradeUnit(int unit)
        {
            if (Battle != null || Progress == null || !Progress.TryUpgradeUnit(unit)) return false;
            dirty = true; Flush(); return true;
        }
        public string SaveStatus => store == null ? "검증 세션" : store.Error;
        public bool IsBossCinematic => Battle != null && Battle.Encounter != null && Battle.Encounter.IsCinematic;
        public bool IsChoosing => !IsBossCinematic && Progress != null && Progress.pendingLevels > 0;
        public bool IsBattlePaused => simulation.IsPaused;
        public bool IsUserPaused => userPaused;
        public bool CanPause => Battle != null && !Battle.Objective.HasEnded && !IsChoosing && !IsBossCinematic;
        public PlaytestRecord PlaytestRecord => playtest.Current;
        public string PlaytestStatus { get; private set; } = "";
        public bool IsPersistentSession => store != null;
        public bool IsPrologueActive { get; set; }

        public void Configure(InputActionAsset actions, Material material, Shader ring)
        { inputActions = actions; prototypeMaterial = material; auraShader = ring; }

        public Lightbringer.Visuals.ArtStyleLibrary ArtStyle => artStyle;
        public void ConfigureArt(Lightbringer.Visuals.ArtStyleLibrary style) => artStyle = style;

        public void InitializeForValidation(CampaignProgress profile)
        { Progress = profile; store = null; SelectedStage = profile.unlockedStage; playtest = new PlaytestRecorder(null); }

        private void Awake()
        {
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
            if (Application.isPlaying && GetComponent<Lightbringer.Audio.SoundEffects>() == null)
                gameObject.AddComponent<Lightbringer.Audio.SoundEffects>();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public bool SelectStage(int stage)
        {
            if (Progress == null || Battle != null || IsPrologueActive || stage < 1 || stage > Progress.unlockedStage) return false;
            SelectedStage = stage;
            return true;
        }

        public bool TryEquip(int slot, int skill)
        {
            if (Battle != null || Progress == null || !Progress.TryEquip(slot, skill)) return false;
            dirty = true; Flush(); return true;
        }

        public bool TryUpgradeSkill(int skill)
        {
            if (Battle != null || Progress == null || !Progress.TryUpgradeSkill(skill)) return false;
            dirty = true; Flush(); return true;
        }

        public bool StartBattle()
        {
            if (Progress == null || Battle != null || IsPrologueActive || IsChoosing || inputActions == null || prototypeMaterial == null || auraShader == null)
                return false;
            Battle = PrototypeBattleBuilder.Build(SelectedStage, Progress, inputActions, prototypeMaterial, auraShader, transform, artStyle);
            Battle.Objective.Completed += OnCompleted;
            Battle.Waves.Spawned += RegisterEnemy;
            RegisterEnemy(Battle.Objective.EnemyBase);
            if (preparationCamera != null) preparationCamera.SetActive(false);
            Battle.Root.SetActive(true);
            if (Application.isPlaying)
            {
                Battle.Root.AddComponent<BattleMusic>().Configure(this);
                Battle.Root.AddComponent<Lightbringer.Audio.BattleAudio>().Configure(this);
            }
            LastResult = "";
            BattleGold = 0;
            playtest.Begin(Battle, SelectedStage, Progress);
            return true;
        }

        private void RegisterEnemy(Combatant enemy) => enemy.Died += OnEnemyDefeated;

        private void OnEnemyDefeated(Combatant enemy)
        {
            if (Battle == null || enemy.LastAttacker == null || enemy.LastAttacker.Faction != Faction.Allied) return;
            int before = Progress.pendingLevels;
            int reward = enemy == Battle.Objective.EnemyBase ? 60 : Battle.Waves.ExperienceFor(enemy);
            Progress.GainExperience(reward);
            Progress.GainGold(reward);
            BattleGold += reward;
            dirty = true;
            if (before != Progress.pendingLevels) Flush();
        }

        private void OnCompleted(bool victory)
        {
            playtest.Finish(victory ? "victory" : "defeat");
            int learned = SkillCatalog.Empty;
            bool rewarded = victory && Progress.CompleteStage(SelectedStage, out learned);
            if (victory) { int bonus = 100 + 50 * SelectedStage; Progress.GainGold(bonus); BattleGold += bonus; }
            string skillLine = SkillCatalog.IsSkill(learned) ? $"새 기술 '{SkillCatalog.Names[learned]}'을(를) 배웠습니다." : "";
            LastResult = victory
                ? rewarded ? "최초 승리! " + (skillLine != "" ? skillLine + " " : "") + "다음 콘텐츠를 해금했습니다." : "승리! 최초 승리 보상은 이미 획득했습니다."
                : "패배했습니다. 영구 성장과 기술은 유지됩니다.";
            if (victory && SelectedStage == 8)
                LastResult = "어둠의 군주가 물러났습니다. 봉화에 다시 빛이 돌아옵니다.\n"
                    + "군주: ‘이것으로 끝이라 생각하지 마라. 우리는 다시 만날 것이다.’\n"
                    + (rewarded ? (skillLine != "" ? skillLine : "최초 승리 보상을 획득했습니다.") : "최초 승리 보상은 이미 획득했습니다.");
            dirty = true;
            Flush();
        }

        public void SetPlaytestFeedback(int rating, string note) => playtest.SetFeedback(rating, note);

        public bool ChooseGrowth(int index)
        {
            if (Progress == null || !Progress.ChooseGrowth(index)) return false;
            if (Battle != null && Battle.Hero != null && Battle.Hero.IsAlive)
                Battle.Abilities.ApplyProgression(Progress.ranks, Progress.level);
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

        // Player pause (Esc / P or the pause screen's button): suspends the battle like a level-up choice and also
        // stops time, so animation, projectiles and effects freeze too.
        public bool SetPaused(bool paused)
        {
            if (paused == userPaused) return true;
            if (paused && !CanPause) return false;
            userPaused = paused;
            if (paused) { resumeTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f; Time.timeScale = 0f; }
            else Time.timeScale = resumeTimeScale;
            UpdatePause();
            return true;
        }

        public bool TogglePause() => SetPaused(!userPaused);

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && Battle != null && (keyboard.escapeKey.wasPressedThisFrame || keyboard.pKey.wasPressedThisFrame))
                TogglePause();
            UpdatePause();
            playtest.Tick(Time.deltaTime, Battle == null || IsChoosing || IsBossCinematic || simulation.IsPaused);
            saveTimer += Time.unscaledDeltaTime;
            if (saveTimer >= 2f) { saveTimer = 0; Flush(); }
        }

        public void UpdatePause()
        {
            if (Battle == null) return;
            if (IsChoosing || userPaused) simulation.Pause(Battle.Root.transform);
            else if (simulation.IsPaused) simulation.Resume(Battle.Objective.HasEnded);
        }

        public void Flush()
        {
            if (dirty && store != null && store.Save(Progress)) dirty = false;
        }
        private void OnApplicationPause(bool paused) { if (paused) Flush(); }
        private void OnApplicationQuit() { playtest.Commit(); Flush(); }
        private void OnDestroy()
        {
            if (userPaused) Time.timeScale = resumeTimeScale;
            playtest.Commit(); Flush();
        }
    }
}

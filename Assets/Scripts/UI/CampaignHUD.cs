using Lightbringer.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UIElements;

namespace Lightbringer.UI
{
    // Hosts the UI Toolkit campaign UI (Assets/UI): preparation, battle HUD, level-up and result screens.
    [RequireComponent(typeof(CampaignSession))]
    public sealed class CampaignHUD : MonoBehaviour
    {
        public const string PanelSettingsPath = "Assets/UI/LightbringerPanelSettings.asset";
        public const string StyleSheetPath = "Assets/UI/LightbringerHUD.uss";

        [SerializeField] private PanelSettings panelSettings;
        [SerializeField] private StyleSheet styleSheet;
        private CampaignSession session;
        private GameObject panelObject;
        private PrototypeBattle shownBattle;
        private bool showingPreparation, showingTitle;
        private VisualElement campaignRoot;
        private PrologueSequence prologueSequence;
        private AudioSource prologueMusic;
        private AudioSource titleMusic;
        private float titleMusicGain;
        private bool checkedPrologue;
        public PrologueView Prologue { get; private set; }
        public CampaignMapView Map { get; private set; }
        public TitleView Title { get; private set; }

        public PreparationView Preparation { get; private set; }
        public BattleHudView BattleView { get; private set; }
        public LevelUpView LevelUp { get; private set; }
        public ResultView Result { get; private set; }
        public PauseView Pause { get; private set; }
        public BossDialogueView BossDialogue { get; private set; }

        public void Configure(PanelSettings panel, StyleSheet style)
        {
            panelSettings = panel; styleSheet = style;
            // Edit-mode setup only stores the references; the panel exists at runtime.
            if (Application.isPlaying && isActiveAndEnabled) { OnDisable(); OnEnable(); }
        }

        private void Awake() => session = GetComponent<CampaignSession>();

        private void OnEnable()
        {
#if UNITY_EDITOR
            // Scenes and probes that predate the UI assets still get the HUD inside the Editor.
            if (panelSettings == null) panelSettings = UnityEditor.AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (styleSheet == null) styleSheet = UnityEditor.AssetDatabase.LoadAssetAtPath<StyleSheet>(StyleSheetPath);
#endif
            if (panelSettings == null || styleSheet == null)
            {
                Debug.LogWarning("CampaignHUD needs the Lightbringer PanelSettings and HUD style sheet (Lightbringer/UI/Build HUD Assets).", this);
                return;
            }
            panelObject = new GameObject("HUD Panel");
            panelObject.SetActive(false);
            panelObject.transform.SetParent(transform, false);
            UIDocument document = panelObject.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            panelObject.SetActive(true);
            VisualElement root = document.rootVisualElement;
            root.styleSheets.Add(styleSheet);
            root.pickingMode = PickingMode.Ignore;
            // Creation order is draw order: the level-up cards must sit above preparation and the battle HUD.
            campaignRoot = Hud.Box(root, "lb-campaign-content");
            campaignRoot.pickingMode = PickingMode.Ignore;
            Preparation = new PreparationView(campaignRoot);
            Preparation.BackRequested += () => showingPreparation = false;
            Map = new CampaignMapView(campaignRoot, () => showingPreparation = true, () => { showingTitle = true; showingPreparation = false; });
            Title = new TitleView(campaignRoot);
            Title.StartRequested += StartGame;
            Title.PrologueRequested += () => PlayPrologue();
            Title.QuitRequested += QuitGame;
            showingPreparation = false;
            BattleView = new BattleHudView(campaignRoot);
            LevelUp = new LevelUpView(campaignRoot);
            Result = new ResultView(campaignRoot);
            Pause = new PauseView(campaignRoot);
            BossDialogue = new BossDialogueView(campaignRoot);
            Prologue = new PrologueView(root, FinishPrologue);
            checkedPrologue = false;
            shownBattle = null;
            BattleView.Bind(null);
            // UI Toolkit buttons receive clicks through an Input System event system.
            if (Application.isPlaying && FindAnyObjectByType<EventSystem>() == null)
            {
                GameObject events = new GameObject("HUD Event System", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(panelObject.transform, false);
            }
        }

        private void OnDisable()
        {
            if (session != null) session.IsPrologueActive = false;
            if (prologueMusic != null) prologueMusic.Stop();
            RefreshTitleMusic(false);
            if (panelObject != null)
            {
                if (Application.isPlaying) Destroy(panelObject); else DestroyImmediate(panelObject);
            }
            panelObject = null;
            prologueMusic = null;
            titleMusic = null;
            Prologue = null;
            campaignRoot = null;
            Preparation = null; Map = null; Title = null; BattleView = null; LevelUp = null; Result = null; Pause = null;
            BossDialogue = null;
        }

        private void LateUpdate()
        {
            if (session == null || BattleView == null) return;
            if (!checkedPrologue)
            {
                checkedPrologue = true;
                showingTitle = Application.isPlaying && session.IsPersistentSession;
            }
            RefreshTitleMusic(showingTitle && session.Battle == null && !(Prologue != null && Prologue.IsOpen));
            if (Prologue != null && Prologue.IsOpen)
            {
                Prologue.Tick(Time.unscaledDeltaTime);
                if (prologueMusic != null)
                    prologueMusic.volume = Mathf.MoveTowards(prologueMusic.volume,
                        BattleMusic.Volume * Prologue.MusicGain, Time.unscaledDeltaTime);
                return;
            }
            if (showingTitle && session.Battle == null)
            {
                Preparation.Refresh(session, false);
                Map.Refresh(session, false);
                Title.Refresh(true);
                return;
            }
            Title.Refresh(false);
            if (session.Battle != shownBattle)
            {
                showingPreparation = false;
                shownBattle = session.Battle;
                BattleView.Bind(shownBattle);
            }
            Preparation.Refresh(session, showingPreparation);
            Map.Refresh(session, !showingPreparation);
            BattleView.Refresh(session);
            BattleView.RefreshHealthBars();
            LevelUp.Refresh(session);
            Result.Refresh(session);
            Pause.Refresh(session);
            BossDialogue.Refresh(session);
            Hud.Show(BattleView.Root, session.Battle != null && !session.IsBossCinematic);
        }

        private void RefreshTitleMusic(bool playing)
        {
            if (!playing)
            {
                if (titleMusic != null && titleMusic.isPlaying) titleMusic.Stop();
                titleMusicGain = 0f;
                return;
            }
            if (!Application.isPlaying || panelObject == null) return;
            if (titleMusic == null)
            {
                titleMusic = panelObject.AddComponent<AudioSource>();
                titleMusic.playOnAwake = false;
                titleMusic.spatialBlend = 0f;
                titleMusic.dopplerLevel = 0f;
                titleMusic.priority = 0;
                titleMusic.loop = true;
                titleMusic.volume = 0f;
                titleMusic.clip = UnityEngine.Resources.Load<AudioClip>(BattleMusic.ResourcePath);
                if (titleMusic.clip == null)
                    Debug.LogWarning("시작 화면 음악을 불러오지 못했습니다: " + BattleMusic.ResourcePath, this);
            }
            if (titleMusic.clip == null) return;
            if (!titleMusic.isPlaying)
            {
                titleMusic.volume = 0f;
                titleMusic.Play();
            }
            titleMusicGain = Mathf.MoveTowards(titleMusicGain, 1f, Time.unscaledDeltaTime / 1.2f);
            titleMusic.volume = BattleMusic.Volume * titleMusicGain;
        }

        private void StartGame()
        {
            if (session == null || session.IsPrologueActive) return;
            showingTitle = false;
            RefreshTitleMusic(false);
            showingPreparation = false;
            if (Application.isPlaying && PlayerPrefs.GetInt(PrologueSequence.ViewedKey, 0) == 0) PlayPrologue();
        }

        private static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public bool PlayPrologue()
        {
            if (session == null || session.Battle != null || session.IsChoosing || Prologue == null) return false;
            if (prologueSequence == null)
                prologueSequence = UnityEngine.Resources.Load<PrologueSequence>(PrologueSequence.ResourcePath);
            if (!Prologue.Open(prologueSequence)) return false;
            RefreshTitleMusic(false);
            session.IsPrologueActive = true;
            showingPreparation = false;
            Hud.Show(campaignRoot, false);
            if (Application.isPlaying && prologueSequence.music != null)
            {
                if (prologueMusic == null) prologueMusic = panelObject.AddComponent<AudioSource>();
                prologueMusic.playOnAwake = false;
                prologueMusic.spatialBlend = 0f;
                prologueMusic.loop = true;
                prologueMusic.volume = 0f;
                prologueMusic.clip = prologueSequence.music;
                prologueMusic.Play();
            }
            return true;
        }

        private void FinishPrologue()
        {
            if (prologueMusic != null) prologueMusic.Stop();
            if (session != null)
            {
                session.IsPrologueActive = false;
                if (Application.isPlaying && session.IsPersistentSession)
                {
                    PlayerPrefs.SetInt(PrologueSequence.ViewedKey, 1);
                    PlayerPrefs.Save();
                }
            }
            showingPreparation = false;
            Hud.Show(campaignRoot, true);
        }
    }
}

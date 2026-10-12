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
        private bool showingPreparation;
        private VisualElement campaignRoot;
        private PrologueSequence prologueSequence;
        private AudioSource prologueMusic;
        private bool checkedPrologue;
        public PrologueView Prologue { get; private set; }
        public CampaignMapView Map { get; private set; }

        public PreparationView Preparation { get; private set; }
        public BattleHudView BattleView { get; private set; }
        public LevelUpView LevelUp { get; private set; }
        public ResultView Result { get; private set; }
        public PauseView Pause { get; private set; }

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
            Map = new CampaignMapView(campaignRoot, () => showingPreparation = true, () => PlayPrologue());
            showingPreparation = false;
            BattleView = new BattleHudView(campaignRoot);
            LevelUp = new LevelUpView(campaignRoot);
            Result = new ResultView(campaignRoot);
            Pause = new PauseView(campaignRoot);
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
            if (panelObject != null)
            {
                if (Application.isPlaying) Destroy(panelObject); else DestroyImmediate(panelObject);
            }
            panelObject = null;
            prologueMusic = null;
            Prologue = null;
            campaignRoot = null;
            Preparation = null; Map = null; BattleView = null; LevelUp = null; Result = null; Pause = null;
        }

        private void LateUpdate()
        {
            if (session == null || BattleView == null) return;
            if (!checkedPrologue)
            {
                checkedPrologue = true;
                if (Application.isPlaying && session.IsPersistentSession && PlayerPrefs.GetInt(PrologueSequence.ViewedKey, 0) == 0)
                    PlayPrologue();
            }
            if (Prologue != null && Prologue.IsOpen)
            {
                Prologue.Tick(Time.unscaledDeltaTime);
                if (prologueMusic != null)
                    prologueMusic.volume = Mathf.MoveTowards(prologueMusic.volume,
                        BattleMusic.Volume * Prologue.MusicGain, Time.unscaledDeltaTime);
                return;
            }
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
        }

        public bool PlayPrologue()
        {
            if (session == null || session.Battle != null || session.IsChoosing || Prologue == null) return false;
            if (prologueSequence == null)
                prologueSequence = UnityEngine.Resources.Load<PrologueSequence>(PrologueSequence.ResourcePath);
            if (!Prologue.Open(prologueSequence)) return false;
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

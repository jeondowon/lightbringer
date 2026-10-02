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

        public PreparationView Preparation { get; private set; }
        public BattleHudView BattleView { get; private set; }
        public LevelUpView LevelUp { get; private set; }
        public ResultView Result { get; private set; }

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
            Preparation = new PreparationView(root);
            BattleView = new BattleHudView(root);
            LevelUp = new LevelUpView(root);
            Result = new ResultView(root);
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
            if (panelObject != null)
            {
                if (Application.isPlaying) Destroy(panelObject); else DestroyImmediate(panelObject);
            }
            panelObject = null;
            Preparation = null; BattleView = null; LevelUp = null; Result = null;
        }

        private void LateUpdate()
        {
            if (session == null || BattleView == null) return;
            if (session.Battle != shownBattle)
            {
                shownBattle = session.Battle;
                BattleView.Bind(shownBattle);
            }
            Preparation.Refresh(session);
            BattleView.Refresh(session);
            BattleView.RefreshHealthBars();
            LevelUp.Refresh(session);
            Result.Refresh(session);
        }
    }
}

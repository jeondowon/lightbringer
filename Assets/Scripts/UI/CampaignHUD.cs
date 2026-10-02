using Lightbringer.Core;
using Lightbringer.Player;
using Lightbringer.Progression;
using Lightbringer.Units;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UIElements;

namespace Lightbringer.UI
{
    // Battle HUD, level-up and result screens run on UI Toolkit (Assets/UI). The preparation screen is still
    // the OnGUI prototype until it is rebuilt (Roadmap Step 2).
    [RequireComponent(typeof(CampaignSession))]
    public sealed class CampaignHUD : MonoBehaviour
    {
        public const string PanelSettingsPath = "Assets/UI/LightbringerPanelSettings.asset";
        public const string StyleSheetPath = "Assets/UI/LightbringerHUD.uss";

        [SerializeField] private PanelSettings panelSettings;
        [SerializeField] private StyleSheet styleSheet;
        private CampaignSession session;
        private GameObject panelObject;
        private Vector2 scroll;
        private PrototypeBattle shownBattle;

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
            BattleView = null; LevelUp = null; Result = null;
        }

        private void LateUpdate()
        {
            if (session == null || BattleView == null) return;
            if (session.Battle != shownBattle)
            {
                shownBattle = session.Battle;
                BattleView.Bind(shownBattle);
            }
            BattleView.Refresh(session);
            BattleView.RefreshHealthBars();
            LevelUp.Refresh(session);
            Result.Refresh(session);
        }

        private void OnGUI()
        {
            if (session == null || session.Progress == null || session.Battle != null) return;
            // IMGUI draws over UI Toolkit, so hide preparation while the level-up cards are open.
            if (!session.IsChoosing) Preparation(session.Progress);
            string status = string.IsNullOrEmpty(session.PlaytestStatus) ? session.SaveStatus
                : session.PlaytestStatus + " " + session.SaveStatus;
            if (!string.IsNullOrEmpty(status))
                GUI.Box(new Rect(16, Screen.height - 48, Screen.width - 32, 38), status);
        }

        private void Preparation(CampaignProgress profile)
        {
            float width = Mathf.Min(700, Screen.width - 32);
            GUILayout.BeginArea(new Rect((Screen.width - width) / 2, 20, width, Screen.height - 80), GUI.skin.box);
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label("LIGHTBRINGER | CAMPAIGN PREPARATION");
            GUILayout.Label($"Level {profile.level} | EXP {profile.experience}/{profile.ExperienceToNext} | Gold {profile.gold}");
            GUILayout.Label(profile.IsComplete ? "Prototype campaign complete. All stages can be replayed." : "Choose a stage and three equipment items.");
            GUI.enabled = !session.IsChoosing;
            GUILayout.BeginHorizontal();
            for (int stage = 1; stage <= CampaignProgress.StageCount; stage++)
            {
                GUI.enabled = !session.IsChoosing && stage <= profile.unlockedStage;
                if (GUILayout.Button((stage == session.SelectedStage ? "> " : "") + stage + (profile.cleared[stage - 1] ? " *" : "")))
                    session.SelectStage(stage);
            }
            GUILayout.EndHorizontal();
            GUI.enabled = !session.IsChoosing;
            for (int slot = 0; slot < EquipmentCatalog.Slots; slot++)
            {
                GUILayout.Label($"Slot {slot + 1} ({(slot == 0 ? "LMB" : slot == 1 ? "Q" : "E")}): {EquipmentCatalog.Names[profile.loadout[slot]]}");
                GUILayout.BeginHorizontal();
                for (int id = 0; id < EquipmentCatalog.Count; id++)
                {
                    GUI.enabled = !session.IsChoosing && profile.equipmentLevels[id] > 0;
                    if (GUILayout.Button(EquipmentCatalog.Names[id] + " +" + profile.equipmentLevels[id])) session.TryEquip(slot, id);
                }
                GUILayout.EndHorizontal();
            }
            GUI.enabled = !session.IsChoosing;
            GUILayout.Label("Unlocked troops (all available during battle):");
            for (int i = 0; i < profile.UnlockedUnits; i++) GUILayout.Label(UnitCatalog.Names[i] + " — " + UnitCatalog.Cost((UnitKind)i) + " Food");
            if (GUILayout.Button("START STAGE " + session.SelectedStage, GUILayout.Height(38))) session.StartBattle();
            GUI.enabled = true;
            GUILayout.Label("Permanent growth:");
            for (int i = 0; i < profile.ranks.Length; i++) if (profile.ranks[i] > 0)
                GUILayout.Label(CampaignProgress.GrowthNames[i] + " × " + profile.ranks[i]);
            GUILayout.Label("Prototype balance. First clears award gold and gear; upgraded gear replaces lower levels.");
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}

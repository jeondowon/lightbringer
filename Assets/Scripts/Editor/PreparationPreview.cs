using System;
using System.IO;
using Lightbringer.Combat;
using Lightbringer.Core;
using Lightbringer.Pathing;
using Lightbringer.Progression;
using Lightbringer.Resources;
using Lightbringer.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Lightbringer.EditorTools
{
    // Renders campaign screens with a sample mid-campaign profile into Docs/Validation without entering Play mode:
    // the preparation screen, the level-up blessing cards and the battle HUD. The profile is memory-only and never touches
    // the real save.
    [InitializeOnLoad]
    public static class PreparationPreview
    {
        private const string Request = "Docs/Validation/PreparationPreview.request";
        private const string LevelUpRequest = "Docs/Validation/LevelUpPreview.request";
        private const string Output = "Docs/Validation/PreparationPreview.png";
        private const string LevelUpOutput = "Docs/Validation/LevelUpPreview.png";
        private const string BattleHudRequest = "Docs/Validation/BattleHudPreview.request";
        private const string BattleHudOutput = "Docs/Validation/BattleHudPreview.png";
        private const string BattleHudBackdrop = "Docs/ArtPass/Preview/Battlefield_Hero.png";
        private const int Width = 1920, Height = 1080;
        // Every hidden object a capture creates carries this name prefix so Purge can find it again.
        private const string Prefix = "Campaign preview";
        private static EditorApplication.CallbackFunction pending;

        static PreparationPreview()
        {
            EditorRequests.Register(Request, Capture);
            EditorRequests.Register(LevelUpRequest, CaptureLevelUp);
            EditorRequests.Register(BattleHudRequest, CaptureBattleHud);
            // A capture cut short by a domain reload or Play mode would leave its hidden battlefield behind: it then runs
            // in Play mode and its units block spawn slots in Greybox validation, which builds at the same coordinates.
            EditorApplication.delayCall += Purge;
            AssemblyReloadEvents.beforeAssemblyReload += Purge;
            EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.ExitingEditMode) Purge(); };
        }

        // Aborts any capture in progress and destroys every hidden object a capture created.
        internal static void Purge()
        {
            if (pending != null) EditorApplication.update -= pending;
            pending = null;
            foreach (Object item in UnityEngine.Resources.FindObjectsOfTypeAll<Object>())
            {
                // Components go with their GameObject; destroying one directly (a Transform) is an error.
                if (item == null || item is Component || item.hideFlags != HideFlags.HideAndDontSave
                    || !item.name.StartsWith(Prefix, StringComparison.Ordinal))
                    continue;
                if (item is RenderTexture texture) texture.Release();
                Object.DestroyImmediate(item);
            }
        }

        [MenuItem("Lightbringer/UI/Capture Preparation Preview")]
        public static void Capture() => Render(Output, profile => { }, (root, session) =>
        {
            PreparationView view = new PreparationView(root);
            return () => view.Refresh(session);
        });

        [MenuItem("Lightbringer/UI/Capture Level-Up Preview")]
        public static void CaptureLevelUp() => Render(LevelUpOutput, profile =>
        {
            profile.ranks[(int)GrowthKind.AuraSize] = 2;
            profile.GainExperience(profile.ExperienceToNext - profile.experience + 140);
            // A fixed, varied hand so the preview shows three categories.
            profile.choices = new[] { (int)GrowthKind.AuraSize, (int)GrowthKind.Leadership, (int)GrowthKind.HeroVitality };
        }, (root, session) =>
        {
            // The preparation screen stands in for the paused battle behind the overlay.
            PreparationView preparation = new PreparationView(root);
            LevelUpView view = new LevelUpView(root);
            return () =>
            {
                preparation.Refresh(session);
                view.Refresh(session);
                view.SkipIntro();
            };
        });

        // Stage 8 (three Paths) with PATH 2 selected and a staged spread of troops, so the battlefield map shows
        // every marker. Only the UI is rendered; the 3D battlefield behind it stays black.
        [MenuItem("Lightbringer/UI/Capture Battle HUD Preview")]
        public static void CaptureBattleHud() => Render(BattleHudOutput, profile =>
        {
            for (int stage = 4; stage < CampaignProgress.StageCount; stage++) profile.CompleteStage(stage);
        }, (root, session) =>
        {
            if (!session.StartBattle()) { Debug.LogError("Battle HUD preview could not start a battle."); return () => { }; }
            PrototypeBattle battle = session.Battle;
            battle.Summoner.TryCyclePath();
            Transform markers = new GameObject("Preview troops").transform;
            markers.SetParent(battle.Root.transform, false);
            WaypointPath[] paths = battle.Paths;
            Place(markers, Faction.Allied, paths[0], 0.15f, 0.3f, 2);
            Place(markers, Faction.Enemy, paths[0], 0.45f, 0.65f, 5);
            Place(markers, Faction.Allied, paths[1], 0.2f, 0.6f, 6);
            Place(markers, Faction.Enemy, paths[1], 0.7f, 0.8f, 2);
            Place(markers, Faction.Allied, paths[2], 0.3f, 0.4f, 1);
            battle.Hero.transform.position = Along(paths[1], 0.12f);
            battle.Hero.transform.rotation = Quaternion.LookRotation(new Vector3(0.4f, 0f, 1f));
            battle.Root.GetComponent<BattlefieldReadability>().Scan();
            StageMidBattle(battle);
            AddBackdrop(root, BattleHudBackdrop);
            BattleHudView view = new BattleHudView(root);
            view.Bind(battle);
            return () => view.Refresh(session);
        });

        // Some Food banked (the cheaper troops are ready), mana partly spent and the Healing Staff recharging,
        // so every card and orb state appears in one capture.
        private static void StageMidBattle(PrototypeBattle battle)
        {
            FoodResource food = battle.Hero.GetComponent<FoodResource>();
            ManaResource mana = battle.Hero.GetComponent<ManaResource>();
            if (food.ProductionPerSecond > 0f)
                typeof(FoodResource).GetMethod("GenerateFood", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    ?.Invoke(food, new object[] { 34f / food.ProductionPerSecond });
            mana.TrySpend(mana.Current);
            if (mana.RecoveryPerSecond > 0f) mana.Tick(80f / mana.RecoveryPerSecond);
            battle.Abilities.TryCast(1, battle.Hero);
            battle.Abilities.Tick(1.8f);
        }

        // Only the UI is rendered, so a saved gameplay screenshot stands in for the 3D battlefield behind the HUD.
        private static void AddBackdrop(VisualElement root, string screenshot)
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", screenshot));
            if (!File.Exists(path)) return;
            Texture2D texture = new Texture2D(2, 2) { name = Prefix + " backdrop", hideFlags = HideFlags.HideAndDontSave };
            texture.LoadImage(File.ReadAllBytes(path));
            VisualElement backdrop = new VisualElement();
            backdrop.style.position = Position.Absolute;
            backdrop.style.left = backdrop.style.top = backdrop.style.right = backdrop.style.bottom = 0;
            backdrop.style.backgroundImage = texture;
            backdrop.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop;
            root.Add(backdrop);
        }

        private static void Place(Transform parent, Faction faction, WaypointPath path, float from, float to, int count)
        {
            for (int i = 0; i < count; i++)
            {
                GameObject marker = new GameObject(faction + " marker");
                marker.transform.SetParent(parent, false);
                float t = count > 1 ? Mathf.Lerp(from, to, i / (count - 1f)) : from;
                marker.transform.position = Along(path, t) + new Vector3((i % 2 == 0 ? -1f : 1f) * 1.2f, 0f, 0f);
                marker.AddComponent<Combatant>().Configure(faction, 100f);
            }
        }

        // Linear position along a Path, 0 at its first waypoint and 1 at its last.
        private static Vector3 Along(WaypointPath path, float t)
        {
            float scaled = Mathf.Clamp01(t) * (path.Count - 1);
            int index = Mathf.Min(Mathf.FloorToInt(scaled), path.Count - 2);
            return Vector3.Lerp(path.GetPosition(index), path.GetPosition(index + 1), scaled - index);
        }

        private static void Render(string output, Action<CampaignProgress> customize, Func<VisualElement, CampaignSession, Action> build)
        {
            // The script watcher ignores style sheet edits; import them before rendering.
            AssetDatabase.Refresh();
            PanelSettings source = HudSetup.EnsureAssets();
            StyleSheet style = AssetDatabase.LoadAssetAtPath<StyleSheet>(CampaignHUD.StyleSheetPath);
            if (source == null || style == null) { Debug.LogError("Campaign preview needs the HUD assets."); return; }
            // One capture at a time; also clears anything an earlier capture that threw left behind.
            Purge();

            // HideAndDontSave keeps asset unloading (e.g. after an art reimport) from destroying these mid-capture.
            RenderTexture target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
                { name = Prefix + " target", hideFlags = HideFlags.HideAndDontSave };
            PanelSettings panel = Object.Instantiate(source);
            panel.name = Prefix + " panel settings";
            panel.hideFlags = HideFlags.HideAndDontSave;
            panel.targetTexture = target;
            panel.clearColor = true;
            panel.colorClearValue = Color.black;
            Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"))
                { name = Prefix + " material", hideFlags = HideFlags.HideAndDontSave };

            GameObject host = new GameObject(Prefix) { hideFlags = HideFlags.HideAndDontSave };
            CampaignSession session = host.AddComponent<CampaignSession>();
            CampaignProgress profile = new CampaignProgress();
            for (int stage = 1; stage <= 3; stage++) profile.CompleteStage(stage);
            profile.level = 4;
            profile.experience = 52;
            profile.ranks[(int)GrowthKind.ManaRecovery] = 2;
            profile.ranks[(int)GrowthKind.AuraSize] = 1;
            customize(profile);
            session.InitializeForValidation(profile);
            EditorAssets.ConfigureSession(session, material);

            GameObject documentObject = new GameObject(Prefix + " document") { hideFlags = HideFlags.HideAndDontSave };
            documentObject.SetActive(false);
            UIDocument document = documentObject.AddComponent<UIDocument>();
            document.panelSettings = panel;
            documentObject.SetActive(true);
            VisualElement root = document.rootVisualElement;
            root.styleSheets.Add(style);
            Action refresh = build(root, session);

            int frames = 0;
            EditorApplication.CallbackFunction tick = null;
            tick = () =>
            {
                // A few player-loop updates let layout, style transitions and the panel repaint settle.
                if (++frames < 20)
                {
                    refresh();
                    // Runtime panels only repaint inside the Play-mode player loop; drive them directly.
                    InvokeRuntime("UpdatePanels", frames == 1);
                    InvokeRuntime("RepaintPanels", frames == 1);
                    InvokeRuntime("RenderOffscreenPanels", frames == 1);
                    return;
                }
                EditorApplication.update -= tick;
                try
                {
                    // Reading with a destroyed target would silently capture whatever view is active instead.
                    if (target == null) { Debug.LogError("Campaign preview render target was destroyed; nothing saved."); return; }
                    RenderTexture previous = RenderTexture.active;
                    RenderTexture.active = target;
                    Texture2D image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                    image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                    image.Apply();
                    RenderTexture.active = previous;
                    string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", output));
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllBytes(path, image.EncodeToPNG());
                    Object.DestroyImmediate(image);
                    Debug.Log("Campaign preview saved to " + output);
                }
                finally { Purge(); }
            };
            pending = tick;
            EditorApplication.update += tick;
        }

        private static void InvokeRuntime(string name, bool log)
        {
            Type utility = typeof(PanelSettings).Assembly.GetType("UnityEngine.UIElements.UIElementsRuntimeUtility");
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Static
                | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            System.Reflection.MethodInfo method = utility?.GetMethod(name, flags);
            if (method == null)
            {
                if (log) Debug.LogWarning("Campaign preview: UIElementsRuntimeUtility." + name + " not found");
                return;
            }
            object[] arguments = Array.ConvertAll(method.GetParameters(),
                p => p.ParameterType == typeof(bool) ? (object)false : p.HasDefaultValue ? p.DefaultValue : null);
            method.Invoke(null, arguments);
        }
    }
}

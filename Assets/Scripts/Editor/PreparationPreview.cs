using System;
using System.IO;
using Lightbringer.Core;
using Lightbringer.Progression;
using Lightbringer.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Lightbringer.EditorTools
{
    // Renders campaign screens with a sample mid-campaign profile into Docs/Validation without entering Play mode:
    // the preparation screen and the level-up blessing cards. The profile is memory-only and never touches the real save.
    [InitializeOnLoad]
    public static class PreparationPreview
    {
        private const string Request = "Docs/Validation/PreparationPreview.request";
        private const string LevelUpRequest = "Docs/Validation/LevelUpPreview.request";
        private const string Output = "Docs/Validation/PreparationPreview.png";
        private const string LevelUpOutput = "Docs/Validation/LevelUpPreview.png";
        private const int Width = 1920, Height = 1080;

        static PreparationPreview()
        {
            EditorRequests.Register(Request, Capture);
            EditorRequests.Register(LevelUpRequest, CaptureLevelUp);
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

        private static void Render(string output, Action<CampaignProgress> customize, Func<VisualElement, CampaignSession, Action> build)
        {
            // The script watcher ignores style sheet edits; import them before rendering.
            AssetDatabase.Refresh();
            PanelSettings source = HudSetup.EnsureAssets();
            StyleSheet style = AssetDatabase.LoadAssetAtPath<StyleSheet>(CampaignHUD.StyleSheetPath);
            if (source == null || style == null) { Debug.LogError("Campaign preview needs the HUD assets."); return; }

            // HideAndDontSave keeps asset unloading (e.g. after an art reimport) from destroying these mid-capture.
            RenderTexture target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
                { hideFlags = HideFlags.HideAndDontSave };
            PanelSettings panel = Object.Instantiate(source);
            panel.hideFlags = HideFlags.HideAndDontSave;
            panel.targetTexture = target;
            panel.clearColor = true;
            panel.colorClearValue = Color.black;
            Material material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { hideFlags = HideFlags.HideAndDontSave };

            GameObject host = new GameObject("Campaign preview") { hideFlags = HideFlags.HideAndDontSave };
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

            GameObject documentObject = new GameObject("Preview Panel") { hideFlags = HideFlags.HideAndDontSave };
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
                finally
                {
                    Object.DestroyImmediate(documentObject);
                    Object.DestroyImmediate(host);
                    Object.DestroyImmediate(panel);
                    Object.DestroyImmediate(material);
                    if (target != null) { target.Release(); Object.DestroyImmediate(target); }
                }
            };
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

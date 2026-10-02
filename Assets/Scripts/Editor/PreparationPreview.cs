using System.IO;
using Lightbringer.Core;
using Lightbringer.Progression;
using Lightbringer.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Lightbringer.EditorTools
{
    // Renders the preparation screen with a sample mid-campaign profile into Docs/Validation/PreparationPreview.png
    // without entering Play mode. The profile is memory-only and never touches the real save.
    [InitializeOnLoad]
    public static class PreparationPreview
    {
        private const string Request = "Docs/Validation/PreparationPreview.request";
        private const string Output = "Docs/Validation/PreparationPreview.png";
        private const int Width = 1920, Height = 1080;

        static PreparationPreview() => EditorRequests.Register(Request, Capture);

        [MenuItem("Lightbringer/UI/Capture Preparation Preview")]
        public static void Capture()
        {
            // The script watcher ignores style sheet edits; import them before rendering.
            AssetDatabase.Refresh();
            PanelSettings source = HudSetup.EnsureAssets();
            StyleSheet style = AssetDatabase.LoadAssetAtPath<StyleSheet>(CampaignHUD.StyleSheetPath);
            if (source == null || style == null) { Debug.LogError("Preparation preview needs the HUD assets."); return; }

            RenderTexture target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            PanelSettings panel = Object.Instantiate(source);
            panel.targetTexture = target;
            panel.clearColor = true;
            panel.colorClearValue = Color.black;
            Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));

            GameObject host = new GameObject("Preparation preview") { hideFlags = HideFlags.HideAndDontSave };
            CampaignSession session = host.AddComponent<CampaignSession>();
            CampaignProgress profile = new CampaignProgress();
            for (int stage = 1; stage <= 3; stage++) profile.CompleteStage(stage);
            profile.level = 4;
            profile.experience = 52;
            profile.ranks[(int)GrowthKind.ManaRecovery] = 2;
            profile.ranks[(int)GrowthKind.AuraSize] = 1;
            session.InitializeForValidation(profile);
            EditorAssets.ConfigureSession(session, material);

            GameObject documentObject = new GameObject("Preview Panel") { hideFlags = HideFlags.HideAndDontSave };
            documentObject.SetActive(false);
            UIDocument document = documentObject.AddComponent<UIDocument>();
            document.panelSettings = panel;
            documentObject.SetActive(true);
            VisualElement root = document.rootVisualElement;
            root.styleSheets.Add(style);
            PreparationView view = new PreparationView(root);

            int frames = 0;
            EditorApplication.CallbackFunction tick = null;
            tick = () =>
            {
                // A few player-loop updates let layout, style transitions and the panel repaint settle.
                if (++frames < 20)
                {
                    view.Refresh(session);
                    // Runtime panels only repaint inside the Play-mode player loop; drive them directly.
                    InvokeRuntime("UpdatePanels", frames == 1);
                    InvokeRuntime("RepaintPanels", frames == 1);
                    InvokeRuntime("RenderOffscreenPanels", frames == 1);
                    return;
                }
                EditorApplication.update -= tick;
                try
                {
                    RenderTexture previous = RenderTexture.active;
                    RenderTexture.active = target;
                    Texture2D image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                    image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                    image.Apply();
                    RenderTexture.active = previous;
                    string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", Output));
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllBytes(path, image.EncodeToPNG());
                    Object.DestroyImmediate(image);
                    Debug.Log("Preparation preview saved to " + Output);
                }
                finally
                {
                    Object.DestroyImmediate(documentObject);
                    Object.DestroyImmediate(host);
                    Object.DestroyImmediate(panel);
                    Object.DestroyImmediate(material);
                    target.Release();
                    Object.DestroyImmediate(target);
                }
            };
            EditorApplication.update += tick;
        }

        private static void InvokeRuntime(string name, bool log)
        {
            System.Type utility = typeof(PanelSettings).Assembly.GetType("UnityEngine.UIElements.UIElementsRuntimeUtility");
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Static
                | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            System.Reflection.MethodInfo method = utility?.GetMethod(name, flags);
            if (method == null)
            {
                if (log) Debug.LogWarning("Preparation preview: UIElementsRuntimeUtility." + name + " not found");
                return;
            }
            object[] arguments = System.Array.ConvertAll(method.GetParameters(),
                p => p.ParameterType == typeof(bool) ? (object)false : p.HasDefaultValue ? p.DefaultValue : null);
            method.Invoke(null, arguments);
        }
    }
}

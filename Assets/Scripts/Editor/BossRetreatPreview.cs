using System;
using System.IO;
using System.Reflection;
using Lightbringer.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Lightbringer.EditorTools
{
    [InitializeOnLoad]
    public static class BossRetreatPreview
    {
        private static GameObject host;
        private static PanelSettings panel;
        private static RenderTexture target;
        private static EditorApplication.CallbackFunction pending;
        static BossRetreatPreview()
        {
            EditorRequests.Register("Docs/Validation/BossRetreatPreview.request", Capture);
            AssemblyReloadEvents.beforeAssemblyReload += Cleanup;
            EditorApplication.update += CaptureDuringPlay;
        }

        // The preview owns only a hidden offscreen UI document; it need not stop the user's battle.
        private static void CaptureDuringPlay()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating || pending != null) return;
            const string request = "Docs/Validation/BossRetreatPreview.request";
            if (!File.Exists(request)) return;
            File.Delete(request);
            try { Capture(); }
            catch (Exception error) { Cleanup(); Debug.LogException(error); }
        }

        [MenuItem("Lightbringer/Boss Story/Capture Retreat Motion")]
        public static void Capture()
        {
            Cleanup();
            target = new RenderTexture(1672, 941, 24) { hideFlags = HideFlags.HideAndDontSave };
            panel = Object.Instantiate(HudSetup.EnsureAssets());
            panel.hideFlags = HideFlags.HideAndDontSave;
            panel.targetTexture = target;
            panel.scaleMode = PanelScaleMode.ConstantPixelSize;
            panel.scale = 1;
            panel.clearColor = true;
            panel.colorClearValue = Color.black;
            host = new GameObject("Boss retreat motion capture") { hideFlags = HideFlags.HideAndDontSave };
            host.SetActive(false);
            var document = host.AddComponent<UIDocument>();
            document.panelSettings = panel;
            host.SetActive(true);
            VisualElement root = document.rootVisualElement;
            root.style.width = 1672; root.style.height = 941;
            root.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(Lightbringer.UI.CampaignHUD.StyleSheetPath));
            var dialogue = new BossDialogueView(root);
            dialogue.Root.RemoveFromClassList("lb-hidden");
            var frame = dialogue.Root.Q<VisualElement>(className: "lb-prologue-frame");
            Image picture = frame.Query<Image>().ToList()[1];
            var escape = new BossPortalEscape(); frame.Insert(2, escape);
            Label speaker = frame.Q<Label>(className: "lb-boss-speaker");
            Label line = frame.Q<Label>(className: "lb-boss-line");
            Label count = frame.Q<Label>(className: "lb-boss-dialogue-count");
            Button next = frame.Q<Button>(className: "lb-boss-next");
            if (!escape.Ready) { Cleanup(); throw new InvalidOperationException("Missing retreat art or clean plate"); }
            // Lifecycle regression checks: a stopped/restarted animation must never inherit completion.
            escape.Begin(); escape.Tick(1.2f);
            if (escape.LordRemoved) throw new InvalidOperationException("Lord removed before he is dragged into the portal");
            escape.Tick(1.2f);
            if (!escape.LordRemoved) throw new InvalidOperationException("Lord still drawn after the pull");
            escape.Tick(BossPortalEscape.Duration);
            if (!escape.Complete) throw new InvalidOperationException("Escape never completes");
            escape.Stop(); escape.Begin();
            if (escape.Complete) throw new InvalidOperationException("Escape did not reset");
            escape.Stop();

            int sample = -1, frames = 0;
            // The first retreat shot (plain art, no overlay), then the escape densely enough to judge its motion on one contact sheet.
            float[] times = { 0f, 0f, .3f, .55f, .8f, 1f, 1.2f, 1.4f, 1.6f, 1.8f, 2f, 2.2f, 2.4f, 2.7f, 3.2f };
            const int sheetColumns = 4, cellWidth = 418, cellHeight = 235;
            int sheetRows = (times.Length + sheetColumns - 1) / sheetColumns;
            var sheet = new Texture2D(cellWidth * sheetColumns, cellHeight * sheetRows, TextureFormat.RGB24, false)
                { hideFlags = HideFlags.HideAndDontSave };
            pending = () =>
            {
                try
                {
                    if (frames == 0)
                    {
                        sample++;
                        if (sample == times.Length)
                        {
                            File.WriteAllBytes("Docs/Validation/BossRetreatMotionSheet.png", sheet.EncodeToPNG());
                            Object.DestroyImmediate(sheet);
                            File.WriteAllText("Docs/Validation/BossRetreatRenderChecks.txt",
                                $"PASS | Escape completion and restart; lord kept until the pull and removed after it; {times.Length} dialogue UI frames rendered.\n");
                            Cleanup(); return;
                        }
                        escape.Stop();
                        picture.image = UnityEngine.Resources.Load<Texture2D>("BossStory/" + (sample < 1 ? "WarlordDefeated_v1" : "Retreat_v1"));
                        speaker.text = "어둠의 군주";
                        line.text = sample < 1 ? "이 빛은… 너를 얕보았군." : "이것으로 끝이라 생각하지 마라. 우리는 다시 만날 것이다.";
                        count.text = sample < 1 ? "1 / 3" : "2 / 3";
                        // The first shot can be read on at once; the escape greys the button out.
                        next.text = "다음";
                        next.SetEnabled(sample < 1);
                        if (sample >= 1) { escape.Begin(); escape.Tick(times[sample]); }
                    }
                    InvokeRuntime("UpdatePanels"); InvokeRuntime("RepaintPanels"); InvokeRuntime("RenderOffscreenPanels");
                    if (++frames < 12) return;
                    RenderTexture previous = RenderTexture.active;
                    var image = new Texture2D(1672, 941, TextureFormat.RGB24, false);
                    try
                    {
                        RenderTexture.active = target;
                        image.ReadPixels(new Rect(0, 0, 1672, 941), 0, 0); image.Apply();
                        File.WriteAllBytes($"Docs/Validation/BossRetreatMotion_{sample}.png", image.EncodeToPNG());
                        int left = sample % sheetColumns * cellWidth, top = (sheetRows - 1 - sample / sheetColumns) * cellHeight;
                        for (int y = 0; y < cellHeight; y++)
                        for (int x = 0; x < cellWidth; x++)
                            sheet.SetPixel(left + x, top + y, image.GetPixelBilinear((x + .5f) / cellWidth, (y + .5f) / cellHeight));
                    }
                    finally { RenderTexture.active = previous; Object.DestroyImmediate(image); }
                    frames = 0;
                }
                catch (Exception error) { Cleanup(); Debug.LogException(error); }
            };
            EditorApplication.update += pending;
        }

        private static void InvokeRuntime(string name)
        {
            Type utility = typeof(PanelSettings).Assembly.GetType("UnityEngine.UIElements.UIElementsRuntimeUtility");
            MethodInfo method = utility?.GetMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (method == null) throw new MissingMethodException(name);
            method.Invoke(null, Array.ConvertAll(method.GetParameters(),
                p => p.ParameterType == typeof(bool) ? (object)false : p.HasDefaultValue ? p.DefaultValue : null));
        }

        private static void Cleanup()
        {
            if (pending != null) EditorApplication.update -= pending;
            pending = null;
            if (host != null) Object.DestroyImmediate(host);
            if (panel != null) Object.DestroyImmediate(panel);
            if (target != null) { target.Release(); Object.DestroyImmediate(target); }
        }
    }
}

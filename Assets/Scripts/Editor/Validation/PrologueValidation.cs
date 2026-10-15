using System;
using System.IO;
using System.Linq;
using System.Text;
using Lightbringer.Core;
using Lightbringer.Progression;
using Lightbringer.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Lightbringer.EditorTools
{
    public static class PrologueValidation
    {
        [MenuItem("Lightbringer/Prologue/Validate")]
        public static void Run()
        {
            StringBuilder report = new StringBuilder();
            try
            {
                PrologueSequence data = AssetDatabase.LoadAssetAtPath<PrologueSequence>(PrologueSetup.AssetPath);
                PrologueSetup.Entry[] expected = PrologueSetup.Resolve();
                int captionCount = expected.Sum(entry => entry.Captions.Length);
                Require(data != null && data.IsReady && data.slides.Length == expected.Length,
                    expected.Length + " complete slides are available in the player resource", report);
                Require(captionCount == PrologueSetup.Entries.Sum(entry => entry.Captions.Length),
                    "Every caption is shown", report);
                for (int i = 0; i < expected.Length; i++)
                {
                    Require(data.slides[i].id == expected[i].Id, "Order " + i + ": " + expected[i].Id, report);
                    Require(data.slides[i].image.name == expected[i].File,
                        "Artwork " + expected[i].Id + ": " + data.slides[i].image.name, report);
                    Require(data.slides[i].captions.SequenceEqual(expected[i].Captions), "Captions " + expected[i].Id, report);
                    foreach (string line in data.slides[i].captions)
                        if (string.IsNullOrWhiteSpace(line) || line.Contains("Warlord"))
                            throw new InvalidOperationException("Empty caption or premature identity reveal.");
                }
                Require(data.music != null && data.music.loadType == AudioClipLoadType.Streaming,
                    "Prologue music is referenced and streamed", report);
                Require(AssetDatabase.LoadAssetAtPath<Font>("Assets/UI/Fonts/NanumGothic-Regular.ttf") != null,
                    "Bundled Korean font exists", report);

                int finishes = 0;
                float originalTimeScale = Time.timeScale;
                int originalViewed = PlayerPrefs.GetInt(PrologueSequence.ViewedKey, 0);
                PrologueView view = new PrologueView(new VisualElement(), () => finishes++);
                Require(!view.Open(null) && view.Open(data) && !view.Open(data), "Missing data rejected; duplicate open rejected", report);
                view.Advance();
                Require(view.SlideIndex == 0 && view.CaptionIndex == 0, "Input during fade cannot skip a caption", report);
                Settle(view);
                view.Advance();
                Require(view.SlideIndex == 0 && view.CaptionIndex == 1, "Second caption stays on same image", report);
                view.Advance(); Settle(view);
                Require(view.CurrentId == "00B", "Last caption advances to next image", report);
                view.Previous(); Settle(view);
                Require(view.CurrentId == "00A" && view.CaptionIndex == 1, "Previous returns to preceding image's last caption", report);
                view.Previous();
                view.Tick(90f, false);
                Require(view.CaptionIndex == 0, "Manual reading never advances on a timer", report);
                int safety = 0;
                while (view.IsOpen && ++safety < 100) { Settle(view); view.Advance(); }
                Settle(view);
                Require(!view.IsOpen && finishes == 1, "All " + expected.Length + " slides finish once", report);
                Require(view.Open(data), "Replay restarts after completion", report);
                view.Skip(); view.Skip(); Settle(view);
                Require(!view.IsOpen && finishes == 2, "Skip during initial fade completes exactly once", report);
                Require(Time.timeScale == originalTimeScale && PlayerPrefs.GetInt(PrologueSequence.ViewedKey, 0) == originalViewed,
                    "View tests leave gameplay time and viewed preference untouched", report);

                GameObject host = new GameObject("Prologue validation") { hideFlags = HideFlags.HideAndDontSave };
                Material material = null;
                try
                {
                    CampaignSession session = host.AddComponent<CampaignSession>();
                    session.InitializeForValidation(new CampaignProgress());
                    material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    EditorAssets.ConfigureSession(session, material);
                    CampaignHUD hud = host.AddComponent<CampaignHUD>();
                    // Non-ExecuteAlways components do not receive runtime Awake/OnEnable in edit-mode probes.
                    const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                    typeof(CampaignHUD).GetMethod("Awake", flags).Invoke(hud, null);
                    if (hud.Prologue == null) typeof(CampaignHUD).GetMethod("OnEnable", flags).Invoke(hud, null);
                    Require(hud.PlayPrologue() && session.IsPrologueActive && !session.StartBattle() && !session.SelectStage(1),
                        "Campaign prologue blocks battle and map selection", report);
                    hud.Prologue.Skip(); Settle(hud.Prologue);
                    Require(!session.IsPrologueActive && hud.PlayPrologue(), "Exit restores campaign and replay is available", report);
                    hud.Prologue.Skip(); Settle(hud.Prologue);
                    Require(session.StartBattle(), "Battle can start after returning from prologue", report);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(host);
                    if (material != null) UnityEngine.Object.DestroyImmediate(material);
                }
                File.WriteAllText("Docs/Validation/PrologueChecks.txt", "PASS | " + DateTime.UtcNow.ToString("O") + "\n" + report);
                Debug.Log("Prologue validation passed: " + expected.Length + " slides, navigation, skip, replay, campaign handoff.");
            }
            catch (Exception error)
            {
                File.WriteAllText("Docs/Validation/PrologueChecks.txt", "FAIL | " + DateTime.UtcNow.ToString("O") + "\n" + report + error);
                throw;
            }
        }

        // Long enough for the slowest transition (the opening fade from black and the white flash).
        private static void Settle(PrologueView view) { for (int i = 0; i < 8; i++) view.Tick(1f, false); }
        private static void Require(bool condition, string description, StringBuilder report)
        {
            if (!condition) throw new InvalidOperationException(description);
            report.AppendLine("PASS: " + description);
        }
    }
}

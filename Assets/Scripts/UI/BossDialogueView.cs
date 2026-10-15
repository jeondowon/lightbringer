using System.Collections.Generic;
using Lightbringer.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Lightbringer.UI
{
    public sealed class BossDialogueView
    {
        public readonly VisualElement Root;
        private readonly VisualElement panel;
        private readonly VisualElement story, frame;
        private readonly Image picture, previousPicture;
        private readonly StoryEffects effects;
        private readonly Dictionary<string, Texture2D> pictures = new Dictionary<string, Texture2D>();
        private float pictureFade;
        private float pictureFadeDuration = .35f;
        private readonly BossPortalEscape portalEscape;
        private bool escapeHandoff;
        private bool wasStoryVisible;
        // Camera move and light of the page on screen (StoryCatalog.Panel), timed from its first frame.
        private StoryBeat page;
        private float pageTime;
        private readonly Label speaker, line, count;
        private readonly Button next;
        private BossEncounter encounter;
        public bool Visible => !Root.ClassListContains("lb-hidden");
        public string LineText => line.text;
        public Texture StoryImage => picture.image;
        public bool StoryVisible => !story.ClassListContains("lb-hidden");
        public BossDialogueView(VisualElement parent)
        {
            Root = Hud.Box(parent, "lb-boss-cinematic");
            Hud.Box(Root, "lb-boss-letterbox");
            story = Hud.Box(Root, "lb-boss-story");
            frame = Hud.Box(story, "lb-prologue-frame");
            frame.style.overflow = Overflow.Hidden;
            previousPicture = new Image { scaleMode = ScaleMode.ScaleToFit };
            previousPicture.AddToClassList("lb-prologue-picture");
            frame.Add(previousPicture);
            picture = new Image { scaleMode = ScaleMode.ScaleToFit };
            picture.AddToClassList("lb-prologue-picture");
            frame.Add(picture);
            effects = new StoryEffects();
            frame.Add(effects);
            portalEscape = new BossPortalEscape();
            frame.Add(portalEscape);
            Root.RegisterCallback<GeometryChangedEvent>(_ => Hud.FitWidescreen(Root, frame));
            panel = Hud.Box(frame, "lb-boss-dialogue", "lb-boss-story-caption");
            speaker = Hud.Text(panel, "", "lb-boss-speaker");
            line = Hud.Text(panel, "", "lb-boss-line");
            VisualElement controls = Hud.Box(panel, "lb-boss-dialogue-controls");
            count = Hud.Text(controls, "", "lb-boss-dialogue-count");
            next = Hud.Button(controls, "다음", Advance, "lb-button", "lb-button--gold", "lb-boss-next");
            Hud.Show(Root, false);
        }

        public void Refresh(CampaignSession session)
        {
            encounter = session.Battle != null ? session.Battle.Encounter : null;
            bool visible = encounter != null && encounter.IsCinematic;
            Hud.Show(Root, visible);
            if (!visible) { wasStoryVisible = false; portalEscape.Stop(); return; }
            bool illustrated = encounter.IsStoryPanel;
            Hud.Show(story, illustrated);
            if (!illustrated) { wasStoryVisible = false; portalEscape.Stop(); return; }
            if (!encounter.IsIllustrated) { RefreshLive(); return; }
            story.RemoveFromClassList("lb-boss-story--live");
            float delta = Time.unscaledDeltaTime;
            portalEscape.Tick(delta);
            int escape = encounter.EscapeLine;
            if (portalEscape.Complete && escape >= 0 && encounter.DialogueIndex == escape)
                encounter.AdvanceDialogue();
            int index = Mathf.Clamp(encounter.DialogueIndex, 0, encounter.CurrentLines.Length - 1);
            string[] paths = encounter.CurrentPictures;
            string path = paths[Mathf.Min(index, paths.Length - 1)];
            Texture2D art = Load(path);
            if (picture.image != art || !wasStoryVisible)
            {
                escapeHandoff = wasStoryVisible && escape >= 0 && index == escape + 1 && portalEscape.Complete;
                pictureFadeDuration = escapeHandoff ? 1.1f : .35f;
                // Keep the clean plate over the old lord but under the arriving Aftermath image.
                // Fading both overlays separately would briefly expose the lord again.
                if (escapeHandoff) portalEscape.PlaceBehind(picture);
                else portalEscape.PlaceInFront(picture);
                if (!escapeHandoff) portalEscape.Stop();
                previousPicture.image = wasStoryVisible ? picture.image : null;
                // The outgoing page fades from wherever its camera move had reached.
                StoryFraming.Copy(picture, previousPicture);
                picture.image = art;
                page = StoryCatalog.Panel(path);
                pageTime = 0;
                effects.Show(page != null ? page.Effect : null);
                pictureFade = 0;
                Hud.FitWidescreen(Root, frame);
            }
            wasStoryVisible = true;
            pageTime += delta;
            effects.Tick(delta);
            StoryShot shot = StoryFraming.At(page, pageTime);
            StoryFraming.Apply(picture, shot);
            StoryFraming.Apply(effects, shot);
            pictureFade = Mathf.MoveTowards(pictureFade, 1, delta / pictureFadeDuration);
            portalEscape.style.opacity = encounter.StoryOpacity;
            if (escapeHandoff && pictureFade >= 1) portalEscape.Stop();
            previousPicture.style.opacity = pictureFade < 1 ? encounter.StoryOpacity : 0;
            picture.style.opacity = pictureFade * encounter.StoryOpacity;
            effects.style.opacity = picture.style.opacity;
            panel.style.opacity = pictureFade * encounter.StoryOpacity;
            ShowLine(index);
        }

        // The finale speaks over the live battlefield: no picture, the caption fades with the closing fade.
        private void RefreshLive()
        {
            portalEscape.Stop();
            story.AddToClassList("lb-boss-story--live");
            previousPicture.image = null;
            picture.image = null;
            effects.Show(null);
            if (!wasStoryVisible) { pictureFade = 0; Hud.FitWidescreen(Root, frame); }
            wasStoryVisible = true;
            pictureFade = Mathf.MoveTowards(pictureFade, 1, Time.unscaledDeltaTime / .35f);
            panel.style.opacity = pictureFade * encounter.StoryOpacity;
            ShowLine(Mathf.Clamp(encounter.DialogueIndex, 0, encounter.CurrentLines.Length - 1));
        }

        private void ShowLine(int index)
        {
            // A silent page keeps only the count and the button.
            string who = encounter.CurrentSpeakers[index], said = encounter.CurrentLines[index];
            Hud.Set(speaker, who);
            Hud.Set(line, said);
            Hud.Show(speaker, !string.IsNullOrEmpty(who));
            Hud.Show(line, !string.IsNullOrEmpty(said));
            Hud.Set(count, $"{index + 1} / {encounter.CurrentLines.Length}");
            // While an animation plays the button simply greys out (see .lb-boss-next:disabled).
            Hud.Set(next, index == encounter.CurrentLines.Length - 1
                ? encounter.IsInterlude ? "마왕에게 맞선다" : encounter.IsRetreating ? "계속" : "맞서 싸운다"
                : "다음");
            next.SetEnabled(encounter.IsDialogue && !portalEscape.Running);
            Keyboard keyboard = Keyboard.current;
            if (Application.isFocused && keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame))
                Advance();
        }

        private void Advance()
        {
            // Guard both the button and Enter/Space; early input is not queued past the animation.
            if (!Visible || !StoryVisible || portalEscape.Running) return;
            // Read the line on the escape page first; the next input starts his escape and advances only on completion.
            if (encounter != null && encounter.IsIllustrated && encounter.IsDialogue && encounter.EscapeLine >= 0
                && encounter.DialogueIndex == encounter.EscapeLine && portalEscape.Begin()) return;
            encounter?.AdvanceDialogue();
        }

        private Texture2D Load(string path)
        {
            if (pictures.TryGetValue(path, out Texture2D art)) return art;
            art = UnityEngine.Resources.Load<Texture2D>(path);
            if (art == null) Debug.LogError("Missing boss story illustration: " + path);
            pictures[path] = art;
            return art;
        }
    }
}

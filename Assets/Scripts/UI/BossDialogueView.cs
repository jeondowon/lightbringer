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
        private readonly Texture2D[] arrivalPictures, retreatPictures;
        private float pictureFade;
        private float pictureFadeDuration = .35f;
        private readonly BossPortalEscape portalEscape;
        private bool escapeHandoff;
        private bool wasStoryVisible;
        private readonly Label chapter, speaker, line, count;
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
            chapter = Hud.Text(Root, "", "lb-boss-chapter");
            story = Hud.Box(Root, "lb-boss-story");
            frame = Hud.Box(story, "lb-prologue-frame");
            previousPicture = new Image { scaleMode = ScaleMode.ScaleToFit };
            previousPicture.AddToClassList("lb-prologue-picture");
            frame.Add(previousPicture);
            picture = new Image { scaleMode = ScaleMode.ScaleToFit };
            picture.AddToClassList("lb-prologue-picture");
            frame.Add(picture);
            portalEscape = new BossPortalEscape();
            frame.Add(portalEscape);
            // The establishing shot spans the opening exchange; each subsequent beat gets its own image.
            arrivalPictures = LoadPictures("Confrontation_v1", "Confrontation_v1", "WarlordClose_v1", "HeroResolve_v1");
            retreatPictures = LoadPictures("WarlordDefeated_v1", "Retreat_v1", "Aftermath_v1");
            Root.RegisterCallback<GeometryChangedEvent>(_ => Fit());
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
            Hud.Show(chapter, !illustrated && encounter.Phase != BossEncounterPhase.Rupture);
            Hud.Set(chapter, encounter.ChapterTitle);
            if (!illustrated) { wasStoryVisible = false; portalEscape.Stop(); return; }
            if (!encounter.IsIllustrated) { RefreshLive(); return; }
            story.RemoveFromClassList("lb-boss-story--live");
            portalEscape.Tick(Time.unscaledDeltaTime);
            if (portalEscape.Complete && encounter.IsRetreating && encounter.DialogueIndex == 1)
                encounter.AdvanceDialogue();
            int index = Mathf.Clamp(encounter.DialogueIndex, 0, encounter.CurrentLines.Length - 1);
            Texture2D[] pictures = encounter.IsRetreating ? retreatPictures : arrivalPictures;
            Texture2D art = pictures[Mathf.Min(index, pictures.Length - 1)];
            if (picture.image != art || !wasStoryVisible)
            {
                escapeHandoff = wasStoryVisible && encounter.IsRetreating && index == 2 && portalEscape.Complete;
                pictureFadeDuration = escapeHandoff ? 1.1f : .35f;
                // Keep the clean plate over the old lord but under the arriving Aftermath image.
                // Fading both overlays separately would briefly expose the lord again.
                if (escapeHandoff) portalEscape.PlaceBehind(picture);
                else portalEscape.PlaceInFront(picture);
                if (!escapeHandoff) portalEscape.Stop();
                previousPicture.image = wasStoryVisible ? picture.image : null;
                picture.image = art;
                pictureFade = 0;
                Fit();
            }
            wasStoryVisible = true;
            pictureFade = Mathf.MoveTowards(pictureFade, 1, Time.unscaledDeltaTime / pictureFadeDuration);
            portalEscape.style.opacity = encounter.StoryOpacity;
            if (escapeHandoff && pictureFade >= 1) portalEscape.Stop();
            previousPicture.style.opacity = pictureFade < 1 ? encounter.StoryOpacity : 0;
            picture.style.opacity = pictureFade * encounter.StoryOpacity;
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
            if (!wasStoryVisible) { pictureFade = 0; Fit(); }
            wasStoryVisible = true;
            pictureFade = Mathf.MoveTowards(pictureFade, 1, Time.unscaledDeltaTime / .35f);
            panel.style.opacity = pictureFade * encounter.StoryOpacity;
            ShowLine(Mathf.Clamp(encounter.DialogueIndex, 0, encounter.CurrentLines.Length - 1));
        }

        private void ShowLine(int index)
        {
            Hud.Set(speaker, encounter.CurrentSpeakers[index]);
            Hud.Set(line, encounter.CurrentLines[index]);
            Hud.Set(count, $"{index + 1} / {encounter.CurrentLines.Length}");
            // While an animation plays the button simply greys out (see .lb-boss-next:disabled).
            Hud.Set(next, index == encounter.CurrentLines.Length - 1
                ? encounter.IsRetreating ? "계속" : "맞서 싸운다"
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
            // Read the lord's final line first; the next input starts his escape and advances only on completion.
            if (encounter != null && encounter.IsIllustrated && encounter.IsDialogue && encounter.IsRetreating && encounter.DialogueIndex == 1
                && portalEscape.Begin()) return;
            encounter?.AdvanceDialogue();
        }

        private static Texture2D[] LoadPictures(params string[] names)
        {
            var pictures = new Texture2D[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                pictures[i] = UnityEngine.Resources.Load<Texture2D>("BossStory/" + names[i]);
                if (pictures[i] == null) Debug.LogError("Missing boss story illustration: BossStory/" + names[i]);
            }
            return pictures;
        }

        private void Fit()
        {
            float width = Root.contentRect.width, height = Root.contentRect.height;
            float unit = Mathf.Min(width / 16f, height / 9f);
            frame.style.width = unit * 16f;
            frame.style.height = unit * 9f;
            frame.style.left = (width - unit * 16f) * .5f;
            frame.style.top = (height - unit * 9f) * .5f;
        }
    }
}

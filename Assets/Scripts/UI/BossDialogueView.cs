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
            // The establishing shot spans the opening exchange; each subsequent beat gets its own image.
            arrivalPictures = LoadPictures("Confrontation_v1", "Confrontation_v1", "WarlordClose_v1", "HeroResolve_v1");
            retreatPictures = LoadPictures("WarlordDefeated_v1", "Retreat_v1", "Aftermath_v1");
            Root.RegisterCallback<GeometryChangedEvent>(_ => Fit());
            panel = Hud.Box(frame, "lb-boss-dialogue", "lb-boss-story-caption");
            speaker = Hud.Text(panel, "", "lb-boss-speaker");
            line = Hud.Text(panel, "", "lb-boss-line");
            VisualElement controls = Hud.Box(panel, "lb-boss-dialogue-controls");
            count = Hud.Text(controls, "", "lb-boss-dialogue-count");
            next = Hud.Button(controls, "다음  ·  Enter / Space", Advance, "lb-boss-next");
            Hud.Show(Root, false);
        }

        public void Refresh(CampaignSession session)
        {
            encounter = session.Battle != null ? session.Battle.Encounter : null;
            bool visible = encounter != null && encounter.IsCinematic;
            Hud.Show(Root, visible);
            if (!visible) { wasStoryVisible = false; return; }
            bool illustrated = encounter.IsStoryPanel;
            Hud.Show(story, illustrated);
            Hud.Show(chapter, !illustrated);
            Hud.Set(chapter, encounter.Phase == BossEncounterPhase.Rupture ? "성채가 함락되었다" : "어둠의 군주");
            if (!illustrated) { wasStoryVisible = false; return; }
            int index = Mathf.Clamp(encounter.DialogueIndex, 0, encounter.CurrentLines.Length - 1);
            Texture2D[] pictures = encounter.IsRetreating ? retreatPictures : arrivalPictures;
            Texture2D art = pictures[Mathf.Min(index, pictures.Length - 1)];
            if (picture.image != art || !wasStoryVisible)
            {
                previousPicture.image = wasStoryVisible ? picture.image : null;
                picture.image = art;
                pictureFade = 0;
                Fit();
            }
            wasStoryVisible = true;
            pictureFade = Mathf.MoveTowards(pictureFade, 1, Time.unscaledDeltaTime / .35f);
            previousPicture.style.opacity = pictureFade < 1 ? encounter.StoryOpacity : 0;
            picture.style.opacity = pictureFade * encounter.StoryOpacity;
            panel.style.opacity = pictureFade * encounter.StoryOpacity;
            Hud.Set(speaker, encounter.CurrentSpeakers[index]);
            Hud.Set(line, encounter.CurrentLines[index]);
            Hud.Set(count, $"{index + 1} / {encounter.CurrentLines.Length}");
            Hud.Set(next, index == encounter.CurrentLines.Length - 1
                ? encounter.IsRetreating ? "계속  ·  Enter / Space" : "맞서 싸운다  ·  Enter / Space"
                : "다음  ·  Enter / Space");
            next.SetEnabled(encounter.IsDialogue);
            Keyboard keyboard = Keyboard.current;
            if (Application.isFocused && keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame))
                Advance();
        }

        private void Advance() => encounter?.AdvanceDialogue();

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

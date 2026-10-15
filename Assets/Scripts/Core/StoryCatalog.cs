using System.Collections.Generic;
using UnityEngine;

namespace Lightbringer.Core
{
    // A framing of a 1672x941 storybook illustration: the artwork pixel at the frame centre and the magnification.
    public readonly struct StoryShot
    {
        public static readonly Vector2 ArtworkSize = new Vector2(1672f, 941f);
        public static readonly StoryShot Full = new StoryShot(836f, 470.5f, 1f);
        public readonly Vector2 Focus;
        public readonly float Zoom;
        public StoryShot(float x, float y, float zoom) { Focus = new Vector2(x, y); Zoom = Mathf.Max(1f, zoom); }

        public static StoryShot Lerp(StoryShot a, StoryShot b, float t) =>
            new StoryShot(Mathf.Lerp(a.Focus.x, b.Focus.x, t), Mathf.Lerp(a.Focus.y, b.Focus.y, t), Mathf.Lerp(a.Zoom, b.Zoom, t));

        // The focus actually shown: the view never leaves the artwork.
        public Vector2 ClampedFocus
        {
            get
            {
                Vector2 half = ArtworkSize * (0.5f / Zoom);
                return new Vector2(Mathf.Clamp(Focus.x, half.x, ArtworkSize.x - half.x), Mathf.Clamp(Focus.y, half.y, ArtworkSize.y - half.y));
            }
        }
    }

    // One page of a storybook scene (Docs/GameDesign/Storyline.md). Captions are UI text, never painted into the artwork;
    // light that has to move or die out is drawn by StoryEffects, keyed by Effect.
    public sealed class StoryBeat
    {
        // Resources paths. Fallback stands in for a planned page whose artwork has not been delivered yet.
        public string Image, Fallback;
        public string Effect, FallbackEffect;
        public string Speaker = "", Line = "";
        // Seconds the picture plays before its caption appears, so a line can follow the action it answers.
        public float LineDelay;
        public StoryShot From = StoryShot.Full, To = StoryShot.Full;
        // Seconds of the eased camera move From -> To; 0 holds From.
        public float Pan;
        // A silent page turns by itself after Hold seconds (input turns it sooner).
        public float Hold = 3f;
        // The scene ends in light growing out of the effect until the screen is white.
        public bool WhiteOut;
        public bool IsSilent => string.IsNullOrEmpty(Line);

        public StoryBeat(string image, string speaker = "", string line = "")
        { Image = image; Speaker = speaker ?? ""; Line = line ?? ""; }

        public StoryBeat Move(StoryShot from, StoryShot to, float seconds) { From = from; To = to; Pan = seconds; return this; }
        public StoryBeat After(float seconds) { LineDelay = seconds; return this; }
        public StoryBeat Silent(float seconds) { Hold = seconds; return this; }
        public StoryBeat With(string effect) { Effect = effect; return this; }
        public StoryBeat Planned(string fallback, string fallbackEffect = null) { Fallback = fallback; FallbackEffect = fallbackEffect; return this; }
        public StoryBeat EndInWhite() { WhiteOut = true; return this; }
    }

    // Storybook scenes outside the prologue. Images live in Assets/UI/Resources/Story (built from
    // Docs/ArtReference/Story by Lightbringer > Story > Build and Validate).
    public static class StoryCatalog
    {
        public const string Folder = "Story/";
        public const string ActOneFlashback = "ActOneFlashback", Epilogue = "Epilogue";
        // First clear of this stage plays the Act 1 memory (proposal in Storyline.md, chosen 2026-10-15).
        public const int FlashbackStage = 4;
        public const string Apprentice = "제자", Mentor = "스승", Hero = "빛의 지휘관";

        // In-battle panels shown by BossDialogueView.
        public const string SwordCloseUp = Folder + "ActTwo_02_SwordRecognition_Storybook_v1";
        public const string HeroRecognition = Folder + "ActTwo_03_HeroRecognition_Storybook_v1";
        public const string WarlordDissolution = Folder + "ActThree_01_WarlordDissolution_Storybook_v1";

        private const string ActOne01 = Folder + "ActOne_01_Apprentice_Storybook_v4";
        private const string Epilogue01 = Folder + "Epilogue_01_BeforeRestoration_Storybook_v1";

        // Act 1: a tired apprentice, her teacher's faith, the staff put back in her hands.
        // Every page holds its full picture still; only the code-drawn light moves.
        private static readonly StoryBeat[] ActOne =
        {
            new StoryBeat(ActOne01, Apprentice, "또 꺼졌어요… 스승님처럼 되려면 얼마나 더 해야 해요?")
                .With("ApprenticeLight").After(2.9f),
            new StoryBeat(Folder + "ActOne_02_MentorSword_Storybook_v4", Mentor, "그렇게 서두르는 이유가 있느냐?"),
            new StoryBeat(Folder + "ActOne_03_ApprenticeWish_Storybook_v1", Apprentice, "스승님이 있으면 다들 안심하잖아요. 저도 그렇게 되고 싶어요."),
            new StoryBeat(Folder + "ActOne_04_MentorFaith_Storybook_v1", Mentor, "될 수 있을 거다. 너는 분명 나보다 더 큰 사람이 될 거야."),
            new StoryBeat(Folder + "ActOne_05_StaffGuidance_Storybook_v1").Silent(4f),
            // Light is born in the staff's gem and swallows the memory in white (no glow is painted into the art).
            new StoryBeat(Folder + "ActOne_06_Together_Storybook_v1")
                .Silent(3.4f).With("StaffLightBirth").EndInWhite(),
        };

        // Epilogue: the Warlord stands before her as her teacher again, thanks her and returns to light.
        // Only the first page has artwork; the planned pages borrow it with a code-drawn light until theirs arrive.
        private static readonly StoryBeat[] EpilogueScene =
        {
            new StoryBeat(Epilogue01).Silent(3.6f).Move(StoryShot.Full, new StoryShot(836f, 460f, 1.05f), 8f),
            new StoryBeat(Folder + "Epilogue_02_Restoration_Storybook_v1", Hero, "스승님…")
                .Planned(Epilogue01, "MentorRestored"),
            new StoryBeat(Folder + "Epilogue_02_Restoration_Storybook_v1", Mentor, "고맙다. 나를 되찾아 주어서.")
                .Planned(Epilogue01, "MentorRestored"),
            new StoryBeat(Folder + "Epilogue_03_Departure_Storybook_v1").Silent(5.5f)
                .Planned(Epilogue01, "MentorDeparts"),
            new StoryBeat(Folder + "Epilogue_04_Alone_Storybook_v1").Silent(4.5f),
        };

        public static StoryBeat[] Scene(string id) =>
            id == ActOneFlashback ? ActOne : id == Epilogue ? EpilogueScene : null;

        // The scene a first clear of this stage unlocks, if any.
        public static string SceneAfter(int stage) =>
            stage == FlashbackStage ? ActOneFlashback : stage == Lightbringer.Units.EnemyCatalog.FinalStage ? Epilogue : null;

        public static readonly string[] Scenes = { ActOneFlashback, Epilogue };

        // Camera moves and effects for in-battle panels; null keeps the picture still and full.
        private static readonly Dictionary<string, StoryBeat> Panels = new Dictionary<string, StoryBeat>
        {
            // From the broad blade up to the round four-point star of the guard.
            { SwordCloseUp, new StoryBeat(SwordCloseUp).Move(new StoryShot(770f, 640f, 1.7f), new StoryShot(772f, 300f, 1.7f), 3.2f) },
            { WarlordDissolution, new StoryBeat(WarlordDissolution).With("RemnantLight")
                .Move(StoryShot.Full, new StoryShot(1000f, 420f, 1.1f), 9f) },
        };

        public static StoryBeat Panel(string picture) => picture != null && Panels.TryGetValue(picture, out StoryBeat beat) ? beat : null;

        // Every artwork the scenes and panels may load, delivered or planned.
        public static IEnumerable<string> Artwork()
        {
            var seen = new HashSet<string>();
            foreach (string id in Scenes)
                foreach (StoryBeat beat in Scene(id))
                {
                    if (seen.Add(beat.Image)) yield return beat.Image;
                    if (beat.Fallback != null && seen.Add(beat.Fallback)) yield return beat.Fallback;
                }
            foreach (string picture in Panels.Keys) if (seen.Add(picture)) yield return picture;
            if (seen.Add(HeroRecognition)) yield return HeroRecognition;
        }
    }
}

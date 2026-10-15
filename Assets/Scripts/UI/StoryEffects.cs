using System;
using Lightbringer.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Lightbringer.UI
{
    // Code-drawn light over the story illustrations (Storyline.md: nothing that has to move or die out is painted in).
    // Positions are artwork pixels (1672x941) of the page shown; keyed by StoryBeat.Effect.
    internal sealed class StoryEffects : VisualElement
    {
        private static readonly Color Warm = new Color(1f, 0.86f, 0.58f);
        private static readonly Color Pale = new Color(1f, 0.96f, 0.84f);

        private readonly SpriteFx fx = new SpriteFx();
        private Action scene;
        private float elapsed, repaintTime;
        // 0..1: how far the closing light has grown (driven by StoryView for white-out pages).
        public float Surge { get; set; }

        public StoryEffects()
        {
            name = "Story Effects";
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = style.right = style.top = style.bottom = 0f;
            generateVisualContent += Draw;
        }

        public void Show(string effect)
        {
            scene = SceneFor(effect);
            elapsed = 0f;
            Surge = 0f;
            MarkDirtyRepaint();
        }

        public void Tick(float deltaTime)
        {
            if (scene == null) return;
            elapsed += Mathf.Max(0f, deltaTime);
            repaintTime += deltaTime;
            if (repaintTime < 1f / 60f) return;
            repaintTime = 0f;
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext context)
        {
            if (scene == null || !fx.Begin(contentRect, elapsed, false)) return;
            scene();
            fx.End(context);
        }

        private static Vector2 P(float x, float y) => new Vector2(x, y);
        private float Ramp(float start, float seconds) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((elapsed - start) / seconds));

        private Action SceneFor(string effect)
        {
            switch (effect)
            {
                case "ApprenticeLight": return ApprenticeLight;
                case "StaffLightBirth": return StaffLightBirth;
                case "RemnantLight": return RemnantLight;
                case "MentorRestored": return MentorRestored;
                case "MentorDeparts": return MentorDeparts;
                default: return null;
            }
        }

        // Act 1, page 1: the small light on the apprentice's staff gutters, sputters and goes out before she speaks.
        private static readonly Vector2 ApprenticeOrb = P(1012f, 302f);
        public const float ApprenticeLightOut = 2.5f;

        private void ApprenticeLight()
        {
            float t = elapsed;
            float envelope = t < 0.7f ? 1f : Mathf.Clamp01(1f - (t - 0.7f) / (ApprenticeLightOut - 0.7f));
            // Dropouts grow more frequent as the light weakens.
            float noise = Mathf.PerlinNoise(t * 7.5f, 0.31f);
            float dropout = noise < Mathf.Lerp(0.15f, 0.62f, 1f - envelope) ? 0.18f : 1f;
            float light = envelope * dropout * (0.78f + 0.22f * Mathf.PerlinNoise(t * 3.1f, 4.2f));
            if (light > 0.01f)
            {
                fx.Glow(ApprenticeOrb, Vector2.one * 74f * (0.7f + 0.3f * light), new Color(1f, 0.78f, 0.42f, 0.32f * light));
                fx.Glow(ApprenticeOrb, Vector2.one * 26f, new Color(1f, 0.9f, 0.66f, 0.62f * light));
                fx.Glow(ApprenticeOrb, Vector2.one * 9f, new Color(1f, 0.98f, 0.9f, 0.85f * light));
            }
            // A few sparks drift off and fade once the light has died.
            float after = Mathf.Clamp01((t - ApprenticeLightOut + 0.3f) / 1.6f);
            if (after > 0f && after < 1f)
                fx.Motes(ApprenticeOrb, P(26f, 18f), 6, new Color(1f, 0.82f, 0.5f, 1f - after), P(0f, -40f), 6f, 0.8f, 2.2f, 41);
        }

        // Act 1, page 6: a new light is born in the gem and grows until it whites out the memory.
        private static readonly Vector2 TogetherGem = P(752f, 238f);

        private void StaffLightBirth()
        {
            float surge = Surge;
            if (surge <= 0f) return;
            float bloom = surge * surge;
            fx.Spill(TogetherGem, Vector2.one * (160f + 1400f * bloom), new Color(1f, 0.93f, 0.76f), 0.55f * surge, 7);
            fx.Rays(TogetherGem, 140f + 900f * bloom, Mathf.Clamp01(surge * 1.4f), 7);
            fx.Glow(TogetherGem, Vector2.one * (30f + 520f * bloom), new Color(1f, 0.9f, 0.68f, 0.5f * surge));
            fx.Glow(TogetherGem, Vector2.one * (10f + 90f * surge), new Color(1f, 0.98f, 0.92f, Mathf.Clamp01(surge * 2f)));
            if (surge > 0.15f)
                fx.Motes(TogetherGem, P(120f + 500f * bloom, 80f + 300f * bloom), 26, new Color(1f, 0.9f, 0.62f, surge), P(0f, -60f), 14f, 1.1f, 3f, 43);
        }

        // Act 3: the small light left in the dissolving Warlord's chest breathes and sheds motes.
        private static readonly Vector2 RemnantCore = P(1015f, 400f);

        private void RemnantLight()
        {
            float pulse = 0.75f + 0.25f * Mathf.Sin(elapsed * 1.8f);
            float rise = Ramp(0f, 1.2f);
            fx.Glow(RemnantCore, Vector2.one * 120f * pulse, new Color(1f, 0.8f, 0.46f, 0.22f * rise));
            fx.Glow(RemnantCore, Vector2.one * 34f, new Color(1f, 0.94f, 0.76f, 0.55f * rise * pulse));
            fx.Glint(RemnantCore, 38f, Pale, 3.4f, 44, 0.25f * rise);
            fx.Motes(RemnantCore + P(0f, -40f), P(60f, 50f), 12, new Color(1f, 0.86f, 0.55f, rise), P(10f, -90f), 10f, 0.9f, 3.5f, 45);
        }

        // Epilogue (until its own pages exist): light gathers over the restored teacher, then he becomes light.
        private static readonly Vector2 MentorChest = P(1215f, 330f);
        private static readonly Vector2 MentorBody = P(1250f, 470f);

        private void MentorRestored()
        {
            float rise = Ramp(0f, 2.5f);
            fx.Spill(MentorBody, P(330f, 470f), Warm, 0.22f * rise, 46);
            fx.Glow(MentorChest, Vector2.one * 160f, new Color(1f, 0.84f, 0.52f, 0.26f * rise));
            fx.Glow(MentorChest, Vector2.one * 36f, new Color(1f, 0.95f, 0.8f, 0.5f * rise));
            fx.Motes(MentorBody, P(230f, 380f), 22, new Color(1f, 0.88f, 0.58f, rise), P(0f, -70f), 12f, 1f, 4.5f, 47);
        }

        private void MentorDeparts()
        {
            float fade = Ramp(0f, 4.2f);
            // The teacher's figure is covered by light that thickens, while motes stream up into the clearing sky.
            fx.Spill(MentorBody, P(360f, 520f), Pale, 0.3f + 0.6f * fade, 48);
            fx.Glow(MentorBody, P(250f, 440f), new Color(1f, 0.95f, 0.84f, 0.25f + 0.7f * fade));
            fx.Glow(MentorChest, Vector2.one * (180f + 160f * fade), new Color(1f, 0.9f, 0.66f, 0.4f));
            fx.Rays(MentorChest, 380f + 320f * fade, 0.5f + 0.5f * fade, 49);
            fx.Motes(MentorBody + P(0f, -120f), P(260f, 420f), 44, new Color(1f, 0.9f, 0.62f, 1f), P(0f, -220f), 18f, 1.2f, 3.2f, 50);
        }
    }

    // Camera moves over storybook pages: the element is scaled about its centre and shifted so the shot's focus
    // lands mid-frame. Children (picture and effects) move together; the frame must clip (overflow hidden).
    internal static class StoryFraming
    {
        public static void Apply(VisualElement layer, StoryShot shot)
        {
            Vector2 focus = shot.ClampedFocus;
            Vector2 offset = (StoryShot.ArtworkSize * 0.5f - focus) / StoryShot.ArtworkSize * shot.Zoom;
            layer.style.transformOrigin = new TransformOrigin(Length.Percent(50f), Length.Percent(50f));
            layer.style.scale = new Scale(new Vector3(shot.Zoom, shot.Zoom, 1f));
            layer.style.translate = new Translate(Length.Percent(offset.x * 100f), Length.Percent(offset.y * 100f));
        }

        public static void Copy(VisualElement from, VisualElement to)
        {
            to.style.transformOrigin = from.style.transformOrigin;
            to.style.scale = from.style.scale;
            to.style.translate = from.style.translate;
        }

        // Eased position along a beat's move after `time` seconds on the page.
        public static StoryShot At(StoryBeat beat, float time) =>
            beat == null ? StoryShot.Full
            : beat.Pan <= 0f ? beat.From
            : StoryShot.Lerp(beat.From, beat.To, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(time / beat.Pan)));
    }
}

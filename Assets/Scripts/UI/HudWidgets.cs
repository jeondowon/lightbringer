using UnityEngine;
using UnityEngine.UIElements;

namespace Lightbringer.UI
{
    // Small helpers shared by the HUD views. Styling lives in Assets/UI/LightbringerHUD.uss.
    internal static class Hud
    {
        public static VisualElement Box(VisualElement parent, params string[] classes)
        {
            VisualElement element = new VisualElement();
            foreach (string name in classes) element.AddToClassList(name);
            parent?.Add(element);
            return element;
        }

        public static Label Text(VisualElement parent, string text, params string[] classes)
        {
            Label label = new Label(text);
            foreach (string name in classes) label.AddToClassList(name);
            parent?.Add(label);
            return label;
        }

        // Assigning identical text or style each frame is cheap but still dirties layout; skip unchanged values.
        public static void Set(TextElement label, string text)
        {
            if (label.text != text) label.text = text;
        }

        public static void Show(VisualElement element, bool visible) => element.EnableInClassList("lb-hidden", !visible);

        public static void Fraction(VisualElement fill, float value, bool vertical = false)
        {
            Length length = Length.Percent(Mathf.Clamp01(float.IsNaN(value) ? 0f : value) * 100f);
            if (vertical) { if (fill.style.height != length) fill.style.height = length; }
            else if (fill.style.width != length) fill.style.width = length;
        }

        public static void IgnorePicking(VisualElement root)
        {
            root.pickingMode = PickingMode.Ignore;
            foreach (VisualElement child in root.Children()) IgnorePicking(child);
        }
    }

    // Horizontal fill bar with centred text, e.g. HP 120/170.
    internal sealed class HudBar
    {
        public readonly VisualElement Root;
        private readonly VisualElement fill;
        private readonly Label text;

        public HudBar(VisualElement parent, string modifier)
        {
            Root = Hud.Box(parent, "lb-bar", modifier);
            fill = Hud.Box(Root, "lb-bar-fill");
            // Sheen over the fill; only the battle HUD styles it.
            Hud.Box(Root, "lb-bar-gloss");
            text = Hud.Text(Root, "", "lb-bar-text");
        }

        public string Label => text.text;

        public void Set(float current, float maximum, string label)
        {
            Hud.Fraction(fill, maximum > 0f ? current / maximum : 0f);
            Hud.Set(text, label);
        }
    }

    // Clockwise cooldown sweep: the shaded wedge is the part still recharging, starting at twelve o'clock.
    internal sealed class HudDial
    {
        public readonly VisualElement Root;
        private readonly Color shade;
        private float value;

        public HudDial(VisualElement parent, string className, Color shade)
        {
            Root = Hud.Box(parent, className);
            this.shade = shade;
            Root.generateVisualContent += Draw;
        }

        public void Set(float fraction)
        {
            fraction = Mathf.Clamp01(float.IsNaN(fraction) ? 0f : fraction);
            if (Mathf.Abs(fraction - value) < 0.002f) return;
            value = fraction;
            Root.MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext context)
        {
            if (value <= 0f) return;
            Rect area = Root.contentRect;
            Vector2 centre = area.center;
            float radius = Mathf.Min(area.width, area.height) * 0.5f;
            Painter2D painter = context.painter2D;
            painter.BeginPath();
            painter.MoveTo(centre);
            painter.LineTo(centre + Vector2.down * radius);
            painter.Arc(centre, radius, new Angle(-90f, AngleUnit.Degree), new Angle(-90f + 360f * value, AngleUnit.Degree));
            painter.ClosePath();
            painter.fillColor = shade;
            painter.Fill();
        }
    }
}

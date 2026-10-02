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
        public static void Set(Label label, string text)
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
            text = Hud.Text(Root, "", "lb-bar-text");
        }

        public string Label => text.text;

        public void Set(float current, float maximum, string label)
        {
            Hud.Fraction(fill, maximum > 0f ? current / maximum : 0f);
            Hud.Set(text, label);
        }
    }
}

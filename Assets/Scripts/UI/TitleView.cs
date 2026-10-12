using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Lightbringer.UI
{
    // First screen of a persistent session: Start (-> campaign map), Prologue replay and Quit.
    public sealed class TitleView
    {
        public readonly VisualElement Root;
        public event Action StartRequested, PrologueRequested, QuitRequested;
        private readonly Button start;
        private readonly TitleBeaconEffects beaconEffects;

        public bool Visible => !Root.ClassListContains("lb-hidden");

        public TitleView(VisualElement parent)
        {
            Root = Hud.Box(parent, "lb-title");
            // The style sheet supplies the storybook backdrop, with dark space for the menu at left.
            beaconEffects = new TitleBeaconEffects();
            Root.Add(beaconEffects);

            VisualElement brand = Hud.Box(Root, "lb-title-brand");
            Hud.IgnorePicking(brand);
            Hud.Box(brand, "lb-title-logo").name = "Lightbringer Logo";

            VisualElement menu = Hud.Box(Root, "lb-title-menu");
            start = Hud.Button(menu, "시작하기", () => StartRequested?.Invoke(), "lb-atlas-primary", "lb-title-start");
            Hud.Button(menu, "프롤로그", () => PrologueRequested?.Invoke(), "lb-title-link");
            Hud.Button(menu, "종료", () => QuitRequested?.Invoke(), "lb-title-link");
            Hud.Show(Root, false);
        }

        public void Refresh(bool visible)
        {
            bool opening = visible && !Visible;
            Hud.Show(Root, visible);
            if (visible) beaconEffects.Tick(Time.unscaledDeltaTime);
            if (opening) start.Focus();
            if (visible && Keyboard.current != null
                && (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame))
                StartRequested?.Invoke();
        }
    }
}

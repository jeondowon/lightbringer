using System;
using Lightbringer.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Lightbringer.UI
{
    // The host owns lifetime/music. Navigation and fades use unscaled time; no gameplay time is changed.
    public sealed class PrologueView
    {
        private const float FadeDuration = .65f;
        public readonly VisualElement Root;
        private readonly VisualElement frame, captionPanel, credits;
        private readonly Image picture;
        private readonly Label caption;
        private readonly Button previous, next;
        private readonly Action finished;
        private PrologueSequence sequence;
        private int pendingSlide = -1, pendingCaption;
        private float transition;
        private bool fadingOut, closing;
        public bool IsOpen { get; private set; }
        public bool IsTransitioning => transition > 0;
        public int SlideIndex { get; private set; }
        public int CaptionIndex { get; private set; }
        public string CurrentId => sequence == null ? "" : sequence.slides[SlideIndex].id;
        public string Caption => caption.text;
        public float MusicGain => !IsOpen ? 0f : closing ? Mathf.Clamp01(transition / FadeDuration) : 1f;

        public PrologueView(VisualElement parent, Action onFinished)
        {
            finished = onFinished;
            Root = Hud.Box(parent, "lb-prologue");
            Root.name = "prologue";
            frame = Hud.Box(Root, "lb-prologue-frame");
            picture = new Image { scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            picture.AddToClassList("lb-prologue-picture");
            frame.Add(picture);
            frame.RegisterCallback<ClickEvent>(evt => { if (evt.button == 0) Advance(); });
            Root.RegisterCallback<GeometryChangedEvent>(_ => Fit());
            VisualElement top = Hud.Box(Root, "lb-prologue-top");
            Hud.Text(top, "LIGHTBRINGER", "lb-atlas-brand");
            AddButton(top, "건너뛰기", Skip);
            captionPanel = Hud.Box(Root, "lb-prologue-caption-panel");
            caption = Hud.Text(captionPanel, "", "lb-prologue-caption");
            VisualElement controls = Hud.Box(captionPanel, "lb-prologue-controls");
            previous = AddButton(controls, "이전", Previous);
            next = AddButton(controls, "다음", Advance);
            VisualElement music = Hud.Box(Root, "lb-prologue-music");
            AddButton(music, "음악 정보", () => Hud.Show(credits, credits.ClassListContains("lb-hidden")));
            credits = Hud.Box(music, "lb-prologue-credits");
            Hud.Text(credits, PrologueSequence.MusicCredit);
            Slider volume = new Slider("음악 음량", 0f, 1f) { value = BattleMusic.Volume };
            volume.RegisterValueChangedCallback(change => BattleMusic.Volume = change.newValue);
            credits.Add(volume);
            Hud.Show(credits, false);
            Hud.Show(Root, false);
        }

        private static Button AddButton(VisualElement parent, string text, Action action)
        {
            Button button = Hud.Button(parent, text, action, "lb-prologue-button");
            button.focusable = false;
            return button;
        }

        public bool Open(PrologueSequence value)
        {
            if (IsOpen || value == null || !value.IsReady) return false;
            sequence = value;
            IsOpen = true;
            closing = false;
            Hud.Show(credits, false);
            Hud.Show(Root, true);
            Root.style.opacity = 1f;
            ShowSlide(0, 0);
            fadingOut = false;
            transition = FadeDuration;
            SetContentOpacity(0);
            Fit();
            return true;
        }

        public void Advance()
        {
            if (!IsOpen || IsTransitioning) return;
            if (CaptionIndex + 1 < sequence.slides[SlideIndex].captions.Length)
                ShowCaption(CaptionIndex + 1);
            else if (SlideIndex + 1 < sequence.slides.Length) ChangeSlide(SlideIndex + 1, 0);
            else Skip();
        }

        public void Previous()
        {
            if (!IsOpen || IsTransitioning) return;
            if (CaptionIndex > 0) ShowCaption(CaptionIndex - 1);
            else if (SlideIndex > 0)
                ChangeSlide(SlideIndex - 1, sequence.slides[SlideIndex - 1].captions.Length - 1);
        }

        public void Skip()
        {
            if (!IsOpen || closing) return;
            closing = true;
            transition = FadeDuration;
            pendingSlide = -1;
        }

        public void Tick(float delta, bool readInput = true)
        {
            if (!IsOpen) return;
            delta = Mathf.Max(0, delta);
            // Escape also works during an image fade. The opaque root prevents clicks reaching the map.
            Keyboard keys = readInput ? Keyboard.current : null;
            bool creditsOpen = !credits.ClassListContains("lb-hidden");
            if (keys != null && keys.escapeKey.wasPressedThisFrame)
            {
                if (creditsOpen) Hud.Show(credits, false); else Skip();
            }
            if (closing)
            {
                transition = Mathf.Max(0, transition - delta);
                Root.style.opacity = transition / FadeDuration;
                if (transition == 0) Close();
                return;
            }
            if (transition > 0)
            {
                transition = Mathf.Max(0, transition - delta);
                SetContentOpacity(fadingOut ? transition / FadeDuration : 1f - transition / FadeDuration);
                if (transition == 0 && fadingOut)
                {
                    ShowSlide(pendingSlide, pendingCaption);
                    fadingOut = false;
                    transition = FadeDuration;
                    SetContentOpacity(0);
                }
                return;
            }
            if (creditsOpen) return;
            if (keys != null)
            {
                if (keys.spaceKey.wasPressedThisFrame || keys.rightArrowKey.wasPressedThisFrame) { Advance(); return; }
                if (keys.leftArrowKey.wasPressedThisFrame) { Previous(); return; }
            }
        }

        private void ChangeSlide(int index, int line)
        {
            pendingSlide = index;
            pendingCaption = line;
            fadingOut = true;
            transition = FadeDuration;
        }

        private void ShowSlide(int index, int line)
        {
            SlideIndex = index;
            picture.image = sequence.slides[index].image;
            ShowCaption(line);
        }

        private void ShowCaption(int line)
        {
            CaptionIndex = line;
            PrologueSlide slide = sequence.slides[SlideIndex];
            caption.text = slide.captions[line];
            previous.SetEnabled(SlideIndex != 0 || line != 0);
            next.text = SlideIndex == sequence.slides.Length - 1 && line == slide.captions.Length - 1 ? "여정 시작" : "다음";
        }

        private void SetContentOpacity(float value)
        {
            picture.style.opacity = value;
            caption.style.opacity = value;
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

        private void Close()
        {
            IsOpen = false;
            closing = false;
            transition = 0;
            picture.image = null;
            sequence = null;
            Hud.Show(Root, false);
            finished?.Invoke();
        }
    }
}

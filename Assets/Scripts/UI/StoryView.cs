using System;
using System.Collections.Generic;
using Lightbringer.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Lightbringer.UI
{
    // Storybook scenes between battles (StoryCatalog): the Act 1 memory and the epilogue. Like the prologue, still
    // pages come alive through camera moves, code-drawn light and captions; everything runs on unscaled time.
    public sealed class StoryView
    {
        private const float OpenFade = 1.1f, PageFade = .6f, CloseFade = .9f, CaptionFade = .35f;
        // White-out ending: the light grows, white covers the frame, then the white itself fades back to the game.
        private const float SurgeSeconds = 2.6f, WhiteSeconds = 1.3f, WhiteRelease = 1.8f;
        private enum Stage { Closed, Opening, Showing, TurningOut, TurningIn, Surging, Releasing, Closing }

        public readonly VisualElement Root;
        private readonly VisualElement frame, canvas, whiteout, captionPanel;
        private readonly Image picture;
        private readonly StoryEffects effects;
        private readonly Label speaker, caption, progress;
        private readonly Button next;
        private readonly Action finished;
        private readonly List<StoryBeat> beats = new List<StoryBeat>();
        private readonly List<Texture2D> images = new List<Texture2D>();
        private readonly List<string> effectIds = new List<string>();
        private Stage stage;
        private float timer, pageTime;
        // A page continuing the same picture starts its move from wherever the previous one ended.
        private StoryShot pageFrom, pageTo;
        private float pagePan;

        public bool IsOpen => stage != Stage.Closed;
        public string SceneId { get; private set; }
        public int PageIndex { get; private set; }
        public int PageCount => beats.Count;
        public string LineText => caption.text;
        public Texture StoryImage => picture.image;
        public bool CaptionVisible => IsOpen && PageIndex < beats.Count && !beats[PageIndex].IsSilent && pageTime >= beats[PageIndex].LineDelay;
        public float WhiteoutOpacity => whiteout.resolvedStyle.opacity;

        public StoryView(VisualElement parent, Action onFinished)
        {
            finished = onFinished;
            Root = Hud.Box(parent, "lb-prologue", "lb-story");
            Root.name = "story";
            frame = Hud.Box(Root, "lb-prologue-frame");
            frame.style.overflow = Overflow.Hidden;
            canvas = Hud.Box(frame, "lb-story-canvas");
            canvas.pickingMode = PickingMode.Ignore;
            picture = new Image { scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            picture.AddToClassList("lb-prologue-picture");
            canvas.Add(picture);
            effects = new StoryEffects();
            canvas.Add(effects);
            frame.RegisterCallback<ClickEvent>(evt => { if (evt.button == 0) Advance(); });
            Root.RegisterCallback<GeometryChangedEvent>(_ => Fit());
            VisualElement top = Hud.Box(Root, "lb-prologue-top");
            Hud.Text(top, "LIGHTBRINGER", "lb-atlas-brand");
            AddButton(top, "건너뛰기", Skip);
            // Captions use the boss dialogue panel (BossDialogueView), set inside the frame below the moving canvas.
            captionPanel = Hud.Box(frame, "lb-boss-dialogue", "lb-boss-story-caption");
            speaker = Hud.Text(captionPanel, "", "lb-boss-speaker");
            caption = Hud.Text(captionPanel, "", "lb-boss-line");
            VisualElement controls = Hud.Box(captionPanel, "lb-boss-dialogue-controls");
            progress = Hud.Text(controls, "", "lb-boss-dialogue-count");
            progress.pickingMode = PickingMode.Ignore;
            next = Hud.Button(controls, "다음", Advance, "lb-button", "lb-button--gold", "lb-boss-next");
            next.focusable = false;
            // The frame turns the page on click too; the button must not turn it twice.
            next.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
            // Full-screen so the white covers the letterbox too; it fades away to reveal whatever lies beneath.
            whiteout = new VisualElement { name = "Story Whiteout", pickingMode = PickingMode.Ignore };
            whiteout.style.position = Position.Absolute;
            whiteout.style.left = whiteout.style.right = whiteout.style.top = whiteout.style.bottom = 0f;
            whiteout.style.backgroundColor = new Color(1f, 0.98f, 0.93f);
            whiteout.style.opacity = 0f;
            Root.Add(whiteout);
            Hud.Show(Root, false);
        }

        private static Button AddButton(VisualElement parent, string text, Action action)
        {
            Button button = Hud.Button(parent, text, action, "lb-prologue-button");
            button.focusable = false;
            return button;
        }

        // Resolves the scene against the artwork on disk: planned pages borrow their fallback, missing pages drop out.
        public bool Open(string sceneId)
        {
            StoryBeat[] scene = StoryCatalog.Scene(sceneId);
            if (IsOpen || scene == null) return false;
            beats.Clear(); images.Clear(); effectIds.Clear();
            var cache = new Dictionary<string, Texture2D>();
            foreach (StoryBeat beat in scene)
            {
                Texture2D image = Load(beat.Image, cache);
                string effect = beat.Effect;
                if (image == null && beat.Fallback != null) { image = Load(beat.Fallback, cache); effect = beat.FallbackEffect; }
                if (image == null)
                {
                    if (!beat.IsSilent) Debug.LogWarning($"Story page '{beat.Image}' has a line but no artwork; it is skipped.");
                    continue;
                }
                beats.Add(beat); images.Add(image); effectIds.Add(effect);
            }
            if (beats.Count == 0) return false;
            SceneId = sceneId;
            Hud.Show(Root, true);
            Root.style.opacity = 0f;
            Root.style.backgroundColor = StyleKeyword.Null;
            canvas.style.opacity = 1f;
            captionPanel.style.opacity = 1f;
            whiteout.style.opacity = 0f;
            ShowPage(0, false);
            SetStage(Stage.Opening);
            Fit();
            return true;
        }

        private static Texture2D Load(string path, Dictionary<string, Texture2D> cache)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (!cache.TryGetValue(path, out Texture2D image))
                cache[path] = image = UnityEngine.Resources.Load<Texture2D>(path);
            return image;
        }

        public void Advance()
        {
            if (stage != Stage.Showing) return;
            StoryBeat beat = beats[PageIndex];
            // The first input on a page whose line is still waiting brings the line in instead of turning the page.
            if (!beat.IsSilent && pageTime < beat.LineDelay) { pageTime = beat.LineDelay; return; }
            Turn();
        }

        public void Skip()
        {
            if (!IsOpen || stage == Stage.Closing || stage == Stage.Releasing) return;
            SetStage(Stage.Closing);
        }

        private void Turn()
        {
            StoryBeat beat = beats[PageIndex];
            if (beat.WhiteOut) { SetStage(Stage.Surging); return; }
            if (PageIndex + 1 >= beats.Count) { SetStage(Stage.Closing); return; }
            // The same picture carries on without a fade: only the caption (and possibly the light) changes.
            if (images[PageIndex + 1] == images[PageIndex]) { ShowPage(PageIndex + 1, true); return; }
            SetStage(Stage.TurningOut);
        }

        private void SetStage(Stage value) { stage = value; timer = 0f; }

        public void Tick(float delta, bool readInput = true)
        {
            if (!IsOpen) return;
            delta = Mathf.Max(0f, delta);
            timer += delta;
            pageTime += delta;
            effects.Tick(delta);
            Keyboard keys = readInput && Application.isFocused ? Keyboard.current : null;
            if (keys != null && keys.escapeKey.wasPressedThisFrame) Skip();
            switch (stage)
            {
                case Stage.Opening:
                    Root.style.opacity = Mathf.Clamp01(timer / OpenFade);
                    if (timer >= OpenFade) SetStage(Stage.Showing);
                    break;
                case Stage.TurningOut:
                    SetContentOpacity(1f - Mathf.Clamp01(timer / PageFade));
                    if (timer >= PageFade) { ShowPage(PageIndex + 1, false); SetStage(Stage.TurningIn); }
                    break;
                case Stage.TurningIn:
                    SetContentOpacity(Mathf.Clamp01(timer / PageFade));
                    if (timer >= PageFade) SetStage(Stage.Showing);
                    break;
                case Stage.Showing:
                    StoryBeat beat = beats[PageIndex];
                    if (beat.IsSilent && pageTime >= beat.Hold) Turn();
                    else if (keys != null && (keys.spaceKey.wasPressedThisFrame || keys.enterKey.wasPressedThisFrame
                        || keys.rightArrowKey.wasPressedThisFrame)) Advance();
                    break;
                case Stage.Surging:
                    effects.Surge = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(timer / SurgeSeconds));
                    captionPanel.style.opacity = 1f - Mathf.Clamp01(timer / .5f);
                    float white = Mathf.Clamp01((timer - SurgeSeconds * .55f) / WhiteSeconds);
                    whiteout.style.opacity = white * white;
                    if (white >= 1f)
                    {
                        // Behind the white the memory is gone; letting the white fade shows the present again.
                        canvas.style.opacity = 0f;
                        Root.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
                        SetStage(Stage.Releasing);
                    }
                    break;
                case Stage.Releasing:
                    whiteout.style.opacity = 1f - Mathf.Clamp01(timer / WhiteRelease);
                    if (timer >= WhiteRelease) Close();
                    break;
                case Stage.Closing:
                    Root.style.opacity = Mathf.Min(Root.resolvedStyle.opacity, 1f - Mathf.Clamp01(timer / CloseFade));
                    if (timer >= CloseFade) Close();
                    break;
            }
            if (!IsOpen) return;
            float move = pagePan <= 0f ? 1f : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(pageTime / pagePan));
            StoryFraming.Apply(canvas, StoryShot.Lerp(pageFrom, pageTo, move));
            RefreshCaption();
        }

        private void ShowPage(int index, bool continuing)
        {
            StoryBeat beat = beats[index];
            StoryShot current = StoryShot.Lerp(pageFrom, pageTo, pagePan <= 0f ? 1f : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(pageTime / pagePan)));
            PageIndex = index;
            pageTime = 0f;
            if (continuing)
            {
                pageFrom = current;
                pageTo = beat.Pan > 0f ? beat.To : current;
                pagePan = beat.Pan;
                if (effectIds[index] != effectIds[index - 1]) effects.Show(effectIds[index]);
            }
            else
            {
                picture.image = images[index];
                pageFrom = beat.From;
                pageTo = beat.To;
                pagePan = beat.Pan;
                effects.Show(effectIds[index]);
            }
            // A silent page keeps only the count and the button, like a silent boss page.
            Hud.Set(speaker, beat.Speaker);
            Hud.Show(speaker, !string.IsNullOrEmpty(beat.Speaker));
            caption.text = beat.Line;
            Hud.Show(caption, !beat.IsSilent);
            progress.text = $"{index + 1} / {beats.Count}";
            next.text = index == beats.Count - 1 || beat.WhiteOut ? "계속" : "다음";
            StoryFraming.Apply(canvas, pageFrom);
            RefreshCaption();
        }

        private void RefreshCaption()
        {
            if (stage == Stage.Surging || stage == Stage.Releasing) return;
            StoryBeat beat = beats[PageIndex];
            float shown = beat.IsSilent ? 0f : Mathf.Clamp01((pageTime - beat.LineDelay) / CaptionFade);
            speaker.style.opacity = caption.style.opacity = shown;
        }

        private void SetContentOpacity(float value)
        {
            canvas.style.opacity = value;
            captionPanel.style.opacity = value;
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
            stage = Stage.Closed;
            picture.image = null;
            effects.Show(null);
            whiteout.style.opacity = 0f;
            beats.Clear(); images.Clear(); effectIds.Clear();
            Hud.Show(Root, false);
            SceneId = null;
            finished?.Invoke();
        }
    }
}

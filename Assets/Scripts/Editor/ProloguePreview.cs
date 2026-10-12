using System;
using System.Collections.Generic;
using System.IO;
using Lightbringer.Core;
using Lightbringer.UI;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Lightbringer.EditorTools
{
    // Uses the game's view and effects in an Editor panel. The sequence and source textures are
    // temporary: no campaign, scene setup, imported-art rebuild or viewed-progress writes are needed.
    public sealed class ProloguePreview : EditorWindow
    {
        private const float ViewWidth = 1920f, ViewHeight = 1080f;
        [SerializeField] private int selected;
        private readonly List<Texture2D> textures = new List<Texture2D>();
        private readonly List<(int slide, int caption)> steps = new List<(int, int)>();
        private PrologueSequence sequence;
        private PrologueView view;
        private VisualElement viewport, canvas;
        private PopupField<string> picker;
        private ToolbarToggle pause;
        private Label status;
        private bool paused;
        private double lastTick;

        [MenuItem("Lightbringer/Prologue/Preview")]
        public static void OpenWindow()
        {
            ProloguePreview window = GetWindow<ProloguePreview>("프롤로그 미리보기");
            window.minSize = new Vector2(760f, 480f);
            window.Show();
        }

        private void OnEnable()
        {
            lastTick = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Tick;
            ReleaseSequence();
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            Font font = AssetDatabase.LoadAssetAtPath<Font>("Assets/UI/Fonts/NanumGothic-Regular.ttf");
            if (font != null) rootVisualElement.style.unityFontDefinition = FontDefinition.FromFont(font);

            var toolbar = new Toolbar();
            rootVisualElement.Add(toolbar);
            toolbar.Add(new ToolbarButton(Reload) { text = "원본 새로고침" });
            picker = new PopupField<string>(new List<string> { "불러오는 중" }, 0);
            picker.style.flexGrow = 1f;
            picker.RegisterValueChangedCallback(_ => StartAt(picker.index));
            toolbar.Add(picker);
            toolbar.Add(new ToolbarButton(() => StartAt(selected)) { text = "현재 장면 다시 보기" });
            pause = new ToolbarToggle { text = "일시정지", value = paused };
            pause.RegisterValueChangedCallback(evt => paused = evt.newValue);
            toolbar.Add(pause);

            status = new Label("그림·자막·효과 미리보기 · 음악은 재생하지 않습니다.");
            status.style.whiteSpace = WhiteSpace.Normal;
            rootVisualElement.Add(status);
            viewport = new VisualElement { focusable = true };
            viewport.style.flexGrow = 1f;
            viewport.style.overflow = Overflow.Hidden;
            viewport.style.backgroundColor = Color.black;
            rootVisualElement.Add(viewport);
            canvas = new VisualElement();
            canvas.style.position = Position.Absolute;
            canvas.style.width = ViewWidth;
            canvas.style.height = ViewHeight;
            canvas.style.transformOrigin = new TransformOrigin(0f, 0f, 0f);
            StyleSheet sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(CampaignHUD.StyleSheetPath);
            if (sheet != null) canvas.styleSheets.Add(sheet);
            viewport.Add(canvas);
            viewport.RegisterCallback<GeometryChangedEvent>(_ => Fit());
            viewport.RegisterCallback<PointerDownEvent>(_ => viewport.Focus());
            viewport.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (view == null || paused) return;
                if (evt.keyCode == KeyCode.RightArrow || evt.keyCode == KeyCode.Space) view.Advance();
                else if (evt.keyCode == KeyCode.LeftArrow) view.Previous();
                else return;
                evt.StopPropagation();
            });
            Reload();
        }

        private void Reload()
        {
            ReleaseSequence();
            try
            {
                PrologueSetup.Entry[] entries = PrologueSetup.Resolve();
                sequence = CreateInstance<PrologueSequence>();
                sequence.hideFlags = HideFlags.HideAndDontSave;
                sequence.slides = new PrologueSlide[entries.Length];
                var choices = new List<string>();
                for (int i = 0; i < entries.Length; i++)
                {
                    PrologueSetup.Entry entry = entries[i];
                    string path = Path.Combine(Application.dataPath, "..", "Docs", "ArtReference", "Prologue", entry.File + ".png");
                    var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                    {
                        name = entry.File, hideFlags = HideFlags.HideAndDontSave,
                        filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
                    };
                    textures.Add(texture);
                    if (!texture.LoadImage(File.ReadAllBytes(path), true))
                        throw new InvalidOperationException("이미지를 읽을 수 없습니다: " + entry.File);
                    sequence.slides[i] = new PrologueSlide
                    {
                        id = entry.Id, image = texture, captions = (string[])entry.Captions.Clone()
                    };
                    for (int c = 0; c < entry.Captions.Length; c++)
                    {
                        steps.Add((i, c));
                        string text = entry.Captions[c].Replace('\n', ' ');
                        choices.Add($"{steps.Count} · {entry.Id} · {text.Substring(0, Math.Min(36, text.Length))}");
                    }
                }
                picker.choices = choices;
                picker.SetEnabled(steps.Count > 0);
                StartAt(Mathf.Clamp(selected, 0, steps.Count - 1));
            }
            catch (Exception error)
            {
                ReleaseSequence();
                picker.SetEnabled(false);
                status.text = "미리보기를 불러오지 못했습니다: " + error.Message;
                Debug.LogException(error);
            }
        }

        private void StartAt(int index)
        {
            if (sequence == null || index < 0 || index >= steps.Count) return;
            selected = index;
            paused = false;
            pause.SetValueWithoutNotify(false);
            canvas.Clear();
            view = new PrologueView(canvas, () => status.text = "미리보기 종료 · 위에서 장면을 선택하면 다시 볼 수 있습니다.");
            var step = steps[index];
            view.Open(sequence, step.slide, step.caption);
            picker.SetValueWithoutNotify(picker.choices[index]);
            status.text = "그림·자막·효과 미리보기 · ← 이전 / → 또는 Space 다음 · 음악은 재생하지 않습니다.";
            lastTick = EditorApplication.timeSinceStartup;
            Fit();
        }

        private void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            float delta = Mathf.Clamp((float)(now - lastTick), 0f, 0.1f);
            lastTick = now;
            if (view == null || !view.IsOpen || paused) return;
            view.Tick(delta, false);
            if (view.IsOpen)
            {
                int index = steps.FindIndex(step => step.slide == view.SlideIndex && step.caption == view.CaptionIndex);
                if (index >= 0 && index != selected)
                {
                    selected = index;
                    picker.SetValueWithoutNotify(picker.choices[index]);
                }
            }
            Repaint();
        }

        private void Fit()
        {
            if (canvas == null || viewport == null) return;
            float width = viewport.contentRect.width, height = viewport.contentRect.height;
            float scale = Mathf.Min(width / ViewWidth, height / ViewHeight);
            if (scale <= 0f || float.IsNaN(scale)) return;
            canvas.style.scale = new Scale(new Vector3(scale, scale, 1f));
            canvas.style.left = (width - ViewWidth * scale) * 0.5f;
            canvas.style.top = (height - ViewHeight * scale) * 0.5f;
        }

        private void ReleaseSequence()
        {
            view = null;
            canvas?.Clear();
            if (sequence != null) DestroyImmediate(sequence);
            sequence = null;
            foreach (Texture2D texture in textures)
                if (texture != null) DestroyImmediate(texture);
            textures.Clear();
            steps.Clear();
        }
    }
}

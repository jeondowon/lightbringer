using System.Collections.Generic;
using Lightbringer.Visuals;
using UnityEditor;
using UnityEngine;

namespace Lightbringer.EditorTools
{
    // Editor window that plays the female hero's clips on the hero built exactly as in battle (model, scale, staff).
    // Open it from Lightbringer > Art > Hero Motion Viewer. Drag in the view to orbit, scroll to zoom.
    public sealed class HeroMotionViewer : EditorWindow
    {
        private const string Folder = ArtModelImport.FemaleHeroFolder + "/Animations";
        private const float EndHold = .5f;
        private struct Entry { public string Label; public AnimationClip Clip; public bool Action, Authored; }

        private readonly List<Entry> entries = new List<Entry>();
        private PreviewRenderUtility preview;
        private GameObject owner, model;
        private AnimationClip idle;
        private string problem = "";
        private int selected;
        private float time, speed = 1f, yaw = 35f, pitch = 8f, distance = 5.5f;
        private bool playing = true, loop = true;
        private double lastTick;

        [MenuItem("Lightbringer/Art/Hero Motion Viewer")]
        private static void Open() => GetWindow<HeroMotionViewer>("Hero Motions").minSize = new Vector2(440f, 560f);

        private void OnEnable()
        {
            lastTick = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
            Build();
        }

        private void OnDisable()
        {
            EditorApplication.update -= Tick;
            Cleanup();
        }

        private void Build()
        {
            Cleanup();
            problem = "";
            ArtStyleLibrary library = AssetDatabase.LoadAssetAtPath<ArtStyleLibrary>(ArtStyleSetup.LibraryPath);
            VisualOverride hero = library != null ? library.FindOverride(VisualId.Hero) : null;
            if (hero == null || hero.prefab == null) { problem = "Art Style Library에 영웅 모델이 없습니다."; return; }

            preview = new PreviewRenderUtility();
            preview.cameraFieldOfView = 30f;
            preview.camera.nearClipPlane = .1f;
            preview.camera.farClipPlane = 60f;
            preview.camera.clearFlags = CameraClearFlags.SolidColor;
            preview.camera.backgroundColor = new Color(.2f, .22f, .27f);
            preview.lights[0].intensity = 1.3f;
            preview.lights[0].transform.rotation = Quaternion.Euler(40f, -30f, 0f);
            preview.ambientColor = new Color(.55f, .58f, .65f);

            owner = new GameObject("Hero Motion Viewer Hero");
            Transform visual = UnitAppearance.Attach(owner, VisualId.Hero, library, 0f);
            if (visual == null || visual.childCount == 0) { problem = "영웅 모델을 만들 수 없습니다."; return; }
            model = visual.GetChild(0).gameObject;
            foreach (MonoBehaviour behaviour in model.GetComponents<MonoBehaviour>()) behaviour.enabled = false;
            preview.AddSingleGO(owner);

            idle = hero.idleClip;
            entries.Clear();
            Add("Idle", hero.idleClip, false, false);
            Add("Move", hero.moveClip, false, false);
            Add("Cast (메테오)", hero.actionClip, true, false);
            foreach (string name in new[] { "Thrust", "Slam", "Raise" })
                Add(name, AssetDatabase.LoadAssetAtPath<AnimationClip>($"{Folder}/FemaleHero_Anim_{name}.anim"), true, true);
            selected = Mathf.Clamp(selected, 0, Mathf.Max(0, entries.Count - 1));
            time = 0f;
        }

        private void Add(string label, AnimationClip clip, bool action, bool authored)
        {
            if (clip != null) entries.Add(new Entry { Label = label, Clip = clip, Action = action, Authored = authored });
        }

        private void Cleanup()
        {
            if (owner != null) DestroyImmediate(owner);
            owner = null; model = null;
            if (preview != null) preview.Cleanup();
            preview = null;
        }

        private void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            float delta = (float)(now - lastTick);
            lastTick = now;
            if (!playing || model == null || entries.Count == 0) return;
            float length = entries[selected].Clip.length;
            time += delta * speed;
            if (time > length + EndHold)
            {
                if (loop) time = 0f;
                else { time = length; playing = false; }
            }
            Repaint();
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("다시 불러오기", EditorStyles.toolbarButton, GUILayout.Width(90f))) { Build(); return; }
                GUILayout.Label("클립이 새로 만들어지면 눌러 주세요.", EditorStyles.miniLabel);
            }
            if (!string.IsNullOrEmpty(problem)) { EditorGUILayout.HelpBox(problem, MessageType.Warning); return; }
            if (entries.Count == 0) { EditorGUILayout.HelpBox("재생할 클립이 없습니다.", MessageType.Info); return; }

            string[] labels = new string[entries.Count];
            for (int i = 0; i < labels.Length; i++) labels[i] = entries[i].Label;
            int pick = GUILayout.SelectionGrid(selected, labels, 3);
            if (pick != selected) { selected = pick; time = 0f; playing = true; }
            if (!HasNewClips()) EditorGUILayout.HelpBox("Thrust / Slam / Raise 클립이 아직 없습니다. Lightbringer > Art > Build Hero Skill Motions 를 실행하세요.", MessageType.Info);

            Rect view = GUILayoutUtility.GetRect(10f, 10f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            HandleInput(view);
            if (Event.current.type == EventType.Repaint && preview != null && model != null)
            {
                ApplyPose();
                preview.BeginPreview(view, GUIStyle.none);
                Vector3 target = new Vector3(0f, 1f, 0f);
                preview.camera.transform.position = target + Quaternion.Euler(-pitch, yaw, 0f) * new Vector3(0f, 0f, distance);
                preview.camera.transform.LookAt(target);
                preview.camera.Render();
                Texture result = preview.EndPreview();
                GUI.DrawTexture(view, result, ScaleMode.StretchToFill, false);
            }

            float length = entries[selected].Clip.length;
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(playing ? "일시정지" : "재생", GUILayout.Width(80f)))
                {
                    if (!playing && time >= length) time = 0f;
                    playing = !playing;
                }
                loop = GUILayout.Toggle(loop, "반복", GUILayout.Width(50f));
                GUILayout.Label("속도", GUILayout.Width(32f));
                speed = GUILayout.HorizontalSlider(speed, .1f, 2f, GUILayout.Width(120f));
                GUILayout.Label(speed.ToString("0.0#") + "x", GUILayout.Width(40f));
                GUILayout.FlexibleSpace();
                GUILayout.Label($"{Mathf.Min(time, length):0.00} / {length:0.00}s", GUILayout.Width(100f));
            }
            float scrub = GUILayout.HorizontalSlider(Mathf.Min(time, length), 0f, length);
            if (!Mathf.Approximately(scrub, Mathf.Min(time, length))) { time = scrub; playing = false; }
        }

        private bool HasNewClips()
        {
            foreach (Entry entry in entries) if (entry.Label == "Thrust") return true;
            return false;
        }

        private void HandleInput(Rect view)
        {
            Event e = Event.current;
            if (!view.Contains(e.mousePosition)) return;
            if (e.type == EventType.MouseDrag)
            {
                yaw += e.delta.x * .5f;
                pitch = Mathf.Clamp(pitch + e.delta.y * .3f, -10f, 60f);
                e.Use(); Repaint();
            }
            else if (e.type == EventType.ScrollWheel)
            {
                distance = Mathf.Clamp(distance + e.delta.y * .15f, 2f, 12f);
                e.Use(); Repaint();
            }
        }

        // Layers the clip over idle like the battle does, then applies the staff correction used in play.
        private void ApplyPose()
        {
            Entry entry = entries[selected];
            if (idle != null && entry.Clip != idle) idle.SampleAnimation(model, idle.length > 0f ? time % idle.length : 0f);
            float length = entry.Clip.length;
            entry.Clip.SampleAnimation(model, entry.Clip.wrapMode == WrapMode.Loop && length > 0f ? time % length : Mathf.Min(time, length));
            bool playingAction = entry.Action && time < length;
            foreach (PropStabilizer prop in model.GetComponentsInChildren<PropStabilizer>(true))
            {
                prop.SetCasting(playingAction && !entry.Authored);
                prop.SetAuthored(playingAction && entry.Authored);
                prop.Apply(10f);
            }
        }
    }
}

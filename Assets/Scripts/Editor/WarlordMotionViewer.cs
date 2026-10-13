using System.Collections.Generic;
using Lightbringer.Combat;
using Lightbringer.Units;
using Lightbringer.Visuals;
using UnityEditor;
using UnityEngine;

namespace Lightbringer.EditorTools
{
    // Editor window that plays the Warlord's clips on the Warlord built exactly as in battle (model, scale,
    // greatsword) at the rates the battle plays them. The run can scroll a checkered ground under the feet at
    // the move clip's ground speed, so a planted foot that slides shows against the squares.
    // Open it from Lightbringer > Art > Warlord Motion Viewer. Drag in the view to orbit, scroll to zoom.
    public sealed class WarlordMotionViewer : EditorWindow
    {
        private const float EndHold = .5f, Tile = 1f;
        private struct Entry { public string Label; public AnimationClip Clip; public float Rate; public bool Moves; }

        private readonly List<Entry> entries = new List<Entry>();
        private readonly List<(SkinnedMeshRenderer skin, Mesh posed)> baked = new List<(SkinnedMeshRenderer, Mesh)>();
        private PreviewRenderUtility preview;
        private GameObject owner, model, ground;
        private Material groundMaterial;
        private Texture2D checker;
        private float groundSpeed;
        private string problem = "";
        private int selected;
        private float time, speed = 1f, yaw = 60f, pitch = 10f, distance = 8f;
        private bool playing = true, loop = true, scroll = true;
        private double lastTick;

        [MenuItem("Lightbringer/Art/Warlord Motion Viewer")]
        private static void Open() => GetWindow<WarlordMotionViewer>("군주 모션").minSize = new Vector2(480f, 600f);

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
            VisualOverride warlord = library != null ? library.FindOverride(VisualId.EnemyBoss) : null;
            if (warlord == null || warlord.prefab == null) { problem = "Art Style Library에 군주 모델이 없습니다. Lightbringer > Art > Build Enemy Rigs 를 실행하세요."; return; }

            preview = new PreviewRenderUtility();
            preview.cameraFieldOfView = 30f;
            preview.camera.nearClipPlane = .1f;
            preview.camera.farClipPlane = 80f;
            preview.camera.clearFlags = CameraClearFlags.SolidColor;
            preview.camera.backgroundColor = new Color(.2f, .22f, .27f);
            preview.lights[0].intensity = 1.3f;
            preview.lights[0].transform.rotation = Quaternion.Euler(40f, -30f, 0f);
            preview.ambientColor = new Color(.55f, .58f, .65f);

            owner = new GameObject("Warlord Motion Viewer");
            Transform visual = UnitAppearance.Attach(owner, VisualId.EnemyBoss, library, 0f);
            if (visual == null || visual.childCount == 0) { problem = "군주 모델을 만들 수 없습니다."; return; }
            model = visual.GetChild(0).gameObject;
            foreach (MonoBehaviour behaviour in model.GetComponents<MonoBehaviour>()) behaviour.enabled = false;
            // Preview scenes do not re-skin after SampleAnimation, so each pose is baked into a plain mesh.
            foreach (SkinnedMeshRenderer skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var posed = new Mesh { name = skin.name + " (posed)" };
                var copy = new GameObject(skin.name + " Posed");
                copy.transform.SetParent(skin.transform, false);
                copy.AddComponent<MeshFilter>().sharedMesh = posed;
                copy.AddComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials;
                // Whether BakeMesh returns local or scaled vertices: match the bind-pose height in local units.
                if (warlord.idleClip != null) warlord.idleClip.SampleAnimation(model, 0f);
                skin.BakeMesh(posed);
                float bakedHeight = posed.bounds.size.y;
                if (bakedHeight > .001f) copy.transform.localScale = Vector3.one * Mathf.Clamp(skin.sharedMesh.bounds.size.y / bakedHeight, .2f, 5f);
                skin.enabled = false;
                baked.Add((skin, posed));
            }
            preview.AddSingleGO(owner);
            BuildGround();
            groundSpeed = warlord.moveClipSpeed;

            // The rates the battle uses: melee swings fill the attack interval, specials stretch the clip so the
            // blow lands when the telegraph ends (BossCombat.Windup).
            float contact = EnemyCatalog.StrikePhase(EnemyKind.Boss);
            float melee = EnemyCatalog.AttackInterval(EnemyKind.Boss) * UnitCombat.ActionShare;
            entries.Clear();
            Add("대기", warlord.idleClip, 1f, false);
            Add("달리기", warlord.moveClip, 1f, true);
            Add("근접 내려치기", warlord.actionClip, ActionRate(warlord.actionClip, melee), false);
            Add("특수기 (경고 1.25초)", warlord.actionClip, ActionRate(warlord.actionClip, 1.25f / contact), false);
            Add("충격파 (경고 1.5초)", warlord.actionClip, ActionRate(warlord.actionClip, 1.5f / contact), false);
            Add("낙뢰 (경고 1.6초)", warlord.actionClip, ActionRate(warlord.actionClip, 1.6f / contact), false);
            selected = Mathf.Clamp(selected, 0, Mathf.Max(0, entries.Count - 1));
            time = 0f;
        }

        private static float ActionRate(AnimationClip clip, float duration) => clip != null ? clip.length / Mathf.Max(.1f, duration) : 1f;

        private void Add(string label, AnimationClip clip, float rate, bool moves)
        {
            if (clip != null) entries.Add(new Entry { Label = label, Clip = clip, Rate = rate, Moves = moves });
        }

        // 1 m squares on a 40 m plane; scrolling it by whole periods looks endless.
        private void BuildGround()
        {
            checker = new Texture2D(2, 2, TextureFormat.RGB24, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat, hideFlags = HideFlags.HideAndDontSave };
            Color dark = new Color(.24f, .28f, .22f), light = new Color(.34f, .39f, .3f);
            checker.SetPixels(new[] { dark, light, light, dark });
            checker.Apply();
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            groundMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave, mainTexture = checker };
            groundMaterial.mainTextureScale = Vector2.one * 40f / (2f * Tile);
            ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            DestroyImmediate(ground.GetComponent<Collider>());
            ground.transform.localScale = Vector3.one * 4f;
            ground.GetComponent<MeshRenderer>().sharedMaterial = groundMaterial;
            preview.AddSingleGO(ground);
        }

        private void Cleanup()
        {
            if (owner != null) DestroyImmediate(owner);
            if (ground != null) DestroyImmediate(ground);
            foreach ((SkinnedMeshRenderer _, Mesh posed) in baked) if (posed != null) DestroyImmediate(posed);
            baked.Clear();
            if (groundMaterial != null) DestroyImmediate(groundMaterial);
            if (checker != null) DestroyImmediate(checker);
            owner = null; model = null; ground = null; groundMaterial = null; checker = null;
            if (preview != null) preview.Cleanup();
            preview = null;
        }

        private void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            float delta = (float)(now - lastTick);
            lastTick = now;
            if (!playing || model == null || entries.Count == 0) return;
            Entry entry = entries[selected];
            float length = entry.Clip.length / entry.Rate; // battle seconds
            time += delta * speed;
            if (!Looping(entry) && time > length + EndHold)
            {
                if (loop) time = 0f;
                else { time = length; playing = false; }
            }
            Repaint();
        }

        private static bool Looping(Entry entry) => entry.Clip.wrapMode == WrapMode.Loop;

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("다시 불러오기", EditorStyles.toolbarButton, GUILayout.Width(90f))) { Build(); return; }
                GUILayout.Label("리그를 다시 빌드하면 눌러 주세요.", EditorStyles.miniLabel);
            }
            if (!string.IsNullOrEmpty(problem)) { EditorGUILayout.HelpBox(problem, MessageType.Warning); return; }
            if (entries.Count == 0) { EditorGUILayout.HelpBox("재생할 클립이 없습니다.", MessageType.Info); return; }

            string[] labels = new string[entries.Count];
            for (int i = 0; i < labels.Length; i++) labels[i] = entries[i].Label;
            int pick = GUILayout.SelectionGrid(selected, labels, 3);
            if (pick != selected) { selected = pick; time = 0f; playing = true; }
            Entry entry = entries[selected];

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("시점", GUILayout.Width(32f));
                if (GUILayout.Button("옆", EditorStyles.miniButtonLeft)) { yaw = 90f; pitch = 5f; distance = 8f; }
                if (GUILayout.Button("앞 3/4", EditorStyles.miniButtonMid)) { yaw = 35f; pitch = 12f; distance = 8f; }
                if (GUILayout.Button("게임 (위에서)", EditorStyles.miniButtonRight)) { yaw = 20f; pitch = 28f; distance = 12f; }
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(!entry.Moves))
                    scroll = GUILayout.Toggle(scroll, $"바닥 이동 ({groundSpeed:0.0} m/s)", GUILayout.Width(150f));
            }

            Rect view = GUILayoutUtility.GetRect(10f, 10f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            HandleInput(view);
            if (Event.current.type == EventType.Repaint && preview != null && model != null)
            {
                ApplyPose(entry);
                preview.BeginPreview(view, GUIStyle.none);
                Vector3 target = new Vector3(0f, 1.3f, 0f);
                preview.camera.transform.position = target + Quaternion.Euler(-pitch, yaw, 0f) * new Vector3(0f, 0f, distance);
                preview.camera.transform.LookAt(target);
                preview.camera.Render();
                Texture result = preview.EndPreview();
                GUI.DrawTexture(view, result, ScaleMode.StretchToFill, false);
            }

            float length = entry.Clip.length / entry.Rate;
            float shown = Looping(entry) ? time % length : Mathf.Min(time, length);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(playing ? "일시정지" : "재생", GUILayout.Width(80f)))
                {
                    if (!playing && time >= length && !Looping(entry)) time = 0f;
                    playing = !playing;
                }
                loop = GUILayout.Toggle(loop, "반복", GUILayout.Width(50f));
                GUILayout.Label("속도", GUILayout.Width(32f));
                speed = GUILayout.HorizontalSlider(speed, .1f, 2f, GUILayout.Width(120f));
                GUILayout.Label(speed.ToString("0.0#") + "x", GUILayout.Width(40f));
                GUILayout.FlexibleSpace();
                GUILayout.Label($"{shown:0.00} / {length:0.00}s", GUILayout.Width(100f));
            }
            float scrub = GUILayout.HorizontalSlider(shown, 0f, length);
            if (!Mathf.Approximately(scrub, shown)) { time = scrub; playing = false; }
            EditorGUILayout.LabelField("시간은 전투 기준입니다(속도 1x = 게임과 같은 빠르기). 드래그: 회전, 휠: 확대/축소", EditorStyles.miniLabel);
        }

        private void HandleInput(Rect view)
        {
            Event e = Event.current;
            if (!view.Contains(e.mousePosition)) return;
            if (e.type == EventType.MouseDrag)
            {
                yaw += e.delta.x * .5f;
                pitch = Mathf.Clamp(pitch + e.delta.y * .3f, -5f, 70f);
                e.Use(); Repaint();
            }
            else if (e.type == EventType.ScrollWheel)
            {
                distance = Mathf.Clamp(distance + e.delta.y * .25f, 3f, 20f);
                e.Use(); Repaint();
            }
        }

        private void ApplyPose(Entry entry)
        {
            float clipTime = time * entry.Rate, length = entry.Clip.length;
            entry.Clip.SampleAnimation(model, Looping(entry) && length > 0f ? clipTime % length : Mathf.Min(clipTime, length));
            foreach ((SkinnedMeshRenderer skin, Mesh posed) in baked) skin.BakeMesh(posed);
            // The ground slides back under the runner as the battlefield would pass under it.
            float travel = entry.Moves && scroll ? time * groundSpeed : 0f;
            ground.transform.position = new Vector3(0f, 0f, -Mathf.Repeat(travel, 2f * Tile));
        }
    }
}

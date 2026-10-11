using System.IO;
using System.Text;
using System.Reflection;
using Lightbringer.Aura;
using Lightbringer.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Lightbringer.EditorTools
{
    [InitializeOnLoad]
    public static class AuraVisualPreview
    {
        static AuraVisualPreview() => EditorRequests.Register("Docs/Validation/AuraVisual.request", Capture);

        [MenuItem("Lightbringer/Art/Capture Aura Visual Preview")]
        public static void Capture()
        {
            if (Application.isPlaying) return;
            AssetDatabase.Refresh();
            ArtStyleLibrary style = AssetDatabase.LoadAssetAtPath<ArtStyleLibrary>("Assets/Art/ArtStyleLibrary.asset");
            if (style == null) return;
            if (style.auraRunesMaterial.GetTexture("_SigilTex") == null) AuraSigilBaker.Bake();
            Scene original = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var target = new RenderTexture(1280, 900, 24, RenderTextureFormat.ARGBHalf) { antiAliasing = 4 };
            RenderTexture previous = RenderTexture.active;
            Texture2D capture = null;
            Material groundMaterial = null;
            Material markMaterial = null;
            AuraRuneVisual auraVisual = null;
            Vector4 previousAura = Shader.GetGlobalVector("_LB_AuraSphere");
            Color previousAuraColor = Shader.GetGlobalColor("_LB_AuraColor");
            try
            {
                SceneManager.SetActiveScene(scene);
                Vector3 origin = new Vector3(12000f, 0f, 0f);
                var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.transform.position = origin;
                ground.transform.localScale = Vector3.one * 3f;
                groundMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                groundMaterial.color = new Color(0.075f, 0.105f, 0.095f);
                ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;
                var sun = new GameObject("Preview Light").AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.intensity = 1.2f;
                sun.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
                RenderSettings.sun = sun;
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.48f, 0.52f, 0.60f);
                RenderSettings.fog = false;
                var hero = new GameObject("Hero Preview");
                hero.SetActive(false);
                hero.transform.position = origin;
                Pose(UnitAppearance.Attach(hero, VisualId.Hero, style, 0f), VisualId.Hero, style);
                hero.AddComponent<HeroAura>().Configure(6f, 0.25f);
                auraVisual = hero.AddComponent<AuraRuneVisual>();
                auraVisual.Configure(style, 0f);
                hero.SetActive(true);
                // Runtime components do not receive lifecycle callbacks in Edit mode.
                if (auraVisual.Disc == null) InvokeAura(auraVisual, "OnEnable");
                markMaterial = new Material(style.auraRunesMaterial);
                markMaterial.SetFloat("_Intensity", 0.45f);
                markMaterial.SetFloat("_LineWidth", 0.007f);
                markMaterial.SetFloat("_FillAlpha", 0.025f);
                markMaterial.renderQueue = 2995;
                for (int i = 0; i < 3; i++)
                {
                    Vector3 feet = origin + new Vector3(-2.5f + i * 2.5f, 0f, -2f);
                    var unit = new GameObject("Ally Preview");
                    unit.transform.position = feet;
                    Pose(UnitAppearance.Attach(unit, VisualId.Swordsman, style, 0f), VisualId.Swordsman, style);
                    Draw("Lower Leg Veil", ProceduralMeshes.AuraVeil, style.auraBlessingMaterial,
                        feet + Vector3.up * 0.025f, new Vector3(1.35f, 0.65f, 1.35f));
                    Draw("Foot Runes", ProceduralMeshes.GlowQuad, markMaterial,
                        feet + Vector3.up * 0.04f, new Vector3(1.6f, 1f, 1.6f));
                }
                var camera = new GameObject("Aura Preview Camera").AddComponent<Camera>();
                camera.transform.position = origin + new Vector3(8f, 10f, -13f);
                camera.transform.LookAt(origin + Vector3.up * 0.3f);
                camera.fieldOfView = 44f;
                camera.allowHDR = true;
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
                var volume = new GameObject("Aura Preview Post Processing").AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 100f;
                volume.sharedProfile = style.postProcessing;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.065f, 0.085f, 0.1f);
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                capture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                capture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                capture.Apply();
                Directory.CreateDirectory("Docs/Validation");
                File.WriteAllBytes("Docs/Validation/AuraVisualPreview.png", capture.EncodeToPNG());
                camera.transform.position = origin + new Vector3(6f, 3.8f, -9f);
                camera.transform.LookAt(origin + new Vector3(0f, 0.3f, -0.5f));
                camera.Render();
                RenderTexture.active = target;
                capture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                capture.Apply();
                File.WriteAllBytes("Docs/Validation/AuraVisualPreview_Low.png", capture.EncodeToPNG());
                var report = new StringBuilder("Aura visual shader checks\n");
                foreach (string name in new[] { "Lightbringer/Aura Blessing", "Lightbringer/Aura Runes", "Lightbringer/Toon" })
                {
                    Shader shader = Shader.Find(name);
                    if (shader == null) { report.AppendLine(name + ": missing"); continue; }
                    report.AppendLine(name + ": " + (ShaderUtil.ShaderHasError(shader) ? "FAIL" : "PASS"));
                    foreach (var message in ShaderUtil.GetShaderMessages(shader)) report.AppendLine(message.message);
                }
                report.AppendLine("Shared veil mesh: " + ProceduralMeshes.AuraVeil.vertexCount + " vertices; height 0.65 m.");
                HeroAura aura = hero.GetComponent<HeroAura>();
                aura.Configure(9f, 0.25f);
                InvokeAura(auraVisual, "LateUpdate");
                bool grew = Mathf.Approximately(auraVisual.Disc.transform.lossyScale.x, 18f);
                Transform arcs = auraVisual.Disc.transform.GetChild(0);
                bool lowArcs = Mathf.Approximately(arcs.lossyScale.y, 0.55f);
                report.AppendLine((grew && lowArcs ? "PASS" : "FAIL") + ": radius growth scales the circle and keeps floating arcs low.");
                aura.Configure(0f, 0.25f);
                InvokeAura(auraVisual, "LateUpdate");
                report.AppendLine((!auraVisual.Disc.activeSelf ? "PASS" : "FAIL") + ": zero radius hides both disc and arcs.");
                InvokeAura(auraVisual, "OnDisable");
                report.AppendLine((auraVisual.Disc == null && Shader.GetGlobalVector("_LB_AuraSphere").w == 0f ? "PASS" : "FAIL") + ": disabling releases visuals and clears the global radius.");
                report.AppendLine("Static presentation preview; runtime entry/exit remains a Play-mode check.");
                File.WriteAllText("Docs/Validation/AuraVisualChecks.txt", report.ToString());
            }
            finally
            {
                RenderTexture.active = previous;
                if (auraVisual != null && auraVisual.Disc != null) InvokeAura(auraVisual, "OnDisable");
                Shader.SetGlobalVector("_LB_AuraSphere", previousAura);
                Shader.SetGlobalColor("_LB_AuraColor", previousAuraColor);
                if (original.IsValid()) SceneManager.SetActiveScene(original);
                EditorSceneManager.CloseScene(scene, true);
                target.Release();
                Object.DestroyImmediate(target);
                if (capture != null) Object.DestroyImmediate(capture);
                if (groundMaterial != null) Object.DestroyImmediate(groundMaterial);
                if (markMaterial != null) Object.DestroyImmediate(markMaterial);
            }
        }

        private static void InvokeAura(AuraRuneVisual visual, string method) =>
            typeof(AuraRuneVisual).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(visual, null);

        private static void Pose(Transform root, VisualId id, ArtStyleLibrary style)
        {
            if (root == null) return;
            VisualOverride model = style.FindOverride(id);
            if (model != null && model.idleClip != null && root.childCount > 0)
                model.idleClip.SampleAnimation(root.GetChild(0).gameObject, 0.3f);
            foreach (SkinnedMeshRenderer skin in root.GetComponentsInChildren<SkinnedMeshRenderer>())
                skin.updateWhenOffscreen = true;
        }

        private static void Draw(string name, Mesh mesh, Material material, Vector3 position, Vector3 scale)
        {
            var item = new GameObject(name);
            item.transform.position = position;
            item.transform.localScale = scale;
            item.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = item.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }
}

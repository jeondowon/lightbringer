using System.IO;
using System.Text;
using Lightbringer.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Lightbringer.EditorTools
{
    [InitializeOnLoad]
    public static class CelestialStrikePreview
    {
        static CelestialStrikePreview() => EditorRequests.Register("Docs/Validation/CelestialStrike.request", Capture);

        [MenuItem("Lightbringer/Art/Capture Celestial Strike Preview")]
        public static void Capture()
        {
            if (Application.isPlaying) return;
            ArtStyleLibrary style = ArtStyleSetup.EnsureLibrary();
            if (style == null || style.celestialStrikeMaterial == null) throw new System.InvalidOperationException("Missing celestial strike material.");
            Scene original = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            RenderTexture previous = RenderTexture.active;
            bool asyncCompile = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            var target = new RenderTexture(720, 900, 24, RenderTextureFormat.ARGBHalf);
            var tile = new Texture2D(720, 900, TextureFormat.RGB24, false);
            var sheet = new Texture2D(2160, 900, TextureFormat.RGB24, false);
            var floorMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            try
            {
                SceneManager.SetActiveScene(scene);
                Vector3 origin = new Vector3(18000, 0, 0);
                var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
                floor.transform.position = origin;
                floor.transform.localScale = Vector3.one * 8;
                floorMaterial.color = new Color(0.1f, 0.14f, 0.13f);
                floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;
                var sun = new GameObject("Sun").AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.transform.rotation = Quaternion.Euler(48, -30, 0);
                sun.intensity = 1.1f;
                RenderSettings.sun = sun;
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.38f, 0.44f, 0.52f);
                RenderSettings.fog = false;
                var camera = new GameObject("Preview Camera").AddComponent<Camera>();
                camera.transform.position = origin + new Vector3(9, 6, -13);
                camera.transform.LookAt(origin + Vector3.up * 5);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.025f, 0.045f, 0.08f);
                camera.fieldOfView = 55;
                camera.targetTexture = target;
                camera.allowHDR = true;
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
                var volume = new GameObject("Battle Post Processing").AddComponent<Volume>();
                volume.isGlobal = true;
                volume.sharedProfile = style.postProcessing;
                var hero = new GameObject("Hero Scale Reference");
                hero.transform.position = origin + new Vector3(-2.8f, 0, -2);
                Transform model = UnitAppearance.Attach(hero, VisualId.Hero, style, 0);
                var heroStyle = style.FindOverride(VisualId.Hero);
                if (model != null && model.childCount > 0 && heroStyle != null && heroStyle.actionClip != null)
                    heroStyle.actionClip.SampleAnimation(model.GetChild(0).gameObject, 0.3f);
                foreach (var skin in hero.GetComponentsInChildren<SkinnedMeshRenderer>()) skin.updateWhenOffscreen = true;
                float[] ages = { 0.12f, 0.35f, 0.85f };
                var report = new StringBuilder("Celestial Light Staff VFX\n");
                for (int i = 0; i < ages.Length; i++)
                {
                    var strike = CelestialStrikeVfx.Spawn(style, origin, null);
                    strike.Tick(ages[i]);
                    foreach (var particles in strike.GetComponentsInChildren<ParticleSystem>()) particles.Simulate(ages[i], true, true);
                    camera.Render();
                    RenderTexture.active = target;
                    tile.ReadPixels(new Rect(0, 0, 720, 900), 0, 0);
                    tile.Apply();
                    sheet.SetPixels(i * 720, 0, 720, 900, tile.GetPixels());
                    if (i == 0)
                    {
                        Mesh mesh = strike.GetComponentInChildren<MeshFilter>().sharedMesh;
                        report.AppendLine((mesh.bounds.size.y >= 25 && strike.GetComponentInChildren<Collider>() == null ? "PASS" : "FAIL") + ": tall light column; no gameplay colliders.");
                        strike.Tick(CelestialStrikeVfx.Lifetime);
                        report.AppendLine((strike == null && mesh == null ? "PASS" : "FAIL") + ": effect and owned mesh released after lifetime.");
                    }
                    else Object.DestroyImmediate(strike.gameObject);
                }
                sheet.Apply();
                Directory.CreateDirectory("Docs/Validation");
                File.WriteAllBytes("Docs/Validation/CelestialStrikePreview.png", sheet.EncodeToPNG());
                bool shaderError = ShaderUtil.ShaderHasError(style.celestialStrikeMaterial.shader);
                report.AppendLine((shaderError ? "FAIL" : "PASS") + ": celestial shader compilation.");
                foreach (var message in ShaderUtil.GetShaderMessages(style.celestialStrikeMaterial.shader)) report.AppendLine(message.message);
                report.AppendLine("Preview samples: 0.12 / 0.35 / 0.85 seconds. Play-mode combat feel requires manual review.");
                File.WriteAllText("Docs/Validation/CelestialStrikeChecks.txt", report.ToString());
                Debug.Log(report.ToString());
            }
            finally
            {
                RenderTexture.active = previous;
                ShaderUtil.allowAsyncCompilation = asyncCompile;
                if (original.IsValid()) SceneManager.SetActiveScene(original);
                EditorSceneManager.CloseScene(scene, true);
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(tile);
                Object.DestroyImmediate(sheet);
                Object.DestroyImmediate(floorMaterial);
            }
        }
    }
}

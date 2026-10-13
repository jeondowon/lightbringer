using System;
using System.IO;
using Lightbringer.Core;
using Lightbringer.Progression;
using Lightbringer.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Lightbringer.EditorTools
{
    [InitializeOnLoad]
    internal static class ActTwoEnvironmentPreview
    {
        static ActTwoEnvironmentPreview() => EditorRequests.Register("Docs/Diagnostics/act-two-preview.request", Capture);

        [MenuItem("Lightbringer/Art/Capture Act Two Environment")]
        public static void Capture()
        {
            if (File.Exists("Docs/Diagnostics/act-two-environment.request"))
            {
                File.WriteAllText("Docs/Diagnostics/act-two-preview.request", "Capture after setup");
                return;
            }
            const int width = 1280, height = 720;
            string folder = "Docs/ArtPass/Preview/ActTwo";
            Directory.CreateDirectory(folder);
            Scene original = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var target = new RenderTexture(width, height, 24) { antiAliasing = 4 };
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            var fallback = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            RenderTexture previous = RenderTexture.active;
            bool async = ShaderUtil.allowAsyncCompilation;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                SceneManager.SetActiveScene(scene);
                var art = AssetDatabase.LoadAssetAtPath<ArtStyleLibrary>(ArtStyleSetup.LibraryPath);
                foreach (int stage in new[] { 9, 11, 12, 15, 16 })
                {
                    var host = new GameObject("Act Two Preview " + stage);
                    try
                    {
                        PrototypeBattle battle = PrototypeBattleBuilder.Build(stage, new CampaignProgress(), EditorAssets.InputActions,
                            fallback, Shader.Find("Universal Render Pipeline/Unlit"), host.transform, art);
                        battle.Root.SetActive(true);
                        foreach (Camera existing in host.GetComponentsInChildren<Camera>(true)) existing.enabled = false;
                        var camera = new GameObject("Environment Preview Camera").AddComponent<Camera>();
                        camera.transform.SetParent(host.transform, false);
                        camera.enabled = false;
                        camera.scene = scene;
                        camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
                        camera.targetTexture = target;
                        camera.clearFlags = CameraClearFlags.Skybox;
                        camera.fieldOfView = 58;
                        camera.nearClipPlane = .1f;
                        camera.farClipPlane = 250;
                        Vector3 focus = stage == 15 ? new Vector3(0, 0, 33) : new Vector3(0, 1, 27);
                        camera.transform.position = stage == 15 ? new Vector3(28, 23, -5) : new Vector3(28, 15, -9);
                        camera.transform.LookAt(focus);
                        camera.Render();
                        RenderTexture.active = target;
                        pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                        pixels.Apply();
                        RenderTexture.active = previous;
                        int invalid = 0;
                        foreach (Color32 pixel in pixels.GetPixels32())
                            if (pixel.r > 230 && pixel.g < 25 && pixel.b > 230 || pixel.r < 15 && pixel.g > 235 && pixel.b > 235) invalid++;
                        if (invalid > 200) throw new InvalidOperationException($"Stage {stage}: shader error/loading colour visible ({invalid} pixels).");
                        File.WriteAllBytes(folder + "/Stage" + stage + ".png", pixels.EncodeToPNG());
                    }
                    finally { RenderTexture.active = previous; Object.DestroyImmediate(host); }
                }
                File.WriteAllText(folder + "/PreviewChecks.txt", "PASS: Stages 9, 11, 12, 15, 16 rendered with synchronous shader compilation; no error/loading colour.\n");
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = async;
                RenderTexture.active = previous;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(pixels);
                Object.DestroyImmediate(fallback);
                if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}

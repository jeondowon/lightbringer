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
    // Static preview shots of Act 2 battlefields for art iteration. Builds each stage in a throwaway scene,
    // never advances gameplay and never touches the player's campaign save.
    [InitializeOnLoad]
    internal static class ActTwoPhotoCapture
    {
        private const string Output = "Docs/Art/Preview/ActTwo";
        private const string RequestPath = Output + "/capture.request";
        // River crossings only (stages 15 and 18), for water iteration.
        private const string RiverRequestPath = Output + "/river.request";
        private static readonly string LoadedScriptStamp = GreyboxScriptAutoRefresh.GetStamp();

        static ActTwoPhotoCapture()
        {
            Register(RequestPath, Run);
            Register(RiverRequestPath, () => Capture(new[] { 15, 18 }));
        }

        private static void Register(string path, Action job) => EditorRequests.Register(path, () =>
        {
            if (LoadedScriptStamp != GreyboxScriptAutoRefresh.GetStamp())
            {
                File.WriteAllText(path, "Capture after script refresh");
                AssetDatabase.Refresh();
                return;
            }
            job();
        });

        [MenuItem("Lightbringer/Art/Capture Act Two Photoscans")]
        public static void Run() => Capture(new[] { 12, 13, 14, 16, 24 });

        private static void Capture(int[] stages)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play before capturing.");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ArtStyleLibrary library = ArtStyleSetup.EnsureLibrary();
            Scene original = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            bool asyncCompile = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            Material baseMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            try
            {
                SceneManager.SetActiveScene(scene);
                Directory.CreateDirectory(Output);
                foreach (int stage in stages)
                {
                    var host = new GameObject("Act Two capture " + stage);
                    try
                    {
                        var session = host.AddComponent<CampaignSession>();
                        var progress = new CampaignProgress();
                        for (int i = 1; i < stage; i++) progress.CompleteStage(i);
                        session.InitializeForValidation(progress);
                        EditorAssets.ConfigureSession(session, baseMaterial);
                        session.ConfigureArt(library);
                        if (!session.StartBattle()) throw new InvalidOperationException("Could not build stage " + stage);
                        long scanned = 0, total = 0;
                        foreach (MeshFilter filter in session.Battle.Root.GetComponentsInChildren<MeshFilter>(true))
                        {
                            if (filter.sharedMesh == null) continue;
                            long triangles = 0;
                            for (int sub = 0; sub < filter.sharedMesh.subMeshCount; sub++) triangles += filter.sharedMesh.GetIndexCount(sub) / 3;
                            total += triangles;
                            if (filter.sharedMesh.name.StartsWith("Scan_")) scanned += triangles;
                        }
                        var scenery = session.Battle.Root.GetComponent<ActTwoScenery>();
                        Debug.Log($"Act Two stage {stage}: {scenery.ScannedProps} photoscans, {scanned:N0} scanned triangles, {total:N0} battlefield triangles.");
                        Camera camera = session.Battle.Camera;
                        camera.scene = scene;
                        Capture(camera, new Vector3(0, 12, -12), new Vector3(0, 1, 16), $"Stage{stage}_Hero");
                        Capture(camera, new Vector3(30, 45, -60), new Vector3(0, 0, 30), $"Stage{stage}_Overview");
                        // Valley slopes from the player's side, where the cliff and boulder scans sit.
                        Capture(camera, new Vector3(-6, 7, -18), new Vector3(-52, 6, 6), $"Stage{stage}_SlopeWest");
                        Capture(camera, new Vector3(6, 7, -18), new Vector3(52, 6, 6), $"Stage{stage}_SlopeEast");
                        // Close-ups of every photographic depot, storeyard and wagon yard.
                        Transform field = session.Battle.Root.GetComponentInChildren<BattlefieldEnvironment>(true).transform;
                        foreach (Transform child in field)
                        {
                            if (child.GetComponent<MeshRenderer>() == null || child.GetComponent<MeshRenderer>().sharedMaterials.Length < 6
                                || child.name.Contains("Citadel")) continue;
                            Vector3 at = field.TransformPoint(child.GetComponent<MeshFilter>().sharedMesh.bounds.center);
                            Capture(camera, at + new Vector3(-5.5f, 3.4f, -7.5f), at + new Vector3(0, .2f, 0), $"Stage{stage}_{child.name.Replace(" ", "")}");
                        }
                        if (scenery.HasRiverCrossing)
                        {
                            Capture(camera, new Vector3(-4, 16, 20), new Vector3(0, 0, 40), $"Stage{stage}_River");
                            Capture(camera, new Vector3(-14, 4.5f, 33), new Vector3(-8, 0, 40), $"Stage{stage}_RiverBank");
                        }
                        if (stage == 24)
                        {
                            Capture(camera, new Vector3(0, 13, 28), new Vector3(0, 30, 112), "Stage24_CitadelBattle");
                            Capture(camera, new Vector3(-30, 22, 64), new Vector3(0, 26, 112), "Stage24_CitadelNear");
                            Capture(camera, new Vector3(4, 12, 91), new Vector3(0, 10, 104), "Stage24_CitadelGate");
                        }
                    }
                    finally { Object.DestroyImmediate(host); }
                }
                Debug.Log("Act Two photoscan capture complete. " + Output);
            }
            finally
            {
                Object.DestroyImmediate(baseMaterial);
                if (original.IsValid()) SceneManager.SetActiveScene(original);
                EditorSceneManager.CloseScene(scene, true);
                ShaderUtil.allowAsyncCompilation = asyncCompile;
            }
        }

        private static void Capture(Camera camera, Vector3 eye, Vector3 target, string name)
        {
            var texture = new RenderTexture(1280, 720, 24);
            RenderTexture previous = RenderTexture.active;
            RenderTexture oldTarget = camera.targetTexture;
            try
            {
                camera.transform.position = eye;
                camera.transform.LookAt(target);
                camera.fieldOfView = 50f;
                camera.targetTexture = texture;
                camera.useOcclusionCulling = false;
                camera.Render();
                RenderTexture.active = texture;
                var image = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
                try
                {
                    image.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                    image.Apply();
                    File.WriteAllBytes(Output + "/" + name + ".png", image.EncodeToPNG());
                }
                finally { Object.DestroyImmediate(image); }
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = oldTarget;
                texture.Release(); Object.DestroyImmediate(texture);
            }
        }
    }
}

using System;
using System.IO;
using Lightbringer.Combat;
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
    // Static preview shots of the Demon King's smoke form over the final citadel and of its abyss core, for art
    // iteration. Builds stage 24 in a throwaway scene, opens the eyes and raises a core without starting the fight,
    // and never touches the player's campaign save.
    // Request file: Docs/Art/Preview/DemonKing/capture.request.
    [InitializeOnLoad]
    internal static class DemonKingCapture
    {
        private const string Output = "Docs/Art/Preview/DemonKing";
        private const string RequestPath = Output + "/capture.request";
        private static readonly string LoadedScriptStamp = GreyboxScriptAutoRefresh.GetStamp();

        static DemonKingCapture() => EditorRequests.Register(RequestPath, () =>
        {
            if (LoadedScriptStamp != GreyboxScriptAutoRefresh.GetStamp())
            {
                File.WriteAllText(RequestPath, "Capture after script refresh");
                AssetDatabase.Refresh();
                return;
            }
            Run();
        });

        [MenuItem("Lightbringer/Art/Capture Demon King")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play before capturing.");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ArtStyleLibrary library = ArtStyleSetup.EnsureLibrary();
            Scene original = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            bool asyncCompile = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            Material baseMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var host = new GameObject("Demon King capture");
            try
            {
                SceneManager.SetActiveScene(scene);
                Directory.CreateDirectory(Output);
                var session = host.AddComponent<CampaignSession>();
                var progress = new CampaignProgress();
                for (int i = 1; i < 24; i++) progress.CompleteStage(i);
                session.InitializeForValidation(progress);
                EditorAssets.ConfigureSession(session, baseMaterial);
                session.ConfigureArt(library);
                if (!session.StartBattle()) throw new InvalidOperationException("Could not build stage 24");
                DemonKingSky sky = session.Battle.Root.GetComponentInChildren<DemonKingSky>(true);
                if (sky == null) throw new InvalidOperationException("Stage 24 has no Demon King sky");
                Camera camera = session.Battle.Camera;
                camera.scene = scene;
                Vector3 centre = session.Battle.Objective.EnemyBase.transform.position;
                centre.y = session.Battle.Root.transform.position.y;
                // The finale reveals the sky after the stronghold falls; hide it so the dialogue frame matches play.
                foreach (Renderer renderer in session.Battle.Objective.EnemyBase.GetComponentsInChildren<Renderer>()) renderer.enabled = false;

                sky.Tick(.1f);
                Capture(camera, new Vector3(0, 13, 28), new Vector3(0, 42, 140), 55, "Dormant_Battle");
                sky.Reveal();
                for (int i = 0; i < 40; i++) sky.Tick(.1f);
                // The finale dialogue framing (BossEncounter.CameraFocus), battle views and a close look at the face.
                Vector3 focus = centre + new Vector3(0, 15, 8);
                Capture(camera, focus + new Vector3(6, -11.5f, -36), focus, 60, "Revealed_Dialogue");
                Capture(camera, new Vector3(0, 13, 28), new Vector3(0, 42, 140), 55, "Revealed_Battle");
                Capture(camera, new Vector3(-40, 20, 40), new Vector3(0, 50, 150), 55, "Revealed_Flank");
                Capture(camera, new Vector3(0, 9, 10), new Vector3(0, 45, 150), 60, "Revealed_Wide");
                Capture(camera, new Vector3(0, 60, 70), new Vector3(0, 78, 150), 40, "Revealed_Face");

                // The abyss core on the ruined stronghold, bound to the sky as BossEncounter.SummonDemonKing does.
                var coreHost = new GameObject("Abyss Core (capture)");
                coreHost.transform.SetParent(session.Battle.Root.transform, false);
                coreHost.transform.position = centre;
                Combatant core = coreHost.AddComponent<Combatant>();
                core.UseTeamTint = false;
                core.Configure(Faction.Enemy, 1000);
                AbyssCoreVisual visual = AbyssCoreVisual.Build(coreHost.transform, core, sky);
                sky.BindCore(coreHost.transform);
                for (int i = 0; i < 30; i++) { visual.Tick(.1f); sky.Tick(.1f); }
                Capture(camera, centre + new Vector3(7, 3.5f, -13), centre + Vector3.up * 3.6f, 45, "Core_Close");
                Capture(camera, centre + new Vector3(0, 8, -24), centre + Vector3.up * 3, 55, "Core_Battle");
                Capture(camera, centre + new Vector3(10, 2, -26), centre + new Vector3(0, 26, 40), 60, "Core_Stream");
                core.TakeDamage(400);
                sky.Flinch(); // DemonKingCombat relays core blows to the sky.
                visual.Tick(.02f);
                Capture(camera, centre + new Vector3(7, 3.5f, -13), centre + Vector3.up * 3.6f, 45, "Core_Hit");
                sky.Tick(.3f);
                Capture(camera, centre + new Vector3(10, 2, -26), centre + new Vector3(0, 26, 40), 60, "Core_Surge");

                sky.SetCharge(1);
                for (int i = 0; i < 10; i++) { sky.Tick(.1f); visual.Tick(.1f); }
                Capture(camera, new Vector3(0, 13, 28), new Vector3(0, 42, 140), 55, "Charged_Battle");
                Capture(camera, centre + new Vector3(7, 3.5f, -13), centre + Vector3.up * 3.6f, 45, "Core_Charged");
                Debug.Log("Demon King capture complete. " + Output);
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(baseMaterial);
                if (original.IsValid()) SceneManager.SetActiveScene(original);
                EditorSceneManager.CloseScene(scene, true);
                ShaderUtil.allowAsyncCompilation = asyncCompile;
            }
        }

        private static void Capture(Camera camera, Vector3 eye, Vector3 target, float fov, string name)
        {
            var texture = new RenderTexture(1280, 720, 24);
            RenderTexture previous = RenderTexture.active;
            RenderTexture oldTarget = camera.targetTexture;
            try
            {
                camera.transform.position = eye;
                camera.transform.LookAt(target);
                camera.fieldOfView = fov;
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

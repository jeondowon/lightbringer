using System.Collections.Generic;
using System.IO;
using Lightbringer.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lightbringer.EditorTools
{
    // Renders contact sheets of the hero override (model + scale + props, built exactly as in battle)
    // sampled across its idle / move / action clips, from the front and the side.
    // Output: Docs/ArtPass/Preview/HeroAnimation_Front.png and HeroAnimation_Side.png.
    // Also runs automatically when Docs/ArtPass/Preview/capture.request exists (the file is then removed).
    [InitializeOnLoad]
    public static class ArtAnimationPreview
    {
        private const int Columns = 6;
        private const int TileWidth = 320, TileHeight = 400;
        // Far from the origin so objects in the open scene (e.g. the greybox player) stay out of frame.
        private static readonly Vector3 Stage = new Vector3(5000f, 0f, 0f);

        static ArtAnimationPreview()
        {
            EditorRequests.Register(Path.Combine(PreviewFolder, "battlefield.request"), CaptureBattlefield);
            EditorRequests.Register(Path.Combine(PreviewFolder, "forest-ground.request"), CaptureForestGround);
            EditorRequests.Register(Path.Combine(PreviewFolder, "capture.request"), Capture);
        }

        private static string PreviewFolder => Path.Combine(Path.GetDirectoryName(Application.dataPath), "Docs", "ArtPass", "Preview");

        [MenuItem("Lightbringer/Art/Capture Hero Animation Preview")]
        public static void Capture()
        {
            if (Application.isPlaying) { Debug.LogWarning("Stop Play mode before capturing the hero animation preview."); return; }
            ArtStyleLibrary library = ArtStyleSetup.EnsureLibrary();
            VisualOverride hero = library != null ? library.FindOverride(VisualId.Hero) : null;
            if (hero == null) { Debug.LogError("Preview: no Hero override in the Art Style Library."); return; }
            var clips = new List<(string label, AnimationClip clip)>();
            if (hero.idleClip != null) clips.Add(("Idle", hero.idleClip));
            if (hero.moveClip != null) clips.Add(("Move", hero.moveClip));
            if (hero.actionClip != null) clips.Add(("Action", hero.actionClip));
            if (clips.Count == 0) clips.Add(("Bind pose", null));

            Scene original = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            RenderTexture target = new RenderTexture(TileWidth, TileHeight, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            try
            {
                SceneManager.SetActiveScene(scene);
                GameObject owner = new GameObject("Preview Hero");
                owner.transform.position = Stage;
                Transform visual = UnitAppearance.Attach(owner, VisualId.Hero, library, 0f);
                if (visual == null || visual.childCount == 0) { Debug.LogError("Preview: hero override could not be built."); return; }
                GameObject model = visual.GetChild(0).gameObject;
                foreach (MonoBehaviour behaviour in model.GetComponents<MonoBehaviour>()) behaviour.enabled = false;

                GameObject sun = new GameObject("Preview Sun");
                Light light = sun.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.3f;
                sun.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
                RenderSettings.ambientLight = new Color(0.55f, 0.58f, 0.65f);
                GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.transform.localScale = Vector3.one * 2f;
                ground.transform.position = Stage;

                Camera camera = new GameObject("Preview Camera").AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.82f, 0.85f, 0.9f);
                camera.fieldOfView = 30f;
                camera.targetTexture = target;

                Directory.CreateDirectory(PreviewFolder);
                RenderSheet(camera, model, clips, Stage + new Vector3(0f, 1.15f, 7.2f), "HeroAnimation_Front.png");
                RenderSheet(camera, model, clips, Stage + new Vector3(7.2f, 1.15f, 0f), "HeroAnimation_Side.png");
                RenderStrike(camera, model, hero, library);
                Debug.Log($"Hero animation preview written to {PreviewFolder} ({clips.Count} clip row(s) x {Columns} frames).");
            }
            finally
            {
                if (original.IsValid()) SceneManager.SetActiveScene(original);
                EditorSceneManager.CloseScene(scene, true);
                target.Release();
                Object.DestroyImmediate(target);
            }
        }

        private static void RenderSheet(Camera camera, GameObject model, List<(string label, AnimationClip clip)> clips,
            Vector3 cameraPosition, string fileName)
        {
            camera.transform.position = cameraPosition;
            camera.transform.LookAt(Stage + new Vector3(0f, 1.05f, 0f));
            Texture2D sheet = new Texture2D(TileWidth * Columns, TileHeight * clips.Count, TextureFormat.RGB24, false);
            Texture2D tile = new Texture2D(TileWidth, TileHeight, TextureFormat.RGB24, false);
            for (int row = 0; row < clips.Count; row++)
            {
                for (int column = 0; column < Columns; column++)
                {
                    AnimationClip clip = clips[row].clip;
                    if (clip != null) clip.SampleAnimation(model, clip.length * column / Columns);
                    // Same post-animation staff correction as in play (LateUpdate does not run in edit mode).
                    foreach (PropStabilizer prop in model.GetComponentsInChildren<PropStabilizer>(true))
                    {
                        prop.SetCasting(clips[row].label == "Action");
                        prop.Apply(10f);
                    }
                    camera.Render();
                    RenderTexture.active = camera.targetTexture;
                    tile.ReadPixels(new Rect(0, 0, TileWidth, TileHeight), 0, 0);
                    tile.Apply();
                    RenderTexture.active = null;
                    // Rows top to bottom in clip order.
                    sheet.SetPixels(column * TileWidth, (clips.Count - 1 - row) * TileHeight, TileWidth, TileHeight, tile.GetPixels());
                }
            }
            sheet.Apply();
            File.WriteAllBytes(Path.Combine(PreviewFolder, fileName), sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
            Object.DestroyImmediate(tile);
        }

        // One row: the hero mid-cast and the Light Staff's celestial strike 5 m ahead.
        private static void RenderStrike(Camera camera, GameObject model, VisualOverride hero, ArtStyleLibrary library)
        {
            if (library.celestialStrikeMaterial == null) { Debug.LogWarning("Preview: no celestial strike material."); return; }
            if (hero.actionClip != null) hero.actionClip.SampleAnimation(model, hero.actionClip.length * 0.35f);
            foreach (PropStabilizer prop in model.GetComponentsInChildren<PropStabilizer>(true)) { prop.SetCasting(true); prop.Apply(10f); }
            Transform holder = new GameObject("Preview Strikes").transform;
            float fov = camera.fieldOfView;
            camera.fieldOfView = 55f;
            camera.transform.position = Stage + new Vector3(11f, 4.5f, -5f);
            camera.transform.LookAt(Stage + new Vector3(0f, 4.5f, 3f));
            float[] ages = { 0.03f, 0.09f, 0.18f, 0.35f, 0.6f, 0.95f };
            Texture2D sheet = new Texture2D(TileWidth * ages.Length, TileHeight, TextureFormat.RGB24, false);
            Texture2D tile = new Texture2D(TileWidth, TileHeight, TextureFormat.RGB24, false);
            try
            {
                for (int i = 0; i < ages.Length; i++)
                {
                    CelestialStrikeVfx strike = CelestialStrikeVfx.Spawn(library, Stage + new Vector3(0f, 0f, 5f), holder);
                    strike.Tick(ages[i]);
                    foreach (ParticleSystem system in strike.GetComponentsInChildren<ParticleSystem>(true))
                        system.Simulate(ages[i], true, true);
                    camera.Render();
                    RenderTexture.active = camera.targetTexture;
                    tile.ReadPixels(new Rect(0, 0, TileWidth, TileHeight), 0, 0);
                    tile.Apply();
                    RenderTexture.active = null;
                    sheet.SetPixels(i * TileWidth, 0, TileWidth, TileHeight, tile.GetPixels());
                    Object.DestroyImmediate(strike.gameObject);
                }
                sheet.Apply();
                File.WriteAllBytes(Path.Combine(PreviewFolder, "HeroCast_Strike.png"), sheet.EncodeToPNG());
            }
            finally
            {
                camera.fieldOfView = fov;
                Object.DestroyImmediate(holder.gameObject);
                Object.DestroyImmediate(sheet);
                Object.DestroyImmediate(tile);
            }
        }

        // Stage 8 battlefield built exactly as in play (styled), with a few troops and an enemy wave,
        // rendered from behind the hero and from a high overview. Output: Battlefield_Hero/Overview.png.
        [MenuItem("Lightbringer/Art/Capture Battlefield Preview")]
        public static void CaptureBattlefield() => CaptureBattlefieldStage(8);

        [MenuItem("Lightbringer/Art/Capture Forest Ground Preview")]
        public static void CaptureForestGround() => CaptureBattlefieldStage(1);

        private static void CaptureBattlefieldStage(int stageNumber)
        {
            if (Application.isPlaying) { Debug.LogWarning("Stop Play mode before capturing the battlefield preview."); return; }
            AssetDatabase.Refresh();
            // Async shader compilation would render not-yet-compiled variants as missing in the first frames.
            bool asyncCompile = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            ArtStyleLibrary library = ArtStyleSetup.EnsureLibrary();
            Scene original = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            RenderTexture target = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            try
            {
                SceneManager.SetActiveScene(scene);
                GameObject host = new GameObject("Battlefield Preview");
                host.transform.position = Stage; // away from objects in the scene that is open in the Editor
                Lightbringer.Core.CampaignSession session = host.AddComponent<Lightbringer.Core.CampaignSession>();
                Lightbringer.Progression.CampaignProgress profile = new Lightbringer.Progression.CampaignProgress();
                for (int stage = 1; stage < Lightbringer.Progression.CampaignProgress.StageCount; stage++) profile.CompleteStage(stage);
                session.InitializeForValidation(profile);
                EditorAssets.ConfigureSession(session, material);
                session.ConfigureArt(library);
                session.SelectStage(stageNumber);
                if (!session.StartBattle()) { Debug.LogError("Preview: battle did not start."); return; }
                Lightbringer.Core.PrototypeBattle battle = session.Battle;
                Lightbringer.Resources.FoodResource food = battle.Hero.GetComponent<Lightbringer.Resources.FoodResource>();
                System.Reflection.MethodInfo generate = typeof(Lightbringer.Resources.FoodResource)
                    .GetMethod("GenerateFood", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                for (int i = 0; i < 9; i++)
                {
                    generate.Invoke(food, new object[] { 1000f });
                    battle.Summoner.TrySelectPath(i % battle.Paths.Length);
                    Physics.SyncTransforms();
                    if (battle.Summoner.TrySummonKind(i % 8))
                    {
                        Transform spawned = battle.Root.transform.Find("Allied Units").GetChild(battle.Root.transform.Find("Allied Units").childCount - 1);
                        Vector3 lane = battle.Paths[i % battle.Paths.Length].GetPosition(1);
                        spawned.position = new Vector3(lane.x + (i / 3) * 1.4f, spawned.position.y, -2f + (i / 3) * 2.5f);
                    }
                }
                Physics.SyncTransforms();
                battle.Waves.Tick(0.1f);
                foreach (Transform enemy in battle.Root.transform.Find("Enemy Units"))
                    enemy.position += Vector3.back * 12f;
                Camera camera = battle.Camera;
                camera.targetTexture = target;
                Vector3 hero = battle.Hero.transform.position;
                // The first Editor render happens before shadows and ambient are ready; render once and discard it.
                camera.Render();
                string prefix = stageNumber == 1 ? "ForestGround" : "Battlefield";
                RenderView(camera, Stage + new Vector3(-55f, 38f, -40f), Stage + new Vector3(0f, 0f, 10f), prefix + "_Overview.png");
                RenderView(camera, hero + new Vector3(0f, 3.6f, -7.5f), hero + new Vector3(0f, 1.2f, 6f), prefix + "_Hero.png");
                // Inspect the panorama at eye level as well as the normal downward gameplay angle.
                Vector3 eye = hero + Vector3.up * 2.5f;
                RenderView(camera, eye, eye + Vector3.forward * 80f, prefix + "_HorizonForward.png");
                Vector3 rearEye = eye + Vector3.right * 24f; // look past the allied fortress, not into its gate
                RenderView(camera, rearEye, rearEye + Vector3.back * 80f, prefix + "_HorizonRear.png");
                RenderView(camera, eye, eye + new Vector3(-76f, 0f, 25f), prefix + "_HorizonSeam.png");
                Vector3 field = hero + new Vector3(9f, -1.05f, 4f);
                RenderView(camera, field + new Vector3(0f, 1.2f, -3f), field + new Vector3(0f, 0.2f, 2f), prefix + "_GrassCloseup.png");
                WriteEnvironmentChecks(battle, library, stageNumber, prefix);
                camera.targetTexture = null;
                Debug.Log("Battlefield preview written to " + PreviewFolder);
            }
            finally
            {
                if (original.IsValid()) SceneManager.SetActiveScene(original);
                EditorSceneManager.CloseScene(scene, true);
                ShaderUtil.allowAsyncCompilation = asyncCompile;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(material);
            }
        }

        private static void WriteEnvironmentChecks(Lightbringer.Core.PrototypeBattle battle,
            ArtStyleLibrary library, int stage, string prefix)
        {
            BattlefieldEnvironment environment = battle.Root.GetComponentInChildren<BattlefieldEnvironment>();
            ForestEnvironmentDressing forest = environment.GetComponentInChildren<ForestEnvironmentDressing>();
            BattlefieldPropDressing props = environment.GetComponentInChildren<BattlefieldPropDressing>();
            bool decorative = forest != null && forest.GetComponentsInChildren<Collider>(true).Length == 0
                && props != null && props.GetComponentsInChildren<Collider>(true).Length == 0;
            bool routesClear = true;
            foreach (BattlefieldPropDressing.Placement placement in props.Placements)
                routesClear &= environment.LaneDistance(placement.Point) >= 4.5f + placement.Radius;
            foreach (Transform child in forest.transform)
                if (child.GetComponentInChildren<Renderer>(true) != null)
                    routesClear &= environment.LaneDistance(new Vector2(child.localPosition.x, child.localPosition.z)) >= 4f;
            bool flat = true;
            foreach (Lightbringer.Pathing.WaypointPath path in battle.Paths)
                for (int i = 0; i < path.Count; i++)
                {
                    Vector3 point = environment.transform.InverseTransformPoint(path.GetPosition(i));
                    flat &= Mathf.Abs(environment.SurfaceHeight(point.x, point.z)) < 0.01f;
                }
            Material ground = environment.Terrain.GetComponent<Renderer>().sharedMaterial;
            bool shader = ground != null && ground.shader.isSupported && !ShaderUtil.ShaderHasError(ground.shader);
            bool scoped = stage == 1 ? ground == library.forestEnvironment.terrain
                && library.forestEnvironment.openingGlade && !library.forestEnvironment.AppliesTo(2)
                : ground == library.FindBackdrop(stage).forest.terrain && ground.GetFloat("_Weathering") == 0f;
            bool passed = decorative && routesClear && flat && shader && scoped;
            File.WriteAllText(Path.Combine(PreviewFolder, prefix + "_Checks.txt"),
                (passed ? "PASS" : "FAIL") + " | Stage " + stage + " | " + System.DateTime.UtcNow.ToString("O")
                + "\nDecorations have no gameplay colliders: " + decorative
                + "\nRoads clear of dressing: " + routesClear + "\nPaths remain flat: " + flat
                + "\nTerrain shader compiled: " + shader + "\nStage material isolation: " + scoped
                + "\nTrees: " + forest.TreeCount + "; decorative instances (including grass): " + forest.PropCount
                + "; grass tufts: " + forest.GrassCount + "; authored props: " + props.Placements.Count + "\n");
            if (!passed) Debug.LogError("Battlefield visual checks failed: " + prefix);
        }

        private static void RenderView(Camera camera, Vector3 position, Vector3 lookAt, string fileName)
        {
            camera.transform.position = position;
            camera.transform.LookAt(lookAt);
            camera.fieldOfView = 55f;
            camera.Render();
            RenderTexture.active = camera.targetTexture;
            Texture2D image = new Texture2D(camera.targetTexture.width, camera.targetTexture.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, image.width, image.height), 0, 0);
            image.Apply();
            RenderTexture.active = null;
            Directory.CreateDirectory(PreviewFolder);
            File.WriteAllBytes(Path.Combine(PreviewFolder, fileName), image.EncodeToPNG());
            Object.DestroyImmediate(image);
        }
    }
}

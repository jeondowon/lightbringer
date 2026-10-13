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
                RenderView(camera, Stage + new Vector3(-70f, 62f, -55f), Stage + new Vector3(0f, 0f, Lightbringer.Core.BattlefieldLayout.CentreZ), prefix + "_Overview.png");
                RenderView(camera, Stage + new Vector3(0f, 4.5f, 16f), Stage + new Vector3(0f, 1.5f, 38f), prefix + "_Midfield.png");
                RenderView(camera, hero + new Vector3(0f, 3.6f, -7.5f), hero + new Vector3(0f, 1.2f, 6f), prefix + "_Hero.png");
                // Inspect the panorama at eye level as well as the normal downward gameplay angle.
                Vector3 eye = hero + Vector3.up * 2.5f;
                RenderView(camera, eye, eye + Vector3.forward * 80f, prefix + "_HorizonForward.png");
                Vector3 rearEye = eye + Vector3.right * 24f; // look past the allied fortress, not into its gate
                RenderView(camera, rearEye, rearEye + Vector3.back * 80f, prefix + "_HorizonRear.png");
                RenderView(camera, eye, eye + new Vector3(-76f, 0f, 25f), prefix + "_HorizonSeam.png");
                if (stageNumber == 1)
                {
                    // Hillside ruin sites seen from the field edge (BattlefieldPropDressing.ForestRuins).
                    var ground = battle.Root.GetComponentInChildren<BattlefieldEnvironment>();
                    Vector3 Site(float x, float z) => Stage + new Vector3(x, ground.SurfaceHeight(x, z) + 3f, z);
                    RenderView(camera, Stage + new Vector3(-24f, 5f, 8f), Site(-48f, 18f), prefix + "_RuinsWestTemple.png");
                    RenderView(camera, Stage + new Vector3(24f, 5f, 46f), Site(47f, 60f), prefix + "_RuinsEastShrine.png");
                    RenderView(camera, Stage + new Vector3(-18f, 6f, 72f), Site(-36f, 96f), prefix + "_RuinsNorth.png");
                }
                Vector3 field = hero + new Vector3(9f, -1.05f, 4f);
                RenderView(camera, field + new Vector3(0f, 1.2f, -3f), field + new Vector3(0f, 0.2f, 2f), prefix + "_GrassCloseup.png");
                WriteEnvironmentChecks(battle, library, stageNumber, prefix);
                CaptureSurfaceChecks(battle, camera, prefix);
                if (stageNumber == 1) CaptureBeacon(session, camera);
                if (camera != null) camera.targetTexture = null;
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

        private static void CaptureBeacon(Lightbringer.Core.CampaignSession session, Camera camera)
        {
            var battle = session.Battle;
            BeaconSanctuary beacon = battle.Beacon;
            var checks = new List<string>();
            bool passed = true;
            void Check(bool condition, string label)
            {
                checks.Add((condition ? "PASS" : "FAIL") + " | " + label);
                passed &= condition;
            }
            Check(beacon != null && !beacon.IsLit && !beacon.IsPresenting, "Beacon starts dormant");
            if (beacon == null) throw new System.InvalidOperationException("Stage one beacon missing");
            var props = battle.Root.GetComponentInChildren<BattlefieldPropDressing>();
            int count = 0;
            foreach (var placement in props.Placements)
                if (placement.Kind == BattlefieldPropKind.AlliedBeacon) count++;
            Check(count == 1, "Exactly one main beacon replaces the camp pair");
            var environment = battle.Root.GetComponentInChildren<BattlefieldEnvironment>();
            Check(environment.LaneDistance(BeaconSanctuary.Centre) - 4.85f > 4.5f,
                "Entire sanctuary paving clears the combat corridor");
            bool clearing = true;
            var forest = battle.Root.GetComponentInChildren<ForestEnvironmentDressing>();
            foreach (Transform child in forest.transform)
            {
                Vector2 point = new Vector2(child.localPosition.x, child.localPosition.z);
                if (child.name == "Forest Grass Patch")
                {
                    foreach (Transform tuft in child)
                    {
                        Vector3 p = environment.transform.InverseTransformPoint(tuft.position);
                        clearing &= Vector2.Distance(new Vector2(p.x, p.z), BeaconSanctuary.Centre) >= 7f;
                    }
                }
                else if (child.GetComponentInChildren<Renderer>() != null)
                    clearing &= Vector2.Distance(point, BeaconSanctuary.Centre) >= 7f;
            }
            Check(clearing, "Sanctuary is free of forest dressing and grass");
            Vector3 position = beacon.transform.position;
            RenderView(camera, position + new Vector3(12f, 7f, -17f), position + Vector3.up * 4f, "Beacon_Dormant.png");
            var view = new Lightbringer.UI.ResultView(new UnityEngine.UIElements.VisualElement());
            // Edit-mode previews do not receive the normal play-mode OnEnable lifecycle.
            battle.Objective.SendMessage("OnEnable");
            battle.Objective.EnemyBase.TakeDamage(100000f, battle.Hero);
            Check(battle.Objective.HasWon && beacon.IsLit && beacon.IsPresenting, "Victory starts beacon presentation");
            view.Refresh(session);
            Check(!view.Visible && !session.IsChoosing, "Results and level-up choices wait for ignition");
            int reward = session.Progress.gold;
            beacon.AdvancePresentation(3.2f);
            RenderView(camera, position + new Vector3(12f, 7f, -17f), position + Vector3.up * 4f, "Beacon_Ignition.png");
            beacon.AdvancePresentation(2f);
            Check(!beacon.IsPresenting && beacon.IsLit, "Ignition finishes and beacon stays lit");
            Check(session.Progress.gold == reward, "Presentation grants no duplicate rewards");
            session.Progress.pendingLevels = 0;
            view.Refresh(session);
            Check(view.Visible, "Result screen becomes available after ignition");
            bool returned = session.ReturnToPreparation();
            session.SelectStage(1);
            Check(returned && session.StartBattle(), "Completed stage can be replayed");
            Check(!session.Battle.Beacon.IsLit, "Replay starts with a dormant beacon");
            session.Battle.Objective.SendMessage("OnEnable");
            session.Battle.Hero.TakeDamage(100000f);
            Check(session.Battle.Objective.HasLost && !session.Battle.Beacon.IsLit, "Defeat does not ignite beacon");
            File.WriteAllText(Path.Combine(PreviewFolder, "Beacon_Checks.txt"),
                (passed ? "PASS" : "FAIL") + " | " + System.DateTime.UtcNow.ToString("O") + "\n" + string.Join("\n", checks));
            if (!passed) Debug.LogError("Beacon sanctuary checks failed");
        }

        private static void CaptureSurfaceChecks(Lightbringer.Core.PrototypeBattle battle, Camera camera, string prefix)
        {
            var terrain = battle.Root.GetComponentInChildren<BattlefieldEnvironment>();
            var forest = battle.Root.GetComponentInChildren<ForestEnvironmentDressing>();
            ForestGroundContact[] contacts = forest.GetComponentsInChildren<ForestGroundContact>();
            float worstGap = float.NegativeInfinity;
            int samples = 0;
            Physics.SyncTransforms();
            bool trianglesMatch = true;
            ForestGroundContact slopeTree = null;
            foreach (ForestGroundContact contact in contacts)
            {
                Vector3 centre = terrain.transform.InverseTransformPoint(contact.transform.position);
                if (contact.name == "ForestTree" && centre.x < 0f && centre.z > 0f && centre.z < 40f) slopeTree = contact;
                foreach (Vector3 support in contact.supportPoints)
                {
                    Vector3 world = contact.transform.TransformPoint(support);
                    Vector3 p = terrain.transform.InverseTransformPoint(world);
                    float height = terrain.SurfaceHeight(p.x, p.z);
                    worstGap = Mathf.Max(worstGap, p.y - height);
                    Ray ray = new Ray(world + terrain.transform.up * 100f, -terrain.transform.up);
                    trianglesMatch &= terrain.Terrain.Raycast(ray, out RaycastHit hit, 200f)
                        && Mathf.Abs(terrain.transform.InverseTransformPoint(hit.point).y - height) < 0.015f;
                    samples++;
                }
            }
            bool grounded = contacts.Length >= forest.TreeCount && samples > 0 && worstGap <= 0.005f && trianglesMatch;
            Material ground = terrain.Terrain.GetComponent<Renderer>().sharedMaterial;
            bool surfaces = ground.GetFloat("_StoneTextureStrength") > 0.9f
                && ground.GetTexture("_StoneTex") != null && ground.GetTexture("_StoneNormal") != null
                && !ShaderUtil.ShaderHasError(ground.shader);
            File.WriteAllText(Path.Combine(PreviewFolder, prefix + "_SurfaceChecks.txt"),
                (grounded && surfaces ? "PASS" : "FAIL") + " | " + System.DateTime.UtcNow.ToString("O")
                + $"\nGrounded trees/saplings: {contacts.Length}; root samples: {samples}; maximum root gap: {worstGap:0.0000}m"
                + $"\nRoot samples match actual terrain collider: {trianglesMatch}\nStone colour and normal maps compiled: {surfaces}\n");
            if (!grounded || !surfaces) Debug.LogError("Forest surface validation failed: " + prefix);
            Vector3 allied = battle.Root.transform.TransformPoint(new Vector3(0f, 0f, -18f));
            RenderView(camera, allied + new Vector3(5f, 2.6f, 5f), allied, prefix + "_StoneCloseup.png");
            if (slopeTree != null)
            {
                Vector3 root = slopeTree.transform.position;
                RenderView(camera, root + new Vector3(5f, 2.5f, -7f), root + Vector3.up * 0.8f, prefix + "_RootContact.png");
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

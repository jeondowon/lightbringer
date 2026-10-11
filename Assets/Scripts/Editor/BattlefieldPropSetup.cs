using System;
using System.IO;
using System.Linq;
using System.Text;
using Lightbringer.Core;
using Lightbringer.Progression;
using Lightbringer.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Lightbringer.EditorTools
{
    [InitializeOnLoad]
    public static class BattlefieldPropSetup
    {
        public const string Folder = "Assets/Art/Environment/Props";
        public const string ProfilePath = Folder + "/BattlefieldProps.asset";
        private const string Output = "Docs/ArtPass/Preview";
        private static readonly float[] Dimensions = { 4.8f, 3.3f, 1.2f, 1.1f, 3.6f, 4.6f, 2.8f, 4.2f, 4.8f };
        private static readonly int[] TriangleBudgets = { 20000, 20000, 10000, 14000, 28000, 24000, 22000, 20000, 36000 };

        static BattlefieldPropSetup()
        {
            EditorRequests.Register(Output + "/battlefield-props-setup.request", Build);
            EditorRequests.Register(Output + "/battlefield-props-preview.request", ValidateAndCapture);
        }

        [MenuItem("Lightbringer/Art/Set Up Battlefield Props")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play before importing props.");
            AssetDatabase.Refresh();
            var profile = AssetDatabase.LoadAssetAtPath<BattlefieldPropProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<BattlefieldPropProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }
            var report = new StringBuilder("Imported battlefield props | " + DateTime.UtcNow.ToString("O") + "\n");
            profile.assets = Enum.GetValues(typeof(BattlefieldPropKind)).Cast<BattlefieldPropKind>()
                .Select(kind => Import(kind, report)).ToArray();
            ArtStyleLibrary library = ArtStyleSetup.EnsureLibrary();
            library.battlefieldProps = profile;
            EditorUtility.SetDirty(profile);
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Docs/Validation");
            File.WriteAllText("Docs/Validation/BattlefieldPropImportChecks.txt", report.ToString());
            ValidateAndCapture();
        }

        private static BattlefieldPropAsset Import(BattlefieldPropKind kind, StringBuilder report)
        {
            string name = kind.ToString(), folder = Folder + "/" + name;
            string modelPath = folder + "/" + name + ".fbx";
            AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
            importer.importCameras = false;
            importer.importLights = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.isReadable = false;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.SaveAndReimport();
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            Texture2D colour = UnitModelSetup.ImportTexture(folder + "/basecolor.jpg", false);
            Texture2D normal = UnitModelSetup.ImportTexture(folder + "/normal.jpg", true);
            string packedPath = folder + "/MetallicSmoothness.png";
            UnitModelSetup.PackMetallicSmoothness(folder + "/metallic.jpg", folder + "/roughness.jpg", packedPath);
            string materialPath = folder + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.SetTexture("_BaseMap", colour);
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BumpMap", normal);
            material.SetFloat("_BumpScale", 0.65f);
            material.EnableKeyword("_NORMALMAP");
            material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(packedPath));
            material.SetFloat("_Metallic", 1f);
            material.SetFloat("_Smoothness", 0.65f);
            material.SetFloat("_SmoothnessTextureChannel", 0f);
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);

            var root = new GameObject(name);
            try
            {
                GameObject visual = Object.Instantiate(model, root.transform);
                visual.name = "Model";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                visual.transform.localScale = Vector3.one;
                foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
                Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) throw new InvalidOperationException("Empty prop: " + name);
                foreach (Renderer renderer in renderers)
                {
                    renderer.sharedMaterials = Enumerable.Repeat(material, Mathf.Max(1, renderer.sharedMaterials.Length)).ToArray();
                    renderer.lightProbeUsage = LightProbeUsage.Off;
                    renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                }
                long sourceTriangles = 0;
                int meshIndex = 0;
                foreach (MeshFilter filter in visual.GetComponentsInChildren<MeshFilter>())
                {
                    Mesh source = filter.sharedMesh;
                    sourceTriangles += (long)source.GetIndexCount(0) / 3;
                    Mesh optimized = CrowdMeshOptimizer.Simplify(source, filter.GetComponent<MeshRenderer>().sharedMaterials,
                        TriangleBudgets[(int)kind], out Material[] merged);
                    optimized.name = name + "_" + meshIndex + "_Optimized";
                    string meshPath = folder + "/" + optimized.name + ".asset";
                    Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                    if (existing != null)
                    {
                        EditorUtility.CopySerialized(optimized, existing);
                        Object.DestroyImmediate(optimized);
                        optimized = existing;
                        EditorUtility.SetDirty(existing);
                    }
                    else AssetDatabase.CreateAsset(optimized, meshPath);
                    filter.sharedMesh = optimized;
                    filter.GetComponent<MeshRenderer>().sharedMaterials = merged;
                    meshIndex++;
                }
                Bounds bounds = BoundsOf(root);
                float scale = Dimensions[(int)kind] / (kind == BattlefieldPropKind.EnemyBoneFence ? bounds.size.x : bounds.size.y);
                visual.transform.localScale = Vector3.one * scale;
                visual.transform.localPosition = -new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) * scale;
                Bounds normalized = BoundsOf(root);
                if (Mathf.Abs(normalized.min.y) > 0.01f) throw new InvalidOperationException("Ungrounded prop: " + name);
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, folder + "/" + name + ".prefab");
                long triangles = root.GetComponentsInChildren<MeshFilter>().Sum(filter => (long)filter.sharedMesh.GetIndexCount(0) / 3);
                report.AppendLine($"{name}: size={normalized.size}, sourceTriangles={sourceTriangles}, optimizedTriangles={triangles}, renderers={renderers.Length}, colliders=0");
                return new BattlefieldPropAsset { kind = kind, prefab = prefab, size = normalized.size };
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static Bounds BoundsOf(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        [MenuItem("Lightbringer/Art/Validate and Capture Battlefield Props")]
        public static void ValidateAndCapture()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play before previewing props.");
            ArtStyleLibrary library = ArtStyleSetup.EnsureLibrary();
            if (library.battlefieldProps == null || library.battlefieldProps.assets.Length != 9)
                throw new InvalidOperationException("Set up all nine props first.");
            Scene original = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            bool asyncCompile = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            Material baseMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var report = new StringBuilder("Battlefield prop placement | " + DateTime.UtcNow.ToString("O") + "\n");
            try
            {
                SceneManager.SetActiveScene(scene);
                Directory.CreateDirectory(Output);
                CaptureModels(library.battlefieldProps, scene);
                foreach (int stage in new[] { 1, 4, 8 })
                {
                    var host = new GameObject("Prop validation " + stage);
                    try
                    {
                        var session = host.AddComponent<CampaignSession>();
                        var progress = new CampaignProgress();
                        for (int i = 1; i < stage; i++) progress.CompleteStage(i);
                        session.InitializeForValidation(progress);
                        EditorAssets.ConfigureSession(session, baseMaterial);
                        session.ConfigureArt(library);
                        if (!session.StartBattle()) throw new InvalidOperationException("Could not build stage " + stage);
                        PrototypeBattle battle = session.Battle;
                        var terrain = battle.Root.GetComponentInChildren<BattlefieldEnvironment>();
                        var dressing = terrain.GetComponentInChildren<BattlefieldPropDressing>();
                        if (dressing == null || dressing.GetComponentsInChildren<Collider>().Length != 0)
                            throw new InvalidOperationException("Missing dressing or unexpected decoration collider.");
                        foreach (BattlefieldPropKind kind in Enum.GetValues(typeof(BattlefieldPropKind)))
                            if (!dressing.Placements.Any(p => p.Kind == kind))
                                throw new InvalidOperationException($"Stage {stage} is missing {kind}; adjust its footprint/placement.");
                        foreach (var placement in dressing.Placements)
                        {
                            if (terrain.LaneDistance(placement.Point) < 4.5f + placement.Radius - 0.01f)
                                throw new InvalidOperationException("Prop blocks a path: " + placement.Kind);
                            Bounds bounds = BoundsOf(placement.Instance);
                            if (Mathf.Abs(bounds.min.y - terrain.SurfaceHeight(placement.Point.x, placement.Point.y)) > 0.06f)
                                throw new InvalidOperationException("Floating prop: " + placement.Kind);
                        }
                        for (int a = 0; a < dressing.Placements.Count; a++)
                            for (int b = a + 1; b < dressing.Placements.Count; b++)
                            {
                                var first = dressing.Placements[a]; var second = dressing.Placements[b];
                                if (Vector2.Distance(first.Point, second.Point) < first.Radius + second.Radius)
                                    throw new InvalidOperationException("Overlapping prop footprints.");
                            }
                        long triangles = dressing.GetComponentsInChildren<MeshFilter>().Sum(f => (long)f.sharedMesh.GetIndexCount(0) / 3);
                        report.AppendLine($"PASS stage {stage}: {dressing.Placements.Count} placements, all 9 kinds, 4.5 m lane margins, grounded, no overlaps/colliders; {triangles} triangles.");
                        foreach (var placement in dressing.Placements)
                            report.AppendLine($"  {placement.Kind}: {placement.Point}, radius={placement.Radius:0.00}");
                        if (stage == 1)
                        {
                            // Static cameras: do not advance gameplay or modify the player's campaign save.
                            Camera camera = battle.Camera;
                            camera.scene = scene;
                            Capture(camera, new Vector3(49f, 57f, -67f), new Vector3(0f, 0f, 9f), "Props_Overview");
                            Capture(camera, new Vector3(0f, 17f, 7f), new Vector3(0f, 2f, -23f), "Props_Allied");
                            Capture(camera, new Vector3(0f, 20f, 5f), new Vector3(0f, 2f, 38f), "Props_Enemy");
                            Capture(camera, new Vector3(-4f, 7f, -10f), new Vector3(-14f, 2f, 3f), "Props_Ruins");
                            Capture(camera, new Vector3(0f, 6f, -23f), new Vector3(0f, 2f, 12f), "Props_Hero");
                        }
                    }
                    finally { Object.DestroyImmediate(host); }
                }
                report.AppendLine("PASS: original routes, terrain and objective colliders preserved; no runtime mesh/material copies for props.");
            }
            catch (Exception error) { report.AppendLine("FAIL: " + error); throw; }
            finally
            {
                Object.DestroyImmediate(baseMaterial);
                if (original.IsValid()) SceneManager.SetActiveScene(original);
                EditorSceneManager.CloseScene(scene, true);
                ShaderUtil.allowAsyncCompilation = asyncCompile;
                Directory.CreateDirectory("Docs/Validation");
                File.WriteAllText("Docs/Validation/BattlefieldPropPlacementChecks.txt", report.ToString());
            }
        }

        private static void CaptureModels(BattlefieldPropProfile profile, Scene scene)
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.58f, 0.65f);
            var light = new GameObject("Prop studio sun").AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.5f;
            light.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
            var camera = new GameObject("Prop studio camera").AddComponent<Camera>();
            camera.scene = scene;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.22f, 0.24f, 0.28f);
            try
            {
                foreach (BattlefieldPropAsset asset in profile.assets)
                {
                    GameObject instance = Object.Instantiate(asset.prefab);
                    try
                    {
                        Bounds bounds = BoundsOf(instance);
                        float distance = bounds.size.magnitude * 1.6f;
                        Capture(camera, bounds.center + new Vector3(0.65f, 0.4f, -1f).normalized * distance,
                            bounds.center, "Prop_" + asset.kind);
                    }
                    finally { Object.DestroyImmediate(instance); }
                }
            }
            finally { Object.DestroyImmediate(camera.gameObject); Object.DestroyImmediate(light.gameObject); }
        }

        private static void Capture(Camera camera, Vector3 eye, Vector3 target, string name)
        {
            var texture = new RenderTexture(1440, 1080, 24);
            RenderTexture previous = RenderTexture.active;
            RenderTexture oldTarget = camera.targetTexture;
            try
            {
                camera.transform.position = eye;
                camera.transform.LookAt(target);
                camera.fieldOfView = 45f;
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

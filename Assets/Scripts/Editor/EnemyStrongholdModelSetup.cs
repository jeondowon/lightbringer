using System;
using System.IO;
using System.Linq;
using Lightbringer.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Lightbringer.EditorTools
{
    [InitializeOnLoad]
    internal static class EnemyStrongholdModelSetup
    {
        private const string PreviewFolder = "Docs/ArtPass/Preview";

        static EnemyStrongholdModelSetup()
        {
            EditorRequests.Register("Docs/ArtPass/Preview/enemy-stronghold.request", Build);
            EditorRequests.Register("Docs/ArtPass/Preview/allied-stronghold.request", BuildAllied);
        }

        [MenuItem("Lightbringer/Art/Set Up Enemy Stronghold Model")]
        public static void Build() => BuildModel(VisualId.EnemyStronghold, "EnemyStronghold",
            "medieval_fortress_gate_3d_model_", 180f, "medieval+fortress+gate+3d+model.zip");

        [MenuItem("Lightbringer/Art/Set Up Allied Stronghold Model")]
        public static void BuildAllied() => BuildModel(VisualId.AlliedStronghold, "AlliedStronghold",
            "fantasy_castle_3d_model_", 0f, "fantasy+castle+3d+model.zip");

        private static void BuildModel(VisualId id, string assetName, string texturePrefix, float yaw, string sourceArchive)
        {
            string folder = "Assets/Art/Buildings/" + assetName;
            string modelPath = folder + "/" + assetName + ".fbx";
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before setting up the stronghold.");
            AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
            importer.isReadable = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.SaveAndReimport();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null) throw new InvalidOperationException("Stronghold FBX import failed.");
            string Map(string kind) => folder + "/" + texturePrefix + kind;
            var color = UnitModelSetup.ImportTexture(Map("basecolor.JPEG"), false);
            var normal = UnitModelSetup.ImportTexture(Map("normal.PNG"), true);
            string packedPath = folder + "/" + assetName + "_MetallicSmoothness.png";
            UnitModelSetup.PackMetallicSmoothness(Map("metallic.JPEG"), Map("roughness.JPEG"), packedPath);
            string materialPath = folder + "/" + assetName + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.SetTexture("_BaseMap", color);
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BumpMap", normal);
            material.SetFloat("_BumpScale", 0.7f);
            material.EnableKeyword("_NORMALMAP");
            material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(packedPath));
            material.SetFloat("_Metallic", 1f);
            material.SetFloat("_Smoothness", 0.7f);
            material.SetFloat("_SmoothnessTextureChannel", 0f);
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            EditorUtility.SetDirty(material);

            var root = new GameObject(assetName);
            try
            {
                var instance = Object.Instantiate(model, root.transform);
                instance.name = "Gate Model";
                // Correct Tripo's Z-up axis; each faction's facing is set by the visual override.
                instance.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                instance.transform.localScale = Vector3.one;
                var renderers = instance.GetComponentsInChildren<Renderer>(true);
                foreach (var renderer in renderers)
                    renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
                Bounds bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                float scale = 7.5f / bounds.size.x;
                instance.transform.localScale = Vector3.one * scale;
                instance.transform.localPosition = -new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) * scale;
                string prefabPath = folder + "/" + assetName + ".prefab";
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                var library = ArtStyleSetup.EnsureLibrary();
                var entries = (library.overrides ?? Array.Empty<VisualOverride>())
                    .Where(entry => entry != null && entry.id != id).ToList();
                entries.Add(new VisualOverride { id = id, prefab = prefab, scale = id == VisualId.AlliedStronghold ? 2.2f : 2f,
                    localEuler = new Vector3(0f, yaw, 0f) });
                library.overrides = entries.ToArray();
                EditorUtility.SetDirty(library);
                AssetDatabase.SaveAssets();
                Directory.CreateDirectory(PreviewFolder);
                Capture(library, id, assetName);
                Directory.CreateDirectory("Docs/Validation");
                File.WriteAllText("Docs/Validation/" + assetName + "ImportChecks.txt",
                    $"PASS | {DateTime.UtcNow:O}\nPrefab: {prefabPath}\nSize: {bounds.size * scale}\n"
                    + $"Source: user-supplied Tripo export, {sourceArchive}.\n"
                    + "URP base color, normal and packed metallic/smoothness assigned. Original objective collider retained.\n"
                    + $"Validated runtime UnitAppearance under the campaign's 6x4x4 objective transform: width {(id == VisualId.AlliedStronghold ? 16.5f : 15f)} m, feet y=0, no troop motion.\n"
                    + "Rebuild: Lightbringer > Art > Set Up Allied/Enemy Stronghold Model. Restart Play to apply.\n");
                Debug.Log(assetName + " model assigned. See its Front/Back.png previews.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void Capture(ArtStyleLibrary library, VisualId id, string assetName)
        {
            Scene original = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            bool asyncCompile = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            var target = new RenderTexture(1280, 960, 24);
            RenderTexture previous = RenderTexture.active;
            try
            {
                SceneManager.SetActiveScene(scene);
                Vector3 origin = new Vector3(6000f, 0f, 0f);
                // Match the campaign objective's scaled collider and feet offset, not just the prefab.
                var model = new GameObject(assetName + " Preview");
                SceneManager.MoveGameObjectToScene(model, scene);
                model.transform.position = origin + Vector3.up * 2f;
                model.transform.localScale = new Vector3(6f, 4f, 4f);
                model.AddComponent<BoxCollider>();
                var visual = UnitAppearance.Attach(model, id, library, -2f);
                var renderers = visual.GetComponentsInChildren<Renderer>();
                Bounds bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                float expectedWidth = id == VisualId.AlliedStronghold ? 16.5f : 15f;
                if (Mathf.Abs(bounds.min.y) > 0.01f || Mathf.Abs(bounds.size.x - expectedWidth) > 0.01f
                    || visual.GetComponent<VisualMotion>() != null)
                    throw new InvalidOperationException("Stronghold placement/size/static-visual validation failed: " + bounds);
                var sun = new GameObject("Preview Light").AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.intensity = 1.5f;
                var camera = new GameObject("Preview Camera").AddComponent<Camera>();
                camera.targetTexture = target;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.22f, 0.24f, 0.28f);
                camera.fieldOfView = 40f;
                camera.useOcclusionCulling = false;
                camera.scene = scene;
                Physics.SyncTransforms();
                foreach (int side in new[] { -1, 1 })
                {
                    float framingScale = expectedWidth / 7.5f;
                    camera.transform.position = origin + new Vector3(8f, 6f, side * 13f) * framingScale;
                    camera.transform.LookAt(origin + Vector3.up * (2.4f * framingScale));
                    sun.transform.rotation = Quaternion.Euler(40f, side == -1 ? 25f : 155f, 0f);
                    camera.Render();
                    RenderTexture.active = target;
                    var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                    try
                    {
                        image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                        image.Apply();
                        File.WriteAllBytes(PreviewFolder + "/" + assetName + "_" + (side == (id == VisualId.AlliedStronghold ? 1 : -1) ? "Front" : "Back") + ".png", image.EncodeToPNG());
                    }
                    finally { Object.DestroyImmediate(image); }
                }
            }
            finally
            {
                RenderTexture.active = previous;
                if (original.IsValid()) SceneManager.SetActiveScene(original);
                EditorSceneManager.CloseScene(scene, true);
                target.Release();
                Object.DestroyImmediate(target);
                ShaderUtil.allowAsyncCompilation = asyncCompile;
            }
        }
    }
}

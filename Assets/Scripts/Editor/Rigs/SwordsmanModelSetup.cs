using System;
using System.IO;
using System.Linq;
using Lightbringer.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lightbringer.EditorTools
{
    [InitializeOnLoad]
    public static class SwordsmanModelSetup
    {
        private const string Folder = "Assets/Art/Characters/Swordsman";
        private const string ModelPath = Folder + "/Swordsman.fbx";
        private const string MaterialPath = Folder + "/Swordsman.mat";
        private const string PackedPath = Folder + "/Swordsman_MetallicSmoothness.png";
        private const float Height = 1.65f;

        static SwordsmanModelSetup() => EditorRequests.Register("Docs/Validation/SwordsmanAlignment.request", Assign);

        [MenuItem("Lightbringer/Art/Assign Swordsman Model")]
        public static void Assign()
        {
            AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceSynchronousImport);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null) throw new InvalidOperationException("Swordsman.fbx was not imported.");
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader is missing.");

            Texture2D color = ModelSurfaceImport.ImportTexture(TexturePath("basecolor"), false);
            Texture2D normal = ModelSurfaceImport.ImportTexture(TexturePath("normal"), true);
            ModelSurfaceImport.PackMetallicSmoothness(TexturePath("metallic"), TexturePath("roughness"), PackedPath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "Swordsman" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.shader = shader;
            ModelSurfaceImport.ConfigureLit(material, color, normal,
                AssetDatabase.LoadAssetAtPath<Texture2D>(PackedPath));

            var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            foreach (Material source in model.GetComponentsInChildren<Renderer>(true)
                .SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct())
            {
                if (source != material)
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(source), material);
            }
            importer.importCameras = false;
            importer.importLights = false;
            // This export has no motion clips. Avoid an idle Animator suppressing the existing bob/lean fallback.
            if (!AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>().Any(c => !c.name.StartsWith("__preview__")))
                importer.animationType = ModelImporterAnimationType.None;
            importer.SaveAndReimport();
            AssignUpright();
            AssetDatabase.SaveAssets();
            Validate();
        }

        [MenuItem("Lightbringer/Art/Assign Swordsman Model", true)]
        private static bool CanAssign() => !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling;

        private static void AssignUpright()
        {
            var library = AssetDatabase.LoadAssetAtPath<ArtStyleLibrary>(ArtStyleSetup.LibraryPath);
            if (library == null) throw new InvalidOperationException("Art style library is missing.");
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            Scene preview = EditorSceneManager.NewPreviewScene();
            var probe = UnityEngine.Object.Instantiate(model);
            SceneManager.MoveGameObjectToScene(probe, preview);
            try
            {
                // This Tripo export has its feet-to-head axis along +Z, not Unity's +Y.
                // Correct the source axis BEFORE measuring height and grounding the feet.
                Vector3 rotation = new Vector3(-90f, 0f, 0f);
                probe.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(rotation));
                probe.transform.localScale = Vector3.one;
                Renderer[] renderers = probe.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) throw new InvalidOperationException("Swordsman has no renderer.");
                Bounds bounds = renderers[0].bounds;
                foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
                if (bounds.size.y <= Mathf.Max(bounds.size.x, bounds.size.z))
                    throw new InvalidOperationException("Swordsman's corrected upright axis is not its tallest axis.");
                float scale = Height / bounds.size.y;
                Undo.RecordObject(library, "Align Swordsman model");
                VisualOverride entry = library.FindOverride(VisualId.Swordsman);
                if (entry == null)
                {
                    entry = new VisualOverride { id = VisualId.Swordsman };
                    library.overrides = (library.overrides ?? new VisualOverride[0]).Concat(new[] { entry }).ToArray();
                }
                entry.prefab = model;
                entry.localEuler = rotation;
                entry.scale = scale;
                entry.localOffset = -scale * new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                EditorUtility.SetDirty(library);
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        private static string TexturePath(string suffix) => Folder + "/Swordsman.fbm/medieval_knight_3d_model_" + suffix + ".JPEG";

        private static void Validate()
        {
            var library = AssetDatabase.LoadAssetAtPath<ArtStyleLibrary>(ArtStyleSetup.LibraryPath);
            var entry = library.FindOverride(VisualId.Swordsman);
            if (entry == null || AssetDatabase.GetAssetPath(entry.prefab) != ModelPath)
                throw new InvalidOperationException("Swordsman override is not connected.");
            Scene preview = EditorSceneManager.NewPreviewScene();
            var owner = new GameObject("Swordsman validation");
            SceneManager.MoveGameObjectToScene(owner, preview);
            try
            {
                Transform visual = UnitAppearance.Attach(owner, VisualId.Swordsman, library, -0.8f);
                Renderer[] renderers = visual.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) throw new InvalidOperationException("Swordsman has no renderer.");
                Bounds bounds = renderers[0].bounds;
                foreach (Renderer renderer in renderers)
                {
                    bounds.Encapsulate(renderer.bounds);
                    if (renderer.sharedMaterials.Any(m => m == null || AssetDatabase.GetAssetPath(m) != MaterialPath))
                        throw new InvalidOperationException("Swordsman renderer has an unmapped material.");
                }
                if (Mathf.Abs(bounds.size.y - Height) > 0.02f || Mathf.Abs(bounds.min.y + 0.8f) > 0.02f
                    || bounds.size.y <= Mathf.Max(bounds.size.x, bounds.size.z))
                    throw new InvalidOperationException("Swordsman height or ground alignment is incorrect.");
                int vertices = visual.GetComponentsInChildren<MeshFilter>().Sum(f => f.sharedMesh.vertexCount);
                string report = $"PASS | {DateTime.UtcNow:O}\nSwordsman assigned through UnitAppearance.Attach.\n"
                    + $"Model: {ModelPath}\nMaterial: URP Lit; base color, normal, metallic and inverted roughness.\n"
                    + $"Height: {bounds.size.y:F3} m; feet: {bounds.min.y:F3}; rotation: {entry.localEuler}; bounds: {bounds.size}.\n"
                    + $"Renderers: {renderers.Length}; static mesh vertices: {vertices}.\n"
                    + "No skeletal animation clips supplied; existing bob/lean fallback is used.\n"
                    + "Editor instantiation validated; gameplay visual review remains manual.\n";
                Directory.CreateDirectory("Docs/Validation");
                File.WriteAllText("Docs/Validation/SwordsmanImportChecks.txt", report);
                Debug.Log(report);
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

    }
}

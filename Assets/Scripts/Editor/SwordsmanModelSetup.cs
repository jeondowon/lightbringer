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
        private const string RequestPath = "Docs/Validation/SwordsmanAlignment.request";
        private const float Height = 1.65f;

        static SwordsmanModelSetup() => EditorApplication.update += RunRequested;

        private static void RunRequested()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating
                || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(RequestPath)) return;
            File.Delete(RequestPath);
            try { Assign(); }
            catch (Exception error) { Debug.LogException(error); }
        }

        [MenuItem("Lightbringer/Art/Assign Swordsman Model")]
        public static void Assign()
        {
            AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceSynchronousImport);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null) throw new InvalidOperationException("Swordsman.fbx was not imported.");
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader is missing.");

            Texture2D color = ImportTexture("basecolor", false);
            Texture2D normal = ImportTexture("normal", true);
            PackMetallicSmoothness();
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "Swordsman" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.shader = shader;
            material.SetTexture("_BaseMap", color);
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BumpMap", normal);
            material.SetFloat("_BumpScale", 1f);
            material.EnableKeyword("_NORMALMAP");
            material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(PackedPath));
            material.SetFloat("_Metallic", 1f);
            material.SetFloat("_Smoothness", 1f);
            material.SetFloat("_SmoothnessTextureChannel", 0f);
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            EditorUtility.SetDirty(material);

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

        private static Texture2D ImportTexture(string suffix, bool normal)
        {
            string path = TexturePath(suffix);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) throw new FileNotFoundException(path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normal;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void PackMetallicSmoothness()
        {
            var metallic = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            var roughness = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            Texture2D packed = null;
            try
            {
                if (!metallic.LoadImage(File.ReadAllBytes(TexturePath("metallic")))
                    || !roughness.LoadImage(File.ReadAllBytes(TexturePath("roughness"))))
                    throw new InvalidOperationException("Could not decode Swordsman surface textures.");
                if (metallic.width != roughness.width || metallic.height != roughness.height)
                    throw new InvalidOperationException("Metallic/roughness texture dimensions differ.");
                Color32[] pixels = metallic.GetPixels32();
                Color32[] rough = roughness.GetPixels32();
                for (int i = 0; i < pixels.Length; i++)
                    pixels[i] = new Color32(pixels[i].r, 0, 0, (byte)(255 - rough[i].r));
                packed = new Texture2D(metallic.width, metallic.height, TextureFormat.RGBA32, false, true);
                packed.SetPixels32(pixels);
                packed.Apply();
                File.WriteAllBytes(PackedPath, packed.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(metallic);
                UnityEngine.Object.DestroyImmediate(roughness);
                if (packed != null) UnityEngine.Object.DestroyImmediate(packed);
            }
            AssetDatabase.ImportAsset(PackedPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(PackedPath);
            importer.sRGBTexture = false;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
            importer.SaveAndReimport();
        }

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

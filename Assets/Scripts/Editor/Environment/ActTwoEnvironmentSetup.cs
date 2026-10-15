using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Lightbringer.Visuals;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Lightbringer.EditorTools
{
    [InitializeOnLoad]
    internal static class ActTwoEnvironmentSetup
    {
        private const string Root = "Assets/Art/Environment/ActTwo";
        private const string Altar = Root + "/Sources/AltarRuins/Prefabs/";
        private const string Output = "Docs/Diagnostics/ActTwoEnvironmentChecks.txt";
        private static readonly Dictionary<Material, Material> Materials = new Dictionary<Material, Material>();
        private static StringBuilder report;
        private static readonly string LoadedScriptStamp = GreyboxScriptAutoRefresh.GetStamp();

        static ActTwoEnvironmentSetup() => EditorRequests.Register("Docs/Diagnostics/act-two-environment.request", () =>
        {
            if (LoadedScriptStamp != GreyboxScriptAutoRefresh.GetStamp())
            {
                File.WriteAllText("Docs/Diagnostics/act-two-environment.request", "Build after script refresh");
                AssetDatabase.Refresh();
                return;
            }
            Run();
        });

        [MenuItem("Lightbringer/Art/Build Act Two Environment")]
        public static void Run()
        {
            report = new StringBuilder("Act Two Environment | " + DateTime.UtcNow.ToString("O") + "\n");
            Materials.Clear();
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                Folder(Root + "/Prefabs");
                Folder(Root + "/Materials");
                // Only the newly selected ruin textures are configured; Dry Trees is already URP-ready.
                foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root + "/Sources" }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null) continue;
                    importer.maxTextureSize = 2048;
                    importer.textureCompression = TextureImporterCompression.Compressed;
                    if (path.IndexOf("Normal", StringComparison.OrdinalIgnoreCase) >= 0)
                        importer.textureType = TextureImporterType.NormalMap;
                    else if (path.IndexOf("Occlusion", StringComparison.OrdinalIgnoreCase) >= 0 || path.Contains("_AO."))
                        importer.sRGBTexture = false;
                    importer.SaveAndReimport();
                }
                string profilePath = Root + "/ActTwoEnvironment.asset";
                var profile = AssetDatabase.LoadAssetAtPath<ActTwoEnvironmentProfile>(profilePath);
                if (profile == null)
                {
                    profile = ScriptableObject.CreateInstance<ActTwoEnvironmentProfile>();
                    AssetDatabase.CreateAsset(profile, profilePath);
                }
                string[] treeSources = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Dry_Trees/Prefab" })
                    .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p).ToArray();
                // Check before assigning: a failed rebuild must never wipe the existing tree wrappers.
                if (treeSources.Length != 7) throw new InvalidOperationException("Expected seven converted Dry Trees prefabs.");
                profile.trees = treeSources.Select(p => Single(p, Path.GetFileNameWithoutExtension(p), true)).ToArray();
                profile.watchtower = Single(Root + "/Sources/RuinedTower/Tower_B.prefab", "RuinedWatchtower", false);
                profile.brokenWall = Composite("BrokenBoundaryWall", root =>
                {
                    Part(root, "Wall_broken_1", new Vector3(-1.5f, 0, 0), 2.5f, 0);
                    Part(root, "Broken_column", new Vector3(2, 0, .3f), 1.8f, 20);
                    Part(root, "Rubble_3", new Vector3(0, 0, -1), .65f, 30);
                });
                profile.sanctuary = Composite("RooflessSanctuary", root =>
                {
                    Part(root, "Arch_1", new Vector3(0, 0, 1.7f), 5.8f, 0);
                    Part(root, "Column", new Vector3(-2.7f, 0, -1.5f), 3.8f, 0);
                    Part(root, "Broken_column", new Vector3(2.7f, 0, -1.5f), 2.1f, 25);
                    Part(root, "Altar_2", new Vector3(0, 0, .2f), 1.3f, 0);
                    Part(root, "Rubble_3", new Vector3(1.8f, 0, -3), .65f, -35);
                });
                profile.monument = Composite("BrokenPilgrimMonument", root =>
                {
                    Part(root, "Altar_2", Vector3.zero, 1.2f, 0);
                    Part(root, "Broken_column", new Vector3(0, 1.05f, 0), 2.5f, 0);
                    Part(root, "Rubble_3", new Vector3(1.8f, 0, -.5f), .7f, 40);
                });
                var forestGround = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Environment/Forest/ForestTerrain.mat");
                if (forestGround != null)
                {
                    string groundPath = Root + "/Materials/AshlandGround.mat";
                    Material ground = AssetDatabase.LoadAssetAtPath<Material>(groundPath);
                    if (ground == null) { ground = new Material(forestGround); AssetDatabase.CreateAsset(ground, groundPath); }
                    else EditorUtility.CopySerialized(forestGround, ground);
                    ground.name = "AshlandGround";
                    ground.SetTexture("_ForestGrassTex", forestGround.GetTexture("_ForestSoilTex"));
                    ground.SetTexture("_ForestGrassNormal", forestGround.GetTexture("_ForestSoilNormal"));
                    ground.SetColor("_ForestGroundTint", new Color(.83f, .81f, .76f));
                    ground.SetFloat("_ForestTextureStrength", .55f);
                    ground.SetFloat("_NaturalLight", .65f);
                    ground.SetFloat("_ForestNormalStrength", .65f);
                    ground.SetFloat("_FlowerAmount", 0);
                    ground.SetColor("_CorruptGlow", Color.black);
                    EditorUtility.SetDirty(ground);
                    AssetDatabase.SaveAssetIfDirty(ground);
                    profile.terrain = ground;
                    report.AppendLine("PASS: Existing forest soil textures reused for the ashland ground; no new ground asset required.");
                }
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssetIfDirty(profile);
                var library = AssetDatabase.LoadAssetAtPath<ArtStyleLibrary>(ArtStyleSetup.LibraryPath);
                if (library == null) throw new InvalidOperationException("Missing ArtStyleLibrary.");
                library.actTwoEnvironment = profile;
                EditorUtility.SetDirty(library);
                AssetDatabase.SaveAssetIfDirty(library);
                report.AppendLine("PASS: Profile connected to the art library. Seven existing URP trees; four textured ruin assemblies.");
                report.AppendLine("PASS: Original Dry Trees materials retained. No new sounds, VFX, character models or wagon assets.");
                Debug.Log("Act Two environment composition complete. " + Output);
            }
            catch (Exception error) { report.AppendLine("FAIL: " + error); throw; }
            finally { Directory.CreateDirectory(Path.GetDirectoryName(Output)); File.WriteAllText(Output, report.ToString()); }
        }

        private static GameObject Single(string path, string name, bool tree)
        {
            var root = new GameObject(name);
            try
            {
                AddModel(root, path, !tree);
                Normalize(root, tree);
                return Save(root, tree);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static GameObject Composite(string name, Action<GameObject> assemble)
        {
            var root = new GameObject(name);
            try { assemble(root); Normalize(root, false); return Save(root, false); }
            finally { Object.DestroyImmediate(root); }
        }

        private static void Part(GameObject root, string name, Vector3 position, float height, float yaw)
        {
            var holder = new GameObject(name);
            holder.transform.SetParent(root.transform, false);
            AddModel(holder, Altar + name + ".prefab", true);
            Normalize(holder, true);
            holder.transform.localPosition = position;
            holder.transform.localScale = Vector3.one * height;
            holder.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        }

        private static void AddModel(GameObject root, string path, bool convert)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (source == null) throw new FileNotFoundException(path);
            GameObject model = Object.Instantiate(source, root.transform, false);
            model.name = source.name;
            foreach (Collider collider in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
            foreach (Rigidbody body in model.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(body);
            foreach (Transform node in model.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(node.gameObject);
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                if (convert) renderer.sharedMaterials = renderer.sharedMaterials.Select(Convert).ToArray();
                foreach (Material material in renderer.sharedMaterials)
                    if (material == null || material.shader == null || !material.shader.name.StartsWith("Universal Render Pipeline/"))
                        throw new InvalidOperationException("Non-URP material in " + path);
            }
        }

        private static Material Convert(Material source)
        {
            if (source == null) throw new InvalidOperationException("Source ruin material is missing.");
            if (Materials.TryGetValue(source, out Material result)) return result;
            string path = Root + "/Materials/" + source.name + "_URP.mat";
            result = AssetDatabase.LoadAssetAtPath<Material>(path);
            var configured = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            configured.SetTexture("_BaseMap", source.GetTexture("_MainTex"));
            configured.SetTextureScale("_BaseMap", source.GetTextureScale("_MainTex"));
            configured.SetTextureOffset("_BaseMap", source.GetTextureOffset("_MainTex"));
            configured.SetColor("_BaseColor", new Color(.87f, .89f, .86f));
            configured.SetFloat("_Metallic", 0);
            configured.SetFloat("_Smoothness", .16f);
            if (source.GetTexture("_BumpMap") != null)
            {
                configured.SetTexture("_BumpMap", source.GetTexture("_BumpMap"));
                configured.SetFloat("_BumpScale", .65f);
                configured.EnableKeyword("_NORMALMAP");
            }
            if (source.GetTexture("_OcclusionMap") != null)
            {
                configured.SetTexture("_OcclusionMap", source.GetTexture("_OcclusionMap"));
                configured.SetFloat("_OcclusionStrength", .7f);
                configured.EnableKeyword("_OCCLUSIONMAP");
            }
            configured.enableInstancing = true;
            if (result == null) { result = configured; AssetDatabase.CreateAsset(result, path); }
            else { EditorUtility.CopySerialized(configured, result); Object.DestroyImmediate(configured); }
            EditorUtility.SetDirty(result);
            AssetDatabase.SaveAssetIfDirty(result);
            Materials[source] = result;
            return result;
        }

        private static void Normalize(GameObject root, bool height)
        {
            Bounds bounds = BoundsOf(root);
            float measure = height ? bounds.size.y : new Vector2(bounds.extents.x, bounds.extents.z).magnitude;
            if (measure < .001f) throw new InvalidOperationException("Empty geometry: " + root.name);
            Vector3 offset = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            foreach (Transform child in root.transform)
            {
                child.localPosition = (child.localPosition - offset) / measure;
                child.localScale /= measure;
            }
            foreach (LODGroup group in root.GetComponentsInChildren<LODGroup>(true)) group.RecalculateBounds();
        }

        private static Bounds BoundsOf(GameObject root)
        {
            Bounds bounds = default;
            bool first = true;
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) throw new InvalidOperationException("Missing mesh in " + root.name);
                Matrix4x4 matrix = root.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                foreach (Vector3 vertex in filter.sharedMesh.vertices)
                {
                    Vector3 p = matrix.MultiplyPoint3x4(vertex);
                    if (first) { bounds = new Bounds(p, Vector3.zero); first = false; }
                    else bounds.Encapsulate(p);
                }
            }
            if (first) throw new InvalidOperationException("No geometry in " + root.name);
            return bounds;
        }

        private static GameObject Save(GameObject root, bool tree)
        {
            Bounds bounds = BoundsOf(root);
            float measure = tree ? bounds.size.y : new Vector2(bounds.extents.x, bounds.extents.z).magnitude;
            if (Mathf.Abs(bounds.min.y) > .001f || Mathf.Abs(measure - 1) > .001f
                || root.GetComponentsInChildren<Collider>(true).Length != 0)
                throw new InvalidOperationException("Invalid footprint or ground pivot: " + root.name);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, Root + "/Prefabs/" + root.name + ".prefab");
            report.AppendLine($"PASS: {root.name}, bounds={bounds.size}, grounded, collision-free, URP materials.");
            return prefab;
        }

        private static void Folder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
        }
    }
}

using System;
using System.IO;
using System.Linq;
using System.Text;
using Lightbringer.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Lightbringer.EditorTools
{
    [InitializeOnLoad]
    public static class ForestEnvironmentSetup
    {
        public const string Folder = "Assets/Art/Environment/Forest";
        public const string ProfilePath = Folder + "/ForestEnvironment.asset";
        private const string Source = "Assets/TriForge Assets/Fantasy Worlds - DEMO Content";
        private const string Common = "Assets/TriForge Assets/Fantasy Worlds - DEMO Common Files";
        private static string Diagnostics => Path.GetFullPath(Path.Combine(Application.dataPath, "../Docs/Diagnostics"));

        static ForestEnvironmentSetup()
        {
            EditorRequests.Register(Path.Combine(Diagnostics, "forest-setup.request"), Configure);
        }

        [MenuItem("Lightbringer/Art/Set Up Fantasy Worlds Forest")]
        public static void Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            AssetDatabase.Refresh();
            EnsureFolder("Assets/Art/Environment");
            EnsureFolder(Folder);
            ForestEnvironmentProfile profile = AssetDatabase.LoadAssetAtPath<ForestEnvironmentProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<ForestEnvironmentProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }
            StringBuilder report = new StringBuilder("Fantasy Worlds Forest setup | " + DateTime.UtcNow.ToString("O") + "\n");
            Scene original = SceneManager.GetActiveScene();
            Scene temporary = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(temporary);
                profile.tree = MakeProp("P_fwOF_Tree_M_2", "ForestTree", 12f, true, report);
                profile.sapling = MakeProp("P_fwOF_TreeSapling_02B", "ForestSapling", 3f, true, report);
                profile.rock = MakeProp("P_fwOF_Rock_01", "ForestRock", 1.9f, true, report);
                profile.stone = MakeProp("P_fwOF_Stone_01", "ForestStone", 0.35f, false, report);
                profile.grass = MakeProp("P_fwOF_Grass_M_1", "ForestGrass", 0.45f, false, report);
                profile.plant = MakeProp("P_fwOF_ForestPlant_B_02", "ForestPlant", 0.75f, false, report);
                profile.wind = AssetDatabase.LoadAssetAtPath<GameObject>(Common + "/TriForge Wind Controller.prefab");
                ArtStyleLibrary style = ArtStyleSetup.EnsureLibrary();
                if (!profile.IsReady || style == null) throw new InvalidOperationException("Forest assets are incomplete.");
                if (profile.terrain == null)
                {
                    string terrainPath = Folder + "/ForestTerrain.mat";
                    profile.terrain = AssetDatabase.LoadAssetAtPath<Material>(terrainPath);
                    if (profile.terrain == null)
                    {
                        Material ground = new Material(style.terrainMaterial) { name = "ForestTerrain" };
                        ground.SetTexture("_ForestGrassTex", AssetDatabase.LoadAssetAtPath<Texture2D>(Source + "/Textures/Terrain/T_fwOF_GrassTerrain_01_BC.png"));
                        ground.SetTexture("_ForestSoilTex", AssetDatabase.LoadAssetAtPath<Texture2D>(Source + "/Textures/Terrain/T_fwOF_Soil_01_BC.png"));
                        ground.SetFloat("_ForestTextureStrength", 0.8f);
                        ground.SetFloat("_ForestTextureScale", 0.25f);
                        ground.SetFloat("_FlowerAmount", 0.15f);
                        AssetDatabase.CreateAsset(ground, terrainPath);
                        profile.terrain = ground;
                    }
                }
                style.forestEnvironment = profile;
                EditorUtility.SetDirty(profile);
                EditorUtility.SetDirty(style);
                AssetDatabase.SaveAssets();
                report.AppendLine("PASS: Stage 1 forest profile assigned. Original source prefabs and gameplay colliders preserved.");
                Debug.Log("Fantasy Worlds Forest is ready for Stage 1.");
            }
            catch (Exception error)
            {
                report.AppendLine("FAIL: " + error);
                throw;
            }
            finally
            {
                if (original.IsValid()) SceneManager.SetActiveScene(original);
                EditorSceneManager.CloseScene(temporary, true);
                Directory.CreateDirectory(Diagnostics);
                File.WriteAllText(Path.Combine(Diagnostics, "ForestSetup.txt"), report.ToString());
            }
        }

        private static GameObject MakeProp(string sourceName, string name, float height, bool shadows, StringBuilder report)
        {
            string path = Folder + "/" + name + ".prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                // Repair only ground contact; preserve scale, materials and other manual tuning.
                GameObject contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    float bottom = GeometryBounds(contents).min.y;
                    report.AppendLine($"{name}: mesh ground offset {bottom:0.0000} m (renderer bounds may include wind padding).");
                    if (Mathf.Abs(bottom) > 0.001f)
                    {
                        foreach (Transform child in contents.transform) child.localPosition -= Vector3.up * bottom;
                        PrefabUtility.SaveAsPrefabAsset(contents, path);
                    }
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
                return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(Source + "/Prefabs/" + sourceName + ".prefab");
            if (source == null) throw new InvalidOperationException("Missing source prefab: " + sourceName);
            GameObject root = new GameObject(name);
            try
            {
                GameObject model = UnityEngine.Object.Instantiate(source, root.transform);
                model.name = sourceName;
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one;
                foreach (Collider collider in model.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
                Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) throw new InvalidOperationException("No renderers: " + sourceName);
                Bounds bounds = GeometryBounds(root);
                if (bounds.size.y < 0.01f) throw new InvalidOperationException("Empty mesh bounds: " + sourceName);
                float scale = height / bounds.size.y;
                model.transform.localScale = Vector3.one * scale;
                model.transform.localPosition = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z) * scale;
                foreach (Renderer renderer in renderers)
                {
                    if (renderer.sharedMaterials.Any(m => m == null || m.shader == null || !m.shader.isSupported))
                        throw new InvalidOperationException("Unsupported or missing material on " + sourceName);
                    renderer.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                    renderer.lightProbeUsage = LightProbeUsage.Off;
                    renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                }
                report.AppendLine($"{name}: {bounds.size} -> {height:0.00} m height; {renderers.Length} renderers, {model.GetComponentsInChildren<LODGroup>().Length} LOD groups.");
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static Bounds GeometryBounds(GameObject root)
        {
            Bounds bounds = default;
            bool found = false;
            // In the Editor imported vertex data is available even when runtime Read/Write is off.
            // Renderer/mesh AABBs can be enlarged for wind and must not define the visible foot.
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                Matrix4x4 toRoot = root.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                foreach (Vector3 vertex in filter.sharedMesh.vertices)
                {
                    Vector3 point = toRoot.MultiplyPoint3x4(vertex);
                    if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; }
                    else bounds.Encapsulate(point);
                }
            }
            if (!found) throw new InvalidOperationException("No mesh vertices: " + root.name);
            return bounds;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
        }
    }
}

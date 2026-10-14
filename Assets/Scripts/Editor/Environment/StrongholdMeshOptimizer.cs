using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Lightbringer.EditorTools
{
    // The Tripo stronghold FBX files carry about 500k triangles each. This gives each stronghold prefab a
    // budgeted copy of its mesh (Mesh LOD simplification, unused vertices dropped, same-material sub-meshes
    // merged) saved beside the FBX as <name>_Optimized.asset. The FBX is left untouched. Already-optimized
    // prefabs are skipped. Re-running Set Up Allied/Enemy Stronghold Model rebuilds the prefab from the FBX,
    // so run this again afterwards. Output: Docs/Diagnostics/StrongholdMeshes.txt.
    // Also runs when Docs/Diagnostics/stronghold-meshes.request exists.
    [InitializeOnLoad]
    public static class StrongholdMeshOptimizer
    {
        private const string Suffix = "_Optimized";
        private const int TriangleBudget = 25000;
        private static readonly string[] Names = { "AlliedStronghold", "EnemyStronghold" };

        private static string Folder => Path.Combine(Path.GetDirectoryName(Application.dataPath), "Docs", "Diagnostics");

        static StrongholdMeshOptimizer() => EditorRequests.Register(Path.Combine(Folder, "stronghold-meshes.request"), Run);

        [MenuItem("Lightbringer/Art/Optimize Stronghold Meshes")]
        public static void Run()
        {
            StringBuilder report = new StringBuilder("Stronghold mesh optimization | " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "\n");
            foreach (string name in Names)
            {
                string folder = "Assets/Art/Buildings/" + name;
                string prefabPath = folder + "/" + name + ".prefab";
                GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    bool changed = false;
                    foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
                    {
                        Mesh source = filter.sharedMesh;
                        MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
                        if (source == null || renderer == null) continue;
                        if (source.name.EndsWith(Suffix))
                        {
                            report.AppendLine($"{name}: already optimized ({Triangles(source):N0} triangles), skipped");
                            continue;
                        }
                        Mesh optimized = CrowdMeshOptimizer.Simplify(source, renderer.sharedMaterials, TriangleBudget, out Material[] merged);
                        optimized.name = name + Suffix;
                        string meshPath = folder + "/" + optimized.name + ".asset";
                        optimized = RigBuildUtility.SaveAsset(optimized, meshPath);
                        filter.sharedMesh = optimized;
                        renderer.sharedMaterials = merged;
                        changed = true;
                        report.AppendLine($"{name}: triangles {Triangles(source):N0} -> {Triangles(optimized):N0}, vertices {source.vertexCount:N0} -> {optimized.vertexCount:N0}, "
                            + $"distance LODs {optimized.lodCount} | {meshPath}");
                    }
                    if (changed) PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Path.Combine(Folder, "StrongholdMeshes.txt"), report.ToString());
            Debug.Log("Stronghold meshes optimized; see Docs/Diagnostics/StrongholdMeshes.txt");
        }

        private static int Triangles(Mesh mesh)
        {
            int count = 0;
            for (int i = 0; i < mesh.subMeshCount; i++) count += (int)mesh.GetIndexCount(i) / 3;
            return count;
        }
    }
}

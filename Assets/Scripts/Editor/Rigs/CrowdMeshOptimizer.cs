using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Lightbringer.Visuals;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Lightbringer.EditorTools
{
    // The generated troop models carry 300k-1M triangles each, far beyond what a battlefield of a hundred
    // troops can draw (and GPU skinning processes every vertex, whatever LOD is shown). This tool gives every
    // allied troop prefab a crowd-budget copy of its skinned mesh:
    //   1. Unity's Mesh LOD generator simplifies the source; the level nearest the budget is kept.
    //   2. Unused vertices are dropped. Kept vertices are untouched, so skin weights and UVs stay exact.
    //   3. Sub-meshes sharing a material are merged, cutting draw calls in every pass (colour, shadows).
    //   4. Mesh LODs are generated on the result so distant troops draw even fewer triangles.
    // The source *_Skinned.asset stays as it was; the prefab points at <source>_Crowd.asset instead.
    // Already-optimized renderers are skipped. Re-running a rig setup rebuilds the prefab from the source
    // mesh, so run this again afterwards. The hero is single and close to the camera, so it is left alone.
    // Output: Docs/Diagnostics/CrowdMeshes.txt. Also runs when Docs/Diagnostics/crowdmeshes.request exists.
    [InitializeOnLoad]
    public static class CrowdMeshOptimizer
    {
        private const string Suffix = "_Crowd";
        private const int DefaultBudget = 12000;
        // Triangle budget per skinned renderer where the default does not fit.
        private static readonly Dictionary<string, int> Budgets = new Dictionary<string, int>
        {
            { "Dragon/Body", 16000 }, { "Knight/Horse", 11000 }, { "Knight/Rider", 9000 },
        };

        private static string Folder => Path.Combine(Path.GetDirectoryName(Application.dataPath), "Docs", "Diagnostics");

        static CrowdMeshOptimizer() => EditorRequests.Register(Path.Combine(Folder, "crowdmeshes.request"), Run);

        [MenuItem("Lightbringer/Art/Optimize Crowd Meshes")]
        public static void Run()
        {
            ArtStyleLibrary library = AssetDatabase.LoadAssetAtPath<ArtStyleLibrary>(ArtStyleSetup.LibraryPath);
            if (library == null || library.overrides == null) { Debug.LogError("Art style library not found."); return; }
            StringBuilder report = new StringBuilder("Crowd mesh optimization | " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "\n");
            foreach (VisualOverride entry in library.overrides)
            {
                if (entry == null || entry.prefab == null || entry.id == VisualId.Hero) continue;
                string prefabPath = AssetDatabase.GetAssetPath(entry.prefab);
                GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    bool changed = false;
                    foreach (SkinnedMeshRenderer skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                        changed |= Optimize(entry.id, skin, report);
                    if (changed) PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Path.Combine(Folder, "CrowdMeshes.txt"), report.ToString());
            Debug.Log("Crowd meshes optimized; see Docs/Diagnostics/CrowdMeshes.txt");
        }

        private static bool Optimize(VisualId id, SkinnedMeshRenderer skin, StringBuilder report)
        {
            Mesh source = skin.sharedMesh;
            string label = $"{id}/{skin.name}";
            if (source == null) return false;
            string sourcePath = AssetDatabase.GetAssetPath(source);
            if (source.name.EndsWith(Suffix)) { report.AppendLine($"{label}: already optimized ({Triangles(source):N0} triangles), skipped"); return false; }
            if (string.IsNullOrEmpty(sourcePath) || !sourcePath.EndsWith(".asset"))
            {
                report.AppendLine($"{label}: mesh is not a standalone .asset ({sourcePath}), skipped");
                return false;
            }
            int budget = Budgets.TryGetValue(label, out int custom) ? custom : DefaultBudget;
            Material[] materials = skin.sharedMaterials;
            Mesh crowd = Simplify(source, materials, budget, out Material[] merged);
            crowd.name = source.name + Suffix;
            string crowdPath = sourcePath.Substring(0, sourcePath.Length - ".asset".Length) + Suffix + ".asset";
            crowd = RigBuildUtility.SaveAsset(crowd, crowdPath);
            skin.sharedMesh = crowd;
            skin.sharedMaterials = merged;
            report.AppendLine($"{label}: triangles {Triangles(source):N0} -> {Triangles(crowd):N0}, vertices {source.vertexCount:N0} -> {crowd.vertexCount:N0}, "
                + $"draw calls {source.subMeshCount} -> {crowd.subMeshCount}, distance LODs {crowd.lodCount} | {crowdPath}");
            return true;
        }

        private static int Triangles(Mesh mesh)
        {
            int count = 0;
            for (int i = 0; i < mesh.subMeshCount; i++) count += (int)mesh.GetIndexCount(i) / 3;
            return count;
        }

        // Also used for static environment props; Compact preserves only attributes present on the source.
        internal static Mesh Simplify(Mesh source, Material[] materials, int budget, out Material[] merged,
            bool distanceLods = true)
        {
            Mesh work = Object.Instantiate(source);
            try
            {
                MeshLodUtility.GenerateMeshLods(work, -1);
                float ratio = Mathf.Min(1f, budget / (float)Mathf.Max(1, Triangles(source)));
                int[] buffer;
                using (Mesh.MeshDataArray data = Mesh.AcquireReadOnlyMeshData(work))
                    buffer = data[0].indexFormat == IndexFormat.UInt16
                        ? data[0].GetIndexData<ushort>().Select(index => (int)index).ToArray()
                        : data[0].GetIndexData<int>().ToArray();

                // Gather each sub-mesh's chosen level, grouped by material in first-seen order.
                List<Material> order = new List<Material>();
                Dictionary<int, List<int>> groups = new Dictionary<int, List<int>>();
                List<MeshLodRange> ranges = new List<MeshLodRange>();
                for (int s = 0; s < work.subMeshCount; s++)
                {
                    SubMeshDescriptor sub = work.GetSubMesh(s);
                    ranges.Clear();
                    work.GetLods(ranges, s);
                    int start = sub.indexStart, count = sub.indexCount;
                    if (ranges.Count > 0)
                    {
                        // Ranges may be absolute in the index buffer or relative to the sub-mesh.
                        int offset = (int)ranges[0].indexStart == sub.indexStart ? 0 : sub.indexStart;
                        float want = sub.indexCount / 3f * ratio;
                        MeshLodRange chosen = ranges[ranges.Count - 1];
                        foreach (MeshLodRange range in ranges)
                            if ((int)range.indexCount / 3f <= want * 1.2f) { chosen = range; break; }
                        start = offset + (int)chosen.indexStart;
                        count = (int)chosen.indexCount;
                    }
                    Material material = s < materials.Length ? materials[s] : null;
                    int key = order.IndexOf(material);
                    if (key < 0) { key = order.Count; order.Add(material); groups[key] = new List<int>(); }
                    for (int i = 0; i < count; i++) groups[key].Add(buffer[start + i] + sub.baseVertex);
                }
                merged = order.ToArray();
                return Compact(source, groups, order.Count, distanceLods);
            }
            finally { Object.DestroyImmediate(work); }
        }

        // Copies only the referenced vertices, every attribute unchanged.
        private static Mesh Compact(Mesh source, Dictionary<int, List<int>> groups, int subMeshes, bool distanceLods)
        {
            Dictionary<int, int> remap = new Dictionary<int, int>();
            List<int> kept = new List<int>();
            List<int>[] indices = new List<int>[subMeshes];
            for (int g = 0; g < subMeshes; g++)
            {
                indices[g] = new List<int>(groups[g].Count);
                foreach (int index in groups[g])
                {
                    if (!remap.TryGetValue(index, out int mapped)) { mapped = kept.Count; remap[index] = mapped; kept.Add(index); }
                    indices[g].Add(mapped);
                }
            }
            Mesh mesh = new Mesh { indexFormat = kept.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(Pick(source.vertices, kept));
            if (source.HasVertexAttribute(VertexAttribute.Normal)) mesh.SetNormals(Pick(source.normals, kept));
            if (source.HasVertexAttribute(VertexAttribute.Tangent)) mesh.SetTangents(Pick(source.tangents, kept));
            if (source.HasVertexAttribute(VertexAttribute.Color)) mesh.SetColors(Pick(source.colors, kept));
            for (int channel = 0; channel < 8; channel++)
            {
                VertexAttribute attribute = VertexAttribute.TexCoord0 + channel;
                if (!source.HasVertexAttribute(attribute)) continue;
                int dimension = source.GetVertexAttributeDimension(attribute);
                if (dimension == 2) { List<Vector2> uv = new List<Vector2>(); source.GetUVs(channel, uv); mesh.SetUVs(channel, Pick(uv.ToArray(), kept)); }
                else if (dimension == 3) { List<Vector3> uv = new List<Vector3>(); source.GetUVs(channel, uv); mesh.SetUVs(channel, Pick(uv.ToArray(), kept)); }
                else { List<Vector4> uv = new List<Vector4>(); source.GetUVs(channel, uv); mesh.SetUVs(channel, Pick(uv.ToArray(), kept)); }
            }
            BoneWeight[] weights = source.boneWeights;
            if (weights.Length == source.vertexCount) mesh.boneWeights = Pick(weights, kept);
            mesh.bindposes = source.bindposes;
            mesh.subMeshCount = subMeshes;
            for (int g = 0; g < subMeshes; g++) mesh.SetTriangles(indices[g], g, false);
            mesh.RecalculateBounds();
            // Re-simplifying an already small mesh collapses it, so low-budget props skip the distance chain.
            if (distanceLods) MeshLodUtility.GenerateMeshLods(mesh, 3);
            mesh.UploadMeshData(false);
            return mesh;
        }

        private static T[] Pick<T>(T[] values, List<int> kept)
        {
            T[] result = new T[kept.Count];
            for (int i = 0; i < kept.Count; i++) result[i] = values[kept[i]];
            return result;
        }
    }
}

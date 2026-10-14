using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Lightbringer.EditorTools
{
    // Counts triangles of every model file (.fbx/.glb) under Assets/Art so heavy meshes can be found.
    // Output: Docs/Diagnostics/ModelTriangles.txt. Also runs when Docs/Diagnostics/modeltriangles.request exists.
    [InitializeOnLoad]
    public static class ModelTriangleReport
    {
        private static string Folder => Path.Combine(Path.GetDirectoryName(Application.dataPath), "Docs", "Diagnostics");

        static ModelTriangleReport() => EditorRequests.Register(Path.Combine(Folder, "modeltriangles.request"), Run, true);

        [MenuItem("Lightbringer/Diagnostics/Report Model Triangles")]
        public static void Run()
        {
            StringBuilder report = new StringBuilder("Model triangle report | " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "\n");
            string[] paths = AssetDatabase.FindAssets("t:Model", new[] { "Assets/Art" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !path.Contains("/Animations/"))
                .OrderBy(path => path).ToArray();
            foreach (string path in paths)
            {
                long triangles = 0, vertices = 0;
                int meshes = 0;
                foreach (Mesh mesh in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>())
                {
                    meshes++;
                    vertices += mesh.vertexCount;
                    for (int s = 0; s < mesh.subMeshCount; s++) triangles += (long)mesh.GetIndexCount(s) / 3;
                }
                report.AppendLine($"{triangles,9:N0} tris {vertices,9:N0} verts {meshes,2} meshes | {path}");
            }
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Path.Combine(Folder, "ModelTriangles.txt"), report.ToString());
            Debug.Log("Model triangle report written; see Docs/Diagnostics/ModelTriangles.txt");
        }
    }
}

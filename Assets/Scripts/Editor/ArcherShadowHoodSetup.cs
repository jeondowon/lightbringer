using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Lightbringer.EditorTools
{
    [InitializeOnLoad]
    public static class ArcherShadowHoodSetup
    {
        private const string Root = "Assets/Art/Characters/Archer";
        private const string Output = Root + "/ShadowHood";

        static ArcherShadowHoodSetup() => EditorRequests.Register("Docs/Diagnostics/ArcherImport/ShadowHood.request", Build);

        [MenuItem("Lightbringer/Art/Build Archer Shadow Hood")]
        public static void Build()
        {
            var source = AssetDatabase.LoadAssetAtPath<Mesh>(Root + "/FaceRepair/Archer_Upright.asset");
            bool hasBody = AssetDatabase.LoadAssetAtPath<Material>(Root + "/FaceRepair/Archer_FaceFixed.mat") != null;
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (source == null || !hasBody || shader == null)
                throw new InvalidOperationException("Archer upright mesh, body material and URP Unlit shader are required.");
            if (!AssetDatabase.IsValidFolder(Output)) AssetDatabase.CreateFolder(Root, "ShadowHood");

            var mesh = UnityEngine.Object.Instantiate(source);
            mesh.name = "Archer_ShadowHood";
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            var exterior = new List<int>();
            var interior = new List<int>();
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 p = (vertices[triangles[i]] + vertices[triangles[i + 1]] + vertices[triangles[i + 2]]) / 3f;
                // Includes cheeks, hair and throat inside the opening, including rear-facing folds.
                float oval = Mathf.Pow(p.x / .077f, 2) + Mathf.Pow((p.y - 1.502f) / .094f, 2);
                bool inHood = p.z > 0 && oval < 1;
                List<int> target = inHood ? interior : exterior;
                target.Add(triangles[i]); target.Add(triangles[i + 1]); target.Add(triangles[i + 2]);
            }
            if (interior.Count < 300) { UnityEngine.Object.DestroyImmediate(mesh); throw new InvalidOperationException("Face region was not found."); }
            mesh.subMeshCount = 2;
            mesh.SetTriangles(exterior, 0);
            mesh.SetTriangles(interior, 1);
            Mesh savedMesh = RigBuildUtility.SaveAsset(mesh, Output + "/Archer_ShadowHood.asset");

            string materialPath = Output + "/HoodInterior.mat";
            var shadow = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (shadow == null) { shadow = new Material(shader); AssetDatabase.CreateAsset(shadow, materialPath); }
            shadow.shader = shader;
            shadow.SetColor("_BaseColor", new Color(.018f, .023f, .035f, 1));
            shadow.SetTexture("_BaseMap", null);
            shadow.SetFloat("_Cull", 0); // Hide face folds from oblique views as well.
            EditorUtility.SetDirty(shadow);
            AssetDatabase.SaveAssets();
            string diagnostics = "Docs/Diagnostics/ArcherImport";
            Directory.CreateDirectory(diagnostics);
            var atlas = new Texture2D(2, 2);
            try
            {
                atlas.LoadImage(File.ReadAllBytes(Root + "/FaceRepair/Archer_BaseColor_FaceFixed.png"));
                ArcherFaceRepair.Render(savedMesh, atlas, diagnostics + "/ShadowHood_Front.png", 0, exterior.Count);
                ArcherFaceRepair.Render(savedMesh, atlas, diagnostics + "/ShadowHood_Side.png", 55, exterior.Count);
            }
            finally { UnityEngine.Object.DestroyImmediate(atlas); }
            File.WriteAllText(diagnostics + "/ShadowHood.txt", $"Shadow triangles: {interior.Count / 3}; total: {triangles.Length / 3}. Unlit interior, no facial texture or specular response.\n");
            Debug.Log("Created Archer_ShadowHood mesh and hood material.");
        }
    }
}

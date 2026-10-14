using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lightbringer.Visuals;
using UnityEditor;
using UnityEngine;

namespace Lightbringer.EditorTools
{
    [InitializeOnLoad]
    public static class ForestSurfaceSetup
    {
        private const string Folder = "Assets/Art/Environment/Forest";
        private const string Textures = "Assets/TriForge Assets/Fantasy Worlds - DEMO Content/Textures/Terrain/";
        static ForestSurfaceSetup() => EditorRequests.Register("Docs/Diagnostics/forest-surfaces.request", Run);

        [MenuItem("Lightbringer/Art/Improve Forest Surfaces and Root Contact")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            AssetDatabase.ImportAsset("Assets/Art/Shaders/LightbringerTerrain.shader", ImportAssetOptions.ForceUpdate);
            var report = new List<string> { "Forest surfaces | " + DateTime.UtcNow.ToString("O") };
            foreach (string name in new[] { "ForestTree", "ForestSapling" }) BakeRoots(name, report);
            foreach (string name in new[] { "StageOneGladeGround", "ForestTerrain" })
            {
                Material ground = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/" + name + ".mat");
                if (ground == null) throw new InvalidOperationException("Missing terrain material: " + name);
                ground.SetTexture("_StoneTex", LoadTexture("T_fwOF_RockSurf_02_BC.png"));
                ground.SetTexture("_StoneNormal", LoadTexture("T_fwOF_RockSurf_02_N.png"));
                ground.SetTexture("_StoneMask", LoadTexture("T_fwOF_RockSurf_02_Mask.png"));
                if (ground.GetTexture("_StoneTex") == null || ground.GetTexture("_StoneNormal") == null)
                    throw new InvalidOperationException("Missing rock surface textures");
                ground.SetFloat("_StoneTextureStrength", 1f);
                ground.SetFloat("_StoneTextureScale", 0.38f);
                ground.SetFloat("_ForestTextureScale", 0.48f);
                ground.SetFloat("_ForestNormalStrength", 0.85f);
                ground.SetFloat("_NaturalLight", 1f);
                ground.SetFloat("_SoilBreakup", 0.85f);
                ground.SetFloat("_PatchStrength", 0.12f);
                ground.SetFloat("_GravelAmount", 0.2f);
                ground.SetColor("_ForestGroundTint", new Color(1.25f, 1.3f, 1.02f, 1f));
                ground.SetColor("_ShadowColor", new Color(0.43f, 0.48f, 0.5f));
                EditorUtility.SetDirty(ground);
                report.Add("PASS: " + name + " uses weathered rock colour/normal/mask textures");
            }
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Docs/Diagnostics");
            File.WriteAllLines("Docs/Diagnostics/ForestSurfaces.txt", report);
        }

        private static Texture2D LoadTexture(string name)
        {
            string path = Textures + name;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && (importer.anisoLevel != 8 || importer.filterMode != FilterMode.Trilinear || !importer.mipmapEnabled))
            {
                importer.anisoLevel = 8;
                importer.filterMode = FilterMode.Trilinear;
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
            }
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null) throw new InvalidOperationException("Could not load texture: " + path);
            return texture;
        }

        private static void BakeRoots(string name, List<string> report)
        {
            string path = Folder + "/" + name + ".prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                // Use visible geometry, including every LOD, never the wind-padded renderer bounds.
                var vertices = new List<Vector3>();
                foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.sharedMesh == null || filter.GetComponent<Renderer>() == null) continue;
                    Matrix4x4 toRoot = root.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                    foreach (Vector3 vertex in filter.sharedMesh.vertices) vertices.Add(toRoot.MultiplyPoint3x4(vertex));
                }
                if (vertices.Count == 0) throw new InvalidOperationException("No root geometry: " + name);
                float bottom = vertices.Min(v => v.y);
                float rootBand = Mathf.Clamp((vertices.Max(v => v.y) - bottom) * 0.1f, 0.25f, 1.3f);
                var samples = new Dictionary<Vector2Int, Vector3>();
                foreach (Vector3 vertex in vertices)
                {
                    if (vertex.y > bottom + rootBand) continue;
                    Vector2Int cell = new Vector2Int(Mathf.FloorToInt(vertex.x / 0.25f), Mathf.FloorToInt(vertex.z / 0.25f));
                    if (!samples.TryGetValue(cell, out Vector3 previous) || vertex.y < previous.y) samples[cell] = vertex;
                }
                var contact = root.GetComponent<ForestGroundContact>();
                if (contact == null) contact = root.AddComponent<ForestGroundContact>();
                contact.supportPoints = samples.OrderBy(pair => pair.Key.x).ThenBy(pair => pair.Key.y).Select(pair => pair.Value).ToArray();
                PrefabUtility.SaveAsPrefabAsset(root, path);
                report.Add($"PASS: {name}, {vertices.Count} vertices, bottom {bottom:0.000}, {samples.Count} baked root contacts");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}

using System.Collections.Generic;
using System.IO;
using System.Text;
using Lightbringer.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Lightbringer.EditorTools
{
    // Diagnostic: renders every battlefield prop at a ladder of triangle budgets (the same simplifier the
    // prop import uses), one row per prop, so the lowest budget that keeps the silhouette can be chosen.
    // Output: Docs/Diagnostics/PropLodLadder_<Kind>.png + PropLodLadder.txt. Request: proplodladder.request.
    [InitializeOnLoad]
    public static class PropLodLadder
    {
        private static readonly int[] Ladder = { 32000, 20000, 14000, 10000, 7000, 5000 };
        private const int Tile = 360;
        private static string Output => Path.Combine(Path.GetDirectoryName(Application.dataPath), "Docs", "Diagnostics");

        static PropLodLadder() => EditorRequests.Register(Path.Combine(Output, "proplodladder.request"), Run);

        [MenuItem("Lightbringer/Diagnostics/Prop LOD Ladder")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Directory.CreateDirectory(Output);
            // Temporary additive scene: nothing is added to the scene open in the Editor.
            Scene original = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var report = new StringBuilder("Prop LOD ladder | " + System.DateTime.UtcNow.ToString("O") + "\n");
            var light = new GameObject("Ladder sun").AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.5f;
            light.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
            var camera = new GameObject("Ladder camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.22f, 0.24f, 0.28f);
            camera.fieldOfView = 40f;
            camera.scene = scene;
            var target = new RenderTexture(Tile, Tile, 24) { antiAliasing = 4 };
            camera.targetTexture = target;
            var created = new List<Object>();
            try
            {
                foreach (BattlefieldPropKind kind in System.Enum.GetValues(typeof(BattlefieldPropKind)))
                {
                    string name = kind.ToString(), folder = BattlefieldPropSetup.Folder + "/" + name;
                    GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "/" + name + ".fbx");
                    Material material = AssetDatabase.LoadAssetAtPath<Material>(folder + "/" + name + ".mat");
                    if (model == null || material == null) { report.AppendLine(name + ": missing model/material"); continue; }
                    GameObject instance = Object.Instantiate(model, new Vector3(9000f, 0f, 0f), Quaternion.Euler(-90f, 0f, 0f));
                    var sheet = new Texture2D(Tile * Ladder.Length, Tile, TextureFormat.RGB24, false);
                    try
                    {
                        MeshFilter filter = instance.GetComponentInChildren<MeshFilter>();
                        MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
                        Mesh source = filter.sharedMesh;
                        report.Append($"{name}: source {source.triangles.Length / 3}");
                        for (int i = 0; i < Ladder.Length; i++)
                        {
                            Mesh simple = CrowdMeshOptimizer.Simplify(source, new[] { material }, Ladder[i], out Material[] merged, false);
                            created.Add(simple);
                            filter.sharedMesh = simple;
                            renderer.sharedMaterials = merged;
                            var ranges = new List<MeshLodRange>();
                            simple.GetLods(ranges, 0);
                            int near = ranges.Count > 0 ? (int)ranges[0].indexCount / 3 : (int)simple.GetIndexCount(0) / 3;
                            report.Append($" | budget {Ladder[i]} -> {near}");
                            Bounds bounds = renderer.bounds;
                            float distance = bounds.extents.magnitude / Mathf.Sin(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.05f;
                            camera.transform.position = bounds.center + new Vector3(0.65f, 0.35f, -1f).normalized * distance;
                            camera.transform.LookAt(bounds.center);
                            camera.Render();
                            RenderTexture.active = target;
                            sheet.ReadPixels(new Rect(0, 0, Tile, Tile), i * Tile, 0);
                            RenderTexture.active = null;
                        }
                        report.AppendLine();
                        sheet.Apply();
                        File.WriteAllBytes(Path.Combine(Output, "PropLodLadder_" + name + ".png"), sheet.EncodeToPNG());
                    }
                    finally { Object.DestroyImmediate(instance); Object.DestroyImmediate(sheet); }
                }
            }
            finally
            {
                foreach (Object item in created) if (item != null) Object.DestroyImmediate(item);
                camera.targetTexture = null;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(camera.gameObject);
                Object.DestroyImmediate(light.gameObject);
                File.WriteAllText(Path.Combine(Output, "PropLodLadder.txt"), report.ToString());
                if (original.IsValid()) SceneManager.SetActiveScene(original);
                EditorSceneManager.CloseScene(scene, true);
            }
            Debug.Log("Prop LOD ladder written to " + Output);
        }
    }
}

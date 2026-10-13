using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Lightbringer.EditorTools
{
    [InitializeOnLoad]
    internal static class DryTreesUrpSetup
    {
        private const string Root = "Assets/Dry_Trees";
        private const string Materials = Root + "/Materials/";
        private const string Output = "Docs/Diagnostics";
        private const int Width = 224, Height = 320;

        static DryTreesUrpSetup() => EditorRequests.Register(Output + "/dry-trees-urp.request", Run);

        [MenuItem("Lightbringer/Art/Convert and Validate Dry Trees URP")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Directory.CreateDirectory(Output);
            var report = new StringBuilder("Dry Trees URP | " + DateTime.UtcNow.ToString("O") + "\n");
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null || !shader.isSupported || ShaderUtil.ShaderHasError(shader))
                    throw new InvalidOperationException("URP/Lit is unavailable or has shader errors.");
                if (GraphicsSettings.currentRenderPipeline == null ||
                    GraphicsSettings.currentRenderPipeline.GetType().Name != "UniversalRenderPipelineAsset")
                    throw new InvalidOperationException("The active render pipeline is not URP.");

                RemoveLegacyMetadata();
                Texture2D color = ConfigureTexture(Materials + "Bark.tif", false, true);
                Texture2D normal = ConfigureTexture(Materials + "Normal.tif", true, false);
                Texture2D mask = PackMask();
                Material bark = AssetDatabase.LoadAssetAtPath<Material>(Materials + "Bark.mat");
                if (bark == null) throw new InvalidOperationException("Missing Bark.mat.");
                // Start with clean URP properties and keywords; retain the material's GUID for all LODs.
                var replacement = new Material(shader) { name = "Bark", enableInstancing = true };
                try
                {
                    replacement.SetTexture("_BaseMap", color);
                    replacement.SetTextureScale("_BaseMap", new Vector2(1f, 1.2f));
                    replacement.SetColor("_BaseColor", Color.white);
                    replacement.SetTexture("_BumpMap", normal);
                    replacement.SetFloat("_BumpScale", 1f);
                    replacement.EnableKeyword("_NORMALMAP");
                    replacement.SetTexture("_MetallicGlossMap", mask);
                    replacement.SetFloat("_Metallic", 1f);
                    replacement.SetFloat("_Smoothness", 1f);
                    replacement.EnableKeyword("_METALLICSPECGLOSSMAP");
                    replacement.SetTexture("_OcclusionMap", mask);
                    replacement.SetFloat("_OcclusionStrength", 1f);
                    replacement.EnableKeyword("_OCCLUSIONMAP");
                    replacement.renderQueue = (int)RenderQueue.Geometry;
                    EditorUtility.CopySerialized(replacement, bark);
                }
                finally { Object.DestroyImmediate(replacement); }
                EditorUtility.SetDirty(bark);
                AssetDatabase.SaveAssetIfDirty(bark);
                report.AppendLine("PASS: Bark uses URP/Lit; base color, tangent normal, metallic, AO and smoothness assigned.");
                report.AppendLine("Mask: R = HDRP metallic remap [0.02374169,1]; G = AO; A = smoothness. Linear sampling.");
                report.AppendLine("Base tiling retained: (1, 1.2). Shared material GUID retained; GPU instancing enabled.");

                string[] paths = AssetDatabase.FindAssets("t:Prefab", new[] { Root + "/Prefab" })
                    .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p).ToArray();
                if (paths.Length != 7) throw new InvalidOperationException("Expected 7 tree prefabs, got " + paths.Length);
                foreach (string path in paths) Validate(path, bark, report);
                Capture(paths, report);
                if (ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("URP/Lit shader errors after rendering.");
                report.AppendLine("PASS: All 7 prefabs and all LODs validated and rendered in URP. No campaign scenes modified.");
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(paths[0]);
                EditorGUIUtility.PingObject(Selection.activeObject);
                Debug.Log("Dry Trees: URP conversion and preview validation complete. " + Output + "/DryTreesUrpChecks.txt");
            }
            catch (Exception error)
            {
                report.AppendLine("FAIL: " + error);
                throw;
            }
            finally { File.WriteAllText(Output + "/DryTreesUrpChecks.txt", report.ToString()); }
        }

        private static void RemoveLegacyMetadata()
        {
            // The source embeds an HDRP version subasset whose script is absent in URP.
            string path = Materials + "Bark.mat";
            string yaml = File.ReadAllText(path);
            string clean = string.Concat(Regex.Split(yaml, @"(?=^--- !u!)", RegexOptions.Multiline)
                .Where(document => !(document.StartsWith("--- !u!114 ") &&
                    document.Contains("guid: da692e001514ec24dbc4cca1949ff7e8"))));
            if (clean == yaml) return;
            File.WriteAllText(path, clean);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }

        private static Texture2D ConfigureTexture(string path, bool normal, bool srgb)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new FileNotFoundException(path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = srgb;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 4;
            if (!normal && !srgb)
            {
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = false;
            }
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Texture2D PackMask()
        {
            string source = Materials + "Bark_Mask.tif";
            string destination = Materials + "Bark_URP_Mask.png";
            var importer = AssetImporter.GetAtPath(source) as TextureImporter;
            if (importer == null) throw new FileNotFoundException(source);
            bool readable = importer.isReadable;
            bool srgb = importer.sRGBTexture;
            TextureImporterCompression compression = importer.textureCompression;
            Texture2D packed = null;
            try
            {
                importer.isReadable = true;
                importer.sRGBTexture = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
                Texture2D input = AssetDatabase.LoadAssetAtPath<Texture2D>(source);
                Color[] pixels = input.GetPixels();
                for (int i = 0; i < pixels.Length; i++)
                {
                    Color pixel = pixels[i];
                    pixels[i] = new Color(Mathf.Lerp(0.02374169f, 1f, pixel.r), pixel.g, 0f, pixel.a);
                }
                packed = new Texture2D(input.width, input.height, TextureFormat.RGBA32, false, true);
                packed.SetPixels(pixels);
                packed.Apply();
                File.WriteAllBytes(destination, packed.EncodeToPNG());
            }
            finally
            {
                if (packed != null) Object.DestroyImmediate(packed);
                importer.isReadable = readable;
                importer.sRGBTexture = srgb;
                importer.textureCompression = compression;
                importer.SaveAndReimport();
            }
            AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceSynchronousImport);
            return ConfigureTexture(destination, false, false);
        }

        private static void Validate(string path, Material bark, StringBuilder report)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            foreach (Transform node in prefab.GetComponentsInChildren<Transform>(true))
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject) > 0)
                    throw new InvalidOperationException("Missing script: " + path);
            foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh == null) throw new InvalidOperationException("Missing mesh: " + path);
            LODGroup group = prefab.GetComponentInChildren<LODGroup>();
            if (group == null || group.lodCount < 2) throw new InvalidOperationException("Missing LODs: " + path);
            LOD[] lods = group.GetLODs();
            var counts = new string[lods.Length];
            for (int i = 0; i < lods.Length; i++)
            {
                if (lods[i].renderers.Length == 0) throw new InvalidOperationException("Empty LOD: " + path);
                long triangles = 0;
                foreach (Renderer renderer in lods[i].renderers)
                {
                    if (renderer == null || renderer.sharedMaterials.Length == 0 ||
                        renderer.sharedMaterials.Any(m => m != bark))
                        throw new InvalidOperationException("Missing or unexpected LOD material: " + path);
                    Mesh mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
                    if (mesh == null) throw new InvalidOperationException("Missing LOD mesh: " + path);
                    for (int sub = 0; sub < mesh.subMeshCount; sub++) triangles += mesh.GetIndexCount(sub) / 3;
                }
                counts[i] = triangles.ToString();
            }
            report.AppendLine("PASS: " + prefab.name + " | LOD triangles: " + string.Join(", ", counts));
        }

        private static void Capture(string[] paths, StringBuilder report)
        {
            int rows = paths.Max(p => AssetDatabase.LoadAssetAtPath<GameObject>(p).GetComponentInChildren<LODGroup>().lodCount);
            Scene scene = EditorSceneManager.NewPreviewScene();
            var target = new RenderTexture(Width, Height, 24);
            var tile = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            var sheet = new Texture2D(Width * paths.Length, Height * rows, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                var camera = new GameObject("Dry Trees Preview Camera").AddComponent<Camera>();
                SceneManager.MoveGameObjectToScene(camera.gameObject, scene);
                camera.enabled = false;
                camera.scene = scene;
                camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
                camera.targetTexture = target;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.22f, .27f, .32f);
                camera.orthographic = true;
                camera.nearClipPlane = .01f;
                camera.farClipPlane = 1000f;
                for (int i = 0; i < 2; i++)
                {
                    var light = new GameObject("Preview Light").AddComponent<Light>();
                    SceneManager.MoveGameObjectToScene(light.gameObject, scene);
                    light.type = LightType.Directional;
                    light.intensity = i == 0 ? 1.6f : .7f;
                    light.transform.rotation = Quaternion.Euler(35f, i == 0 ? 35f : 210f, 0f);
                }
                for (int column = 0; column < paths.Length; column++)
                {
                    GameObject model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(paths[column]));
                    SceneManager.MoveGameObjectToScene(model, scene);
                    try
                    {
                        var bounds = new Bounds(model.transform.position, Vector3.zero);
                        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(renderer.bounds);
                        camera.orthographicSize = Mathf.Max(bounds.extents.y, Mathf.Max(bounds.extents.x, bounds.extents.z) * Height / Width) * 1.15f;
                        camera.transform.position = bounds.center + Vector3.back * (bounds.size.magnitude + 5f);
                        camera.transform.LookAt(bounds.center);
                        LODGroup group = model.GetComponentInChildren<LODGroup>();
                        for (int row = 0; row < group.lodCount; row++)
                        {
                            group.ForceLOD(row);
                            camera.Render();
                            RenderTexture.active = target;
                            tile.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                            tile.Apply();
                            Color32[] pixels = tile.GetPixels32();
                            Color32 background = pixels[0];
                            int visible = 0, magenta = 0;
                            foreach (Color32 p in pixels)
                            {
                                if (Math.Abs(p.r - background.r) + Math.Abs(p.g - background.g) + Math.Abs(p.b - background.b) > 24) visible++;
                                if (p.r > 220 && p.g < 40 && p.b > 220) magenta++;
                            }
                            if (visible < 100 || magenta > 10)
                                throw new InvalidOperationException($"Render failed: {model.name} LOD {row}, visible={visible}, magenta={magenta}");
                            sheet.SetPixels32(column * Width, (rows - row - 1) * Height, Width, Height, pixels);
                            report.AppendLine($"RENDER: {Path.GetFileNameWithoutExtension(paths[column])} LOD{row}, visible={visible}, magenta={magenta}");
                        }
                    }
                    finally { Object.DestroyImmediate(model); }
                }
                sheet.Apply();
                File.WriteAllBytes(Output + "/DryTreesUrpPreview.png", sheet.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                EditorSceneManager.ClosePreviewScene(scene);
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(tile);
                Object.DestroyImmediate(sheet);
            }
        }
    }
}

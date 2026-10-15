using System;
using System.IO;
using System.Linq;
using System.Text;
using Lightbringer.Visuals;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Lightbringer.EditorTools
{
    // Photoscanned (CC0, Poly Haven) ground textures and props for Act 2: configures the texture imports, wraps each glTF
    // model into a real-scale ground-pivot prefab and fills the Act Two profile's scatter list.
    [InitializeOnLoad]
    internal static class ActTwoPhotoSetup
    {
        private const string Output = "Docs/Diagnostics/ActTwoPhotoChecks.txt";
        private const string RequestPath = "Docs/Diagnostics/act-two-photo.request";
        private static readonly string LoadedScriptStamp = GreyboxScriptAutoRefresh.GetStamp();

        static ActTwoPhotoSetup() => EditorRequests.Register(RequestPath, () =>
        {
            if (LoadedScriptStamp != GreyboxScriptAutoRefresh.GetStamp())
            {
                File.WriteAllText(RequestPath, "Build after script refresh");
                AssetDatabase.Refresh();
                return;
            }
            Run();
        });

        [MenuItem("Lightbringer/Art/Build Act Two Photoscans")]
        public static void Run()
        {
            var report = new StringBuilder("Act Two Photoscans | " + DateTime.UtcNow.ToString("O") + "\n");
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                ConfigureTextures();
                var profile = AssetDatabase.LoadAssetAtPath<ActTwoEnvironmentProfile>("Assets/Art/Environment/ActTwo/ActTwoEnvironment.asset");
                if (profile == null) throw new InvalidOperationException("Missing ActTwoEnvironment.asset");
                var ground = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Environment/ActTwo/Materials/AshlandGround.mat");
                if (ground == null) throw new InvalidOperationException("Missing AshlandGround.mat");
                ApplyGround(ground, report);
                EditorUtility.SetDirty(ground);
                BuildScatter(profile, report);
                BuildStructureMaterials(profile, report);
                AssetDatabase.SaveAssets();
                report.AppendLine("PASS: complete");
                Debug.Log("Act Two photoscans complete. " + Output);
            }
            catch (Exception error) { report.AppendLine("FAIL: " + error); throw; }
            finally { Directory.CreateDirectory(Path.GetDirectoryName(Output)); File.WriteAllText(Output, report.ToString()); }
        }

        private const string Root = "Assets/Art/Environment/ActTwo/Photo";

        private static readonly (string name, ActTwoScatterKind kind)[] Models =
        {
            // rock_face_01 is excluded: it is an open-backed cliff facade, so freestanding copies showed a single face.
            // namaqualand_boulder_02 is closed apart from its base, which every placement sinks into the ground.
            ("namaqualand_boulder_02", ActTwoScatterKind.Cliff),
            ("boulder_01", ActTwoScatterKind.Boulder),
            ("rock_moss_set_01", ActTwoScatterKind.Boulder),
            ("dead_tree_trunk", ActTwoScatterKind.Log),
            ("tree_stump_01", ActTwoScatterKind.Stump),
            ("dry_branches_medium_01", ActTwoScatterKind.Brush),
            ("wooden_crate_01", ActTwoScatterKind.Crate),
            ("wooden_crate_02", ActTwoScatterKind.Crate),
            ("wooden_barrels_01", ActTwoScatterKind.Barrel),
            // Modern metal drums are excluded from this fantasy setting.
            ("wooden_bucket_01", ActTwoScatterKind.Bucket),
            ("kite_shield", ActTwoScatterKind.Shield),
            ("stone_fire_pit", ActTwoScatterKind.FirePit),
            ("large_castle_door", ActTwoScatterKind.Gate),
            ("gothic_statue", ActTwoScatterKind.Statue),
        };

        public static void ConfigureTextures()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root + "/Textures" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                bool normal = path.EndsWith("_nor.jpg", StringComparison.OrdinalIgnoreCase);
                importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.maxTextureSize = 2048;
                importer.mipmapEnabled = true;
                importer.anisoLevel = 8;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.SaveAndReimport();
            }
        }

        // Replaces the ground layers of the ashland terrain material with the scanned textures.
        public static void ApplyGround(Material ground, StringBuilder report)
        {
            Texture2D Load(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/" + name + ".jpg");
            Texture2D ashDiff = Load("burned_ground_01_diff"), ashNor = Load("burned_ground_01_nor");
            Texture2D mudDiff = Load("brown_mud_dry_diff"), mudNor = Load("brown_mud_dry_nor");
            Texture2D stoneDiff = Load("monastery_stone_floor_diff"), stoneNor = Load("monastery_stone_floor_nor");
            if (ashDiff == null || mudDiff == null || stoneDiff == null || ashNor == null || mudNor == null || stoneNor == null)
                throw new FileNotFoundException("Missing Poly Haven ground textures under " + Root + "/Textures");
            ground.SetTexture("_ForestGrassTex", ashDiff);
            ground.SetTexture("_ForestGrassNormal", ashNor);
            ground.SetTexture("_ForestSoilTex", mudDiff);
            ground.SetTexture("_ForestSoilNormal", mudNor);
            ground.SetTexture("_StoneTex", stoneDiff);
            ground.SetTexture("_StoneNormal", stoneNor);
            ground.SetTexture("_StoneMask", Texture2D.whiteTexture);
            ground.SetFloat("_ForestTextureScale", .42f);
            ground.SetFloat("_StoneTextureScale", .3f);
            ground.SetFloat("_ForestTextureStrength", .95f);
            ground.SetFloat("_NaturalLight", 1);
            ground.SetFloat("_ForestNormalStrength", 1.1f);
            report.AppendLine("PASS: Ashland ground uses Poly Haven burned_ground_01, brown_mud_dry and monastery_stone_floor (CC0).");
        }

        public static void BuildScatter(ActTwoEnvironmentProfile profile, StringBuilder report)
        {
            if (!AssetDatabase.IsValidFolder(Root + "/Prefabs"))
                AssetDatabase.CreateFolder(Root, "Prefabs");
            var assets = new System.Collections.Generic.List<ActTwoScatterAsset>();
            foreach (var model in Models)
            {
                string source = $"{Root}/Models/{model.name}/{model.name}.gltf";
                var imported = AssetDatabase.LoadAssetAtPath<GameObject>(source);
                if (imported == null) { report.AppendLine("SKIP: not imported yet " + source); continue; }
                if (!HasTextures(imported))
                {
                    // A glTF imported before its jpgs finished downloading keeps untextured (white) materials.
                    AssetDatabase.ImportAsset(source, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                    imported = AssetDatabase.LoadAssetAtPath<GameObject>(source);
                    report.AppendLine((HasTextures(imported) ? "PASS: reimported untextured " : "WARN: still untextured ") + source);
                }
                var root = new GameObject(model.name);
                try
                {
                    GameObject instance = Object.Instantiate(imported, root.transform, false);
                    instance.name = model.name;
                    foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
                    Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
                    if (renderers.Length == 0) throw new InvalidOperationException("No renderers in " + source);
                    Bounds bounds = renderers[0].bounds;
                    foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
                    // Ground pivot at the bottom centre; models stay at their real-world size.
                    instance.transform.localPosition = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
                    foreach (Renderer renderer in renderers)
                    {
                        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                        renderer.receiveShadows = true;
                    }
                    int part = 0;
                    foreach (MeshFilter filter in instance.GetComponentsInChildren<MeshFilter>(true))
                        filter.sharedMesh = WithMeshLods(filter.sharedMesh, $"Scan_{model.name}_{part++}", report);
                    GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{Root}/Prefabs/{model.name}.prefab");
                    float radius = new Vector2(bounds.extents.x, bounds.extents.z).magnitude;
                    assets.Add(new ActTwoScatterAsset { kind = model.kind, prefab = prefab, radius = radius,
                        length = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z) });
                    report.AppendLine($"PASS: {model.name} -> {model.kind}, size={bounds.size}, radius={radius:F2}");
                }
                finally { Object.DestroyImmediate(root); }
            }
            profile.scatter = assets.ToArray();
            EditorUtility.SetDirty(profile);
        }

        private static bool HasTextures(GameObject model)
        {
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
                foreach (Material material in renderer.sharedMaterials)
                    if (material != null && material.GetTexturePropertyNameIDs().Any(id => material.GetTexture(id) != null))
                        return true;
            return false;
        }

        // URP Lit materials for TexturedShapes structures; UVs are in metres, so tiling = 1 / texture width in metres.
        public static void BuildStructureMaterials(ActTwoEnvironmentProfile profile, StringBuilder report)
        {
            if (!AssetDatabase.IsValidFolder(Root + "/Materials")) AssetDatabase.CreateFolder(Root, "Materials");
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            Material Make(string name, string texture, float metres, Color tint, float smoothness, float bump)
            {
                string path = $"{Root}/Materials/{name}.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null) { material = new Material(lit); AssetDatabase.CreateAsset(material, path); }
                material.shader = lit;
                if (texture != null)
                {
                    var diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Root}/Textures/{texture}_diff.jpg");
                    var normal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Root}/Textures/{texture}_nor.jpg");
                    if (diffuse == null || normal == null) throw new FileNotFoundException("Missing Poly Haven texture " + texture);
                    material.SetTexture("_BaseMap", diffuse);
                    material.SetTexture("_BumpMap", normal);
                    material.SetTextureScale("_BaseMap", Vector2.one / metres);
                    material.SetFloat("_BumpScale", bump);
                    material.EnableKeyword("_NORMALMAP");
                }
                material.SetColor("_BaseColor", tint);
                material.SetFloat("_Metallic", 0);
                material.SetFloat("_Smoothness", smoothness);
                EditorUtility.SetDirty(material);
                report.AppendLine($"PASS: {name} <- {texture ?? "emission"} ({metres} m tile)");
                return material;
            }
            profile.castleStone = Make("PhotoCastleStone", "castle_wall_slates", 2.6f, new Color(.78f, .78f, .8f), .14f, 1.3f);
            profile.roofSlate = Make("PhotoRoofSlate", "roof_slates_02", 1.8f, new Color(.5f, .52f, .56f), .3f, 1.1f);
            profile.timber = Make("PhotoWeatheredPlanks", "weathered_planks", 1.6f, new Color(.86f, .84f, .8f), .1f, 1);
            profile.bark = Make("PhotoBark", "bark_brown_02", 1.2f, new Color(.7f, .68f, .64f), .08f, 1.2f);
            // Unlit HDR colour: needs no shader keyword (URP's material import cleared Lit's _EMISSION), still fogs and blooms.
            const string glowPath = Root + "/Materials/PhotoWindowGlow.mat";
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            var glow = AssetDatabase.LoadAssetAtPath<Material>(glowPath);
            if (glow == null) { glow = new Material(unlit); AssetDatabase.CreateAsset(glow, glowPath); }
            glow.shader = unlit;
            glow.shaderKeywords = new string[0];
            glow.SetColor("_BaseColor", new Color(.92f, .38f, .12f) * 1.15f);
            EditorUtility.SetDirty(glow);
            report.AppendLine("PASS: PhotoWindowGlow <- Unlit HDR firelight");
            profile.windowGlow = glow;
            EditorUtility.SetDirty(profile);
        }

        // Scans are 20k-180k triangles. A saved copy gets Unity's built-in Mesh LOD chain so distant rocks
        // render a fraction of that. Called by reflection: the API is new in Unity 6.2 and its overloads vary.
        private static Mesh WithMeshLods(Mesh source, string name, StringBuilder report)
        {
            if (!AssetDatabase.IsValidFolder(Root + "/Meshes")) AssetDatabase.CreateFolder(Root, "Meshes");
            string path = $"{Root}/Meshes/{name}.asset";
            Mesh copy = Object.Instantiate(source);
            copy.name = name;
            var utility = Type.GetType("UnityEditor.MeshLodUtility, UnityEditor.CoreModule")
                ?? Type.GetType("UnityEditor.MeshLodUtility, UnityEditor");
            var method = utility?.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .Where(m => m.Name == "GenerateMeshLods" && m.GetParameters().Length > 0
                    && m.GetParameters()[0].ParameterType == typeof(Mesh))
                .OrderBy(m => m.GetParameters().Length).FirstOrDefault();
            if (method == null) report.AppendLine("WARN: MeshLodUtility.GenerateMeshLods not found; " + name + " keeps full detail.");
            else
            {
                object[] arguments = method.GetParameters().Select(p => p.HasDefaultValue ? p.DefaultValue
                    : p.ParameterType == typeof(int) ? (object)8
                    : p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null).ToArray();
                arguments[0] = copy;
                method.Invoke(null, arguments);
                report.AppendLine($"PASS: {name} Mesh LODs via {method}; lods={copy.lodCount}, lod0 triangles={copy.GetIndexCount(0) / 3}");
            }
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) { AssetDatabase.CreateAsset(copy, path); return copy; }
            EditorUtility.CopySerialized(copy, existing);
            Object.DestroyImmediate(copy);
            return existing;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Lightbringer.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using static Lightbringer.EditorTools.RigBuildUtility;

namespace Lightbringer.EditorTools
{
    // Tripo exports of the Shieldbearer, Spearman, Mage, Priest, mounted Knight and Dragon. The downloaded FBX files stay
    // untouched. Each model is placed (upright, facing +Z, sized) and then rigged by its *RigSetup; the Knight is
    // first assembled into Knight_Mounted.prefab (horse FBX plus the rider mesh baked into a seated pose on the
    // saddle), which KnightRigSetup then rigs as horse and rider.
    [InitializeOnLoad]
    public static class UnitModelSetup
    {
        private const string Root = "Assets/Art/Characters";
        private const string Request = "Docs/Validation/UnitModels.request";
        private const string ReportPath = "Docs/Validation/UnitModelImportChecks.txt";
        private const string MountedFolder = Root + "/Knight/Mounted";
        private const string SeatedMeshPath = MountedFolder + "/Knight_Seated.asset";
        private const string MountedPrefabPath = MountedFolder + "/Knight_Mounted.prefab";
        private const float ShieldbearerHeight = 1.7f;
        private const float SpearmanHeight = 1.7f;
        private const float CasterHeight = 1.7f;
        private const float HorseHeight = 2.1f;
        private const float RiderHeight = 1.65f;
        private const float DragonLength = 4f;
        private const float FeetOffset = -0.8f;
        // Tripo exports have their feet-to-head axis along +Z, not Unity's +Y.
        private static readonly Quaternion Upright = Quaternion.Euler(-90f, 0f, 0f);

        // Uprighted (unscaled) model space -> unit space: feet centre on the pivot, facing +Z.
        private struct Placement
        {
            public float Yaw, Scale;
            public Vector3 Offset;
            public Vector3 Euler => new Vector3(-90f, Yaw, 0f);
            public Vector3 Apply(Vector3 upright) => Offset + Quaternion.Euler(0f, Yaw, 0f) * upright * Scale;
            // FBX root -> unit space, for rig builders that bake the placed mesh.
            public Matrix4x4 ToUnit => Matrix4x4.TRS(Offset, Quaternion.Euler(Euler), Vector3.one * Scale);
        }

        static UnitModelSetup() => EditorRequests.Register(Request, AssignAll);

        [MenuItem("Lightbringer/Art/Assign and Rig Tripo Unit Models")]
        public static void AssignAll()
        {
            var library = AssetDatabase.LoadAssetAtPath<ArtStyleLibrary>(ArtStyleSetup.LibraryPath);
            if (library == null) throw new InvalidOperationException("Art style library is missing.");
            var report = new StringBuilder();

            GameObject shield = PrepareSource("Shieldbearer");
            Placement shieldPlace = PlaceHumanoid(UprightPoints(shield), ShieldbearerHeight);
            SetOverride(library, ShieldbearerRigSetup.Build(shield, shieldPlace.ToUnit, library));
            report.AppendLine($"Shieldbearer: {Describe(shieldPlace)}; rigged with a tower shield and mace (see ShieldbearerRigChecks.txt)");

            GameObject spear = PrepareSource("Spearman");
            Placement spearPlace = PlaceHumanoid(UprightPoints(spear), SpearmanHeight);
            SetOverride(library, SpearmanRigSetup.Build(spear, spearPlace.ToUnit, library));
            report.AppendLine($"Spearman: {Describe(spearPlace)}; rigged with a hand-held spear (see SpearmanRigChecks.txt)");

            GameObject mage = PrepareSource("Mage");
            Placement magePlace = PlaceHumanoid(UprightPoints(mage), CasterHeight);
            SetOverride(library, CasterRigSetup.BuildMage(mage, magePlace.ToUnit));
            report.AppendLine($"Mage: {Describe(magePlace)}; rigged with a rune-crystal staff (see MageRigChecks.txt)");

            GameObject priest = PrepareSource("Priest");
            Placement priestPlace = PlaceHumanoid(UprightPoints(priest), CasterHeight);
            SetOverride(library, CasterRigSetup.BuildPriest(priest, priestPlace.ToUnit));
            report.AppendLine($"Priest: {Describe(priestPlace)}; rigged with a holy staff (see PriestRigChecks.txt)");

            GameObject dragon = PrepareSource("Dragon");
            Placement dragonPlace = PlaceQuadruped(UprightPoints(dragon), DragonLength, true, out string dragonNote);
            SetOverride(library, DragonRigSetup.Build(dragon, dragonPlace.ToUnit, library));
            report.AppendLine($"Dragon: {Describe(dragonPlace)}; {dragonNote}; rigged (see DragonRigChecks.txt)");

            GameObject horse = PrepareSource("KnightHorse");
            GameObject rider = PrepareSource("Knight");
            GameObject mounted = BuildMounted(horse, rider, report);
            SetOverride(library, KnightRigSetup.Build(mounted, library));
            report.AppendLine("Knight: horse and rider rigged with a lance and kite shield (see KnightRigChecks.txt)");

            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            Validate(library, report);
        }

        [MenuItem("Lightbringer/Art/Assign and Rig Tripo Unit Models", true)]
        private static bool CanAssign() => !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling;

        private static string Describe(Placement p) => $"yaw {p.Yaw:0}, scale {p.Scale:0.####}, offset {p.Offset}";

        // ---------- Source import: URP Lit material from the Tripo PBR maps, remapped onto the FBX ----------

        private static GameObject PrepareSource(string name)
        {
            string folder = Root + "/" + name;
            string modelPath = folder + "/" + name + ".fbx";
            AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceSynchronousImport);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null) throw new InvalidOperationException(modelPath + " was not imported.");
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader is missing.");

            string maps = folder + "/" + name + ".fbm";
            Texture2D color = ImportTexture(MapPath(maps, "basecolor"), false);
            Texture2D normal = ImportTexture(MapPath(maps, "normal"), true);
            string packedPath = folder + "/" + name + "_MetallicSmoothness.png";
            PackMetallicSmoothness(MapPath(maps, "metallic"), MapPath(maps, "roughness"), packedPath);

            string materialPath = folder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.shader = shader;
            material.SetTexture("_BaseMap", color);
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BumpMap", normal);
            material.SetFloat("_BumpScale", 1f);
            material.EnableKeyword("_NORMALMAP");
            material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(packedPath));
            material.SetFloat("_Metallic", 1f);
            material.SetFloat("_Smoothness", 1f);
            material.SetFloat("_SmoothnessTextureChannel", 0f);
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            EditorUtility.SetDirty(material);

            var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
            foreach (Material source in model.GetComponentsInChildren<Renderer>(true)
                .SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct())
            {
                if (source != material)
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(source), material);
            }
            importer.importCameras = false;
            importer.importLights = false;
            // No motion clips in these exports; an idle Animator would suppress the bob/lean fallback.
            importer.animationType = ModelImporterAnimationType.None;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        }

        private static string MapPath(string folder, string map)
        {
            string root = Path.GetDirectoryName(Application.dataPath);
            string file = Directory.GetFiles(Path.Combine(root, folder))
                .Where(p => !p.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault(p => Path.GetFileNameWithoutExtension(p).EndsWith("_" + map, StringComparison.OrdinalIgnoreCase));
            if (file == null) throw new FileNotFoundException($"No *_{map} texture in {folder}.");
            return folder + "/" + Path.GetFileName(file);
        }

        private static Texture2D ImportTexture(string path, bool normal)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) throw new FileNotFoundException(path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normal;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // URP Lit: metallic in R, smoothness (1 - roughness) in A.
        private static void PackMetallicSmoothness(string metallicPath, string roughnessPath, string packedPath)
        {
            var metallic = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            var roughness = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            Texture2D packed = null;
            try
            {
                if (!metallic.LoadImage(File.ReadAllBytes(metallicPath)) || !roughness.LoadImage(File.ReadAllBytes(roughnessPath)))
                    throw new InvalidOperationException("Could not decode surface textures for " + packedPath);
                if (metallic.width != roughness.width || metallic.height != roughness.height)
                    throw new InvalidOperationException("Metallic/roughness texture dimensions differ for " + packedPath);
                Color32[] pixels = metallic.GetPixels32();
                Color32[] rough = roughness.GetPixels32();
                for (int i = 0; i < pixels.Length; i++)
                    pixels[i] = new Color32(pixels[i].r, 0, 0, (byte)(255 - rough[i].r));
                packed = new Texture2D(metallic.width, metallic.height, TextureFormat.RGBA32, false, true);
                packed.SetPixels32(pixels);
                packed.Apply();
                File.WriteAllBytes(packedPath, packed.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(metallic);
                UnityEngine.Object.DestroyImmediate(roughness);
                if (packed != null) UnityEngine.Object.DestroyImmediate(packed);
            }
            AssetDatabase.ImportAsset(packedPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(packedPath);
            importer.sRGBTexture = false;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
            importer.SaveAndReimport();
        }

        // ---------- Measuring and orienting ----------

        // Combines every mesh of the model in uprighted space (Tripo axis corrected, scale 1).
        private static Mesh UprightMesh(GameObject model)
        {
            Scene preview = EditorSceneManager.NewPreviewScene();
            var probe = UnityEngine.Object.Instantiate(model);
            SceneManager.MoveGameObjectToScene(probe, preview);
            try
            {
                probe.transform.SetPositionAndRotation(Vector3.zero, Upright);
                probe.transform.localScale = Vector3.one;
                CombineInstance[] parts = probe.GetComponentsInChildren<MeshFilter>(true)
                    .Where(f => f.sharedMesh != null)
                    .SelectMany(f => Enumerable.Range(0, f.sharedMesh.subMeshCount).Select(s => new CombineInstance
                        { mesh = f.sharedMesh, subMeshIndex = s, transform = f.transform.localToWorldMatrix }))
                    .ToArray();
                if (parts.Length == 0) throw new InvalidOperationException(model.name + " has no static mesh.");
                var mesh = new Mesh { name = model.name, indexFormat = IndexFormat.UInt32 };
                mesh.CombineMeshes(parts, true, true);
                return mesh;
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        private static Vector3[] UprightPoints(GameObject model)
        {
            Mesh mesh = UprightMesh(model);
            Vector3[] points = mesh.vertices;
            UnityEngine.Object.DestroyImmediate(mesh);
            return points;
        }

        private static Bounds BoundsOf(IEnumerable<Vector3> points)
        {
            var bounds = new Bounds(points.First(), Vector3.zero);
            foreach (Vector3 p in points) bounds.Encapsulate(p);
            return bounds;
        }

        // Front-view humanoid: only front or back is possible. Toes reach further from the body than heels.
        private static Placement PlaceHumanoid(Vector3[] points, float height)
        {
            Bounds bounds = BoundsOf(points);
            if (bounds.size.y <= Mathf.Max(bounds.size.x, bounds.size.z))
                throw new InvalidOperationException("Corrected upright axis is not the humanoid's tallest axis.");
            float floor = bounds.min.y + bounds.size.y * 0.03f;
            float front = float.MinValue, back = float.MaxValue;
            foreach (Vector3 p in points)
                if (p.y <= floor) { front = Mathf.Max(front, p.z); back = Mathf.Min(back, p.z); }
            float yaw = front - bounds.center.z >= bounds.center.z - back ? 0f : 180f;
            return Centre(points, yaw, height / bounds.size.y);
        }

        // Body axis from the low part (legs, belly, tail) so raised wings and necks do not skew it; the head
        // end is the one that rises higher. `byLength` sizes by nose-to-tail length instead of height.
        private static Placement PlaceQuadruped(Vector3[] points, float size, bool byLength, out string note)
        {
            Bounds bounds = BoundsOf(points);
            float lowY = bounds.min.y + bounds.size.y * 0.35f;
            Vector2 mean = Vector2.zero;
            int count = 0;
            foreach (Vector3 p in points)
                if (p.y <= lowY) { mean += new Vector2(p.x, p.z); count++; }
            mean /= Mathf.Max(count, 1);
            float sxx = 0f, szz = 0f, sxz = 0f;
            foreach (Vector3 p in points)
            {
                if (p.y > lowY) continue;
                float dx = p.x - mean.x, dz = p.z - mean.y;
                sxx += dx * dx; szz += dz * dz; sxz += dx * dz;
            }
            float angle = 0.5f * Mathf.Atan2(2f * sxz, sxx - szz);
            Vector2 axis = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            float min = float.MaxValue, max = float.MinValue;
            foreach (Vector3 p in points)
            {
                float t = Vector2.Dot(new Vector2(p.x, p.z) - mean, axis);
                min = Mathf.Min(min, t); max = Mathf.Max(max, t);
            }
            float band = (max - min) * 0.15f, plusTop = float.MinValue, minusTop = float.MinValue;
            foreach (Vector3 p in points)
            {
                float t = Vector2.Dot(new Vector2(p.x, p.z) - mean, axis);
                if (t > max - band) plusTop = Mathf.Max(plusTop, p.y);
                if (t < min + band) minusTop = Mathf.Max(minusTop, p.y);
            }
            if (minusTop > plusTop) axis = -axis;
            note = $"head end top {Mathf.Max(plusTop, minusTop):0.###} vs tail end {Mathf.Min(plusTop, minusTop):0.###}";
            float scale = size / (byLength ? max - min : bounds.size.y);
            // A curled tail skews the low-vertex axis; four planted feet give the true heading and body centre.
            Vector2? centre = null;
            if (TryFeet(points, bounds, axis, out Vector2[] feet))
            {
                Vector2 front = (feet[0] + feet[1]) * 0.5f, hind = (feet[2] + feet[3]) * 0.5f;
                axis = (front - hind).normalized;
                centre = (front + hind) * 0.5f;
                note += "; heading from 4 feet";
            }
            else note += $"; heading from {count} low vertices";
            float yaw = -Mathf.Atan2(axis.x, axis.y) * Mathf.Rad2Deg;
            return Centre(points, yaw, scale, centre);
        }

        // Feet are compact clusters of the lowest vertices; long low shapes (a tail on the ground) are skipped.
        // Returns the cluster centres front pair first, along `roughAxis`.
        private static bool TryFeet(Vector3[] points, Bounds bounds, Vector2 roughAxis, out Vector2[] feet)
        {
            float floor = bounds.min.y + bounds.size.y * 0.04f, cell = bounds.size.y * 0.04f;
            var cells = new Dictionary<Vector2Int, List<Vector2>>();
            foreach (Vector3 p in points)
            {
                if (p.y > floor) continue;
                var key = new Vector2Int(Mathf.FloorToInt(p.x / cell), Mathf.FloorToInt(p.z / cell));
                if (!cells.TryGetValue(key, out List<Vector2> list)) cells[key] = list = new List<Vector2>();
                list.Add(new Vector2(p.x, p.z));
            }
            var clusters = new List<List<Vector2>>();
            var seen = new HashSet<Vector2Int>();
            foreach (Vector2Int start in cells.Keys)
            {
                if (!seen.Add(start)) continue;
                var cluster = new List<Vector2>();
                var open = new Stack<Vector2Int>();
                open.Push(start);
                while (open.Count > 0)
                {
                    Vector2Int c = open.Pop();
                    cluster.AddRange(cells[c]);
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dz = -1; dz <= 1; dz++)
                        {
                            var n = new Vector2Int(c.x + dx, c.y + dz);
                            if (cells.ContainsKey(n) && seen.Add(n)) open.Push(n);
                        }
                }
                Vector2 lo = cluster.Aggregate(Vector2.Min), hi = cluster.Aggregate(Vector2.Max);
                if (Mathf.Max(hi.x - lo.x, hi.y - lo.y) < bounds.size.y * 0.25f) clusters.Add(cluster);
            }
            feet = clusters.OrderByDescending(c => c.Count).Take(4)
                .Select(c => c.Aggregate(Vector2.zero, (sum, p) => sum + p) / c.Count)
                .OrderByDescending(f => Vector2.Dot(f, roughAxis)).ToArray();
            return feet.Length == 4;
        }

        private static Placement Centre(Vector3[] points, float yaw, float scale, Vector2? footCentre = null)
        {
            var place = new Placement { Yaw = yaw, Scale = scale };
            Bounds placed = BoundsOf(points.Select(place.Apply));
            Vector3 centre = placed.center;
            if (footCentre.HasValue) centre = place.Apply(new Vector3(footCentre.Value.x, 0f, footCentre.Value.y));
            place.Offset = -new Vector3(centre.x, placed.min.y, centre.z);
            return place;
        }

        private static void SetOverride(ArtStyleLibrary library, VisualOverride entry)
        {
            Undo.RecordObject(library, "Assign " + entry.id + " model");
            List<VisualOverride> list = (library.overrides ?? new VisualOverride[0]).Where(o => o != null && o.id != entry.id).ToList();
            list.Add(entry);
            library.overrides = list.ToArray();
            EditorUtility.SetDirty(library);
        }

        // ---------- Mounted Knight ----------

        private static GameObject BuildMounted(GameObject horseModel, GameObject riderModel, StringBuilder report)
        {
            if (!AssetDatabase.IsValidFolder(MountedFolder)) AssetDatabase.CreateFolder(Root + "/Knight", "Mounted");
            Vector3[] horseUpright = UprightPoints(horseModel);
            Placement horsePlace = PlaceQuadruped(horseUpright, HorseHeight, false, out string horseNote);
            Vector3[] horse = horseUpright.Select(horsePlace.Apply).ToArray();
            Vector3 seat = FindSeat(horse, out float barrelHalfWidth);

            Mesh rider = UprightMesh(riderModel);
            Vector3[] riderUpright = rider.vertices;
            Placement riderPlace = PlaceHumanoid(riderUpright, RiderHeight);
            Seat(rider, riderPlace, barrelHalfWidth, out Vector3 hip, out float contactY, out float spread);
            rider = SaveAsset(rider, SeatedMeshPath);
            Vector3 riderPosition = new Vector3(seat.x, seat.y - contactY - 0.03f, seat.z - hip.z);

            Scene preview = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Knight_Mounted");
            SceneManager.MoveGameObjectToScene(root, preview);
            try
            {
                var horseInstance = (GameObject)PrefabUtility.InstantiatePrefab(horseModel, preview);
                horseInstance.name = "Horse";
                horseInstance.transform.SetParent(root.transform, false);
                horseInstance.transform.localPosition = horsePlace.Offset;
                horseInstance.transform.localRotation = Quaternion.Euler(horsePlace.Euler);
                horseInstance.transform.localScale = Vector3.one * horsePlace.Scale;

                var riderObject = new GameObject("Rider");
                riderObject.transform.SetParent(root.transform, false);
                riderObject.transform.localPosition = riderPosition;
                riderObject.AddComponent<MeshFilter>().sharedMesh = rider;
                var renderer = riderObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Knight/Knight.mat");
                renderer.shadowCastingMode = ShadowCastingMode.On;

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, MountedPrefabPath);
                if (prefab == null) throw new InvalidOperationException("Could not save the mounted Knight prefab.");
                report.AppendLine($"Knight horse: {Describe(horsePlace)}; {horseNote}");
                report.AppendLine($"Knight rider: {Describe(riderPlace)}; seated hip {hip}, leg spread {spread:0.###} m, "
                    + $"seat {seat}, barrel half-width {barrelHalfWidth:0.###} m, rider offset {riderPosition}");
                return prefab;
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        // Saddle seat: the dip in the back's top line between the cantle and the pommel, a little forward of
        // the body centre. Hooves locate the body; front legs are on +Z after placement.
        private static Vector3 FindSeat(Vector3[] horse, out float barrelHalfWidth)
        {
            Bounds bounds = BoundsOf(horse);
            float hoofY = bounds.min.y + bounds.size.y * 0.12f;
            List<float> hooves = horse.Where(p => p.y <= hoofY).Select(p => p.z).OrderBy(z => z).ToList();
            float hind = hooves[hooves.Count / 10], fore = hooves[hooves.Count * 9 / 10];
            float span = fore - hind;
            const float slice = 0.04f;
            Vector3 seat = new Vector3(0f, float.MaxValue, 0f);
            for (float z = hind + span * 0.45f; z <= hind + span * 0.85f; z += slice * 0.5f)
            {
                float top = float.MinValue;
                foreach (Vector3 p in horse)
                    if (Mathf.Abs(p.z - z) < slice && Mathf.Abs(p.x - bounds.center.x) < 0.07f) top = Mathf.Max(top, p.y);
                if (top > float.MinValue && top < seat.y) seat = new Vector3(bounds.center.x, top, z);
            }
            if (seat.y == float.MaxValue) throw new InvalidOperationException("Could not find the horse's saddle.");
            // Width the rider's knees must clear: the barrel under the saddle, ignoring dangling straps.
            List<float> widths = horse.Where(p => Mathf.Abs(p.z - seat.z) < 0.25f && p.y < seat.y - 0.15f && p.y > seat.y - 0.6f)
                .Select(p => Mathf.Abs(p.x - bounds.center.x)).OrderBy(x => x).ToList();
            barrelHalfWidth = widths.Count > 0 ? widths[widths.Count * 85 / 100] : 0.3f;
            return seat;
        }

        // Bakes the rider into unit space and bends the legs at hip and knee: thighs swing forward and splay
        // around the horse, shins hang down. The A-posed hands beside the hips are masked out of the bend.
        private static void Seat(Mesh mesh, Placement place, float barrelHalfWidth, out Vector3 hip, out float contactY, out float spread)
        {
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            Vector4[] tangents = mesh.tangents;
            Quaternion turn = Quaternion.Euler(0f, place.Yaw, 0f);
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = place.Apply(vertices[i]);
                if (normals.Length > 0) normals[i] = turn * normals[i];
                if (tangents.Length > 0) { Vector3 t = turn * (Vector3)tangents[i]; tangents[i] = new Vector4(t.x, t.y, t.z, tangents[i].w); }
            }

            const float h = RiderHeight;
            float hipY = 0.47f * h, kneeY = 0.3f * h, blend = 0.03f * h;
            float hipZ = vertices.Where(v => Mathf.Abs(v.y - hipY) < blend && Mathf.Abs(v.x) < 0.12f * h).Select(v => v.z).DefaultIfEmpty(0f).Average();
            float legX = vertices.Where(v => Mathf.Abs(v.y - kneeY) < blend && Mathf.Abs(v.x) < 0.15f * h && Mathf.Abs(v.x) > 0.02f)
                .Select(v => Mathf.Abs(v.x)).DefaultIfEmpty(0.1f).Average();
            spread = Mathf.Max(0f, barrelHalfWidth + 0.06f - legX);
            hip = new Vector3(0f, hipY, hipZ);
            Quaternion thigh = Quaternion.AngleAxis(-60f, Vector3.right);
            Quaternion shin = Quaternion.AngleAxis(10f, Vector3.right);
            Vector3 knee = new Vector3(0f, kneeY, hipZ);
            Vector3 kneeSeated = hip + thigh * (knee - hip);

            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 p = vertices[i];
                float bend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(hipY + blend, hipY - blend, p.y));
                float hand = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.38f * h, 0.42f * h, p.y))
                    * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.19f * h, 0.22f * h, Mathf.Abs(p.x)));
                bend *= 1f - hand;
                if (bend <= 0f) continue;
                float side = Mathf.Clamp(p.x / (0.05f * h), -1f, 1f);
                float along = Mathf.Clamp01((hipY - p.y) / (hipY - kneeY));
                Vector3 onThigh = hip + thigh * (p - hip) + Vector3.right * (side * spread * along);
                Vector3 onShin = kneeSeated + shin * (p - knee) + Vector3.right * (side * spread);
                float lower = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(kneeY + blend, kneeY - blend, p.y));
                vertices[i] = Vector3.Lerp(p, Vector3.Lerp(onThigh, onShin, lower), bend);
                Quaternion rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.Slerp(thigh, shin, lower), bend);
                if (normals.Length > 0) normals[i] = rotation * normals[i];
                if (tangents.Length > 0) { Vector3 t = rotation * (Vector3)tangents[i]; tangents[i] = new Vector4(t.x, t.y, t.z, tangents[i].w); }
            }
            // The lowest point under the hip is what rests on the saddle.
            contactY = vertices.Where(v => Mathf.Abs(v.x) < 0.08f && Mathf.Abs(v.z - hipZ) < 0.08f).Select(v => v.y).DefaultIfEmpty(hipY - 0.1f).Min();
            mesh.vertices = vertices;
            if (normals.Length > 0) mesh.normals = normals;
            if (tangents.Length > 0) mesh.tangents = tangents;
            mesh.name = "Knight_Seated";
            mesh.RecalculateBounds();
        }

        // ---------- Validation ----------

        private static void Validate(ArtStyleLibrary library, StringBuilder details)
        {
            var report = new StringBuilder($"PASS | {DateTime.UtcNow:O}\n");
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                foreach (VisualId id in new[] { VisualId.Shieldbearer, VisualId.Spearman, VisualId.Mage, VisualId.Priest, VisualId.Knight, VisualId.Dragon })
                {
                    var owner = new GameObject(id + " validation");
                    SceneManager.MoveGameObjectToScene(owner, preview);
                    Transform visual = UnitAppearance.Attach(owner, id, library, FeetOffset);
                    if (visual == null) throw new InvalidOperationException(id + " visual was not attached.");
                    Renderer[] renderers = visual.GetComponentsInChildren<Renderer>();
                    if (renderers.Length == 0) throw new InvalidOperationException(id + " has no renderer.");
                    Bounds bounds = MeshBounds(renderers[0]);
                    int vertices = 0;
                    foreach (Renderer renderer in renderers)
                    {
                        bounds.Encapsulate(MeshBounds(renderer));
                        vertices += renderer is SkinnedMeshRenderer skin ? skin.sharedMesh.vertexCount
                            : renderer.GetComponent<MeshFilter>().sharedMesh.vertexCount;
                        if (renderer.sharedMaterials.Any(m => m == null || m.shader.name != "Universal Render Pipeline/Lit"))
                            throw new InvalidOperationException(id + " renderer has an unmapped material.");
                    }
                    if (Mathf.Abs(bounds.min.y - FeetOffset) > 0.05f)
                        throw new InvalidOperationException($"{id} is not standing on the ground (feet at {bounds.min.y:0.###}).");
                    bool animated = visual.GetComponentInChildren<ModelAnimationDriver>() != null;
                    report.AppendLine($"{id}: {bounds.size.x:0.00} x {bounds.size.y:0.00} x {bounds.size.z:0.00} m (W x H x L, bind pose), "
                        + $"feet {bounds.min.y:0.000}, renderers {renderers.Length}, vertices {vertices}, "
                        + (animated ? "rigged clips." : "static with bob/lean fallback."));
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
            report.Append(details);
            report.AppendLine("Editor instantiation validated; gameplay visual review remains manual.");
            Directory.CreateDirectory("Docs/Validation");
            File.WriteAllText(ReportPath, report.ToString());
            Debug.Log(report.ToString());
        }

        // Skinned renderers report a fixed animation envelope, so measure their baked vertices instead.
        private static Bounds MeshBounds(Renderer renderer)
        {
            if (!(renderer is SkinnedMeshRenderer skin)) return renderer.bounds;
            var baked = new Mesh();
            try
            {
                skin.BakeMesh(baked, true);
                return BoundsOf(baked.vertices.Select(skin.transform.TransformPoint));
            }
            finally { UnityEngine.Object.DestroyImmediate(baked); }
        }
    }
}

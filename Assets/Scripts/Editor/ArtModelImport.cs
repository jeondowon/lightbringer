using System.Collections.Generic;
using System.Linq;
using Lightbringer.Visuals;
using UnityEditor;
using UnityEngine;

namespace Lightbringer.EditorTools
{
    // Registers an imported model (GLB via glTFast, or FBX) as a VisualId override:
    // fits it to the gameplay height, puts its feet on the ground and centres it on the pivot.
    public static class ArtModelImport
    {
        public const string FemaleHeroFolder = "Assets/Art/Characters/FemaleHero";
        public const float HeroHeight = 2.0f;

        [MenuItem("Lightbringer/Art/Assign Female Hero Model")]
        private static void AssignFemaleHero()
        {
            string path = FindModel(FemaleHeroFolder);
            if (path == null)
            {
                EditorUtility.DisplayDialog("Female Hero Model",
                    "No .glb or .fbx model found in " + FemaleHeroFolder + ".", "OK");
                return;
            }
            if (!Assign(VisualId.Hero, path, HeroHeight)) return;
            int clips = AssignAnimations(VisualId.Hero, FemaleHeroFolder + "/Animations");
            Debug.Log($"Female hero model assigned from {path} with {clips} animation clip(s). Enter Play to see it in battle.");
        }

        public static string FindModel(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder)) return null;
            // Search files directly: a model copied in before glTFast was installed is first imported as a
            // plain file and is not a GameObject until it is reimported. Source/ and Animations/ are ignored.
            string root = System.IO.Path.GetDirectoryName(Application.dataPath);
            List<string> models = System.IO.Directory.GetFiles(System.IO.Path.Combine(root, folder), "*.*", System.IO.SearchOption.TopDirectoryOnly)
                .Where(p => p.EndsWith(".glb", System.StringComparison.OrdinalIgnoreCase)
                    || p.EndsWith(".gltf", System.StringComparison.OrdinalIgnoreCase)
                    || p.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                .Select(p => p.Substring(root.Length + 1).Replace('\\', '/'))
                .OrderByDescending(p => p.ToLowerInvariant().Contains("rig"))
                .ThenByDescending(p => System.IO.File.GetLastWriteTimeUtc(System.IO.Path.Combine(root, p)))
                .ToList();
            foreach (string path in models)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return path;
                Debug.LogWarning("Could not import " + path + " as a model. For .glb files, check that the glTFast package is installed.");
            }
            return null;
        }

        public static bool Assign(VisualId id, string modelPath, float height)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            ArtStyleLibrary library = AssetDatabase.LoadAssetAtPath<ArtStyleLibrary>(ArtStyleSetup.LibraryPath)
                ?? ArtStyleSetup.EnsureLibrary();
            if (model == null || library == null)
            {
                Debug.LogError("Model import failed: " + modelPath + ". Is the glTFast package installed?");
                return false;
            }
            if (!TryMeasure(model, out Bounds bounds, out float yaw) || bounds.size.y < 0.01f)
            {
                Debug.LogError("Model has no visible renderers to measure: " + modelPath);
                return false;
            }
            float scale = height / bounds.size.y;
            if (scale < 0.1f || scale > 10f)
                Debug.LogWarning($"{id} model measured {bounds.size.y:0.###} units tall (scale {scale:0.###}). " +
                    "Check the model's unit scale if it looks too small or large in Play.");
            VisualOverride entry = (library.overrides ?? new VisualOverride[0]).FirstOrDefault(o => o != null && o.id == id);
            List<VisualOverride> list = (library.overrides ?? new VisualOverride[0]).Where(o => o != null && o.id != id).ToList();
            entry = entry ?? new VisualOverride { id = id };
            entry.prefab = model;
            entry.scale = scale;
            // Face +Z, then put the feet centre on the pivot; UnitAppearance applies the gameplay feet offset.
            entry.localEuler = new Vector3(0f, yaw, 0f);
            Vector3 centre = Quaternion.Euler(entry.localEuler) * new Vector3(bounds.center.x, 0f, bounds.center.z) * scale;
            entry.localOffset = new Vector3(-centre.x, -bounds.min.y * scale, -centre.z);
            list.Add(entry);
            Undo.RecordObject(library, "Assign " + id + " model");
            library.overrides = list.ToArray();
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            Debug.Log($"{id} override: yaw {yaw:0}, scale {scale:0.###}, offset {entry.localOffset}, source height {bounds.size.y:0.###}m.");
            return true;
        }


        // Measures in the model's own space (identity root) and estimates its facing from the feet:
        // toes reach further from the body centre than heels. Returns the yaw that turns it to +Z.
        private static bool TryMeasure(GameObject model, out Bounds bounds, out float yaw)
        {
            GameObject probe = Object.Instantiate(model);
            probe.hideFlags = HideFlags.HideAndDontSave;
            probe.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            probe.transform.localScale = Vector3.one;
            List<Vector3> points = new List<Vector3>();
            try
            {
                foreach (Renderer renderer in probe.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer is SkinnedMeshRenderer skinned && skinned.sharedMesh != null)
                        points.AddRange(SkinnedPoints(skinned));
                    else if (renderer.TryGetComponent(out MeshFilter filter) && filter.sharedMesh != null)
                    {
                        Matrix4x4 pose = renderer.transform.localToWorldMatrix;
                        foreach (Vector3 vertex in filter.sharedMesh.vertices) points.Add(pose.MultiplyPoint3x4(vertex));
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(probe);
            }
            bounds = default;
            yaw = 0f;
            if (points.Count == 0) return false;
            bounds = new Bounds(points[0], Vector3.zero);
            foreach (Vector3 point in points) bounds.Encapsulate(point);
            yaw = EstimateYaw(points, bounds);
            return true;
        }

        // BakeMesh space depends on how scale is split between the armature, the renderer and the bones
        // (Blender rigs often use a 0.01 armature over centimetre vertices). Bone world positions are always
        // true to scale, so bake every interpretation and keep the one whose height matches the skeleton.
        private static List<Vector3> SkinnedPoints(SkinnedMeshRenderer skinned)
        {
            float boneMin = float.MaxValue, boneMax = float.MinValue;
            foreach (Transform bone in skinned.bones)
            {
                if (bone == null) continue;
                boneMin = Mathf.Min(boneMin, bone.position.y);
                boneMax = Mathf.Max(boneMax, bone.position.y);
            }
            float boneSpan = boneMax - boneMin;
            Matrix4x4 full = skinned.transform.localToWorldMatrix;
            Matrix4x4 unscaled = Matrix4x4.TRS(skinned.transform.position, skinned.transform.rotation, Vector3.one);
            List<Vector3> best = null;
            float bestError = float.MaxValue;
            foreach (bool useScale in new[] { false, true })
            {
                Mesh baked = new Mesh();
                skinned.BakeMesh(baked, useScale);
                Vector3[] vertices = baked.vertices;
                Object.DestroyImmediate(baked);
                foreach (Matrix4x4 pose in new[] { full, unscaled })
                {
                    List<Vector3> candidate = new List<Vector3>(vertices.Length);
                    float min = float.MaxValue, max = float.MinValue;
                    foreach (Vector3 vertex in vertices)
                    {
                        Vector3 point = pose.MultiplyPoint3x4(vertex);
                        candidate.Add(point);
                        min = Mathf.Min(min, point.y); max = Mathf.Max(max, point.y);
                    }
                    if (boneSpan <= 0.0001f) return candidate;
                    // The mesh is a little taller than the bone span (hair, soles): aim for ~1.1x.
                    float error = Mathf.Abs(Mathf.Log(Mathf.Max(max - min, 1e-6f) / (boneSpan * 1.1f)));
                    if (error < bestError) { bestError = error; best = candidate; }
                }
            }
            return best ?? new List<Vector3>();
        }

        private static float EstimateYaw(List<Vector3> points, Bounds bounds)
        {
            float floor = bounds.min.y + bounds.size.y * 0.03f;
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (Vector3 point in points)
            {
                if (point.y > floor) continue;
                minX = Mathf.Min(minX, point.x); maxX = Mathf.Max(maxX, point.x);
                minZ = Mathf.Min(minZ, point.z); maxZ = Mathf.Max(maxZ, point.z);
            }
            if (minX > maxX) return 0f;
            float plusX = maxX - bounds.center.x, minusX = bounds.center.x - minX;
            float plusZ = maxZ - bounds.center.z, minusZ = bounds.center.z - minZ;
            // The axis with the most lopsided foot reach is the facing axis.
            float asymmetryX = Mathf.Abs(plusX - minusX), asymmetryZ = Mathf.Abs(plusZ - minusZ);
            Vector3 forward = asymmetryX > asymmetryZ
                ? (plusX > minusX ? Vector3.right : Vector3.left)
                : (plusZ > minusZ ? Vector3.forward : Vector3.back);
            // Rotation that maps `forward` onto +Z.
            return -Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        }

        // Extracts one clip per role from animation GLBs in `folder`/Source (file names containing
        // idle / run|walk / cast|attack), strips horizontal root motion so the gameplay controller stays in
        // charge of movement, saves them as .anim assets and wires them into the override.
        public static int AssignAnimations(VisualId id, string folder)
        {
            string source = folder + "/Source";
            if (!AssetDatabase.IsValidFolder(source)) return 0;
            ArtStyleLibrary library = AssetDatabase.LoadAssetAtPath<ArtStyleLibrary>(ArtStyleSetup.LibraryPath);
            VisualOverride entry = library != null ? library.FindOverride(id) : null;
            if (entry == null) return 0;
            string root = System.IO.Path.GetDirectoryName(Application.dataPath);
            string[] files = System.IO.Directory.GetFiles(System.IO.Path.Combine(root, source), "*.glb")
                .Select(p => p.Substring(root.Length + 1).Replace('\\', '/')).ToArray();
            int assigned = 0;
            foreach (string file in files)
            {
                string name = System.IO.Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                string role = name.Contains("idle") ? "Idle" : name.Contains("run") || name.Contains("walk") ? "Move"
                    : name.Contains("cast") || name.Contains("attack") ? "Action" : null;
                if (role == null) continue;
                if (AssetDatabase.LoadAssetAtPath<GameObject>(file) == null)
                    AssetDatabase.ImportAsset(file, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(file).OfType<AnimationClip>()
                    .OrderByDescending(c => c.length).FirstOrDefault();
                if (clip == null) { Debug.LogWarning("No animation clip inside " + file); continue; }
                AnimationClip saved = SaveInPlace(clip, folder + "/" + System.IO.Path.GetFileNameWithoutExtension(file) + ".anim",
                    out float rootSpeed);
                if (role == "Idle") entry.idleClip = saved;
                else if (role == "Move")
                {
                    entry.moveClip = saved;
                    if (rootSpeed > 0.2f) entry.moveClipSpeed = rootSpeed * entry.scale;
                }
                else entry.actionClip = saved;
                assigned++;
            }
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            return assigned;
        }

        private static AnimationClip SaveInPlace(AnimationClip source, string path, out float rootSpeed)
        {
            AnimationClip clip = new AnimationClip();
            EditorUtility.CopySerialized(source, clip);
            clip.legacy = true;
            clip.name = System.IO.Path.GetFileNameWithoutExtension(path);
            rootSpeed = 0f;
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
            {
                bool hips = binding.path.EndsWith("Hips");
                bool horizontal = binding.propertyName == "m_LocalPosition.x" || binding.propertyName == "m_LocalPosition.z";
                if (!hips || !horizontal) continue;
                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve == null || curve.length < 2) continue;
                float travel = curve.keys[curve.length - 1].value - curve.keys[0].value;
                if (binding.propertyName == "m_LocalPosition.z" && clip.length > 0f)
                    rootSpeed = Mathf.Abs(travel) / clip.length;
                // In-place clips only sway; keep that. Strip only real forward/sideways travel.
                if (Mathf.Abs(travel) < 0.05f) continue;
                // Keep the starting offset, remove drift: the CharacterController moves the hero.
                float first = curve.keys[0].value;
                AnimationUtility.SetEditorCurve(clip, binding, AnimationCurve.Constant(0f, clip.length, first));
            }
            AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(clip, existing);
                Object.DestroyImmediate(clip);
                return existing;
            }
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        public const string FemaleHeroPropsFolder = FemaleHeroFolder + "/Props";

        [MenuItem("Lightbringer/Art/Assign Female Hero Staff")]
        private static void AssignFemaleHeroStaff()
        {
            ArtStyleLibrary library = AssetDatabase.LoadAssetAtPath<ArtStyleLibrary>(ArtStyleSetup.LibraryPath);
            VisualOverride hero = library != null ? library.FindOverride(VisualId.Hero) : null;
            string staff = FindModel(FemaleHeroPropsFolder);
            if (hero == null || staff == null)
            {
                EditorUtility.DisplayDialog("Female Hero Staff", hero == null
                    ? "Run Assign Female Hero Model first."
                    : "No staff model found in " + FemaleHeroPropsFolder + ".", "OK");
                return;
            }
            if (!AttachProp(library, hero, staff, "RightHand", 2.0f)) return;
            Debug.Log("Staff attached to the hero's right hand from " + staff + ".");
        }

        // Places a long prop upright in the bone's bind pose: gripped ~38% up its length, a little past the
        // wrist, and sized to `gameLength` metres in play. Stored as bone-local values so it follows animation.
        public static bool AttachProp(ArtStyleLibrary library, VisualOverride owner, string propPath, string boneName, float gameLength)
        {
            GameObject prop = AssetDatabase.LoadAssetAtPath<GameObject>(propPath);
            if (prop == null || owner.prefab == null || !TryMeasure(prop, out Bounds propBounds, out _) || propBounds.size.y < 1e-4f)
            {
                Debug.LogError("Cannot measure prop " + propPath);
                return false;
            }
            if (propBounds.size.y < Mathf.Max(propBounds.size.x, propBounds.size.z))
                Debug.LogWarning("Prop's long axis is not vertical; it may need a manual rotation in the library.");
            GameObject probe = Object.Instantiate(owner.prefab);
            probe.hideFlags = HideFlags.HideAndDontSave;
            probe.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            probe.transform.localScale = Vector3.one;
            try
            {
                Transform hand = probe.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == boneName);
                if (hand == null) { Debug.LogError($"Bone {boneName} not found on {owner.prefab.name}."); return false; }
                // Work in the model's own space; the override scale is applied on top in play.
                float length = gameLength / Mathf.Max(owner.scale, 1e-4f);
                float propScale = length / propBounds.size.y;
                Vector3 along = hand.parent != null ? (hand.position - hand.parent.position).normalized : Vector3.down;
                Vector3 grip = hand.position + along * (0.07f / Mathf.Max(owner.scale, 1e-4f));
                Vector3 bottom = grip - Vector3.up * (0.38f * length);
                Vector3 origin = bottom - propScale * new Vector3(propBounds.center.x, propBounds.min.y, propBounds.center.z);
                Vector3 boneScale = hand.lossyScale;
                PropAttachment attachment = new PropAttachment
                {
                    prefab = prop,
                    boneName = boneName,
                    localPosition = hand.InverseTransformPoint(origin),
                    localEuler = (Quaternion.Inverse(hand.rotation)).eulerAngles,
                    localScale = new Vector3(propScale / boneScale.x, propScale / boneScale.y, propScale / boneScale.z)
                };
                Undo.RecordObject(library, "Attach prop");
                List<PropAttachment> list = (owner.attachments ?? new PropAttachment[0])
                    .Where(a => a != null && a.boneName != boneName).ToList();
                list.Add(attachment);
                owner.attachments = list.ToArray();
                EditorUtility.SetDirty(library);
                AssetDatabase.SaveAssets();
                return true;
            }
            finally
            {
                Object.DestroyImmediate(probe);
            }
        }
    }
}

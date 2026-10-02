using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lightbringer.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using static Lightbringer.EditorTools.RigBuildUtility;

namespace Lightbringer.EditorTools
{
    // Project-native prototype rig for the Spearman. The Tripo export is an A-posed armoured soldier without a
    // weapon, so the spear is built from primitives and parented to the right hand. Landmark heights were
    // measured on the placed export (1.7 m including the helmet, feet on the pivot, facing +Z) and are kept as
    // fractions of that height; the downloaded FBX stays untouched.
    internal static class SpearmanRigSetup
    {
        private const string Folder = "Assets/Art/Characters/Spearman";
        private const string RigFolder = Folder + "/Rigged";
        private const string PrefabPath = RigFolder + "/Spearman_Rigged.prefab";
        private const string ReportPath = "Docs/Validation/SpearmanRigChecks.txt";
        private const float H = 1.7f;
        private const float LegTop = .47f * H, HeadBase = .85f * H;
        // Spear in its own space: grip at the origin, shaft along +Y towards the head.
        private const float ButtY = -.74f, TipY = 1.62f;
        private static readonly Vector3 HipsRest = new Vector3(0f, .50f * H, 0f);
        private static readonly List<Transform> bones = new List<Transform>();
        private static readonly List<Vector3> ends = new List<Vector3>();
        private static int[] palmVertices = new int[0];

        // `toUnit` maps the FBX root into unit space (the static override placement).
        public static VisualOverride Build(GameObject source, Matrix4x4 toUnit, ArtStyleLibrary library)
        {
            if (!AssetDatabase.IsValidFolder(RigFolder)) AssetDatabase.CreateFolder(Folder, "Rigged");
            Scene scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Spearman_Rigged");
            SceneManager.MoveGameObjectToScene(root, scene);
            var probe = UnityEngine.Object.Instantiate(source);
            SceneManager.MoveGameObjectToScene(probe, scene);
            try
            {
                probe.transform.SetPositionAndRotation(toUnit.GetColumn(3), toUnit.rotation);
                probe.transform.localScale = toUnit.lossyScale;
                CombineInstance[] parts = probe.GetComponentsInChildren<MeshFilter>()
                    .SelectMany(f => Enumerable.Range(0, f.sharedMesh.subMeshCount).Select(s => new CombineInstance
                        { mesh = f.sharedMesh, subMeshIndex = s, transform = f.transform.localToWorldMatrix }))
                    .ToArray();
                var mesh = new Mesh { name = "Spearman_Skinned", indexFormat = IndexFormat.UInt32 };
                mesh.CombineMeshes(parts, true, true);
                bones.Clear(); ends.Clear();
                Vector3[] vertices = mesh.vertices;
                MakeSkeleton(root.transform, vertices);
                AssignWeights(mesh);
                mesh.bindposes = bones.Select(b => b.worldToLocalMatrix * root.transform.localToWorldMatrix).ToArray();
                mesh.RecalculateBounds();
                mesh = SaveAsset(mesh, RigFolder + "/Spearman_Skinned.asset");

                var body = new GameObject("Body");
                body.transform.SetParent(root.transform, false);
                var skin = body.AddComponent<SkinnedMeshRenderer>();
                skin.sharedMesh = mesh;
                skin.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Spearman.mat");
                skin.bones = bones.ToArray();
                skin.rootBone = bones[0];
                skin.quality = SkinQuality.Bone4;
                // Conservative envelope covering the lunge; the spear has its own renderers.
                skin.localBounds = new Bounds(new Vector3(0f, .9f, .2f), new Vector3(1.8f, 2.1f, 2.2f));
                skin.updateWhenOffscreen = false;
                Transform spear = BuildSpear(root.transform, mesh);

                AnimationClip idle = MakeClip(root.transform, "Idle", 2.2f);
                AnimationClip run = MakeClip(root.transform, "Run", .8f);
                AnimationClip thrust = MakeClip(root.transform, "Thrust", .7f);
                string checks = ValidateRig(root, skin, spear, idle, run, thrust);
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (prefab == null) throw new InvalidOperationException("Could not save the Spearman rig prefab.");
                File.WriteAllText(ReportPath, $"PASS | {DateTime.UtcNow:O}\n"
                    + $"Unity-native prototype skeleton: {bones.Count} bones; {mesh.vertexCount} weighted vertices.\n"
                    + checks + "Spear: primitive parts on RightHand (grip at the palm). Original FBX preserved.\n"
                    + "No external animation assets. Hand-tuned landmark weights and generated prototype clips.\n"
                    + "Visual review of joints and the tabard remains manual; no mesh decimation performed.\n");
                return new VisualOverride
                {
                    id = VisualId.Spearman, prefab = prefab, scale = 1f,
                    idleClip = idle, moveClip = run, actionClip = thrust, moveClipSpeed = 3f,
                    attachments = new PropAttachment[0], // The spear is a child of the hand bone in this prefab.
                };
            }
            finally
            {
                bones.Clear(); ends.Clear();
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static Transform Find(string name) => bones.First(b => b.name == name);

        private static Transform Bone(string name, Transform parent, Vector3 point, Vector3 end)
        {
            var bone = new GameObject(name).transform;
            bone.SetParent(parent, false);
            bone.position = point;
            bones.Add(bone); ends.Add(end);
            return bone;
        }

        private static void MakeSkeleton(Transform root, Vector3[] vertices)
        {
            Transform hips = Bone("Hips", root, HipsRest, new Vector3(0, .56f * H, 0));
            Transform spine = Bone("Spine", hips, new Vector3(0, .56f * H, 0), new Vector3(0, .68f * H, 0));
            Transform chest = Bone("Chest", spine, new Vector3(0, .68f * H, 0), new Vector3(0, .82f * H, 0));
            Transform neck = Bone("Neck", chest, new Vector3(0, .82f * H, 0), new Vector3(0, HeadBase, 0));
            Bone("Head", neck, new Vector3(0, HeadBase, 0), new Vector3(0, H, 0));
            foreach (int sign in new[] { 1, -1 })
            {
                string side = sign == 1 ? "Right" : "Left";
                // The pauldrons pull the shoulder centroid outwards; the joint sits under them.
                Vector3 pad = Landmark(vertices, sign, .775f * H, .805f * H, .11f * H, out _);
                Vector3 shoulderPoint = new Vector3(sign * Mathf.Min(Mathf.Abs(pad.x), .12f * H), pad.y, pad.z);
                Vector3 elbow = Landmark(vertices, sign, .655f * H, .69f * H, .13f * H, out _);
                Vector3 wrist = Landmark(vertices, sign, .555f * H, .575f * H, .17f * H, out _);
                Vector3 palm = Landmark(vertices, sign, .475f * H, .51f * H, .21f * H, out int[] indices);
                if (sign == 1) palmVertices = indices;
                Transform shoulder = Bone(side + "Shoulder", chest, new Vector3(sign * .06f * H, .80f * H, shoulderPoint.z), shoulderPoint);
                Transform upper = Bone(side + "UpperArm", shoulder, shoulderPoint, elbow);
                Transform fore = Bone(side + "ForeArm", upper, elbow, wrist);
                Bone(side + "Hand", fore, wrist, palm + (palm - wrist).normalized * .02f * H);

                Vector3 knee = Landmark(vertices, sign, .28f * H, .31f * H, .015f * H, out _);
                Vector3 ankle = Landmark(vertices, sign, .065f * H, .09f * H, .015f * H, out _);
                Transform thigh = Bone(side + "Thigh", hips, new Vector3(sign * .065f * H, LegTop, 0), knee);
                Transform shin = Bone(side + "Shin", thigh, knee, ankle);
                Bone(side + "Foot", shin, ankle, new Vector3(ankle.x, .02f * H, .10f * H));
            }
            Bone("Tabard", hips, new Vector3(0, .50f * H, .07f * H), new Vector3(0, .36f * H, .075f * H));
            Bone("TabardBack", hips, new Vector3(0, .50f * H, -.08f * H), new Vector3(0, .38f * H, -.10f * H));
        }

        // A-posed arms leave a gap to the torso that widens towards the hands.
        private static bool IsArm(Vector3 p) => p.y > .46f * H && p.y < .84f * H
            && Mathf.Abs(p.x) > Mathf.Lerp(.18f, .105f, Mathf.InverseLerp(.47f * H, .66f * H, p.y)) * H;

        private static void AssignWeights(Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices;
            var weights = new BoneWeight[vertices.Length];
            string[] names = bones.Select(b => b.name).ToArray();
            Vector3[] starts = bones.Select(b => b.position).ToArray();
            int[] sides = names.Select(n => n.StartsWith("Right") ? 1 : n.StartsWith("Left") ? -1 : 0).ToArray();
            bool[] armBones = names.Select(n => n.EndsWith("Arm") || n.EndsWith("Hand") || n.EndsWith("Shoulder")).ToArray();
            bool[] legBones = names.Select(n => n.EndsWith("Thigh") || n.EndsWith("Shin") || n.EndsWith("Foot")).ToArray();
            int rightHand = Array.IndexOf(names, "RightHand"), leftHand = Array.IndexOf(names, "LeftHand");
            int[] best = new int[4]; float[] distances = new float[4];
            for (int v = 0; v < vertices.Length; v++)
            {
                Vector3 p = vertices[v];
                bool arm = IsArm(p);
                int handIndex = p.x >= 0 ? rightHand : leftHand;
                // Keep the palm and the spear socket on exactly the same transform. Blend the wrist above it.
                if (arm && p.y < starts[handIndex].y - .01f * H)
                {
                    weights[v] = new BoneWeight { boneIndex0 = handIndex, weight0 = 1f };
                    continue;
                }
                // Tabard flaps hang in front of and behind the thighs and must not follow them.
                bool front = !arm && p.y > .35f * H && p.y < .50f * H && p.z > .06f * H && Mathf.Abs(p.x) < .08f * H;
                bool back = !arm && !front && p.y > .38f * H && p.y < LegTop && p.z < -.065f * H;
                for (int j = 0; j < 4; j++) { best[j] = 0; distances[j] = float.PositiveInfinity; }
                for (int i = 0; i < bones.Count; i++)
                {
                    string name = names[i];
                    if (sides[i] != 0 && sides[i] != (p.x >= 0 ? 1 : -1)) continue;
                    bool isArm = armBones[i];
                    bool isLeg = legBones[i];
                    if (arm && !isArm && name != "Chest") continue;
                    if (!arm && isArm) continue;
                    if (front && name != "Tabard" && name != "Hips") continue;
                    if (back && name != "TabardBack" && name != "Hips") continue;
                    if (!front && name == "Tabard" || !back && name == "TabardBack") continue;
                    if (!arm && !front && !back && (p.y < LegTop ? !isLeg && name != "Hips" : isLeg)) continue;
                    if (p.y > HeadBase && name != "Head") continue;
                    Vector3 a = starts[i], delta = ends[i] - a;
                    float t = Mathf.Clamp01(Vector3.Dot(p - a, delta) / Mathf.Max(delta.sqrMagnitude, .00001f));
                    float d = (p - a - delta * t).sqrMagnitude;
                    for (int j = 0; j < 4; j++)
                    {
                        if (d >= distances[j]) continue;
                        for (int k = 3; k > j; k--) { distances[k] = distances[k - 1]; best[k] = best[k - 1]; }
                        distances[j] = d; best[j] = i; break;
                    }
                }
                float sum = 0, nearest = distances[0];
                if (float.IsInfinity(nearest)) throw new InvalidOperationException("No eligible bone at vertex " + p);
                for (int j = 0; j < 4; j++)
                {
                    // Compact support keeps armour mostly rigid while blending across the nearest joint.
                    distances[j] = float.IsInfinity(distances[j]) ? 0 : Mathf.Exp(-(distances[j] - nearest) / .006f);
                    sum += distances[j];
                }
                weights[v] = new BoneWeight { boneIndex0 = best[0], boneIndex1 = best[1], boneIndex2 = best[2], boneIndex3 = best[3],
                    weight0 = distances[0] / sum, weight1 = distances[1] / sum, weight2 = distances[2] / sum, weight3 = distances[3] / sum };
            }
            mesh.boneWeights = weights;
        }

        // ---------- Spear ----------

        private static Transform BuildSpear(Transform root, Mesh skinned)
        {
            Material wood = MakeMaterial("SpearShaft", new Color(.22f, .13f, .07f), 0f, .3f);
            Material leather = MakeMaterial("SpearGrip", new Color(.12f, .07f, .04f), 0f, .25f);
            Material steel = MakeMaterial("SpearSteel", new Color(.70f, .76f, .82f), .85f, .6f);
            Material gold = MakeMaterial("SpearGold", new Color(.78f, .55f, .2f), .7f, .45f);
            Transform hand = Find("RightHand");
            var spear = new GameObject("Spear").transform;
            spear.SetParent(hand, false);
            spear.position = root.TransformPoint(PalmCentre(skinned.vertices));
            // Upright in the bind pose; the clips counter-rotate the hand to aim it.
            spear.rotation = Quaternion.identity;
            float shaftTop = 1.30f;
            Part(spear, "Shaft", PrimitiveType.Cylinder, new Vector3(0, (ButtY + shaftTop) * .5f, 0),
                new Vector3(.034f, (shaftTop - ButtY) * .5f, .034f), wood);
            Part(spear, "Grip", PrimitiveType.Cylinder, Vector3.zero, new Vector3(.042f, .09f, .042f), leather);
            Part(spear, "Butt", PrimitiveType.Sphere, new Vector3(0, ButtY, 0), Vector3.one * .05f, steel);
            Part(spear, "Collar", PrimitiveType.Cylinder, new Vector3(0, shaftTop + .01f, 0), new Vector3(.05f, .03f, .05f), gold);
            Part(spear, "Wings", PrimitiveType.Cube, new Vector3(0, shaftTop + .04f, 0), new Vector3(.13f, .022f, .025f), gold);

            // Leaf blade with a diamond cross-section: base, widest belly, tip.
            var headMesh = new Mesh { name = "SpearHead" };
            float b = shaftTop + .05f, m = shaftTop + .13f;
            headMesh.vertices = new[] { new Vector3(-.022f,b,0), new Vector3(0,b,.012f), new Vector3(.022f,b,0), new Vector3(0,b,-.012f),
                new Vector3(-.048f,m,0), new Vector3(0,m,.016f), new Vector3(.048f,m,0), new Vector3(0,m,-.016f), new Vector3(0,TipY,0),
                new Vector3(0,b,0) };
            var triangles = new List<int>();
            for (int i = 0; i < 4; i++)
            {
                int j = (i + 1) % 4;
                triangles.AddRange(new[] { i, i + 4, j, j, i + 4, j + 4, i + 4, 8, j + 4, i, j, 9 });
            }
            triangles.Reverse();
            headMesh.triangles = triangles.ToArray(); headMesh.RecalculateNormals(); headMesh.RecalculateBounds();
            headMesh = SaveAsset(headMesh, RigFolder + "/SpearHead.asset");
            var head = new GameObject("Head"); head.transform.SetParent(spear, false);
            head.AddComponent<MeshFilter>().sharedMesh = headMesh;
            head.AddComponent<MeshRenderer>().sharedMaterial = steel;
            return spear;
        }

        private static Vector3 PalmCentre(Vector3[] vertices)
        {
            Vector3 sum = Vector3.zero;
            foreach (int index in palmVertices) sum += vertices[index];
            return sum / palmVertices.Length;
        }

        private static Material MakeMaterial(string name, Color color, float metallic, float smoothness)
            => RigBuildUtility.MakeMaterial(RigFolder, name, color, metallic, smoothness);

        // ---------- Clips ----------

        private static AnimationClip MakeClip(Transform root, string role, float duration)
        {
            var clip = new AnimationClip { name = "Spearman_" + role, legacy = true, frameRate = 30,
                wrapMode = role == "Thrust" ? WrapMode.Once : WrapMode.Loop };
            foreach (Transform bone in bones)
            {
                string path = AnimationUtility.CalculateTransformPath(bone, root);
                var curves = new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve(), new AnimationCurve() };
                var lift = new AnimationCurve();
                int samples = Mathf.RoundToInt(duration * 30);
                for (int i = 0; i <= samples; i++)
                {
                    float phase = (float)i / samples;
                    Quaternion q = Rotation(bone.name, role, phase);
                    float time = phase * duration;
                    curves[0].AddKey(time, q.x); curves[1].AddKey(time, q.y); curves[2].AddKey(time, q.z); curves[3].AddKey(time, q.w);
                    float bob = role == "Run" ? .018f * (1f - Mathf.Cos(phase * Mathf.PI * 4f))
                        : role == "Thrust" ? -.04f * Strike(phase) : .006f * Mathf.Sin(phase * Mathf.PI * 2f);
                    lift.AddKey(time, HipsRest.y + bob);
                }
                for (int k = 0; k < 4; k++) clip.SetCurve(path, typeof(Transform), "m_LocalRotation." + "xyzw"[k], curves[k]);
                if (bone.name == "Hips") clip.SetCurve(path, typeof(Transform), "m_LocalPosition.y", lift);
            }
            clip.EnsureQuaternionContinuity();
            return SaveAsset(clip, RigFolder + "/Spearman_" + role + ".anim");
        }

        // Thrust timing: pull back, drive forward (contact at 0.45), recover. Damage remains owned by UnitCombat.
        private static float Windup(float phase) => phase < .28f ? Mathf.SmoothStep(0, 1, phase / .28f)
            : phase < .45f ? Mathf.SmoothStep(1, 0, (phase - .28f) / .17f) : 0f;
        private static float Strike(float phase) => phase < .28f ? 0f : phase < .45f ? Mathf.SmoothStep(0, 1, (phase - .28f) / .17f)
            : Mathf.SmoothStep(1, 0, (phase - .45f) / .55f);
        private static float Lowered(float phase) => phase < .2f ? Mathf.SmoothStep(0, 1, phase / .2f)
            : phase < .6f ? 1f : Mathf.SmoothStep(1, 0, (phase - .6f) / .4f);
        private const float ContactPhase = .45f;

        private static Vector3 Pose(string name, string role, float phase)
        {
            float wave = Mathf.Sin(phase * Mathf.PI * 2f);
            float side = name.StartsWith("Right") ? 1f : -1f;
            Vector3 pose = Vector3.zero;
            // Arms in from the A-pose; the spear hand rides at the waist, the free hand relaxed.
            if (name == "RightUpperArm") pose = new Vector3(-10, 0, -14);
            if (name == "RightForeArm") pose = new Vector3(-50, 0, 4);
            if (name == "LeftUpperArm") pose = new Vector3(-6, 0, 16);
            if (name == "LeftForeArm") pose = new Vector3(-20, 0, -4);
            if (name == "Chest") pose.x = wave * 1.5f;
            if (role == "Run")
            {
                if (name.EndsWith("Thigh")) pose.x = -side * wave * 23;
                if (name.EndsWith("Shin")) pose.x = Mathf.Max(0, side * wave) * 32;
                if (name.EndsWith("Foot")) pose.x = -Mathf.Max(0, side * wave) * 12;
                if (name == "LeftUpperArm") pose.x += -wave * 14;
                if (name == "RightUpperArm") pose.x += wave * 4;
                if (name == "Chest") pose = new Vector3(6, wave * 3, 0);
                if (name == "Tabard") pose.x = -6 + wave * 6;
                if (name == "TabardBack") pose.x = 9 + wave * 4;
            }
            if (role == "Thrust")
            {
                float w = Windup(phase), s = Strike(phase), l = Lowered(phase);
                if (name == "RightUpperArm") pose += new Vector3(22 * w - 62 * s, 0, 8 * s);
                if (name == "RightForeArm") pose.x += -30 * w + 38 * s;
                if (name == "LeftUpperArm") pose.x += -34 * l;
                if (name == "LeftForeArm") pose.x += -36 * l;
                if (name == "Chest") pose = new Vector3(5 * s, 14 * w - 12 * s, 0);
                if (name == "Spine") pose = new Vector3(4 * s, 0, 0);
                // Lunge onto the front (left) foot.
                if (name == "LeftThigh") pose.x = -18 * s;
                if (name == "LeftShin") pose.x = 16 * s;
                if (name == "RightThigh") pose.x = 12 * s;
                if (name == "Tabard") pose.x = -8 * s;
            }
            return pose;
        }

        private static Quaternion Rotation(string name, string role, float phase)
        {
            if (name != "RightHand") return Quaternion.Euler(Pose(name, role, phase));
            Quaternion parent = Quaternion.identity;
            foreach (string ancestor in new[] { "Hips", "Spine", "Chest", "RightShoulder", "RightUpperArm", "RightForeArm" })
                parent *= Quaternion.Euler(Pose(ancestor, role, phase));
            // Aim the spear in root space: near-upright at rest, tilted forward on the run, level for the thrust.
            float pitch = role == "Run" ? 30f : 8f;
            if (role == "Thrust") pitch = Mathf.Lerp(8f, 88f, Lowered(phase));
            return Quaternion.Inverse(parent) * Quaternion.Euler(pitch, 0f, 0f);
        }

        // ---------- Validation ----------

        private static string ValidateRig(GameObject root, SkinnedMeshRenderer skin, Transform spear, params AnimationClip[] clips)
        {
            if (skin.bones.Length != 21 || skin.sharedMesh.bindposes.Length != skin.bones.Length)
                throw new InvalidOperationException("Skeleton/bind pose mismatch.");
            foreach (BoneWeight w in skin.sharedMesh.boneWeights)
                if (float.IsNaN(w.weight0) || Mathf.Abs(w.weight0 + w.weight1 + w.weight2 + w.weight3 - 1) > .001f)
                    throw new InvalidOperationException("Unnormalized skin weights.");
            if (spear.parent != Find("RightHand")) throw new InvalidOperationException("Spear is not attached to the right hand.");
            var rest = new Mesh(); var posed = new Mesh();
            var results = new List<string>();
            try
            {
                skin.BakeMesh(rest);
                Vector3[] vertices = rest.vertices;
                Vector3 forward = root.transform.forward;
                foreach (AnimationClip clip in clips)
                {
                    ResetPose();
                    clip.SampleAnimation(root, clip.length * .3f);
                    skin.BakeMesh(posed);
                    float largest = 0; Vector3[] changed = posed.vertices;
                    for (int i = 0; i < changed.Length; i++) largest = Mathf.Max(largest, (changed[i] - vertices[i]).magnitude);
                    if (largest < .001f || largest > 1.5f) throw new InvalidOperationException("Invalid deformation in " + clip.name);
                    results.Add($"{clip.name}: sampled weighted vertex motion {largest:F3} m");

                    bool thrust = clip.name.EndsWith("Thrust");
                    clip.SampleAnimation(root, clip.length * (thrust ? ContactPhase : .5f));
                    skin.BakeMesh(posed);
                    Vector3[] gripPose = posed.vertices;
                    Vector3 palm = Vector3.zero;
                    foreach (int index in palmVertices) palm += skin.transform.TransformPoint(gripPose[index]);
                    palm /= palmVertices.Length;
                    float gripError = Vector3.Distance(palm, spear.position);
                    float butt = spear.TransformPoint(new Vector3(0, ButtY, 0)).y;
                    if (gripError > .015f) throw new InvalidOperationException("Spear/palm alignment failed for " + clip.name);
                    if (butt < .02f) throw new InvalidOperationException($"Spear butt goes through the ground in {clip.name} ({butt:F3} m).");
                    results.Add($"{clip.name}: grip error {gripError:F4} m; spear butt {butt:F2} m above ground.");
                    if (thrust)
                    {
                        Vector3 tip = spear.TransformPoint(new Vector3(0, TipY, 0));
                        float reach = Vector3.Dot(tip - root.transform.position, forward);
                        float level = Vector3.Dot(spear.up, forward);
                        if (reach < 1.5f || level < .95f || tip.y < .7f || tip.y > 1.7f)
                            throw new InvalidOperationException($"Thrust does not drive the spear forward (reach {reach:F2} m, tip height {tip.y:F2} m, level {level:F2}).");
                        results.Add($"Thrust: spear tip {reach:F2} m in front of the pivot at {tip.y:F2} m height at contact.");
                    }
                }
                ResetPose();
                return string.Join("\n", results) + "\n";
            }
            finally { UnityEngine.Object.DestroyImmediate(rest); UnityEngine.Object.DestroyImmediate(posed); }
        }

        private static void ResetPose()
        {
            foreach (Transform bone in bones) bone.localRotation = Quaternion.identity;
            bones[0].localPosition = HipsRest;
        }
    }
}

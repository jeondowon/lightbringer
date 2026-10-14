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
    // Project-native prototype rig for the ground Dragon: four walking legs, neck/head for the fire breath,
    // a swaying tail and rigid wings held folded back along the body (opened during the breath).
    // Landmarks were measured on the placed Tripo export (4 m nose-to-tail, feet on the pivot, facing +Z);
    // the downloaded FBX stays untouched.
    internal static class DragonRigSetup
    {
        private const string Folder = "Assets/Art/Characters/Dragon";
        private const string RigFolder = Folder + "/Rigged";
        private const string PrefabPath = RigFolder + "/Dragon_Rigged.prefab";
        private const string ReportPath = "Docs/Validation/DragonRigChecks.txt";
        private const float WingFold = 70f;
        private static readonly Vector3 MouthPoint = new Vector3(0f, 1.72f, 1.06f);

        private sealed class BoneInfo
        {
            public Transform Transform;
            public Vector3 Start, End;
            public int Side;
            public bool Skinned = true;
        }

        private static readonly List<BoneInfo> bones = new List<BoneInfo>();

        // `toUnit` maps the FBX root into unit space (the static override placement).
        public static VisualOverride Build(GameObject source, Matrix4x4 toUnit, ArtStyleLibrary library)
        {
            if (!AssetDatabase.IsValidFolder(RigFolder)) AssetDatabase.CreateFolder(Folder, "Rigged");
            Scene scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Dragon_Rigged");
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
                var mesh = new Mesh { name = "Dragon_Skinned", indexFormat = IndexFormat.UInt32 };
                mesh.CombineMeshes(parts, true, true);
                bones.Clear();
                MakeSkeleton(root.transform);
                AssignWeights(mesh);
                BoneInfo[] skinned = bones.Where(b => b.Skinned).ToArray();
                mesh.bindposes = skinned.Select(b => b.Transform.worldToLocalMatrix * root.transform.localToWorldMatrix).ToArray();
                mesh.RecalculateBounds();
                mesh = SaveAsset(mesh, RigFolder + "/Dragon_Skinned.asset");

                var body = new GameObject("Body");
                body.transform.SetParent(root.transform, false);
                var skin = body.AddComponent<SkinnedMeshRenderer>();
                skin.sharedMesh = mesh;
                skin.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Dragon.mat");
                skin.bones = skinned.Select(b => b.Transform).ToArray();
                skin.rootBone = Find("Spine");
                skin.quality = SkinQuality.Bone4;
                // Conservative envelope covering the walk and the opened wings of the breath.
                skin.localBounds = new Bounds(new Vector3(0f, 1.3f, -0.6f), new Vector3(6f, 3f, 5f));

                Transform mouth = new GameObject("Mouth").transform;
                mouth.SetParent(Find("Head"), false);
                mouth.position = MouthPoint;
                Material fire = FireMaterial();
                root.AddComponent<DragonBreathVfx>().Configure(mouth, fire);

                AnimationClip idle = MakeClip(root.transform, "Idle", 2.4f, WrapMode.Loop);
                AnimationClip walk = MakeClip(root.transform, "Walk", 1.1f, WrapMode.Loop);
                AnimationClip breath = MakeClip(root.transform, "Breath", 1f, WrapMode.Once);
                string checks = ValidateRig(root, skin, idle, walk, breath);
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (prefab == null) throw new InvalidOperationException("Could not save the Dragon rig prefab.");
                File.WriteAllText(ReportPath, $"PASS | {DateTime.UtcNow:O}\n"
                    + $"Unity-native prototype skeleton: {skinned.Length} bones; {mesh.vertexCount} weighted vertices.\n"
                    + checks + "Wings rest folded back (" + WingFold + " deg) and open during the breath; fire from Head/Mouth.\n"
                    + "Ground unit (no flight). Original FBX preserved. Visual review of joints remains manual.\n");
                return new VisualOverride
                {
                    id = VisualId.Dragon, prefab = prefab, scale = 1f,
                    idleClip = idle, moveClip = walk, actionClip = breath, moveClipSpeed = 3f,
                };
            }
            finally
            {
                bones.Clear();
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static Transform Find(string name) => bones.First(b => b.Transform.name == name).Transform;

        private static Transform Bone(string name, Transform parent, Vector3 start, Vector3 end, int side = 0, bool skinned = true)
        {
            var bone = new GameObject(name).transform;
            bone.SetParent(parent, false);
            bone.position = start;
            bones.Add(new BoneInfo { Transform = bone, Start = start, End = end, Side = side, Skinned = skinned });
            return bone;
        }

        private static void MakeSkeleton(Transform root)
        {
            // Spine carries the walk bob/roll and is not skinned itself.
            Transform spine = Bone("Spine", root, new Vector3(0f, 1f, -0.25f), new Vector3(0f, 1f, -0.25f), skinned: false);
            Transform hips = Bone("Hips", spine, new Vector3(0f, 1f, -0.3f), new Vector3(0f, 0.95f, -1f));
            Transform chest = Bone("Chest", spine, new Vector3(0f, 1.02f, -0.2f), new Vector3(0f, 1.2f, 0.35f));
            Transform neck = Bone("Neck", chest, new Vector3(0f, 1.2f, 0.3f), new Vector3(0f, 1.65f, 0.3f));
            Bone("Head", neck, new Vector3(0f, 1.65f, 0.3f), new Vector3(0f, 1.78f, 1.02f));
            Vector3[] tail =
            {
                new Vector3(0.1f, 0.85f, -1f), new Vector3(0.65f, 0.45f, -1.3f), new Vector3(1.3f, 0.2f, -1.5f),
                new Vector3(1.52f, 0.15f, -2f), new Vector3(1.3f, 0.3f, -2.5f), new Vector3(0.9f, 0.45f, -2.8f),
            };
            Transform parent = hips;
            for (int i = 0; i < tail.Length - 1; i++) parent = Bone("Tail" + (i + 1), parent, tail[i], tail[i + 1]);
            foreach (int side in new[] { -1, 1 })
            {
                string prefix = side < 0 ? "Left" : "Right";
                Vector3 S(float x, float y, float z) => new Vector3(side * x, y, z);
                Transform upper = Bone(prefix + "FrontUpper", chest, S(0.26f, 1.05f, 0.42f), S(0.3f, 0.55f, 0.37f), side);
                Transform lower = Bone(prefix + "FrontLower", upper, S(0.3f, 0.55f, 0.37f), S(0.41f, 0.18f, 0.6f), side);
                Bone(prefix + "FrontFoot", lower, S(0.41f, 0.18f, 0.6f), S(0.46f, 0.03f, 0.88f), side);
                upper = Bone(prefix + "HindUpper", hips, S(0.18f, 0.95f, -0.68f), S(0.28f, 0.45f, -0.8f), side);
                lower = Bone(prefix + "HindLower", upper, S(0.28f, 0.45f, -0.8f), S(0.38f, 0.18f, -0.92f), side);
                Bone(prefix + "HindFoot", lower, S(0.38f, 0.18f, -0.92f), S(0.52f, 0.03f, -0.72f), side);
                Bone(prefix + "Wing", chest, S(0.35f, 1.3f, 0f), S(2.5f, 1.6f, -0.35f), side);
            }
        }

        // Region rules keep the rigid parts apart; inside a region the nearest bone segments blend smoothly.
        private static void AssignWeights(Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices;
            var weights = new BoneWeight[vertices.Length];
            BoneInfo[] skinned = bones.Where(b => b.Skinned).ToArray();
            string[] names = skinned.Select(b => b.Transform.name).ToArray();
            bool[] wing = names.Select(n => n.EndsWith("Wing")).ToArray();
            bool[] leg = names.Select(n => n.Contains("Front") || n.Contains("Hind")).ToArray();
            bool[] tail = names.Select(n => n.StartsWith("Tail")).ToArray();
            int[] best = new int[4];
            float[] distances = new float[4];
            for (int v = 0; v < vertices.Length; v++)
            {
                Vector3 p = vertices[v];
                int side = p.x >= 0f ? 1 : -1;
                float ax = Mathf.Abs(p.x);
                // Wing claws hang lowest at the far tips, beyond any leg (the tail lies further back).
                bool onWing = (ax > 0.45f && p.y > 1.2f || ax > 0.6f && p.y > 0.75f || ax > 0.85f) && p.z > -0.7f && p.z < 0.4f;
                bool onHead = !onWing && p.y > 1.7f && ax < 0.45f && p.z > -0.2f;
                for (int j = 0; j < 4; j++) { best[j] = 0; distances[j] = float.PositiveInfinity; }
                for (int i = 0; i < skinned.Length; i++)
                {
                    BoneInfo bone = skinned[i];
                    string name = names[i];
                    if (bone.Side != 0 && bone.Side != side) continue;
                    if (onWing ? !wing[i] && name != "Chest" : wing[i]) continue;
                    if (onHead && name != "Head") continue;
                    if (leg[i] && p.y > 1.2f) continue;
                    if (tail[i] && p.z > -0.6f) continue;
                    if ((name == "Neck" || name == "Head") && (p.y < 1.15f || p.z < -0.15f)) continue;
                    Vector3 a = bone.Start, delta = bone.End - a;
                    float t = Mathf.Clamp01(Vector3.Dot(p - a, delta) / Mathf.Max(delta.sqrMagnitude, 0.00001f));
                    float d = (p - a - delta * t).sqrMagnitude;
                    for (int j = 0; j < 4; j++)
                    {
                        if (d >= distances[j]) continue;
                        for (int k = 3; k > j; k--) { distances[k] = distances[k - 1]; best[k] = best[k - 1]; }
                        distances[j] = d; best[j] = i; break;
                    }
                }
                float nearest = distances[0], sum = 0f;
                if (float.IsInfinity(nearest)) throw new InvalidOperationException("No eligible Dragon bone at vertex " + p);
                for (int j = 0; j < 4; j++)
                {
                    distances[j] = float.IsInfinity(distances[j]) ? 0f : Mathf.Exp(-(distances[j] - nearest) / 0.012f);
                    sum += distances[j];
                }
                weights[v] = new BoneWeight
                {
                    boneIndex0 = best[0], boneIndex1 = best[1], boneIndex2 = best[2], boneIndex3 = best[3],
                    weight0 = distances[0] / sum, weight1 = distances[1] / sum, weight2 = distances[2] / sum, weight3 = distances[3] / sum,
                };
            }
            mesh.boneWeights = weights;
        }

        private static Material FireMaterial()
        {
            Shader glow = Shader.Find(ArtStyleSetup.GlowShader);
            if (glow == null) throw new InvalidOperationException("Glow particle shader is missing.");
            var material = new Material(glow) { name = "DragonFire" };
            material.SetColor("_Color", new Color(1.6f, 0.85f, 0.4f));
            material.SetFloat("_Softness", 1.4f);
            return SaveAsset(material, RigFolder + "/DragonFire.mat");
        }

        // ---------- Generated clips ----------

        private static AnimationClip MakeClip(Transform root, string role, float duration, WrapMode wrap)
        {
            var clip = new AnimationClip { name = "Dragon_" + role, legacy = true, frameRate = 30, wrapMode = wrap };
            int samples = Mathf.RoundToInt(duration * 30);
            foreach (BoneInfo bone in bones)
            {
                Transform t = bone.Transform;
                string path = AnimationUtility.CalculateTransformPath(t, root);
                var curves = new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve(), new AnimationCurve() };
                var lift = new AnimationCurve();
                for (int i = 0; i <= samples; i++)
                {
                    float phase = (float)i / samples, time = phase * duration;
                    Quaternion q = Quaternion.Euler(Pose(t.name, bone.Side, role, phase));
                    curves[0].AddKey(time, q.x); curves[1].AddKey(time, q.y); curves[2].AddKey(time, q.z); curves[3].AddKey(time, q.w);
                    lift.AddKey(time, t.localPosition.y + Bob(role, phase));
                }
                for (int k = 0; k < 4; k++) clip.SetCurve(path, typeof(Transform), "m_LocalRotation." + "xyzw"[k], curves[k]);
                if (t.name == "Spine") clip.SetCurve(path, typeof(Transform), "m_LocalPosition.y", lift);
            }
            clip.EnsureQuaternionContinuity();
            return SaveAsset(clip, RigFolder + "/Dragon_" + role + ".anim");
        }

        private static float Bob(string role, float phase)
        {
            if (role == "Walk") return 0.035f * Mathf.Cos(phase * Mathf.PI * 4f);
            if (role == "Idle") return 0.012f * Mathf.Sin(phase * Mathf.PI * 2f);
            return -0.05f * Thrust(phase);
        }

        // Rears back briefly, then thrusts the head forward and down for the flame and recovers.
        private static float Rear(float phase) => phase < 0.12f ? Mathf.SmoothStep(0f, 1f, phase / 0.12f)
            : phase < 0.25f ? Mathf.SmoothStep(1f, 0f, (phase - 0.12f) / 0.13f) : 0f;
        private static float Thrust(float phase) => phase < 0.12f ? 0f : phase < 0.25f ? Mathf.SmoothStep(0f, 1f, (phase - 0.12f) / 0.13f)
            : phase < 0.7f ? 1f : Mathf.SmoothStep(1f, 0f, (phase - 0.7f) / 0.3f);

        // Local Euler (degrees) per bone. Bind rotations are identity, so axes are the unit's axes at rest:
        // +X pitch swings a hanging limb backward and tips the head down.
        private static Vector3 Pose(string name, int side, string role, float phase)
        {
            float tau = Mathf.PI * 2f;
            float wave = Mathf.Sin(phase * tau);
            Vector3 pose = Vector3.zero;
            if (name.EndsWith("Wing")) pose = new Vector3(0f, side * WingFold, 0f);
            if (role == "Idle")
            {
                if (name == "Chest") pose.x = 1.5f * wave;
                if (name == "Neck") pose = new Vector3(-2f * wave, 5f * Mathf.Sin((phase + 0.1f) * tau), 0f);
                if (name == "Head") pose = new Vector3(2f * wave, 4f * Mathf.Sin((phase + 0.35f) * tau), 0f);
                if (name.EndsWith("Wing")) pose.z = side * 1.5f * wave;
            }
            else if (role == "Walk")
            {
                // Diagonal trot: front-left with hind-right, front-right with hind-left.
                bool front = name.Contains("Front"), hind = name.Contains("Hind");
                if (front || hind)
                {
                    float offset = (front ? side < 0 : side > 0) ? 0f : 0.5f;
                    float swing = Mathf.Sin((phase + offset) * tau);
                    float raise = Mathf.Max(0f, Mathf.Cos((phase + offset) * tau));
                    float upper = (front ? -24f : -20f) * swing;
                    float lower = (front ? 40f : 35f) * raise;
                    if (name.EndsWith("Upper")) pose.x = upper;
                    if (name.EndsWith("Lower")) pose.x = lower;
                    if (name.EndsWith("Foot")) pose.x = -0.7f * (upper + lower);
                }
                if (name == "Spine") pose = new Vector3(1.5f * Mathf.Sin(phase * tau * 2f), 0f, 2f * wave);
                if (name == "Chest") pose.y = 3f * wave;
                if (name == "Neck") pose = new Vector3(4f * Mathf.Sin(phase * tau * 2f + 1f), -3f * wave, 0f);
                if (name == "Head") pose.x = -3f * Mathf.Sin(phase * tau * 2f + 1f);
                if (name.EndsWith("Wing")) pose.z = side * 2f * Mathf.Sin(phase * tau * 2f);
            }
            else
            {
                float rear = Rear(phase), thrust = Thrust(phase);
                if (name == "Chest") pose.x = -4f * rear + 5f * thrust;
                // Lunge forward with the snout only ~20 deg down so the flame reads as aimed ahead.
                if (name == "Neck") pose.x = -14f * rear + 16f * thrust;
                if (name == "Head") pose.x = -8f * rear + 2f * thrust;
                if (name.EndsWith("Wing")) pose = new Vector3(0f, side * (WingFold - 45f * thrust), side * 8f * thrust);
                if (name.StartsWith("Tail")) pose.x = 6f * thrust;
            }
            if (name.StartsWith("Tail") && role != "Breath")
            {
                int index = name[name.Length - 1] - '1';
                float amplitude = role == "Walk" ? 6f + 3f * index : 4f + 2f * index;
                pose.y = amplitude * Mathf.Sin(phase * tau - 0.7f * index);
            }
            return pose;
        }

        private static string ValidateRig(GameObject root, SkinnedMeshRenderer skin, params AnimationClip[] clips)
        {
            if (skin.sharedMesh.bindposes.Length != skin.bones.Length) throw new InvalidOperationException("Dragon skeleton/bind pose mismatch.");
            foreach (BoneWeight w in skin.sharedMesh.boneWeights)
                if (float.IsNaN(w.weight0) || Mathf.Abs(w.weight0 + w.weight1 + w.weight2 + w.weight3 - 1f) > 0.001f)
                    throw new InvalidOperationException("Unnormalized Dragon skin weights.");
            var rest = new Mesh();
            var posed = new Mesh();
            var lines = new List<string>();
            try
            {
                skin.BakeMesh(rest);
                Vector3[] restVertices = rest.vertices;
                Transform foot = Find("LeftFrontFoot"), mouth = Find("Head").Find("Mouth");
                Vector3 mouthRest = mouth.position;
                foreach (AnimationClip clip in clips)
                {
                    float largest = 0f;
                    Vector3 mouthLowest = mouthRest;
                    for (int i = 0; i <= 8; i++)
                    {
                        clip.SampleAnimation(root, clip.length * i / 8f);
                        if (mouth.position.y < mouthLowest.y) mouthLowest = mouth.position;
                        if (i != 3) continue;
                        skin.BakeMesh(posed);
                        Vector3[] moved = posed.vertices;
                        for (int k = 0; k < moved.Length; k++) largest = Mathf.Max(largest, (moved[k] - restVertices[k]).magnitude);
                    }
                    if (largest < 0.01f || largest > 4f) throw new InvalidOperationException("Invalid Dragon deformation in " + clip.name);
                    lines.Add($"{clip.name}: largest sampled vertex motion {largest:F3} m; mouth lowest {mouthLowest.y:F2} m (rest {mouthRest.y:F2}).");
                }
                // Walk must actually move the feet.
                AnimationClip walk = clips[1];
                walk.SampleAnimation(root, 0f);
                Vector3 a = foot.position;
                walk.SampleAnimation(root, walk.length * 0.5f);
                float stride = Vector3.Distance(a, foot.position);
                if (stride < 0.15f) throw new InvalidOperationException("Dragon walk does not move the feet.");
                lines.Add($"Walk: front foot stride {stride:F2} m between half cycles.");
                foreach (BoneInfo bone in bones) { bone.Transform.localRotation = Quaternion.identity; }
                Find("Spine").localPosition = new Vector3(0f, 1f, -0.25f);
                return string.Join("\n", lines) + "\n";
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rest);
                UnityEngine.Object.DestroyImmediate(posed);
            }
        }
    }
}

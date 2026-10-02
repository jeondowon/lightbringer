using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lightbringer.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lightbringer.EditorTools
{
    // Project-native prototype rig. The downloaded FBX remains an untouched source asset.
    [InitializeOnLoad]
    public static class SwordsmanRigSetup
    {
        private const string Folder = "Assets/Art/Characters/Swordsman";
        private const string RigFolder = Folder + "/Rigged";
        private const string Request = "Docs/Validation/SwordsmanGrip.request";
        private const string PrefabPath = RigFolder + "/Swordsman_Rigged.prefab";
        private static readonly List<Transform> bones = new List<Transform>();
        private static readonly List<Vector3> ends = new List<Vector3>();
        private static readonly Dictionary<string, Vector3> palms = new Dictionary<string, Vector3>();
        private static readonly Dictionary<string, int[]> palmVertices = new Dictionary<string, int[]>();
        private static readonly Quaternion SwordMountRotation = Quaternion.Euler(75f, 0f, 0f);

        static SwordsmanRigSetup() => EditorApplication.update += Poll;

        private static void Poll()
        {
            if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode
                || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            File.Delete(Request);
            try { Build(); }
            catch (Exception error) { Debug.LogException(error); }
        }

        [MenuItem("Lightbringer/Art/Build Swordsman Rig and Weapons")]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder(RigFolder)) AssetDatabase.CreateFolder(Folder, "Rigged");
            var library = AssetDatabase.LoadAssetAtPath<ArtStyleLibrary>(ArtStyleSetup.LibraryPath);
            if (library == null) throw new InvalidOperationException("Art style library is missing.");
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Swordsman.fbx");
            if (source == null) throw new InvalidOperationException("Swordsman source FBX is missing.");
            Scene scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Swordsman_Rigged");
            SceneManager.MoveGameObjectToScene(root, scene);
            var probe = UnityEngine.Object.Instantiate(source);
            SceneManager.MoveGameObjectToScene(probe, scene);
            try
            {
                probe.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(-90, 0, 0));
                probe.transform.localScale = Vector3.one;
                MeshFilter[] filters = probe.GetComponentsInChildren<MeshFilter>();
                if (filters.Length != 1 || filters[0].sharedMesh.subMeshCount != 1)
                    throw new InvalidOperationException("This prototype rig expects the inspected single-mesh Tripo export.");
                Bounds bounds = probe.GetComponentInChildren<Renderer>().bounds;
                float scale = 1.65f / bounds.size.y;
                probe.transform.localScale = Vector3.one * scale;
                probe.transform.position = -scale * new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                var mesh = new Mesh { name = "Swordsman_Skinned", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.CombineMeshes(new[] { new CombineInstance { mesh = filters[0].sharedMesh,
                    transform = filters[0].transform.localToWorldMatrix } }, true, true);
                bones.Clear(); ends.Clear();
                MakeSkeleton(root.transform, mesh.vertices);
                AssignWeights(mesh);
                mesh.bindposes = bones.Select(b => b.worldToLocalMatrix * root.transform.localToWorldMatrix).ToArray();
                mesh.RecalculateBounds();
                mesh = SaveAsset(mesh, RigFolder + "/Swordsman_Skinned.asset");
                var body = new GameObject("Body");
                body.transform.SetParent(root.transform, false);
                var skin = body.AddComponent<SkinnedMeshRenderer>();
                skin.sharedMesh = mesh;
                skin.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Swordsman.mat");
                skin.bones = bones.ToArray();
                skin.rootBone = bones[0];
                skin.quality = SkinQuality.Bone4;
                // Conservative animation envelope; weapons have their own renderers.
                skin.localBounds = new Bounds(new Vector3(0, 0.85f, 0), new Vector3(1.8f, 2.1f, 1.8f));
                skin.updateWhenOffscreen = false;
                BuildWeapons();
                AnimationClip idle = MakeClip(root.transform, "Idle", 2f);
                AnimationClip run = MakeClip(root.transform, "Run", 0.8f);
                AnimationClip attack = MakeClip(root.transform, "Slash", 0.65f);
                ValidateRig(root, skin, idle, run, attack);
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (prefab == null) throw new InvalidOperationException("Could not save Swordsman rig prefab.");
                Undo.RecordObject(library, "Assign rigged Swordsman");
                VisualOverride entry = library.FindOverride(VisualId.Swordsman);
                if (entry == null) throw new InvalidOperationException("Assign the Swordsman model first.");
                entry.prefab = prefab;
                entry.localOffset = Vector3.zero;
                entry.localEuler = Vector3.zero;
                entry.scale = 1f;
                entry.idleClip = idle;
                entry.moveClip = run;
                entry.actionClip = attack;
                entry.moveClipSpeed = 3f;
                entry.attachments = new PropAttachment[0]; // Weapons are children of hand bones in this prefab.
                EditorUtility.SetDirty(library);
                AssetDatabase.SaveAssets();
                Debug.Log("Rigged Swordsman assigned: weighted skeleton, sword, shield, idle/run/slash. Restart Play to use it.");
            }
            finally
            {
                bones.Clear(); ends.Clear();
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [MenuItem("Lightbringer/Art/Build Swordsman Rig and Weapons", true)]
        private static bool CanBuild() => !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling;

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
            Transform hips = Bone("Hips", root, new Vector3(0, .87f, 0), new Vector3(0, .98f, 0));
            Transform spine = Bone("Spine", hips, new Vector3(0, .98f, 0), new Vector3(0, 1.12f, 0));
            Transform chest = Bone("Chest", spine, new Vector3(0, 1.12f, 0), new Vector3(0, 1.29f, 0));
            Transform neck = Bone("Neck", chest, new Vector3(0, 1.29f, 0), new Vector3(0, 1.36f, 0));
            Bone("Head", neck, new Vector3(0, 1.36f, 0), new Vector3(0, 1.62f, 0));
            foreach (int sign in new[] { 1, -1 })
            {
                string side = sign == 1 ? "Right" : "Left";
                Vector3 shoulderPoint = Landmark(vertices, sign, 1.205f, 1.25f, .18f, out _);
                Vector3 elbow = Landmark(vertices, sign, 1.045f, 1.09f, .28f, out _);
                Vector3 wrist = Landmark(vertices, sign, .925f, .95f, .34f, out _);
                Vector3 palm = Landmark(vertices, sign, .86f, .90f, .355f, out int[] indices);
                palms[side] = palm;
                palmVertices[side] = indices;
                Transform shoulder = Bone(side + "Shoulder", chest, new Vector3(sign * .12f, 1.23f, shoulderPoint.z), shoulderPoint);
                Transform upper = Bone(side + "UpperArm", shoulder, shoulderPoint, elbow);
                Transform fore = Bone(side + "ForeArm", upper, elbow, wrist);
                Bone(side + "Hand", fore, wrist, palm + (palm - wrist).normalized * .035f);
                Transform thigh = Bone(side + "Thigh", hips, new Vector3(sign * .12f, .86f, 0), new Vector3(sign * .16f, .43f, .015f));
                Transform shin = Bone(side + "Shin", thigh, new Vector3(sign * .16f, .43f, .015f), new Vector3(sign * .18f, .10f, 0));
                Bone(side + "Foot", shin, new Vector3(sign * .18f, .10f, 0), new Vector3(sign * .18f, .045f, .16f));
            }
            Bone("Tabard", hips, new Vector3(0, .82f, .09f), new Vector3(0, .48f, .11f));
            Bone("Cape", chest, new Vector3(0, 1.20f, -.10f), new Vector3(0, .50f, -.15f));
        }

        private static Vector3 Landmark(Vector3[] vertices, int sign, float minY, float maxY, float minX, out int[] indices)
        {
            var matches = new List<int>();
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 p = vertices[i];
                if (p.x * sign < minX || p.y < minY || p.y > maxY) continue;
                sum += p; matches.Add(i);
            }
            if (matches.Count == 0) throw new InvalidOperationException("Cannot locate Swordsman arm landmark.");
            indices = matches.ToArray();
            return sum / matches.Count;
        }

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
                bool arm = Mathf.Abs(p.x) > (p.y > 1.15f ? .18f : .235f) && p.y > .80f && p.y < 1.34f;
                int handIndex = p.x >= 0 ? rightHand : leftHand;
                // Keep the palm and its socket on exactly the same transform. Blend the wrist above it.
                if (arm && p.y < starts[handIndex].y - .025f)
                {
                    weights[v] = new BoneWeight { boneIndex0 = handIndex, weight0 = 1f };
                    continue;
                }
                bool cape = !arm && p.y > .43f && p.y < 1.19f && p.z < -.125f;
                bool tabard = !arm && !cape && p.y > .43f && p.y < .86f && p.z > .08f;
                for (int j = 0; j < 4; j++) { best[j] = 0; distances[j] = float.PositiveInfinity; }
                for (int i = 0; i < bones.Count; i++)
                {
                    string name = names[i];
                    if (sides[i] != 0 && sides[i] != (p.x >= 0 ? 1 : -1)) continue;
                    bool isArm = armBones[i];
                    bool isLeg = legBones[i];
                    if (arm && !isArm && name != "Chest") continue;
                    if (!arm && isArm) continue;
                    if (cape && name != "Cape" && name != "Hips" && name != "Chest") continue;
                    if (tabard && name != "Tabard" && name != "Hips") continue;
                    if (!cape && name == "Cape" || !tabard && name == "Tabard") continue;
                    if (!arm && !cape && !tabard && (p.y < .82f ? !isLeg && name != "Hips" : isLeg)) continue;
                    if (p.y > 1.37f && name != "Head") continue;
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
                    // Compact support keeps armor mostly rigid while blending across the nearest joint.
                    distances[j] = float.IsInfinity(distances[j]) ? 0 : Mathf.Exp(-(distances[j] - nearest) / .006f);
                    sum += distances[j];
                }
                weights[v] = new BoneWeight { boneIndex0 = best[0], boneIndex1 = best[1], boneIndex2 = best[2], boneIndex3 = best[3],
                    weight0 = distances[0] / sum, weight1 = distances[1] / sum, weight2 = distances[2] / sum, weight3 = distances[3] / sum };
            }
            mesh.boneWeights = weights;
        }

        private static void BuildWeapons()
        {
            Material steel = MakeMaterial("SwordSteel", new Color(.68f, .75f, .82f), .8f, .55f);
            Material gold = MakeMaterial("WeaponGold", new Color(.78f, .53f, .18f), .65f, .4f);
            Material leather = MakeMaterial("SwordGrip", new Color(.13f, .075f, .045f), 0, .25f);
            Material blue = MakeMaterial("ShieldBlue", new Color(.045f, .11f, .3f), .25f, .35f);
            Transform hand = bones.First(b => b.name == "RightHand");
            var sword = new GameObject("Sword").transform;
            sword.SetParent(hand, false);
            sword.localRotation = SwordMountRotation;
            sword.localPosition = hand.InverseTransformPoint(palms["Right"])
                - SwordMountRotation * new Vector3(0, -.015f, 0);
            Part(sword, "Grip", PrimitiveType.Cylinder, new Vector3(0, -.015f, 0), new Vector3(.03f, .065f, .03f), leather);
            Part(sword, "Pommel", PrimitiveType.Sphere, new Vector3(0, -.085f, 0), Vector3.one * .045f, gold);
            Part(sword, "Guard", PrimitiveType.Cube, new Vector3(0, .055f, 0), new Vector3(.20f, .028f, .04f), gold);
            var bladeMesh = new Mesh { name = "SwordsmanBlade" };
            bladeMesh.vertices = new[] { new Vector3(-.035f,.07f,0), new Vector3(0,.07f,.014f), new Vector3(.035f,.07f,0), new Vector3(0,.07f,-.014f),
                new Vector3(-.025f,.62f,0), new Vector3(0,.62f,.01f), new Vector3(.025f,.62f,0), new Vector3(0,.62f,-.01f), new Vector3(0,.76f,0) };
            var triangles = new List<int>();
            for (int i = 0; i < 4; i++) { int j = (i + 1) % 4; triangles.AddRange(new[] { i, i+4, j, j, i+4, j+4, i+4, 8, j+4 }); }
            triangles.Reverse();
            bladeMesh.triangles = triangles.ToArray(); bladeMesh.RecalculateNormals(); bladeMesh.RecalculateBounds();
            bladeMesh = SaveAsset(bladeMesh, RigFolder + "/SwordBlade.asset");
            var blade = new GameObject("Blade"); blade.transform.SetParent(sword, false);
            blade.AddComponent<MeshFilter>().sharedMesh = bladeMesh;
            blade.AddComponent<MeshRenderer>().sharedMaterial = steel;
            Transform shieldHand = bones.First(b => b.name == "LeftHand");
            var shield = new GameObject("Shield").transform;
            shield.SetParent(shieldHand, false);
            shield.localPosition = shieldHand.InverseTransformPoint(palms["Left"]) + Vector3.forward * .055f;
            Part(shield, "Rim", PrimitiveType.Cylinder, Vector3.zero, new Vector3(.42f, .022f, .42f), gold).localRotation = Quaternion.Euler(90,0,0);
            Part(shield, "Face", PrimitiveType.Cylinder, new Vector3(0,0,.022f), new Vector3(.38f,.008f,.38f), blue).localRotation = Quaternion.Euler(90,0,0);
            Part(shield, "Boss", PrimitiveType.Sphere, new Vector3(0,0,.045f), new Vector3(.12f,.12f,.065f), gold);
        }

        private static Transform Part(Transform parent, string name, PrimitiveType shape, Vector3 position, Vector3 scale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(shape);
            part.name = name; part.transform.SetParent(parent, false); part.transform.localPosition = position; part.transform.localScale = scale;
            UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
            part.GetComponent<Renderer>().sharedMaterial = material;
            return part.transform;
        }

        private static Material MakeMaterial(string name, Color color, float metallic, float smoothness)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            material.SetColor("_BaseColor", color); material.SetFloat("_Metallic", metallic); material.SetFloat("_Smoothness", smoothness);
            return SaveAsset(material, RigFolder + "/" + name + ".mat");
        }

        private static AnimationClip MakeClip(Transform root, string role, float duration)
        {
            var clip = new AnimationClip { name = "Swordsman_" + role, legacy = true, frameRate = 30,
                wrapMode = role == "Slash" ? WrapMode.Once : WrapMode.Loop };
            foreach (Transform bone in bones)
            {
                string path = AnimationUtility.CalculateTransformPath(bone, root);
                var curves = new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve(), new AnimationCurve() };
                var lift = new AnimationCurve();
                int samples = Mathf.RoundToInt(duration * 30);
                for (int i = 0; i <= samples; i++)
                {
                    float phase = (float)i / samples;
                    Quaternion q = HandRotation(bone.name, role, phase);
                    float time = phase * duration;
                    curves[0].AddKey(time,q.x); curves[1].AddKey(time,q.y); curves[2].AddKey(time,q.z); curves[3].AddKey(time,q.w);
                    float bob = role == "Run" ? .018f * (1f - Mathf.Cos(phase * Mathf.PI * 4f)) : .006f * Mathf.Sin(phase * Mathf.PI * 2f);
                    lift.AddKey(time, bone.localPosition.y + bob);
                }
                for (int k = 0; k < 4; k++) clip.SetCurve(path, typeof(Transform), "m_LocalRotation." + "xyzw"[k], curves[k]);
                if (bone.name == "Hips") clip.SetCurve(path, typeof(Transform), "m_LocalPosition.y", lift);
            }
            clip.EnsureQuaternionContinuity();
            return SaveAsset(clip, RigFolder + "/Swordsman_" + role + ".anim");
        }

        private static Vector3 Pose(string name, string role, float phase)
        {
            float wave = Mathf.Sin(phase * Mathf.PI * 2f);
            float side = name.StartsWith("Right") ? 1f : -1f;
            Vector3 pose = Vector3.zero;
            if (name.EndsWith("UpperArm")) pose = new Vector3(-12, 0, side * -12);
            if (name.EndsWith("ForeArm")) pose = new Vector3(-32, 0, side * 6);
            if (name == "Chest") pose.x = wave * 1.5f;
            if (role == "Run")
            {
                if (name.EndsWith("Thigh")) pose.x = -side * wave * 23;
                if (name.EndsWith("Shin")) pose.x = Mathf.Max(0, side * wave) * 32;
                if (name.EndsWith("Foot")) pose.x = -Mathf.Max(0, side * wave) * 12;
                if (name.EndsWith("UpperArm")) pose.x += side * wave * 12;
                if (name == "Chest") pose = new Vector3(5, wave * 3, 0);
                if (name == "Tabard") pose.x = 7 + wave * 5;
                if (name == "Cape") pose.x = -8 + wave * 4;
            }
            if (role == "Slash")
            {
                // A short wind-up, diagonal cut, then recovery. Damage remains owned by UnitCombat.
                float raise = phase < .22f ? Mathf.SmoothStep(0,1,phase/.22f)
                    : phase < .52f ? Mathf.SmoothStep(1,0,(phase-.22f)/.30f) : 0;
                float cut = phase < .22f ? 0 : phase < .52f ? Mathf.SmoothStep(0,1,(phase-.22f)/.30f)
                    : Mathf.SmoothStep(1,0,(phase-.52f)/.48f);
                if (name == "RightUpperArm") pose += new Vector3(-63 * raise - 23 * cut, -8 * raise + 8 * cut, -12 * raise + 12 * cut);
                if (name == "RightForeArm") pose.x += -13 * raise + 22 * cut;
                if (name == "Chest") pose = new Vector3(4 * cut, -8 * raise + 12 * cut, 0);
                if (name == "LeftForeArm") pose.x -= 12 * (raise + cut);
            }
            return pose;
        }

        private static Quaternion HandRotation(string name, string role, float phase)
        {
            if (name != "RightHand" && name != "LeftHand") return Quaternion.Euler(Pose(name, role, phase));
            string side = name == "RightHand" ? "Right" : "Left";
            Quaternion parent = Quaternion.identity;
            foreach (string ancestor in new[] { "Hips", "Spine", "Chest", side + "Shoulder", side + "UpperArm", side + "ForeArm" })
                parent *= Quaternion.Euler(Pose(ancestor, role, phase));
            // The shield faces the enemy, and the blade pitches through +Z instead of behind the soldier.
            if (side == "Left") return Quaternion.Inverse(parent);
            float pitch = 25f, roll = 0f;
            if (role == "Slash")
            {
                float raise = phase < .22f ? Mathf.SmoothStep(0,1,phase/.22f)
                    : phase < .52f ? Mathf.SmoothStep(1,0,(phase-.22f)/.30f) : 0;
                float cut = phase < .22f ? 0 : phase < .52f ? Mathf.SmoothStep(0,1,(phase-.22f)/.30f)
                    : Mathf.SmoothStep(1,0,(phase-.52f)/.48f);
                pitch += -30f * raise + 70f * cut;
                roll = -15f * raise + 15f * cut;
            }
            return Quaternion.Inverse(parent) * Quaternion.Euler(pitch, 0, roll) * Quaternion.Inverse(SwordMountRotation);
        }

        private static T SaveAsset<T>(T value, string path) where T : UnityEngine.Object
        {
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null) { AssetDatabase.CreateAsset(value, path); return value; }
            EditorUtility.CopySerialized(value, existing); UnityEngine.Object.DestroyImmediate(value); EditorUtility.SetDirty(existing); return existing;
        }

        private static void ValidateRig(GameObject root, SkinnedMeshRenderer skin, params AnimationClip[] clips)
        {
            if (skin.bones.Length < 19 || skin.sharedMesh.bindposes.Length != skin.bones.Length)
                throw new InvalidOperationException("Skeleton/bind pose mismatch.");
            foreach (BoneWeight w in skin.sharedMesh.boneWeights)
                if (float.IsNaN(w.weight0) || Mathf.Abs(w.weight0+w.weight1+w.weight2+w.weight3-1) > .001f)
                    throw new InvalidOperationException("Unnormalized skin weights.");
            var rest = new Mesh(); var posed = new Mesh();
            try
            {
                skin.BakeMesh(rest);
                Vector3[] vertices = rest.vertices;
                Transform hand = bones.First(b => b.name == "RightHand");
                Transform sword = hand.Find("Sword");
                if (sword == null || bones.First(b => b.name == "LeftHand").Find("Shield") == null)
                    throw new InvalidOperationException("Weapons are not attached to hand bones.");
                var results = new List<string>();
                foreach (AnimationClip clip in clips)
                {
                    foreach (Transform bone in bones) bone.localRotation = Quaternion.identity;
                    bones[0].localPosition = new Vector3(0,.87f,0);
                    clip.SampleAnimation(root, clip.length * .3f);
                    skin.BakeMesh(posed);
                    float largest = 0; Vector3[] changed = posed.vertices;
                    for (int i = 0; i < changed.Length; i++) largest = Mathf.Max(largest, (changed[i]-vertices[i]).magnitude);
                    if (largest < .001f || largest > 1.5f) throw new InvalidOperationException("Invalid deformation in " + clip.name);
                    results.Add(clip.name + ": sampled weighted vertex motion " + largest.ToString("F3") + " m");
                    clip.SampleAnimation(root, clip.length * .5f);
                    skin.BakeMesh(posed);
                    Vector3[] gripPose = posed.vertices;
                    Vector3 rightPalm = Vector3.zero, leftPalm = Vector3.zero;
                    foreach (int index in palmVertices["Right"]) rightPalm += skin.transform.TransformPoint(gripPose[index]);
                    foreach (int index in palmVertices["Left"]) leftPalm += skin.transform.TransformPoint(gripPose[index]);
                    rightPalm /= palmVertices["Right"].Length;
                    leftPalm /= palmVertices["Left"].Length;
                    float gripError = Vector3.Distance(rightPalm, sword.TransformPoint(new Vector3(0, -.015f, 0)));
                    Transform shield = bones.First(b => b.name == "LeftHand").Find("Shield");
                    float shieldFront = Vector3.Dot(shield.position - leftPalm, root.transform.forward);
                    if (gripError > .015f || shieldFront < .04f || Vector3.Dot(shield.forward, root.transform.forward) < .98f)
                        throw new InvalidOperationException("Weapon/palm alignment failed for " + clip.name);
                    results.Add(clip.name + $": grip error {gripError:F4} m; shield ahead of palm {shieldFront:F3} m.");
                    if (clip.name.EndsWith("Slash"))
                    {
                        float forwardReach = Vector3.Dot(sword.TransformPoint(new Vector3(0,.76f,0)) - rightPalm, root.transform.forward);
                        if (forwardReach < .55f) throw new InvalidOperationException("Sword does not cut into the forward hemisphere.");
                        results.Add($"Slash: sword tip {forwardReach:F3} m in front of palm at contact pose.");
                    }
                }
                foreach (Transform bone in bones) bone.localRotation = Quaternion.identity;
                bones[0].localPosition = new Vector3(0,.87f,0);
                Directory.CreateDirectory("Docs/Validation");
                File.WriteAllText("Docs/Validation/SwordsmanRigChecks.txt", "PASS | " + DateTime.UtcNow.ToString("O")
                    + "\nUnity-native prototype skeleton: " + bones.Count + " bones; " + vertices.Length + " weighted vertices.\n"
                    + string.Join("\n",results) + "\nSword: RightHand; shield: LeftHand. Original FBX preserved.\n"
                    + "No Mixamo upload or external animation assets. Hand-tuned landmark weights and generated prototype clips.\n"
                    + "Visual review and joint/cloth polish remain manual; no mesh decimation performed.\n");
            }
            finally { UnityEngine.Object.DestroyImmediate(rest); UnityEngine.Object.DestroyImmediate(posed); }
        }
    }
}

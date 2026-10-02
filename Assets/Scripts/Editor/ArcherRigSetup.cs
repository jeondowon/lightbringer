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
    // Project-native prototype rig for the Archer, following SwordsmanRigSetup. The shadow-hood mesh stays an
    // untouched source; a skinned copy, a primitive bow with string and nocked arrow, and a quiver are generated.
    [InitializeOnLoad]
    public static class ArcherRigSetup
    {
        private const string Folder = "Assets/Art/Characters/Archer";
        private const string RigFolder = Folder + "/Rigged";
        private const string SourceMesh = Folder + "/ShadowHood/Archer_ShadowHood.asset";
        private const string Request = "Docs/Validation/ArcherRig.request";
        private const string PrefabPath = RigFolder + "/Archer_Rigged.prefab";
        private const float BowHalf = .5f, Brace = .11f, ArrowLength = .5f;
        // Full-draw anchor under the right jaw, just in front of the hood (root space; the archer faces +Z).
        private static readonly Vector3 Anchor = new Vector3(.07f, 1.38f, .2f);
        private static readonly Vector3 ArrowRest = new Vector3(-.018f, .025f, 0f);
        private static readonly Vector3 RestNock = new Vector3(0f, 0f, -Brace);
        // |x| above which a vertex belongs to the arm, sampled from the A-pose mesh between fingertips and armpit.
        private static readonly float[] BoundaryY = { .76f, .86f, .94f, 1.02f, 1.06f, 1.10f, 1.18f };
        private static readonly float[] BoundaryX = { .31f, .275f, .245f, .19f, .15f, .12f, .115f };
        private static readonly List<Transform> bones = new List<Transform>();
        private static readonly List<Vector3> ends = new List<Vector3>();
        private static readonly Dictionary<string, Transform> byName = new Dictionary<string, Transform>();
        private static readonly Dictionary<string, Vector3> bind = new Dictionary<string, Vector3>();
        private static readonly Dictionary<string, Vector3> palms = new Dictionary<string, Vector3>();
        private static readonly Dictionary<string, int[]> palmVertices = new Dictionary<string, int[]>();
        private static Transform bow, limbs, upperString, lowerString, arrow;
        private static Vector3 hipsRest, gripFar, gripNear;
        private static float drawLength;
        // Maps the bow hand's pointing axis onto the arrow axis, so the fist stays in line with the forearm.
        private static Quaternion bowGrip;

        static ArcherRigSetup() => EditorApplication.update += Poll;

        private static void Poll()
        {
            if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode
                || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            File.Delete(Request);
            try { Build(); }
            catch (Exception error) { Debug.LogException(error); }
        }

        [MenuItem("Lightbringer/Art/Build Archer Rig and Bow")]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder(RigFolder)) AssetDatabase.CreateFolder(Folder, "Rigged");
            var library = AssetDatabase.LoadAssetAtPath<ArtStyleLibrary>(ArtStyleSetup.LibraryPath);
            if (library == null) throw new InvalidOperationException("Art style library is missing.");
            var source = AssetDatabase.LoadAssetAtPath<Mesh>(SourceMesh);
            var body = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/FaceRepair/Archer_FaceFixed.mat");
            var hood = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/ShadowHood/HoodInterior.mat");
            if (source == null || body == null || hood == null)
                throw new InvalidOperationException("Archer shadow-hood mesh and materials are required. Run Build Archer Shadow Hood first.");
            Scene scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Archer_Rigged");
            SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                Clear();
                // Already upright, 1.65 m tall, feet at y = 0 and facing +Z (see ArcherFaceRepair.Inspect).
                Mesh mesh = UnityEngine.Object.Instantiate(source);
                mesh.name = "Archer_Skinned";
                MakeSkeleton(root.transform, mesh.vertices);
                AssignWeights(mesh);
                mesh.bindposes = bones.Select(b => b.worldToLocalMatrix * root.transform.localToWorldMatrix).ToArray();
                mesh.RecalculateBounds();
                mesh = SaveAsset(mesh, RigFolder + "/Archer_Skinned.asset");
                var bodyObject = new GameObject("Body");
                bodyObject.transform.SetParent(root.transform, false);
                var skin = bodyObject.AddComponent<SkinnedMeshRenderer>();
                skin.sharedMesh = mesh;
                skin.sharedMaterials = new[] { body, hood };
                skin.bones = bones.ToArray();
                skin.rootBone = bones[0];
                skin.quality = SkinQuality.Bone4;
                // Conservative envelope around the hips; the bow and quiver have their own renderers.
                skin.localBounds = new Bounds(Vector3.zero, Vector3.one * 2.4f);
                skin.updateWhenOffscreen = false;
                BuildProps();
                PrepareShot();
                AnimationClip idle = MakeClip(root.transform, "Idle", 2f);
                AnimationClip run = MakeClip(root.transform, "Run", .8f);
                AnimationClip shoot = MakeClip(root.transform, "Shoot", 1f);
                bool pass = ValidateRig(root, skin, idle, run, shoot);
                ApplyPose("Idle", 0f);
                try { RenderPreview(root, idle, run, shoot); }
                catch (Exception error) { Debug.LogWarning("Archer preview skipped: " + error.Message); }
                if (!pass) throw new InvalidOperationException("Archer rig validation failed. See Docs/Validation/ArcherRigChecks.txt.");
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (prefab == null) throw new InvalidOperationException("Could not save Archer rig prefab.");
                Undo.RecordObject(library, "Assign rigged Archer");
                VisualOverride entry = library.FindOverride(VisualId.Archer);
                if (entry == null) throw new InvalidOperationException("Assign the Archer model first.");
                entry.prefab = prefab;
                entry.localOffset = Vector3.zero;
                entry.localEuler = Vector3.zero;
                entry.scale = 1f;
                entry.idleClip = idle;
                entry.moveClip = run;
                entry.actionClip = shoot;
                entry.moveClipSpeed = 3f;
                entry.attachments = new PropAttachment[0]; // Bow and quiver are children of bones in this prefab.
                EditorUtility.SetDirty(library);
                AssetDatabase.SaveAssets();
                Debug.Log("Rigged Archer assigned: weighted skeleton, bow, nocked arrow, quiver, idle/run/shoot. Restart Play to use it.");
            }
            finally
            {
                Clear();
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [MenuItem("Lightbringer/Art/Build Archer Rig and Bow", true)]
        private static bool CanBuild() => !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling;

        private static void Clear()
        {
            bones.Clear(); ends.Clear(); byName.Clear(); bind.Clear(); palms.Clear(); palmVertices.Clear();
            bow = limbs = upperString = lowerString = arrow = null;
        }

        private static Transform Bone(string name, Transform parent, Vector3 point, Vector3 end)
        {
            var bone = new GameObject(name).transform;
            bone.SetParent(parent, false);
            bone.position = point;
            bones.Add(bone); ends.Add(end);
            byName[name] = bone; bind[name] = point;
            return bone;
        }

        private static void MakeSkeleton(Transform root, Vector3[] vertices)
        {
            Transform hips = Bone("Hips", root, new Vector3(0, .86f, .05f), new Vector3(0, .98f, .05f));
            Transform spine = Bone("Spine", hips, new Vector3(0, .98f, .05f), new Vector3(0, 1.12f, .05f));
            Transform chest = Bone("Chest", spine, new Vector3(0, 1.12f, .05f), new Vector3(0, 1.3f, .05f));
            Transform neck = Bone("Neck", chest, new Vector3(0, 1.3f, .05f), new Vector3(0, 1.38f, .06f));
            Bone("Head", neck, new Vector3(0, 1.38f, .06f), new Vector3(0, 1.62f, .06f));
            hipsRest = hips.localPosition;
            foreach (int sign in new[] { 1, -1 })
            {
                string side = sign == 1 ? "Right" : "Left";
                Vector3 shoulderPoint = Landmark(vertices, sign, 1.22f, 1.27f, .15f, out _);
                Vector3 wrist = Landmark(vertices, sign, .95f, .99f, .25f, out _);
                Vector3 palm = Landmark(vertices, sign, .85f, .89f, .31f, out int[] indices);
                // The A-pose arm is straight; the puffed sleeve would pull a landmark elbow inwards.
                Vector3 elbow = Vector3.Lerp(shoulderPoint, wrist, .47f);
                palms[side] = palm;
                palmVertices[side] = indices;
                Transform shoulder = Bone(side + "Shoulder", chest, new Vector3(sign * .07f, 1.25f, shoulderPoint.z), shoulderPoint);
                Transform upper = Bone(side + "UpperArm", shoulder, shoulderPoint, elbow);
                Transform fore = Bone(side + "ForeArm", upper, elbow, wrist);
                Bone(side + "Hand", fore, wrist, palm + (palm - wrist).normalized * .035f);
                // Leg axes measured from cross-sections inside the long coat.
                Vector3 hip = new Vector3(sign * .09f, .84f, .1f), knee = new Vector3(sign * .12f, .46f, .07f);
                Vector3 ankle = new Vector3(sign * .14f, .11f, 0f), toe = new Vector3(sign * .15f, .03f, .12f);
                Transform thigh = Bone(side + "Thigh", hips, hip, knee);
                Transform shin = Bone(side + "Shin", thigh, knee, ankle);
                Bone(side + "Foot", shin, ankle, toe);
            }
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
            if (matches.Count == 0) throw new InvalidOperationException("Cannot locate Archer arm landmark.");
            indices = matches.ToArray();
            return sum / matches.Count;
        }

        private static bool IsArm(Vector3 p)
        {
            float x = Mathf.Abs(p.x);
            if (p.y < BoundaryY[0] || p.y > 1.34f) return false;
            // Above the armpit the shoulder cap and capelet edge blend between chest, shoulder and upper arm.
            if (p.y >= BoundaryY[BoundaryY.Length - 1]) return x > .135f;
            int i = 1;
            while (p.y > BoundaryY[i]) i++;
            return x > Mathf.Lerp(BoundaryX[i - 1], BoundaryX[i], Mathf.InverseLerp(BoundaryY[i - 1], BoundaryY[i], p.y));
        }

        private static float SegmentDistance(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 delta = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, delta) / Mathf.Max(delta.sqrMagnitude, .00001f));
            return (p - a - delta * t).magnitude;
        }

        private static void AssignWeights(Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices;
            var weights = new BoneWeight[vertices.Length];
            string[] names = bones.Select(b => b.name).ToArray();
            Vector3[] starts = bones.Select(b => b.position).ToArray();
            int[] sides = names.Select(n => n.StartsWith("Right") ? 1 : n.StartsWith("Left") ? -1 : 0).ToArray();
            bool[] armBones = names.Select(n => n.EndsWith("Arm") || n.EndsWith("Hand")).ToArray();
            bool[] shoulderBones = names.Select(n => n.EndsWith("Shoulder")).ToArray();
            bool[] legBones = names.Select(n => n.EndsWith("Thigh") || n.EndsWith("Shin") || n.EndsWith("Foot")).ToArray();
            int hips = Array.IndexOf(names, "Hips");
            int rightHand = Array.IndexOf(names, "RightHand"), leftHand = Array.IndexOf(names, "LeftHand");
            int rightThigh = Array.IndexOf(names, "RightThigh"), leftThigh = Array.IndexOf(names, "LeftThigh");
            int[] best = new int[4]; float[] distances = new float[4];
            for (int v = 0; v < vertices.Length; v++)
            {
                Vector3 p = vertices[v];
                int side = p.x >= 0 ? 1 : -1;
                string prefix = side == 1 ? "Right" : "Left";
                bool arm = IsArm(p);
                int handIndex = side == 1 ? rightHand : leftHand;
                // Keep the palm and the bow grip on exactly the same transform. Blend the wrist above it.
                if (arm && p.y < starts[handIndex].y - .015f)
                {
                    weights[v] = new BoneWeight { boneIndex0 = handIndex, weight0 = 1f };
                    continue;
                }
                bool lower = !arm && p.y < .86f;
                bool leg = lower && (p.y < .39f || Mathf.Min(
                    SegmentDistance(p, bind[prefix + "Thigh"], bind[prefix + "Shin"]),
                    SegmentDistance(p, bind[prefix + "Shin"], bind[prefix + "Foot"]),
                    SegmentDistance(p, bind[prefix + "Foot"], ends[bones.IndexOf(byName[prefix + "Foot"])])) < .09f);
                if (lower && !leg)
                {
                    // Coat panels follow the nearer thigh more towards the hem, so a stride never punches through.
                    float follow = .55f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.84f, .42f, p.y));
                    float right = Mathf.Clamp01(.5f + p.x / .16f);
                    weights[v] = new BoneWeight { boneIndex0 = hips, weight0 = 1f - follow,
                        boneIndex1 = rightThigh, weight1 = follow * right, boneIndex2 = leftThigh, weight2 = follow * (1f - right) };
                    continue;
                }
                for (int j = 0; j < 4; j++) { best[j] = 0; distances[j] = float.PositiveInfinity; }
                for (int i = 0; i < bones.Count; i++)
                {
                    if (sides[i] != 0 && sides[i] != side) continue;
                    string name = names[i];
                    if (leg) { if (!legBones[i] && !(i == hips && p.y > .74f)) continue; }
                    else if (legBones[i]) continue;
                    else if (arm) { if (!armBones[i] && !shoulderBones[i] && name != "Chest") continue; }
                    else if (armBones[i] || shoulderBones[i] && p.y < 1.12f) continue;
                    else if (name == "Head" ? p.y < 1.3f : p.y > 1.42f) continue;
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
                    // Compact support keeps the leather and cloth mostly rigid while blending across joints.
                    distances[j] = float.IsInfinity(distances[j]) ? 0 : Mathf.Exp(-(distances[j] - nearest) / .006f);
                    sum += distances[j];
                }
                weights[v] = new BoneWeight { boneIndex0 = best[0], boneIndex1 = best[1], boneIndex2 = best[2], boneIndex3 = best[3],
                    weight0 = distances[0] / sum, weight1 = distances[1] / sum, weight2 = distances[2] / sum, weight3 = distances[3] / sum };
            }
            mesh.boneWeights = weights;
        }

        private static void BuildProps()
        {
            Material wood = MakeMaterial("BowWood", new Color(.32f, .18f, .08f), 0f, .35f);
            Material leather = MakeMaterial("ArcherLeather", new Color(.13f, .075f, .045f), 0f, .25f);
            Material gold = MakeMaterial("ArcherGold", new Color(.78f, .53f, .18f), .65f, .4f);
            Material cord = MakeMaterial("BowString", new Color(.86f, .83f, .74f), 0f, .2f);
            Material shaft = MakeMaterial("ArrowShaft", new Color(.62f, .48f, .3f), 0f, .3f);
            Material steel = MakeMaterial("ArrowHead", new Color(.68f, .75f, .82f), .8f, .55f);
            Material fletch = MakeMaterial("ArrowFletch", new Color(.12f, .22f, .55f), 0f, .2f);

            Vector3 palmOffset = palms["Left"] - bind["LeftHand"];
            bowGrip = Quaternion.FromToRotation(palmOffset.normalized, Vector3.forward);
            bow = new GameObject("Bow").transform;
            bow.SetParent(byName["LeftHand"], false);
            bow.localPosition = palmOffset;
            bow.localRotation = Quaternion.Inverse(bowGrip);
            limbs = new GameObject("Limbs").transform;
            limbs.SetParent(bow, false);
            limbs.gameObject.AddComponent<MeshFilter>().sharedMesh = SaveAsset(LimbMesh(), RigFolder + "/BowLimbs.asset");
            limbs.gameObject.AddComponent<MeshRenderer>().sharedMaterial = wood;
            Part(limbs, "TipTop", PrimitiveType.Sphere, new Vector3(0, BowHalf, -Brace), Vector3.one * .028f, gold);
            Part(limbs, "TipBottom", PrimitiveType.Sphere, new Vector3(0, -BowHalf, -Brace), Vector3.one * .028f, gold);
            Part(bow, "Grip", PrimitiveType.Cylinder, Vector3.zero, new Vector3(.06f, .07f, .05f), leather);
            upperString = Part(bow, "StringUpper", PrimitiveType.Cylinder, Vector3.zero, Vector3.one, cord);
            lowerString = Part(bow, "StringLower", PrimitiveType.Cylinder, Vector3.zero, Vector3.one, cord);
            arrow = new GameObject("NockedArrow").transform;
            arrow.SetParent(bow, false);
            BuildArrow(arrow, shaft, steel, fletch);

            // Greybox quiver: reads as "archer" from the gameplay camera even though the concept art has none.
            var quiver = new GameObject("Quiver").transform;
            quiver.SetParent(byName["Chest"], false);
            quiver.position = new Vector3(.06f, 1.1f, -.12f);
            quiver.rotation = Quaternion.Euler(-10f, 0f, -20f);
            Part(quiver, "Case", PrimitiveType.Cylinder, Vector3.zero, new Vector3(.09f, .22f, .09f), leather);
            Part(quiver, "Rim", PrimitiveType.Cylinder, new Vector3(0, .22f, 0), new Vector3(.1f, .012f, .1f), gold);
            for (int i = 0; i < 3; i++)
            {
                var spare = new GameObject("QuiverArrow" + i).transform;
                spare.SetParent(quiver, false);
                spare.localPosition = new Vector3((i - 1) * .022f, .3f - (i % 2) * .02f, (i % 2) * .015f);
                spare.localRotation = Quaternion.Euler(90f, 0f, 0f); // Head down into the case.
                BuildArrow(spare, shaft, steel, fletch);
            }
        }

        // Arrow along +Z with its pivot at the nock.
        private static void BuildArrow(Transform parent, Material shaft, Material steel, Material fletch)
        {
            Part(parent, "Shaft", PrimitiveType.Cylinder, new Vector3(0, 0, ArrowLength * .5f), new Vector3(.008f, ArrowLength * .5f, .008f), shaft)
                .localRotation = Quaternion.Euler(90f, 0f, 0f);
            Part(parent, "Head", PrimitiveType.Cube, new Vector3(0, 0, ArrowLength + .015f), new Vector3(.016f, .016f, .045f), steel)
                .localRotation = Quaternion.Euler(0f, 0f, 45f);
            Part(parent, "FletchA", PrimitiveType.Cube, new Vector3(0, 0, .05f), new Vector3(.002f, .028f, .07f), fletch);
            Part(parent, "FletchB", PrimitiveType.Cube, new Vector3(0, 0, .05f), new Vector3(.028f, .002f, .07f), fletch);
        }

        // Recurve-like limbs in the bow's YZ plane: back faces +Z (the target), tips sweep towards the string.
        private static Mesh LimbMesh()
        {
            const int rings = 41, sides = 8;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int i = 0; i < rings; i++)
            {
                float t = -1f + 2f * i / (rings - 1), a = Mathf.Abs(t);
                var centre = new Vector3(0, t * BowHalf, -Brace * (1.45f * t * t - .45f * Mathf.Pow(t, 6)));
                Vector3 tangent = new Vector3(0, BowHalf, -Brace * (2.9f * t - 2.7f * Mathf.Pow(t, 5))).normalized;
                Vector3 normal = Vector3.Cross(Vector3.right, tangent).normalized;
                float radius = a < .12f ? .02f : Mathf.Lerp(.017f, .007f, (a - .12f) / .88f);
                for (int j = 0; j < sides; j++)
                {
                    float angle = j * Mathf.PI * 2f / sides;
                    vertices.Add(centre + Vector3.right * (Mathf.Cos(angle) * radius * 1.4f) + normal * (Mathf.Sin(angle) * radius * .8f));
                }
            }
            for (int i = 0; i < rings - 1; i++)
                for (int j = 0; j < sides; j++)
                {
                    int k = (j + 1) % sides;
                    int a = i * sides + j, b = (i + 1) * sides + j, c = i * sides + k, d = (i + 1) * sides + k;
                    triangles.AddRange(new[] { a, b, c, b, d, c });
                }
            var mesh = new Mesh { name = "ArcherBowLimbs" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
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

        // Grip positions for the shot: the longest draw the short stylised bow arm can hold, and a drawn-in
        // grip used while nocking so the string hand can reach the string.
        private static void PrepareShot()
        {
            foreach (Transform bone in bones) bone.localRotation = Quaternion.Euler(BodyPose(bone.name, "Shoot", .9f));
            Vector3 shoulder = byName["LeftUpperArm"].position, forward = Vector3.forward;
            float reach = .97f * (Vector3.Distance(bind["LeftUpperArm"], bind["LeftForeArm"]) + Vector3.Distance(bind["LeftForeArm"], bind["LeftHand"]));
            float hand = (palms["Left"] - bind["LeftHand"]).magnitude;
            Vector3 offset = Anchor - shoulder;
            float along = Vector3.Dot(offset, forward);
            float discriminant = along * along - offset.sqrMagnitude + reach * reach;
            if (discriminant <= 0f) throw new InvalidOperationException("Archer bow arm cannot reach the shooting line.");
            drawLength = -along + Mathf.Sqrt(discriminant) + hand;
            gripFar = Anchor + forward * drawLength;
            gripNear = gripFar - forward * .24f - Vector3.up * .05f;
            foreach (Transform bone in bones) bone.localRotation = Quaternion.identity;
        }

        private static float Smooth(float from, float to, float value) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, value));

        private static bool IsArmBone(string name) => name.EndsWith("Shoulder") || name.EndsWith("Arm") || name.EndsWith("Hand");

        private static void ApplyPose(string role, float phase)
        {
            float bob = role == "Run" ? .016f * (1f - Mathf.Cos(phase * Mathf.PI * 4f)) : role == "Idle" ? .005f * Mathf.Sin(phase * Mathf.PI * 2f) : 0f;
            byName["Hips"].localPosition = hipsRest + Vector3.up * bob;
            foreach (Transform bone in bones)
                if (!IsArmBone(bone.name)) bone.localRotation = Quaternion.Euler(BodyPose(bone.name, role, phase));
            if (role == "Shoot") ShootArms(phase);
            else CarryArms(role, phase);
        }

        private static Vector3 BodyPose(string name, string role, float phase)
        {
            float wave = Mathf.Sin(phase * Mathf.PI * 2f);
            float side = name.StartsWith("Right") ? 1f : -1f;
            if (role == "Idle")
            {
                if (name == "Chest") return new Vector3(wave * 1.5f, 0, 0);
                if (name == "Head") return new Vector3(-wave * .8f, 0, 0);
                return Vector3.zero;
            }
            if (role == "Run")
            {
                if (name.EndsWith("Thigh")) return new Vector3(-side * wave * 18f, 0, 0);
                if (name.EndsWith("Shin")) return new Vector3(Mathf.Max(0, side * wave) * 30f, 0, 0);
                if (name.EndsWith("Foot")) return new Vector3(-Mathf.Max(0, side * wave) * 10f, 0, 0);
                if (name == "Spine") return new Vector3(4f, 0, 0);
                if (name == "Chest") return new Vector3(2f, wave * 4f, 0);
                if (name == "Head") return new Vector3(-4f, -wave * 3f, 0);
                return Vector3.zero;
            }
            // Shoot: side-on stance with the head turned back to the target; the chest flinches on release.
            float flinch = phase < .2f ? Mathf.Sin(phase / .2f * Mathf.PI) : 0f;
            switch (name)
            {
                case "Hips": return new Vector3(0, 30f, 0);
                case "Spine": return new Vector3(0, 20f, 0);
                case "Chest": return new Vector3(-2f * flinch, 25f, 0);
                case "Neck": return new Vector3(0, -35f, 0);
                case "Head": return new Vector3(3f, -32f, 0);
                default: return Vector3.zero;
            }
        }

        private static void CarryArms(string role, float phase)
        {
            float wave = Mathf.Sin(phase * Mathf.PI * 2f);
            foreach (int sign in new[] { 1, -1 })
            {
                string side = sign == 1 ? "Right" : "Left";
                float swing = role == "Run" ? sign * wave * (sign == 1 ? 12f : 7f) : 0f;
                byName[side + "Shoulder"].localRotation = Quaternion.identity;
                byName[side + "UpperArm"].localRotation = Quaternion.Euler(-6f + swing, 0, sign * -14f);
                byName[side + "ForeArm"].localRotation = Quaternion.Euler(role == "Run" ? -30f : -14f, 0, sign * 4f);
            }
            byName["RightHand"].localRotation = Quaternion.Euler(-6f, 0, 0);
            // The bow hangs from a relaxed fist: limbs fore and aft, upper limb tipped forward and up.
            Vector3 hang = byName["LeftForeArm"].rotation * (bind["LeftHand"] - bind["LeftForeArm"]);
            Vector3 point = (hang.normalized + Vector3.forward * .3f).normalized;
            byName["LeftHand"].rotation = Quaternion.LookRotation(point, new Vector3(0, .45f, 1f)) * bowGrip;
            SetString(RestNock, 0f, false);
        }

        // Release at phase 0 (UnitCombat has just dealt the damage), follow-through, nock, push-pull draw, hold.
        private static void ShootArms(float phase)
        {
            Transform chest = byName["Chest"];
            byName["RightShoulder"].localRotation = byName["LeftShoulder"].localRotation = Quaternion.identity;
            float near = phase < .12f ? 0f : phase < .4f ? Smooth(.12f, .4f, phase) : phase < .82f ? 1f - Smooth(.4f, .82f, phase) : 0f;
            Vector3 grip = Vector3.Lerp(gripFar, gripNear, near);
            Quaternion aim = Quaternion.LookRotation(Vector3.forward, Quaternion.AngleAxis(-8f, Vector3.forward) * Vector3.up);
            Quaternion bowHand = aim * bowGrip;
            Reach("Left", grip - bowHand * (palms["Left"] - bind["LeftHand"]), bind["LeftHand"], chest.rotation * new Vector3(-.3f, -1f, 0f));
            byName["LeftHand"].rotation = bowHand;

            Vector3 brace = bow.TransformPoint(RestNock);
            Vector3 recoil = Anchor + new Vector3(.05f, .01f, -.08f);
            Vector3 target;
            if (phase < .12f) target = Vector3.Lerp(Anchor, recoil, Smooth(0f, .12f, phase));
            else if (phase < .4f)
            {
                float t = Smooth(.12f, .4f, phase);
                target = Vector3.Lerp(recoil, brace, t) + Mathf.Sin(t * Mathf.PI) * new Vector3(.04f, -.03f, .03f);
            }
            else target = Vector3.Lerp(brace, Anchor, Smooth(.4f, .82f, phase));
            Transform stringHand = byName["RightHand"];
            stringHand.localRotation = Quaternion.identity;
            Reach("Right", target, palms["Right"], chest.rotation * new Vector3(1f, .3f, -.6f));
            Vector3 reached = stringHand.TransformPoint(palms["Right"] - bind["RightHand"]);

            Vector3 nock = phase < .05f ? Vector3.Lerp(bow.InverseTransformPoint(Anchor), RestNock, phase / .05f)
                : phase < .4f ? RestNock : bow.InverseTransformPoint(reached);
            float draw = Mathf.Clamp01((-nock.z - Brace) / Mathf.Max(.01f, drawLength - Brace));
            SetString(nock, draw, phase >= .36f);
        }

        // Two-bone arm: places the elbow towards `pole` so that `effector` (bind position below the elbow) meets `target`.
        private static void Reach(string side, Vector3 target, Vector3 effector, Vector3 pole)
        {
            Transform upper = byName[side + "UpperArm"], fore = byName[side + "ForeArm"];
            Vector3 s0 = bind[side + "UpperArm"], e0 = bind[side + "ForeArm"];
            float a = Vector3.Distance(s0, e0), b = Vector3.Distance(e0, effector);
            Vector3 shoulder = upper.position, toTarget = target - shoulder, direction = toTarget.normalized;
            float d = Mathf.Clamp(toTarget.magnitude, Mathf.Abs(a - b) + .001f, a + b - .001f);
            float cos = Mathf.Clamp((a * a + d * d - b * b) / (2f * a * d), -1f, 1f);
            Vector3 elbow = shoulder + direction * (a * cos)
                + Vector3.ProjectOnPlane(pole, direction).normalized * (a * Mathf.Sqrt(1f - cos * cos));
            // Bind rotations are identity, so world rotations map bind directions directly.
            upper.rotation = Quaternion.FromToRotation(e0 - s0, elbow - shoulder);
            fore.rotation = Quaternion.FromToRotation(effector - e0, target - elbow);
        }

        private static void SetString(Vector3 nock, float draw, bool arrowVisible)
        {
            limbs.localScale = new Vector3(1f, 1f, 1f + .35f * draw);
            var top = new Vector3(0, BowHalf, -Brace * limbs.localScale.z);
            var bottom = new Vector3(0, -BowHalf, top.z);
            Segment(upperString, top, nock);
            Segment(lowerString, bottom, nock);
            arrow.localPosition = nock;
            arrow.localRotation = Quaternion.LookRotation(ArrowRest - nock, Vector3.up);
            arrow.localScale = Vector3.one * (arrowVisible ? 1f : 0f);
        }

        private static void Segment(Transform segment, Vector3 from, Vector3 to)
        {
            segment.localPosition = (from + to) * .5f;
            segment.localRotation = Quaternion.FromToRotation(Vector3.up, to - from);
            segment.localScale = new Vector3(.005f, (to - from).magnitude * .5f, .005f);
        }

        private static AnimationClip MakeClip(Transform root, string role, float duration)
        {
            var clip = new AnimationClip { name = "Archer_" + role, legacy = true, frameRate = 30,
                wrapMode = role == "Shoot" ? WrapMode.Once : WrapMode.Loop };
            var curves = new Dictionary<string, AnimationCurve>();
            int samples = Mathf.RoundToInt(duration * 30);
            for (int i = 0; i <= samples; i++)
            {
                float phase = (float)i / samples, time = phase * duration;
                ApplyPose(role, phase);
                foreach (Transform bone in bones) KeyTransform(curves, root, bone, time, bone == bones[0], false);
                KeyTransform(curves, root, limbs, time, false, true);
                foreach (Transform part in new[] { upperString, lowerString, arrow }) KeyTransform(curves, root, part, time, true, true);
            }
            string arrowPath = AnimationUtility.CalculateTransformPath(arrow, root);
            foreach (KeyValuePair<string, AnimationCurve> pair in curves)
            {
                string[] binding = pair.Key.Split('|');
                if (binding[0] == arrowPath && binding[1].StartsWith("m_LocalScale"))
                    for (int k = 0; k < pair.Value.length; k++)
                    {
                        // The nocked arrow pops in and out instead of growing.
                        AnimationUtility.SetKeyLeftTangentMode(pair.Value, k, AnimationUtility.TangentMode.Constant);
                        AnimationUtility.SetKeyRightTangentMode(pair.Value, k, AnimationUtility.TangentMode.Constant);
                    }
                clip.SetCurve(binding[0], typeof(Transform), binding[1], pair.Value);
            }
            clip.EnsureQuaternionContinuity();
            return SaveAsset(clip, RigFolder + "/Archer_" + role + ".anim");
        }

        private static void KeyTransform(Dictionary<string, AnimationCurve> curves, Transform root, Transform target, float time, bool position, bool scale)
        {
            string path = AnimationUtility.CalculateTransformPath(target, root);
            Quaternion q = target.localRotation;
            for (int k = 0; k < 4; k++) Key(curves, path, "m_LocalRotation." + "xyzw"[k], time, q[k]);
            if (position) for (int k = 0; k < 3; k++) Key(curves, path, "m_LocalPosition." + "xyz"[k], time, target.localPosition[k]);
            if (scale) for (int k = 0; k < 3; k++) Key(curves, path, "m_LocalScale." + "xyz"[k], time, target.localScale[k]);
        }

        private static void Key(Dictionary<string, AnimationCurve> curves, string path, string property, float time, float value)
        {
            string key = path + "|" + property;
            if (!curves.TryGetValue(key, out AnimationCurve curve)) curves[key] = curve = new AnimationCurve();
            curve.AddKey(time, value);
        }

        private static T SaveAsset<T>(T value, string path) where T : UnityEngine.Object
        {
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null) { AssetDatabase.CreateAsset(value, path); return value; }
            EditorUtility.CopySerialized(value, existing); UnityEngine.Object.DestroyImmediate(value); EditorUtility.SetDirty(existing); return existing;
        }

        private static Vector3 Centroid(SkinnedMeshRenderer skin, Vector3[] posed, int[] indices)
        {
            Vector3 sum = Vector3.zero;
            foreach (int index in indices) sum += skin.transform.TransformPoint(posed[index]);
            return sum / indices.Length;
        }

        private static bool ValidateRig(GameObject root, SkinnedMeshRenderer skin, AnimationClip idle, AnimationClip run, AnimationClip shoot)
        {
            var results = new List<string>();
            bool pass = true;
            void Check(bool ok, string line) { results.Add((ok ? "ok   " : "FAIL ") + line); pass &= ok; }
            Check(skin.bones.Length >= 19 && skin.sharedMesh.bindposes.Length == skin.bones.Length,
                $"skeleton: {skin.bones.Length} bones, {skin.sharedMesh.bindposes.Length} bind poses");
            Check(skin.sharedMesh.boneWeights.All(w => !float.IsNaN(w.weight0) && Mathf.Abs(w.weight0 + w.weight1 + w.weight2 + w.weight3 - 1) < .001f),
                $"normalized weights on {skin.sharedMesh.vertexCount} vertices");
            Transform tipTop = limbs.Find("TipTop"), tipBottom = limbs.Find("TipBottom"), head = byName["Head"];
            var rest = new Mesh(); var posed = new Mesh();
            try
            {
                foreach (Transform bone in bones) bone.localRotation = Quaternion.identity;
                bones[0].localPosition = hipsRest;
                skin.BakeMesh(rest);
                Vector3[] vertices = rest.vertices;
                foreach (AnimationClip clip in new[] { idle, run, shoot })
                {
                    clip.SampleAnimation(root, clip.length * .3f);
                    skin.BakeMesh(posed);
                    Vector3[] changed = posed.vertices;
                    float largest = 0;
                    for (int i = 0; i < changed.Length; i++) largest = Mathf.Max(largest, (changed[i] - vertices[i]).magnitude);
                    Check(largest > .001f && largest < 1.5f, $"{clip.name}: sampled weighted vertex motion {largest:F3} m");
                    float lowest = float.PositiveInfinity;
                    for (int k = 0; k <= 8; k++)
                    {
                        clip.SampleAnimation(root, clip.length * k / 8f);
                        lowest = Mathf.Min(lowest, tipTop.position.y, tipBottom.position.y);
                    }
                    Check(lowest > .03f, $"{clip.name}: lowest bow tip {lowest:F3} m above ground");
                }

                shoot.SampleAnimation(root, shoot.length * .02f);
                Check(arrow.lossyScale.x < .01f, "Shoot: nocked arrow hidden right after release");
                shoot.SampleAnimation(root, shoot.length * .4f);
                float nockReach = Vector3.Distance(arrow.position, bow.TransformPoint(RestNock));
                Check(nockReach < .03f, $"Shoot: string hand reaches the braced string to nock (gap {nockReach:F3} m)");
                foreach (float phase in new[] { .6f, .9f })
                {
                    shoot.SampleAnimation(root, shoot.length * phase);
                    skin.BakeMesh(posed);
                    Vector3[] pose = posed.vertices;
                    float stringGap = Vector3.Distance(Centroid(skin, pose, palmVertices["Right"]), arrow.position);
                    Check(stringGap < .045f, $"Shoot {phase:F1}: string hand on the nock (gap {stringGap:F3} m)");
                    if (phase < .9f) continue;
                    float gripGap = Vector3.Distance(Centroid(skin, pose, palmVertices["Left"]), bow.position);
                    float reachGap = Vector3.Distance(bow.position, gripFar);
                    float aim = Vector3.Dot(arrow.forward, root.transform.forward);
                    float drawn = Vector3.Distance(arrow.position, bow.position);
                    float face = Vector3.Distance(Centroid(skin, pose, palmVertices["Right"]), head.position + head.rotation * new Vector3(0, .13f, 0));
                    Check(gripGap < .03f, $"Shoot full draw: bow in the left palm (gap {gripGap:F3} m)");
                    Check(reachGap < .04f, $"Shoot full draw: bow arm reaches the shooting line (gap {reachGap:F3} m)");
                    Check(aim > .95f, $"Shoot full draw: arrow points at the target (dot {aim:F3})");
                    Check(drawn > .28f, $"Shoot full draw: draw length {drawn:F3} m (brace {Brace:F2} m)");
                    Check(face > .1f, $"Shoot full draw: string hand clear of the face ({face:F3} m from head centre)");
                    Check(arrow.lossyScale.x > .99f, "Shoot full draw: arrow nocked and visible");
                }
                foreach (Transform bone in bones) bone.localRotation = Quaternion.identity;
                bones[0].localPosition = hipsRest;
            }
            finally { UnityEngine.Object.DestroyImmediate(rest); UnityEngine.Object.DestroyImmediate(posed); }
            Directory.CreateDirectory("Docs/Validation");
            File.WriteAllText("Docs/Validation/ArcherRigChecks.txt", (pass ? "PASS" : "FAIL") + " | " + DateTime.UtcNow.ToString("O")
                + "\nUnity-native prototype skeleton on " + SourceMesh + " (source mesh unchanged).\n"
                + string.Join("\n", results)
                + "\nBow: LeftHand; nocked arrow and string animated in the clips; quiver on Chest.\n"
                + "Shoot clip releases at phase 0 to match UnitCombat.Attacked (damage is instant; no projectile).\n"
                + "No Mixamo upload or external animation assets. Visual review and cloth polish remain manual.\n");
            return pass;
        }

        // Contact sheets (rows: idle, run, shoot) so the rig can be reviewed without entering Play mode.
        private static void RenderPreview(GameObject source, params AnimationClip[] clips)
        {
            const int width = 300, height = 380, columns = 6;
            Vector3 stage = new Vector3(5000f, 0f, 0f);
            Scene original = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            try
            {
                SceneManager.SetActiveScene(scene);
                GameObject model = UnityEngine.Object.Instantiate(source);
                model.transform.position = stage;
                // Edit-mode Camera.Render does not refresh skinning, so draw a per-frame baked copy instead.
                SkinnedMeshRenderer skin = model.GetComponentInChildren<SkinnedMeshRenderer>();
                var baked = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                var bakedObject = new GameObject("Baked Preview");
                bakedObject.transform.SetParent(skin.transform, false);
                bakedObject.AddComponent<MeshFilter>().sharedMesh = baked;
                bakedObject.AddComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials;
                skin.enabled = false;
                Light light = new GameObject("Preview Sun").AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.3f;
                light.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
                RenderSettings.ambientLight = new Color(.55f, .58f, .65f);
                GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.transform.position = stage;
                Camera camera = new GameObject("Preview Camera").AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.82f, .85f, .9f);
                camera.fieldOfView = 30f;
                camera.targetTexture = target;
                foreach ((string label, float yaw) in new[] { ("Front", 30f), ("Side", -80f) })
                {
                    camera.transform.position = stage + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 1.1f, 5.4f);
                    camera.transform.LookAt(stage + new Vector3(0f, .95f, 0f));
                    var sheet = new Texture2D(width * columns, height * clips.Length, TextureFormat.RGB24, false);
                    var tile = new Texture2D(width, height, TextureFormat.RGB24, false);
                    for (int row = 0; row < clips.Length; row++)
                        for (int column = 0; column < columns; column++)
                        {
                            clips[row].SampleAnimation(model, clips[row].length * column / columns);
                            skin.BakeMesh(baked);
                            camera.Render();
                            RenderTexture.active = target;
                            tile.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                            tile.Apply();
                            RenderTexture.active = null;
                            sheet.SetPixels(column * width, (clips.Length - 1 - row) * height, width, height, tile.GetPixels());
                        }
                    sheet.Apply();
                    File.WriteAllBytes("Docs/Validation/ArcherRigPreview_" + label + ".png", sheet.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(sheet);
                    UnityEngine.Object.DestroyImmediate(tile);
                }
                UnityEngine.Object.DestroyImmediate(baked);
            }
            finally
            {
                if (original.IsValid()) SceneManager.SetActiveScene(original);
                EditorSceneManager.CloseScene(scene, true);
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}

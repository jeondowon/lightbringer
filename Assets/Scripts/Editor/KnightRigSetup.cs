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
    // Project-native prototype rig for the mounted Knight, built from the static Knight_Mounted prefab
    // (placed horse FBX + seated rider mesh). The horse gets a four-legged gallop skeleton with neck, head
    // and tail; the rider is skinned separately on bones parented to the horse spine, so it rides every
    // stride. A couched lance is held in the right hand and a kite shield on the left. Landmarks were
    // measured on the placed meshes (horse 2.1 m at the withers' top line, facing +Z).
    internal static class KnightRigSetup
    {
        private const string Folder = "Assets/Art/Characters/Knight";
        private const string RigFolder = Folder + "/Rigged";
        private const string PrefabPath = RigFolder + "/Knight_Rigged.prefab";
        private const string ReportPath = "Docs/Validation/KnightRigChecks.txt";
        private const float ContactPhase = .45f;
        // Lance in its own space: grip at the origin, shaft along +Y towards the tip.
        private const float LanceButt = -.95f, LanceTip = 2.18f;
        private const float ShieldYaw = -25f;
        private static readonly Vector3 SpineRest = new Vector3(0f, 1.25f, -.2f);

        private static GeneratedRig horseRig, riderRig;
        private static float hcx, rcx, riderWristY;
        private static readonly Dictionary<string, int[]> palms = new Dictionary<string, int[]>();

        public static VisualOverride Build(GameObject mounted, ArtStyleLibrary library)
        {
            if (!AssetDatabase.IsValidFolder(RigFolder)) AssetDatabase.CreateFolder(Folder, "Rigged");
            Scene scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Knight_Rigged");
            SceneManager.MoveGameObjectToScene(root, scene);
            var probe = UnityEngine.Object.Instantiate(mounted);
            SceneManager.MoveGameObjectToScene(probe, scene);
            try
            {
                probe.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                Mesh horseMesh = Combine(probe.transform.Find("Horse"), "KnightHorse_Skinned");
                Mesh riderMesh = Combine(probe.transform.Find("Rider"), "KnightRider_Skinned");
                horseRig = new GeneratedRig(root.transform);
                riderRig = new GeneratedRig(root.transform);
                palms.Clear();
                MakeHorse(root.transform, horseMesh.vertices);
                MakeRider(riderMesh.vertices, horseRig.Find("Spine"));
                horseRig.Skin(horseMesh, null, HorseEligible, .012f, hcx);
                riderRig.Skin(riderMesh, RiderRigid, RiderEligible, .006f, rcx);
                horseMesh = SaveAsset(horseMesh, RigFolder + "/KnightHorse_Skinned.asset");
                riderMesh = SaveAsset(riderMesh, RigFolder + "/KnightRider_Skinned.asset");
                SkinnedMeshRenderer horseSkin = horseRig.AddSkin("Horse", horseMesh,
                    AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Characters/KnightHorse/KnightHorse.mat"),
                    new Bounds(new Vector3(0f, 1.1f, 0f), new Vector3(1.6f, 2.6f, 3.6f)));
                SkinnedMeshRenderer riderSkin = riderRig.AddSkin("Rider", riderMesh,
                    AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Knight.mat"),
                    new Bounds(new Vector3(0f, .2f, 0f), new Vector3(2f, 2.2f, 1.6f)));
                Transform lance = BuildLance();
                Transform shield = BuildShield();

                Transform[] bones = horseRig.Bones.Concat(riderRig.Bones).ToArray();
                AnimationClip idle = MakeClip(root.transform, bones, "Idle", 2.4f, WrapMode.Loop);
                AnimationClip gallop = MakeClip(root.transform, bones, "Gallop", .7f, WrapMode.Loop);
                AnimationClip charge = MakeClip(root.transform, bones, "Charge", .9f, WrapMode.Once);
                string checks = Validate(root, horseSkin, riderSkin, lance, shield, idle, gallop, charge);
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (prefab == null) throw new InvalidOperationException("Could not save the Knight rig prefab.");
                File.WriteAllText(ReportPath, $"PASS | {DateTime.UtcNow:O}\n"
                    + $"Horse skeleton: {horseRig.Bones.Count} bones, {horseMesh.vertexCount} weighted vertices; "
                    + $"rider skeleton: {riderRig.Bones.Count} bones (parented to the horse spine), {riderMesh.vertexCount} weighted vertices.\n"
                    + checks + "Lance on the rider's RightHand, kite shield on the LeftHand. Source FBX files and Knight_Mounted.prefab preserved.\n"
                    + "No external animation assets. Hand-tuned landmark weights and generated prototype clips.\n"
                    + "Visual review of the gallop, the rider's arms and the tack remains manual.\n");
                return new VisualOverride
                {
                    id = VisualId.Knight, prefab = prefab, scale = 1f,
                    idleClip = idle, moveClip = gallop, actionClip = charge, moveClipSpeed = 4f,
                    attachments = new PropAttachment[0], // Lance and shield are children of the rider's hand bones.
                };
            }
            finally
            {
                horseRig = riderRig = null;
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static Mesh Combine(Transform part, string name)
        {
            CombineInstance[] parts = part.GetComponentsInChildren<MeshFilter>()
                .SelectMany(f => Enumerable.Range(0, f.sharedMesh.subMeshCount).Select(s => new CombineInstance
                    { mesh = f.sharedMesh, subMeshIndex = s, transform = f.transform.localToWorldMatrix }))
                .ToArray();
            if (parts.Length == 0) throw new InvalidOperationException("Knight_Mounted has no mesh under " + part.name);
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.CombineMeshes(parts, true, true);
            return mesh;
        }

        // ---------- Horse ----------

        private static void MakeHorse(Transform root, Vector3[] v)
        {
            hcx = GeneratedRig.Landmark(v, p => p.y < .06f, "hooves").x;
            Vector3 C(float y, float z) => new Vector3(hcx, y, z);
            // The spine carries the stride bob and rock and is not skinned itself.
            Transform spine = horseRig.Bone("Spine", root, C(SpineRest.y, SpineRest.z), C(SpineRest.y, SpineRest.z + .4f));
            Transform hips = horseRig.Bone("Hips", spine, C(1.3f, -.25f), C(1.28f, -1f));
            Transform chest = horseRig.Bone("Chest", spine, C(1.3f, -.25f), C(1.22f, .62f));
            Transform neck = horseRig.Bone("Neck", chest, C(1.42f, .45f), C(1.95f, .86f));
            horseRig.Bone("Head", neck, C(1.95f, .86f), C(1.5f, 1.28f));
            Vector3 tailTop = GeneratedRig.Landmark(v, p => p.z < -.95f && p.y > 1.32f && p.y < 1.5f, "tail root");
            Vector3 tailMid = GeneratedRig.Landmark(v, p => p.z < -.95f && p.y > .85f && p.y < 1f, "tail middle");
            Vector3 tailEnd = GeneratedRig.Landmark(v, p => p.z < -1.1f && p.y > .12f && p.y < .3f, "tail end");
            Transform tail = horseRig.Bone("Tail1", hips, tailTop, tailMid);
            horseRig.Bone("Tail2", tail, tailMid, tailEnd);
            foreach (int sign in new[] { 1, -1 })
            {
                string side = sign == 1 ? "Right" : "Left";
                foreach (bool front in new[] { true, false })
                {
                    string leg = side + (front ? "Front" : "Hind");
                    bool On(Vector3 p) => (p.x - hcx) * sign > .04f && (front ? p.z > .2f && p.z < .95f : p.z > -1f && p.z < -.35f);
                    Vector3 hoof = GeneratedRig.Landmark(v, p => p.y < .07f && On(p), leg + " hoof");
                    Vector3 fetlock = GeneratedRig.Landmark(v, p => p.y > .18f && p.y < .25f && On(p), leg + " fetlock");
                    Vector3 knee = GeneratedRig.Landmark(v, p => p.y > .48f && p.y < .58f && On(p), leg + (front ? " knee" : " hock"));
                    Vector3 top = new Vector3(knee.x, 1.05f, front ? .42f : -.78f);
                    Transform upper = horseRig.Bone(leg + "Upper", front ? chest : hips, top, knee);
                    Transform lower = horseRig.Bone(leg + "Lower", upper, knee, fetlock);
                    horseRig.Bone(leg + "Foot", lower, fetlock, hoof);
                }
            }
        }

        private static bool HorseEligible(Vector3 p, string name)
        {
            if (name == "Spine") return false;
            bool tail = p.z < -.93f && p.y < 1.45f || p.z < -1.02f;
            bool tailBone = name.StartsWith("Tail");
            if (tail) return tailBone || name == "Hips" && p.y > 1.25f;
            if (tailBone) return false;
            bool headZone = p.z > .45f && p.y > 1.35f;
            if (name == "Head") return p.z > .72f && p.y > 1.35f || p.y > 1.85f;
            if (name == "Neck") return headZone || p.z > .3f && p.y > 1.45f;
            if (headZone && p.z > .75f) return false;
            bool leg = name.Contains("Front") || name.Contains("Hind");
            if (leg) return p.y < 1.12f && (name.Contains("Front") ? p.z > -.15f : p.z <= -.15f);
            return p.y > .75f;
        }

        // ---------- Rider ----------

        private static void MakeRider(Vector3[] v, Transform horseSpine)
        {
            rcx = GeneratedRig.Landmark(v, p => p.y > 2.3f, "helmet").x;
            float z = GeneratedRig.Landmark(v, p => p.y > 1.8f && p.y < 1.9f && Mathf.Abs(p.x - rcx) < .13f, "rider torso").z;
            Vector3 C(float y) => new Vector3(rcx, y, z);
            Transform hips = riderRig.Bone("RiderHips", horseSpine, C(1.62f), C(1.76f));
            Transform spine = riderRig.Bone("RiderSpine", hips, C(1.76f), C(1.93f));
            Transform chest = riderRig.Bone("RiderChest", spine, C(1.93f), C(2.13f));
            Transform neck = riderRig.Bone("RiderNeck", chest, C(2.13f), C(2.22f));
            riderRig.Bone("RiderHead", neck, C(2.22f), C(2.49f));
            foreach (int sign in new[] { 1, -1 })
            {
                string side = sign == 1 ? "Right" : "Left";
                float D(Vector3 p) => (p.x - rcx) * sign;
                Vector3 pad = GeneratedRig.Landmark(v, p => p.y > 2.08f && p.y < 2.16f && D(p) > .17f && p.z > -.2f, side + " rider pauldron");
                Vector3 joint = new Vector3(rcx + sign * .17f, pad.y, pad.z);
                Vector3 elbow = GeneratedRig.Landmark(v, p => p.y > 1.87f && p.y < 1.93f && D(p) > .2f && p.z > -.17f && p.z < .03f, side + " rider elbow");
                Vector3 wrist = GeneratedRig.Landmark(v, p => p.y > 1.74f && p.y < 1.8f && D(p) > .29f && p.z > -.13f && p.z < .03f, side + " rider wrist");
                // Hands hang beside the thighs; the thighs reach further forward (+Z).
                Vector3 palm = GeneratedRig.Landmark(v, p => p.y > 1.6f && p.y < 1.7f && D(p) > .36f && p.z > -.09f && p.z < .045f,
                    out int[] indices, side + " rider palm");
                palms[side] = indices;
                riderWristY = wrist.y;
                Transform shoulder = riderRig.Bone("Rider" + side + "Shoulder", chest, new Vector3(rcx + sign * .07f, 2.11f, pad.z), joint);
                Transform upper = riderRig.Bone("Rider" + side + "UpperArm", shoulder, joint, elbow);
                Transform fore = riderRig.Bone("Rider" + side + "ForeArm", upper, elbow, wrist);
                riderRig.Bone("Rider" + side + "Hand", fore, wrist, palm + (palm - wrist).normalized * .03f);
            }
        }

        private static bool RiderArm(Vector3 p) => p.y > 1.58f && p.y < 2.18f && p.z > -.19f && p.z < (p.y < 1.75f ? .045f : .1f)
            && Mathf.Abs(p.x - rcx) > (p.y < 1.9f ? Mathf.Lerp(.33f, .165f, Mathf.InverseLerp(1.6f, 1.9f, p.y)) : .165f);

        private static string RiderRigid(Vector3 p) => RiderArm(p) && p.y < riderWristY - .015f
            ? (p.x >= rcx ? "RiderRightHand" : "RiderLeftHand") : null;

        private static bool RiderEligible(Vector3 p, string name)
        {
            bool armBone = name.EndsWith("Arm") || name.EndsWith("Hand") || name.EndsWith("Shoulder");
            if (RiderArm(p)) return armBone || name == "RiderChest";
            if (armBone) return false;
            if (p.y > 2.22f) return name == "RiderHead";
            if (name == "RiderHead") return false;
            // Seated legs and the coat draped over the horse ride rigidly on the pelvis.
            if (p.y < 1.7f) return name == "RiderHips";
            return true;
        }

        // ---------- Props ----------

        private static Transform BuildLance()
        {
            Material wood = MakeMaterial("LanceShaft", new Color(.82f, .78f, .7f), 0f, .35f);
            Material leather = MakeMaterial("LanceGrip", new Color(.12f, .07f, .04f), 0f, .25f);
            Material steel = MakeMaterial("LanceSteel", new Color(.72f, .77f, .84f), .85f, .6f);
            Material gold = MakeMaterial("LanceGold", new Color(.8f, .58f, .22f), .75f, .5f);
            Material blue = MakeMaterial("LancePennant", new Color(.06f, .14f, .42f), .1f, .35f);
            var lance = new GameObject("Lance").transform;
            lance.SetParent(riderRig.Find("RiderRightHand"), false);
            lance.position = CentreOf(palms["Right"]);
            lance.rotation = Quaternion.identity;
            AddMesh(lance, "Shaft", GeneratedRig.Lathe("LanceShaft", new[] { LanceButt, LanceButt + .05f, -.1f, .14f, 1.98f },
                new[] { .03f, .04f, .036f, .036f, .018f }), wood);
            AddMesh(lance, "Vamplate", GeneratedRig.Lathe("LanceVamplate", new[] { .1f, .13f, .32f }, new[] { .12f, .13f, .035f }, 14), steel);
            AddMesh(lance, "Tip", GeneratedRig.Lathe("LanceTip", new[] { 1.96f, 2.02f, LanceTip }, new[] { .026f, .03f, 0f }), steel);
            Part(lance, "Grip", PrimitiveType.Cylinder, Vector3.zero, new Vector3(.046f, .08f, .046f), leather);
            Part(lance, "Butt", PrimitiveType.Sphere, new Vector3(0f, LanceButt, 0f), Vector3.one * .065f, gold);
            Part(lance, "Ring", PrimitiveType.Cylinder, new Vector3(0f, 1.94f, 0f), new Vector3(.045f, .015f, .045f), gold);
            // Swallow-tailed pennant flying to the outer side below the tip.
            // Both faces get their own vertices so each side lights correctly.
            var outline = new[] { new Vector3(0f, 1.88f, 0f), new Vector3(0f, 1.62f, 0f), new Vector3(.42f, 1.84f, 0f),
                new Vector3(.3f, 1.75f, 0f), new Vector3(.42f, 1.66f, 0f) };
            var pennant = new Mesh { name = "LancePennant" };
            pennant.vertices = outline.Concat(outline).ToArray();
            pennant.triangles = new[] { 0, 2, 3, 0, 3, 1, 1, 3, 4, 8, 7, 5, 6, 8, 5, 9, 8, 6 };
            pennant.RecalculateNormals(); pennant.RecalculateBounds();
            AddMesh(lance, "Pennant", pennant, blue);
            return lance;
        }

        private static Transform BuildShield()
        {
            Material face = MakeMaterial("KiteShieldBlue", new Color(.06f, .13f, .36f), .3f, .4f);
            Material gold = MakeMaterial("KiteShieldGold", new Color(.8f, .58f, .22f), .75f, .5f);
            var shield = new GameObject("Shield").transform;
            shield.SetParent(riderRig.Find("RiderLeftHand"), false);
            shield.position = CentreOf(palms["Left"]);
            shield.rotation = Quaternion.identity;
            var board = new GameObject("Board").transform;
            board.SetParent(shield, false);
            board.localPosition = new Vector3(-.04f, .06f, .07f);
            // Gold backing shows as the rim around the blue face; the lower point is a turned square.
            Part(board, "Rim", PrimitiveType.Cube, new Vector3(0f, .1f, -.012f), new Vector3(.52f, .5f, .025f), gold);
            Part(board, "Face", PrimitiveType.Cube, new Vector3(0f, .1f, 0f), new Vector3(.46f, .46f, .03f), face);
            Part(board, "RimPoint", PrimitiveType.Cube, new Vector3(0f, -.13f, -.012f), new Vector3(.37f, .37f, .025f), gold)
                .localRotation = Quaternion.Euler(0f, 0f, 45f);
            Part(board, "FacePoint", PrimitiveType.Cube, new Vector3(0f, -.13f, 0f), new Vector3(.325f, .325f, .03f), face)
                .localRotation = Quaternion.Euler(0f, 0f, 45f);
            Part(board, "Upright", PrimitiveType.Cube, new Vector3(0f, 0f, .02f), new Vector3(.05f, .62f, .015f), gold);
            Part(board, "Bar", PrimitiveType.Cube, new Vector3(0f, .14f, .02f), new Vector3(.36f, .05f, .015f), gold);
            Part(board, "Boss", PrimitiveType.Sphere, new Vector3(0f, .14f, .03f), new Vector3(.1f, .1f, .05f), gold);
            return shield;
        }

        private static void AddMesh(Transform parent, string name, Mesh mesh, Material material)
        {
            mesh = SaveAsset(mesh, RigFolder + "/" + mesh.name + ".asset");
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static Vector3 CentreOf(int[] indices)
        {
            Vector3[] vertices = riderRig.Bones[0].root.Find("Rider").GetComponent<SkinnedMeshRenderer>().sharedMesh.vertices;
            Vector3 sum = Vector3.zero;
            foreach (int index in indices) sum += vertices[index];
            return sum / indices.Length;
        }

        private static Material MakeMaterial(string name, Color color, float metallic, float smoothness)
            => RigBuildUtility.MakeMaterial(RigFolder, name, color, metallic, smoothness);

        // ---------- Clips ----------

        private static AnimationClip MakeClip(Transform root, Transform[] bones, string role, float duration, WrapMode wrap)
        {
            Transform spine = horseRig.Find("Spine");
            Vector3 rest = horseRig.RestPosition(spine);
            return GeneratedRig.Clip(root, bones, RigFolder + "/Knight_" + role + ".anim", duration, wrap,
                (bone, phase) => Rotation(bone.name, role, phase),
                (bone, phase) => bone == spine ? rest + Vector3.up * Bob(role, phase) : (Vector3?)null);
        }

        private static float Bob(string role, float phase) => role == "Gallop" ? .05f * Mathf.Cos(phase * Mathf.PI * 4f)
            : role == "Charge" ? .04f * Mathf.Cos(phase * Mathf.PI * 4f) - .04f * Strike(phase) : .01f * Mathf.Sin(phase * Mathf.PI * 2f);

        // Charge timing: couch the lance, drive it home at contact (0.45), recover to the upright carry.
        private static float Lowered(float phase) => GeneratedRig.Pulse(phase, 0f, .25f, .7f, 1f);
        private static float Strike(float phase) => GeneratedRig.Pulse(phase, .25f, ContactPhase, .6f, 1f);

        private static Vector3 Pose(string name, string role, float phase)
        {
            float tau = Mathf.PI * 2f, wave = Mathf.Sin(phase * tau);
            int side = GeneratedRig.Side(name);
            Vector3 pose = Vector3.zero;
            // Rider carry: lance hand forward at the waist, shield arm across the left flank.
            if (name == "RiderRightUpperArm") pose = new Vector3(-20, 0, -15);
            if (name == "RiderRightForeArm") pose = new Vector3(-70, 0, 0);
            if (name == "RiderLeftUpperArm") pose = new Vector3(-25, 0, 20);
            if (name == "RiderLeftForeArm") pose = new Vector3(-70, 0, 0);
            bool moving = role != "Idle";
            if (role == "Idle")
            {
                if (name == "Chest") pose.x = wave;
                if (name == "Neck") pose = new Vector3(-3f * wave, 4f * Mathf.Sin((phase + .15f) * tau), 0f);
                if (name == "Head") pose = new Vector3(2f * Mathf.Sin((phase + .3f) * tau), 0f, 0f);
                if (name.StartsWith("Tail")) pose.y = (name == "Tail1" ? 4f : 7f) * Mathf.Sin(phase * tau - (name == "Tail1" ? 0f : .8f));
                if (name == "RiderChest") pose.x = .8f * wave;
            }
            if (moving)
            {
                // Two strides per Charge clip; one per Gallop cycle. Diagonal pairs like a fast trot.
                float cycle = role == "Charge" ? phase * 2f : phase;
                bool front = name.Contains("Front"), hind = name.Contains("Hind");
                if (front || hind)
                {
                    float offset = (front ? side < 0 : side > 0) ? 0f : .5f;
                    float swing = Mathf.Sin((cycle + offset) * tau);
                    float raise = Mathf.Max(0f, Mathf.Cos((cycle + offset) * tau));
                    float upper = (front ? -28f : -24f) * swing;
                    float lower = (front ? 50f : 38f) * raise;
                    if (name.EndsWith("Upper")) pose.x = upper;
                    if (name.EndsWith("Lower")) pose.x = lower;
                    if (name.EndsWith("Foot")) pose.x = -.7f * (upper + lower);
                }
                float rock = Mathf.Sin(cycle * tau * 2f);
                if (name == "Spine") pose.x = 2.5f * rock;
                if (name == "Neck") pose.x = 7f * Mathf.Sin(cycle * tau * 2f + 1f);
                if (name == "Head") pose.x = -4f * Mathf.Sin(cycle * tau * 2f + 1f);
                if (name == "Tail1") pose = new Vector3(-18f, 5f * Mathf.Sin(cycle * tau), 0f);
                if (name == "Tail2") pose = new Vector3(-10f, 8f * Mathf.Sin(cycle * tau - .8f), 0f);
                if (name == "RiderHips") pose.x = -2.5f * rock;
                if (name == "RiderSpine") pose.x = 6f + 2f * rock;
                if (name == "RiderChest") pose.x = 3f;
            }
            if (role == "Charge")
            {
                float l = Lowered(phase), s = Strike(phase);
                if (name == "Spine") pose.x += 4f * s;
                if (name == "Neck") pose.x += 12f * s;
                if (name == "Head") pose.x -= 6f * s;
                if (name == "RiderSpine") pose.x += 10f * s;
                if (name == "RiderChest") pose.x += 6f * s;
                // Tuck the lance under the arm, then drive it forward.
                if (name == "RiderRightUpperArm") pose += new Vector3(30f * l - 15f * s, 0f, 10f * l);
                if (name == "RiderRightForeArm") pose.x += -15f * l + 10f * s;
            }
            return pose;
        }

        private static readonly string[] RightChain = { "Spine", "RiderHips", "RiderSpine", "RiderChest", "RiderRightShoulder", "RiderRightUpperArm", "RiderRightForeArm" };
        private static readonly string[] LeftChain = { "Spine", "RiderHips", "RiderSpine", "RiderChest", "RiderLeftShoulder", "RiderLeftUpperArm", "RiderLeftForeArm" };

        private static Quaternion Rotation(string name, string role, float phase)
        {
            if (name != "RiderRightHand" && name != "RiderLeftHand") return Quaternion.Euler(Pose(name, role, phase));
            bool right = name == "RiderRightHand";
            Quaternion parent = Quaternion.identity;
            foreach (string ancestor in right ? RightChain : LeftChain) parent *= Quaternion.Euler(Pose(ancestor, role, phase));
            // Props are aimed in root space: upright lance carried on the move, couched level for the charge.
            if (!right) return Quaternion.Inverse(parent) * Quaternion.Euler(0f, ShieldYaw, 0f);
            float pitch = role == "Gallop" ? 40f : 10f, roll = 8f;
            if (role == "Charge") { float l = Lowered(phase); pitch = Mathf.Lerp(10f, 88f, l); roll = Mathf.Lerp(8f, 0f, l); }
            return Quaternion.Inverse(parent) * Quaternion.Euler(pitch, 0f, roll);
        }

        // ---------- Validation ----------

        private static string Validate(GameObject root, SkinnedMeshRenderer horseSkin, SkinnedMeshRenderer riderSkin,
            Transform lance, Transform shield, params AnimationClip[] clips)
        {
            GeneratedRig.CheckWeights(horseSkin);
            GeneratedRig.CheckWeights(riderSkin);
            if (lance.parent != riderRig.Find("RiderRightHand") || shield.parent != riderRig.Find("RiderLeftHand"))
                throw new InvalidOperationException("Knight props are not attached to the rider's hands.");
            Vector3[] horseRest = GeneratedRig.RestVertices(horseSkin), riderRest = GeneratedRig.RestVertices(riderSkin);
            Vector3 forward = root.transform.forward;
            var lines = new List<string>();
            foreach (AnimationClip clip in clips)
            {
                horseRig.ResetPose(); riderRig.ResetPose();
                float horseMotion = GeneratedRig.Motion(root, horseSkin, clip, .3f, horseRest);
                float riderMotion = GeneratedRig.Motion(root, riderSkin, clip, .3f, riderRest);
                if (horseMotion < .005f || horseMotion > 2f || riderMotion < .001f || riderMotion > 2f)
                    throw new InvalidOperationException($"Invalid deformation in {clip.name} (horse {horseMotion:F3}, rider {riderMotion:F3}).");
                bool charge = clip.name.EndsWith("Charge");
                clip.SampleAnimation(root, clip.length * (charge ? ContactPhase : .5f));
                float grip = Mathf.Max(Vector3.Distance(GeneratedRig.Centroid(riderSkin, palms["Right"]), lance.position),
                    Vector3.Distance(GeneratedRig.Centroid(riderSkin, palms["Left"]), shield.position));
                if (grip > .015f) throw new InvalidOperationException("Prop/palm alignment failed for " + clip.name);
                Vector3 butt = lance.TransformPoint(new Vector3(0f, LanceButt, 0f));
                // The butt must stay clear of the horse: outside the barrel or above the back.
                bool clear = Mathf.Abs(butt.x - hcx) > .38f || butt.y > 1.55f;
                if (!clear) throw new InvalidOperationException($"Lance butt sinks into the horse in {clip.name} at {butt}.");
                lines.Add($"{clip.name}: horse motion {horseMotion:F3} m, rider motion {riderMotion:F3} m; grip error {grip:F4} m; "
                    + $"lance butt at ({butt.x - hcx:F2}, {butt.y:F2}, {butt.z:F2}).");
                if (charge)
                {
                    Vector3 tip = lance.TransformPoint(new Vector3(0f, LanceTip, 0f));
                    float reach = Vector3.Dot(tip - root.transform.position, forward);
                    float level = Vector3.Dot(lance.up, forward);
                    if (reach < 2f || level < .95f || tip.y < .8f || tip.y > 2.2f)
                        throw new InvalidOperationException($"Charge does not couch the lance forward (reach {reach:F2} m, tip height {tip.y:F2} m, level {level:F2}).");
                    lines.Add($"Charge: lance tip {reach:F2} m in front of the pivot at {tip.y:F2} m height at contact.");
                }
            }
            // The gallop must actually move the hooves.
            AnimationClip gallop = clips[1];
            Transform hoof = horseRig.Find("LeftFrontFoot");
            gallop.SampleAnimation(root, 0f);
            Vector3 a = hoof.position;
            gallop.SampleAnimation(root, gallop.length * .5f);
            float stride = Vector3.Distance(a, hoof.position);
            if (stride < .2f) throw new InvalidOperationException("Knight gallop does not move the hooves.");
            lines.Add($"Gallop: front fetlock travels {stride:F2} m between half cycles.");
            horseRig.ResetPose(); riderRig.ResetPose();
            return string.Join("\n", lines) + "\n";
        }
    }
}

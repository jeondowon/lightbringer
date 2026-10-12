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
    // Project-native prototype rig for the Shieldbearer: an A-posed heavy knight in a long split coat. A tall
    // tower shield is braced in front of the body on the left hand and a flanged mace rides in the right.
    // Landmarks were measured on the placed Tripo export (1.7 m, feet on the pivot, facing +Z); the coat hangs
    // on Hips/Tabard/Cape/Skirt bones so the legs can step inside it. The downloaded FBX stays untouched.
    internal static class ShieldbearerRigSetup
    {
        private const string Folder = "Assets/Art/Characters/Shieldbearer";
        private const string RigFolder = Folder + "/Rigged";
        private const string PrefabPath = RigFolder + "/Shieldbearer_Rigged.prefab";
        private const string ReportPath = "Docs/Validation/ShieldbearerRigChecks.txt";
        private const float ContactPhase = .45f;
        // Tower shield (grip at the origin, face towards +Z) and mace (grip at the origin, head along +Y).
        private const float ShieldWidth = .80f, ShieldHeight = 1.40f;
        private static readonly Vector3 ShieldCentre = new Vector3(.12f, -.15f, .08f);
        private const float MaceHeadY = .56f;

        // Measured on the placed export; z values are relative to the helmet's depth (about +0.07 m).
        private static readonly RobedHumanoid.Spec Body = new RobedHumanoid.Spec
        {
            HeadBase = 1.48f, NeckBase = 1.42f, ChestBase = 1.16f, Belt = .95f, HipY = .80f, KneeY = .53f, HipDx = .12f, ToeZ = .10f,
            PadMinY = 1.36f, PadMaxY = 1.40f, PadDx = .25f, JointDx = .22f, ElbowMinY = 1.16f, ElbowMaxY = 1.20f, ElbowDx = .28f,
            WristMinY = .98f, WristMaxY = 1.02f, WristDx = .34f, PalmMinY = .84f, PalmMaxY = .92f, PalmDx = .39f,
            AnkleMinY = .12f, AnkleMaxY = .16f, AnkleDxMin = .08f, AnkleDxMax = .36f, AnkleZMin = -.25f, AnkleZMax = .05f,
            ArmRadius = .10f, LegRadius = .11f,
            TabardTop = new Vector3(0f, .92f, .05f), TabardBottom = new Vector3(0f, .38f, .11f),
            CapeTop = new Vector3(0f, 1.40f, -.23f), CapeBottom = new Vector3(0f, .28f, -.35f),
            SkirtTopDx = .28f, SkirtTopY = .92f, SkirtBottomDx = .40f, SkirtBottomY = .34f,
            TabardMinZ = -.02f, TabardHalfWidth = .2f, CapeMaxZ = -.17f,
        };

        private static RobedHumanoid body;
        private static GeneratedRig rig => body.Rig;
        private static float cx => body.Cx;

        public static VisualOverride Build(GameObject source, Matrix4x4 toUnit, ArtStyleLibrary library)
        {
            if (!AssetDatabase.IsValidFolder(RigFolder)) AssetDatabase.CreateFolder(Folder, "Rigged");
            Scene scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Shieldbearer_Rigged");
            SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                Mesh mesh = GeneratedRig.BakePlaced(source, toUnit, scene, "Shieldbearer_Skinned");
                body = new RobedHumanoid(root.transform, Body);
                body.Build(root.transform, mesh.vertices);
                body.Skin(mesh);
                mesh = SaveAsset(mesh, RigFolder + "/Shieldbearer_Skinned.asset");
                SkinnedMeshRenderer skin = rig.AddSkin("Body", mesh, AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Shieldbearer.mat"),
                    new Bounds(new Vector3(0f, .9f, .1f), new Vector3(1.6f, 2.1f, 1.8f)));
                Transform shield = BuildShield();
                Transform mace = BuildMace();

                AnimationClip idle = MakeClip(root.transform, "Idle", 2.4f, WrapMode.Loop);
                AnimationClip walk = MakeClip(root.transform, "Walk", 1.1f, WrapMode.Loop);
                AnimationClip bash = MakeClip(root.transform, "Bash", .8f, WrapMode.Once);
                string checks = Validate(root, skin, shield, mace, idle, walk, bash);
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (prefab == null) throw new InvalidOperationException("Could not save the Shieldbearer rig prefab.");
                File.WriteAllText(ReportPath, $"PASS | {DateTime.UtcNow:O}\n"
                    + $"Unity-native prototype skeleton: {rig.Bones.Count} bones; {mesh.vertexCount} weighted vertices.\n"
                    + checks + $"Tower shield {ShieldWidth:0.00} x {ShieldHeight:0.00} m on LeftHand; flanged mace on RightHand. Original FBX preserved.\n"
                    + "No external animation assets. Hand-tuned landmark weights and generated prototype clips.\n"
                    + "Visual review of joints and the coat remains manual; no mesh decimation performed.\n");
                return new VisualOverride
                {
                    id = VisualId.Shieldbearer, prefab = prefab, scale = 1f,
                    idleClip = idle, moveClip = walk, actionClip = bash, moveClipSpeed = 2.2f,
                    attachments = new PropAttachment[0], // Shield and mace are children of the hand bones.
                };
            }
            finally
            {
                body = null;
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        // ---------- Props ----------

        private static Transform BuildShield()
        {
            Material face = WeaponMeshes.BlueEnamel(RigFolder, "TowerEnamel");
            Material gold = WeaponMeshes.AgedGold(RigFolder, "TowerShieldAgedGold");
            Material steel = WeaponMeshes.BrushedSteel(RigFolder, "TowerShieldBrushedSteel");
            Transform hand = rig.Find("LeftHand");
            var shield = new GameObject("Shield").transform;
            shield.SetParent(hand, false);
            shield.position = CentreOf(body.Palm("Left"));
            shield.rotation = Quaternion.identity;
            var board = new GameObject("Board").transform;
            board.SetParent(shield, false);
            board.localPosition = ShieldCentre;
            WeaponMeshes.TowerShield("Tower", new Vector2(-ShieldCentre.x, -ShieldCentre.y), out Mesh paint, out Mesh trim, out Mesh metal);
            MeshPart(board, "Face", paint, face, RigFolder);
            MeshPart(board, "Gold", trim, gold, RigFolder);
            MeshPart(board, "Steel", metal, steel, RigFolder);
            return shield;
        }

        private static Transform BuildMace()
        {
            Material leather = WeaponMeshes.PebbledLeather(RigFolder, "MaceGripLeather", new Color(.5f, .3f, .18f));
            Material steel = WeaponMeshes.BrushedSteel(RigFolder, "MaceBrushedSteel");
            Material gold = WeaponMeshes.AgedGold(RigFolder, "MaceAgedGold");
            Transform hand = rig.Find("RightHand");
            var mace = new GameObject("Mace").transform;
            mace.SetParent(hand, false);
            mace.position = CentreOf(body.Palm("Right"));
            mace.rotation = Quaternion.identity;
            WeaponMeshes.Mace("Mace", MaceHeadY, out Mesh grip, out Mesh head, out Mesh trim);
            MeshPart(mace, "Grip", grip, leather, RigFolder);
            MeshPart(mace, "Head", head, steel, RigFolder);
            MeshPart(mace, "Trim", trim, gold, RigFolder);
            return mace;
        }

        private static Vector3 CentreOf(int[] indices)
        {
            Vector3[] vertices = rig.Bones[0].root.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh.vertices;
            Vector3 sum = Vector3.zero;
            foreach (int index in indices) sum += vertices[index];
            return sum / indices.Length;
        }


        // ---------- Clips ----------

        private static AnimationClip MakeClip(Transform root, string role, float duration, WrapMode wrap)
        {
            Transform hips = rig.Find("Hips");
            Vector3 hipsRest = rig.RestPosition(hips);
            return GeneratedRig.Clip(root, rig.Bones, RigFolder + "/Shieldbearer_" + role + ".anim", duration, wrap,
                (bone, phase) => Rotation(bone.name, role, phase),
                (bone, phase) => bone == hips ? hipsRest + Vector3.up * Lift(role, phase) : (Vector3?)null);
        }

        private static float Lift(string role, float phase) => role == "Walk" ? .02f * (1f - Mathf.Cos(phase * Mathf.PI * 4f))
            : role == "Bash" ? -.05f * Strike(phase) : .006f * Mathf.Sin(phase * Mathf.PI * 2f);

        // Bash timing: raise the mace, then chop over the shield while shoving it forward (contact 0.45).
        private static float Windup(float phase) => GeneratedRig.Pulse(phase, 0f, .3f, .3f, .45f);
        private static float Strike(float phase) => GeneratedRig.Pulse(phase, .3f, ContactPhase, .55f, 1f);

        private static Vector3 Pose(string name, string role, float phase)
        {
            float wave = Mathf.Sin(phase * Mathf.PI * 2f);
            Vector3 pose = Vector3.zero;
            // Braced guard: the shield arm forward and in front of the body, the mace held up beside it.
            if (name == "LeftUpperArm") pose = new Vector3(-40, 0, 25);
            if (name == "LeftForeArm") pose = new Vector3(-50, 0, 0);
            if (name == "RightUpperArm") pose = new Vector3(-15, 0, -18);
            if (name == "RightForeArm") pose = new Vector3(-55, 0, 0);
            if (name == "Chest") pose.x = wave * 1.2f;
            if (role == "Walk")
            {
                float side = name.StartsWith("Right") ? 1f : -1f;
                // A heavy, short stride inside the coat.
                if (name.EndsWith("Thigh")) pose.x = -side * wave * 18;
                if (name.EndsWith("Shin")) pose.x = Mathf.Max(0, side * wave) * 26;
                if (name.EndsWith("Foot")) pose.x = -Mathf.Max(0, side * wave) * 10;
                if (name.EndsWith("Skirt")) pose.x = -side * wave * 8;
                if (name == "Tabard") pose.x = -4 + Mathf.Abs(wave) * -4;
                if (name == "Cape") pose.x = 6 + wave * 2;
                if (name == "Chest") pose = new Vector3(4, wave * 3, 0);
                if (name == "RightUpperArm") pose.x += wave * 6;
            }
            if (role == "Bash")
            {
                float w = Windup(phase), s = Strike(phase);
                if (name == "RightUpperArm") pose += new Vector3(-115 * w - 65 * s, 0, 10 * w);
                if (name == "RightForeArm") pose.x += -40 * w + 40 * s;
                if (name == "LeftUpperArm") pose.x += -18 * s;
                if (name == "LeftForeArm") pose.x += 14 * s;
                if (name == "Chest") pose = new Vector3(6 * s, 12 * w - 10 * s, 0);
                if (name == "Spine") pose.x = 4 * s;
                if (name == "LeftThigh") pose.x = -14 * s;
                if (name == "LeftShin") pose.x = 12 * s;
                if (name == "RightThigh") pose.x = 10 * s;
                if (name == "Tabard") pose.x = -6 * s;
            }
            return pose;
        }

        private static Quaternion Rotation(string name, string role, float phase)
        {
            if (name != "RightHand" && name != "LeftHand") return Quaternion.Euler(Pose(name, role, phase));
            // Props are aimed in root space: the shield faces the enemy, the mace tilts forward.
            if (name == "LeftHand") return RobedHumanoid.Aim("Left", bone => Pose(bone, role, phase), Quaternion.Euler(0f, -4f, 0f));
            float pitch = 20f;
            if (role == "Bash") pitch += -70f * Windup(phase) + 80f * Strike(phase);
            return RobedHumanoid.Aim("Right", bone => Pose(bone, role, phase), Quaternion.Euler(pitch, 0f, 0f));
        }

        // ---------- Validation ----------

        private static string Validate(GameObject root, SkinnedMeshRenderer skin, Transform shield, Transform mace, params AnimationClip[] clips)
        {
            GeneratedRig.CheckWeights(skin);
            if (shield.parent != rig.Find("LeftHand") || mace.parent != rig.Find("RightHand"))
                throw new InvalidOperationException("Shieldbearer props are not attached to the hand bones.");
            Vector3[] rest = GeneratedRig.RestVertices(skin);
            Vector3 forward = root.transform.forward;
            Transform board = shield.Find("Board");
            var lines = new List<string>();
            foreach (AnimationClip clip in clips)
            {
                rig.ResetPose();
                float motion = GeneratedRig.Motion(root, skin, clip, .3f, rest);
                if (motion < .001f || motion > 1.5f) throw new InvalidOperationException("Invalid deformation in " + clip.name);
                bool bash = clip.name.EndsWith("Bash");
                clip.SampleAnimation(root, clip.length * (bash ? ContactPhase : .5f));
                float grip = Mathf.Max(Vector3.Distance(GeneratedRig.Centroid(skin, body.Palm("Left")), shield.position),
                    Vector3.Distance(GeneratedRig.Centroid(skin, body.Palm("Right")), mace.position));
                Vector3 centre = board.position;
                float bottom = centre.y - ShieldHeight * .5f;
                float ahead = Vector3.Dot(centre - root.transform.position, forward);
                if (grip > .015f) throw new InvalidOperationException("Prop/palm alignment failed for " + clip.name);
                if (bottom < .02f) throw new InvalidOperationException($"Shield goes through the ground in {clip.name} ({bottom:F2} m).");
                if (ahead < .25f || Vector3.Dot(board.forward, forward) < .97f || Mathf.Abs(centre.x - cx) > .3f)
                    throw new InvalidOperationException($"Shield does not cover the front in {clip.name} (ahead {ahead:F2} m, x {centre.x - cx:F2}).");
                lines.Add($"{clip.name}: weighted vertex motion {motion:F3} m; grip error {grip:F4} m; shield centre {ahead:F2} m ahead, "
                    + $"{centre.x - cx:+0.00;-0.00} m across, {bottom:F2}-{bottom + ShieldHeight:F2} m high.");
                if (bash)
                {
                    Vector3 head = mace.TransformPoint(new Vector3(0f, MaceHeadY, 0f));
                    float reach = Vector3.Dot(head - root.transform.position, forward);
                    if (reach < .7f || head.y < .6f || head.y > 1.7f)
                        throw new InvalidOperationException($"Mace does not strike ahead (reach {reach:F2} m, height {head.y:F2} m).");
                    lines.Add($"Bash: mace head {reach:F2} m in front of the pivot at {head.y:F2} m height at contact.");
                }
            }
            rig.ResetPose();
            return string.Join("\n", lines) + "\n";
        }
    }
}

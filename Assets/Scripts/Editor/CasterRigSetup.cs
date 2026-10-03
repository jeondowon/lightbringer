using System;
using System.Collections.Generic;
using System.IO;
using Lightbringer.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static Lightbringer.EditorTools.RigBuildUtility;

namespace Lightbringer.EditorTools
{
    // Project-native prototype rigs for the staff casters. The Mage (hooded coat and boots) carries a dark staff
    // with a gold claw holding a glowing rune crystal and casts by thrusting it at the enemy; the Priest (long
    // white robe) carries a white-and-gold holy staff with a radiant sun and raises it overhead for the heal
    // pulse. Both staffs rest their butt on the ground at idle. Measurements come from the placed Tripo exports
    // (1.7 m, feet on the pivot, facing +Z); the downloaded FBX files stay untouched.
    internal static class CasterRigSetup
    {
        private const float ContactPhase = .5f;

        private sealed class Caster
        {
            public string Name;
            public VisualId Id;
            public RobedHumanoid.Spec Body;
            public string Action;
            public float ButtY, HeadY;            // staff space: grip at the origin, shaft along +Y
            public Func<string, Transform> BuildStaff;
            public Func<string, string, float, Vector3> Pose;
            public Func<string, float, float> StaffPitch;
            public string RigFolder => $"Assets/Art/Characters/{Name}/Rigged";
        }

        private static RobedHumanoid body;

        // z values are relative to the hood's depth (about +0.16 m: the coat tails trail far behind).
        private static readonly Caster Mage = new Caster
        {
            Name = "Mage", Id = VisualId.Mage, Action = "Cast", ButtY = -.86f, HeadY = 1.0f,
            Body = new RobedHumanoid.Spec
            {
                HeadBase = 1.45f, NeckBase = 1.40f, ChestBase = 1.18f, Belt = 1.05f, HipY = .85f, KneeY = .55f, HipDx = .10f, ToeZ = .14f,
                PadMinY = 1.36f, PadMaxY = 1.40f, PadDx = .15f, JointDx = .19f, ElbowMinY = 1.17f, ElbowMaxY = 1.21f, ElbowDx = .28f,
                WristMinY = .99f, WristMaxY = 1.03f, WristDx = .40f, PalmMinY = .88f, PalmMaxY = .94f, PalmDx = .40f,
                AnkleMinY = .10f, AnkleMaxY = .14f, AnkleDxMin = .10f, AnkleDxMax = .35f, AnkleZMin = -.15f, AnkleZMax = .15f,
                ArmRadius = .08f, LegRadius = .09f,
                TabardTop = new Vector3(0f, 1.0f, .10f), TabardBottom = new Vector3(0f, .35f, .14f),
                CapeTop = new Vector3(0f, 1.38f, -.12f), CapeBottom = new Vector3(0f, .30f, -.45f),
                SkirtTopDx = .20f, SkirtTopY = 1.0f, SkirtBottomDx = .42f, SkirtBottomY = .30f,
                TabardMinZ = .03f, TabardHalfWidth = .14f, CapeMaxZ = -.10f,
            },
            BuildStaff = MageStaff, Pose = MagePose, StaffPitch = MagePitch,
        };

        // z values are relative to the hood's depth (about +0.05 m).
        private static readonly Caster Priest = new Caster
        {
            Name = "Priest", Id = VisualId.Priest, Action = "Bless", ButtY = -.86f, HeadY = 1.02f,
            Body = new RobedHumanoid.Spec
            {
                HeadBase = 1.42f, NeckBase = 1.38f, ChestBase = 1.18f, Belt = 1.05f, HipY = .85f, KneeY = .52f, HipDx = .10f, ToeZ = .07f,
                PadMinY = 1.33f, PadMaxY = 1.37f, PadDx = .14f, JointDx = .16f, ElbowMinY = 1.16f, ElbowMaxY = 1.20f, ElbowDx = .17f,
                WristMinY = .93f, WristMaxY = .97f, WristDx = .36f, PalmMinY = .82f, PalmMaxY = .87f, PalmDx = .37f,
                AnkleMinY = .05f, AnkleMaxY = .09f, AnkleDxMin = .10f, AnkleDxMax = .33f, AnkleZMin = -.15f, AnkleZMax = .15f,
                ArmRadius = .11f, LegRadius = .10f,
                TabardTop = new Vector3(0f, 1.0f, .08f), TabardBottom = new Vector3(0f, .10f, .12f),
                CapeTop = new Vector3(0f, 1.35f, -.10f), CapeBottom = new Vector3(0f, .12f, -.20f),
                SkirtTopDx = .16f, SkirtTopY = 1.0f, SkirtBottomDx = .34f, SkirtBottomY = .12f,
                TabardMinZ = .02f, TabardHalfWidth = .12f, CapeMaxZ = -.06f,
            },
            BuildStaff = PriestStaff, Pose = PriestPose, StaffPitch = PriestPitch,
        };

        public static VisualOverride BuildMage(GameObject source, Matrix4x4 toUnit) => Build(Mage, source, toUnit);
        public static VisualOverride BuildPriest(GameObject source, Matrix4x4 toUnit) => Build(Priest, source, toUnit);

        private static VisualOverride Build(Caster c, GameObject source, Matrix4x4 toUnit)
        {
            string folder = "Assets/Art/Characters/" + c.Name;
            if (!AssetDatabase.IsValidFolder(c.RigFolder)) AssetDatabase.CreateFolder(folder, "Rigged");
            Scene scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject(c.Name + "_Rigged");
            SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                Mesh mesh = GeneratedRig.BakePlaced(source, toUnit, scene, c.Name + "_Skinned");
                body = new RobedHumanoid(root.transform, c.Body);
                body.Build(root.transform, mesh.vertices);
                body.Skin(mesh);
                mesh = SaveAsset(mesh, c.RigFolder + "/" + c.Name + "_Skinned.asset");
                SkinnedMeshRenderer skin = body.Rig.AddSkin("Body", mesh, AssetDatabase.LoadAssetAtPath<Material>(folder + "/" + c.Name + ".mat"),
                    new Bounds(new Vector3(0f, .95f, 0f), new Vector3(1.6f, 2.2f, 1.6f)));
                Transform staff = c.BuildStaff(c.RigFolder);
                staff.SetParent(body.Rig.Find("RightHand"), false);
                staff.position = body.PalmCentre("Right", mesh.vertices);
                staff.rotation = Quaternion.identity;

                AnimationClip idle = MakeClip(c, root.transform, "Idle", 2.4f, WrapMode.Loop);
                AnimationClip walk = MakeClip(c, root.transform, "Walk", 1f, WrapMode.Loop);
                AnimationClip action = MakeClip(c, root.transform, c.Action, 1f, WrapMode.Once);
                string checks = Validate(c, root, skin, staff, idle, walk, action);
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, c.RigFolder + "/" + c.Name + "_Rigged.prefab");
                if (prefab == null) throw new InvalidOperationException($"Could not save the {c.Name} rig prefab.");
                File.WriteAllText($"Docs/Validation/{c.Name}RigChecks.txt", $"PASS | {DateTime.UtcNow:O}\n"
                    + $"Unity-native prototype skeleton: {body.Rig.Bones.Count} bones; {mesh.vertexCount} weighted vertices.\n"
                    + checks + "Staff on RightHand (grip at the palm). Original FBX preserved.\n"
                    + "No external animation assets. Hand-tuned landmark weights and generated prototype clips.\n"
                    + "Visual review of the sleeves and the robe remains manual; no mesh decimation performed.\n");
                return new VisualOverride
                {
                    id = c.Id, prefab = prefab, scale = 1f,
                    idleClip = idle, moveClip = walk, actionClip = action, moveClipSpeed = 3f,
                    attachments = new PropAttachment[0], // The staff is a child of the hand bone in this prefab.
                };
            }
            finally
            {
                body = null;
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        // ---------- Staffs ----------

        private static Transform MageStaff(string folder)
        {
            Material wood = MakeMaterial(folder, "StaffWood", new Color(.16f, .09f, .06f), 0f, .3f);
            Material gold = MakeMaterial(folder, "StaffGold", new Color(.8f, .58f, .22f), .75f, .5f);
            Material leather = MakeMaterial(folder, "StaffGrip", new Color(.1f, .06f, .04f), 0f, .25f);
            Material crystal = MakeGlowMaterial(folder, "RuneCrystal", new Color(.35f, .6f, 1f), 2.2f);
            var staff = new GameObject("Staff").transform;
            AddMesh(staff, "Shaft", GeneratedRig.Lathe("MageStaffShaft", new[] { Mage.ButtY, Mage.ButtY + .04f, .6f, .7f },
                new[] { .016f, .022f, .024f, .03f }), wood, folder);
            Part(staff, "Grip", PrimitiveType.Cylinder, Vector3.zero, new Vector3(.034f, .08f, .034f), leather);
            Part(staff, "Ferrule", PrimitiveType.Cylinder, new Vector3(0f, Mage.ButtY + .03f, 0f), new Vector3(.03f, .03f, .03f), gold);
            Part(staff, "Collar", PrimitiveType.Cylinder, new Vector3(0f, .7f, 0f), new Vector3(.05f, .025f, .05f), gold);
            // Three gold claws curl up around the crystal.
            for (int i = 0; i < 3; i++)
            {
                var claw = new GameObject("Claw" + i).transform;
                claw.SetParent(staff, false);
                claw.localPosition = new Vector3(0f, .72f, 0f);
                claw.localRotation = Quaternion.Euler(0f, i * 120f, 0f);
                Part(claw, "Lower", PrimitiveType.Cube, new Vector3(.05f, .08f, 0f), new Vector3(.018f, .17f, .025f), gold).localRotation = Quaternion.Euler(0f, 0f, -25f);
                Part(claw, "Upper", PrimitiveType.Cube, new Vector3(.065f, .22f, 0f), new Vector3(.016f, .14f, .022f), gold).localRotation = Quaternion.Euler(0f, 0f, 20f);
            }
            AddMesh(staff, "Crystal", GeneratedRig.Lathe("RuneCrystal", new[] { .78f, .86f, Mage.HeadY }, new[] { 0f, .055f, 0f }, 4), crystal, folder);
            return staff;
        }

        private static Transform PriestStaff(string folder)
        {
            Material ivory = MakeMaterial(folder, "StaffIvory", new Color(.92f, .9f, .84f), .1f, .5f);
            Material gold = MakeMaterial(folder, "StaffGold", new Color(.85f, .62f, .24f), .8f, .55f);
            Material holy = MakeGlowMaterial(folder, "HolyOrb", new Color(1f, .86f, .55f), 2.5f);
            var staff = new GameObject("Staff").transform;
            AddMesh(staff, "Shaft", GeneratedRig.Lathe("PriestStaffShaft", new[] { Priest.ButtY, Priest.ButtY + .04f, .66f, .7f },
                new[] { .016f, .022f, .022f, .026f }), ivory, folder);
            foreach (float y in new[] { Priest.ButtY + .03f, -.06f, .06f, .4f })
                Part(staff, "Band", PrimitiveType.Cylinder, new Vector3(0f, y, 0f), new Vector3(.032f, .012f, .032f), gold);
            Part(staff, "Collar", PrimitiveType.Cylinder, new Vector3(0f, .7f, 0f), new Vector3(.045f, .02f, .045f), gold);
            // Radiant sun: a gold ring of rays around the holy orb, crowned by a spike.
            Vector3 centre = new Vector3(0f, .86f, 0f);
            for (int i = 0; i < 12; i++)
            {
                float length = i % 2 == 0 ? .2f : .13f;
                Part(staff, "Ray" + i, PrimitiveType.Cube, centre, new Vector3(.018f, length * 2f, .014f), gold)
                    .localRotation = Quaternion.Euler(0f, 0f, i * 15f);
            }
            Part(staff, "Halo", PrimitiveType.Cylinder, centre, new Vector3(.17f, .006f, .17f), gold).localRotation = Quaternion.Euler(90f, 0f, 0f);
            Part(staff, "Orb", PrimitiveType.Sphere, centre, Vector3.one * .1f, holy);
            Part(staff, "Spike", PrimitiveType.Cube, new Vector3(0f, Priest.HeadY + .02f, 0f), new Vector3(.03f, .1f, .03f), gold);
            return staff;
        }

        private static void AddMesh(Transform parent, string name, Mesh mesh, Material material, string folder)
        {
            mesh = SaveAsset(mesh, folder + "/" + mesh.name + ".asset");
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        // ---------- Clips ----------

        private static AnimationClip MakeClip(Caster c, Transform root, string role, float duration, WrapMode wrap)
        {
            GeneratedRig rig = body.Rig;
            Transform hips = rig.Find("Hips");
            Vector3 hipsRest = rig.RestPosition(hips);
            return GeneratedRig.Clip(root, rig.Bones, $"{c.RigFolder}/{c.Name}_{role}.anim", duration, wrap,
                (bone, phase) => bone.name == "RightHand"
                    ? RobedHumanoid.Aim("Right", name => c.Pose(name, role, phase), Quaternion.Euler(c.StaffPitch(role, phase), 0f, 0f))
                    : Quaternion.Euler(c.Pose(bone.name, role, phase)),
                (bone, phase) => bone == hips ? hipsRest + Vector3.up * Lift(role, phase) : (Vector3?)null);
        }

        private static float Lift(string role, float phase) => role == "Walk" ? .016f * (1f - Mathf.Cos(phase * Mathf.PI * 4f))
            : .006f * Mathf.Sin(phase * Mathf.PI * 2f);

        // Action timing: gather (to 0.3), release at contact (0.5), recover.
        private static float Gather(float phase) => GeneratedRig.Pulse(phase, 0f, .3f, .3f, ContactPhase);
        private static float Release(float phase) => GeneratedRig.Pulse(phase, .3f, ContactPhase, .65f, 1f);

        private static Vector3 Walk(string name, float phase, Vector3 pose)
        {
            float wave = Mathf.Sin(phase * Mathf.PI * 2f), side = GeneratedRig.Side(name);
            if (name.EndsWith("Thigh")) pose.x = -side * wave * 22;
            if (name.EndsWith("Shin")) pose.x = Mathf.Max(0, side * wave) * 30;
            if (name.EndsWith("Foot")) pose.x = -Mathf.Max(0, side * wave) * 12;
            if (name.EndsWith("Skirt")) pose.x = -side * wave * 9;
            if (name == "Tabard") pose.x = -5 - Mathf.Abs(wave) * 4;
            if (name == "Cape") pose.x = 8 + wave * 3;
            if (name == "Chest") pose = new Vector3(3, wave * 3, 0);
            if (name == "LeftUpperArm") pose.x += -wave * 12;
            return pose;
        }

        // Staff hand forward at the waist with the staff planted; the free hand relaxed.
        private static Vector3 Rest(string name, float phase)
        {
            Vector3 pose = Vector3.zero;
            if (name == "RightUpperArm") pose = new Vector3(-8, 0, -14);
            if (name == "RightForeArm") pose = new Vector3(-40, 0, 0);
            if (name == "LeftUpperArm") pose = new Vector3(-6, 0, 16);
            if (name == "LeftForeArm") pose = new Vector3(-22, 0, 0);
            if (name == "Chest") pose.x = 1.2f * Mathf.Sin(phase * Mathf.PI * 2f);
            return pose;
        }

        private static Vector3 MagePose(string name, string role, float phase)
        {
            Vector3 pose = Rest(name, phase);
            if (role == "Walk") return Walk(name, phase, pose);
            if (role != Mage.Action) return pose;
            // Draw the staff back, then thrust the crystal at the target with the off hand guiding the spell.
            float g = Gather(phase), r = Release(phase);
            if (name == "RightUpperArm") pose += new Vector3(-30 * g - 62 * r, 0, 6 * r);
            if (name == "RightForeArm") pose.x += -35 * g + 25 * r;
            if (name == "LeftUpperArm") pose += new Vector3(-20 * g - 62 * r, 0, -14 * r);
            if (name == "LeftForeArm") pose.x += -30 * g + 12 * r;
            if (name == "Chest") pose = new Vector3(-4 * g + 6 * r, 12 * g - 8 * r, 0);
            if (name == "LeftThigh") pose.x = -12 * r;
            if (name == "LeftShin") pose.x = 10 * r;
            if (name == "Cape") pose.x = 6 * r;
            return pose;
        }

        private static float MagePitch(string role, float phase) => role == "Walk" ? 12f + 4f * Mathf.Sin(phase * Mathf.PI * 2f)
            : role == Mage.Action ? 6f - 20f * Gather(phase) + 62f * Release(phase) : 6f;

        private static Vector3 PriestPose(string name, string role, float phase)
        {
            Vector3 pose = Rest(name, phase);
            if (role == "Walk") return Walk(name, phase, pose);
            if (role != Priest.Action) return pose;
            // Lift the staff high and open the free hand in blessing.
            float g = Gather(phase), r = Release(phase), up = Mathf.Max(g, r);
            if (name == "RightUpperArm") pose += new Vector3(-110 * up, 0, 10 * up);
            if (name == "RightForeArm") pose.x += 20 * up;
            if (name == "LeftUpperArm") pose += new Vector3(-70 * r, 0, -10 * r);
            if (name == "LeftForeArm") pose.x += -35 * r;
            if (name == "Chest") pose.x = -6 * up;
            if (name == "Neck") pose.x = -8 * up;
            return pose;
        }

        private static float PriestPitch(string role, float phase) => role == "Walk" ? 12f + 4f * Mathf.Sin(phase * Mathf.PI * 2f)
            : role == Priest.Action ? 6f - 4f * Mathf.Max(Gather(phase), Release(phase)) : 6f;

        // ---------- Validation ----------

        private static string Validate(Caster c, GameObject root, SkinnedMeshRenderer skin, Transform staff, params AnimationClip[] clips)
        {
            GeneratedRig.CheckWeights(skin);
            if (staff.parent != body.Rig.Find("RightHand")) throw new InvalidOperationException(c.Name + " staff is not attached to the right hand.");
            Vector3[] rest = GeneratedRig.RestVertices(skin);
            Vector3 forward = root.transform.forward;
            var lines = new List<string>();
            foreach (AnimationClip clip in clips)
            {
                body.Rig.ResetPose();
                float motion = GeneratedRig.Motion(root, skin, clip, .3f, rest);
                if (motion < .001f || motion > 1.5f) throw new InvalidOperationException("Invalid deformation in " + clip.name);
                bool action = clip.name.EndsWith(c.Action);
                clip.SampleAnimation(root, clip.length * (action ? ContactPhase : .5f));
                float grip = Vector3.Distance(GeneratedRig.Centroid(skin, body.Palm("Right")), staff.position);
                float butt = staff.TransformPoint(new Vector3(0f, c.ButtY, 0f)).y;
                Vector3 head = staff.TransformPoint(new Vector3(0f, c.HeadY, 0f));
                if (grip > .015f) throw new InvalidOperationException("Staff/palm alignment failed for " + clip.name);
                if (butt < -.03f) throw new InvalidOperationException($"{c.Name} staff goes through the ground in {clip.name} ({butt:F2} m).");
                lines.Add($"{clip.name}: weighted vertex motion {motion:F3} m; grip error {grip:F4} m; staff butt {butt:F2} m, head at {head.y:F2} m.");
                if (!action) continue;
                float reach = Vector3.Dot(head - root.transform.position, forward);
                if (c == Mage && (reach < .7f || head.y < 1.3f))
                    throw new InvalidOperationException($"Cast does not aim the crystal forward (reach {reach:F2} m, height {head.y:F2} m).");
                if (c == Priest && head.y < 2f)
                    throw new InvalidOperationException($"Blessing does not raise the staff overhead (head {head.y:F2} m).");
                lines.Add($"{clip.name}: staff head {reach:F2} m in front of the pivot at {head.y:F2} m height at the release.");
            }
            body.Rig.ResetPose();
            return string.Join("\n", lines) + "\n";
        }
    }
}

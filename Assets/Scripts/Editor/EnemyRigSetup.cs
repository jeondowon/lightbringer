using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Lightbringer.Units;
using Lightbringer.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static Lightbringer.EditorTools.RigBuildUtility;

namespace Lightbringer.EditorTools
{
    // Project-native rigs for the A-posed Tripo enemies (Raider, Swarmling, Brute, Shaman, Warlord), with no
    // external rigging or animation. Each source GLB (Enemy*/Source/*_Source.glb, 1 m tall, untouched) is placed
    // at 1.7 m with the feet on the pivot facing +Z; the gameplay size comes from the override scale.
    // Instead of hand-measured specs, the RobedHumanoid landmarks are measured from the mesh: in the A pose the
    // arms stand apart from the torso, so height slices separate arm and body by the widest gap, and the crotch
    // gap, ankles and toes place the legs. Weapons are generated primitives on the hand bones; idle, move and
    // attack clips are generated with the attack's contact at the enemy's strike phase (EnemyCatalog).
    [InitializeOnLoad]
    internal static class EnemyRigSetup
    {
        private const string Request = "Docs/Validation/EnemyRigs.request";
        private const string ReportPath = "Docs/Validation/EnemyRigChecks.txt";
        private const float H = 1.7f;               // rig height (unit space)
        private const float TroopVisualScale = 1.2f; // UnitAppearance's troop scale
        private const int CrowdBudget = 12000;

        private sealed class Enemy
        {
            public VisualId Id;
            public EnemyKind Kind;
            public float Height;                     // gameplay height before the troop scale
            public bool Robe;                        // robe or armoured skirt hanging over the legs
            public string Move = "Walk";
            public float MoveLength = 1f, Stride = 22f;
            public string Action;
            public Func<Transform, Transform, List<Weapon>> Weapons; // (right hand, left hand)
            public Func<string, string, float, Vector3> Pose;        // (bone, role, phase) -> euler
            public Func<string, float, Vector3> RightAim, LeftAim;   // weapon aim in root space; null = free hand
            public float Lift(string role, float phase) => role == Move
                ? (Move == "Run" ? .03f : .016f) * (1f - Mathf.Cos(phase * Mathf.PI * 4f))
                : role == "Idle" ? .006f * Mathf.Sin(phase * Mathf.PI * 2f) : -.05f * Strike(this, phase);
            public string Folder => "Assets/Art/Characters/" + Id;
            public string RigFolder => Folder + "/Rigged";
            public float Contact => EnemyCatalog.StrikePhase(Kind);
        }

        private sealed class Weapon
        {
            public Transform Root;
            public string Hand;
            public Vector3 Strike;                   // local point that lands the blow (or casts)
        }

        private static RobedHumanoid body;
        private static GeneratedRig rig => body.Rig;
        private static Materials mats;
        private static Scene weaponScene;       // preview scene of the rig being built, for baking imported weapon meshes
        private static string weaponFolder;

        static EnemyRigSetup() => EditorRequests.Register(Request, BuildAll);

        [MenuItem("Lightbringer/Art/Build Enemy Rigs")]
        public static void BuildAll()
        {
            ArtStyleLibrary library = AssetDatabase.LoadAssetAtPath<ArtStyleLibrary>(ArtStyleSetup.LibraryPath)
                ?? throw new InvalidOperationException("Art style library is missing.");
            var report = new StringBuilder();
            bool pass = true;
            foreach (Enemy enemy in Roster(library))
            {
                try { report.Append(Build(enemy, library)); }
                catch (Exception error)
                {
                    pass = false;
                    report.AppendLine($"FAIL {enemy.Id}: {error.Message}\n");
                    Debug.LogException(error);
                }
            }
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, (pass ? "PASS" : "FAIL") + " | Enemy rigs | " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "\n"
                + "Project-native skeletons (RobedHumanoid, auto-measured landmarks), generated weapons and clips. Source GLBs preserved.\n\n"
                + report);
            Debug.Log("Enemy rigs built" + (pass ? "" : " with failures") + "; see " + ReportPath + ". Restart Play to use them.");
        }

        [MenuItem("Lightbringer/Art/Build Enemy Rigs", true)]
        private static bool CanBuild() => !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling;

        // ---------- Build ----------

        private static string Build(Enemy e, ArtStyleLibrary library)
        {
            string sourcePath = $"{e.Folder}/Source/{e.Id}_Source.glb";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath) == null)
                AssetDatabase.ImportAsset(sourcePath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath)
                ?? throw new InvalidOperationException(sourcePath + " is missing or glTFast could not import it.");
            if (!AssetDatabase.IsValidFolder(e.RigFolder)) AssetDatabase.CreateFolder(e.Folder, "Rigged");
            mats = new Materials(e.RigFolder, library);
            var lines = new StringBuilder($"== {e.Id} ({e.Kind}, {e.Height:0.0} m, {(e.Robe ? "robe" : "no skirt")}) ==\n");
            Scene scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject(e.Id + "_Rigged");
            SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                weaponScene = scene;
                weaponFolder = e.RigFolder;
                Mesh mesh = Place(source, scene, e.Id + "_Skinned", lines);
                RobedHumanoid.Spec spec = Measure(mesh.vertices, e.Robe, lines);
                body = new RobedHumanoid(root.transform, spec);
                body.Build(root.transform, mesh.vertices);
                body.Skin(mesh);
                SkinnedMeshRenderer skin = rig.AddSkin("Body", mesh, null, new Bounds(new Vector3(0f, .9f, 0f), new Vector3(2f, 2.4f, 2f)));
                skin.sharedMaterials = SourceMaterials(source);

                List<Weapon> weapons = e.Weapons(rig.Find("RightHand"), rig.Find("LeftHand"));
                foreach (Weapon w in weapons)
                {
                    w.Root.SetParent(rig.Find(w.Hand), false);
                    w.Root.position = body.PalmCentre(w.Hand.Replace("Hand", ""), mesh.vertices);
                    w.Root.rotation = Quaternion.identity;
                }
                AnimationClip idle = MakeClip(e, root.transform, "Idle", 2.4f, WrapMode.Loop);
                AnimationClip move = MakeClip(e, root.transform, e.Move, e.MoveLength, WrapMode.Loop);
                AnimationClip action = MakeClip(e, root.transform, e.Action, 1f, WrapMode.Once);
                Validate(e, root, skin, weapons, lines, idle, move, action);

                // Crowd copy of the skinned mesh (weights and UVs unchanged); validation used the full mesh.
                Mesh crowd = CrowdMeshOptimizer.Simplify(mesh, skin.sharedMaterials, CrowdBudget, out Material[] merged);
                crowd.name = e.Id + "_Skinned_Crowd";
                crowd = SaveAsset(crowd, $"{e.RigFolder}/{crowd.name}.asset");
                lines.AppendLine($"Mesh: {mesh.vertexCount:N0} vertices, {Triangles(mesh):N0} -> {Triangles(crowd):N0} triangles (crowd copy), {rig.Bones.Count} bones.");
                skin.sharedMesh = crowd;
                skin.sharedMaterials = merged;
                rig.ResetPose();
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{e.RigFolder}/{e.Id}_Rigged.prefab")
                    ?? throw new InvalidOperationException("Could not save the rig prefab.");

                float scale = e.Height / H;
                // Leg length times the stride swing, two steps per cycle: ground speed at normal playback.
                float speed = 2f * 2f * spec.HipY * Mathf.Sin(e.Stride * Mathf.Deg2Rad) / e.MoveLength * scale * TroopVisualScale;
                Assign(library, new VisualOverride
                {
                    id = e.Id, prefab = prefab, scale = scale,
                    idleClip = idle, moveClip = move, actionClip = action, moveClipSpeed = speed,
                    attachments = new PropAttachment[0], // Weapons are children of the hand bones in this prefab.
                });
                lines.AppendLine($"Override: scale {scale:0.###}, move clip speed {speed:0.00} m/s (enemy speed {EnemyCatalog.Speed(e.Kind):0.0}), "
                    + $"contact phase {e.Contact:0.00}.\n");
                return lines.ToString();
            }
            finally
            {
                body = null;
                mats = null;
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        // Bakes the GLB at 1.7 m: feet on the pivot (feet centre on the origin), toes towards +Z.
        private static Mesh Place(GameObject source, Scene scene, string name, StringBuilder lines)
        {
            Mesh raw = GeneratedRig.BakePlaced(source, Matrix4x4.identity, scene, name);
            Vector3[] v = raw.vertices;
            UnityEngine.Object.DestroyImmediate(raw);
            float minY = v.Min(p => p.y), height = v.Max(p => p.y) - minY;
            Vector3[] feet = v.Where(p => p.y < minY + height * .04f).ToArray();
            float fx = feet.Average(p => p.x), fz = feet.Average(p => p.z);
            // Toes reach much further ahead of the ankle than the heel behind it. glTF faces +Z, so only a
            // clear reversal turns the model round.
            float ankle = v.Where(p => p.y > minY + height * .06f && p.y < minY + height * .1f).Average(p => p.z);
            float ahead = feet.Max(p => p.z) - ankle, behind = ankle - feet.Min(p => p.z);
            float yaw = behind > ahead * 1.15f ? 180f : 0f;
            float s = H / height;
            Matrix4x4 toUnit = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, yaw, 0f), Vector3.one * s)
                * Matrix4x4.Translate(new Vector3(-fx, -minY, -fz));
            lines.AppendLine($"Placement: source height {height:0.###}, scale x{s:0.###}, yaw {yaw:0} (from the ankle: toes {ahead:0.###} ahead / heels {behind:0.###} behind).");
            Mesh mesh = GeneratedRig.BakePlaced(source, Matrix4x4.identity, scene, name);
            Vector3[] placed = mesh.vertices;
            for (int i = 0; i < placed.Length; i++) placed[i] = toUnit.MultiplyPoint3x4(placed[i]);
            mesh.vertices = placed;
            // Uniform scale and a yaw only rotate the shading frames; recomputing them would split UV seams.
            Quaternion turn = Quaternion.Euler(0f, yaw, 0f);
            Vector3[] normals = mesh.normals;
            for (int i = 0; i < normals.Length; i++) normals[i] = turn * normals[i];
            if (normals.Length == placed.Length) mesh.normals = normals;
            else mesh.RecalculateNormals();
            Vector4[] tangents = mesh.tangents;
            for (int i = 0; i < tangents.Length; i++)
            {
                Vector3 t = turn * new Vector3(tangents[i].x, tangents[i].y, tangents[i].z);
                tangents[i] = new Vector4(t.x, t.y, t.z, tangents[i].w);
            }
            if (tangents.Length == placed.Length) mesh.tangents = tangents;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material[] SourceMaterials(GameObject source)
            => source.GetComponentsInChildren<MeshFilter>()
                .SelectMany(f => f.GetComponent<MeshRenderer>().sharedMaterials.Take(f.sharedMesh.subMeshCount)).ToArray();

        private static void Assign(ArtStyleLibrary library, VisualOverride entry)
        {
            Undo.RecordObject(library, "Assign " + entry.id);
            library.overrides = (library.overrides ?? new VisualOverride[0]).Where(o => o != null && o.id != entry.id)
                .Append(entry).ToArray();
            EditorUtility.SetDirty(library);
        }

        // ---------- Landmarks ----------

        // Gap between the torso (inner cluster) and the arm (outer cluster) in one height slice of one side.
        private struct Slice
        {
            public float Y, TorsoMax, ArmMin, ArmMax;
            public Vector3 ArmCentre;
            public float Gap => (TorsoMax + ArmMin) * .5f;
        }

        private static RobedHumanoid.Spec Measure(Vector3[] v, bool robe, StringBuilder lines)
        {
            Vector3 head = GeneratedRig.Landmark(v, p => p.y > .93f * H, "head");
            float cx = head.x, cz = head.z;
            var arms = new Dictionary<int, List<Slice>>();
            foreach (int sign in new[] { 1, -1 })
            {
                var slices = new List<Slice>();
                for (float y = .84f * H; y > .2f * H; y -= .01f * H)
                    if (TrySlice(v, cx, sign, y, .012f * H, out Slice slice)) slices.Add(slice);
                // The arm is the contiguous run of separated slices that reaches furthest out (the hand).
                List<List<Slice>> runs = new List<List<Slice>>();
                foreach (Slice slice in slices)
                {
                    if (runs.Count == 0 || runs[runs.Count - 1].Last().Y - slice.Y > .015f * H) runs.Add(new List<Slice>());
                    runs[runs.Count - 1].Add(slice);
                }
                List<Slice> arm = runs.OrderByDescending(r => r.Max(s => s.ArmMax)).FirstOrDefault()
                    ?? throw new InvalidOperationException((sign > 0 ? "Right" : "Left") + " arm does not stand apart from the body.");
                if (arm.Count < 6) throw new InvalidOperationException($"{(sign > 0 ? "Right" : "Left")} arm run too short ({arm.Count} slices).");
                arms[sign] = arm;
            }
            Slice At(int sign, float y) => arms[sign].OrderBy(s => Mathf.Abs(s.Y - y)).First();
            // Threshold between body and arm valid on both sides.
            float Dx(float y) => Mathf.Max(At(1, y).TorsoMax, At(-1, y).TorsoMax) * .5f + Mathf.Min(At(1, y).ArmMin, At(-1, y).ArmMin) * .5f;

            float armpit = Mathf.Min(arms[1][0].Y, arms[-1][0].Y);
            float handBottom = Mathf.Max(arms[1].Last().Y, arms[-1].Last().Y) - .006f * H;
            float shoulderY = armpit + .05f * H;
            // The A-posed hand hangs at about 45 degrees: its vertical extent is roughly 8% of the height.
            float wristY = handBottom + .08f * H;
            float elbowY = shoulderY + (wristY - shoulderY) * .48f;
            // Extend the forearm/upper-arm centre line up to the shoulder to place the joint under any pauldron.
            float jointDx = new[] { 1, -1 }.Average(sign =>
            {
                Slice[] line = arms[sign].Where(s => s.Y < elbowY + .05f * H && s.Y > wristY).ToArray();
                if (line.Length < 2) line = arms[sign].ToArray();
                float my = line.Average(s => s.Y), md = line.Average(s => (s.ArmCentre.x - cx) * sign);
                float slope = line.Sum(s => (s.Y - my) * ((s.ArmCentre.x - cx) * sign - md)) / Mathf.Max(1e-5f, line.Sum(s => (s.Y - my) * (s.Y - my)));
                return md + slope * (shoulderY - my);
            });
            float torsoHalf = Mathf.Max(At(1, armpit).TorsoMax, At(-1, armpit).TorsoMax);
            jointDx = Mathf.Clamp(jointDx, .06f * H, Mathf.Max(.07f * H, torsoHalf));
            float armRadius = Mathf.Clamp(new[] { 1, -1 }.Average(sign => At(sign, elbowY).ArmMax - At(sign, elbowY).ArmMin) * .38f, .035f * H, .07f * H);

            // Legs: the highest slice with nothing on the centre line but both legs present.
            float crotch = .2f * H;
            for (float y = .6f * H; y > .1f * H; y -= .005f * H)
            {
                bool centre = false; int left = 0, right = 0;
                foreach (Vector3 p in v)
                {
                    if (Mathf.Abs(p.y - y) > .005f * H) continue;
                    float d = p.x - cx;
                    if (Mathf.Abs(d) < .012f * H) { centre = true; break; }
                    if (d > 0 && d < .2f * H) right++; else if (d < 0 && d > -.2f * H) left++;
                }
                if (!centre && left > 10 && right > 10) { crotch = y; break; }
            }
            float hipY = Mathf.Clamp(crotch + .06f * H, .42f * H, .52f * H);
            float LegCentre(int sign, float y) => v.Where(p => Mathf.Abs(p.y - y) < .01f * H && (p.x - cx) * sign > 0 && (p.x - cx) * sign < .2f * H)
                .Select(p => (p.x - cx) * sign).DefaultIfEmpty(.05f * H).Average();
            float hipDx = Mathf.Clamp((LegCentre(1, crotch - .05f * H) + LegCentre(-1, crotch - .05f * H)) * .5f, .035f * H, .09f * H);
            const float ankleY = .065f * H;
            float kneeY = ankleY + .55f * (hipY - ankleY);
            Vector3[] feet = v.Where(p => p.y < .08f * H).ToArray();
            float footZ = feet.Average(p => p.z) - cz;
            float toeZ = v.Where(p => p.y < .03f * H).Max(p => p.z) - cz - .02f;
            float legRadius = Mathf.Clamp(new[] { 1, -1 }.Average(sign =>
            {
                float[] d = v.Where(p => Mathf.Abs(p.y - kneeY) < .01f * H && (p.x - cx) * sign > 0 && (p.x - cx) * sign < .2f * H)
                    .Select(p => (p.x - cx) * sign).DefaultIfEmpty(0f).ToArray();
                return (d.Max() - d.Min()) * .5f;
            }) * 1.15f, .035f * H, .075f * H);

            float belt = hipY + .32f * (shoulderY - hipY);
            float hem = Mathf.Max(crotch, .12f * H);
            float hemHalf = v.Where(p => Mathf.Abs(p.y - (hem + .03f * H)) < .01f * H).Select(p => Mathf.Abs(p.x - cx)).DefaultIfEmpty(.15f * H).Max();
            var spec = new RobedHumanoid.Spec
            {
                HeadProbeY = .93f * H,
                HeadBase = shoulderY + .035f * H, NeckBase = shoulderY + .012f * H,
                ChestBase = hipY + .62f * (shoulderY - hipY), Belt = belt,
                HipY = hipY, KneeY = kneeY, HipDx = hipDx, ToeZ = toeZ,
                PadMinY = shoulderY - .012f * H, PadMaxY = shoulderY + .012f * H, PadDx = jointDx - .02f * H, JointDx = jointDx,
                ElbowMinY = elbowY - .012f * H, ElbowMaxY = elbowY + .012f * H, ElbowDx = Dx(elbowY),
                WristMinY = wristY - .012f * H, WristMaxY = wristY + .012f * H, WristDx = Dx(wristY),
                PalmMinY = handBottom + .02f * H, PalmMaxY = handBottom + .055f * H, PalmDx = Dx(handBottom + .04f * H),
                AnkleMinY = ankleY - .012f * H, AnkleMaxY = ankleY + .012f * H, AnkleDxMin = .005f * H, AnkleDxMax = .3f * H,
                AnkleZMin = footZ - .1f * H, AnkleZMax = footZ + .06f * H,
                ArmRadius = armRadius, LegRadius = legRadius,
                // Tabard and cape are not used by these enemies (their gates exclude every vertex).
                TabardTop = new Vector3(0f, belt, .05f), TabardBottom = new Vector3(0f, hem, .08f), TabardMinZ = 9f, TabardHalfWidth = 0f,
                CapeTop = new Vector3(0f, shoulderY, -.1f), CapeBottom = new Vector3(0f, hem, -.15f), CapeMaxZ = -9f,
                SkirtTopDx = hipDx * 1.6f, SkirtTopY = belt, SkirtBottomDx = Mathf.Max(hipDx * 1.8f, hemHalf * .8f), SkirtBottomY = hem,
                Skirt = robe,
            };
            lines.AppendLine($"Landmarks: head centre x {cx:+0.000;-0.000} z {cz:+0.000;-0.000}; shoulder {shoulderY:0.00} (joint dx {jointDx:0.00}), "
                + $"elbow {elbowY:0.00} (dx>{spec.ElbowDx:0.00}), wrist {wristY:0.00} (dx>{spec.WristDx:0.00}), hand bottom {handBottom:0.00} (palm dx>{spec.PalmDx:0.00}); "
                + $"crotch {crotch:0.00}, hip {hipY:0.00} (dx {hipDx:0.00}), knee {kneeY:0.00}, toe z {toeZ:0.00}; arm r {armRadius:0.000}, leg r {legRadius:0.000}"
                + (robe ? $"; skirt to {hem:0.00} (half width {hemHalf:0.00})" : "") + ".");
            return spec;
        }

        private static bool TrySlice(Vector3[] v, float cx, int sign, float y, float half, out Slice slice)
        {
            slice = default;
            var d = new List<float>();
            var points = new List<Vector3>();
            foreach (Vector3 p in v)
            {
                if (Mathf.Abs(p.y - y) > half) continue;
                float lateral = (p.x - cx) * sign;
                if (lateral <= 0f) continue;
                d.Add(lateral); points.Add(p);
            }
            if (d.Count < 20) return false;
            float[] sorted = d.OrderBy(x => x).ToArray();
            float gap = 0f, at = 0f;
            int split = 0;
            for (int i = 1; i < sorted.Length; i++)
            {
                if (sorted[i] - sorted[i - 1] <= gap) continue;
                gap = sorted[i] - sorted[i - 1]; at = (sorted[i] + sorted[i - 1]) * .5f; split = i;
            }
            if (gap < .012f * H || split < 8 || sorted.Length - split < 8 || sorted[split] < .06f * H) return false;
            Vector3 centre = Vector3.zero;
            int count = 0;
            for (int i = 0; i < d.Count; i++) if (d[i] > at) { centre += points[i]; count++; }
            slice = new Slice { Y = y, TorsoMax = sorted[split - 1], ArmMin = sorted[split], ArmMax = sorted[sorted.Length - 1], ArmCentre = centre / count };
            return true;
        }

        // ---------- Clips ----------

        private static AnimationClip MakeClip(Enemy e, Transform root, string role, float duration, WrapMode wrap)
        {
            Transform hips = rig.Find("Hips");
            Vector3 hipsRest = rig.RestPosition(hips);
            return GeneratedRig.Clip(root, rig.Bones, $"{e.RigFolder}/{e.Id}_{role}.anim", duration, wrap,
                (bone, phase) => Rotation(e, bone.name, role, phase),
                (bone, phase) => bone == hips ? hipsRest + Vector3.up * e.Lift(role, phase) : (Vector3?)null);
        }

        private static Quaternion Rotation(Enemy e, string name, string role, float phase)
        {
            Func<string, float, Vector3> aim = name == "RightHand" ? e.RightAim : name == "LeftHand" ? e.LeftAim : null;
            if (aim == null) return Quaternion.Euler(e.Pose(name, role, phase));
            return RobedHumanoid.Aim(name.Replace("Hand", ""), bone => e.Pose(bone, role, phase), Quaternion.Euler(aim(role, phase)));
        }

        // Attack timing around the contact phase c: wind up to 0.8c, strike lands at c, hold briefly, recover.
        private static float Windup(Enemy e, float phase) => GeneratedRig.Pulse(phase, 0f, e.Contact * .8f, e.Contact * .8f, e.Contact);
        private static float Strike(Enemy e, float phase) => GeneratedRig.Pulse(phase, e.Contact * .8f, e.Contact, e.Contact + .12f, 1f);

        // Arms lowered from the A pose to hang beside the body; breathing in the chest.
        private static Vector3 Rest(string name, float phase)
        {
            Vector3 pose = Vector3.zero;
            if (name == "RightUpperArm") pose = new Vector3(-8, 0, -22);
            if (name == "RightForeArm") pose = new Vector3(-30, 0, 0);
            if (name == "LeftUpperArm") pose = new Vector3(-8, 0, 22);
            if (name == "LeftForeArm") pose = new Vector3(-26, 0, 0);
            if (name == "Chest") pose.x = 1.2f * Mathf.Sin(phase * Mathf.PI * 2f);
            return pose;
        }

        // Walk or run cycle layered on the arm pose; the free left arm swings against the right leg.
        private static Vector3 Locomotion(Enemy e, string name, string role, float phase, Vector3 pose, bool swingLeft)
        {
            if (role != e.Move) return pose;
            float wave = Mathf.Sin(phase * Mathf.PI * 2f), side = GeneratedRig.Side(name), a = e.Stride;
            bool run = role == "Run";
            if (name.EndsWith("Thigh")) pose.x = -side * wave * a;
            if (name.EndsWith("Shin")) pose.x = Mathf.Max(0, side * wave) * a * (run ? 1.8f : 1.35f);
            if (name.EndsWith("Foot")) pose.x = -Mathf.Max(0, side * wave) * a * .55f;
            if (name.EndsWith("Skirt")) pose.x = -side * wave * a * .4f;
            if (name == "Spine") pose.x += run ? 10f : 2f;
            if (name == "Chest") pose = new Vector3(pose.x + 3f, wave * (run ? 6f : 3f), 0f);
            if (swingLeft && name == "LeftUpperArm") pose.x += -wave * (run ? 28f : 14f);
            if (swingLeft && name == "LeftForeArm" && run) pose.x -= 40f;
            return pose;
        }

        // ---------- Roster ----------

        private static IEnumerable<Enemy> Roster(ArtStyleLibrary library)
        {
            // Raider: sword and round shield, guard up; a diagonal slash over the shield.
            yield return Behave(Raider, new Enemy
            {
                Weapons = (right, left) => new List<Weapon> { Sword(), RoundShield() },
                Pose = (n, role, p) =>
                {
                    var e = Raider;
                    Vector3 pose = Rest(n, p);
                    if (n == "RightUpperArm") pose = new Vector3(-20, 0, -26);
                    if (n == "RightForeArm") pose = new Vector3(-50, 0, 0);
                    if (n == "LeftUpperArm") pose = new Vector3(-38, 0, 26);
                    if (n == "LeftForeArm") pose = new Vector3(-58, 0, 0);
                    pose = Locomotion(e, n, role, p, pose, false);
                    if (role != e.Action) return pose;
                    float w = Windup(e, p), s = Strike(e, p);
                    if (n == "RightUpperArm") pose += new Vector3(-110 * w - 55 * s, 0, 20 * w - 6 * s);
                    if (n == "RightForeArm") pose.x += -35 * w + 45 * s;
                    if (n == "LeftUpperArm") pose.x += -10 * s;
                    if (n == "Chest") pose = new Vector3(6 * s, 18 * w - 16 * s, 0);
                    if (n == "Spine") pose.x = 4 * s;
                    if (n == "LeftThigh") pose.x = -14 * s;
                    if (n == "LeftShin") pose.x = 12 * s;
                    return pose;
                },
                RightAim = (role, p) => new Vector3(35f + (role == "Slash" ? -60f * Windup(Raider, p) + 95f * Strike(Raider, p) : 0f), 0f, 0f),
                LeftAim = (role, p) => Vector3.zero,
            });
            // Swarmling: crouched, quick runner with a dagger; a lunging stab.
            yield return Behave(Swarm, new Enemy
            {
                Weapons = (right, left) => new List<Weapon> { Dagger() },
                Pose = (n, role, p) =>
                {
                    var e = Swarm;
                    Vector3 pose = Rest(n, p);
                    if (n == "Spine") pose.x = 8;
                    if (n == "RightUpperArm") pose = new Vector3(-25, 0, -20);
                    if (n == "RightForeArm") pose = new Vector3(-70, 0, 0);
                    if (n == "LeftForeArm") pose = new Vector3(-55, 0, 0);
                    pose = Locomotion(e, n, role, p, pose, true);
                    if (role != e.Action) return pose;
                    float w = Windup(e, p), s = Strike(e, p);
                    if (n == "RightUpperArm") pose += new Vector3(25 * w - 70 * s, 0, 0);
                    if (n == "RightForeArm") pose.x += -30 * w + 60 * s;
                    if (n == "Spine") pose.x += 12 * s;
                    if (n == "Chest") pose = new Vector3(4 * s, -10 * w + 12 * s, 0);
                    if (n == "LeftThigh") pose.x = -20 * s;
                    if (n == "LeftShin") pose.x = 16 * s;
                    return pose;
                },
                RightAim = (role, p) => new Vector3(75f + (role == "Stab" ? 10f * Strike(Swarm, p) : 0f), 0f, 0f),
            });
            // Brute: a heavy club; an overhead smash.
            yield return Behave(Brute, new Enemy
            {
                Weapons = (right, left) => new List<Weapon> { Club() },
                Pose = (n, role, p) =>
                {
                    var e = Brute;
                    Vector3 pose = Rest(n, p);
                    if (n == "RightUpperArm") pose = new Vector3(-12, 0, -16);
                    if (n == "RightForeArm") pose = new Vector3(-35, 0, 0);
                    pose = Locomotion(e, n, role, p, pose, true);
                    if (role != e.Action) return pose;
                    float w = Windup(e, p), s = Strike(e, p);
                    if (n == "RightUpperArm") pose += new Vector3(-150 * w - 40 * s, 0, 10 * w);
                    if (n == "RightForeArm") pose.x += -40 * w + 35 * s;
                    if (n == "LeftUpperArm") pose += new Vector3(-50 * w - 30 * s, 0, -10 * s);
                    if (n == "Chest") pose = new Vector3(-8 * w + 16 * s, 10 * w - 6 * s, 0);
                    if (n == "Spine") pose.x = -5 * w + 8 * s;
                    if (n == "LeftThigh") pose.x = -16 * s;
                    if (n == "LeftShin") pose.x = 14 * s;
                    return pose;
                },
                RightAim = (role, p) => new Vector3(25f + (role == "Smash" ? -80f * Windup(Brute, p) + 110f * Strike(Brute, p) : 0f), 0f, 0f),
            });
            // Shaman: robed caster with a skull staff planted at rest; thrusts the staff to cast.
            yield return Behave(Shaman, new Enemy
            {
                Weapons = (right, left) => new List<Weapon> { SkullStaff() },
                Pose = (n, role, p) =>
                {
                    var e = Shaman;
                    Vector3 pose = Rest(n, p);
                    // Staff hand forward at the waist with the staff planted.
                    if (n == "RightUpperArm") pose = new Vector3(-8, 0, -14);
                    if (n == "RightForeArm") pose = new Vector3(-40, 0, 0);
                    pose = Locomotion(e, n, role, p, pose, true);
                    if (role != e.Action) return pose;
                    float w = Windup(e, p), s = Strike(e, p);
                    if (n == "RightUpperArm") pose += new Vector3(-30 * w - 62 * s, 0, 6 * s);
                    if (n == "RightForeArm") pose.x += -35 * w + 25 * s;
                    if (n == "LeftUpperArm") pose += new Vector3(-20 * w - 62 * s, 0, -14 * s);
                    if (n == "LeftForeArm") pose.x += -30 * w + 12 * s;
                    if (n == "Chest") pose = new Vector3(-4 * w + 6 * s, 12 * w - 8 * s, 0);
                    return pose;
                },
                RightAim = (role, p) => new Vector3(role == "Cast" ? 6f - 20f * Windup(Shaman, p) + 62f * Strike(Shaman, p)
                    : role == "Walk" ? 10f + 4f * Mathf.Sin(p * Mathf.PI * 2f) : 6f, 0f, 0f),
            });
            // Warlord: a corrupted greatsword; a wide diagonal cleave.
            yield return Behave(Boss, new Enemy
            {
                Weapons = (right, left) => new List<Weapon> { Greatsword() },
                Pose = (n, role, p) =>
                {
                    var e = Boss;
                    Vector3 pose = Rest(n, p);
                    if (n == "RightUpperArm") pose = new Vector3(-25, 0, -20);
                    if (n == "RightForeArm") pose = new Vector3(-45, 0, 0);
                    pose = Locomotion(e, n, role, p, pose, true);
                    if (role != e.Action) return pose;
                    float w = Windup(e, p), s = Strike(e, p);
                    if (n == "RightUpperArm") pose += new Vector3(-130 * w - 50 * s, 0, 25 * w - 10 * s);
                    if (n == "RightForeArm") pose.x += -30 * w + 40 * s;
                    if (n == "LeftUpperArm") pose += new Vector3(-40 * w - 40 * s, 0, -10 * s);
                    if (n == "Chest") pose = new Vector3(-6 * w + 12 * s, 25 * w - 20 * s, 0);
                    if (n == "Spine") pose.x = 4 * s;
                    if (n == "LeftThigh") pose.x = -16 * s;
                    if (n == "LeftShin") pose.x = 14 * s;
                    return pose;
                },
                RightAim = (role, p) => new Vector3(40f + (role == "Cleave" ? -70f * Windup(Boss, p) + 80f * Strike(Boss, p) : 0f), 0f, 0f),
            });
        }

        // Size, gait and action of each enemy; Roster adds the weapons and poses, which read these for timing.
        private static readonly Enemy Raider = new Enemy { Id = VisualId.EnemyRaider, Kind = EnemyKind.Raider, Height = 1.7f, Action = "Slash" };
        private static readonly Enemy Swarm = new Enemy { Id = VisualId.EnemySwarm, Kind = EnemyKind.Swarm, Height = 1.2f,
            Move = "Run", MoveLength = .7f, Stride = 32f, Action = "Stab" };
        private static readonly Enemy Brute = new Enemy { Id = VisualId.EnemyBrute, Kind = EnemyKind.Brute, Height = 2.1f,
            Stride = 18f, MoveLength = 1.3f, Action = "Smash" };
        private static readonly Enemy Shaman = new Enemy { Id = VisualId.EnemyShaman, Kind = EnemyKind.Shaman, Height = 1.7f, Robe = true, Action = "Cast" };
        private static readonly Enemy Boss = new Enemy { Id = VisualId.EnemyBoss, Kind = EnemyKind.Boss, Height = 2.3f, Robe = true,
            Stride = 17f, MoveLength = 1.4f, Action = "Cleave" };

        private static Enemy Behave(Enemy target, Enemy behaviour)
        {
            target.Weapons = behaviour.Weapons;
            target.Pose = behaviour.Pose;
            target.RightAim = behaviour.RightAim;
            target.LeftAim = behaviour.LeftAim;
            return target;
        }

        // ---------- Weapons (grip at the origin, along +Y) ----------

        private sealed class Materials
        {
            public readonly Material Iron, DarkIron, Leather, Wood, Bone, Glow;
            private readonly string folder;
            private Material hammeredIron, darkHammered, woodPlank, wrapLeather, purpleLeather, bronze;
            // Materials of the reference-built weapons, made only for the enemies that carry one.
            public Material HammeredIron => hammeredIron ?? (hammeredIron = WeaponMeshes.HammeredIron(folder, "EnemyHammeredIron", new Color(.92f, .92f, .95f), .7f, .42f));
            public Material DarkHammered => darkHammered ?? (darkHammered = WeaponMeshes.HammeredIron(folder, "EnemyDarkHammered", new Color(.62f, .52f, .5f), .6f, .35f));
            public Material WoodPlank => woodPlank ?? (woodPlank = WeaponMeshes.WoodGrain(folder, "EnemyWoodPlank", new Color(1f, .95f, 1f)));
            public Material WrapLeather => wrapLeather ?? (wrapLeather = MakeMaterial(folder, "EnemyWrapLeather", new Color(.3f, .16f, .1f), 0f, .3f));
            public Material PurpleLeather => purpleLeather ?? (purpleLeather = MakeMaterial(folder, "EnemyPurpleLeather", new Color(.22f, .1f, .3f), 0f, .3f));
            public Material Bronze => bronze ?? (bronze = MakeMaterial(folder, "EnemyBronze", new Color(.55f, .4f, .22f), .75f, .45f));
            public Materials(string folder, ArtStyleLibrary library)
            {
                this.folder = folder;
                Iron = MakeMaterial(folder, "EnemyIron", new Color(.36f, .36f, .4f), .8f, .5f);
                DarkIron = MakeMaterial(folder, "EnemyDarkIron", library.corruptArmor, .6f, .4f);
                Leather = MakeMaterial(folder, "EnemyLeather", new Color(.13f, .08f, .07f), 0f, .25f);
                Wood = MakeMaterial(folder, "EnemyWood", new Color(.2f, .13f, .09f), 0f, .3f);
                Bone = MakeMaterial(folder, "EnemyBone", library.bone, 0f, .35f);
                Glow = MakeGlowMaterial(folder, "EnemyCorruptGlow", library.corruptGlow, 2.2f);
            }
        }

        private static Weapon Sword()
        {
            // Built after Docs/ArtReference/Enemies/Raider/Raider_03_Weapon_Sword_v1.png (see WeaponMeshes).
            float tip = WeaponMeshes.Sword("RaiderSword", WeaponMeshes.SwordSpecRaider, out Mesh blade, out Mesh hilt, out Mesh grip);
            var w = new Weapon { Root = new GameObject("Sword").transform, Hand = "RightHand", Strike = new Vector3(0f, tip, 0f) };
            AddMesh(w.Root, "Blade", blade, mats.HammeredIron);
            AddMesh(w.Root, "Hilt", hilt, mats.HammeredIron);
            AddMesh(w.Root, "Grip", grip, mats.WrapLeather);
            return w;
        }

        private static Weapon RoundShield()
        {
            // Built after Raider_03_Weapon_Shield_v1.png; the face sits a little above the palm.
            WeaponMeshes.RoundShield("RaiderShield", out Mesh planks, out Mesh iron);
            var w = new Weapon { Root = new GameObject("Shield").transform, Hand = "LeftHand", Strike = new Vector3(0f, 0f, .08f) };
            AddMesh(w.Root, "Planks", planks, mats.WoodPlank);
            AddMesh(w.Root, "Iron", iron, mats.HammeredIron);
            foreach (Transform part in w.Root) part.localPosition = new Vector3(0f, .02f, 0f);
            Part(w.Root, "Grip", PrimitiveType.Cube, new Vector3(0f, 0f, .02f), new Vector3(.04f, .12f, .03f), mats.Leather);
            return w;
        }

        private static Weapon Dagger()
        {
            // Built after Swarmling_03_Weapon_Dagger_v1.png (see WeaponMeshes).
            float tip = WeaponMeshes.Sword("SwarmDagger", WeaponMeshes.DaggerSpec, out Mesh blade, out Mesh hilt, out Mesh grip);
            var w = new Weapon { Root = new GameObject("Dagger").transform, Hand = "RightHand", Strike = new Vector3(0f, tip - .015f, 0f) };
            AddMesh(w.Root, "Blade", blade, mats.HammeredIron);
            AddMesh(w.Root, "Hilt", hilt, mats.HammeredIron);
            AddMesh(w.Root, "Grip", grip, mats.WrapLeather);
            return w;
        }

        private static Weapon Club()
        {
            // Built after Brute_03_Weapon_Club_v1.png (see WeaponMeshes).
            float strike = WeaponMeshes.SpikedClub("BruteClub", out Mesh wood, out Mesh iron, out Mesh grip);
            var w = new Weapon { Root = new GameObject("Club").transform, Hand = "RightHand", Strike = new Vector3(0f, strike, 0f) };
            w.Root.localScale = Vector3.one * .85f; // 0.95 m: the full 1.12 m smash put the head through the ground
            AddMesh(w.Root, "Haft", wood, mats.WoodPlank);
            AddMesh(w.Root, "Iron", iron, mats.DarkHammered);
            AddMesh(w.Root, "Grip", grip, mats.WrapLeather);
            return w;
        }

        private static Weapon SkullStaff()
        {
            // Tripo model (Enemy Shaman/Weapon/EnemyShaman_Staff.glb), 1.9 m with the planted grip at 47% of its
            // length: the butt rests just above the ground, clear of it while walking.
            const float length = 1.9f, grip = .47f;
            return ImportedWeapon("Staff", "RightHand", "Assets/Art/Characters/EnemyShaman/Weapon/EnemyShaman_Staff.glb",
                length, grip, 0f, new Vector3(0f, (.96f - grip) * length, 0f));
        }

        private static Weapon Greatsword()
        {
            // Tripo model (Enemy Boss/Weapon/EnemyBoss_Greatsword.glb), 1.6 m, gripped at 17.5% of its length. The
            // model's flats face ±Z; a quarter turn puts the edges forward for the cleave.
            const float length = 1.6f, grip = .175f;
            return ImportedWeapon("Greatsword", "RightHand", "Assets/Art/Characters/EnemyBoss/Weapon/EnemyBoss_Greatsword.glb",
                length, grip, 90f, new Vector3(0f, (.95f - grip) * length, 0f));
        }

        // A weapon from a 1 m tall Tripo GLB: scaled to `length`, the grip point (a fraction of the height) moved
        // to the origin, turned by `yaw`, baked to a mesh asset and drawn with the GLB's own materials.
        private static Weapon ImportedWeapon(string name, string hand, string glbPath, float length, float grip, float yaw, Vector3 strike)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(glbPath) == null)
                AssetDatabase.ImportAsset(glbPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(glbPath)
                ?? throw new InvalidOperationException(glbPath + " is missing or glTFast could not import it.");
            Mesh mesh = GeneratedRig.BakePlaced(source, Matrix4x4.identity, weaponScene, Path.GetFileNameWithoutExtension(glbPath));
            Quaternion turn = Quaternion.Euler(0f, yaw, 0f);
            Vector3[] vertices = mesh.vertices, normals = mesh.normals;
            Vector4[] tangents = mesh.tangents;
            for (int i = 0; i < vertices.Length; i++)
                vertices[i] = turn * (vertices[i] * length - Vector3.up * grip * length);
            for (int i = 0; i < normals.Length; i++) normals[i] = turn * normals[i];
            for (int i = 0; i < tangents.Length; i++)
            {
                Vector3 t = turn * new Vector3(tangents[i].x, tangents[i].y, tangents[i].z);
                tangents[i] = new Vector4(t.x, t.y, t.z, tangents[i].w);
            }
            mesh.vertices = vertices;
            if (normals.Length == vertices.Length) mesh.normals = normals;
            if (tangents.Length == vertices.Length) mesh.tangents = tangents;
            mesh.RecalculateBounds();
            mesh = SaveAsset(mesh, $"{weaponFolder}/{mesh.name}.asset");
            var w = new Weapon { Root = new GameObject(name).transform, Hand = hand, Strike = strike };
            var part = new GameObject("Model");
            part.transform.SetParent(w.Root, false);
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            part.AddComponent<MeshRenderer>().sharedMaterials = SourceMaterials(source);
            return w;
        }

        private static void AddMesh(Transform parent, string name, Mesh mesh, Material material)
        {
            string folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(material)).Replace('\\', '/');
            mesh = SaveAsset(mesh, folder + "/" + mesh.name + ".asset");
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        // ---------- Validation ----------

        private static void Validate(Enemy e, GameObject root, SkinnedMeshRenderer skin, List<Weapon> weapons, StringBuilder lines, params AnimationClip[] clips)
        {
            GeneratedRig.CheckWeights(skin);
            Vector3[] rest = GeneratedRig.RestVertices(skin);
            Vector3 forward = root.transform.forward;
            foreach (AnimationClip clip in clips)
            {
                rig.ResetPose();
                float motion = GeneratedRig.Motion(root, skin, clip, .3f, rest);
                if (motion < .001f || motion > 1.5f) throw new InvalidOperationException($"Invalid deformation in {clip.name} ({motion:0.000} m).");
                bool action = clip.name.EndsWith(e.Action);
                float grip = 0f, lowest = float.MaxValue;
                foreach (float phase in new[] { 0f, .25f, .5f, .75f, e.Contact })
                {
                    clip.SampleAnimation(root, clip.length * phase);
                    foreach (Weapon w in weapons)
                    {
                        grip = Mathf.Max(grip, Vector3.Distance(GeneratedRig.Centroid(skin, body.Palm(w.Hand.Replace("Hand", ""))), w.Root.position));
                        foreach (Renderer r in w.Root.GetComponentsInChildren<Renderer>()) lowest = Mathf.Min(lowest, r.bounds.min.y);
                    }
                }
                if (grip > .015f) throw new InvalidOperationException($"Weapon/palm alignment failed in {clip.name} ({grip:0.000} m).");
                if (lowest < -.03f) throw new InvalidOperationException($"A weapon goes through the ground in {clip.name} ({lowest:0.00} m).");
                string line = $"{clip.name}: vertex motion {motion:0.000} m, grip error {grip:0.0000} m, lowest weapon point {lowest:0.00} m";
                if (action)
                {
                    clip.SampleAnimation(root, clip.length * e.Contact);
                    Weapon main = weapons.First(w => w.Hand == "RightHand");
                    Vector3 tip = main.Root.TransformPoint(main.Strike);
                    float reach = Vector3.Dot(tip - root.transform.position, forward);
                    if (reach < .45f || tip.y < .1f || tip.y > 2.4f)
                        throw new InvalidOperationException($"{e.Action} does not land ahead (reach {reach:0.00} m, height {tip.y:0.00} m at contact).");
                    line += $"; at contact {e.Contact:0.00} the {main.Root.name.ToLowerInvariant()} reaches {reach:0.00} m ahead at {tip.y:0.00} m";
                }
                lines.AppendLine(line + ".");
            }
            rig.ResetPose();
        }

        private static int Triangles(Mesh mesh)
        {
            int count = 0;
            for (int i = 0; i < mesh.subMeshCount; i++) count += (int)mesh.GetIndexCount(i) / 3;
            return count;
        }
    }
}

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
            public Func<string, float, Vector3> Hips;                // hips offset from rest (role, phase); null = the bob below
            public float GroundSpeed;                                // move clip ground speed (rig units/s); 0 = from Stride
            public float Lift(string role, float phase) => role == Move
                ? (Move == "Run" ? .03f : .016f) * (1f - Mathf.Cos(phase * Mathf.PI * 4f))
                : role == "Idle" ? .006f * Mathf.Sin(phase * Mathf.PI * 2f) : -.05f * Strike(this, phase);
            public Vector3 HipsOffset(string role, float phase) => Hips != null ? Hips(role, phase) : Vector3.up * Lift(role, phase);
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
            mats = new Materials(e.RigFolder);
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
                rightLeg = new Leg(body.LegLine(1));
                leftLeg = new Leg(body.LegLine(-1));
                if (e == Boss) lines.AppendLine(PrepareWarlordRun());
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
                if (e.Hips != null)
                    foreach (AnimationClip clip in new[] { idle, move, action })
                        lines.AppendLine(FootContact(root, clip, clip == move ? e.GroundSpeed : 0f));

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
                float speed = (e.GroundSpeed > 0f ? e.GroundSpeed : 2f * 2f * spec.HipY * Mathf.Sin(e.Stride * Mathf.Deg2Rad) / e.MoveLength)
                    * scale * TroopVisualScale;
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
                leftLeg = rightLeg = null;
                warlordRun = null;
                cachedRole = null;
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
                (bone, phase) => bone == hips ? hipsRest + e.HipsOffset(role, phase) : (Vector3?)null);
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
            // Warlord: a corrupted greatsword carried low; it runs the hero down and lands a wide diagonal cleave.
            yield return Behave(Boss, new Enemy
            {
                Weapons = (right, left) => new List<Weapon> { Greatsword() },
                Pose = (n, role, p) => WarlordPose(n, Warlord(role, p)),
                RightAim = (role, p) => Warlord(role, p).Aim,
                Hips = (role, p) => Warlord(role, p).Hips,
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
            Move = "Run", Action = "Cleave" }; // run length and ground speed come from the mocap (PrepareWarlordRun)

        private static Enemy Behave(Enemy target, Enemy behaviour)
        {
            target.Weapons = behaviour.Weapons;
            target.Pose = behaviour.Pose;
            target.RightAim = behaviour.RightAim;
            target.LeftAim = behaviour.LeftAim;
            target.Hips = behaviour.Hips;
            target.GroundSpeed = behaviour.GroundSpeed;
            return target;
        }

        // ---------- Warlord motion ----------

        // The Warlord hunts the hero at its catalog speed (3.5 m/s), so it runs instead of walking. The run is
        // retargeted from the RunFast mocap (Higgsfield animation library on a Meshy rig, Animations/Source):
        // ankle paths, sole pitch and hips bob drive the leg IK, the spine, head and free left arm take the mocap
        // rotations, and the greatsword arm keeps its carry pose. A bigger body steps slower (Froude scaling on
        // the gameplay size), the stride is scaled so normal playback covers the catalog speed, and each planted
        // foot is pinned to the ground moving back at that speed. Idle and the cleave plant both feet with the
        // same IK, so the weight shows in the knees instead of a sliding stance.
        private const string WarlordRunMocap = "Assets/Art/Characters/EnemyBoss/Animations/Source/EnemyBoss_Mocap_RunFast.glb";
        private static readonly string[] WarlordMocapBones = { "Spine01", "Spine", "neck", "Head", "LeftShoulder", "LeftArm", "LeftForeArm", "LeftHand" };
        private const int WarlordRunSamples = 60;

        private static Leg leftLeg, rightLeg;
        private static WarlordFrame[] warlordRun;

        private struct WarlordFrame
        {
            public Vector3 Hips;               // hips offset from rest (y lift, z forward)
            public Vector3 LeftFoot, RightFoot; // ankle (forward, raise) from its rest spot on the ground, sole pitch (toe down +)
            public Vector3 Aim;                // greatsword aim in root space
            public float Swing, Breath, Coil, Strike, Follow, Phase;
            public Dictionary<string, Quaternion> Bones; // retargeted local rotations (run)
            public string Role;
        }

        private static string cachedRole;
        private static float cachedPhase = -1f;
        private static WarlordFrame cached;

        // Every bone of a frame reads the same pose; it is computed once per (role, phase).
        private static WarlordFrame Warlord(string role, float p)
        {
            if (role == cachedRole && p == cachedPhase && leftLeg != null) return cached;
            WarlordFrame f = role == "Run" ? WarlordRun(p) : role == "Cleave" ? WarlordCleave(p) : WarlordIdle(p);
            f.Role = role; f.Phase = p;
            cachedRole = role; cachedPhase = p; cached = f;
            return f;
        }

        private static float Ease(float a, float b, float t) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, t));

        private static WarlordFrame WarlordRun(float p)
        {
            float x = Mathf.Repeat(p, 1f) * warlordRun.Length;
            int i = Mathf.FloorToInt(x) % warlordRun.Length;
            WarlordFrame a = warlordRun[i], b = warlordRun[(i + 1) % warlordRun.Length];
            float t = x - Mathf.Floor(x);
            return new WarlordFrame
            {
                Hips = Vector3.Lerp(a.Hips, b.Hips, t),
                LeftFoot = Vector3.Lerp(a.LeftFoot, b.LeftFoot, t),
                RightFoot = Vector3.Lerp(a.RightFoot, b.RightFoot, t),
                Aim = Vector3.Lerp(a.Aim, b.Aim, t),
                Swing = Mathf.Lerp(a.Swing, b.Swing, t),
                Bones = a.Bones.ToDictionary(pair => pair.Key, pair => Quaternion.Slerp(pair.Value, b.Bones[pair.Key], t)),
            };
        }

        // Samples the mocap cycle into run frames on this rig and sets the Warlord's run length and ground speed.
        private static string PrepareWarlordRun()
        {
            MocapGait g = MocapGait.Load(WarlordRunMocap, WarlordMocapBones, WarlordRunSamples);
            int n = g.Count;
            float ratio = .5f * (leftLeg.RestDown + rightLeg.RestDown) / g.LegLength; // mocap metres -> rig units
            float world = Boss.Height / H * TroopVisualScale;                          // rig units -> battle metres
            float slow = Mathf.Sqrt(ratio * world);
            float duration = g.Period * slow;
            float want = EnemyCatalog.Speed(EnemyKind.Boss) / world;                   // rig units per second
            float stride = Mathf.Min(1f, want * duration / (g.GroundSpeed * g.Period * ratio));
            float ground = g.GroundSpeed * g.Period * ratio * stride / duration;
            // The mocap is a sprint; at the Warlord's pace the feet lift less and the body leans less.
            float calm = Mathf.Sqrt(stride);
            float dt = duration / n;

            Vector3[] Feet(MocapGait.Foot foot, Leg leg)
            {
                var mocap = new Vector3[n];
                for (int i = 0; i < n; i++) mocap[i] = new Vector3(ratio * stride * foot.Forward[i], ratio * calm * foot.Rise[i], calm * foot.Pitch[i]);
                var path = (Vector3[])mocap.Clone();
                List<int[]> runs = MocapGait.Runs(foot.Planted);
                // Planted: the flat-foot spot moves back at the ground speed; the heel rises about the toe.
                foreach (int[] run in runs)
                {
                    float spot = mocap[run[0] % n].x;
                    foreach (int k in run)
                    {
                        Vector3 m = mocap[k % n];
                        Vector2 pivot = leg.ToePivot(Mathf.Max(0f, m.z));
                        path[k % n] = new Vector3(spot - ground * (k - run[0]) * dt + pivot.x, pivot.y, m.z);
                    }
                }
                // In the air: the mocap path, eased from where the last contact let go into the next landing.
                for (int r = 0; r < runs.Count; r++)
                {
                    int[] run = runs[r], next = runs[(r + 1) % runs.Count];
                    int from = run[run.Length - 1], to = next[0] + (r + 1 == runs.Count ? n : 0);
                    if (to <= from) to += n;
                    Vector3 leave = path[from % n] - mocap[from % n], land = path[to % n] - mocap[to % n];
                    for (int k = from + 1; k < to; k++)
                    {
                        float u = (k - from) / (float)(to - from);
                        Vector3 m = mocap[k % n] + Vector3.Lerp(leave, land, Mathf.SmoothStep(0f, 1f, u));
                        // Heel and toe clear the ground within a few samples of the toe-off and until the landing.
                        float clear = .04f * Mathf.Min(1f, Mathf.Min(k - from, to - k) / 3f);
                        m.y = Mathf.Max(m.y, Mathf.Max(0f, leg.ToePivot(m.z).y) + clear);
                        path[k % n] = m;
                    }
                }
                return path;
            }
            Vector3[] left = Feet(g.Left, leftLeg), right = Feet(g.Right, rightLeg);
            float reach = Enumerable.Range(0, n).Max(i => Mathf.Abs(right[i].x - left[i].x));

            // The mocap hips sit high for this rig's legs: drop them where a foot would be out of reach (knee
            // still a little bent), widened and smoothed so the body sinks into those moments instead of jerking.
            Vector3[] hips = Enumerable.Range(0, n).Select(i => new Vector3(0f, ratio * g.HipsRise[i], ratio * stride * g.HipsSway[i])).ToArray();
            float[] need = Enumerable.Range(0, n).Select(i => Mathf.Max(leftLeg.Overreach(hips[i], left[i], .975f), rightLeg.Overreach(hips[i], right[i], .975f))).ToArray();
            float[] Spread(float[] v, Func<IEnumerable<float>, float> pick) =>
                Enumerable.Range(0, n).Select(i => pick(Enumerable.Range(-3, 7).Select(o => v[(i + o + n) % n]))).ToArray();
            float[] drop = Spread(Spread(need, w => w.Max()), w => w.Average());
            for (int i = 0; i < n; i++) hips[i].y -= Mathf.Max(drop[i], need[i]);

            // Spine, head and the free arm: the mocap world rotations from the bind, as local rotations on this
            // rig (whose bones are at identity in the bind). The arm follows the mocap bone directions.
            Transform Bone(string name) => rig.Find(name);
            Vector3 upperArm = (Bone("LeftForeArm").position - Bone("LeftUpperArm").position).normalized;
            Vector3 foreArm = (Bone("LeftHand").position - Bone("LeftForeArm").position).normalized;
            Quaternion Aligned(Quaternion delta, Vector3 rest, Vector3 target) => Quaternion.FromToRotation(delta * rest, target) * delta;

            warlordRun = new WarlordFrame[n];
            for (int i = 0; i < n; i++)
            {
                Quaternion D(string b) => g.Delta[b][i];
                Vector3 J(string b) => g.Joint[b][i];
                Quaternion C(string b) => Quaternion.Slerp(Quaternion.identity, D(b), calm);
                Quaternion spine = C("Spine01"), chest = C("Spine"), neck = C("neck"), head = C("Head");
                Quaternion shoulder = chest * Quaternion.Inverse(D("Spine")) * D("LeftShoulder");
                Quaternion upper = Aligned(D("LeftArm"), upperArm, J("LeftForeArm") - J("LeftArm"));
                Quaternion fore = Aligned(D("LeftForeArm"), foreArm, J("LeftHand") - J("LeftForeArm"));
                float swing = (right[i].x - left[i].x) / reach; // +1: right leg forward
                warlordRun[i] = new WarlordFrame
                {
                    Hips = hips[i],
                    LeftFoot = left[i], RightFoot = right[i],
                    Aim = new Vector3(122f + 4f * swing, 0f, -34f),
                    Swing = swing,
                    Bones = new Dictionary<string, Quaternion>
                    {
                        ["Spine"] = spine,
                        ["Chest"] = Quaternion.Inverse(spine) * chest,
                        ["Neck"] = Quaternion.Inverse(chest) * neck,
                        ["Head"] = Quaternion.Inverse(neck) * head,
                        ["LeftShoulder"] = Quaternion.Inverse(chest) * shoulder,
                        ["LeftUpperArm"] = Quaternion.Inverse(shoulder) * upper,
                        ["LeftForeArm"] = Quaternion.Inverse(upper) * fore,
                        ["LeftHand"] = Quaternion.Inverse(D("LeftForeArm")) * D("LeftHand"),
                    },
                };
            }
            Boss.MoveLength = duration;
            Boss.GroundSpeed = ground;
            return $"Run mocap: {Path.GetFileName(WarlordRunMocap)}, cycle {g.Period:0.000} s x{slow:0.00} -> {duration:0.000} s, "
                + $"mocap ground speed {g.GroundSpeed:0.00} m/s, leg ratio {ratio:0.00}, stride x{stride:0.00}, lift and lean x{calm:0.00}, "
                + $"planted {g.Left.Planted.Count(b => b)}/{n} left, {g.Right.Planted.Count(b => b)}/{n} right samples, "
                + $"hips {ratio * g.HipsRise.Min():0.000}..{ratio * g.HipsRise.Max():0.000} -> {hips.Min(h => h.y):0.000}..{hips.Max(h => h.y):0.000} (rig units).";
        }

        private static WarlordFrame WarlordIdle(float p)
        {
            float breath = Mathf.Sin(p * Mathf.PI * 2f);
            return new WarlordFrame
            {
                // Staggered stance, knees soft, sinking a little with each breath.
                Hips = new Vector3(0f, -.03f - .006f * breath, 0f),
                LeftFoot = new Vector3(.07f, 0f, 0f),
                RightFoot = new Vector3(-.05f, 0f, 0f),
                Aim = new Vector3(126f + 3f * breath, 0f, -22f),
                Breath = breath,
            };
        }

        // Fast raise, a held coil while the telegraph builds, a quick heavy drop onto the contact phase, a carry
        // through and a slow recovery. Specials stretch the same clip over their longer wind-up.
        private static WarlordFrame WarlordCleave(float p)
        {
            float c = Boss.Contact;
            float raise = 1f - Mathf.Pow(1f - Mathf.InverseLerp(0f, .4f * c, p), 3f);
            float creep = Mathf.InverseLerp(.4f * c, .88f * c, p);
            float hit = Mathf.Pow(Mathf.InverseLerp(.88f * c, c, p), 2f);
            float recover = Ease(c + .2f, 1f, p);
            float coil = raise * (1f + .1f * creep) * (1f - hit);
            float strike = hit * (1f - recover);
            float follow = p > c ? Mathf.Sin(Mathf.PI * Mathf.InverseLerp(c, c + .22f, p)) : 0f;
            // The lead foot draws back with the raise, stamps forward just before the blow lands and steps back
            // during the recovery; it only travels while lifted.
            float stamp = Ease(.8f * c, .97f * c, p);
            float pull = Ease(0f, .4f * c, p) * (1f - stamp);
            float plant = stamp * (1f - recover);
            float lift = .03f * Mathf.Sin(Mathf.PI * Mathf.InverseLerp(0f, .4f * c, p))
                + .05f * Mathf.Sin(Mathf.PI * Mathf.InverseLerp(.8f * c, c, p))
                + (p > c + .2f ? .035f * Mathf.Sin(Mathf.PI * Mathf.InverseLerp(c + .2f, 1f, p)) : 0f);
            return new WarlordFrame
            {
                Hips = new Vector3(0f, -.03f - .02f * coil - .055f * strike, -.04f * coil + .1f * strike),
                LeftFoot = new Vector3(.07f - .04f * pull + .2f * plant, lift, -200f * lift),
                RightFoot = new Vector3(-.05f, 0f, 0f),
                Aim = new Vector3(126f - 161f * coil - 8f * strike + 6f * follow, 0f, -22f - 18f * coil + 57f * strike + 5f * follow),
                Coil = coil, Strike = strike, Follow = follow,
            };
        }

        private static Vector3 WarlordPose(string n, WarlordFrame f)
        {
            if (n.EndsWith("Thigh") || n.EndsWith("Shin") || n.EndsWith("Foot") || n.EndsWith("Skirt"))
            {
                bool right = n.StartsWith("Right");
                Vector3 foot = right ? f.RightFoot : f.LeftFoot;
                Vector3 leg = (right ? rightLeg : leftLeg).Solve(f.Hips, foot.x, foot.y, foot.z);
                // The armoured skirt follows half the thigh swing.
                float x = n.EndsWith("Thigh") ? leg.x : n.EndsWith("Shin") ? leg.y : n.EndsWith("Foot") ? leg.z : .5f * Mathf.Min(0f, leg.x) + .25f * Mathf.Max(0f, leg.x);
                return new Vector3(x, 0f, 0f);
            }
            float sw = f.Swing, b = f.Breath, k = f.Coil, s = f.Strike, fo = f.Follow;
            switch (f.Role)
            {
                case "Run":
                    if (f.Bones.TryGetValue(n, out Quaternion mocap)) return mocap.eulerAngles;
                    switch (n)
                    {
                        case "RightUpperArm": return new Vector3(-18f + 10f * sw, 0f, -28f);
                        case "RightForeArm": return new Vector3(-36f, 0f, 0f);
                    }
                    return Vector3.zero;
                case "Cleave":
                    float spineY = 8f + 18f * k - 28f * s - 8f * fo, chestY = 14f * k - 18f * s - 6f * fo;
                    switch (n)
                    {
                        case "Spine": return new Vector3(6f - 10f * k + 22f * s + 4f * fo, spineY, 0f);
                        case "Chest": return new Vector3(2f - 6f * k + 12f * s, chestY, 0f);
                        // The head stays on the target through the twist.
                        case "Neck": return new Vector3(3f + 6f * k - 10f * s, -.5f * (spineY - 8f + chestY), 0f);
                        case "Head": return new Vector3(0f, -.2f * (spineY - 8f + chestY), 0f);
                        case "LeftUpperArm": return new Vector3(-10f - 55f * k + 35f * s, 0f, 20f + 10f * k + 15f * s);
                        case "LeftForeArm": return new Vector3(-32f - 30f * k + 15f * s, 0f, 0f);
                        case "RightUpperArm": return new Vector3(-20f - 125f * k - 25f * s, 0f, -22f + 28f * k - 6f * s);
                        case "RightForeArm": return new Vector3(-38f - 35f * k + 30f * s, 0f, 0f);
                    }
                    return Vector3.zero;
                default:
                    switch (n)
                    {
                        // Bladed towards the lead foot, chest heaving, the head slowly scanning.
                        case "Spine": return new Vector3(6f + .6f * b, 8f, 0f);
                        case "Chest": return new Vector3(2f + 2.2f * b, 0f, 0f);
                        case "Neck": return new Vector3(3f - b, -5f, 0f);
                        case "Head": return new Vector3(0f, -2f + 4f * Mathf.Sin(f.Phase * Mathf.PI * 2f + 1.3f), 0f);
                        case "LeftShoulder": return new Vector3(0f, 0f, -1.5f * b);
                        case "RightShoulder": return new Vector3(0f, 0f, 1.5f * b);
                        case "LeftUpperArm": return new Vector3(-10f - 2f * b, 0f, 20f);
                        case "LeftForeArm": return new Vector3(-32f - 4f * b, 0f, 0f);
                        case "RightUpperArm": return new Vector3(-20f, 0f, -22f);
                        case "RightForeArm": return new Vector3(-38f - 2f * b, 0f, 0f);
                    }
                    return Vector3.zero;
            }
        }

        // Two-bone leg in the side plane, measured from the rest skeleton (bones at identity rotation). Solve
        // places the ankle relative to its rest spot and returns the Euler x of thigh, shin and foot (+x swings a
        // bone backwards and tips the toe down), keeping the knee in front of the hip-ankle line.
        private sealed class Leg
        {
            private readonly float upper, lower, upperRest, lowerRest, restForward;
            private readonly Vector2 toe;                             // toe from the ankle (forward, up)
            public readonly float RestDown;
            public float Reach => upper + lower;

            public Leg(Vector3[] line)
            {
                // (forward, down) from a to b
                Vector2 Side(Vector3 a, Vector3 b) => new Vector2(b.z - a.z, a.y - b.y);
                Vector2 thigh = Side(line[0], line[1]), shin = Side(line[1], line[2]), whole = Side(line[0], line[2]);
                upper = thigh.magnitude; lower = shin.magnitude;
                upperRest = Mathf.Atan2(thigh.x, thigh.y); lowerRest = Mathf.Atan2(shin.x, shin.y);
                restForward = whole.x; RestDown = whole.y;
                toe = new Vector2(line[3].z - line[2].z, line[3].y - line[2].y);
            }

            // Ankle offset (forward, raise) that keeps the toe where it was when the sole tips `pitch` degrees toe down.
            // How far the hips must drop for the ankle (as in Solve) to sit within `slack` of the full reach.
            public float Overreach(Vector3 hips, Vector3 foot, float slack)
            {
                float f = restForward + foot.x - hips.z, d = RestDown + hips.y - foot.y, l = slack * Reach;
                return Mathf.Max(0f, f * f >= l * l ? d : d - Mathf.Sqrt(l * l - f * f));
            }

            public Vector2 ToePivot(float pitch)
            {
                float a = pitch * Mathf.Deg2Rad;
                var turned = new Vector2(toe.y * Mathf.Sin(a) + toe.x * Mathf.Cos(a), toe.y * Mathf.Cos(a) - toe.x * Mathf.Sin(a));
                return toe - turned;
            }

            public Vector3 Solve(Vector3 hips, float forward, float raise, float pitch)
            {
                float f = restForward + forward - hips.z, d = RestDown + hips.y - raise;
                float l = Mathf.Clamp(Mathf.Sqrt(f * f + d * d), Mathf.Abs(upper - lower) + .001f, (upper + lower) * .9995f);
                float line = Mathf.Atan2(f, d);
                float atHip = Mathf.Acos(Mathf.Clamp((upper * upper + l * l - lower * lower) / (2f * upper * l), -1f, 1f));
                float atAnkle = Mathf.Acos(Mathf.Clamp((lower * lower + l * l - upper * upper) / (2f * lower * l), -1f, 1f));
                float thighX = (upperRest - (line + atHip)) * Mathf.Rad2Deg;
                float shinX = (lowerRest - (line - atAnkle)) * Mathf.Rad2Deg - thighX;
                return new Vector3(thighX, shinX, pitch - thighX - shinX);
            }
        }

        // Foot contact of a generated clip: how far a sole sinks below the ground and how far a planted heel or toe
        // slides along the ground (the ground moving back at `groundSpeed` under a running clip).
        private static string FootContact(GameObject root, AnimationClip clip, float groundSpeed)
        {
            const float contact = .006f;
            const int samples = 120;
            float sink = 0f, slide = 0f; string worst = "";
            var points = new List<(Transform foot, Vector3 local, float rest)>();
            foreach (int sign in new[] { 1, -1 })
            {
                Vector3[] line = body.LegLine(sign);
                Transform foot = rig.Find((sign == 1 ? "Right" : "Left") + "Foot");
                points.Add((foot, Vector3.zero, line[2].y));
                points.Add((foot, line[3] - line[2], line[3].y));
            }
            var start = new float?[points.Count];
            for (int i = 0; i <= samples; i++)
            {
                float time = clip.length * i / samples;
                clip.SampleAnimation(root, time);
                for (int j = 0; j < points.Count; j++)
                {
                    Vector3 at = points[j].foot.TransformPoint(points[j].local);
                    float height = at.y - points[j].rest;
                    sink = Mathf.Max(sink, -height);
                    float ground = at.z + groundSpeed * time;
                    if (height > contact) { start[j] = null; continue; }
                    if (start[j] == null) start[j] = ground;
                    float drift = Mathf.Abs(ground - start[j].Value);
                    if (drift > slide) { slide = drift; worst = $" ({(j < 2 ? "right" : "left")} {(j % 2 == 0 ? "ankle" : "toe")} at phase {(float)i / samples:0.00}, height {height:0.000})"; }
                }
            }
            rig.ResetPose();
            if (sink > .02f || slide > .03f)
                throw new InvalidOperationException($"{clip.name}: feet do not stay planted (sink {sink:0.000} m, slide {slide:0.000} m{worst}).");
            return $"{clip.name} feet: deepest sink {sink:0.000} m, planted slide {slide:0.000} m"
                + (groundSpeed > 0f ? $" at {groundSpeed:0.00} m/s ground speed (rig units)." : ".");
        }

        // ---------- Weapons (grip at the origin, along +Y) ----------

        private sealed class Materials
        {
            private readonly string folder;
            private Material hammeredIron, darkHammered, woodPlank, wrapLeather, leather;
            // Materials of the reference-built weapons, made only for the enemies that carry one.
            public Material HammeredIron => hammeredIron ?? (hammeredIron = WeaponMeshes.HammeredIron(folder, "EnemyHammeredIron", new Color(.92f, .92f, .95f), .7f, .42f));
            public Material DarkHammered => darkHammered ?? (darkHammered = WeaponMeshes.HammeredIron(folder, "EnemyDarkHammered", new Color(.62f, .52f, .5f), .6f, .35f));
            public Material WoodPlank => woodPlank ?? (woodPlank = WeaponMeshes.WoodGrain(folder, "EnemyWoodPlank", new Color(1f, .95f, 1f)));
            public Material WrapLeather => wrapLeather ?? (wrapLeather = MakeMaterial(folder, "EnemyWrapLeather", new Color(.3f, .16f, .1f), 0f, .3f));
            public Material Leather => leather ?? (leather = MakeMaterial(folder, "EnemyLeather", new Color(.13f, .08f, .07f), 0f, .25f));
            public Materials(string folder) => this.folder = folder;
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

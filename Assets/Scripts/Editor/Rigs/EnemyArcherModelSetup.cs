using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Lightbringer.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static Lightbringer.EditorTools.RigBuildUtility;

namespace Lightbringer.EditorTools
{
    // Enemy Archer from a Tripo GLB rigged by Higgsfield (Meshy auto-rig). Each Higgsfield call returns the
    // rigged mesh with one mocap clip, so Animations/Source holds EnemyArcher_Anim_Idle.glb and _Walk.glb with
    // the same skeleton. This builds a crowd-budget prefab from the idle GLB, assigns the override with its
    // idle/move clips, puts a quiver on the back and a primitive bow in the left hand. Source GLBs are untouched.
    // The shoot clip is generated here: it animates only the spine, head and arms (computed from the Meshy bind
    // pose, whose bone rotations are not identity), so the mocap idle/walk keeps the legs. BowString on the bow
    // makes the string and nocked arrow follow the drawing hand.
    [InitializeOnLoad]
    public static class EnemyArcherModelSetup
    {
        private const string Folder = "Assets/Art/Characters/EnemyArcher";
        private const string RigFolder = Folder + "/Rigged";
        private const string SourceGlb = Folder + "/Animations/Source/EnemyArcher_Anim_Idle.glb";
        private const string PrefabPath = RigFolder + "/EnemyArcher_Rigged.prefab";
        private const string BowPath = RigFolder + "/EnemyBow.prefab";
        private const string Request = "Docs/Validation/EnemyArcherModel.request";
        private const string Report = "Docs/Validation/EnemyArcherModelChecks.txt";
        private const float Height = 1.7f;
        private const int CrowdBudget = 12000;
        // Mesh LOD generation stops near 20-30k on these scans; allied troops land at 23-29k (Docs/Diagnostics/CrowdMeshes.txt).
        private const int MaxCrowdTriangles = 30000;
        // Meshy Casual_Walk covers about 1.25 m/s on a 1.7 m figure (model space).
        private const float WalkSpeed = 1.25f;
        // UnitAppearance scales every troop visual by this on top of the override scale.
        private const float TroopVisualScale = 1.2f;
        private const float BowLength = 0.95f;
        // Grip just below the arrow rest, so the string's rest point sits a few centimetres above the fist.
        private const float BowGrip = 0.47f;
        // Longest string pull in bow units (the bow is about one unit tall).
        private const float MaxDraw = 0.75f;
        private const float ShootLength = 1f;
        // Bones the shoot clip animates. Hips and legs are left out so the idle/walk mocap keeps the stance.
        private static readonly string[] ShootBones = { "Spine02", "Spine01", "Spine", "neck", "Head",
            "LeftShoulder", "LeftArm", "LeftForeArm", "LeftHand", "RightShoulder", "RightArm", "RightForeArm", "RightHand" };
        // Side-on stance: the spine turns the bow shoulder to the target, neck and head turn back to it.
        private static readonly float[] SpineYaw = { 15f, 15f, 20f };
        private const float NeckYaw = -20f, HeadYaw = -22f;

        static EnemyArcherModelSetup() => EditorRequests.Register(Request, Build);

        [MenuItem("Lightbringer/Art/Build Enemy Archer Model")]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder(RigFolder)) AssetDatabase.CreateFolder(Folder, "Rigged");
            ArtStyleLibrary library = AssetDatabase.LoadAssetAtPath<ArtStyleLibrary>(ArtStyleSetup.LibraryPath);
            if (library == null) throw new InvalidOperationException("Art style library is missing.");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(SourceGlb) == null)
                AssetDatabase.ImportAsset(SourceGlb, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SourceGlb);
            if (source == null) throw new InvalidOperationException(SourceGlb + " is missing or glTFast could not import it.");

            var report = new StringBuilder("Enemy Archer model setup | " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "\n");
            Scene scene = EditorSceneManager.NewPreviewScene();
            try
            {
                GameObject root = UnityEngine.Object.Instantiate(source);
                root.name = "EnemyArcher_Rigged";
                SceneManager.MoveGameObjectToScene(root, scene);
                root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                foreach (Animation legacy in root.GetComponentsInChildren<Animation>(true)) UnityEngine.Object.DestroyImmediate(legacy);

                SkinnedMeshRenderer skin = BuildCrowdSkin(root.transform, report);
                SavePrefab(root);

                // Fit height, feet and facing from the body alone, before props widen the bounds.
                if (!ArtModelImport.Assign(VisualId.EnemyArcher, PrefabPath, Height))
                    throw new InvalidOperationException("Could not assign the Enemy Archer override.");
                VisualOverride entry = library.FindOverride(VisualId.EnemyArcher);
                Vector3 forward = Quaternion.Euler(0f, -entry.localEuler.y, 0f) * Vector3.forward;

                BuildQuiver(root.transform, skin, forward);
                SavePrefab(root);

                int clips = ArtModelImport.AssignAnimations(VisualId.EnemyArcher, Folder + "/Animations");
                entry = library.FindOverride(VisualId.EnemyArcher);
                // Meshy hips travel is in centimetres under the 0.01 armature, so set the clip speed directly.
                entry.moveClipSpeed = WalkSpeed * entry.scale * TroopVisualScale;
                EditorUtility.SetDirty(library);

                BuildBow(forward, library, scene);
                if (!ArtModelImport.AttachProp(library, entry, BowPath, "LeftHand", BowLength, BowGrip))
                    throw new InvalidOperationException("Could not attach the bow to LeftHand.");
                // The bow stays upright facing the target; the shoot clip aims the arm, not the bow.
                PropAttachment bowProp = entry.attachments.First(a => a != null && a.boneName == "LeftHand");
                bowProp.follow = 0f;
                bowProp.castLean = 0f;

                entry.actionClip = BuildShootClip(root.transform, entry, forward, report, out bool shotPass);
                EditorUtility.SetDirty(library);
                AssetDatabase.SaveAssets();

                bool pass = Validate(entry, clips, report) && shotPass;
                Directory.CreateDirectory(Path.GetDirectoryName(Report));
                File.WriteAllText(Report, (pass ? "PASS" : "FAIL") + " | " + report);
                if (!pass) throw new InvalidOperationException("Enemy Archer validation failed. See " + Report);
                Debug.Log("Enemy Archer model assigned: Higgsfield rig, crowd mesh, idle/walk/shoot, bow and quiver. Restart Play to use it.");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [MenuItem("Lightbringer/Art/Build Enemy Archer Model", true)]
        private static bool CanBuild() => !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling;

        // Replaces the 240k-triangle GLB mesh with a crowd-budget copy (weights and UVs unchanged) and gives the
        // renderer fixed bounds, so a wave of archers does not recompute skinned bounds every frame.
        private static SkinnedMeshRenderer BuildCrowdSkin(Transform root, StringBuilder report)
        {
            SkinnedMeshRenderer[] skins = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (skins.Length != 1) throw new InvalidOperationException($"Expected one skinned mesh, found {skins.Length}.");
            SkinnedMeshRenderer skin = skins[0];
            Mesh original = skin.sharedMesh;
            Mesh crowd = CrowdMeshOptimizer.Simplify(original, skin.sharedMaterials, CrowdBudget, out Material[] merged);
            crowd.name = "EnemyArcher_Skinned_Crowd";
            crowd = SaveAsset(crowd, RigFolder + "/EnemyArcher_Skinned_Crowd.asset");
            skin.sharedMesh = crowd;
            skin.sharedMaterials = merged;
            skin.quality = SkinQuality.Bone4;
            skin.updateWhenOffscreen = false;
            Transform hips = UnitAppearance.FindDeep(root, "Hips");
            if (hips != null) skin.rootBone = hips;
            Transform anchor = skin.rootBone != null ? skin.rootBone : skin.transform;
            float unit = Mathf.Max(anchor.lossyScale.x, 1e-4f);
            skin.localBounds = new Bounds(anchor.InverseTransformPoint(new Vector3(0f, Height * 0.5f, 0f)), Vector3.one * (Height * 1.4f / unit));
            report.AppendLine($"Mesh: triangles {Triangles(original):N0} -> {Triangles(crowd):N0}, vertices {original.vertexCount:N0} -> {crowd.vertexCount:N0}, "
                + $"bones {skin.bones.Length}, bind poses {crowd.bindposes.Length}, weights {crowd.boneWeights.Length:N0}, distance LODs {crowd.lodCount}");
            return skin;
        }

        // Greybox quiver on the upper back, parented to the top spine bone so it follows the torso.
        private static void BuildQuiver(Transform root, SkinnedMeshRenderer skin, Vector3 forward)
        {
            Transform chest = UnitAppearance.FindDeep(root, "Spine") ?? throw new InvalidOperationException("Bone Spine not found.");
            Material leather = MakeMaterial(RigFolder, "EnemyQuiverLeather", new Color(.15f, .09f, .1f), 0f, .25f);
            Material trim = MakeMaterial(RigFolder, "EnemyQuiverTrim", new Color(.8f, .75f, .64f), 0f, .35f);
            Material shaft = MakeMaterial(RigFolder, "EnemyArrowShaft", new Color(.3f, .22f, .18f), 0f, .3f);
            Material steel = MakeMaterial(RigFolder, "EnemyArrowHead", new Color(.36f, .36f, .4f), .7f, .45f);
            Material fletch = MakeMaterial(RigFolder, "EnemyArrowFletch", new Color(.55f, .08f, .14f), 0f, .2f);

            // Back surface behind the chest bone, measured on the bind-pose mesh (vertices are model-space metres).
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            float back = 0f;
            foreach (Vector3 vertex in skin.sharedMesh.vertices)
            {
                Vector3 offset = vertex - chest.position;
                if (Mathf.Abs(offset.y) > .08f || Mathf.Abs(Vector3.Dot(offset, right)) > .12f) continue;
                back = Mathf.Min(back, Vector3.Dot(offset, forward));
            }

            var quiver = new GameObject("Quiver").transform;
            quiver.SetParent(chest, false);
            quiver.position = chest.position + forward * (back - .05f) + right * .06f + Vector3.down * .04f;
            quiver.rotation = Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(-10f, 0f, -20f);
            quiver.localScale = Vector3.one / Mathf.Max(chest.lossyScale.x, 1e-4f);
            Part(quiver, "Case", PrimitiveType.Cylinder, Vector3.zero, new Vector3(.09f, .22f, .09f), leather);
            Part(quiver, "Rim", PrimitiveType.Cylinder, new Vector3(0, .22f, 0), new Vector3(.1f, .012f, .1f), trim);
            for (int i = 0; i < 3; i++)
            {
                var spare = new GameObject("QuiverArrow" + i).transform;
                spare.SetParent(quiver, false);
                spare.localPosition = new Vector3((i - 1) * .022f, .3f - (i % 2) * .02f, (i % 2) * .015f);
                spare.localRotation = Quaternion.Euler(90f, 0f, 0f); // Head down into the case.
                ArcherRigSetup.BuildArrow(spare, shaft, steel, fletch);
            }
        }

        // Upright bow prop (limbs along +Y, back towards the archer's forward). ArtModelImport.AttachProp grips it
        // in the left hand and PropStabilizer keeps it upright over the empty-hand mocap clips.
        private static void BuildBow(Vector3 forward, ArtStyleLibrary library, Scene scene)
        {
            Material wood = MakeMaterial(RigFolder, "EnemyBowWood", new Color(.16f, .11f, .09f), 0f, .3f);
            Material bone = MakeMaterial(RigFolder, "EnemyBowBone", library.bone, 0f, .35f);
            Material cord = MakeMaterial(RigFolder, "EnemyBowString", new Color(.5f, .46f, .42f), 0f, .2f);
            float half = ArcherRigSetup.BowHalf, brace = ArcherRigSetup.Brace;

            var bow = new GameObject("EnemyBow");
            SceneManager.MoveGameObjectToScene(bow, scene);
            var body = new GameObject("Body").transform;
            body.SetParent(bow.transform, false);
            body.localRotation = Quaternion.LookRotation(forward, Vector3.up);
            var limbs = new GameObject("Limbs").transform;
            limbs.SetParent(body, false);
            Mesh limbMesh = ArcherRigSetup.LimbMesh();
            limbMesh.name = "EnemyBowLimbs";
            limbs.gameObject.AddComponent<MeshFilter>().sharedMesh = SaveAsset(limbMesh, RigFolder + "/EnemyBowLimbs.asset");
            limbs.gameObject.AddComponent<MeshRenderer>().sharedMaterial = wood;
            Part(limbs, "TipTop", PrimitiveType.Sphere, new Vector3(0, half, -brace), Vector3.one * .03f, bone);
            Part(limbs, "TipBottom", PrimitiveType.Sphere, new Vector3(0, -half, -brace), Vector3.one * .03f, bone);
            Part(body, "Grip", PrimitiveType.Cylinder, Vector3.zero, new Vector3(.06f, .07f, .05f), bone);
            Transform upper = Part(body, "StringUpper", PrimitiveType.Cylinder, Vector3.zero, Vector3.one, cord);
            Transform lower = Part(body, "StringLower", PrimitiveType.Cylinder, Vector3.zero, Vector3.one, cord);
            var arrow = new GameObject("NockedArrow").transform;
            arrow.SetParent(body, false);
            ArcherRigSetup.BuildArrow(arrow,
                MakeMaterial(RigFolder, "EnemyArrowShaft", new Color(.3f, .22f, .18f), 0f, .3f),
                MakeMaterial(RigFolder, "EnemyArrowHead", new Color(.36f, .36f, .4f), .7f, .45f),
                MakeMaterial(RigFolder, "EnemyArrowFletch", new Color(.55f, .08f, .14f), 0f, .2f));
            bow.AddComponent<BowString>().Configure(body, limbs, upper, lower, arrow, half, brace, ArcherRigSetup.ArrowLength, MaxDraw);
            // Hidden and collapsed so it does not widen the bow's measured bounds (grip and fit).
            arrow.localPosition = new Vector3(0f, 0f, -brace);
            arrow.localScale = Vector3.zero;
            if (PrefabUtility.SaveAsPrefabAsset(bow, BowPath) == null) throw new InvalidOperationException("Could not save the bow prefab.");
            UnityEngine.Object.DestroyImmediate(bow);
        }

        // Release at phase 0 (strike phase 0: the arrow is loosed on UnitCombat.Attacked), follow-through, nock with
        // the bow drawn in, push-pull draw to the jaw, hold. Same timing as the allied Archer_Shoot clip.
        // Meshy bind rotations are not identity, so every change is a world-space rotation applied on top of the
        // bind pose (bone.rotation = delta * bone.rotation), never an absolute local rotation.
        private static AnimationClip BuildShootClip(Transform root, VisualOverride entry, Vector3 forward, StringBuilder report, out bool pass)
        {
            Dictionary<string, Transform> bone = ShootBones.ToDictionary(n => n,
                n => UnitAppearance.FindDeep(root, n) ?? throw new InvalidOperationException("Bone " + n + " not found."));
            Dictionary<string, Quaternion> bind = ShootBones.ToDictionary(n => n, n => bone[n].localRotation);
            Vector3 up = Vector3.up, right = Vector3.Cross(up, forward);
            Vector3 nockOffset = MeasureNockOffset(entry, out float bowUnit);
            // PropStabilizer pins the bow grip 7 cm past the left wrist (ArtModelImport.AttachProp); BowString reads the
            // drawing palm at 30% of the forearm past the right wrist.
            float leftPalm = 0.07f / Mathf.Max(entry.scale, 1e-4f);
            float rightPalm = 0.3f * Vector3.Distance(bone["RightForeArm"].position, bone["RightHand"].position);

            void Reset() { foreach (string n in ShootBones) bone[n].localRotation = bind[n]; }
            Reset();
            PoseTorso(bone, up, right, 0f);
            Vector3 anchor = bone["Head"].position + forward * .12f + right * .04f - up * .01f;
            // Arrow line: the string's rest point at jaw height, straight at the target. The bow arm reaches 97%.
            Vector3 line = anchor - up * nockOffset.y;
            Vector3 shoulder = bone["LeftArm"].position;
            float reach = .97f * (Vector3.Distance(shoulder, bone["LeftForeArm"].position)
                + Vector3.Distance(bone["LeftForeArm"].position, bone["LeftHand"].position) + leftPalm);
            Vector3 offset = line - shoulder;
            float along = Vector3.Dot(offset, forward);
            float discriminant = along * along - offset.sqrMagnitude + reach * reach;
            if (discriminant <= 0f) throw new InvalidOperationException("Enemy Archer bow arm cannot reach the shooting line.");
            Vector3 gripFar = line + forward * (-along + Mathf.Sqrt(discriminant));
            // While nocking the bow comes in until the string hand can reach the string.
            Vector3 drawShoulder = bone["RightArm"].position;
            float drawReach = .95f * (Vector3.Distance(drawShoulder, bone["RightForeArm"].position)
                + Vector3.Distance(bone["RightForeArm"].position, bone["RightHand"].position) + rightPalm);
            Vector3 gripNear = gripFar - forward * .22f - up * .05f;
            for (int i = 0; i < 30 && Vector3.Distance(gripNear + nockOffset, drawShoulder) > drawReach; i++) gripNear -= forward * .01f;
            Quaternion chestYaw = Quaternion.AngleAxis(SpineYaw.Sum(), up);
            Vector3 bowElbow = chestYaw * (-.3f * right - up);
            Vector3 drawElbow = chestYaw * (right + .3f * up - .6f * forward);
            Vector3 recoil = anchor + right * .06f + up * .01f - forward * .08f;

            var clip = new AnimationClip { name = "EnemyArcher_Shoot", legacy = true, frameRate = 30, wrapMode = WrapMode.Once };
            var curves = new Dictionary<string, AnimationCurve>();
            int samples = Mathf.RoundToInt(ShootLength * 30f);
            float bowError = 0f, drawError = 0f, catchDistance = float.MaxValue, holdError = 0f;
            for (int i = 0; i <= samples; i++)
            {
                float phase = (float)i / samples;
                Reset();
                PoseTorso(bone, up, right, phase < .2f ? Mathf.Sin(phase / .2f * Mathf.PI) : 0f);
                float near = phase < .12f ? 0f : phase < .4f ? Smooth(.12f, .4f, phase) : phase < .82f ? 1f - Smooth(.4f, .82f, phase) : 0f;
                Vector3 grip = Vector3.Lerp(gripFar, gripNear, near);
                bowError = Mathf.Max(bowError, Reach(bone["LeftArm"], bone["LeftForeArm"], bone["LeftHand"], grip, leftPalm, bowElbow));

                Vector3 rest = grip + nockOffset;
                Vector3 target;
                if (phase < .12f) target = Vector3.Lerp(anchor, recoil, Smooth(0f, .12f, phase));
                else if (phase < .4f)
                {
                    float t = Smooth(.12f, .4f, phase);
                    target = Vector3.Lerp(recoil, rest, t) + Mathf.Sin(t * Mathf.PI) * (right * .04f - up * .03f + forward * .03f);
                }
                else target = Vector3.Lerp(rest, anchor, Smooth(.4f, .82f, phase));
                drawError = Mathf.Max(drawError, Reach(bone["RightArm"], bone["RightForeArm"], bone["RightHand"], target, rightPalm, drawElbow));

                Vector3 palm = Palm(bone["RightForeArm"], bone["RightHand"], rightPalm);
                if (phase > .3f && phase < .5f) catchDistance = Mathf.Min(catchDistance, Vector3.Distance(palm, rest) / bowUnit);
                if (phase >= .82f) holdError = Mathf.Max(holdError, Vector3.Distance(palm, anchor));
                foreach (string n in ShootBones)
                {
                    string path = AnimationUtility.CalculateTransformPath(bone[n], root);
                    Quaternion q = bone[n].localRotation;
                    for (int k = 0; k < 4; k++) Key(curves, path + "|m_LocalRotation." + "xyzw"[k], phase * ShootLength, q[k]);
                }
            }
            Reset();
            foreach (KeyValuePair<string, AnimationCurve> pair in curves)
            {
                string[] binding = pair.Key.Split('|');
                clip.SetCurve(binding[0], typeof(Transform), binding[1], pair.Value);
            }
            clip.EnsureQuaternionContinuity();
            clip = SaveAsset(clip, Folder + "/Animations/EnemyArcher_Shoot.anim");

            float drawLength = Vector3.Dot(gripFar + nockOffset - anchor, forward) / bowUnit;
            var failures = new List<string>();
            if (bowError > .02f) failures.Add($"bow hand misses its grip by {bowError:0.000} m");
            if (drawError > .02f) failures.Add($"string hand misses its path by {drawError:0.000} m");
            if (catchDistance > BowString.CatchRadius * .5f) failures.Add($"string hand passes {catchDistance:0.000} bow units from the string (catch {BowString.CatchRadius})");
            if (holdError > .02f) failures.Add($"full draw is {holdError:0.000} m off the anchor");
            if (drawLength < .3f || drawLength > MaxDraw) failures.Add($"draw length {drawLength:0.00} bow units outside 0.30-{MaxDraw}");
            if (Vector3.Dot(gripFar - bone["Spine"].position, forward) < .25f) failures.Add("bow is not held out in front of the chest");
            report.AppendLine($"Shoot: {curves.Count / 4} bones (no hips/legs), bow hand error {bowError:0.000} m, string hand error {drawError:0.000} m, "
                + $"string catch {catchDistance:0.000} bow units, full-draw error {holdError:0.000} m, draw {drawLength:0.00} bow units, "
                + $"bow {Vector3.Dot(gripFar - bone["Spine"].position, forward):0.00} m in front of the chest");
            foreach (string failure in failures) report.AppendLine("FAIL: " + failure);
            pass = failures.Count == 0;
            return clip;
        }

        private static void PoseTorso(Dictionary<string, Transform> bone, Vector3 up, Vector3 right, float flinch)
        {
            string[] spine = { "Spine02", "Spine01", "Spine" };
            for (int i = 0; i < spine.Length; i++) bone[spine[i]].rotation = Quaternion.AngleAxis(SpineYaw[i], up) * bone[spine[i]].rotation;
            float yaw = SpineYaw.Sum();
            // The chest flinches back on the release.
            Transform chest = bone["Spine"];
            chest.rotation = Quaternion.AngleAxis(-2f * flinch, Quaternion.AngleAxis(yaw, up) * right) * chest.rotation;
            bone["neck"].rotation = Quaternion.AngleAxis(NeckYaw, up) * bone["neck"].rotation;
            Transform head = bone["Head"];
            head.rotation = Quaternion.AngleAxis(HeadYaw, up) * head.rotation;
            head.rotation = Quaternion.AngleAxis(3f, Quaternion.AngleAxis(yaw + NeckYaw + HeadYaw, up) * right) * head.rotation;
        }

        // Two-bone arm: bends the elbow towards `pole` so the palm (`palm` past the wrist along the forearm) meets
        // `target`. Returns the remaining palm error (non-zero only when the target is out of reach).
        private static float Reach(Transform upper, Transform fore, Transform hand, Vector3 target, float palm, Vector3 pole)
        {
            Vector3 shoulder = upper.position;
            float a = Vector3.Distance(shoulder, fore.position), b = Vector3.Distance(fore.position, hand.position) + palm;
            Vector3 toTarget = target - shoulder, direction = toTarget.normalized;
            float d = Mathf.Clamp(toTarget.magnitude, Mathf.Abs(a - b) + .001f, a + b - .001f);
            float cos = Mathf.Clamp((a * a + d * d - b * b) / (2f * a * d), -1f, 1f);
            Vector3 elbow = shoulder + direction * (a * cos) + Vector3.ProjectOnPlane(pole, direction).normalized * (a * Mathf.Sqrt(1f - cos * cos));
            upper.rotation = Quaternion.FromToRotation(fore.position - shoulder, elbow - shoulder) * upper.rotation;
            fore.rotation = Quaternion.FromToRotation(hand.position - fore.position, target - fore.position) * fore.rotation;
            return Vector3.Distance(Palm(fore, hand, palm), target);
        }

        private static Vector3 Palm(Transform fore, Transform hand, float palm)
            => hand.position + (hand.position - fore.position).normalized * palm;

        // Model-space offset from the bow grip (where PropStabilizer pins the bow to the palm) to the string's rest
        // point, with the bow upright and facing forward as the stabilizer holds it (follow 0, no lean).
        // bowUnit: model metres per bow unit (AttachProp's prop scale).
        private static Vector3 MeasureNockOffset(VisualOverride entry, out float bowUnit)
        {
            GameObject bow = PrefabUtility.LoadPrefabContents(BowPath);
            try
            {
                bow.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                bow.transform.localScale = Vector3.one;
                // Same box as PropStabilizer.MeasureGrip.
                Bounds bounds = default;
                bool found = false;
                foreach (MeshFilter filter in bow.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.sharedMesh == null) continue;
                    Bounds mesh = filter.sharedMesh.bounds;
                    for (int i = 0; i < 8; i++)
                    {
                        Vector3 corner = filter.transform.TransformPoint(mesh.center + Vector3.Scale(mesh.extents,
                            new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                        if (!found) { bounds = new Bounds(corner, Vector3.zero); found = true; }
                        else bounds.Encapsulate(corner);
                    }
                }
                bowUnit = BowLength / Mathf.Max(entry.scale, 1e-4f) / bounds.size.y;
                Vector3 grip = new Vector3(bounds.center.x, bounds.min.y + bounds.size.y * BowGrip, bounds.center.z);
                Vector3 nock = bow.transform.Find("Body").TransformPoint(new Vector3(0f, 0f, -ArcherRigSetup.Brace));
                return (nock - grip) * bowUnit;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(bow);
            }
        }

        private static float Smooth(float from, float to, float value) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, value));

        private static void Key(Dictionary<string, AnimationCurve> curves, string key, float time, float value)
        {
            if (!curves.TryGetValue(key, out AnimationCurve curve)) curves[key] = curve = new AnimationCurve();
            curve.AddKey(time, value);
        }

        private static void SavePrefab(GameObject root)
        {
            if (PrefabUtility.SaveAsPrefabAsset(root, PrefabPath) == null)
                throw new InvalidOperationException("Could not save " + PrefabPath);
        }

        private static bool Validate(VisualOverride entry, int clips, StringBuilder report)
        {
            var failures = new List<string>();
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            SkinnedMeshRenderer skin = prefab != null ? prefab.GetComponentInChildren<SkinnedMeshRenderer>(true) : null;
            if (skin == null || skin.sharedMesh == null) failures.Add("prefab has no skinned mesh");
            else
            {
                Mesh mesh = skin.sharedMesh;
                if (mesh.boneWeights.Length != mesh.vertexCount) failures.Add("bone weights do not cover every vertex");
                if (mesh.bindposes.Length != skin.bones.Length) failures.Add("bind pose count differs from bone count");
                if (Triangles(mesh) > MaxCrowdTriangles) failures.Add($"crowd mesh over budget ({Triangles(mesh):N0})");
            }
            if (entry.prefab != prefab) failures.Add("override does not point at the rigged prefab");
            foreach ((string role, AnimationClip clip) in new[] { ("idle", entry.idleClip), ("move", entry.moveClip), ("shoot", entry.actionClip) })
            {
                if (clip == null) failures.Add(role + " clip missing");
                else
                {
                    if (!clip.legacy) failures.Add(role + " clip is not legacy");
                    report.AppendLine($"Clip {role}: {clip.name}, {clip.length:0.00}s, {AnimationUtility.GetCurveBindings(clip).Length} curves");
                }
            }
            PropAttachment bow = entry.attachments?.FirstOrDefault(a => a != null && a.boneName == "LeftHand");
            if (bow == null || bow.prefab == null) failures.Add("bow not attached to LeftHand");
            Transform quiver = prefab != null ? prefab.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Quiver") : null;
            if (quiver == null) failures.Add("quiver missing");
            report.AppendLine($"Override: scale {entry.scale:0.###}, yaw {entry.localEuler.y:0}, offset {entry.localOffset}, move clip speed {entry.moveClipSpeed:0.##} m/s, clips assigned {clips}");
            report.AppendLine("Props: bow on LeftHand (upright, " + BowLength + " m before troop scale, BowString), quiver on Spine.");
            foreach (string failure in failures) report.AppendLine("FAIL: " + failure);
            return failures.Count == 0;
        }
    }
}

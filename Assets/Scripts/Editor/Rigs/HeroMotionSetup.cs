using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lightbringer.Player;
using Lightbringer.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Lightbringer.EditorTools
{
    // Authors the female hero's skill motions as legacy clips on her GLB skeleton: Thrust (staff driven forward),
    // Slam (staff raised overhead, then brought down) and Raise (staff lifted high, free hand opened, like the Priest's
    // blessing). Poses are solved in model space (two-bone IK for the arms, aim for the staff hand), so nothing depends
    // on the GLB's bone axis conventions. Clips key only the upper body and fade to/from the idle pose, so they layer over
    // idle and run like the Cast clip. The existing Cast clip is left untouched. Request file: Docs/Validation/HeroMotions.request.
    [InitializeOnLoad]
    internal static class HeroMotionSetup
    {
        private const string Folder = ArtModelImport.FemaleHeroFolder + "/Animations";
        private const float FrameRate = 30f;
        // Slam: the staff ends tipped this far from upright toward forward (90 = horizontal), head 35 degrees below it.
        private const float StrikePitch = 125f;
        // Thrust: the staff points at the target from the first frame and only the hands travel (pull back, drive forward).
        private const float ThrustPitch = 80f;
        // Distance from the grip to the staff's head: 62% of the 2 m staff (AttachProp grips it 38% up from the butt).
        private const float StaffHeadLength = 1.24f;
        private static readonly string[] Spine = { "Spine01", "Spine" };
        private static readonly string[] RightArm = { "RightShoulder", "RightArm", "RightForeArm", "RightHand" };
        private static readonly string[] LeftArm = { "LeftShoulder", "LeftArm", "LeftForeArm", "LeftHand" };

        // Hand targets are offsets from the shoulder in units of arm length: x forward, y up, z toward the arm's own side
        // (for the left arm, negative z is across the body).
        private struct Frame
        {
            public float Twist, Bend, Pitch;
            public Vector3 Right;
            // 0..1: how far the right hand's height is replaced by the height that puts the staff butt on the ground.
            public float Plant;
            // The left hand grips the staff shaft above the right hand (two-handed blows).
            public bool LeftOnStaff;
        }

        // What a motion needs to know about the idle pose and the staff: idle hand offsets and the hand height (in arm
        // lengths above the shoulder at rest) at which the staff butt touches the ground.
        private struct Context
        {
            public Vector3 RestRight, RestLeft;
            public float PlantHandY;    // relative to the idle shoulder, in arm lengths
            public float PlantWorldY;   // model-space height of the right hand when the staff head touches the ground
        }

        private sealed class Motion
        {
            public string Name;
            public float Duration, Contact, FadeOutFrom;
            public bool UsesLeftArm;
            public Func<float, Context, Frame> Evaluate;
            public Action<string, float, float, float> Check;      // name, contact reach, hand up, staff forward dot
        }

        private static readonly Motion[] Motions =
        {
            new Motion
            {
                Name = "Thrust", Duration = .9f, Contact = .3f, FadeOutFrom = .76f,
                // Pull back, drive forward, hold a beat, then draw the hand straight back along the same line to its idle place
                // while the torso turns back to square. The retreat is posed explicitly: blending the finished pose into idle
                // would sweep the arm through an arbitrary path.
                Evaluate = (t, c) =>
                {
                    float windup = Ph(t, 0f, .15f), thrust = Ph(t, .15f, .3f), retreat = Ph(t, .42f, .7f), straighten = Ph(t, .56f, .76f);
                    return new Frame
                    {
                        Twist = (Mathf.Lerp(0f, -22f, windup) + Mathf.Lerp(0f, 36f, thrust)) * (1f - retreat),
                        Pitch = Mathf.Lerp(ThrustPitch, 0f, straighten),
                        Right = Vector3.Lerp(Vector3.Lerp(Vector3.Lerp(c.RestRight, new Vector3(.22f, -.5f, .1f), windup),
                            new Vector3(.97f, -.03f, .02f), thrust), c.RestRight, retreat),
                    };
                },
                Check = (n, reach, up, dot) =>
                {
                    if (reach < .6f || dot <= .5f) throw new InvalidOperationException($"{n}: staff is not driven forward (reach {reach:F2}, aim {dot:F2}).");
                },
            },
            new Motion
            {
                Name = "Slam", Duration = 1.1f, Contact = .46f, FadeOutFrom = .72f, UsesLeftArm = true,
                // A two-handed overhead blow with the staff's head: both hands lift the staff straight up (it is not turned
                // while rising), hold a beat, then one accelerating swing brings the head over and down so it strikes the
                // ground in front of the hero (StrikePitch). It rests there for a moment before the clip fades out.
                Evaluate = (t, c) =>
                {
                    float lift = Ph(t, 0f, .3f), swing = Ph(t, .36f, .46f);
                    swing *= swing;   // accelerates into the blow
                    return new Frame
                    {
                        Bend = -5f * lift * (1f - swing) + 24f * swing,
                        Pitch = StrikePitch * swing,
                        Right = Vector3.Lerp(Vector3.Lerp(c.RestRight, new Vector3(.1f, .9f, .08f), lift), new Vector3(.5f, c.PlantHandY, .06f), swing),
                        Plant = swing,
                        LeftOnStaff = true,
                    };
                },
                Check = (n, reach, up, dot) =>
                {
                    if (reach < .25f || up > 0f || dot < .6f)
                        throw new InvalidOperationException($"{n}: the staff head does not come down in front (reach {reach:F2}, hand {up:F2}, aim {dot:F2}).");
                },
            },
            new Motion
            {
                Name = "Raise", Duration = 1f, Contact = .35f, FadeOutFrom = .9f,
                // Only the staff arm moves: the right hand rises straight up with the staff held upright, stays a beat at the
                // top, and comes straight back down. The torso, left arm and legs are not touched, and the staff never tilts.
                Evaluate = (t, c) =>
                {
                    float rise = Ph(t, 0f, .35f), fall = Ph(t, .65f, 1f);
                    float height = rise * (1f - fall);
                    return new Frame
                    {
                        Pitch = 0f,
                        Right = Vector3.Lerp(c.RestRight, new Vector3(.08f, .95f, .1f), height),
                    };
                },
                Check = (n, reach, up, dot) =>
                {
                    if (up < .5f || dot > .5f) throw new InvalidOperationException($"{n}: staff is not held high and upright (hand {up:F2}, aim {dot:F2}).");
                },
            },
        };

        static HeroMotionSetup() => EditorRequests.Register("Docs/Validation/HeroMotions.request", Build);

        [MenuItem("Lightbringer/Art/Build Hero Skill Motions")]
        private static void Build()
        {
            ArtStyleLibrary library = AssetDatabase.LoadAssetAtPath<ArtStyleLibrary>(ArtStyleSetup.LibraryPath);
            VisualOverride hero = library != null ? library.FindOverride(VisualId.Hero) : null;
            if (hero == null || hero.prefab == null || hero.idleClip == null)
            {
                Debug.LogError("Hero motions: assign the female hero model and its animations first.");
                return;
            }

            Scene scene = EditorSceneManager.NewPreviewScene();
            GameObject root = Object.Instantiate(hero.prefab);
            SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                root.transform.localScale = Vector3.one;
                Dictionary<string, Transform> bones = root.GetComponentsInChildren<Transform>(true)
                    .GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
                foreach (string name in Spine.Concat(RightArm).Concat(LeftArm).Concat(new[] { "Hips", "neck", "Head" }))
                    if (!bones.ContainsKey(name)) throw new InvalidOperationException("Hero bone missing: " + name);

                var report = new List<string> { $"PASS | {DateTime.UtcNow:O}" };
                // The bind pose, before anything is sampled: every clip starts from it (BuildClip), because the direction in
                // which the staff sits in the hand is read from it and a pose left over from the previous clip would skew it.
                Dictionary<Transform, Quaternion> bind = bones.Values.ToDictionary(t => t, t => t.localRotation);
                foreach (Motion motion in Motions) report.Add(BuildClip(hero, root, bones, bind, motion));
                AssetDatabase.SaveAssets();
                report.Add(AssignSkillMotions(library, hero));
                File.WriteAllText("Docs/Validation/HeroMotionChecks.txt", string.Join("\n", report) + "\n");
                Debug.Log("Hero skill motions built.\n" + string.Join("\n", report));
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        // Wires the clips to the skills: Light Beam and Ice thrust, Shockwave slams, Heal raises the staff, Meteor keeps
        // the existing Cast (crouch) clip. The Light Beam clip is sped up so its contact meets the 0.5 s cooldown.
        private static string AssignSkillMotions(ArtStyleLibrary library, VisualOverride hero)
        {
            AnimationClip Load(string name) => AssetDatabase.LoadAssetAtPath<AnimationClip>($"{Folder}/FemaleHero_Anim_{name}.anim");
            AnimationClip thrust = Load("Thrust"), slam = Load("Slam"), raise = Load("Raise"), cast = hero.actionClip;
            if (thrust == null || slam == null || raise == null || cast == null)
                throw new InvalidOperationException("Skill motions need the Thrust, Slam and Raise clips and the hero's Cast action clip.");
            SkillMotion Of(SkillKind skill, AnimationClip clip, bool authored, float speed) =>
                new SkillMotion { skill = skill, clip = clip, authored = authored, speed = speed };
            hero.skillMotions = new[]
            {
                Of(SkillKind.LightBeam, thrust, true, 1.5f),
                Of(SkillKind.Heal, raise, true, 1f),
                Of(SkillKind.Ice, thrust, true, 1f),
                Of(SkillKind.Shockwave, slam, true, 1f),
                Of(SkillKind.Meteor, cast, false, Mathf.Max(1f, cast.length / 1.2f)),
            };
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            return "Skill motions: Light Beam and Ice = Thrust, Heal = Raise, Shockwave = Slam, Meteor = Cast.";
        }

        private static string BuildClip(VisualOverride hero, GameObject root, Dictionary<string, Transform> bones,
            Dictionary<Transform, Quaternion> bind, Motion motion)
        {
            foreach (KeyValuePair<Transform, Quaternion> pose in bind) pose.Key.localRotation = pose.Value;
            Vector3 up = Vector3.up;
            Vector3 forward = Quaternion.Inverse(Quaternion.Euler(hero.localEuler)) * Vector3.forward;
            Vector3 pitchAxis = Vector3.Cross(up, forward);   // positive rotation about it tips `up` toward `forward`
            Vector3 right = pitchAxis;
            Transform arm = bones["RightArm"], fore = bones["RightForeArm"], hand = bones["RightHand"];
            Transform lArm = bones["LeftArm"], lFore = bones["LeftForeArm"], lHand = bones["LeftHand"];
            if (Vector3.Dot(arm.position - bones["Hips"].position, right) < 0f) right = -right;
            // The staff is attached upright against the bind pose, so its axis in the hand's frame is world up now.
            Vector3 staffInHand = Quaternion.Inverse(hand.rotation) * up;
            float upperLength = Vector3.Distance(arm.position, fore.position), foreLength = Vector3.Distance(fore.position, hand.position);
            float lUpper = Vector3.Distance(lArm.position, lFore.position), lLower = Vector3.Distance(lFore.position, lHand.position);
            float reach = upperLength + foreLength;

            string[] keyed = Spine.Concat(new[] { "neck", "Head" }).Concat(RightArm).Concat(motion.UsesLeftArm ? LeftArm : new string[0]).ToArray();
            int frames = Mathf.RoundToInt(motion.Duration * FrameRate) + 1;
            var times = new float[frames];
            var rotations = keyed.ToDictionary(n => n, n => new Quaternion[frames]);

            hero.idleClip.SampleAnimation(root, 0f);
            Vector3 restRight = Relative(hand.position - arm.position, forward, up, right, reach);
            Vector3 restLeft = Relative(lHand.position - lArm.position, forward, up, -right, reach);
            // Height of the right hand when the staff head, tipped StrikePitch from upright, touches the ground.
            float scale = Mathf.Max(hero.scale, 1e-4f);
            float groundY = Mathf.Min(bones["LeftToeBase"].position.y, bones["RightToeBase"].position.y) - .03f / scale;
            float headDrop = StaffHeadLength * Mathf.Sin((StrikePitch - 90f) * Mathf.Deg2Rad);
            var context = new Context { RestRight = restRight, RestLeft = restLeft, PlantWorldY = groundY + headDrop / scale };
            context.PlantHandY = (context.PlantWorldY - arm.position.y) / reach;
            float contactReach = 0f, contactUp = 0f, contactAim = 0f;
            for (int f = 0; f < frames; f++)
            {
                float t = Mathf.Min(f / FrameRate, motion.Duration);
                times[f] = t;
                hero.idleClip.SampleAnimation(root, 0f);
                var idlePose = keyed.ToDictionary(n => n, n => bones[n].localRotation);

                Frame frame = motion.Evaluate(t, context);
                float envelope = Ph(t, 0f, .1f) * (1f - Ph(t, motion.FadeOutFrom, motion.Duration));
                // Turn and bend the torso about the vertical and the side axis, split across the two upper spine bones.
                foreach (string name in Spine)
                    bones[name].rotation = Quaternion.AngleAxis(frame.Bend * .5f, pitchAxis)
                        * Quaternion.AngleAxis(frame.Twist * .5f, up) * bones[name].rotation;

                Vector3 shoulder = arm.position;
                Vector3 handTarget = shoulder + World(frame.Right, forward, up, right) * reach;
                if (frame.Plant > 0f) handTarget.y = Mathf.Lerp(handTarget.y, context.PlantWorldY, frame.Plant);
                SolveArm(arm, fore, hand, handTarget, upperLength, foreLength, -up * .8f + right * .6f - forward * .3f);

                // Staff direction: `up` tipped toward `forward` by Pitch degrees.
                Vector3 staffDir = Quaternion.AngleAxis(frame.Pitch, pitchAxis) * up;
                hand.rotation = Quaternion.FromToRotation(hand.rotation * staffInHand, staffDir) * hand.rotation;

                // Two-handed blows: the left hand grips the shaft a hand's width above the right hand.
                if (frame.LeftOnStaff)
                    SolveArm(lArm, lFore, lHand, hand.position + staffDir * (.34f * reach), lUpper, lLower, -up * .8f - right * .6f - forward * .3f);

                foreach (string name in keyed)
                    rotations[name][f] = Quaternion.Slerp(idlePose[name], bones[name].localRotation, envelope);
                if (f > 0 && times[f - 1] < motion.Contact && t >= motion.Contact - 1e-4f)
                {
                    contactReach = Vector3.Dot(hand.position - arm.position, forward) / reach;
                    contactUp = Vector3.Dot(hand.position - arm.position, up) / reach;
                    contactAim = Vector3.Dot((hand.rotation * staffInHand).normalized, forward);
                    motion.Check(motion.Name, contactReach, contactUp, contactAim);
                }
            }

            var clip = new AnimationClip { name = "FemaleHero_Anim_" + motion.Name, legacy = true, frameRate = FrameRate, wrapMode = WrapMode.Once };
            foreach (string name in keyed)
            {
                string path = AnimationUtility.CalculateTransformPath(bones[name], root.transform);
                Quaternion[] q = rotations[name];
                for (int f = 1; f < frames; f++)
                    if (Quaternion.Dot(q[f - 1], q[f]) < 0f) q[f] = new Quaternion(-q[f].x, -q[f].y, -q[f].z, -q[f].w);
                for (int c = 0; c < 4; c++)
                {
                    var keys = new Keyframe[frames];
                    for (int f = 0; f < frames; f++) keys[f] = new Keyframe(times[f], q[f][c]);
                    var curve = new AnimationCurve(keys);
                    for (int k = 0; k < frames; k++)
                    {
                        AnimationUtility.SetKeyLeftTangentMode(curve, k, AnimationUtility.TangentMode.Linear);
                        AnimationUtility.SetKeyRightTangentMode(curve, k, AnimationUtility.TangentMode.Linear);
                    }
                    clip.SetCurve(path, typeof(Transform), "m_LocalRotation." + "xyzw"[c], curve);
                }
            }

            string clipPath = $"{Folder}/FemaleHero_Anim_{motion.Name}.anim";
            AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (existing != null) { EditorUtility.CopySerialized(clip, existing); Object.DestroyImmediate(clip); clip = existing; }
            else AssetDatabase.CreateAsset(clip, clipPath);
            EditorUtility.SetDirty(clip);
            return $"{motion.Name}: {motion.Duration:F2}s, contact {motion.Contact:F2}s; hand {contactReach:F2} arm lengths forward and "
                + $"{contactUp:F2} up of the shoulder, staff aim {contactAim:F2} forward; keyed {keyed.Length} bones.";
        }

        private static float Ph(float t, float from, float to) => Smooth((t - from) / (to - from));
        private static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
        private static Vector3 World(Vector3 rel, Vector3 forward, Vector3 up, Vector3 side) => forward * rel.x + up * rel.y + side * rel.z;
        private static Vector3 Relative(Vector3 world, Vector3 forward, Vector3 up, Vector3 side, float reach) =>
            new Vector3(Vector3.Dot(world, forward), Vector3.Dot(world, up), Vector3.Dot(world, side)) / reach;

        // Two-bone IK in world space: the elbow lands on the circle of valid positions closest to `pole`.
        private static void SolveArm(Transform arm, Transform fore, Transform hand, Vector3 target, float upper, float lower, Vector3 pole)
        {
            Vector3 shoulder = arm.position;
            Vector3 toTarget = target - shoulder;
            float distance = Mathf.Clamp(toTarget.magnitude, Mathf.Abs(upper - lower) + 1e-3f, upper + lower - 1e-3f);
            Vector3 direction = toTarget.normalized;
            float along = (upper * upper - lower * lower + distance * distance) / (2f * distance);
            float height = Mathf.Sqrt(Mathf.Max(upper * upper - along * along, 0f));
            Vector3 bend = Vector3.ProjectOnPlane(pole, direction).normalized;
            Vector3 elbow = shoulder + direction * along + bend * height;
            Vector3 wrist = shoulder + direction * distance;

            arm.rotation = Quaternion.FromToRotation(fore.position - arm.position, elbow - shoulder) * arm.rotation;
            fore.rotation = Quaternion.FromToRotation(hand.position - fore.position, wrist - elbow) * fore.rotation;
        }
    }
}

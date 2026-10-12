using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    internal static class HeroMotionSetup
    {
        private const string Folder = ArtModelImport.FemaleHeroFolder + "/Animations";
        private const float FrameRate = 30f;
        private static readonly string[] Spine = { "Spine01", "Spine" };
        private static readonly string[] RightArm = { "RightShoulder", "RightArm", "RightForeArm", "RightHand" };
        private static readonly string[] LeftArm = { "LeftShoulder", "LeftArm", "LeftForeArm", "LeftHand" };

        // Hand targets are offsets from the shoulder in units of arm length: x forward, y up, z toward the arm's own side
        // (for the left arm, negative z is across the body).
        private struct Frame
        {
            public float Twist, Bend, Pitch;
            public Vector3 Right;
            public Vector3? Left;
            // 0..1: how far the right hand's height is replaced by the height that puts the staff butt on the ground.
            public float Plant;
        }

        // What a motion needs to know about the idle pose and the staff: idle hand offsets and the hand height (in arm
        // lengths above the shoulder at rest) at which the staff butt touches the ground.
        private struct Context
        {
            public Vector3 RestRight, RestLeft;
            public float PlantHandY;    // relative to the idle shoulder, in arm lengths
            public float PlantWorldY;   // model-space height of the right hand with the staff butt on the ground
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
                Name = "Thrust", Duration = .9f, Contact = .3f, FadeOutFrom = .5f,
                Evaluate = (t, c) =>
                {
                    float windup = Ph(t, 0f, .15f), thrust = Ph(t, .15f, .3f);
                    return new Frame
                    {
                        Twist = Mathf.Lerp(0f, -22f, windup) + Mathf.Lerp(0f, 36f, thrust),
                        Pitch = Mathf.Lerp(8f, 82f, thrust),
                        Right = Vector3.Lerp(Vector3.Lerp(c.RestRight, new Vector3(.22f, -.5f, .1f), windup), new Vector3(.97f, -.03f, .02f), thrust),
                    };
                },
                Check = (n, reach, up, dot) =>
                {
                    if (reach < .6f || dot <= .5f) throw new InvalidOperationException($"{n}: staff is not driven forward (reach {reach:F2}, aim {dot:F2}).");
                },
            },
            new Motion
            {
                Name = "Slam", Duration = 1f, Contact = .4f, FadeOutFrom = .65f,
                // The staff stays upright the whole time: lifted overhead (leaning a touch back), then driven straight down
                // so its butt lands on the ground in front of the hero.
                Evaluate = (t, c) =>
                {
                    float lift = Ph(t, 0f, .28f), drop = Ph(t, .28f, .4f);
                    return new Frame
                    {
                        Twist = Mathf.Lerp(0f, -8f, lift) + Mathf.Lerp(0f, 8f, drop),
                        Bend = -6f * lift + 16f * drop,
                        Pitch = Mathf.Lerp(Mathf.Lerp(0f, -8f, lift), 4f, drop),
                        Right = Vector3.Lerp(Vector3.Lerp(c.RestRight, new Vector3(.18f, .75f, .06f), lift), new Vector3(.55f, c.PlantHandY, .04f), drop),
                        Plant = drop,
                    };
                },
                Check = (n, reach, up, dot) =>
                {
                    if (reach < .3f || up > -.3f || Mathf.Abs(dot) > .35f)
                        throw new InvalidOperationException($"{n}: staff is not planted upright in front (reach {reach:F2}, hand {up:F2}, tilt {dot:F2}).");
                },
            },
            new Motion
            {
                Name = "Raise", Duration = 1.2f, Contact = .4f, FadeOutFrom = .85f, UsesLeftArm = true,
                Evaluate = (t, c) =>
                {
                    float lift = Ph(t, 0f, .4f);
                    return new Frame
                    {
                        Bend = -10f * lift,
                        Pitch = 4f,
                        Right = Vector3.Lerp(c.RestRight, new Vector3(.12f, .92f, .04f), lift),
                        Left = Vector3.Lerp(c.RestLeft, new Vector3(.5f, .45f, -.3f), lift),
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
                foreach (Motion motion in Motions) report.Add(BuildClip(hero, root, bones, motion));
                AssetDatabase.SaveAssets();
                File.WriteAllText("Docs/Validation/HeroMotionChecks.txt", string.Join("\n", report) + "\n");
                Debug.Log("Hero skill motions built.\n" + string.Join("\n", report));
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static string BuildClip(VisualOverride hero, GameObject root, Dictionary<string, Transform> bones, Motion motion)
        {
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
            // Staff butt on the ground: the grip sits 7 cm past the wrist and 38% of the 2 m staff above its butt (AttachProp).
            float scale = Mathf.Max(hero.scale, 1e-4f);
            float groundY = Mathf.Min(bones["LeftToeBase"].position.y, bones["RightToeBase"].position.y) - .03f / scale;
            var context = new Context { RestRight = restRight, RestLeft = restLeft, PlantWorldY = groundY + (.07f + .38f * 2f) / scale };
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
                if (frame.Left.HasValue)
                {
                    Vector3 lShoulder = lArm.position;
                    SolveArm(lArm, lFore, lHand, lShoulder + World(frame.Left.Value, forward, up, -right) * reach, lUpper, lLower,
                        -up * .8f - right * .6f - forward * .3f);
                }

                // Staff direction: `up` tipped toward `forward` by Pitch degrees.
                Vector3 staffDir = Quaternion.AngleAxis(frame.Pitch, pitchAxis) * up;
                hand.rotation = Quaternion.FromToRotation(hand.rotation * staffInHand, staffDir) * hand.rotation;

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

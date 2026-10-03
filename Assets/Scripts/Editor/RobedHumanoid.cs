using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lightbringer.EditorTools
{
    // Skeleton and skin weights shared by the A-posed, coat- or robe-wearing Tripo humanoids (Shieldbearer,
    // Mage, Priest). Measurements come from a per-unit Spec on the placed mesh (feet on the pivot, facing +Z);
    // x is measured from the head's centre line and z from the head's depth. Arms and legs own the vertices
    // close to their bone lines and fade out over one more radius, so sleeves, coat tails and robes hang
    // from the chest and hips while the limbs move inside them.
    internal sealed class RobedHumanoid
    {
        internal sealed class Spec
        {
            public float HeadProbeY = 1.58f, HeadBase, NeckBase, ChestBase, Belt, HipY, KneeY, HipDx, ToeZ;
            public float PadMinY, PadMaxY, PadDx, JointDx, ElbowMinY, ElbowMaxY, ElbowDx;
            public float WristMinY, WristMaxY, WristDx, PalmMinY, PalmMaxY, PalmDx;
            public float AnkleMinY, AnkleMaxY, AnkleDxMin, AnkleDxMax, AnkleZMin, AnkleZMax;
            public float ArmRadius, LegRadius;
            // Coat bones (x from the centre line, z from the head depth) and the regions they own.
            public Vector3 TabardTop, TabardBottom, CapeTop, CapeBottom;
            public float SkirtTopDx, SkirtTopY, SkirtBottomDx, SkirtBottomY;
            public float TabardMinZ, TabardHalfWidth, CapeMaxZ;
        }

        public readonly GeneratedRig Rig;
        public float Cx { get; private set; }
        public float Cz { get; private set; }
        private readonly Spec spec;
        private readonly Dictionary<string, int[]> palms = new Dictionary<string, int[]>();
        private readonly Dictionary<int, Vector3[]> armLines = new Dictionary<int, Vector3[]>();
        private readonly Dictionary<int, Vector3[]> legLines = new Dictionary<int, Vector3[]>();
        private readonly Dictionary<int, float> wristY = new Dictionary<int, float>();

        public RobedHumanoid(Transform root, Spec spec)
        {
            Rig = new GeneratedRig(root);
            this.spec = spec;
        }

        public int[] Palm(string side) => palms[side];

        public void Build(Transform root, Vector3[] v)
        {
            Spec s = spec;
            Vector3 head = GeneratedRig.Landmark(v, p => p.y > s.HeadProbeY, "head");
            Cx = head.x; Cz = head.z;
            Vector3 C(float y) => new Vector3(Cx, y, Cz);
            Vector3 Off(Vector3 o) => new Vector3(Cx + o.x, o.y, Cz + o.z);
            Transform hips = Rig.Bone("Hips", root, C(s.HipY + .02f), C(s.Belt + .03f));
            Transform spine = Rig.Bone("Spine", hips, C(s.Belt + .03f), C(s.ChestBase));
            Transform chest = Rig.Bone("Chest", spine, C(s.ChestBase), C(s.NeckBase));
            Transform neck = Rig.Bone("Neck", chest, C(s.NeckBase), C(s.HeadBase));
            Rig.Bone("Head", neck, C(s.HeadBase), C(1.7f));
            foreach (int sign in new[] { 1, -1 })
            {
                string side = sign == 1 ? "Right" : "Left";
                float D(Vector3 p) => (p.x - Cx) * sign;
                Vector3 pad = GeneratedRig.Landmark(v, p => p.y > s.PadMinY && p.y < s.PadMaxY && D(p) > s.PadDx, side + " shoulder");
                // Pauldrons and capelets pull the shoulder centroid outwards; the joint sits under them.
                Vector3 joint = new Vector3(Cx + sign * s.JointDx, (s.PadMinY + s.PadMaxY) * .5f, pad.z);
                Vector3 elbow = GeneratedRig.Landmark(v, p => p.y > s.ElbowMinY && p.y < s.ElbowMaxY && D(p) > s.ElbowDx, side + " elbow");
                Vector3 wrist = GeneratedRig.Landmark(v, p => p.y > s.WristMinY && p.y < s.WristMaxY && D(p) > s.WristDx, side + " wrist");
                Vector3 palm = GeneratedRig.Landmark(v, p => p.y > s.PalmMinY && p.y < s.PalmMaxY && D(p) > s.PalmDx, out int[] indices, side + " palm");
                palms[side] = indices;
                wristY[sign] = wrist.y;
                Vector3 fingers = palm + (palm - wrist).normalized * .04f;
                armLines[sign] = new[] { joint, elbow, wrist, fingers };
                Transform shoulder = Rig.Bone(side + "Shoulder", chest, new Vector3(Cx + sign * .06f, joint.y + .01f, joint.z), joint);
                Transform upper = Rig.Bone(side + "UpperArm", shoulder, joint, elbow);
                Transform fore = Rig.Bone(side + "ForeArm", upper, elbow, wrist);
                Rig.Bone(side + "Hand", fore, wrist, fingers);

                bool Boot(Vector3 p) => D(p) > s.AnkleDxMin && D(p) < s.AnkleDxMax && p.z - Cz > s.AnkleZMin && p.z - Cz < s.AnkleZMax;
                Vector3 ankle = GeneratedRig.Landmark(v, p => p.y > s.AnkleMinY && p.y < s.AnkleMaxY && Boot(p), side + " ankle");
                Vector3 hip = new Vector3(Cx + sign * s.HipDx, s.HipY, Cz);
                Vector3 knee = Vector3.Lerp(hip, ankle, (s.HipY - s.KneeY) / (s.HipY - ankle.y)) + Vector3.forward * .02f;
                Vector3 toe = new Vector3(ankle.x, .02f, Cz + s.ToeZ);
                legLines[sign] = new[] { hip, knee, ankle, toe };
                Transform thigh = Rig.Bone(side + "Thigh", hips, hip, knee);
                Transform shin = Rig.Bone(side + "Shin", thigh, knee, ankle);
                Rig.Bone(side + "Foot", shin, ankle, toe);
                Rig.Bone(side + "Skirt", hips, new Vector3(Cx + sign * s.SkirtTopDx, s.SkirtTopY, Cz),
                    new Vector3(Cx + sign * s.SkirtBottomDx, s.SkirtBottomY, Cz));
            }
            Rig.Bone("Tabard", hips, Off(s.TabardTop), Off(s.TabardBottom));
            Rig.Bone("Cape", chest, Off(s.CapeTop), Off(s.CapeBottom));
        }

        public void Skin(Mesh mesh) => Rig.Skin(mesh, Rigid, Influence, .006f, Cx);

        private static float LineDistance(Vector3 p, Vector3[] line)
        {
            float best = float.PositiveInfinity;
            for (int i = 0; i < line.Length - 1; i++)
            {
                Vector3 a = line[i], delta = line[i + 1] - a;
                float t = Mathf.Clamp01(Vector3.Dot(p - a, delta) / Mathf.Max(delta.sqrMagnitude, .00001f));
                best = Mathf.Min(best, (p - a - delta * t).magnitude);
            }
            return best;
        }

        private int SideOf(Vector3 p) => p.x >= Cx ? 1 : -1;
        private float ArmShare(Vector3 p) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(2f * spec.ArmRadius, spec.ArmRadius, LineDistance(p, armLines[SideOf(p)])));
        private float LegShare(Vector3 p) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(2f * spec.LegRadius, spec.LegRadius, LineDistance(p, legLines[SideOf(p)])));
        private bool InHead(Vector3 p) => p.y > spec.HeadBase && Mathf.Abs(p.x - Cx) < spec.JointDx;
        private bool IsHand(Vector3 p) => Mathf.Abs(p.x - Cx) > spec.PalmDx - .03f;

        // Palm and prop socket stay on exactly the same transform; the wrist above blends.
        private string Rigid(Vector3 p)
        {
            int sign = SideOf(p);
            bool core = LineDistance(p, armLines[sign]) < spec.ArmRadius;
            return core && p.y < wristY[sign] - .015f && IsHand(p) && !InHead(p) ? (sign == 1 ? "RightHand" : "LeftHand") : null;
        }

        private float Influence(Vector3 p, string name)
        {
            if (InHead(p)) return name == "Head" ? 1f : 0f;
            if (name == "Head") return 0f;
            float arm = ArmShare(p);
            bool upper = p.y >= spec.Belt;
            if (name.EndsWith("Arm") || name.EndsWith("Shoulder")) return arm;
            if (name.EndsWith("Hand")) return IsHand(p) ? arm : 0f;
            if (name == "Chest") return upper ? 1f : arm;
            float body = 1f - arm;
            float dz = p.z - Cz, dx = Mathf.Abs(p.x - Cx);
            if (name == "Hips") return body;
            if (name == "Spine" || name == "Neck") return upper ? body : 0f;
            if (name == "Cape") return dz < spec.CapeMaxZ && p.y < spec.CapeTop.y ? body * (upper ? 1f : 1f - LegShare(p)) : 0f;
            if (upper) return 0f;
            float leg = LegShare(p);
            if (name.EndsWith("Thigh") || name.EndsWith("Shin") || name.EndsWith("Foot")) return body * leg;
            if (name == "Tabard") return dz > spec.TabardMinZ && dx < spec.TabardHalfWidth ? body * (1f - leg) : 0f;
            if (name.EndsWith("Skirt")) return body * (1f - leg);
            return 0f;
        }

        // Hand rotation that aims a held prop in root space regardless of the arm pose.
        public static Quaternion Aim(string side, Func<string, Vector3> pose, Quaternion aim)
        {
            Quaternion parent = Quaternion.identity;
            foreach (string ancestor in new[] { "Hips", "Spine", "Chest", side + "Shoulder", side + "UpperArm", side + "ForeArm" })
                parent *= Quaternion.Euler(pose(ancestor));
            return Quaternion.Inverse(parent) * aim;
        }

        // Bind-pose centre of the palm vertices, in root space.
        public Vector3 PalmCentre(string side, Vector3[] bindVertices)
        {
            Vector3 sum = Vector3.zero;
            foreach (int index in palms[side]) sum += bindVertices[index];
            return sum / palms[side].Length;
        }
    }
}

using UnityEngine;

namespace Lightbringer.Visuals
{
    // Keeps a hand-held staff readable on top of generic (empty-hand) animation clips: the grip stays in
    // the hand, but the shaft stays mostly upright and only partly follows the wrist. While casting it
    // leans forward so the orb points at the target. Runs after the animation and the driver each frame.
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class PropStabilizer : MonoBehaviour
    {
        [Tooltip("0 = always upright, 1 = fully follows the wrist.")]
        [Range(0f, 1f)] public float follow = 0.3f;
        [Tooltip("Forward/back lean limit from the wrist (degrees).")]
        [Range(0f, 90f)] public float maxPitch = 25f;
        [Tooltip("Sideways lean limit from the wrist (degrees); kept small so the staff never swings across the body.")]
        [Range(0f, 90f)] public float maxRoll = 10f;
        [Range(0f, 80f)] public float castLean = 35f;
        [Tooltip("Grip height as a fraction of the prop's length from its bottom.")]
        [Range(0f, 1f)] public float gripFraction = 0.38f;

        private Transform bone;
        private Transform characterRoot;
        private Vector3 gripInBone;
        private Quaternion restInBone;
        private Vector3 gripInProp;
        private float lean;
        private float leanTarget;

        // Called once the prop sits at its bind-pose attachment under `handBone`.
        public void Configure(Transform handBone, Transform modelRoot)
        {
            bone = handBone;
            characterRoot = modelRoot;
            gripInProp = MeasureGrip();
            gripInBone = bone.InverseTransformPoint(transform.TransformPoint(gripInProp));
            restInBone = Quaternion.Inverse(bone.rotation) * transform.rotation;
        }

        public void SetCasting(bool casting) => leanTarget = casting ? castLean : 0f;

        private Vector3 MeasureGrip()
        {
            Bounds local = default;
            bool found = false;
            foreach (MeshFilter filter in GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                Bounds mesh = filter.sharedMesh.bounds;
                Matrix4x4 toProp = transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = mesh.center + Vector3.Scale(mesh.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 point = toProp.MultiplyPoint3x4(corner);
                    if (!found) { local = new Bounds(point, Vector3.zero); found = true; }
                    else local.Encapsulate(point);
                }
            }
            return found ? new Vector3(local.center.x, local.min.y + local.size.y * gripFraction, local.center.z) : Vector3.zero;
        }

        private void LateUpdate() => Apply(Time.deltaTime);

        public void Apply(float deltaTime)
        {
            if (bone == null || characterRoot == null) return;
            lean = Mathf.MoveTowards(lean, leanTarget, deltaTime * 240f);
            // Split the wrist-driven shaft direction into forward/back and sideways lean in the character's
            // frame, scale both by `follow`, clamp them separately, then add the cast lean forward.
            Quaternion root = characterRoot.rotation;
            Vector3 animatedUp = Quaternion.Inverse(root) * (bone.rotation * restInBone * Vector3.up);
            float pitch = Mathf.Atan2(animatedUp.z, Mathf.Max(animatedUp.y, 0.05f)) * Mathf.Rad2Deg;
            float roll = Mathf.Atan2(animatedUp.x, Mathf.Max(animatedUp.y, 0.05f)) * Mathf.Rad2Deg;
            pitch = Mathf.Clamp(pitch * follow, -maxPitch, maxPitch) + lean;
            roll = Mathf.Clamp(roll * follow, -maxRoll, maxRoll);
            transform.rotation = root * Quaternion.Euler(pitch, 0f, 0f) * Quaternion.Euler(0f, 0f, -roll);
            // Rotate about the grip so the staff never slides out of the hand.
            Vector3 grip = bone.TransformPoint(gripInBone);
            transform.position += grip - transform.TransformPoint(gripInProp);
        }
    }
}

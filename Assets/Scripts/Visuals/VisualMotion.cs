using UnityEngine;

namespace Lightbringer.Visuals
{
    // Cheap procedural life for silhouettes: stride bob and lean while moving, hover, wing flaps
    // and spinning crystals. Visual only; it never moves the gameplay transform or colliders.
    [DisallowMultipleComponent]
    public sealed class VisualMotion : MonoBehaviour
    {
        private float bobHeight;
        private float bobRate;
        private float leanDegrees;
        private float hoverHeight;
        private Transform[] parts = new Transform[0];
        private PartMotion[] motions = new PartMotion[0];
        private Quaternion[] baseRotations = new Quaternion[0];
        private Vector3 basePosition;
        private Vector3 lastOwnerPosition;
        private float smoothedSpeed;
        private float phase;
        private float seed;

        public void Configure(Silhouette silhouette, Transform[] movingParts)
        {
            bobHeight = silhouette.BobHeight;
            bobRate = silhouette.BobRate;
            leanDegrees = silhouette.LeanDegrees;
            hoverHeight = silhouette.HoverHeight;
            parts = movingParts ?? new Transform[0];
            motions = new PartMotion[parts.Length];
            baseRotations = new Quaternion[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                motions[i] = silhouette.Parts[i].Motion;
                baseRotations[i] = parts[i].localRotation;
            }
            basePosition = transform.localPosition;
            seed = (GetInstanceID() & 255) * 0.37f;
        }

        private void OnEnable()
        {
            if (transform.parent != null) lastOwnerPosition = transform.parent.position;
        }

        private void LateUpdate()
        {
            float delta = Time.deltaTime;
            Transform owner = transform.parent;
            if (delta <= 0f || owner == null) return;
            Vector3 position = owner.position;
            Vector3 step = position - lastOwnerPosition;
            step.y = 0f;
            lastOwnerPosition = position;
            // Crowd movement runs at a staggered 30 Hz, so smooth the measured speed.
            smoothedSpeed = Mathf.Lerp(smoothedSpeed, step.magnitude / delta, 1f - Mathf.Exp(-delta * 10f));
            float moving = Mathf.Clamp01((smoothedSpeed - 0.2f) / 1.5f);
            phase += delta * bobRate * Mathf.Lerp(0.3f, 1f, moving);
            float time = Time.time + seed;
            float lift = Mathf.Abs(Mathf.Sin(phase)) * bobHeight * moving + Mathf.Sin(time * 1.3f) * hoverHeight;
            float scaleY = Mathf.Max(owner.lossyScale.y, 0.0001f);
            transform.localPosition = basePosition + Vector3.up * (lift / scaleY);
            transform.localRotation = Quaternion.Euler(leanDegrees * moving, 0f, 0f);

            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == null) continue;
                switch (motions[i])
                {
                    case PartMotion.WingLeft:
                        parts[i].localRotation = baseRotations[i] * Quaternion.Euler(0f, 0f, Mathf.Sin(time * 3.2f) * 28f);
                        break;
                    case PartMotion.WingRight:
                        parts[i].localRotation = baseRotations[i] * Quaternion.Euler(0f, 0f, -Mathf.Sin(time * 3.2f) * 28f);
                        break;
                    case PartMotion.Spin:
                        parts[i].localRotation = baseRotations[i] * Quaternion.Euler(0f, time * 35f, 0f);
                        break;
                }
            }
        }
    }
}

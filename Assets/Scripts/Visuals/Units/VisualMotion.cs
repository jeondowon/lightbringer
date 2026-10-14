using UnityEngine;

namespace Lightbringer.Visuals
{
    // Cheap procedural life for models without skeletal animation: stride bob, lean and an attack lunge.
    // Visual only; it never moves the gameplay transform or colliders.
    [DisallowMultipleComponent]
    public sealed class VisualMotion : MonoBehaviour
    {
        private float bobHeight;
        private float bobRate;
        private float leanDegrees;
        private Vector3 basePosition;
        private Vector3 lastOwnerPosition;
        private float smoothedSpeed;
        private float phase;

        public void Configure(float bob, float rate, float lean)
        {
            bobHeight = bob;
            bobRate = rate;
            leanDegrees = lean;
            basePosition = transform.localPosition;
        }

        // Attack lunge for unanimated bodies: rear back during the wind-up, snap forward as the blow lands
        // (UnitCombat.StrikeDelay), then settle. Ranged attackers recoil slightly instead.
        private Lightbringer.Combat.UnitCombat combat;
        private float attackAge = -1f;
        private float windup;
        private bool rangedAttack;

        private void OnEnable()
        {
            if (transform.parent != null) lastOwnerPosition = transform.parent.position;
            combat = GetComponentInParent<Lightbringer.Combat.UnitCombat>();
            if (combat != null) combat.Attacked += OnAttacked;
        }

        private void OnDisable()
        {
            if (combat != null) combat.Attacked -= OnAttacked;
            combat = null;
            attackAge = -1f;
        }

        private void OnAttacked()
        {
            attackAge = 0f;
            windup = Mathf.Max(0.06f, combat.StrikeDelay);
            rangedAttack = combat.ProjectileType != Lightbringer.Combat.ProjectileKind.None;
        }

        // Forward offset (m) and pitch (deg) of the lunge at the current attack age.
        private void Lunge(float delta, out float forward, out float pitch)
        {
            forward = pitch = 0f;
            if (attackAge < 0f) return;
            attackAge += delta;
            float reach = rangedAttack ? 0.05f : 0.22f;
            if (attackAge < windup)
            {
                float t = Mathf.SmoothStep(0f, 1f, attackAge / windup);
                forward = -reach * 0.4f * t;
                pitch = -7f * t;
                return;
            }
            float after = attackAge - windup;
            const float Snap = 0.06f, Settle = 0.3f;
            if (after < Snap)
            {
                float t = after / Snap;
                forward = Mathf.Lerp(-reach * 0.4f, reach, t);
                pitch = Mathf.Lerp(-7f, rangedAttack ? -4f : 14f, t);
            }
            else if (after < Snap + Settle)
            {
                float t = Mathf.SmoothStep(0f, 1f, (after - Snap) / Settle);
                forward = Mathf.Lerp(reach, 0f, t);
                pitch = Mathf.Lerp(rangedAttack ? -4f : 14f, 0f, t);
            }
            else attackAge = -1f;
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
            float lift = Mathf.Abs(Mathf.Sin(phase)) * bobHeight * moving;
            float scaleY = Mathf.Max(owner.lossyScale.y, 0.0001f);
            Lunge(delta, out float lunge, out float lungePitch);
            float scaleZ = Mathf.Max(owner.lossyScale.z, 0.0001f);
            transform.localPosition = basePosition + Vector3.up * (lift / scaleY) + Vector3.forward * (lunge / scaleZ);
            transform.localRotation = Quaternion.Euler(leanDegrees * moving + lungePitch, 0f, 0f);
        }
    }
}

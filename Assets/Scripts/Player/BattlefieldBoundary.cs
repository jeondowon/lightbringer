using Lightbringer.Core;
using UnityEngine;

namespace Lightbringer.Player
{
    // Constrain the hero only; decorative terrain remains available to cameras.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class BattlefieldBoundary : MonoBehaviour
    {
        private Transform battlefield;
        private CharacterController controller;

        public void Configure(Transform root)
        {
            battlefield = root;
            controller = GetComponent<CharacterController>();
        }

        public Vector3 ClampPosition(Vector3 world)
        {
            if (battlefield == null) return world;
            Vector3 local = battlefield.InverseTransformPoint(world);
            const float margin = 1f;
            local.x = Mathf.Clamp(local.x, -BattlefieldLayout.HalfWidth + margin, BattlefieldLayout.HalfWidth - margin);
            local.z = Mathf.Clamp(local.z, BattlefieldLayout.MinZ + margin, BattlefieldLayout.MaxZ - margin);
            return battlefield.TransformPoint(local);
        }

        public Vector3 ConstrainMotion(Vector3 motion) => ClampPosition(transform.position + motion) - transform.position;

        // Also catches knockback and scripted displacement after the movement update.
        private void LateUpdate() => KeepInside();

        public void KeepInside()
        {
            if (battlefield == null || controller == null || !controller.enabled) return;
            Vector3 target = ClampPosition(transform.position);
            Vector3 local = battlefield.InverseTransformPoint(target);
            if (local.y < -3f)
            {
                local.y = controller.height * 0.5f - controller.center.y + 0.1f;
                target = battlefield.TransformPoint(local);
            }
            if ((target - transform.position).sqrMagnitude < 0.000001f) return;
            controller.enabled = false;
            transform.position = target;
            controller.enabled = true;
        }
    }
}

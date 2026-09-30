using Lightbringer.Pathing;
using UnityEngine;

namespace Lightbringer.Units
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class UnitPathFollower : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float moveSpeed = 3f;
        [SerializeField, Min(0f)] private float rotationSpeed = 360f;
        [SerializeField, Min(0.05f)] private float arrivalDistance = 0.3f;
        [SerializeField] private float gravity = -25f;

        public WaypointPath AssignedPath { get; private set; }
        public bool HasReachedEnd { get; private set; }
        private CharacterController controller;
        private int waypointIndex;
        private float verticalSpeed;
        private bool hasSteeringOverride;
        private Vector3 steeringDestination;
        private float steeringStopDistance;

        public void SetSteeringOverride(Vector3 destination, float stopDistance)
        {
            hasSteeringOverride = true;
            steeringDestination = destination;
            steeringStopDistance = Mathf.Max(0f, stopDistance);
        }

        public void ClearSteeringOverride()
        {
            if (!hasSteeringOverride)
                return;
            hasSteeringOverride = false;
            if (AssignedPath != null && AssignedPath.IsValid)
            {
                waypointIndex = Mathf.Min(AssignedPath.Count - 1,
                    Mathf.Max(waypointIndex, AssignedPath.FindEntryWaypoint(transform.position)));
                HasReachedEnd = false;
            }
        }

        // Assignment is permanent for this unit. Changing the summoner affects new units only.
        public bool TryAssignPath(WaypointPath path)
        {
            if (AssignedPath != null || path == null || !path.IsValid)
                return false;
            controller = GetComponent<CharacterController>();
            AssignedPath = path;
            waypointIndex = path.FindEntryWaypoint(transform.position);
            return true;
        }

        private void Update() => Tick(Time.deltaTime);

        private void Tick(float deltaTime)
        {
            if (controller == null)
                controller = GetComponent<CharacterController>();
            if (deltaTime <= 0f || !isActiveAndEnabled || !controller.enabled)
                return;

            Vector3 horizontal = Vector3.zero;
            if (hasSteeringOverride)
            {
                Vector3 offset = steeringDestination - transform.position;
                offset.y = 0f;
                float travel = Mathf.Min(moveSpeed * deltaTime, Mathf.Max(0f, offset.magnitude - steeringStopDistance));
                horizontal = offset.normalized * travel;
                if (offset.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation,
                        Quaternion.LookRotation(offset, Vector3.up), rotationSpeed * deltaTime);
            }
            while (!hasSteeringOverride && AssignedPath != null && AssignedPath.IsValid && !HasReachedEnd)
            {
                waypointIndex = Mathf.Min(waypointIndex, AssignedPath.Count - 1);
                Vector3 offset = AssignedPath.GetPosition(waypointIndex) - transform.position;
                offset.y = 0f;
                if (offset.sqrMagnitude <= arrivalDistance * arrivalDistance)
                {
                    waypointIndex++;
                    HasReachedEnd = waypointIndex >= AssignedPath.Count;
                    continue;
                }
                horizontal = Vector3.ClampMagnitude(offset, moveSpeed * deltaTime);
                if (horizontal.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation,
                        Quaternion.LookRotation(offset, Vector3.up), rotationSpeed * deltaTime);
                break;
            }

            if (controller.isGrounded && verticalSpeed < 0f)
                verticalSpeed = -2f;
            verticalSpeed += gravity * deltaTime;
            CollisionFlags collisions = controller.Move(horizontal + Vector3.up * verticalSpeed * deltaTime);
            if ((collisions & CollisionFlags.Below) != 0 && verticalSpeed < 0f)
                verticalSpeed = -2f;
            if ((collisions & CollisionFlags.Above) != 0 && verticalSpeed > 0f)
                verticalSpeed = 0f;
        }

        private void OnValidate()
        {
            moveSpeed = Mathf.Max(0f, moveSpeed);
            rotationSpeed = Mathf.Max(0f, rotationSpeed);
            arrivalDistance = Mathf.Max(0.05f, arrivalDistance);
            gravity = Mathf.Min(-0.01f, gravity);
        }
    }
}

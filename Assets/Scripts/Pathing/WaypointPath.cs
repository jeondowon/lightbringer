using UnityEngine;

namespace Lightbringer.Pathing
{
    [DisallowMultipleComponent]
    public sealed class WaypointPath : MonoBehaviour
    {
        [Tooltip("Ordered ground-level waypoints, from the allied side toward the objective.")]
        [SerializeField] private Transform[] waypoints = new Transform[0];

        public int Count => waypoints.Length;
        public bool IsValid
        {
            get
            {
                if (waypoints.Length == 0)
                    return false;
                foreach (Transform waypoint in waypoints)
                    if (waypoint == null)
                        return false;
                return true;
            }
        }

        public Vector3 GetPosition(int index) => waypoints[index].position;

        public void Configure(Transform[] points) => waypoints = points ?? new Transform[0];

        // Join the closest segment in its forward direction instead of returning to the start.
        public int FindEntryWaypoint(Vector3 position)
        {
            int bestIndex = 0;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < Count - 1; i++)
            {
                float distance = ProjectOnSegment(i, position, out float progress);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = progress <= 0f ? i : i + 1;
                }
            }
            return bestIndex;
        }

        // Horizontal squared distance to the nearest segment (infinite for a single-waypoint Path).
        public float SqrDistanceTo(Vector3 position)
        {
            float best = float.PositiveInfinity;
            for (int i = 0; i < Count - 1; i++)
                best = Mathf.Min(best, ProjectOnSegment(i, position, out _));
            return best;
        }

        // Ground-plane projection onto segment i: progress along it (0..1) and squared distance from it.
        private float ProjectOnSegment(int i, Vector3 position, out float progress)
        {
            Vector3 start = GetPosition(i);
            Vector3 end = GetPosition(i + 1);
            position.y = start.y = end.y = 0f;
            Vector3 segment = end - start;
            progress = segment.sqrMagnitude > 0.0001f
                ? Mathf.Clamp01(Vector3.Dot(position - start, segment) / segment.sqrMagnitude) : 0f;
            return (position - (start + segment * progress)).sqrMagnitude;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < Count; i++)
            {
                if (waypoints[i] == null)
                    continue;
                Gizmos.DrawWireSphere(GetPosition(i) + Vector3.up * 0.2f, 0.4f);
                if (i > 0 && waypoints[i - 1] != null)
                    Gizmos.DrawLine(GetPosition(i - 1) + Vector3.up * 0.2f,
                        GetPosition(i) + Vector3.up * 0.2f);
            }
        }
    }
}

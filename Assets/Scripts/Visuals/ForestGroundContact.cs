using UnityEngine;

namespace Lightbringer.Visuals
{
    // Baked from visible root vertices in the Editor; no readable meshes required in a player build.
    public sealed class ForestGroundContact : MonoBehaviour
    {
        public Vector3[] supportPoints = System.Array.Empty<Vector3>();

        public void Ground(BattlefieldEnvironment terrain)
        {
            if (supportPoints.Length == 0) return;
            float highestGap = float.NegativeInfinity;
            foreach (Vector3 point in supportPoints)
            {
                Vector3 p = terrain.transform.InverseTransformPoint(transform.TransformPoint(point));
                highestGap = Mathf.Max(highestGap, p.y - terrain.SurfaceHeight(p.x, p.z));
            }
            transform.position -= terrain.transform.up * (highestGap + 0.06f);
        }
    }
}

using Lightbringer.Core;
using Lightbringer.Player;
using UnityEngine;

namespace Lightbringer.EditorTools
{
    public static partial class GreyboxValidation
    {
        private static void ValidateBattlefieldBoundary()
        {
            GameObject root = new GameObject("Boundary validation");
            root.transform.position = new Vector3(7000f, 0f, 7000f);
            root.transform.rotation = Quaternion.Euler(0f, 35f, 0f);
            GameObject hero = new GameObject("Boundary hero");
            hero.transform.SetParent(root.transform, false);
            CharacterController controller = hero.AddComponent<CharacterController>();
            BattlefieldBoundary boundary = hero.AddComponent<BattlefieldBoundary>();
            boundary.Configure(root.transform);
            try
            {
                Vector3 inside = root.transform.TransformPoint(new Vector3(12f, 1f, 42f));
                Check(Vector3.Distance(boundary.ClampPosition(inside), inside) < 0.002f,
                    "Boundary preserves interior movement in a translated and rotated battlefield");
                foreach (Vector3 local in new[] { new Vector3(-100f, 2f, 20f), new Vector3(100f, 2f, 20f),
                    new Vector3(0f, 2f, -100f), new Vector3(0f, 2f, 200f), new Vector3(100f, 2f, 200f) })
                {
                    Vector3 clamped = root.transform.InverseTransformPoint(boundary.ClampPosition(root.transform.TransformPoint(local)));
                    Check(Mathf.Abs(clamped.x) <= BattlefieldLayout.HalfWidth - 0.99f
                        && clamped.z >= BattlefieldLayout.MinZ + 0.99f && clamped.z <= BattlefieldLayout.MaxZ - 0.99f
                        && Mathf.Abs(clamped.y - 2f) < 0.002f, "Hero stays inside edge/corner " + local);
                }
                hero.transform.localPosition = new Vector3(33f, 1f, 20f);
                Vector3 motion = root.transform.InverseTransformVector(boundary.ConstrainMotion(root.transform.TransformVector(new Vector3(3f, 0f, 4f))));
                Check(Mathf.Abs(motion.x) < 0.002f && Mathf.Abs(motion.z - 4f) < 0.002f,
                    "Walking diagonally against an edge preserves movement along it");
                hero.transform.localPosition = new Vector3(120f, 1f, 30f);
                boundary.KeepInside();
                Check(hero.transform.localPosition.x <= 33.01f && controller.enabled,
                    "External knockback outside the field is recovered with controller enabled");
                hero.transform.localPosition = new Vector3(0f, -8f, 20f);
                boundary.KeepInside();
                Check(hero.transform.localPosition.y >= 1f, "Fallen hero recovers above the playable terrain");
                controller.enabled = false;
                hero.transform.localPosition = new Vector3(100f, 1f, 20f);
                boundary.KeepInside();
                Check(!controller.enabled && hero.transform.localPosition.x > 99f,
                    "Boundary respects a disabled controller during scripted sequences");
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}

using Lightbringer.Combat;
using UnityEngine;

namespace Lightbringer.Visuals
{
    // String and nocked arrow of a hand-held bow prop whose shoot clip only animates the arms: when the string
    // hand reaches the string's rest point it catches it, and the string and arrow follow that hand until the
    // shot is loosed (UnitCombat.Attacked) or the hand leaves the draw zone. Runs after PropStabilizer has
    // placed the bow. Works in the bow body's space: +Z towards the target, string at -Z, limbs along Y.
    [DefaultExecutionOrder(110)]
    [DisallowMultipleComponent]
    public sealed class BowString : MonoBehaviour
    {
        // Bow-body units (the bow is about 1 unit tall).
        public const float CatchRadius = 0.08f;
        private const float Flex = 0.35f;
        private static readonly Vector3 ArrowRest = new Vector3(-0.018f, 0.025f, 0f);

        [SerializeField] private Transform body, limbs, upperString, lowerString, arrow;
        [SerializeField] private float half = 0.5f, brace = 0.11f, arrowLength = 0.5f, maxDraw = 0.75f;
        private Transform hand, foreArm;
        private UnitCombat combat;
        private bool drawn, shown = true;

        public Vector3 RestNock => new Vector3(0f, 0f, -brace);

        public void Configure(Transform bowBody, Transform limbMesh, Transform upper, Transform lower, Transform nockedArrow,
            float bowHalf, float bowBrace, float shaftLength, float drawLimit)
        {
            body = bowBody; limbs = limbMesh; upperString = upper; lowerString = lower; arrow = nockedArrow;
            half = bowHalf; brace = bowBrace; arrowLength = shaftLength; maxDraw = drawLimit;
            Apply(RestNock, 0f, false);
        }

        private void Start()
        {
            ModelAnimationDriver driver = GetComponentInParent<ModelAnimationDriver>();
            hand = FindDeep(driver != null ? driver.transform : transform.root, "RightHand");
            foreArm = hand != null ? hand.parent : null;
            combat = GetComponentInParent<UnitCombat>();
            if (combat != null) combat.Attacked += Release;
        }

        private void OnDestroy()
        {
            if (combat != null) combat.Attacked -= Release;
        }

        private void Release() => drawn = false;

        private void LateUpdate()
        {
            if (hand == null || foreArm == null || body == null) return;
            Vector3 wrist = hand.position;
            // The palm sits a little past the wrist, along the forearm.
            Vector3 local = body.InverseTransformPoint(wrist + (wrist - foreArm.position) * 0.3f);
            bool inZone = Mathf.Abs(local.x) < 0.2f && Mathf.Abs(local.y) < 0.25f
                && local.z < -brace + 0.05f && local.z > -brace - maxDraw - 0.15f;
            if (!drawn && (local - RestNock).sqrMagnitude < CatchRadius * CatchRadius) drawn = true;
            else if (drawn && !inZone) drawn = false;
            if (!drawn)
            {
                if (shown) Apply(RestNock, 0f, false);
                return;
            }
            Vector3 nock = new Vector3(0f, Mathf.Clamp(local.y, -0.1f, 0.1f), Mathf.Clamp(local.z, -brace - maxDraw, -brace));
            Apply(nock, Mathf.Clamp01((-brace - nock.z) / maxDraw), true);
        }

        private void Apply(Vector3 nock, float draw, bool arrowVisible)
        {
            limbs.localScale = new Vector3(1f, 1f, 1f + Flex * draw);
            Vector3 top = new Vector3(0f, half, -brace * limbs.localScale.z);
            Vector3 bottom = new Vector3(0f, -half, top.z);
            Segment(upperString, top, nock);
            Segment(lowerString, bottom, nock);
            if (arrow.gameObject.activeSelf != arrowVisible) arrow.gameObject.SetActive(arrowVisible);
            if (arrowVisible)
            {
                arrow.localPosition = nock;
                arrow.localRotation = Quaternion.LookRotation(ArrowRest - nock, Vector3.up);
                // Long enough to reach past the bow at full draw.
                float length = (ArrowRest - nock).magnitude + 0.12f;
                arrow.localScale = new Vector3(1f, 1f, Mathf.Max(1f, length / arrowLength));
            }
            shown = arrowVisible || draw > 0f;
        }

        private static void Segment(Transform segment, Vector3 from, Vector3 to)
        {
            segment.localPosition = (from + to) * 0.5f;
            segment.localRotation = Quaternion.FromToRotation(Vector3.up, to - from);
            segment.localScale = new Vector3(0.005f, (to - from).magnitude * 0.5f, 0.005f);
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                Transform found = FindDeep(child, name);
                if (found != null) return found;
            }
            return null;
        }
    }
}

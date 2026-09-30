using UnityEngine;
using UnityEngine.Rendering;

namespace Lightbringer.Aura
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(HeroAura))]
    public sealed class AuraRangeVisual : MonoBehaviour
    {
        [Tooltip("Serialized shader reference keeps the URP shader available in builds.")]
        [SerializeField] private Shader ringShader;
        [SerializeField] private Color color = new Color(1f, 0.8f, 0.2f);
        [SerializeField, Min(0.01f)] private float width = 0.06f;
        [Tooltip("Offset from the Capsule centre; intended for the flat Greybox ground.")]
        [SerializeField] private float heightOffset = -0.9f;

        private HeroAura aura;
        private LineRenderer ring;
        private Material material;
        private const int Segments = 96;

        private void OnEnable()
        {
            aura = GetComponent<HeroAura>();
            if (ringShader == null)
            {
                Debug.LogError("AuraRangeVisual needs its URP shader reference. Run Greybox setup.", this);
                enabled = false;
                return;
            }
            GameObject visual = new GameObject("Aura Range (Runtime)");
            visual.transform.SetParent(transform, false);
            ring = visual.AddComponent<LineRenderer>();
            material = new Material(ringShader);
            ring.sharedMaterial = material;
            ring.loop = true;
            ring.useWorldSpace = true;
            ring.positionCount = Segments;
            ring.shadowCastingMode = ShadowCastingMode.Off;
            ring.receiveShadows = false;
            LateUpdate();
        }

        private void LateUpdate()
        {
            if (ring == null || aura == null)
                return;
            ring.enabled = aura.isActiveAndEnabled && aura.Radius > 0f;
            if (!ring.enabled)
                return;
            material.SetColor("_BaseColor", color);
            ring.widthMultiplier = width;
            Vector3 centre = transform.position + Vector3.up * heightOffset;
            for (int i = 0; i < Segments; i++)
            {
                float angle = i * Mathf.PI * 2f / Segments;
                ring.SetPosition(i, centre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * aura.Radius);
            }
        }

        private void OnDisable()
        {
            if (ring != null)
            {
                ring.enabled = false;
                if (Application.isPlaying)
                    Destroy(ring.gameObject);
                else
                    DestroyImmediate(ring.gameObject);
            }
            if (material != null)
            {
                if (Application.isPlaying)
                    Destroy(material);
                else
                    DestroyImmediate(material);
            }
            ring = null;
            material = null;
        }
    }
}

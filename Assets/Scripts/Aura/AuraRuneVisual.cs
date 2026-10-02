using Lightbringer.Visuals;
using UnityEngine;
using UnityEngine.Rendering;

namespace Lightbringer.Aura
{
    // Art Pass aura: a rotating rune circle on the ground, light motes rising inside the radius,
    // and a global aura sphere that makes allied toon materials glow while inside.
    // Radius always follows HeroAura, so Aura Size growth is visible immediately.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(HeroAura))]
    public sealed class AuraRuneVisual : MonoBehaviour
    {
        private static readonly int AuraSphereId = Shader.PropertyToID("_LB_AuraSphere");
        private static readonly int AuraColorId = Shader.PropertyToID("_LB_AuraColor");
        private static readonly int GlyphCountId = Shader.PropertyToID("_GlyphCount");

        [SerializeField] private ArtStyleLibrary style;
        [Tooltip("Ground height relative to the hero pivot.")]
        [SerializeField] private float groundOffset = -0.97f;

        private HeroAura aura;
        private GameObject disc;
        private Material discMaterial;
        private ParticleSystem motes;
        private float appliedRadius = -1f;

        public GameObject Disc => disc;

        public void Configure(ArtStyleLibrary library, float groundHeight)
        {
            style = library;
            groundOffset = groundHeight;
        }

        private void OnEnable()
        {
            aura = GetComponent<HeroAura>();
            if (style == null || style.auraRunesMaterial == null || style.auraMotesMaterial == null)
            {
                Debug.LogError("AuraRuneVisual needs an Art Style Library with aura materials. Run Lightbringer > Art > Build Art Style Assets.", this);
                enabled = false;
                return;
            }
            // Not parented to the hero so the runes do not spin with the hero's facing.
            disc = new GameObject("Aura Runes (Runtime)");
            disc.transform.SetParent(transform.parent, false);
            discMaterial = new Material(style.auraRunesMaterial) { name = "Aura Runes (Instance)" };
            disc.AddComponent<MeshFilter>().sharedMesh = ProceduralMeshes.GroundQuad;
            MeshRenderer renderer = disc.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = discMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            motes = CreateMotes(disc.transform.parent);
            appliedRadius = -1f;
            LateUpdate();
        }

        private ParticleSystem CreateMotes(Transform parent)
        {
            GameObject item = new GameObject("Aura Motes (Runtime)");
            item.SetActive(false);
            item.transform.SetParent(parent, false);
            ParticleSystem system = item.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.4f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.17f);
            main.startColor = new ParticleSystem.MinMaxGradient(Color.white, new Color(0.7f, 0.85f, 1f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 400;
            main.playOnAwake = true;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.rotation = new Vector3(90f, 0f, 0f);
            shape.radiusThickness = 1f;
            ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
            ParticleSystem.ColorOverLifetimeModule fade = system.colorOverLifetime;
            fade.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0f, 1f) });
            fade.color = new ParticleSystem.MinMaxGradient(gradient);
            ParticleSystemRenderer renderer = item.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = style.auraMotesMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            item.SetActive(true);
            return system;
        }

        private void LateUpdate()
        {
            if (disc == null || aura == null) return;
            float radius = aura.Radius;
            bool visible = aura.isActiveAndEnabled && radius > 0f;
            if (disc.activeSelf != visible) disc.SetActive(visible);
            if (motes != null && motes.gameObject.activeSelf != visible) motes.gameObject.SetActive(visible);
            Vector3 centre = transform.position + Vector3.up * groundOffset;
            Shader.SetGlobalVector(AuraSphereId, new Vector4(centre.x, centre.y, centre.z, visible ? radius : 0f));
            Shader.SetGlobalColor(AuraColorId, style.auraColor);
            if (!visible) return;
            disc.transform.position = centre + Vector3.up * 0.03f;
            disc.transform.rotation = Quaternion.identity;
            if (motes != null) motes.transform.position = centre + Vector3.up * 0.05f;
            if (Mathf.Approximately(radius, appliedRadius)) return;
            appliedRadius = radius;
            disc.transform.localScale = new Vector3(radius * 2f, 1f, radius * 2f);
            // Keep glyphs a similar world size as the aura grows.
            discMaterial.SetFloat(GlyphCountId, Mathf.Clamp(Mathf.Round(radius * 5f), 16f, 96f));
            if (motes == null) return;
            ParticleSystem.ShapeModule shape = motes.shape;
            shape.radius = radius;
            ParticleSystem.EmissionModule emission = motes.emission;
            emission.rateOverTime = Mathf.Clamp(radius * radius * 1.1f, 12f, 140f);
        }

        private void OnDisable()
        {
            Shader.SetGlobalVector(AuraSphereId, Vector4.zero);
            Release(disc);
            if (motes != null) Release(motes.gameObject);
            Release(discMaterial);
            disc = null; motes = null; discMaterial = null;
        }

        private static void Release(Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) Destroy(target); else DestroyImmediate(target);
        }
    }
}

using UnityEngine;
using UnityEngine.Rendering;
using Lightbringer.Player;

namespace Lightbringer.Visuals
{
    // Presentation only: the impact footprint never participates in damage queries.
    [ExecuteAlways]
    public sealed class CelestialStrikeVfx : MonoBehaviour
    {
        public const float Lifetime = 1.35f;
        private static readonly int AgeId = Shader.PropertyToID("_Age");
        private static readonly int ModeId = Shader.PropertyToID("_Mode");
        private static readonly int DescentId = Shader.PropertyToID("_Descent");
        private MaterialPropertyBlock properties;
        private Mesh columnMesh;
        private MeshRenderer column, ground;
        private LineRenderer core;
        private Light impactLight;
        private float age;
        private float startedAt;
        private float startDelay;

        public static CelestialStrikeVfx Spawn(ArtStyleLibrary style, Vector3 point, Transform parent, float delay = 0)
        {
            if (style == null || style.celestialStrikeMaterial == null) return null;
            var root = new GameObject("Celestial Light Strike (VFX)");
            root.transform.SetParent(parent, false);
            root.transform.position = point;
            var effect = root.AddComponent<CelestialStrikeVfx>();
            effect.startDelay = Mathf.Max(0, delay);
            effect.age = -effect.startDelay;
            effect.startedAt = Time.time + effect.startDelay;
            effect.Build(style);
            return effect;
        }

        private void Build(ArtStyleLibrary style)
        {
            properties = new MaterialPropertyBlock();
            const int segments = 64;
            var vertices = new Vector3[(segments + 1) * 2];
            var uv = new Vector2[vertices.Length];
            var indices = new int[segments * 6];
            for (int i = 0; i <= segments; i++)
            {
                float u = i / (float)segments;
                Vector3 radial = new Vector3(Mathf.Cos(u * Mathf.PI * 2), 0, Mathf.Sin(u * Mathf.PI * 2));
                vertices[i * 2] = radial * 0.78f;
                vertices[i * 2 + 1] = radial * 1.10f + Vector3.up * 26;
                uv[i * 2] = new Vector2(u, 0);
                uv[i * 2 + 1] = new Vector2(u, 1);
                if (i == segments) continue;
                int k = i * 6, v = i * 2;
                indices[k] = v; indices[k + 1] = v + 1; indices[k + 2] = v + 2;
                indices[k + 3] = v + 2; indices[k + 4] = v + 1; indices[k + 5] = v + 3;
            }
            columnMesh = new Mesh { name = "Celestial Column", hideFlags = HideFlags.DontSave };
            columnMesh.vertices = vertices; columnMesh.uv = uv; columnMesh.triangles = indices;
            columnMesh.RecalculateBounds();
            column = Surface("Prismatic Light Curtain", columnMesh, style.celestialStrikeMaterial);
            ground = Surface("Impact Sigil and Shockwave", ProceduralMeshes.GroundQuad, style.celestialStrikeMaterial);
            ground.transform.localPosition = Vector3.up * 0.065f;
            ground.transform.localScale = new Vector3(5.6f, 1, 5.6f);
            if (style.lightningMaterial != null)
            {
                core = new GameObject("White Gold Core").AddComponent<LineRenderer>();
                core.transform.SetParent(transform, false);
                core.sharedMaterial = style.lightningMaterial;
                core.useWorldSpace = false;
                core.positionCount = 2;
                core.widthMultiplier = 0.72f;
                core.shadowCastingMode = ShadowCastingMode.Off;
                core.receiveShadows = false;
            }
            impactLight = new GameObject("Impact Light").AddComponent<Light>();
            impactLight.transform.SetParent(transform, false);
            impactLight.transform.localPosition = Vector3.up * 1.8f;
            impactLight.color = new Color(1, 0.79f, 0.43f);
            impactLight.range = 6;
            impactLight.shadows = LightShadows.None;
            if (style.auraMotesMaterial != null) BuildSparks(style.auraMotesMaterial);
            Tick(0);
        }

        private MeshRenderer Surface(string label, Mesh mesh, Material material)
        {
            var item = new GameObject(label);
            item.transform.SetParent(transform, false);
            item.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = item.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        private void BuildSparks(Material material)
        {
            var item = new GameObject("Golden Impact Fragments");
            item.SetActive(false);
            item.transform.SetParent(transform, false);
            item.transform.localPosition = Vector3.up * 0.15f;
            item.transform.localRotation = Quaternion.Euler(-90, 0, 0);
            var particles = item.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.loop = false;
            main.duration = 0.12f;
            main.startDelay = startDelay + HeroAbilities.LightStaffDescent;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.95f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2, 6);
            main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.085f);
            main.startColor = new Color(1, 0.77f, 0.35f);
            main.gravityModifier = 0.55f;
            main.maxParticles = 48;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var emission = particles.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0, 42) });
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 67; shape.radius = 0.48f;
            var color = particles.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(new Color(1, 0.65f, 0.2f), 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
            color.color = gradient;
            var renderer = item.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 2;
            renderer.velocityScale = 0.08f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            item.SetActive(true);
        }

        private void Update()
        {
            if (Application.IsPlaying(gameObject)) Tick(Mathf.Max(0, Time.time - startedAt - age));
        }

        public void Tick(float delta)
        {
            age += Mathf.Max(0, delta);
            properties.SetFloat(AgeId, age);
            properties.SetFloat(DescentId, HeroAbilities.LightStaffDescent);
            properties.SetFloat(ModeId, 0);
            column.SetPropertyBlock(properties);
            properties.SetFloat(ModeId, 1);
            ground.SetPropertyBlock(properties);
            float fade = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.18f, 0.6f, age));
            if (core != null)
            {
                core.SetPosition(0, Vector3.up * 26);
                core.SetPosition(1, Vector3.up * Mathf.Lerp(26, 0.08f, Mathf.Clamp01(age / HeroAbilities.LightStaffDescent)));
                core.widthMultiplier = 0.72f * fade;
                core.startColor = new Color(1, 0.83f, 0.5f, 0);
                core.endColor = new Color(1, 0.83f, 0.5f, fade * 0.8f);
            }
            impactLight.intensity = age < HeroAbilities.LightStaffDescent ? 0 : 4 * fade;
            if (age >= Lifetime)
            {
                if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (columnMesh == null) return;
            if (Application.isPlaying) Destroy(columnMesh); else DestroyImmediate(columnMesh);
        }
    }
}

using UnityEngine;
using UnityEngine.Rendering;

namespace Lightbringer.Visuals
{
    // Shared surfaces for ranged shots drawn with the ShotFx shader: camera-facing comet heads and glints,
    // energy trail ribbons, and a small budget of point lights so a volley of bolts never floods the
    // forward renderer. One material; per-renderer settings travel in property blocks.
    public static class ShotVfx
    {
        public const float CometMode = 0, GlintMode = 1, SigilMode = 2, ColumnMode = 3, RibbonMode = 4;
        private const int MaxLights = 8;

        private static readonly int ModeId = Shader.PropertyToID("_Mode");
        private static readonly int AlliedId = Shader.PropertyToID("_Allied");
        private static readonly int KindId = Shader.PropertyToID("_Kind");
        private static readonly int StretchId = Shader.PropertyToID("_Stretch");
        private static readonly int AgeId = Shader.PropertyToID("_Age");
        private static readonly int LifeId = Shader.PropertyToID("_Life");
        private static readonly int SeedId = Shader.PropertyToID("_Seed");
        private static Material material;
        private static MaterialPropertyBlock block;
        private static int lights;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetBudget() => lights = 0;

        public static bool Ready
        {
            get
            {
                if (material != null) return true;
                Shader shader = UnityEngine.Resources.Load<Shader>("ShotFx");
                if (shader == null) return false;
                material = new Material(shader) { name = "LB Shot FX", hideFlags = HideFlags.DontSave };
                block = new MaterialPropertyBlock();
                return true;
            }
        }

        // Camera-facing quad; the shader turns it toward the view and stretches comets along their flight.
        public static Renderer Billboard(Transform parent, string label, float size, float mode, bool allied, float stretch = 1f)
        {
            if (!Ready) return null;
            var item = new GameObject(label);
            item.transform.SetParent(parent, false);
            item.transform.localScale = Vector3.one * size;
            item.AddComponent<MeshFilter>().sharedMesh = AttackVfx.HaloQuad;
            Renderer renderer = Prepare(item.AddComponent<MeshRenderer>());
            // The shader moves the quad's corners, so the mesh bounds would cull a comet tail too early.
            renderer.localBounds = new Bounds(Vector3.zero, Vector3.one * (2f + stretch * 2f));
            Apply(renderer, mode, allied, 1f, stretch, Random.value * 10f);
            return renderer;
        }

        // Surface on an arbitrary mesh (impact decals and columns), animated through Apply with its age.
        public static Renderer Surface(Transform parent, string label, Mesh mesh)
        {
            if (!Ready) return null;
            var item = new GameObject(label);
            item.transform.SetParent(parent, false);
            item.AddComponent<MeshFilter>().sharedMesh = mesh;
            return Prepare(item.AddComponent<MeshRenderer>());
        }

        // Energy ribbon behind a shot. Arrows get a keen streak, bolts a twisting plasma (or burning smoke) band.
        public static TrailRenderer Trail(Transform parent, string label, bool allied, bool bolt, float time, float width)
        {
            if (!Ready) return null;
            var item = new GameObject(label);
            item.transform.SetParent(parent, false);
            TrailRenderer trail = item.AddComponent<TrailRenderer>();
            trail.sharedMaterial = material;
            trail.time = time;
            trail.minVertexDistance = 0.05f;
            trail.widthMultiplier = width;
            trail.widthCurve = bolt
                ? new AnimationCurve(new Keyframe(0f, 0.75f), new Keyframe(0.1f, 1f), new Keyframe(1f, 0.1f))
                : new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(0.08f, 1f), new Keyframe(1f, 0f));
            trail.textureMode = LineTextureMode.Stretch;
            trail.alignment = LineAlignment.View;
            trail.numCapVertices = 2;
            Prepare(trail);
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.55f, 0.45f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = gradient;
            Apply(trail, RibbonMode, allied, bolt ? 1f : 0f, 1f, Random.value * 10f);
            return trail;
        }

        public static void Apply(Renderer renderer, float mode, bool allied, float kind, float stretch, float seed,
            float age = 0f, float life = 1f)
        {
            if (renderer == null || block == null) return;
            block.Clear();
            block.SetFloat(ModeId, mode);
            block.SetFloat(AlliedId, allied ? 1f : 0f);
            block.SetFloat(KindId, kind);
            block.SetFloat(StretchId, stretch);
            block.SetFloat(SeedId, seed);
            block.SetFloat(AgeId, age);
            block.SetFloat(LifeId, life);
            renderer.SetPropertyBlock(block);
        }

        // A point light from the shared budget, or null when enough shots already light the field.
        public static Light Glow(Transform parent, Vector3 local, Color color, float range, float intensity)
        {
            if (lights >= MaxLights) return null;
            Light light = new GameObject("Shot Light").AddComponent<Light>();
            light.transform.SetParent(parent, false);
            light.transform.localPosition = local;
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            light.gameObject.AddComponent<LightBudget>();
            return light;
        }

        private static Renderer Prepare(Renderer renderer)
        {
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return renderer;
        }

        private sealed class LightBudget : MonoBehaviour
        {
            private void Awake() => lights++;
            private void OnDestroy() => lights = Mathf.Max(0, lights - 1);
        }
    }

    // A magic bolt in flight: pulses its comet head, spins the orbiting sparks that draw its spiral trails,
    // flickers its light and sheds sparkle motes. Destroyed with the projectile it rides.
    public sealed class ShotFlight : MonoBehaviour
    {
        private const float MoteInterval = 0.045f;
        private ArtStyleLibrary style;
        private Color tint;
        private Transform head, orbit;
        private Light glow;
        private float size, glowPeak, seed, moteTimer, spin;

        public void Configure(ArtStyleLibrary library, Color color, Transform comet, Transform orbitPivot, Light light)
        {
            style = library;
            tint = color;
            head = comet;
            orbit = orbitPivot;
            glow = light;
            size = head != null ? head.localScale.x : 1f;
            glowPeak = light != null ? light.intensity : 0f;
            seed = Random.value * 10f;
        }

        private void Update()
        {
            float t = Time.time * 21f + seed;
            float pulse = 1f + 0.12f * Mathf.Sin(t) + 0.06f * Mathf.Sin(t * 2.7f);
            if (head != null) head.localScale = Vector3.one * (size * pulse);
            if (orbit != null)
            {
                spin += Time.deltaTime * 840f;
                orbit.localRotation = Quaternion.Euler(0f, 0f, spin);
            }
            if (glow != null) glow.intensity = glowPeak * (0.85f + 0.3f * (pulse - 1f) / 0.18f);
            moteTimer -= Time.deltaTime;
            if (moteTimer > 0f || style == null) return;
            moteTimer = MoteInterval;
            CombatEffects.Trail(style, transform.position, tint);
        }
    }
}

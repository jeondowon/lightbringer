using UnityEngine;
using UnityEngine.Rendering;

namespace Lightbringer.Visuals
{
    // The hero's staff slam: a golden wave that runs out exactly to the aura radius (the Shockwave's reach), a low
    // light wall riding its front and a flash along the aura edge when it arrives. Presentation only.
    public sealed class HeroShockwaveVfx : MonoBehaviour
    {
        private const float Expand = 0.32f, Lifetime = 0.8f;
        // Overall strength of the additive wave and its light; the shape and timing stay the same.
        private const float Brightness = 0.55f, LightBrightness = 0.4f;
        private static readonly int ModeId = Shader.PropertyToID("_Mode");
        private static readonly int AgeId = Shader.PropertyToID("_Age");
        private static readonly int WaveId = Shader.PropertyToID("_Wave");
        private static readonly int FadeId = Shader.PropertyToID("_Fade");
        private static readonly int RadiusId = Shader.PropertyToID("_Radius");
        private static readonly int EchoId = Shader.PropertyToID("_EchoStrength");
        private static readonly int WaveColorId = Shader.PropertyToID("_WaveColor");
        private static readonly int CoreColorId = Shader.PropertyToID("_CoreColor");
        private static readonly int AccentColorId = Shader.PropertyToID("_AccentColor");
        private static Material material;
        private MaterialPropertyBlock properties;
        private MeshRenderer ground, wall;
        private Light flash;
        private float radius, age;
        private bool healing;

        public static HeroShockwaveVfx Spawn(Vector3 ground, float radius, Transform parent, bool healing = false)
        {
            if (material == null)
            {
                Shader shader = UnityEngine.Resources.Load<Shader>("HeroShockwave");
                if (shader == null) return null;
                material = new Material(shader) { name = "LB Hero Shockwave", hideFlags = HideFlags.DontSave };
            }
            var root = new GameObject(healing ? "Hero Healing Wave (VFX)" : "Hero Shockwave (VFX)");
            root.transform.SetParent(parent, false);
            root.transform.position = ground;
            HeroShockwaveVfx effect = root.AddComponent<HeroShockwaveVfx>();
            effect.radius = Mathf.Max(0.5f, radius);
            effect.healing = healing;
            effect.Build();
            effect.Tick();
            return effect;
        }

        private void Build()
        {
            properties = new MaterialPropertyBlock();
            ground = Surface("Ground Wave", ProceduralMeshes.GroundQuad);
            ground.transform.localPosition = Vector3.up * 0.08f;
            // r = 1 in the shader is the aura edge; the quad leaves a little room for the ragged front.
            ground.transform.localScale = new Vector3(radius * 2.2f, 1f, radius * 2.2f);
            wall = Surface("Light Wall", ProceduralMeshes.Frustum(1f, 48));
            flash = new GameObject("Slam Light").AddComponent<Light>();
            flash.transform.SetParent(transform, false);
            flash.transform.localPosition = Vector3.up * 1.2f;
            flash.color = healing ? new Color(0.25f, 1f, 0.4f) : new Color(1f, 0.82f, 0.5f);
            flash.range = radius * 2f + 2f;
            flash.shadows = LightShadows.None;
        }

        private MeshRenderer Surface(string label, Mesh mesh)
        {
            var item = new GameObject(label);
            item.transform.SetParent(transform, false);
            item.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = item.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (age >= Lifetime) { Destroy(gameObject); return; }
            Tick();
        }

        private void Tick()
        {
            float wave = 1f - Mathf.Pow(1f - Mathf.Clamp01(age / Expand), 2.5f);
            float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(Expand, Lifetime, age));
            Apply(ground, 0, wave, fade);
            float reach = Mathf.Max(0.05f, wave) * radius;
            float wallFade = (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(Expand * 0.7f, Expand * 1.4f, age))) * 0.9f;
            wall.transform.localPosition = Vector3.up * 0.04f;
            wall.transform.localScale = new Vector3(reach * 2f, 0.7f + radius * 0.12f, reach * 2f);
            Apply(wall, 1, wave, wallFade);
            flash.intensity = (6f * Mathf.Exp(-age * 7f) + 2f * Mathf.Exp(-Mathf.Pow((age - Expand) * 9f, 2f))) * LightBrightness;
        }

        private void Apply(MeshRenderer renderer, int mode, float wave, float fade)
        {
            properties.SetFloat(ModeId, mode);
            properties.SetFloat(AgeId, age);
            properties.SetFloat(WaveId, wave);
            properties.SetFloat(FadeId, fade);
            properties.SetFloat(RadiusId, radius);
            properties.SetFloat(EchoId, healing ? 0f : 1f);
            // Per-instance colors let healing and the golden slam overlap without changing each other.
            properties.SetColor(WaveColorId, (healing ? new Color(0.2f, 2.2f, 0.5f) : new Color(2.2f, 1.55f, 0.6f)) * Brightness);
            properties.SetColor(CoreColorId, (healing ? new Color(0.8f, 2.6f, 1f) : new Color(2.6f, 2.4f, 1.9f)) * Brightness);
            properties.SetColor(AccentColorId, (healing ? new Color(0.15f, 1.8f, 0.65f) : new Color(0.45f, 0.8f, 1.8f)) * Brightness);
            renderer.SetPropertyBlock(properties);
        }
    }
}

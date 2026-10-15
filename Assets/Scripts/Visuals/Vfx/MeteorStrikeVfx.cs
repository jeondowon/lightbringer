using UnityEngine;
using UnityEngine.Rendering;

namespace Lightbringer.Visuals
{
    // Presentation only: the warning, the falling meteor and the impact dome for the Meteor spell.
    // Damage timing lives in HeroAbilities; this effect is spawned with the same delay and lands with it.
    // Red / orange / yellow palette. Warning = ground aurora + fire curtain, fall = molten body with a tail.
    public sealed class MeteorStrikeVfx : MonoBehaviour
    {
        private const float CurtainHeight = 11f;
        private const float AfterImpact = 1.3f;
        private static readonly Vector3 SkyOffset = new Vector3(-16f, 62f, -9f);
        private static readonly Color FlameLight = new Color(1f, 0.55f, 0.18f);

        private static readonly int ModeId = Shader.PropertyToID("_Mode");
        private static readonly int AgeId = Shader.PropertyToID("_Age");
        private static readonly int ProgressId = Shader.PropertyToID("_Progress");
        private static readonly int FadeId = Shader.PropertyToID("_Fade");
        private static readonly int BurstId = Shader.PropertyToID("_Burst");

        private ArtStyleLibrary style;
        private float radius, delay, startedAt;
        private Vector3 point, start;
        private MaterialPropertyBlock properties;
        private MeshRenderer ground, curtain, body, corona, tail, dome;
        private Light flameLight;
        private bool landed, roared;

        public static MeteorStrikeVfx Spawn(ArtStyleLibrary style, Vector3 point, float radius, float delay, Transform parent)
        {
            if (style == null || style.meteorMaterial == null) return null;
            GameObject root = new GameObject("Meteor Strike (VFX)");
            root.transform.SetParent(parent, false);
            root.transform.position = point;
            MeteorStrikeVfx effect = root.AddComponent<MeteorStrikeVfx>();
            effect.style = style;
            effect.point = point;
            effect.radius = radius;
            effect.delay = Mathf.Max(0.1f, delay);
            effect.startedAt = Time.time;
            effect.start = point + SkyOffset;
            effect.Build();
            effect.Tick();
            return effect;
        }

        private void Build()
        {
            properties = new MaterialPropertyBlock();
            Material material = style.meteorMaterial;
            float diameter = radius * 2f;
            ground = Surface("Ground Aurora", ProceduralMeshes.GroundQuad, material);
            ground.transform.position = point + Vector3.up * 0.08f;
            ground.transform.localScale = new Vector3(diameter / 0.89f, 1f, diameter / 0.89f);
            curtain = Surface("Fire Curtain", ProceduralMeshes.Frustum(1f, 48), material);
            curtain.transform.position = point + Vector3.up * 0.05f;
            curtain.transform.localScale = new Vector3(diameter * 1.02f, CurtainHeight, diameter * 1.02f);
            body = Surface("Meteor Body", ProceduralMeshes.Sphere, material);
            body.transform.localScale = Vector3.one * 3.4f;
            corona = Surface("Meteor Corona", ProceduralMeshes.Sphere, material);
            corona.transform.localScale = Vector3.one * 8.5f;
            tail = Surface("Meteor Tail", ProceduralMeshes.Frustum(0f, 24), material);
            dome = Surface("Impact Dome", ProceduralMeshes.Sphere, material);
            dome.gameObject.SetActive(false);
            flameLight = new GameObject("Meteor Light").AddComponent<Light>();
            flameLight.transform.SetParent(transform, false);
            flameLight.color = FlameLight;
            flameLight.range = radius * 2.5f;
            flameLight.shadows = LightShadows.None;
            flameLight.intensity = 0f;
        }

        private MeshRenderer Surface(string label, Mesh mesh, Material material)
        {
            GameObject item = new GameObject(label);
            item.transform.SetParent(transform, false);
            item.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = item.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        private void Apply(MeshRenderer renderer, int mode, float age, float progress, float fade, float burst)
        {
            properties.SetFloat(ModeId, mode);
            properties.SetFloat(AgeId, age);
            properties.SetFloat(ProgressId, progress);
            properties.SetFloat(FadeId, fade);
            properties.SetFloat(BurstId, burst);
            renderer.SetPropertyBlock(properties);
        }

        private void Update() => Tick();

        private void Tick()
        {
            float age = Time.time - startedAt;
            float progress = Mathf.Clamp01(age / delay);
            float since = age - delay;
            // Fades in over the first moments so the mark does not pop in.
            float appear = Mathf.SmoothStep(0f, 1f, age / 0.35f);
            float burn = since < 0f ? 1f : Mathf.Clamp01(1f - since / AfterImpact);
            burn *= burn;
            if (since >= AfterImpact) { Destroy(gameObject); return; }

            Apply(ground, 0, age, progress, appear * (since < 0f ? 1f : burn), since);
            Apply(curtain, 1, age, progress, appear * (since < 0f ? 1f : Mathf.Max(burn, 0.001f)), since);

            bool falling = since < 0f;
            body.gameObject.SetActive(falling);
            corona.gameObject.SetActive(falling);
            tail.gameObject.SetActive(falling);
            if (falling)
            {
                // Accelerating fall along a slanted line into the mark.
                float t = progress * progress;
                Vector3 position = Vector3.Lerp(start, point + Vector3.up * 0.4f, t);
                Vector3 direction = (point - start).normalized;
                body.transform.position = position;
                corona.transform.position = position;
                float speedLook = Mathf.Lerp(0.5f, 1f, progress);
                float length = Mathf.Lerp(7f, 20f, progress) * speedLook;
                tail.transform.position = position;
                tail.transform.rotation = Quaternion.FromToRotation(Vector3.up, -direction);
                tail.transform.localScale = new Vector3(3.2f, length, 3.2f);
                Apply(body, 2, age, progress, 1f, -1f);
                Apply(corona, 4, age, progress, 0.9f, -1f);
                Apply(tail, 3, age, progress, 1f, -1f);
                flameLight.transform.position = position;
                flameLight.intensity = Mathf.Lerp(2f, 40f, progress);
                CombatEffects.Fireball(style, position, FlameLight, 3.2f);
                // The roar of the falling fire, timed so it swells into the impact (heard at the mark: the meteor
                // itself starts beyond hearing range).
                if (!roared && delay - age <= 1.1f)
                {
                    roared = true;
                    Lightbringer.Audio.SoundEffects.World(Lightbringer.Audio.SoundCue.FireBreath, point);
                }
            }
            else
            {
                if (!landed) Land();
                float grow = Mathf.Clamp01(since / 0.5f);
                grow = 1f - (1f - grow) * (1f - grow);
                dome.transform.localScale = Vector3.one * Mathf.Lerp(1f, radius * 2.3f, grow);
                Apply(dome, 5, age, progress, 1f, since);
                flameLight.transform.position = point + Vector3.up * 2f;
                flameLight.intensity = 55f * Mathf.Exp(-since * 3.5f);
            }
        }

        private void Land()
        {
            landed = true;
            dome.gameObject.SetActive(true);
            dome.transform.position = point + Vector3.up * 0.2f;
            dome.transform.localScale = Vector3.one;
        }
    }
}

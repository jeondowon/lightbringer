using UnityEngine;
using UnityEngine.Rendering;

namespace Lightbringer.Visuals
{
    // Shared, pooled particle bursts for combat feedback: weapon sparks, magic bursts, ground shock rings and
    // death motes. Four world-space particle systems serve every unit, so a crowded battle costs a few draw
    // calls instead of one effect object per hit. Play mode only; validations never create it.
    public static class CombatEffects
    {
        private static GameObject host;
        private static ParticleSystem sparks, flashes, motes, rings;
        private static Material glow;

        public static readonly Color SteelSpark = new Color(1f, 0.86f, 0.55f);
        public static readonly Color AlliedLight = new Color(0.6f, 0.82f, 1f);
        public static readonly Color HolyGold = new Color(1f, 0.82f, 0.45f);
        public static readonly Color Corruption = new Color(1f, 0.2f, 0.42f);

        private static bool Ready(ArtStyleLibrary style)
        {
            if (!Application.isPlaying || style == null || style.auraMotesMaterial == null) return false;
            if (host != null) return true;
            if (glow != null) Object.Destroy(glow);
            host = new GameObject("Combat Effects (VFX)");
            glow = new Material(style.auraMotesMaterial) { name = "LB Combat Glow", hideFlags = HideFlags.DontSave };
            glow.SetColor("_Color", new Color(1.5f, 1.5f, 1.5f));
            sparks = Create("Sparks", ParticleSystemRenderMode.Stretch, 1.6f, 800);
            flashes = Create("Flashes", ParticleSystemRenderMode.Billboard, 0f, 200);
            motes = Create("Motes", ParticleSystemRenderMode.Billboard, -0.25f, 800);
            rings = Create("Rings", ParticleSystemRenderMode.HorizontalBillboard, 0f, 100);
            Grow(flashes, 0.7f, 1.25f);
            Grow(rings, 0.25f, 1f);
            Grow(motes, 1f, 0.2f);
            return true;
        }

        // Melee or arrow contact: a short, directional spray of streaks plus a bright flash at the contact point.
        public static void Hit(ArtStyleLibrary style, Vector3 point, Vector3 direction, Color color, float intensity)
        {
            if (!Ready(style)) return;
            intensity = Mathf.Clamp(intensity, 0.3f, 2.5f);
            Flash(point, color, 0.55f * Mathf.Sqrt(intensity), 0.1f);
            Vector3 forward = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.up;
            int count = Mathf.RoundToInt(5 + 5 * intensity);
            for (int i = 0; i < count; i++)
            {
                Vector3 spread = (forward + Random.insideUnitSphere * 0.9f + Vector3.up * 0.35f).normalized;
                Emit(sparks, point, spread * Random.Range(3f, 7.5f) * Mathf.Lerp(0.8f, 1.2f, intensity * 0.4f),
                    Color.Lerp(color, Color.white, Random.value * 0.5f), Random.Range(0.04f, 0.08f), Random.Range(0.12f, 0.3f));
            }
        }

        // Spell impact: a soft bloom of light with slow motes, and a ground ring when the spell has splash.
        public static void Burst(ArtStyleLibrary style, Vector3 point, Color color, float radius)
        {
            if (!Ready(style)) return;
            Flash(point, color, 0.9f + radius * 0.25f, 0.18f);
            for (int i = 0; i < 14; i++)
                Emit(motes, point + Random.insideUnitSphere * 0.2f, Random.insideUnitSphere * 2.6f + Vector3.up * 0.8f,
                    Color.Lerp(color, Color.white, Random.value * 0.35f), Random.Range(0.08f, 0.16f), Random.Range(0.35f, 0.7f));
            for (int i = 0; i < 6; i++)
                Emit(sparks, point, (Random.onUnitSphere + Vector3.up * 0.5f).normalized * Random.Range(2.5f, 5f),
                    color, Random.Range(0.035f, 0.06f), Random.Range(0.15f, 0.3f));
            if (radius > 0.5f) Ring(point, color, radius);
        }

        // Expanding shock ring on the ground (slams, charges, splash spells).
        public static void Ring(Vector3 point, Color color, float radius)
        {
            if (host == null) return;
            Vector3 ground = HeroSpellVfx.GroundBelow(point) + Vector3.up * 0.06f;
            Emit(rings, ground, Vector3.zero, color * 0.8f, radius * 2.2f, 0.32f);
            for (int i = 0; i < 10; i++)
            {
                Vector3 outward = Quaternion.Euler(0f, i * 36f + Random.Range(-12f, 12f), 0f) * Vector3.forward;
                Emit(sparks, ground + Vector3.up * 0.1f, (outward * Random.Range(2.5f, 4.5f) + Vector3.up * Random.Range(1f, 2.5f)),
                    color, Random.Range(0.04f, 0.07f), Random.Range(0.2f, 0.4f));
            }
        }

        // Fallen units dissolve into rising motes: light for allies, corruption for enemies.
        public static void Death(ArtStyleLibrary style, Vector3 position, Color color, float size)
        {
            if (!Ready(style)) return;
            size = Mathf.Clamp(size, 0.5f, 3f);
            Flash(position, color, 0.8f * size, 0.16f);
            int count = Mathf.RoundToInt(12 * size);
            for (int i = 0; i < count; i++)
            {
                Vector3 offset = Vector3.Scale(Random.insideUnitSphere, new Vector3(0.35f, 0.7f, 0.35f)) * size;
                Emit(motes, position + offset, Vector3.up * Random.Range(1f, 2.6f) + Random.insideUnitSphere * 0.6f,
                    Color.Lerp(color, Color.white, Random.value * 0.3f), Random.Range(0.07f, 0.15f) * Mathf.Sqrt(size),
                    Random.Range(0.6f, 1.1f));
            }
        }

        // Small glow at a staff tip or bow as a shot is loosed.
        public static void Muzzle(ArtStyleLibrary style, Vector3 point, Color color)
        {
            if (!Ready(style)) return;
            Flash(point, color, 0.45f, 0.08f);
        }

        private static void Flash(Vector3 point, Color color, float size, float life) =>
            Emit(flashes, point, Vector3.zero, Color.Lerp(color, Color.white, 0.35f), size, life);

        private static void Emit(ParticleSystem system, Vector3 position, Vector3 velocity, Color color, float size, float life)
        {
            ParticleSystem.EmitParams settings = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = velocity,
                startColor = color,
                startSize = size,
                startLifetime = life,
            };
            system.Emit(settings, 1);
        }

        private static ParticleSystem Create(string name, ParticleSystemRenderMode mode, float gravity, int capacity)
        {
            GameObject item = new GameObject(name);
            item.SetActive(false);
            item.transform.SetParent(host.transform, false);
            ParticleSystem system = item.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = system.main;
            // Always playing with emission off: bursts come only from Emit calls.
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = gravity;
            main.maxParticles = capacity;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;
            // Every particle fades out over its life; additive glow treats alpha as brightness.
            ParticleSystem.ColorOverLifetimeModule colour = system.colorOverLifetime;
            colour.enabled = true;
            Gradient fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.4f), new GradientAlphaKey(0f, 1f) });
            colour.color = fade;
            if (mode != ParticleSystemRenderMode.Stretch)
            {
                ParticleSystem.LimitVelocityOverLifetimeModule drag = system.limitVelocityOverLifetime;
                drag.enabled = true;
                drag.drag = 2.5f;
            }
            ParticleSystemRenderer renderer = item.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = mode;
            if (mode == ParticleSystemRenderMode.Stretch)
            {
                renderer.velocityScale = 0.06f;
                renderer.lengthScale = 1.5f;
            }
            renderer.sharedMaterial = glow;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            item.SetActive(true);
            system.Play();
            return system;
        }

        private static void Grow(ParticleSystem system, float from, float to)
        {
            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, from, 1f, to));
        }
    }
}

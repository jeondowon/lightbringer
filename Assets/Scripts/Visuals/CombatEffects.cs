using UnityEngine;
using UnityEngine.Rendering;

namespace Lightbringer.Visuals
{
    // Shared, pooled particle bursts for combat feedback: weapon sparks and slashes, star flares, magic bursts,
    // ground shock rings, rune marks, dust and death motes. A handful of world-space particle systems serve
    // every unit, so a crowded battle costs a few draw calls instead of one effect object per hit.
    // Shapes come from the procedural VFX Shape shader (falls back to soft glow dots without it).
    // Play mode only; validations never create it.
    public static class CombatEffects
    {
        private static GameObject host;
        private static ParticleSystem sparks, flashes, flares, slashes, motes, rings, runes, dust;
        private static ParticleSystem impactWaves, healingGlow, healingMotes;
        private static readonly System.Collections.Generic.List<Material> materials = new System.Collections.Generic.List<Material>();

        public static readonly Color SteelSpark = new Color(1f, 0.86f, 0.55f);
        public static readonly Color AlliedLight = new Color(0.6f, 0.82f, 1f);
        public static readonly Color HolyGold = new Color(1f, 0.82f, 0.45f);
        public static readonly Color Corruption = new Color(1f, 0.2f, 0.42f);
        public static readonly Color HealLight = new Color(0.25f, 1f, 0.48f);
        private static readonly Color DustColor = new Color(0.62f, 0.55f, 0.45f, 1f);

        private enum Sprite { Dot = 0, Star = 1, Ring = 2, Slash = 3, RuneRing = 4, Flame = 5, Puff = 6 }

        private static bool Ready(ArtStyleLibrary style)
        {
            if (!Application.isPlaying || style == null || (style.vfxMaterial == null && style.auraMotesMaterial == null)) return false;
            if (host != null) return true;
            foreach (Material material in materials) if (material != null) Object.Destroy(material);
            materials.Clear();
            host = new GameObject("Combat Effects (VFX)");
            sparks = Create("Sparks", ParticleSystemRenderMode.Stretch, 1.6f, 1000, Make(style, Sprite.Dot, 1.5f, 2.2f));
            flashes = Create("Flashes", ParticleSystemRenderMode.Billboard, 0f, 250, Make(style, Sprite.Dot, 1.5f, 1.6f));
            flares = Create("Flares", ParticleSystemRenderMode.Billboard, 0f, 250, Make(style, Sprite.Star, 1.6f));
            slashes = Create("Slashes", ParticleSystemRenderMode.Billboard, 0f, 200, Make(style, Sprite.Slash, 1.4f));
            motes = Create("Motes", ParticleSystemRenderMode.Billboard, -0.25f, 1200, Make(style, Sprite.Dot, 1.5f, 1.8f));
            rings = Create("Rings", ParticleSystemRenderMode.HorizontalBillboard, 0f, 120, Make(style, Sprite.Ring, 1.4f));
            runes = Create("Runes", ParticleSystemRenderMode.HorizontalBillboard, 0f, 200, Make(style, Sprite.RuneRing, 1.3f));
            impactWaves = Create("Impact Ripples", ParticleSystemRenderMode.Billboard, 0f, 120, Make(style, Sprite.Ring, 1.1f));
            healingGlow = Create("Healing Light", ParticleSystemRenderMode.Billboard, 0f, 180, Make(style, Sprite.Dot, 1.2f, 2.8f), soft: true);
            healingMotes = Create("Healing Motes", ParticleSystemRenderMode.Billboard, -0.025f, 500, Make(style, Sprite.Dot, 1.4f, 2f), soft: true);
            Material puff = Make(style, Sprite.Puff, 1f, 1.4f, additive: false);
            if (puff != null) dust = Create("Dust", ParticleSystemRenderMode.Billboard, -0.05f, 300, puff, soft: true);
            Grow(flashes, 0.7f, 1.25f);
            Grow(flares, 1f, 0.35f);
            Grow(slashes, 0.8f, 1.15f);
            Grow(rings, 0.25f, 1f);
            Grow(runes, 0.6f, 1f);
            Grow(motes, 1f, 0.2f);
            Grow(impactWaves, 0.2f, 1f);
            Grow(healingGlow, 0.6f, 1.35f);
            Grow(healingMotes, 0.7f, 0.15f);
            if (dust != null) Grow(dust, 0.5f, 1.4f);
            Spin(runes, 40f);
            Spin(dust, 25f);
            return true;
        }

        // Melee or arrow contact: a short, directional spray of streaks, a bright flash and a star flare at the
        // contact point. Melee blows also draw a slash crescent across the target.
        public static void Hit(ArtStyleLibrary style, Vector3 point, Vector3 direction, Color color, float intensity, bool melee = false)
        {
            if (!Ready(style)) return;
            intensity = Mathf.Clamp(intensity, 0.3f, 2.5f);
            float scale = Mathf.Sqrt(intensity);
            Flash(point, color, 0.55f * scale, 0.1f);
            Emit(flares, point, Vector3.zero, Color.Lerp(color, Color.white, 0.4f), 0.9f * scale, 0.14f, Random.Range(0f, 90f));
            Vector3 forward = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.up;
            if (melee)
            {
                // Follow the projected attack direction instead of rotating every contact randomly.
                UnityEngine.Camera view = UnityEngine.Camera.main;
                Vector3 screenDirection = view != null ? view.transform.InverseTransformDirection(forward) : forward;
                float angle = -Mathf.Atan2(screenDirection.y, screenDirection.x) * Mathf.Rad2Deg + 65f;
                float length = Mathf.Lerp(1.05f, 1.7f, intensity / 2.5f);
                Emit(slashes, point, forward * 0.65f, color, length, 0.19f, angle);
                Emit(slashes, point + forward * 0.06f, forward * 0.9f, Color.Lerp(color, Color.white, 0.75f),
                    length * 0.78f, 0.095f, angle + 6f);
            }
            int count = Mathf.RoundToInt(5 + 4 * intensity);
            for (int i = 0; i < count; i++)
            {
                Vector3 spread = (forward + Random.insideUnitSphere * 0.9f + Vector3.up * 0.35f).normalized;
                Emit(sparks, point, spread * Random.Range(3f, 7.5f) * Mathf.Lerp(0.8f, 1.2f, intensity * 0.4f),
                    Color.Lerp(color, Color.white, Random.value * 0.5f), Random.Range(0.04f, 0.08f), Random.Range(0.12f, 0.3f));
            }
        }

        // Spell impact: a bloom of light with a star flare, a shock ring, slow motes and embers.
        public static void Burst(ArtStyleLibrary style, Vector3 point, Color color, float radius)
        {
            if (!Ready(style)) return;
            radius = Mathf.Clamp(radius, 0f, 6f);
            Flash(point, color, 1.1f + radius * 0.3f, 0.2f);
            Emit(impactWaves, point, Vector3.zero, color * 0.65f, 1.4f + radius * 0.3f, 0.28f);
            Emit(flares, point, Vector3.zero, Color.Lerp(color, Color.white, 0.5f), 1.8f + radius * 0.4f, 0.22f, Random.Range(0f, 90f));
            for (int i = 0; i < 18; i++)
                Emit(motes, point + Random.insideUnitSphere * 0.25f, Random.insideUnitSphere * 3f + Vector3.up * 1f,
                    Color.Lerp(color, Color.white, Random.value * 0.35f), Random.Range(0.08f, 0.18f), Random.Range(0.4f, 0.8f));
            for (int i = 0; i < 10; i++)
                Emit(sparks, point, (Random.onUnitSphere + Vector3.up * 0.5f).normalized * Random.Range(2.5f, 6f),
                    color, Random.Range(0.035f, 0.065f), Random.Range(0.15f, 0.35f));
            Ring(point, color, Mathf.Max(radius, 1.1f));
        }

        // Corrupted bolts add torn arcs. Light bolts skip ground runes, which read as the hero aura.
        public static void SpellImpact(ArtStyleLibrary style, Vector3 point, Color color, float radius, bool allied)
        {
            Burst(style, point, color, radius);
            if (host == null || allied) return;
            for (int i = 0; i < 3; i++)
                Emit(slashes, point, Quaternion.Euler(0f, i * 120f, 0f) * Vector3.forward * 0.8f,
                    color * 0.7f, 0.95f, 0.24f, i * 120f + 20f);
        }

        // Expanding shock ring on the ground (slams, charges, splash spells), with a low dust skirt for big ones.
        public static void Ring(Vector3 point, Color color, float radius)
        {
            if (host == null) return;
            Vector3 ground = HeroSpellVfx.GroundBelow(point) + Vector3.up * 0.06f;
            Emit(rings, ground, Vector3.zero, color, radius * 2.3f, 0.36f);
            Emit(rings, ground + Vector3.up * 0.01f, Vector3.zero, Color.Lerp(color, Color.white, 0.5f) * 0.6f, radius * 1.4f, 0.22f);
            for (int i = 0; i < 12; i++)
            {
                Vector3 outward = Quaternion.Euler(0f, i * 30f + Random.Range(-12f, 12f), 0f) * Vector3.forward;
                Emit(sparks, ground + Vector3.up * 0.1f, (outward * Random.Range(2.5f, 4.5f) + Vector3.up * Random.Range(1f, 2.5f)),
                    color, Random.Range(0.04f, 0.07f), Random.Range(0.2f, 0.4f));
            }
            if (radius >= 1.5f) DustRing(ground, radius * 0.7f, Mathf.RoundToInt(Mathf.Clamp(radius * 4f, 6f, 16f)));
        }

        // Fallen units dissolve into rising motes (light for allies, corruption for enemies) over a dust puff.
        public static void Death(ArtStyleLibrary style, Vector3 position, Color color, float size)
        {
            if (!Ready(style)) return;
            size = Mathf.Clamp(size, 0.5f, 3f);
            Flash(position, color, 0.9f * size, 0.18f);
            Emit(flares, position, Vector3.zero, Color.Lerp(color, Color.white, 0.3f), 1.4f * size, 0.2f, Random.Range(0f, 90f));
            int count = Mathf.RoundToInt(16 * size);
            for (int i = 0; i < count; i++)
            {
                Vector3 offset = Vector3.Scale(Random.insideUnitSphere, new Vector3(0.35f, 0.7f, 0.35f)) * size;
                Emit(motes, position + offset, Vector3.up * Random.Range(1f, 2.8f) + Random.insideUnitSphere * 0.6f,
                    Color.Lerp(color, Color.white, Random.value * 0.3f), Random.Range(0.07f, 0.16f) * Mathf.Sqrt(size),
                    Random.Range(0.6f, 1.2f));
            }
            DustRing(HeroSpellVfx.GroundBelow(position) + Vector3.up * 0.2f, 0.4f * size, Mathf.RoundToInt(4 * size));
        }

        // Small glow at a staff tip or bow as a shot is loosed.
        public static void Muzzle(ArtStyleLibrary style, Vector3 point, Color color)
        {
            if (!Ready(style)) return;
            Flash(point, color, 0.5f, 0.09f);
            Emit(flares, point, Vector3.zero, Color.Lerp(color, Color.white, 0.4f), 0.7f, 0.1f, Random.Range(0f, 90f));
        }

        // Where an arrow lands: a crisp star glint and a tight ripple (the struck unit adds its own sparks), or a
        // spit of sparks and dirt when it buries itself in the ground.
        public static void ArrowImpact(ArtStyleLibrary style, Vector3 point, Vector3 direction, Color color, bool grounded)
        {
            if (!Ready(style)) return;
            Emit(flares, point, Vector3.zero, Color.Lerp(color, Color.white, 0.45f), 0.95f, 0.13f, Random.Range(0f, 90f));
            Emit(impactWaves, point, Vector3.zero, color * 0.55f, 0.7f, 0.16f);
            if (!grounded) return;
            Vector3 back = direction.sqrMagnitude > 0.0001f ? -direction.normalized : Vector3.up;
            for (int i = 0; i < 5; i++)
                Emit(sparks, point, (back + Random.insideUnitSphere * 0.7f + Vector3.up * 0.8f).normalized * Random.Range(2f, 4.5f),
                    color, Random.Range(0.03f, 0.055f), Random.Range(0.12f, 0.25f));
            DustRing(point, 0.15f, 3);
        }

        // Dark smoke curling up from a corrupted impact (alpha-blended, so it can shade the ground under the fire).
        public static void Smoke(Vector3 ground, float radius, int count)
        {
            if (host == null || dust == null) return;
            for (int i = 0; i < count; i++)
            {
                Vector2 offset = Random.insideUnitCircle * radius;
                Color tint = Color.Lerp(new Color(0.09f, 0.03f, 0.1f), new Color(0.22f, 0.06f, 0.14f), Random.value);
                tint.a = Random.Range(0.45f, 0.7f);
                Emit(dust, ground + new Vector3(offset.x, Random.Range(0.2f, 0.6f), offset.y),
                    new Vector3(offset.x, 0f, offset.y) * 1.2f + Vector3.up * Random.Range(1f, 1.9f),
                    tint, Random.Range(0.9f, 1.5f), Random.Range(0.7f, 1.1f), Random.Range(0f, 360f));
            }
        }

        // A magic bolt's sparkle trail: called every few frames while it flies.
        public static void Trail(ArtStyleLibrary style, Vector3 point, Color color)
        {
            if (!Ready(style)) return;
            Emit(motes, point + Random.insideUnitSphere * 0.08f, Random.insideUnitSphere * 0.5f + Vector3.up * 0.3f,
                Color.Lerp(color, Color.white, Random.value * 0.4f), Random.Range(0.06f, 0.12f), Random.Range(0.25f, 0.45f));
        }

        // A troop stepping into the hero aura: a rune mark flashes at its feet with a short rise of light.
        public static void Blessing(ArtStyleLibrary style, Vector3 feet, Color color)
        {
            if (!Ready(style)) return;
            Emit(runes, feet + Vector3.up * 0.05f, Vector3.zero, color, 2.4f, 0.55f, Random.Range(0f, 360f));
            Emit(flares, feet + Vector3.up * 1.1f, Vector3.zero, Color.Lerp(color, Color.white, 0.5f), 1.3f, 0.2f, Random.Range(0f, 90f));
            for (int i = 0; i < 8; i++)
            {
                Vector3 around = Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward * 0.45f;
                Emit(motes, feet + around + Vector3.up * 0.1f, Vector3.up * Random.Range(1.6f, 2.6f),
                    Color.Lerp(color, Color.white, 0.3f), Random.Range(0.08f, 0.13f), Random.Range(0.5f, 0.8f));
            }
        }

        // One rising light mote around an empowered troop (trickled while it stays in the aura).
        public static void AuraMote(ArtStyleLibrary style, Vector3 feet, Color color, float spread)
        {
            if (!Ready(style)) return;
            Vector2 offset = Random.insideUnitCircle * spread;
            Emit(motes, feet + new Vector3(offset.x, Random.Range(0.06f, 0.2f), offset.y), Vector3.up * Random.Range(0.18f, 0.35f),
                Color.Lerp(color, Color.white, Random.value * 0.3f), Random.Range(0.025f, 0.06f), Random.Range(0.6f, 1f));
        }

        // Priests use only green light: soft overlapping blooms and rising motes, no ring or glyph shapes.
        public static void Heal(ArtStyleLibrary style, Vector3 feet, float radius, Color color, bool patterned = false)
        {
            if (!Ready(style)) return;
            radius = Mathf.Clamp(radius, 0.5f, 6f);
            Vector3 ground = feet + Vector3.up * 0.05f;
            if (patterned)
            {
                Emit(runes, ground, Vector3.zero, color * 0.65f, radius * 2.1f, 0.6f);
                Emit(rings, ground + Vector3.up * 0.01f, Vector3.zero, color * 0.5f, radius * 2.2f, 0.4f);
            }
            Color veil = color;
            veil.a = 0.22f;
            Emit(healingGlow, feet + Vector3.up * 0.65f, Vector3.up * 0.25f, veil, radius * 1.4f, 0.85f);
            Color core = Color.Lerp(color, new Color(0.7f, 1f, 0.8f), 0.4f);
            core.a = 0.7f;
            Emit(healingGlow, feet + Vector3.up * 1.15f, Vector3.up * 0.45f, core, 1.25f, 0.55f);
            for (int i = 0; i < 20; i++)
            {
                Vector2 offset = Random.insideUnitCircle * radius * 0.7f;
                Vector3 position = ground + new Vector3(offset.x, Random.Range(0.1f, 0.65f), offset.y);
                Emit(healingMotes, position, Vector3.up * Random.Range(0.65f, 1.6f),
                    Color.Lerp(color, core, Random.value * 0.5f), Random.Range(0.055f, 0.13f), Random.Range(0.65f, 1.15f));
                if (i < 5)
                    Emit(healingGlow, position, Vector3.up * 0.35f, veil, Random.Range(0.65f, 1.1f), Random.Range(0.65f, 1f));
            }
        }

        // Ground dust kicked up around a point (charges, slams, falls). Nothing without the VFX shader.
        public static void DustRing(Vector3 ground, float radius, int count)
        {
            if (host == null || dust == null) return;
            for (int i = 0; i < count; i++)
            {
                Vector3 outward = Quaternion.Euler(0f, i * 360f / Mathf.Max(1, count) + Random.Range(-15f, 15f), 0f) * Vector3.forward;
                Color tint = Color.Lerp(DustColor, DustColor * 1.2f, Random.value);
                tint.a = Random.Range(0.35f, 0.55f);
                Emit(dust, ground + outward * radius + Vector3.up * 0.15f, outward * Random.Range(1.2f, 2.4f) + Vector3.up * Random.Range(0.2f, 0.6f),
                    tint, Random.Range(0.8f, 1.4f), Random.Range(0.6f, 1f), Random.Range(0f, 360f));
            }
        }

        // Ground warning for a delayed area spell: a rune mark of the full area that stays for `life` seconds,
        // with a ring closing in on it from beyond the edge.
        public static void Warning(ArtStyleLibrary style, Vector3 ground, Color color, float radius, float life)
        {
            if (!Ready(style)) return;
            Vector3 flat = ground + Vector3.up * 0.07f;
            Color faint = color * 0.55f;
            Emit(runes, flat, Vector3.zero, faint, radius * 2f, life, Random.Range(0f, 360f));
            Emit(rings, flat + Vector3.up * 0.01f, Vector3.zero, color, radius * 2.2f, life);
        }

        // A falling fireball: call every frame at its position. Leaves a hot core and a trail of embers.
        public static void Fireball(ArtStyleLibrary style, Vector3 point, Color color, float size)
        {
            if (!Ready(style)) return;
            Flash(point, color, size, 0.08f);
            Emit(flares, point, Vector3.zero, Color.Lerp(color, Color.white, 0.6f), size * 0.8f, 0.07f, Random.Range(0f, 90f));
            for (int i = 0; i < 3; i++)
                Emit(motes, point + Random.insideUnitSphere * size * 0.25f, Random.insideUnitSphere * 1.2f + Vector3.up * 1.5f,
                    Color.Lerp(color, Color.white, Random.value * 0.3f), Random.Range(0.12f, 0.3f), Random.Range(0.4f, 0.8f));
        }

        private static void Flash(Vector3 point, Color color, float size, float life) =>
            Emit(flashes, point, Vector3.zero, Color.Lerp(color, Color.white, 0.35f), size, life);

        private static void Emit(ParticleSystem system, Vector3 position, Vector3 velocity, Color color, float size, float life, float rotation = 0f)
        {
            if (system == null) return;
            ParticleSystem.EmitParams settings = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = velocity,
                startColor = color,
                startSize = size,
                startLifetime = life,
                rotation = rotation,
            };
            system.Emit(settings, 1);
        }

        // One material per sprite shape; without the VFX Shape material every shape is the old soft glow dot.
        private static Material Make(ArtStyleLibrary style, Sprite shape, float brightness, float softness = 1.6f, bool additive = true)
        {
            Material material;
            if (style.vfxMaterial != null)
            {
                material = new Material(style.vfxMaterial);
                material.SetFloat("_Shape", (float)shape);
                material.SetFloat("_Additive", additive ? 1f : 0f);
                material.SetFloat("_Softness", softness);
                if (!additive) material.renderQueue = 2990; // dust sorts under the additive glow
            }
            else
            {
                if (!additive) return null;
                material = new Material(style.auraMotesMaterial);
            }
            material.name = "LB Combat " + shape;
            material.hideFlags = HideFlags.DontSave;
            material.SetColor("_Color", additive ? new Color(brightness, brightness, brightness) : new Color(1f, 1f, 1f, 1f));
            materials.Add(material);
            return material;
        }

        private static ParticleSystem Create(string name, ParticleSystemRenderMode mode, float gravity, int capacity, Material material, bool soft = false)
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
                soft ? new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) }
                     : new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.4f), new GradientAlphaKey(0f, 1f) });
            colour.color = fade;
            if (mode != ParticleSystemRenderMode.Stretch)
            {
                ParticleSystem.LimitVelocityOverLifetimeModule drag = system.limitVelocityOverLifetime;
                drag.enabled = true;
                drag.drag = soft ? 3.5f : 2.5f;
            }
            ParticleSystemRenderer renderer = item.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = mode;
            if (mode == ParticleSystemRenderMode.Stretch)
            {
                renderer.velocityScale = 0.06f;
                renderer.lengthScale = 1.5f;
            }
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            item.SetActive(true);
            system.Play();
            return system;
        }

        private static void Grow(ParticleSystem system, float from, float to)
        {
            if (system == null) return;
            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, from, 1f, to));
        }

        private static void Spin(ParticleSystem system, float degreesPerSecond)
        {
            if (system == null) return;
            ParticleSystem.RotationOverLifetimeModule rotation = system.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = degreesPerSecond * Mathf.Deg2Rad;
        }
    }
}

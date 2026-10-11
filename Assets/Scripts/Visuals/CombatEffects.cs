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
        private static readonly System.Collections.Generic.List<Material> materials = new System.Collections.Generic.List<Material>();

        public static readonly Color SteelSpark = new Color(1f, 0.86f, 0.55f);
        public static readonly Color AlliedLight = new Color(0.6f, 0.82f, 1f);
        public static readonly Color HolyGold = new Color(1f, 0.82f, 0.45f);
        public static readonly Color Corruption = new Color(1f, 0.2f, 0.42f);
        public static readonly Color HealLight = new Color(0.75f, 1f, 0.6f);
        private static readonly Color DustColor = new Color(0.62f, 0.55f, 0.45f, 1f);

        private enum Sprite { Dot = 0, Star = 1, Ring = 2, Slash = 3, RuneRing = 4, Flame = 5, Puff = 6 }

        private static bool Ready(ArtStyleLibrary style)
        {
            if (!Application.isPlaying || style == null || style.auraMotesMaterial == null) return false;
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
            Material puff = Make(style, Sprite.Puff, 1f, 1.4f, additive: false);
            if (puff != null) dust = Create("Dust", ParticleSystemRenderMode.Billboard, -0.05f, 300, puff, soft: true);
            Grow(flashes, 0.7f, 1.25f);
            Grow(flares, 1f, 0.35f);
            Grow(slashes, 0.8f, 1.15f);
            Grow(rings, 0.25f, 1f);
            Grow(runes, 0.6f, 1f);
            Grow(motes, 1f, 0.2f);
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
            if (melee)
                Emit(slashes, point + Vector3.up * 0.1f, Vector3.zero, Color.Lerp(color, Color.white, 0.25f),
                    Mathf.Lerp(1.1f, 1.9f, intensity / 2.5f), 0.16f, Random.Range(-70f, 70f) + (Random.value < 0.5f ? 0f : 180f));
            Vector3 forward = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.up;
            int count = Mathf.RoundToInt(6 + 6 * intensity);
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
            Flash(point, color, 1.1f + radius * 0.3f, 0.2f);
            Emit(flares, point, Vector3.zero, Color.Lerp(color, Color.white, 0.5f), 1.8f + radius * 0.4f, 0.22f, Random.Range(0f, 90f));
            for (int i = 0; i < 18; i++)
                Emit(motes, point + Random.insideUnitSphere * 0.25f, Random.insideUnitSphere * 3f + Vector3.up * 1f,
                    Color.Lerp(color, Color.white, Random.value * 0.35f), Random.Range(0.08f, 0.18f), Random.Range(0.4f, 0.8f));
            for (int i = 0; i < 10; i++)
                Emit(sparks, point, (Random.onUnitSphere + Vector3.up * 0.5f).normalized * Random.Range(2.5f, 6f),
                    color, Random.Range(0.035f, 0.065f), Random.Range(0.15f, 0.35f));
            Ring(point, color, Mathf.Max(radius, 1.1f));
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

        // Healing pulse: a rune circle across the heal radius, a soft ring and motes rising around the healer.
        public static void Heal(ArtStyleLibrary style, Vector3 feet, float radius, Color color)
        {
            if (!Ready(style)) return;
            Vector3 ground = feet + Vector3.up * 0.05f;
            Emit(runes, ground, Vector3.zero, color * 0.8f, radius * 2.1f, 0.7f, Random.Range(0f, 360f));
            Emit(rings, ground + Vector3.up * 0.01f, Vector3.zero, color * 0.7f, radius * 2.2f, 0.45f);
            Flash(feet + Vector3.up * 1.2f, color, 0.9f, 0.2f);
            for (int i = 0; i < 14; i++)
            {
                Vector2 offset = Random.insideUnitCircle * radius * 0.8f;
                Emit(motes, ground + new Vector3(offset.x, 0.1f, offset.y), Vector3.up * Random.Range(1.2f, 2.2f),
                    Color.Lerp(color, Color.white, Random.value * 0.4f), Random.Range(0.08f, 0.14f), Random.Range(0.5f, 0.9f));
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

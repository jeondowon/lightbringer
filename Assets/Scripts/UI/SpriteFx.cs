using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Lightbringer.UI
{
    // Shared painter for storybook illustration overlays (title, prologue).
    // Effects take positions in artwork pixels; everything is drawn as textured sprites built from soft
    // procedural gradients, batched per layer. Animation is a pure function of Time, so nothing touches
    // Unity's gameplay random state.
    internal sealed class SpriteFx
    {
        public static readonly Vector2 ArtworkSize = new Vector2(1672f, 941f);

        private sealed class Layer
        {
            public readonly Func<Texture2D> Texture;
            public readonly List<Vertex> Vertices = new List<Vertex>();
            public readonly List<ushort> Indices = new List<ushort>();
            public Layer(Func<Texture2D> texture) { Texture = texture; }
        }

        private static Texture2D glowTexture, dotTexture, raysTexture, starTexture, streakTexture, shaftTexture, smokeTexture;

        // Draw order: back to front.
        private readonly Layer fog = new Layer(() => smokeTexture);
        private readonly Layer back = new Layer(() => glowTexture);
        private readonly Layer dark = new Layer(() => smokeTexture);
        private readonly Layer fireGlow = new Layer(() => glowTexture);
        private readonly Layer halo = new Layer(() => glowTexture);
        private readonly Layer core = new Layer(() => dotTexture);
        private readonly Layer rays = new Layer(() => raysTexture);
        private readonly Layer shafts = new Layer(() => shaftTexture);
        private readonly Layer glow = new Layer(() => glowTexture);
        private readonly Layer star = new Layer(() => starTexture);
        private readonly Layer streak = new Layer(() => streakTexture);
        private readonly Layer frontHalo = new Layer(() => glowTexture);
        private readonly Layer frontCore = new Layer(() => dotTexture);
        private readonly Layer[] layers;

        private float viewHeight;

        public float Time { get; private set; }
        public float Scale { get; private set; }
        public Vector2 Offset { get; private set; }
        // Fraction of the view height below which effects soften so captions stay readable; negative disables it.
        public float CaptionFadeStart = -1f;

        public SpriteFx()
        {
            layers = new[] { fog, back, dark, fireGlow, halo, core, rays, shafts, glow, star, streak, frontHalo, frontCore };
        }

        // cover: true for scale-and-crop backgrounds, false for scale-to-fit images.
        public bool Begin(Rect rect, float time, bool cover)
        {
            float width = rect.width, height = rect.height;
            if (width <= 0f || height <= 0f || float.IsNaN(width) || float.IsNaN(height)) return false;
            EnsureTextures();
            Time = time;
            viewHeight = height;
            Scale = cover ? Mathf.Max(width / ArtworkSize.x, height / ArtworkSize.y) : Mathf.Min(width / ArtworkSize.x, height / ArtworkSize.y);
            Offset = (new Vector2(width, height) - ArtworkSize * Scale) * 0.5f;
            foreach (Layer layer in layers) { layer.Vertices.Clear(); layer.Indices.Clear(); }
            return true;
        }

        public void End(MeshGenerationContext context)
        {
            foreach (Layer layer in layers)
            {
                if (layer.Indices.Count == 0) continue;
                MeshWriteData mesh = context.Allocate(layer.Vertices.Count, layer.Indices.Count, layer.Texture());
                foreach (Vertex vertex in layer.Vertices) mesh.SetNextVertex(vertex);
                foreach (ushort index in layer.Indices) mesh.SetNextIndex(index);
            }
        }

        public Vector2 At(Vector2 artwork) => Offset + artwork * Scale;

        // ---- Shared rhythms ----

        public float Flicker(int seed) => 0.88f + 0.09f * (Mathf.PerlinNoise(Time * 2.4f, 0.37f + seed * 5.1f) * 2f - 1f)
            + 0.03f * Mathf.Sin(Time * 9.1f + seed);

        public float Breath(int seed) => 0.5f + 0.5f * Mathf.Sin(Time * 0.55f + seed * 1.9f);

        // ---- Light ----

        // A beacon or celestial light: atmosphere, two counter-rotating shaft layers, halo, hot core and lens glints.
        public void Beacon(Vector2 at, float size, float intensity, int seed)
        {
            Vector2 centre = At(at);
            float s = Scale * size, flicker = Flicker(seed) * intensity, breath = Breath(seed);

            Quad(back, centre, new Vector2(290f, 240f) * s * (0.95f + 0.05f * breath), 0f,
                new Color(1f, 0.5f, 0.18f, (0.12f + 0.04f * breath) * flicker));

            Fan(rays, centre, 400f * s, Time * 0.022f + seed, false, new Color(1f, 0.84f, 0.56f, (0.34f + 0.08f * breath) * flicker));
            Fan(rays, centre, 320f * s, 1.3f + seed - Time * 0.015f, true, new Color(1f, 0.7f, 0.36f, 0.26f * flicker));

            Quad(glow, centre, new Vector2(95f, 95f) * s * (0.96f + 0.04f * breath), 0f, new Color(1f, 0.66f, 0.28f, 0.4f * flicker));
            Quad(glow, centre, new Vector2(22f, 40f) * s, 0f, new Color(1f, 0.93f, 0.74f, 0.6f * flicker));
            Quad(glow, centre, new Vector2(10f, 19f) * s, 0f, new Color(1f, 0.98f, 0.9f, 0.55f * flicker));

            float glint = 0.75f + 0.25f * Mathf.Sin(Time * 1.7f + seed);
            Quad(star, centre, Vector2.one * 58f * s * glint, Time * 0.06f + seed, new Color(1f, 0.93f, 0.74f, 0.32f * flicker));
            Quad(star, centre, Vector2.one * 34f * s, Time * 0.06f + seed + Mathf.PI * 0.25f, new Color(1f, 0.85f, 0.6f, 0.2f * flicker));
            Quad(streak, centre, new Vector2(190f * (0.9f + 0.1f * breath), 5f) * s, 0f, new Color(1f, 0.82f, 0.55f, 0.2f * flicker));
        }

        // Light shafts alone, for a raised sword or similar radiant object.
        public void Rays(Vector2 at, float radius, float intensity, int seed)
        {
            float flicker = Flicker(seed) * intensity;
            Fan(rays, At(at), radius * Scale, Time * 0.03f + seed, false, new Color(1f, 0.86f, 0.6f, 0.3f * flicker));
            Fan(rays, At(at), radius * 0.8f * Scale, 1.1f + seed - Time * 0.02f, true, new Color(1f, 0.74f, 0.4f, 0.2f * flicker));
        }

        // Sunlight pouring down through a break in the clouds: shafts fan downward, upward ones fade away.
        public void SunRays(Vector2 at, float radius, float intensity, int seed)
        {
            float breath = 0.85f + 0.15f * Breath(seed);
            Fan(rays, At(at), radius * Scale, Time * 0.012f + seed, false, new Color(1f, 0.86f, 0.58f, 0.3f * intensity * breath), true);
            Fan(rays, At(at), radius * 0.75f * Scale, 0.9f + seed - Time * 0.009f, true, new Color(1f, 0.78f, 0.46f, 0.2f * intensity * breath), true);
        }

        // Soft light pooled on the ground or walls; drawn under particles.
        public void Spill(Vector2 at, Vector2 radii, Color color, float alpha, int seed)
        {
            color.a = alpha * Flicker(seed);
            Quad(back, At(at), radii * Scale, 0f, color);
        }

        public void Glow(Vector2 at, Vector2 half, Color color, float angle = 0f) => Quad(glow, At(at), half * Scale, angle, color);

        // Warm flickering light: lanterns, windows, distant hearths.
        public void Lamp(Vector2 at, float size, float intensity, int seed)
        {
            float flicker = 0.8f + 0.15f * Mathf.PerlinNoise(Time * 2.6f, seed * 1.7f + 0.3f) + 0.05f * Mathf.Sin(Time * 13f + seed);
            Vector2 centre = At(at);
            float s = Scale * size, a = intensity * flicker;
            Quad(back, centre, Vector2.one * 46f * s, 0f, new Color(1f, 0.56f, 0.2f, 0.2f * a));
            Quad(glow, centre, Vector2.one * 15f * s, 0f, new Color(1f, 0.74f, 0.38f, 0.3f * a));
            Quad(glow, centre, Vector2.one * 5f * s, 0f, new Color(1f, 0.9f, 0.68f, 0.35f * a));
        }

        // Calm blue magic light with a slow pulse (the hero's rune staff).
        public void MagicGem(Vector2 at, float size, int seed)
        {
            Vector2 gem = At(at);
            float s = Scale * size;
            float pulse = 0.82f + 0.12f * Mathf.Sin(Time * 1.6f + seed)
                + 0.06f * (Mathf.PerlinNoise(Time * 1.3f, 7.7f + seed) * 2f - 1f);
            Quad(glow, gem, Vector2.one * 36f * s * (0.94f + 0.06f * pulse), 0f, new Color(0.38f, 0.64f, 1f, 0.32f * pulse));
            Quad(glow, gem, Vector2.one * 11f * s, 0f, new Color(0.62f, 0.84f, 1f, 0.6f * pulse));
            Quad(glow, gem, Vector2.one * 4.5f * s, 0f, new Color(0.9f, 0.97f, 1f, 0.8f * pulse));
            Quad(star, gem, Vector2.one * 20f * s * (0.85f + 0.15f * pulse), -Time * 0.1f + seed, new Color(0.7f, 0.88f, 1f, 0.3f * pulse));
        }

        // A glowing blade from hilt to tip, with a sheen that slides along it and a glint at the point.
        public void Blade(Vector2 hilt, Vector2 tip, float width, float intensity, int seed)
        {
            Vector2 from = At(hilt), to = At(tip), direction = to - from;
            float length = direction.magnitude, angle = Mathf.Atan2(direction.y, direction.x), w = width * Scale;
            Vector2 middle = (from + to) * 0.5f;
            float pulse = (0.84f + 0.1f * Mathf.Sin(Time * 2.2f + seed) + 0.06f * Mathf.PerlinNoise(Time * 1.7f, seed + 0.4f)) * intensity;
            Quad(glow, middle, new Vector2(length * 0.64f, w * 4.5f), angle, new Color(1f, 0.76f, 0.4f, 0.22f * pulse));
            Quad(glow, middle, new Vector2(length * 0.56f, w * 1.5f), angle, new Color(1f, 0.93f, 0.76f, 0.34f * pulse));

            float sheen = Mathf.Repeat(Time / 3.4f + Hash(seed + 41), 1f);
            Quad(glow, Vector2.Lerp(from, to, sheen), new Vector2(w * 3.4f, w * 1.8f), angle,
                new Color(1f, 0.97f, 0.88f, 0.5f * Mathf.Sin(sheen * Mathf.PI) * intensity));
            Quad(star, to, Vector2.one * w * 4.5f * (0.85f + 0.15f * pulse), Time * 0.12f + seed, new Color(1f, 0.93f, 0.74f, 0.36f * pulse));
        }

        // Eyes that burn in the dark with a slow, menacing pulse.
        public void Eye(Vector2 at, float size, Color color, int seed)
        {
            Vector2 centre = At(at);
            float s = Scale * size, pulse = 0.6f + 0.4f * (0.5f + 0.5f * Mathf.Sin(Time * 1.4f + seed * 2.1f));
            Quad(glow, centre, Vector2.one * 13f * s, 0f, new Color(color.r, color.g, color.b, 0.32f * pulse));
            Quad(glow, centre, Vector2.one * 3.6f * s, 0f, new Color(Mathf.Lerp(color.r, 1f, 0.5f), Mathf.Lerp(color.g, 1f, 0.5f), Mathf.Lerp(color.b, 1f, 0.5f), 0.85f * pulse));
        }

        // An occasional sharp twinkle (metal, coins, water, distant lights).
        public void Glint(Vector2 at, float size, Color color, float period, int seed, float floor = 0f)
        {
            float phase = Mathf.Repeat(Time / period + Hash(seed + 13), 1f);
            float flash = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(phase * Mathf.PI * 2f)), 12f);
            float alpha = Mathf.Max(flash, floor);
            if (alpha <= 0.01f) return;
            Vector2 centre = At(at);
            Quad(star, centre, Vector2.one * size * Scale * (0.55f + 0.45f * flash), Time * 0.2f + seed, new Color(color.r, color.g, color.b, 0.75f * alpha));
            Quad(glow, centre, Vector2.one * size * 0.45f * Scale, 0f, new Color(color.r, color.g, color.b, 0.45f * alpha));
        }

        // ---- Fire ----

        // A burning spot: ground blaze, a lit smoke column, a hot heart and rising embers.
        public void Fire(Vector2 at, float size, int embers, int seed)
        {
            Vector2 centre = At(at);
            float flicker = 0.62f + 0.32f * Mathf.PerlinNoise(Time * 2.2f, seed * 3.1f + 0.5f) + 0.06f * Mathf.Sin(Time * (11f + seed));
            Quad(fireGlow, centre, new Vector2(66f, 34f) * size * Scale, 0f, new Color(1f, 0.3f, 0.08f, 0.42f * flicker));
            Quad(fireGlow, centre + new Vector2(6f, -48f) * size * Scale, new Vector2(30f, 70f) * size * Scale, 0.08f,
                new Color(0.9f, 0.24f, 0.07f, 0.15f * flicker));
            Quad(fireGlow, centre, new Vector2(12f, 8f) * size * Scale, 0f, new Color(1f, 0.72f, 0.32f, 0.55f * flicker));

            for (int i = 0; i < embers; i++)
            {
                int index = seed * 37 + i;
                Ember(at, index, size, (80f + Hash(index + 9) * 90f) * size, 36f * size, 0f, false);
            }
        }

        // One ember on a staggered loop: shoots up then slows, drifts with the wind, cools from gold to red.
        public void Ember(Vector2 origin, int index, float size, float rise, float spread, float lifeBonus, bool front)
        {
            const float wind = 0.5f;
            float seed = Hash(index);
            float life = 6.5f + seed * 5.5f + lifeBonus;
            float age = Mathf.Repeat(Time / life + Hash(index + 501), 1f);
            float fade = Mathf.Sin(age * Mathf.PI);
            float twinkle = 0.72f + 0.28f * Mathf.Sin(Time * (2.2f + seed * 2.6f) + index * 1.7f);

            float climb = 1f - (1f - age) * (1f - age);
            float x = (seed - 0.5f) * spread + age * age * wind * rise
                + Mathf.Sin(Time * (0.45f + seed * 0.4f) + index * 2.3f) * (2f + age * 10f) * size;
            Vector2 position = At(origin + new Vector2(x, -climb * rise));
            Vector2 velocity = new Vector2(2f * age * wind * rise, -2f * (1f - age) * rise) / life;
            float angle = Mathf.Atan2(velocity.y, velocity.x);
            float stretch = 1f + Mathf.Clamp01(velocity.magnitude / 160f) * 0.9f;

            Color tint = age < 0.45f
                ? Color.Lerp(new Color(1f, 0.86f, 0.52f), new Color(1f, 0.46f, 0.14f), age / 0.45f)
                : Color.Lerp(new Color(1f, 0.46f, 0.14f), new Color(0.78f, 0.14f, 0.05f), (age - 0.45f) / 0.55f);
            float alpha = fade * twinkle * 0.6f;
            float radius = Mathf.Lerp(1.4f, 0.8f, age) * size * (0.75f + Hash(index + 77) * 0.5f) * Scale;

            tint.a = alpha * 0.22f;
            Quad(front ? frontHalo : halo, position, new Vector2(radius * 4.5f * stretch, radius * 4.5f), angle, tint);
            Color hot = Color.Lerp(new Color(1f, 0.95f, 0.8f), tint, age * 0.8f);
            hot.a = alpha;
            Quad(front ? frontCore : core, position, new Vector2(radius * 1.6f * stretch, radius * 1.6f), angle, hot);
        }

        // ---- Particles ----

        // Small drifting lights (fireflies, golden dust, ash) inside a box, each wandering on its own loop.
        public void Motes(Vector2 centre, Vector2 extent, int count, Color color, Vector2 drift, float wander,
            float size, float life, int seed, bool front = false)
        {
            for (int i = 0; i < count; i++)
            {
                int index = seed * 131 + i;
                float h = Hash(index), lifeTime = life * (0.7f + 0.6f * Hash(index + 3));
                float age = Mathf.Repeat(Time / lifeTime + Hash(index + 7), 1f);
                Vector2 start = centre + new Vector2((h - 0.5f) * 2f * extent.x, (Hash(index + 11) - 0.5f) * 2f * extent.y);
                Vector2 sway = new Vector2(Mathf.Sin(Time * (0.3f + h * 0.5f) + index), Mathf.Cos(Time * (0.25f + h * 0.4f) + index * 1.3f)) * wander;
                Vector2 position = At(start + drift * age + sway);
                float fade = Mathf.Sin(age * Mathf.PI) * (0.65f + 0.35f * Mathf.Sin(Time * (1.5f + h * 2f) + index));
                float radius = size * (0.7f + 0.6f * Hash(index + 19)) * Scale;
                Quad(front ? frontHalo : halo, position, Vector2.one * radius * 5f, 0f, new Color(color.r, color.g, color.b, color.a * 0.25f * fade));
                Quad(front ? frontCore : core, position, Vector2.one * radius * 1.6f, 0f,
                    new Color(Mathf.Lerp(color.r, 1f, 0.4f), Mathf.Lerp(color.g, 1f, 0.4f), Mathf.Lerp(color.b, 1f, 0.4f), color.a * 0.85f * fade));
            }
        }

        // Darkness seeping up from a path: smoky wisps that rise, swell and dissolve, with faint violet sparks.
        public void DarkWisps(Vector2[] path, int count, float rise, float size, float strength, int seed)
        {
            if (path == null || path.Length == 0) return;
            for (int i = 0; i < count; i++)
            {
                int index = seed * 173 + i;
                float h = Hash(index), life = 5f + Hash(index + 5) * 4f;
                float age = Mathf.Repeat(Time / life + Hash(index + 9), 1f);
                float along = Hash(index + 21) * (path.Length - 1);
                int segment = Mathf.Min((int)along, Mathf.Max(0, path.Length - 2));
                Vector2 basePoint = path.Length == 1 ? path[0] : Vector2.Lerp(path[segment], path[segment + 1], along - segment);
                float sway = Mathf.Sin(Time * (0.4f + h * 0.4f) + index) * (6f + age * 18f) * size;
                Vector2 position = At(basePoint + new Vector2(sway, -age * rise));
                float fade = Mathf.Sin(age * Mathf.PI);
                float radius = Mathf.Lerp(9f, 24f, age) * size * (0.7f + 0.6f * h) * Scale;
                Quad(dark, position, Vector2.one * radius, Time * 0.2f * (h - 0.5f) + index, new Color(0.05f, 0.02f, 0.08f, 0.42f * strength * fade));
                if (i % 3 == 0)
                {
                    float spark = fade * (0.6f + 0.4f * Mathf.Sin(Time * 2.3f + index));
                    Quad(halo, position, Vector2.one * 5f * size * Scale, 0f, new Color(0.55f, 0.32f, 0.85f, 0.18f * strength * spark));
                    Quad(core, position, Vector2.one * 1.6f * size * Scale, 0f, new Color(0.78f, 0.6f, 1f, 0.6f * strength * spark));
                }
            }
        }

        // Darkness creeping along a path from its first point to its last: smoky tendrils stretched in the
        // direction of travel, swaying as they go, with a few violet sparks.
        public void DarkFlow(Vector2[] path, int count, float speed, float size, float strength, int seed)
        {
            if (path == null || path.Length < 2 || strength <= 0.002f) return;
            float total = 0f;
            for (int k = 1; k < path.Length; k++) total += Vector2.Distance(path[k - 1], path[k]);
            for (int i = 0; i < count; i++)
            {
                int index = seed * 191 + i;
                float h = Hash(index);
                float life = total / (speed * (0.75f + 0.5f * Hash(index + 1)));
                float u = Mathf.Repeat(Time / life + Hash(index + 2), 1f);
                Vector2 point = PointAlong(path, u * total, out Vector2 direction);
                Vector2 normal = new Vector2(-direction.y, direction.x);
                float drift = (h - 0.5f) * 26f + Mathf.Sin(Time * (0.5f + h * 0.5f) + index * 1.7f) * 9f;
                Vector2 position = At(point + normal * drift * size);
                float fade = Mathf.Clamp01(u * 6f) * Mathf.Clamp01((1f - u) * 6f);
                float radius = (10f + 9f * Hash(index + 3)) * size * Scale * (0.9f + 0.1f * Mathf.Sin(Time * 1.3f + index));
                float angle = Mathf.Atan2(direction.y, direction.x);
                Quad(dark, position, new Vector2(radius * 1.9f, radius), angle, new Color(0.05f, 0.02f, 0.08f, 0.4f * strength * fade));
                if (i % 4 == 0)
                {
                    float spark = fade * (0.6f + 0.4f * Mathf.Sin(Time * 2.1f + index));
                    Quad(halo, position, Vector2.one * 5f * size * Scale, 0f, new Color(0.55f, 0.32f, 0.85f, 0.18f * strength * spark));
                    Quad(core, position, Vector2.one * 1.6f * size * Scale, 0f, new Color(0.78f, 0.6f, 1f, 0.6f * strength * spark));
                }
            }
        }

        private static Vector2 PointAlong(Vector2[] path, float distance, out Vector2 direction)
        {
            for (int k = 1; k < path.Length; k++)
            {
                Vector2 segment = path[k] - path[k - 1];
                float length = segment.magnitude;
                if (distance <= length || k == path.Length - 1)
                {
                    direction = length > 0f ? segment / length : Vector2.right;
                    return path[k - 1] + direction * Mathf.Min(distance, length);
                }
                distance -= length;
            }
            direction = Vector2.right;
            return path[0];
        }

        // Low fog drifting sideways across a band; blobs fade in and out at the band's ends.
        public void Fog(Vector2 centre, Vector2 extent, int count, Color color, float alpha, float speed, int seed)
        {
            for (int i = 0; i < count; i++)
            {
                int index = seed * 61 + i;
                float h = Hash(index);
                float u = Mathf.Repeat(h + Time * speed / (2f * extent.x) * (0.7f + 0.6f * Hash(index + 2)), 1f);
                Vector2 at = new Vector2(centre.x - extent.x + u * 2f * extent.x, centre.y + (Hash(index + 4) - 0.5f) * 2f * extent.y);
                Vector2 half = new Vector2(extent.x * 0.32f * (0.7f + 0.6f * Hash(index + 6)), extent.y * 0.9f * (0.7f + 0.6f * Hash(index + 8)));
                float a = alpha * Mathf.Sin(u * Mathf.PI) * (0.8f + 0.2f * Mathf.Sin(Time * 0.3f + index));
                Quad(fog, At(at), half * Scale, 0f, new Color(color.r, color.g, color.b, a));
            }
        }

        // A light shaft from the sky with dust drifting down inside it.
        public void Shaft(Vector2 top, Vector2 bottom, float topHalfWidth, float bottomHalfWidth, float intensity, int seed, int motes)
        {
            float shimmer = intensity * (0.85f + 0.1f * Mathf.Sin(Time * (0.7f + Hash(seed) * 0.4f) + seed) + 0.05f * Mathf.Sin(Time * 2.9f + seed));
            float breathe = 1f + 0.05f * Mathf.Sin(Time * 0.5f + seed * 1.3f);
            Vector2 a = At(top), b = At(bottom), across = new Vector2(b.y - a.y, a.x - b.x).normalized;
            float wTop = topHalfWidth * Scale * breathe, wBottom = bottomHalfWidth * Scale * breathe;
            var color = new Color(1f, 0.9f, 0.66f, 0.55f * shimmer);
            ushort start = (ushort)shafts.Vertices.Count;
            AddVertex(shafts, a - across * wTop, new Vector2(0f, 1f), color);
            AddVertex(shafts, a + across * wTop, new Vector2(1f, 1f), color);
            AddVertex(shafts, b + across * wBottom, new Vector2(1f, 0f), color);
            AddVertex(shafts, b - across * wBottom, new Vector2(0f, 0f), color);
            AddTriangle(shafts, start, start + 1, start + 2);
            AddTriangle(shafts, start, start + 2, start + 3);

            Vector2 axis = bottom - top, side = new Vector2(axis.y, -axis.x).normalized;
            for (int i = 0; i < motes; i++)
            {
                int index = seed * 97 + i;
                float life = 6f + Hash(index) * 5f, t = Mathf.Repeat(Time / life + Hash(index + 3), 1f);
                float width = Mathf.Lerp(topHalfWidth, bottomHalfWidth, t) * 0.8f;
                Vector2 p = top + axis * t + side * ((Hash(index + 5) - 0.5f) * 2f * width + Mathf.Sin(Time * 0.6f + index) * 6f);
                float fade = Mathf.Sin(t * Mathf.PI) * (0.6f + 0.4f * Mathf.Sin(Time * 2f + index)) * intensity;
                Vector2 position = At(p);
                float r = (0.8f + Hash(index + 7) * 0.7f) * Scale;
                Quad(halo, position, Vector2.one * r * 5f, 0f, new Color(1f, 0.85f, 0.55f, 0.2f * fade));
                Quad(core, position, Vector2.one * r * 1.6f, 0f, new Color(1f, 0.96f, 0.84f, 0.75f * fade));
            }
        }

        // Shards of light flung outward from an impact on a staggered loop.
        public void Burst(Vector2 at, int count, float radius, Color color, float period, int seed)
        {
            for (int i = 0; i < count; i++)
            {
                int index = seed * 211 + i;
                float life = period * (0.7f + 0.6f * Hash(index + 1));
                float age = Mathf.Repeat(Time / life + Hash(index + 2), 1f);
                float angle = Hash(index) * Mathf.PI * 2f;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float distance = radius * (0.35f + 0.65f * Hash(index + 3)) * (1f - (1f - age) * (1f - age));
                Vector2 position = At(at + direction * distance + new Vector2(0f, age * age * radius * 0.25f));
                float fade = Mathf.Pow(1f - age, 1.4f) * Mathf.Clamp01(age * 12f);
                float r = (1f + Hash(index + 4)) * Scale;
                float stretch = 1f + 2.2f * (1f - age);
                Quad(halo, position, new Vector2(r * 5f * stretch, r * 4f), angle, new Color(color.r, color.g, color.b, 0.25f * fade));
                Quad(core, position, new Vector2(r * 1.6f * stretch, r * 1.4f), angle,
                    new Color(Mathf.Lerp(color.r, 1f, 0.5f), Mathf.Lerp(color.g, 1f, 0.5f), Mathf.Lerp(color.b, 1f, 0.5f), 0.9f * fade));
            }
        }

        // ---- Mesh helpers ----

        private void Quad(Layer layer, Vector2 centre, Vector2 half, float angle, Color color)
        {
            if (CaptionFadeStart > 0f && viewHeight > 0f)
                color.a *= Mathf.Lerp(1f, 0.45f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(CaptionFadeStart, 0.97f, centre.y / viewHeight)));
            if (color.a <= 0.002f || layer.Vertices.Count > 60000) return;
            Vector2 right = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * half.x;
            Vector2 down = new Vector2(-Mathf.Sin(angle), Mathf.Cos(angle)) * half.y;
            ushort start = (ushort)layer.Vertices.Count;
            AddVertex(layer, centre - right - down, new Vector2(0f, 1f), color);
            AddVertex(layer, centre + right - down, new Vector2(1f, 1f), color);
            AddVertex(layer, centre + right + down, new Vector2(1f, 0f), color);
            AddVertex(layer, centre - right + down, new Vector2(0f, 0f), color);
            AddTriangle(layer, start, start + 1, start + 2);
            AddTriangle(layer, start, start + 2, start + 3);
        }

        // Radial fan carrying the light-shaft texture; shafts pointing downward (or upward, for sunlight
        // falling from the sky) fade out per vertex.
        private static void Fan(Layer layer, Vector2 centre, float radius, float rotation, bool mirror, Color color, bool fadeUp = false)
        {
            float maskSign = fadeUp ? -1f : 1f;
            const int segments = 72;
            if (layer.Vertices.Count > 60000) return;
            ushort start = (ushort)layer.Vertices.Count;
            AddVertex(layer, centre, new Vector2(0.5f, 0.5f), color);
            for (int ring = 1; ring <= 2; ring++)
            {
                float extent = ring == 1 ? 0.22f : 1f;
                for (int i = 0; i < segments; i++)
                {
                    float angle = i * (Mathf.PI * 2f / segments);
                    Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    float textureAngle = (mirror ? -angle : angle) - rotation;
                    Vector2 uv = new Vector2(0.5f + 0.5f * extent * Mathf.Cos(textureAngle), 0.5f + 0.5f * extent * Mathf.Sin(textureAngle));
                    Color tint = color;
                    tint.a *= Mathf.Lerp(1f, fadeUp ? 0f : 0.12f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(direction.y * 1.3f * maskSign + (fadeUp ? 0.5f : 0f))));
                    AddVertex(layer, centre + direction * radius * extent, uv, tint);
                }
            }
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                int inner = start + 1, outer = start + 1 + segments;
                AddTriangle(layer, start, inner + i, inner + next);
                AddTriangle(layer, inner + i, outer + i, outer + next);
                AddTriangle(layer, inner + i, outer + next, inner + next);
            }
        }

        private static void AddVertex(Layer layer, Vector2 position, Vector2 uv, Color color) =>
            layer.Vertices.Add(new Vertex { position = new Vector3(position.x, position.y, Vertex.nearZ), uv = uv, tint = color });

        private static void AddTriangle(Layer layer, int a, int b, int c)
        {
            layer.Indices.Add((ushort)a);
            layer.Indices.Add((ushort)b);
            layer.Indices.Add((ushort)c);
        }

        public static float Hash(int value) => Mathf.Repeat(Mathf.Sin(value * 12.9898f) * 43758.5453f, 1f);

        // ---- Procedural sprite textures (white, shaped by alpha; tinted per vertex) ----

        private static void EnsureTextures()
        {
            if (glowTexture == null)
                glowTexture = MakeTexture("Fx Glow", 128, 128, (x, y) =>
                {
                    float r2 = x * x + y * y;
                    return (0.55f * Mathf.Exp(-r2 * 16f) + 0.45f * Mathf.Exp(-r2 * 3.6f)) * EdgeFade(Mathf.Sqrt(r2));
                });
            if (dotTexture == null)
                dotTexture = MakeTexture("Fx Dot", 32, 32, (x, y) =>
                {
                    float r2 = x * x + y * y;
                    return Mathf.Clamp01(1.15f * Mathf.Exp(-r2 * 7f)) * EdgeFade(Mathf.Sqrt(r2));
                });
            if (starTexture == null)
                starTexture = MakeTexture("Fx Star", 128, 128, (x, y) =>
                {
                    float ax = Mathf.Abs(x), ay = Mathf.Abs(y), r = Mathf.Sqrt(x * x + y * y);
                    float arms = Mathf.Max(Mathf.Exp(-ay * 55f) * Mathf.Exp(-ax * 3f), Mathf.Exp(-ax * 55f) * Mathf.Exp(-ay * 3f));
                    return Mathf.Clamp01(arms + 0.5f * Mathf.Exp(-r * r * 40f)) * EdgeFade(r);
                });
            if (streakTexture == null)
                streakTexture = MakeTexture("Fx Streak", 256, 16, (x, y) =>
                    Mathf.Exp(-x * x * 5f) * Mathf.Exp(-y * y * 6f) * (1f - x * x) * (1f - y * y));
            if (shaftTexture == null)
                shaftTexture = MakeTexture("Fx Shaft", 128, 256, (x, y) =>
                {
                    // x across the beam, y along it (+1 at the sky end).
                    float across = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(x)), 1.5f);
                    float strands = 0.72f + 0.28f * Mathf.Sin(x * 21f + 1.3f) * Mathf.Sin(x * 7.7f + 0.4f);
                    float along = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-1f, -0.35f, y)) * Mathf.Lerp(0.55f, 1f, Mathf.InverseLerp(1f, 0.75f, y));
                    return across * strands * along;
                });
            if (smokeTexture == null)
                smokeTexture = MakeTexture("Fx Smoke", 96, 96, (x, y) =>
                {
                    float r = Mathf.Sqrt(x * x + y * y);
                    float noise = 0.5f + 0.25f * Mathf.Sin(x * 5.3f + y * 3.1f + 0.7f) * Mathf.Sin(y * 4.7f - x * 2.3f)
                        + 0.25f * Mathf.Sin(x * 11.7f - y * 9.3f) * Mathf.Sin(y * 12.1f + x * 6.2f + 1.1f);
                    return Mathf.Exp(-r * r * 3.2f) * (0.55f + 0.45f * noise) * EdgeFade(r);
                });
            if (raysTexture == null)
                raysTexture = MakeRaysTexture(512);
        }

        private static float EdgeFade(float r) => 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.72f, 1f, r));

        private static Texture2D MakeTexture(string textureName, int width, int height, Func<float, float, float> alpha)
        {
            var pixels = new Color32[width * height];
            for (int j = 0; j < height; j++)
            for (int i = 0; i < width; i++)
            {
                float x = (i + 0.5f) / width * 2f - 1f, y = (j + 0.5f) / height * 2f - 1f;
                pixels[j * width + i] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha(x, y)) * 255f));
            }
            return Finish(textureName, width, height, pixels);
        }

        // Crepuscular shafts: wrapped angular noise sharpened into distinct beams, fading in from the core and out at the rim.
        private static Texture2D MakeRaysTexture(int size)
        {
            const int samples = 2048;
            int[] harmonics = { 5, 8, 11, 14, 19, 23, 29, 37, 47, 61, 79 };
            var random = new System.Random(1931);
            var phases = new float[harmonics.Length];
            for (int k = 0; k < phases.Length; k++) phases[k] = (float)random.NextDouble() * Mathf.PI * 2f;

            var beams = new float[samples];
            float min = float.MaxValue, max = float.MinValue;
            for (int s = 0; s < samples; s++)
            {
                float theta = s * (Mathf.PI * 2f / samples), value = 0f;
                for (int k = 0; k < harmonics.Length; k++)
                    value += Mathf.Sin(harmonics[k] * theta + phases[k]) / Mathf.Sqrt(harmonics[k]);
                beams[s] = value;
                min = Mathf.Min(min, value);
                max = Mathf.Max(max, value);
            }
            for (int s = 0; s < samples; s++)
                beams[s] = Mathf.Pow(Mathf.InverseLerp(min, max, beams[s]), 3.2f);

            var pixels = new Color32[size * size];
            for (int j = 0; j < size; j++)
            for (int i = 0; i < size; i++)
            {
                float x = (i + 0.5f) / size * 2f - 1f, y = (j + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(x * x + y * y);
                float theta = Mathf.Repeat(Mathf.Atan2(y, x), Mathf.PI * 2f);
                float beam = beams[Mathf.Min(samples - 1, (int)(theta / (Mathf.PI * 2f) * samples))];
                float radial = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.03f, 0.14f, r))
                    * Mathf.Pow(Mathf.Clamp01(1f - r), 1.7f) * EdgeFade(r);
                float alpha = (0.12f + 0.88f * beam) * radial;
                pixels[j * size + i] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha) * 255f));
            }
            return Finish("Fx Light Shafts", size, size, pixels);
        }

        private static Texture2D Finish(string textureName, int width, int height, Color32[] pixels)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, true)
            {
                name = textureName,
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
            };
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return texture;
        }
    }
}

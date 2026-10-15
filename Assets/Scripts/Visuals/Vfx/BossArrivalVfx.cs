using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Lightbringer.Visuals
{
    // The Warlord's arrival: the citadel's fall throws a rolling wall of dust across the field under his
    // shockwave (Rupture, alongside BossShockwaveVfx), then a dense shroud of fire-lit smoke gathers in front of the
    // ruin and is blown apart to reveal him standing there (Reveal). Lit smoke billboards, a fissure decal,
    // embers and a fire light. Presentation only: BossEncounter owns timing, kills and the boss.
    public sealed class BossArrivalVfx : MonoBehaviour
    {
        public enum Kind { Rupture, Reveal }

        private const float ShroudFadeSeconds = 3f, SmokeEndFadeSeconds = 2f;

        private static readonly Color Ember = new Color(1f, 0.6f, 0.3f);
        private static readonly Color FireLight = new Color(1f, 0.36f, 0.16f);
        private static readonly Color Dust = new Color(0.42f, 0.37f, 0.33f);
        private static readonly Color Ash = new Color(0.17f, 0.15f, 0.16f);
        private static readonly Color Soot = new Color(0.075f, 0.06f, 0.075f);
        private static readonly int GlowId = Shader.PropertyToID("_Glow");
        private static readonly int GlowAmountId = Shader.PropertyToID("_GlowAmount");
        private static readonly int ModeId = Shader.PropertyToID("_Mode");
        private static readonly int ProgressId = Shader.PropertyToID("_Progress");
        private static readonly int HeatId = Shader.PropertyToID("_Heat");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int SeedId = Shader.PropertyToID("_Seed");

        // The battle camera only renders scene depth while some arrival effect needs soft smoke edges.
        private static int depthUsers;
        private static bool depthWasRequired;
        private static UniversalAdditionalCameraData depthCamera;

        private readonly List<Material> owned = new List<Material>();
        private readonly List<Material> smokeMaterials = new List<Material>();
        private ArtStyleLibrary style;
        private System.Random random;
        private Kind kind;
        private float age, lifetime, ground, front, ringFront;
        private float partAge;
        private bool holdsDepth, burst, parted;
        private Shader shader;
        private Material fissure;
        private Material shroudMaterial;
        private Light fire;
        private ParticleSystem dust, fireSmoke, sparks, shroud;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetDepth() { depthUsers = 0; depthCamera = null; }

        public static BossArrivalVfx Spawn(ArtStyleLibrary style, Transform parent, Vector3 groundPoint, Kind kind, Camera viewer)
        {
            if (!Application.isPlaying || style == null) return null;
            Shader shader = UnityEngine.Resources.Load<Shader>("BossArrival");
            if (shader == null) return null;
            var root = new GameObject("Boss " + kind + " (VFX)");
            root.transform.SetParent(parent, false);
            root.transform.position = groundPoint;
            BossArrivalVfx vfx = root.AddComponent<BossArrivalVfx>();
            vfx.style = style;
            vfx.kind = kind;
            vfx.shader = shader;
            vfx.ground = groundPoint.y;
            vfx.random = new System.Random(root.GetInstanceID());
            vfx.holdsDepth = AcquireDepth(viewer);
            vfx.Build();
            return vfx;
        }

        // Rupture only: the leading edge of the dust wall, kept on the radius that is killing allies.
        public void SetFront(float radius) => front = Mathf.Max(front, radius);

        // Reveal only: a gust tears the shroud apart and the Warlord stands where it was.
        public void Part()
        {
            if (kind != Kind.Reveal || parted) return;
            parted = true;
            partAge = age;
            Vector3 centre = transform.position;
            if (shroud != null)
            {
                var puffs = new ParticleSystem.Particle[shroud.particleCount];
                int count = shroud.GetParticles(puffs);
                for (int i = 0; i < count; i++)
                {
                    Vector3 outward = puffs[i].position - centre;
                    outward.y = 0f;
                    outward = outward.sqrMagnitude > 0.04f ? outward.normalized : Flat(Range(0f, Mathf.PI * 2f));
                    puffs[i].velocity += outward * Range(1.2f, 2.4f) + Vector3.up * Range(0.4f, 1f);
                    // Preserve particle age: opacity, size and shader density all depend on it.
                    // New emission stops below; existing puffs drift apart and fade over their natural lifetimes.
                }
                shroud.SetParticles(puffs, count);
            }
            // The gust skims the ground as a skirt of dust.
            for (int i = 0; i < 30; i++)
            {
                Vector3 direction = Flat(Range(0f, Mathf.PI * 2f));
                Puff(dust, centre + direction * Range(0.8f, 2.4f) + Vector3.up * Range(0.4f, 1f),
                    direction * Range(6f, 13f) + Vector3.up * Range(0.2f, 1f), Range(2.6f, 4.4f), Range(1.6f, 2.6f), Shade(Dust, Range(0.4f, 0.65f)));
            }
        }

        private void Build()
        {
            bool rupture = kind == Kind.Rupture;
            lifetime = rupture ? 9f : 8f;
            Material litSmoke = Smoke(0f);
            Material fireLitSmoke = Smoke(1.25f);
            dust = Smoky("Rolling Dust", litSmoke, rupture ? 2400 : 260, 1.6f);
            fireSmoke = Smoky(rupture ? "Citadel Smoke Plume" : "Ruin Smoke", fireLitSmoke, rupture ? 260 : 200, 0.55f);
            sparks = Sparks(rupture ? 320 : 360);
            // The citadel's fissures already cover the ground in front of the ruin where the Warlord is revealed.
            if (rupture) Fissure(9f);
            else
            {
                // The reveal cloud fades independently of the smoke still venting from the ruin.
                shroudMaterial = Smoke(1.25f);
                shroud = Smoky("Reveal Shroud", shroudMaterial, 320, 0.8f);
            }
            fire = new GameObject("Fissure Firelight").AddComponent<Light>();
            fire.transform.SetParent(transform, false);
            fire.transform.localPosition = Vector3.up * (rupture ? 3f : 0.8f);
            fire.type = LightType.Point;
            fire.color = FireLight;
            fire.range = rupture ? 26f : 15f;
            fire.shadows = LightShadows.None;
            fire.intensity = 0f;
        }

        private float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);

        private Vector3 Flat(float angle) => new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));

        // ---------------------------------------------------------------- materials and systems

        private Material Smoke(float glow)
        {
            var material = new Material(shader) { name = "Boss Arrival Smoke", hideFlags = HideFlags.DontSave };
            material.SetFloat(ModeId, 0f);
            material.SetFloat(GlowAmountId, glow);
            material.SetFloat(OpacityId, 1f);
            material.SetColor(GlowId, new Color(2.6f, 0.42f, 0.14f));
            if (holdsDepth) material.EnableKeyword("_SOFT_PARTICLES");
            material.renderQueue = 3010;
            owned.Add(material);
            smokeMaterials.Add(material);
            return material;
        }

        private void Fissure(float radius)
        {
            fissure = new Material(shader) { name = "Boss Arrival Fissure", hideFlags = HideFlags.DontSave };
            fissure.SetFloat(ModeId, 1f);
            fissure.SetFloat(SeedId, Range(0f, 40f));
            fissure.SetFloat(ProgressId, 0f);
            fissure.renderQueue = 2995;
            owned.Add(fissure);
            var item = new GameObject("Molten Fissures");
            item.transform.SetParent(transform, false);
            item.transform.localPosition = Vector3.up * 0.06f;
            item.transform.localRotation = Quaternion.Euler(0f, Range(0f, 360f), 0f);
            item.transform.localScale = new Vector3(radius * 2f, 1f, radius * 2f);
            item.AddComponent<MeshFilter>().sharedMesh = ProceduralMeshes.GroundQuad;
            var renderer = item.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = fissure;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private ParticleSystem CreateSystem(string label, Material material, int capacity, out ParticleSystemRenderer renderer)
        {
            var item = new GameObject(label);
            item.SetActive(false);
            item.transform.SetParent(transform, false);
            var system = item.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = system.main;
            main.loop = true; // emission is off; looping keeps hand-emitted particles simulating
            main.playOnAwake = false;
            main.duration = lifetime;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = capacity;
            main.startSpeed = 0f;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;
            renderer = item.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            item.SetActive(true);
            system.Play();
            return system;
        }

        // Smoke and dust: emitted by hand on the timeline, slowed by air drag, swelling and churning as they age.
        private ParticleSystem Smoky(string label, Material material, int capacity, float drag)
        {
            ParticleSystem system = CreateSystem(label, material, capacity, out ParticleSystemRenderer renderer);
            renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
            {
                ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Color, ParticleSystemVertexStream.UV,
                ParticleSystemVertexStream.StableRandomXY, ParticleSystemVertexStream.AgePercent
            });
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.maxParticleSize = 4f;
            ParticleSystem.LimitVelocityOverLifetimeModule limit = system.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = 100f;
            limit.drag = drag;
            limit.multiplyDragByParticleSize = false;
            limit.multiplyDragByParticleVelocity = true;
            ParticleSystem.NoiseModule noise = system.noise;
            noise.enabled = true;
            noise.strength = 0.45f;
            noise.frequency = 0.22f;
            noise.scrollSpeed = 0.15f;
            noise.quality = ParticleSystemNoiseQuality.Medium;
            ParticleSystem.RotationOverLifetimeModule spin = system.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);
            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.45f, 0f, 3f),
                new Keyframe(0.35f, 1f), new Keyframe(1f, 1.45f)));
            ParticleSystem.ColorOverLifetimeModule colour = system.colorOverLifetime;
            colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(0.75f, 0.6f), new GradientAlphaKey(0f, 1f) });
            colour.color = gradient;
            return system;
        }

        // Hot embers and grit: additive streaks that arc under gravity and cool from white-orange to deep red.
        private ParticleSystem Sparks(int capacity)
        {
            Material source = style.vfxMaterial;
            if (source == null) return null;
            var material = new Material(source) { name = "Boss Arrival Embers", hideFlags = HideFlags.DontSave };
            material.SetFloat("_Shape", 0f);
            material.SetFloat("_Softness", 2.4f);
            material.SetFloat("_Additive", 1f);
            material.SetColor("_Color", new Color(3f, 3f, 3f));
            owned.Add(material);
            ParticleSystem system = CreateSystem("Embers", material, capacity, out ParticleSystemRenderer renderer);
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.04f;
            renderer.lengthScale = 1.5f;
            ParticleSystem.MainModule main = system.main;
            main.gravityModifier = 0.35f;
            ParticleSystem.NoiseModule noise = system.noise;
            noise.enabled = true;
            noise.strength = 0.9f;
            noise.frequency = 0.8f;
            ParticleSystem.ColorOverLifetimeModule colour = system.colorOverLifetime;
            colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.85f, 0.6f), 0f), new GradientColorKey(new Color(1f, 0.3f, 0.08f), 0.4f),
                    new GradientColorKey(new Color(0.5f, 0.05f, 0.03f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
            colour.color = gradient;
            return system;
        }

        private void Puff(ParticleSystem system, Vector3 position, Vector3 velocity, float size, float life, Color colour)
        {
            if (system == null) return;
            var emit = new ParticleSystem.EmitParams
            {
                position = position, velocity = velocity, startSize = size, startLifetime = life,
                startColor = colour, rotation = Range(0f, 360f), randomSeed = (uint)random.Next()
            };
            system.Emit(emit, 1);
        }

        private Color Shade(Color colour, float alpha) =>
            new Color(colour.r * Range(0.85f, 1.12f), colour.g * Range(0.85f, 1.12f), colour.b * Range(0.85f, 1.1f), alpha);

        // ---------------------------------------------------------------- timeline

        private void Update() => Tick(Time.deltaTime);

        private void Tick(float delta)
        {
            if (delta <= 0f) return;
            age += delta;
            if (kind == Kind.Rupture) RuptureTimeline(delta);
            else RevealTimeline(delta);
            FadeSmoke();
            if (age >= lifetime) Destroy(gameObject);
        }

        private void FadeSmoke()
        {
            // Reach zero opacity before destroying the root, including late-born wisps.
            float endFade = 1f - Mathf.SmoothStep(0f, 1f,
                (age - (lifetime - SmokeEndFadeSeconds)) / SmokeEndFadeSeconds);
            float revealFade = parted
                ? 1f - Mathf.SmoothStep(0f, 1f, (age - partAge) / ShroudFadeSeconds)
                : 1f;
            foreach (Material material in smokeMaterials)
                material.SetFloat(OpacityId, endFade * (material == shroudMaterial ? revealFade : 1f));
        }

        private void RuptureTimeline(float delta)
        {
            Vector3 centre = transform.position;
            if (!burst)
            {
                burst = true;
                // The citadel bursts: a choking cloud at its foot.
                for (int i = 0; i < 46; i++)
                {
                    Vector3 direction = Flat(Range(0f, Mathf.PI * 2f));
                    Puff(dust, centre + direction * Range(1f, 5f) + Vector3.up * Range(0.5f, 4f),
                        direction * Range(4f, 10f) + Vector3.up * Range(0.5f, 3f), Range(5f, 9f), Range(2.5f, 4f), Shade(Dust, Range(0.45f, 0.7f)));
                }
                for (int i = 0; i < 90; i++)
                {
                    Vector3 direction = Flat(Range(0f, Mathf.PI * 2f));
                    Puff(sparks, centre + Vector3.up * Range(1f, 5f), direction * Range(3f, 11f) + Vector3.up * Range(3f, 10f),
                        Range(0.05f, 0.14f), Range(1f, 2.4f), Ember);
                }
            }
            // A plume of fire-lit smoke climbing out of the ruin, thinning as the wave runs out.
            float plume = Mathf.Clamp01(1f - age / 3.2f);
            int count = Mathf.FloorToInt(delta * 30f * plume + (float)random.NextDouble());
            for (int i = 0; i < count; i++)
            {
                Vector3 direction = Flat(Range(0f, Mathf.PI * 2f));
                Puff(fireSmoke, centre + direction * Range(0f, 3.5f) + Vector3.up * Range(1f, 4f),
                    direction * Range(0.4f, 1.6f) + Vector3.up * Range(3f, 6.5f), Range(5.5f, 9.5f), Range(3.5f, 5f), Shade(Ash, Range(0.55f, 0.8f)));
            }
            // Ground dust rolling outward on the killing front. Puffs are scattered at random angles and depths behind
            // the edge, large and faint, so they merge into one ragged wall instead of reading as a ring of discs.
            const float Step = 1.8f;
            while (front - ringFront >= Step)
            {
                ringFront += Step;
                float radius = ringFront;
                // Lower graphics tiers thin the wall; each puff is a large, noisy overdraw billboard.
                float spacing = Lightbringer.Core.GraphicsQuality.Current == Lightbringer.Core.GraphicsQuality.Tier.Low ? 5.6f
                    : Lightbringer.Core.GraphicsQuality.Current == Lightbringer.Core.GraphicsQuality.Tier.Medium ? 4.4f : 3.6f;
                // Only the arc crossing the field gets dust, so the wall keeps its density out to the far edge.
                float onField = FieldArc(centre, radius);
                if (onField <= 0f) continue;
                int around = Mathf.Clamp(Mathf.RoundToInt(Mathf.PI * 2f * radius * onField / spacing), 6, 70);
                for (int i = 0; i < around; i++)
                {
                    Vector3 direction = Flat(Range(0f, Mathf.PI * 2f));
                    for (int retry = 0; retry < 4 && Outside(centre + direction * radius) > 6f; retry++)
                        direction = Flat(Range(0f, Mathf.PI * 2f));
                    if (Outside(centre + direction * radius) > 6f) continue;
                    float depth = Range(0f, 1f);
                    float size = Range(3.5f, 6.5f) * (0.85f + Mathf.Min(radius, 40f) / 80f);
                    Puff(dust, centre + direction * (radius - depth * depth * 5f + Range(-1.5f, 1.5f)) + Vector3.up * Range(0.6f, 1.8f),
                        direction * Range(2.5f, 6f) + Vector3.up * Range(0.2f, 1.4f), size, Range(1.8f, 3f), Shade(Dust, Range(0.35f, 0.6f)));
                    if (random.NextDouble() < 0.1)
                        Puff(sparks, centre + direction * radius + Vector3.up * 0.3f,
                            direction * Range(2f, 6f) + Vector3.up * Range(2f, 5f), Range(0.04f, 0.1f), Range(0.6f, 1.2f), Ember);
                }
            }
            CrackAndFire(0.6f, 6.5f, 10f, 2.4f);
        }

        // Distance beyond the playable field (0 inside), in the battle root's space.
        private float Outside(Vector3 world)
        {
            Vector3 local = transform.parent != null ? transform.parent.InverseTransformPoint(world) : world;
            return BattlefieldEnvironment.OutsideDistance(local.x, local.z);
        }

        // Fraction of the circle at `radius` that lies on (or just off) the field.
        private float FieldArc(Vector3 centre, float radius)
        {
            const int Samples = 48;
            int inside = 0;
            for (int i = 0; i < Samples; i++)
                if (Outside(centre + Flat(i * Mathf.PI * 2f / Samples) * radius) <= 6f) inside++;
            return inside / (float)Samples;
        }

        private void RevealTimeline(float delta)
        {
            Vector3 centre = transform.position;
            if (!burst)
            {
                burst = true;
                // Smoke rolls in front of the ruin, thick enough to hide the Warlord when the view returns to it.
                for (int i = 0; i < 70; i++) ShroudPuff(centre);
            }
            // The shroud churns slowly in place until Part() blows it away.
            if (!parted)
            {
                int count = Mathf.FloorToInt(delta * 48f + (float)random.NextDouble());
                for (int i = 0; i < count; i++) ShroudPuff(centre);
            }
            // Heat keeps venting from the citadel's cracks: thin fire-lit smoke and drifting embers until the effect ends.
            float vent = Mathf.Clamp01((lifetime - 2f - age) / 2f);
            int wisps = vent > 0f
                ? Mathf.FloorToInt(delta * (age < 2.5f ? 14f : 6f) * vent + (float)random.NextDouble()) : 0;
            for (int i = 0; i < wisps; i++)
            {
                Vector3 direction = Flat(Range(0f, Mathf.PI * 2f));
                Puff(fireSmoke, centre + direction * Range(0.5f, 4.5f) + Vector3.up * 0.4f,
                    direction * Range(0.1f, 0.5f) + Vector3.up * Range(0.9f, 2.2f), Range(1.8f, 3.2f), Range(2.4f, 3.4f), Shade(Ash, 0.4f));
            }
            int embers = vent > 0f
                ? Mathf.FloorToInt(delta * (age < 2.5f ? 34f : 10f) * vent + (float)random.NextDouble()) : 0;
            for (int i = 0; i < embers; i++)
            {
                Vector3 direction = Flat(Range(0f, Mathf.PI * 2f));
                Puff(sparks, centre + direction * Range(0.3f, 4.5f) + Vector3.up * 0.15f,
                    direction * Range(0.2f, 0.8f) + Vector3.up * Range(1.5f, 4f), Range(0.03f, 0.08f), Range(1.2f, 2.4f), Ember);
            }
            CrackAndFire(0.35f, 7f, 7.5f, 2f);
        }

        // One large, opaque soot puff of the shroud, drifting slowly around the Warlord's standing spot.
        private void ShroudPuff(Vector3 centre)
        {
            Vector3 direction = Flat(Range(0f, Mathf.PI * 2f));
            Vector3 swirl = new Vector3(direction.z, 0f, -direction.x);
            Puff(shroud, centre + direction * Range(0f, 2.6f) + Vector3.up * Range(0.3f, 4.8f),
                swirl * Range(0.3f, 0.9f) + Vector3.up * Range(0.1f, 0.5f), Range(3.4f, 5.4f), Range(2.6f, 3.4f), Shade(Soot, 1f));
        }

        // Fissures tear open over `spread` seconds; heat and firelight flare, then settle to a smoulder and fade out.
        private void CrackAndFire(float spread, float lightPeak, float heatPeak, float settle)
        {
            float fade = Mathf.Clamp01((lifetime - age) / 1.5f);
            float flare = Mathf.Exp(-age / settle);
            float heat = (0.55f + heatPeak / 7.5f * flare) * fade;
            if (fissure != null)
            {
                float t = Mathf.Clamp01(age / spread);
                fissure.SetFloat(ProgressId, 1f - (1f - t) * (1f - t) * (1f - t));
                fissure.SetFloat(HeatId, heat);
                fissure.SetFloat(OpacityId, fade);
            }
            if (fire != null)
            {
                float flicker = 0.82f + 0.18f * Mathf.PerlinNoise(age * 7f, 0.3f);
                float rise = Mathf.Clamp01(age / 0.06f);
                fire.intensity = (1.6f + lightPeak * flare) * rise * flicker * fade;
            }
        }

        // ---------------------------------------------------------------- scene depth for soft smoke

        private static bool AcquireDepth(Camera viewer)
        {
            if (viewer == null) return false;
            UniversalAdditionalCameraData data = viewer.GetUniversalAdditionalCameraData();
            if (data == null) return false;
            if (depthUsers > 0 && depthCamera != data) return false;
            if (depthUsers == 0) { depthCamera = data; depthWasRequired = data.requiresDepthTexture; }
            depthUsers++;
            data.requiresDepthTexture = true;
            return true;
        }

        private void OnDestroy()
        {
            if (holdsDepth && --depthUsers == 0)
            {
                if (depthCamera != null) depthCamera.requiresDepthTexture = depthWasRequired;
                depthCamera = null;
            }
            holdsDepth = false;
            foreach (Material material in owned) if (material != null) Destroy(material);
            owned.Clear();
        }
    }
}

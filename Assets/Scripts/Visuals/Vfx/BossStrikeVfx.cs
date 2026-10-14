using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Lightbringer.Visuals
{
    // Boss special-attack impacts built to the hero spell standard: layered HDR surfaces (thorned sigil, scorched
    // ground, sky pillar, sweeping slash, shock walls), forked violet bolts, a flash light and spark/ember bursts.
    // Presentation only: damage stays in BossCombat. Shader lives in Resources (BossStrike) for standalone builds.
    public sealed class BossStrikeVfx : MonoBehaviour
    {
        public enum Kind { Lightning, Cleave, Shockwave, Summon, ChargeImpact, ChargeTrail }

        public static readonly Color Violet = new Color(0.72f, 0.16f, 1f);
        public static readonly Color Crimson = new Color(1f, 0.2f, 0.42f);
        public const float CleaveHalfArc = 55f;

        private const float SigilMode = 0, PillarMode = 1, SlashMode = 2, WallMode = 3, ScorchMode = 4;
        private static readonly int AgeId = Shader.PropertyToID("_Age");
        private static readonly int ModeId = Shader.PropertyToID("_Mode");
        private static readonly int ArcId = Shader.PropertyToID("_Arc");
        private static readonly int LifeId = Shader.PropertyToID("_Life");
        private static readonly int DescentId = Shader.PropertyToID("_Descent");
        private static readonly int SweepId = Shader.PropertyToID("_Sweep");
        private static readonly int SeedId = Shader.PropertyToID("_Seed");
        private static Material additive, scorch, bolt;
        private static Mesh cylinder, cleaveFan;

        private struct Layer
        {
            public Renderer Renderer;
            public float Mode, Arc, Delay, Life, Descent, Sweep, Seed;
        }

        private struct Growth
        {
            public Transform Item;
            public Vector3 From, To;
            public float Delay, Duration;
        }

        private readonly List<Layer> layers = new List<Layer>();
        private readonly List<Growth> growth = new List<Growth>();
        private readonly List<(float time, Vector3 point, Vector3 direction)> slashSparks = new List<(float, Vector3, Vector3)>();
        private MaterialPropertyBlock block;
        private ArtStyleLibrary style;
        private System.Random random;
        private float age, lifetime, radius;
        private Light flash;
        private float lightPeak, lightDecay;
        private LineRenderer[] bolts, groundArcs;
        private Vector3 boltTop;
        private float nextReshape, arcLength;

        public static BossStrikeVfx Spawn(ArtStyleLibrary style, Transform parent, Vector3 ground, Vector3 forward, Kind kind, float radius)
        {
            if (!Application.isPlaying || !EnsureMaterials(style)) return null;
            var root = new GameObject("Boss " + kind + " Strike (VFX)");
            root.transform.SetParent(parent, false);
            root.transform.position = ground;
            forward.y = 0;
            root.transform.rotation = Quaternion.LookRotation(forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward);
            var effect = root.AddComponent<BossStrikeVfx>();
            effect.style = style;
            effect.radius = Mathf.Max(0.5f, radius);
            effect.random = new System.Random(root.GetInstanceID());
            effect.block = new MaterialPropertyBlock();
            effect.Build(kind);
            effect.Tick(0);
            return effect;
        }

        private static bool EnsureMaterials(ArtStyleLibrary style)
        {
            if (additive == null || scorch == null)
            {
                Shader shader = UnityEngine.Resources.Load<Shader>("BossStrike");
                if (shader == null) return false;
                additive = new Material(shader) { name = "Boss Strike (Additive)", hideFlags = HideFlags.DontSave };
                scorch = new Material(shader) { name = "Boss Strike (Scorch)", hideFlags = HideFlags.DontSave };
                scorch.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                scorch.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                // Burnt ground sorts under the glowing layers drawn on top of it.
                scorch.renderQueue = 3004;
            }
            if (bolt == null && style != null && style.lightningMaterial != null)
            {
                bolt = new Material(style.lightningMaterial) { name = "Boss Violet Lightning", hideFlags = HideFlags.DontSave };
                bolt.SetColor("_Color", new Color(1.5f, 0.3f, 3.2f, 1f));
            }
            return true;
        }

        private void Build(Kind kind)
        {
            const float full = Mathf.PI;
            float cone = CleaveHalfArc * Mathf.Deg2Rad;
            switch (kind)
            {
                case Kind.Lightning:
                    lifetime = 1.4f;
                    Decal(ScorchMode, radius * 1.5f, full, 0f, 1.4f);
                    Decal(SigilMode, radius * 1.25f, full, 0f, 1.1f);
                    Pillar(radius * 0.32f, 22f, 0f, 0.55f, 0.07f);
                    Bolts(18f, 1.25f);
                    GroundArcs(6, radius * 1.4f);
                    Flash(Vector3.up * 2.2f, 11f, radius * 4f, 0.6f);
                    Burst("Violet Sparks", Vector3.up * 0.2f, 48, new Vector2(4f, 10f), new Vector2(0.3f, 0.8f),
                        new Vector2(0.04f, 0.1f), 1.4f, 70f, Color.white, Violet, true);
                    Embers(26, radius * 0.8f);
                    break;
                case Kind.Cleave:
                    lifetime = 1.1f;
                    Decal(ScorchMode, radius * 1.05f, cone, 0.05f, 1.1f);
                    Decal(SigilMode, radius, cone, 0f, 0.9f);
                    Slash(radius, 0.14f, 0.6f);
                    Flash(Vector3.forward * radius * 0.5f + Vector3.up * 1.6f, 8f, radius * 2.5f, 0.45f);
                    for (int i = 0; i <= 6; i++)
                    {
                        float t = i / 6f;
                        Vector3 direction = Quaternion.Euler(0f, Mathf.Lerp(-CleaveHalfArc, CleaveHalfArc, t), 0f) * Vector3.forward;
                        slashSparks.Add((t * 0.14f, direction * radius * 0.85f + Vector3.up * 0.7f, direction));
                    }
                    Burst("Cleave Sparks", Vector3.forward * radius * 0.55f + Vector3.up * 0.3f, 30, new Vector2(3f, 8f),
                        new Vector2(0.25f, 0.6f), new Vector2(0.035f, 0.08f), 1.2f, 60f, Color.white, Crimson, true);
                    break;
                case Kind.Shockwave:
                    lifetime = 1.5f;
                    Decal(ScorchMode, radius * 0.8f, full, 0f, 1.5f);
                    Decal(SigilMode, radius, full, 0f, 1.2f);
                    Wall(2.4f, 0.6f, radius, 0f, 0.36f, 0.75f);
                    Wall(1.3f, 0.4f, radius * 1.08f, 0.08f, 0.5f, 0.85f);
                    Pillar(1.4f, 7f, 0f, 0.4f, 0.001f);
                    Flash(Vector3.up * 1.5f, 12f, radius * 2f, 0.7f);
                    Burst("Shock Debris", Vector3.up * 0.2f, 56, new Vector2(5f, 11f), new Vector2(0.35f, 0.9f),
                        new Vector2(0.04f, 0.1f), 1.6f, 82f, Color.white, Violet, true);
                    Embers(32, radius * 0.6f);
                    break;
                case Kind.Summon:
                    lifetime = 1.6f;
                    Decal(ScorchMode, radius * 0.9f, full, 0f, 1.6f);
                    Decal(SigilMode, radius, full, 0f, 1.5f);
                    Pillar(1.5f, 20f, 0f, 0.9f, 0.14f);
                    Flash(Vector3.up * 2.5f, 7f, 14f, 1f);
                    Embers(40, radius * 0.7f);
                    break;
                case Kind.ChargeImpact:
                    lifetime = 1f;
                    Decal(ScorchMode, radius * 1.1f, full, 0f, 1f);
                    Decal(SigilMode, radius, full, 0f, 0.8f);
                    Wall(1.6f, 0.4f, radius, 0f, 0.3f, 0.55f);
                    Flash(Vector3.up * 1.4f, 7f, radius * 3f, 0.4f);
                    Burst("Charge Sparks", Vector3.up * 0.2f, 30, new Vector2(3f, 8f), new Vector2(0.25f, 0.6f),
                        new Vector2(0.035f, 0.08f), 1.3f, 75f, Color.white, Violet, true);
                    break;
                case Kind.ChargeTrail:
                    lifetime = 0.9f;
                    Decal(ScorchMode, radius, full, 0f, 0.9f);
                    break;
            }
        }

        private void Decal(float mode, float size, float arc, float delay, float life)
        {
            Renderer renderer = Surface(mode == ScorchMode ? "Scorched Ground" : "Corrupted Sigil", ProceduralMeshes.GroundQuad,
                mode == ScorchMode ? scorch : additive);
            renderer.transform.localPosition = Vector3.up * (mode == ScorchMode ? 0.04f : 0.065f);
            renderer.transform.localScale = new Vector3(size * 2f, 1f, size * 2f);
            AddLayer(renderer, mode, arc, delay, life);
        }

        private void Pillar(float width, float height, float delay, float life, float descent)
        {
            Renderer renderer = Surface("Corrupted Pillar", Cylinder, additive);
            renderer.transform.localScale = new Vector3(width, height, width);
            AddLayer(renderer, PillarMode, Mathf.PI, delay, life, descent);
        }

        private void Slash(float size, float sweep, float life)
        {
            Renderer renderer = Surface("Cleave Crescent", CleaveFan, additive);
            renderer.transform.localScale = new Vector3(size, 1f, size);
            AddLayer(renderer, SlashMode, Mathf.PI, 0f, life, 0f, sweep);
        }

        private void Wall(float height, float from, float to, float delay, float duration, float life)
        {
            Renderer renderer = Surface("Shock Wall", Cylinder, additive);
            renderer.transform.localScale = new Vector3(from, height, from);
            AddLayer(renderer, WallMode, Mathf.PI, delay, life);
            growth.Add(new Growth
            {
                Item = renderer.transform, From = new Vector3(from, height, from), To = new Vector3(to, height * 0.55f, to),
                Delay = delay, Duration = duration
            });
        }

        private void AddLayer(Renderer renderer, float mode, float arc, float delay, float life, float descent = 0.08f, float sweep = 0.14f)
        {
            layers.Add(new Layer
            {
                Renderer = renderer, Mode = mode, Arc = arc, Delay = delay, Life = life,
                Descent = Mathf.Max(0.001f, descent), Sweep = sweep, Seed = (float)random.NextDouble() * 10f
            });
        }

        private Renderer Surface(string label, Mesh mesh, Material material)
        {
            var item = new GameObject(label);
            item.transform.SetParent(transform, false);
            item.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = item.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return renderer;
        }

        private void Flash(Vector3 local, float peak, float range, float decay)
        {
            flash = new GameObject("Corruption Flash").AddComponent<Light>();
            flash.transform.SetParent(transform, false);
            flash.transform.localPosition = local;
            flash.type = LightType.Point;
            flash.color = Color.Lerp(Violet, Crimson, 0.3f);
            flash.range = range;
            flash.shadows = LightShadows.None;
            lightPeak = peak;
            lightDecay = decay;
        }

        private void Bolts(float height, float strength)
        {
            if (bolt == null) return;
            boltTop = Vector3.up * height + new Vector3(Range(-1.5f, 1.5f), 0f, Range(-1.5f, 1.5f));
            bolts = new LineRenderer[7];
            bolts[0] = Line("Bolt Halo", 18, 0.95f * strength);
            bolts[1] = Line("Bolt Core", 18, 0.34f * strength);
            for (int i = 2; i < bolts.Length; i++) bolts[i] = Line("Bolt Fork " + i, 8, 0.13f * strength);
        }

        private void GroundArcs(int count, float length)
        {
            if (bolt == null) return;
            groundArcs = new LineRenderer[count];
            for (int i = 0; i < count; i++) groundArcs[i] = Line("Ground Arc " + i, 9, 0.12f);
            arcLength = length;
        }

        private LineRenderer Line(string label, int points, float width)
        {
            var line = new GameObject(label).AddComponent<LineRenderer>();
            line.transform.SetParent(transform, false);
            line.sharedMaterial = bolt;
            line.useWorldSpace = false;
            line.positionCount = points;
            line.textureMode = LineTextureMode.Stretch;
            line.alignment = LineAlignment.View;
            line.numCapVertices = 2;
            line.widthMultiplier = width;
            line.widthCurve = new AnimationCurve(new Keyframe(0f, 0.55f), new Keyframe(0.85f, 1f), new Keyframe(1f, 0.6f));
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        private float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);

        // Jagged bolt from the sky to the impact point, re-forked a few times during the first frames.
        private void ReshapeBolts()
        {
            if (bolts != null)
            {
                Vector3[] main = new Vector3[18];
                for (int i = 0; i < main.Length; i++)
                {
                    float t = i / (main.Length - 1f);
                    float amplitude = Mathf.Sin(t * Mathf.PI) * 1.3f;
                    main[i] = Vector3.Lerp(boltTop, Vector3.up * 0.05f, t) + new Vector3(Range(-1f, 1f), 0f, Range(-1f, 1f)) * amplitude;
                }
                main[0] = boltTop;
                main[main.Length - 1] = Vector3.up * 0.05f;
                bolts[0].SetPositions(main);
                bolts[1].SetPositions(main);
                for (int b = 2; b < bolts.Length; b++)
                {
                    Vector3 start = main[Mathf.Clamp((int)(main.Length * Range(0.3f, 0.8f)), 1, main.Length - 2)];
                    Vector3 direction = (Vector3.down * Range(0.5f, 1f) + new Vector3(Range(-1f, 1f), 0f, Range(-1f, 1f))).normalized;
                    float length = Range(2f, 4f);
                    for (int i = 0; i < 8; i++)
                    {
                        float t = i / 7f;
                        bolts[b].SetPosition(i, start + direction * (length * t)
                            + new Vector3(Range(-1f, 1f), Range(-0.3f, 0.3f), Range(-1f, 1f)) * (0.35f * t));
                    }
                }
            }
            if (groundArcs == null) return;
            for (int a = 0; a < groundArcs.Length; a++)
            {
                float angle = (a / (float)groundArcs.Length) * Mathf.PI * 2f + Range(-0.35f, 0.35f);
                Vector3 outward = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                Vector3 side = new Vector3(outward.z, 0f, -outward.x);
                float length = arcLength * Range(0.65f, 1f);
                for (int i = 0; i < 9; i++)
                {
                    float t = i / 8f;
                    groundArcs[a].SetPosition(i, outward * (length * t) + side * (Range(-0.45f, 0.45f) * t) + Vector3.up * 0.12f);
                }
            }
        }

        private void Burst(string label, Vector3 local, int count, Vector2 speed, Vector2 life, Vector2 size, float gravity,
            float spread, Color from, Color to, bool stretch)
        {
            Material material = style.auraMotesMaterial;
            if (material == null || count <= 0) return;
            var item = new GameObject(label);
            item.SetActive(false);
            item.transform.SetParent(transform, false);
            item.transform.localPosition = local;
            item.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var system = item.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = system.main;
            main.loop = false;
            main.duration = 0.1f;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startColor = new ParticleSystem.MinMaxGradient(from, to);
            main.gravityModifier = gravity;
            main.maxParticles = count;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = spread;
            shape.radius = 0.4f;
            ParticleSystem.ColorOverLifetimeModule colour = system.colorOverLifetime;
            colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Crimson, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
            colour.color = gradient;
            var renderer = item.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = stretch ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            renderer.lengthScale = 2.2f;
            renderer.velocityScale = 0.07f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            item.SetActive(true);
        }

        // Slow crimson/violet embers drifting up out of the scorched ground after the hit.
        private void Embers(int count, float area)
        {
            Material material = style.auraMotesMaterial;
            if (material == null) return;
            var item = new GameObject("Corruption Embers");
            item.SetActive(false);
            item.transform.SetParent(transform, false);
            item.transform.localPosition = Vector3.up * 0.15f;
            var system = item.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = system.main;
            main.loop = false;
            main.duration = 0.35f;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.5f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.13f);
            main.startColor = new ParticleSystem.MinMaxGradient(Crimson, Violet);
            main.maxParticles = count;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = count / 0.35f;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.rotation = new Vector3(90f, 0f, 0f);
            shape.radius = area;
            ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.8f, 2.2f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
            ParticleSystem.ColorOverLifetimeModule colour = system.colorOverLifetime;
            colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
            colour.color = gradient;
            var renderer = item.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            item.SetActive(true);
        }

        private void Update() => Tick(Time.deltaTime);

        public void Tick(float delta)
        {
            age += Mathf.Max(0f, delta);
            foreach (Layer layer in layers)
            {
                float local = age - layer.Delay;
                bool visible = local >= 0f && local <= layer.Life;
                layer.Renderer.enabled = visible;
                if (!visible) continue;
                block.Clear();
                block.SetFloat(AgeId, local);
                block.SetFloat(ModeId, layer.Mode);
                block.SetFloat(ArcId, layer.Arc);
                block.SetFloat(LifeId, layer.Life);
                block.SetFloat(DescentId, layer.Descent);
                block.SetFloat(SweepId, layer.Sweep);
                block.SetFloat(SeedId, layer.Seed);
                layer.Renderer.SetPropertyBlock(block);
            }
            foreach (Growth item in growth)
            {
                float t = Mathf.Clamp01((age - item.Delay) / Mathf.Max(0.01f, item.Duration));
                item.Item.localScale = Vector3.Lerp(item.From, item.To, 1f - (1f - t) * (1f - t) * (1f - t));
            }
            if (flash != null)
            {
                float rise = Mathf.Clamp01(age / 0.03f);
                float fall = Mathf.Clamp01(1f - age / lightDecay);
                flash.intensity = lightPeak * rise * fall * fall * (age < 0.2f ? 0.8f + 0.2f * Mathf.Sin(age * 140f) : 1f);
            }
            if ((bolts != null || groundArcs != null) && age < 0.22f && age >= nextReshape)
            {
                ReshapeBolts();
                nextReshape = age + 0.045f;
            }
            float strike = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.06f, 0.42f, age));
            float flicker = age < 0.2f ? 0.75f + 0.25f * Mathf.Sin(age * 120f) : 1f;
            if (bolts != null)
            {
                bolts[0].startColor = bolts[0].endColor = new Color(1f, 1f, 1f, 0.45f * strike * flicker);
                bolts[1].startColor = bolts[1].endColor = new Color(1f, 1f, 1f, strike * flicker);
                for (int i = 2; i < bolts.Length; i++)
                    bolts[i].startColor = bolts[i].endColor = new Color(1f, 1f, 1f, Mathf.Clamp01(strike * 1.4f - 0.4f) * flicker);
            }
            if (groundArcs != null)
            {
                float crawl = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.04f, 0.32f, age));
                foreach (LineRenderer arc in groundArcs)
                {
                    arc.startColor = new Color(1f, 1f, 1f, crawl * flicker);
                    arc.endColor = new Color(1f, 1f, 1f, 0f);
                }
            }
            for (int i = slashSparks.Count - 1; i >= 0; i--)
            {
                if (age < slashSparks[i].time) continue;
                CombatEffects.Hit(style, transform.TransformPoint(slashSparks[i].point), transform.TransformDirection(slashSparks[i].direction),
                    Violet, 2f, true);
                slashSparks.RemoveAt(i);
            }
            if (age >= lifetime) Destroy(gameObject);
        }

        // Unit open cylinder (radius 1 at the base flaring to 1.12, height 1); uv.x around, uv.y up.
        // Shared with SpellImpactVfx columns.
        internal static Mesh Cylinder
        {
            get
            {
                if (cylinder != null) return cylinder;
                const int segments = 48;
                var vertices = new Vector3[(segments + 1) * 2];
                var uv = new Vector2[vertices.Length];
                var triangles = new int[segments * 6];
                for (int i = 0; i <= segments; i++)
                {
                    float u = i / (float)segments;
                    Vector3 radial = new Vector3(Mathf.Cos(u * Mathf.PI * 2f), 0f, Mathf.Sin(u * Mathf.PI * 2f));
                    vertices[i * 2] = radial;
                    vertices[i * 2 + 1] = radial * 1.12f + Vector3.up;
                    uv[i * 2] = new Vector2(u, 0f);
                    uv[i * 2 + 1] = new Vector2(u, 1f);
                    if (i == segments) continue;
                    int k = i * 6, v = i * 2;
                    triangles[k] = v; triangles[k + 1] = v + 1; triangles[k + 2] = v + 2;
                    triangles[k + 3] = v + 2; triangles[k + 4] = v + 1; triangles[k + 5] = v + 3;
                }
                cylinder = new Mesh { name = "Boss Strike Cylinder", hideFlags = HideFlags.DontSave };
                cylinder.vertices = vertices; cylinder.uv = uv; cylinder.triangles = triangles;
                cylinder.RecalculateBounds();
                return cylinder;
            }
        }

        // Cleave fan across ±CleaveHalfArc, rising from the ground near the boss to chest height at the outer edge
        // so the crescent reads from the third-person camera. uv.x runs across the arc, uv.y outwards.
        private static Mesh CleaveFan
        {
            get
            {
                if (cleaveFan != null) return cleaveFan;
                const int across = 40, rings = 6;
                var vertices = new Vector3[(across + 1) * (rings + 1)];
                var uv = new Vector2[vertices.Length];
                var triangles = new int[across * rings * 6];
                int k = 0;
                for (int r = 0; r <= rings; r++)
                {
                    float radial = r / (float)rings;
                    float distance = Mathf.Lerp(0.12f, 1f, radial);
                    for (int i = 0; i <= across; i++)
                    {
                        float x = i / (float)across;
                        float angle = Mathf.Lerp(-CleaveHalfArc, CleaveHalfArc, x) * Mathf.Deg2Rad;
                        int index = r * (across + 1) + i;
                        vertices[index] = new Vector3(Mathf.Sin(angle) * distance, Mathf.Lerp(0.3f, 1.1f, radial), Mathf.Cos(angle) * distance);
                        uv[index] = new Vector2(x, radial);
                        if (r == rings || i == across) continue;
                        int a = index, b = index + 1, c = index + across + 1, d = c + 1;
                        triangles[k++] = a; triangles[k++] = c; triangles[k++] = b;
                        triangles[k++] = b; triangles[k++] = c; triangles[k++] = d;
                    }
                }
                cleaveFan = new Mesh { name = "Boss Cleave Fan", hideFlags = HideFlags.DontSave };
                cleaveFan.vertices = vertices; cleaveFan.uv = uv; cleaveFan.triangles = triangles;
                cleaveFan.RecalculateBounds();
                return cleaveFan;
            }
        }
    }
}

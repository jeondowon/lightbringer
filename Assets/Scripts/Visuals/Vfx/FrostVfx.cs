using Lightbringer.Combat;
using UnityEngine;
using UnityEngine.Rendering;

namespace Lightbringer.Visuals
{
    // Shared surfaces for the Ice spell, drawn with the Frost FX shader (Resources/FrostFx). Presentation only:
    // HeroAbilities owns the flight time and StatusEffects owns the freeze.
    public static class FrostVfx
    {
        public const float BoltMode = 0, GlowMode = 1, ShellMode = 2, GroundMode = 3, TrailMode = 4;
        public static readonly Color IceBlue = new Color(0.55f, 0.88f, 1f);
        private static readonly int ModeId = Shader.PropertyToID("_Mode");
        private static readonly int AgeId = Shader.PropertyToID("_Age");
        private static readonly int FadeId = Shader.PropertyToID("_Fade");
        private static readonly int SeedId = Shader.PropertyToID("_Seed");
        private static Material material;
        private static MaterialPropertyBlock block;
        private static Mesh crystalMesh;

        // A flat-shaded crystal with a long, off-centre point, rooted at y = 0.
        public static Mesh Crystal
        {
            get
            {
                if (crystalMesh != null) return crystalMesh;
                var vertices = new System.Collections.Generic.List<Vector3>();
                var triangles = new System.Collections.Generic.List<int>();
                for (int side = 0; side < 5; side++)
                {
                    float a = side * Mathf.PI * 2f / 5f;
                    float b = (side + 1) * Mathf.PI * 2f / 5f;
                    Vector3 bottomA = new Vector3(Mathf.Cos(a) * 0.36f, 0f, Mathf.Sin(a) * 0.36f);
                    Vector3 bottomB = new Vector3(Mathf.Cos(b) * 0.36f, 0f, Mathf.Sin(b) * 0.36f);
                    Vector3 shoulderA = new Vector3(Mathf.Cos(a) * 0.5f, 0.58f + 0.06f * Mathf.Sin(a), Mathf.Sin(a) * 0.5f);
                    Vector3 shoulderB = new Vector3(Mathf.Cos(b) * 0.5f, 0.58f + 0.06f * Mathf.Sin(b), Mathf.Sin(b) * 0.5f);
                    Vector3 tip = new Vector3(0.12f, 1f, -0.08f);
                    Vector3[] face = { bottomA, shoulderA, bottomB, bottomB, shoulderA, shoulderB,
                        shoulderA, tip, shoulderB, bottomA, bottomB, Vector3.zero };
                    foreach (Vector3 point in face) { triangles.Add(vertices.Count); vertices.Add(point); }
                }
                crystalMesh = new Mesh { name = "LB Pointed Frost Crystal", hideFlags = HideFlags.DontSave };
                crystalMesh.SetVertices(vertices);
                crystalMesh.SetTriangles(triangles, 0);
                crystalMesh.RecalculateNormals();
                crystalMesh.RecalculateBounds();
                return crystalMesh;
            }
        }

        public static bool Ready
        {
            get
            {
                if (material != null) return true;
                Shader shader = UnityEngine.Resources.Load<Shader>("FrostFx");
                if (shader == null) return false;
                material = new Material(shader) { name = "LB Frost FX", hideFlags = HideFlags.DontSave };
                block = new MaterialPropertyBlock();
                return true;
            }
        }

        public static MeshRenderer Surface(Transform parent, string label, Mesh mesh)
        {
            var item = new GameObject(label);
            item.transform.SetParent(parent, false);
            item.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = Prepare(item.AddComponent<MeshRenderer>());
            // Spawned after Update: keep the surface invisible until its mode is initialized.
            Apply(renderer, GlowMode, 0f, 0f, 0f);
            return renderer;
        }

        public static TrailRenderer Trail(Transform parent, float time, float width)
        {
            var item = new GameObject("Frost Trail");
            item.transform.SetParent(parent, false);
            TrailRenderer trail = item.AddComponent<TrailRenderer>();
            trail.time = time;
            trail.minVertexDistance = 0.05f;
            trail.widthMultiplier = width;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.05f));
            trail.textureMode = LineTextureMode.Stretch;
            trail.alignment = LineAlignment.View;
            trail.numCapVertices = 2;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = gradient;
            Prepare(trail);
            Apply(trail, TrailMode, 0f, 1f, Random.value * 10f);
            return trail;
        }

        public static void Apply(Renderer renderer, float mode, float age, float fade, float seed)
        {
            if (renderer == null || block == null) return;
            block.Clear();
            block.SetFloat(ModeId, mode);
            block.SetFloat(AgeId, age);
            block.SetFloat(FadeId, fade);
            block.SetFloat(SeedId, seed);
            renderer.SetPropertyBlock(block);
        }

        private static T Prepare<T>(T renderer) where T : Renderer
        {
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return renderer;
        }

        // Turns a quad in the XY plane toward the camera.
        public static void Face(Transform quad)
        {
            UnityEngine.Camera view = UnityEngine.Camera.main;
            if (view != null) quad.rotation = Quaternion.LookRotation(quad.position - view.transform.position, view.transform.up);
        }
    }

    // A frost bolt from the hero's staff to its target: a spinning crystal inside a cold halo, an icy ribbon,
    // snow motes and two orbiting frost sparks. It arcs slightly and arrives exactly when the freeze lands.
    public sealed class IceBoltVfx : MonoBehaviour
    {
        private ArtStyleLibrary style;
        private Combatant target;
        private Vector3 start, lastAim;
        private float travel, age, seed, moteTimer;
        private Transform crystal, orbit;
        private MeshRenderer crystalRenderer, glowRenderer;
        private Light glow;

        public static IceBoltVfx Spawn(ArtStyleLibrary style, Vector3 start, Combatant target, float travel, Transform parent)
        {
            if (!FrostVfx.Ready || target == null) return null;
            var root = new GameObject("Frost Bolt (VFX)");
            root.transform.SetParent(parent, false);
            root.transform.position = start;
            IceBoltVfx bolt = root.AddComponent<IceBoltVfx>();
            bolt.style = style;
            bolt.start = start;
            bolt.target = target;
            bolt.travel = Mathf.Max(0.05f, travel);
            bolt.seed = Random.value * 10f;
            bolt.lastAim = target.GetAimPoint(start);
            bolt.Build();
            CombatEffects.Frost(style, start, 0.6f);
            return bolt;
        }

        private void Build()
        {
            crystal = FrostVfx.Surface(transform, "Frost Wisp", AttackVfx.HaloQuad).transform;
            crystal.localScale = new Vector3(0.7f, 1.1f, 1f);
            crystalRenderer = crystal.GetComponent<MeshRenderer>();
            glowRenderer = FrostVfx.Surface(transform, "Cold Halo", AttackVfx.HaloQuad);
            glowRenderer.transform.localScale = Vector3.one * 1.7f;
            FrostVfx.Apply(crystalRenderer, FrostVfx.GlowMode, 0f, 0.7f, seed + 4f);
            FrostVfx.Apply(glowRenderer, FrostVfx.GlowMode, 0f, 0.65f, seed);
            FrostVfx.Face(crystal);
            FrostVfx.Face(glowRenderer.transform);
            FrostVfx.Trail(transform, 0.28f, 0.42f);
            orbit = new GameObject("Orbit").transform;
            orbit.SetParent(transform, false);
            for (int i = 0; i < 2; i++)
            {
                MeshRenderer spark = FrostVfx.Surface(orbit, "Frost Spark", AttackVfx.HaloQuad);
                spark.transform.localPosition = (i == 0 ? Vector3.right : Vector3.left) * 0.38f;
                spark.transform.localScale = Vector3.one * 0.45f;
                FrostVfx.Apply(spark, FrostVfx.GlowMode, 0f, 0.9f, seed + i);
                FrostVfx.Face(spark.transform);
                TrailRenderer spiral = FrostVfx.Trail(spark.transform, 0.16f, 0.08f);
                spiral.name = "Frost Spiral";
            }
            glow = ShotVfx.Glow(transform, Vector3.zero, FrostVfx.IceBlue, 5f, 2.6f);
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (target != null && target.IsAlive) lastAim = target.GetAimPoint(start);
            float t = Mathf.Clamp01(age / travel);
            // A shallow arc that rises in the middle of the flight.
            Vector3 position = Vector3.Lerp(start, lastAim, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.7f;
            Vector3 heading = position - transform.position;
            transform.position = position;
            FrostVfx.Face(crystal);
            orbit.rotation = Quaternion.LookRotation(heading.sqrMagnitude > 0.0001f ? heading : Vector3.forward) * Quaternion.Euler(0f, 0f, age * 900f);
            FrostVfx.Face(glowRenderer.transform);
            foreach (Transform spark in orbit) FrostVfx.Face(spark);
            float pulse = 1f + 0.15f * Mathf.Sin(age * 28f + seed);
            glowRenderer.transform.localScale = Vector3.one * 1.7f * pulse;
            FrostVfx.Apply(crystalRenderer, FrostVfx.GlowMode, age, 0.7f, seed + 4f);
            FrostVfx.Apply(glowRenderer, FrostVfx.GlowMode, age, 0.65f, seed);
            if (glow != null) glow.intensity = 2.6f * pulse;
            moteTimer -= Time.deltaTime;
            if (moteTimer <= 0f)
            {
                moteTimer = 0.025f;
                CombatEffects.Trail(style, position, FrostVfx.IceBlue);
                CombatEffects.Trail(style, position, Color.white);
            }
            if (t >= 1f) Arrive();
        }

        private void Arrive()
        {
            Vector3 point = transform.position;
            CombatEffects.Frost(style, point, 1.3f);
            FrostEncaseVfx.Attach(style, target, transform.parent);
            // The ribbons finish fading where the bolt struck.
            foreach (TrailRenderer ribbon in GetComponentsInChildren<TrailRenderer>())
            {
                ribbon.transform.SetParent(transform.parent, true);
                ribbon.emitting = false;
                Destroy(ribbon.gameObject, ribbon.time + 0.05f);
            }
            Destroy(gameObject);
        }
    }

    // A frozen unit is encased in ice: a translucent shell over its body, jagged crystals around its feet and frost
    // spreading on the ground. It holds while StatusEffects says the unit is frozen, then shatters into shards.
    public sealed class FrostEncaseVfx : MonoBehaviour
    {
        private const float WaitForFreeze = 0.25f;
        private ArtStyleLibrary style;
        private Combatant target;
        private StatusEffects status;
        private Vector3 offset;
        private float age, seed, height;
        private MeshRenderer[] shells, spikes;
        private MeshRenderer ground;
        private bool seen;

        public static FrostEncaseVfx Attach(ArtStyleLibrary style, Combatant target, Transform parent)
        {
            if (!FrostVfx.Ready || target == null || !target.IsAlive || target.StatusImmune) return null;
            FrostEncaseVfx existing = null;
            foreach (FrostEncaseVfx item in FindObjectsByType<FrostEncaseVfx>(FindObjectsSortMode.None))
                if (item.target == target) existing = item;
            if (existing != null) { existing.age = Mathf.Min(existing.age, 0.3f); return existing; }
            var root = new GameObject("Frozen in Ice (VFX)");
            root.transform.SetParent(parent, false);
            FrostEncaseVfx effect = root.AddComponent<FrostEncaseVfx>();
            effect.style = style;
            effect.target = target;
            effect.seed = Random.value * 10f;
            effect.Build();
            return effect;
        }

        private void Build()
        {
            // Size the ice to the unit's visible body (its renderers), falling back to a soldier-sized block.
            Bounds body = new Bounds(target.transform.position + Vector3.up * 0.9f, new Vector3(0.9f, 1.8f, 0.9f));
            bool found = false;
            foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>())
            {
                if (!(renderer is MeshRenderer || renderer is SkinnedMeshRenderer) || !renderer.enabled) continue;
                if (found) body.Encapsulate(renderer.bounds); else { body = renderer.bounds; found = true; }
            }
            Vector3 feet = HeroSpellVfx.GroundBelow(target.transform.position + Vector3.up * 0.5f);
            height = Mathf.Clamp(body.max.y - feet.y, 0.8f, 6f);
            float width = Mathf.Clamp(Mathf.Max(body.size.x, body.size.z), 0.6f, 5f);
            transform.position = feet;
            offset = feet - target.transform.position;

            // Staggered pointed columns break up the rounded silhouette around the body.
            shells = new MeshRenderer[5];
            for (int i = 0; i < shells.Length; i++)
            {
                shells[i] = FrostVfx.Surface(transform, "Ice Crystal Column", FrostVfx.Crystal);
                float angle = i * 137.5f;
                Vector3 radial = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                float w = width * (i == 0 ? 0.95f : Random.Range(0.42f, 0.62f));
                shells[i].transform.localPosition = i == 0 ? Vector3.zero : radial * width * 0.32f;
                shells[i].transform.localScale = new Vector3(w, height * (i == 0 ? 1.22f : Random.Range(0.7f, 1.1f)), w);
                shells[i].transform.localRotation = Quaternion.Euler(0f, angle, 0f) * Quaternion.Euler(i == 0 ? 0f : Random.Range(8f, 18f), 0f, 0f);
            }
            // Jagged crystals leaning out around the base.
            spikes = new MeshRenderer[7];
            for (int i = 0; i < spikes.Length; i++)
            {
                spikes[i] = FrostVfx.Surface(transform, "Ice Spike", FrostVfx.Crystal);
                float angle = i * 360f / spikes.Length + Random.Range(-18f, 18f);
                Vector3 around = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * width * Random.Range(0.4f, 0.6f);
                spikes[i].transform.localPosition = around;
                spikes[i].transform.localRotation = Quaternion.LookRotation(around) * Quaternion.Euler(Random.Range(20f, 40f), 0f, 0f);
                float size = width * Random.Range(0.18f, 0.3f);
                spikes[i].transform.localScale = new Vector3(size, height * Random.Range(0.35f, 0.6f), size);
            }
            ground = FrostVfx.Surface(transform, "Ground Frost", ProceduralMeshes.GroundQuad);
            ground.transform.localPosition = Vector3.up * 0.05f;
            ground.transform.localScale = Vector3.one * width * 2.6f;
            Apply(0f, 1f);
        }

        private void Apply(float grow, float fade)
        {
            foreach (MeshRenderer shell in shells) FrostVfx.Apply(shell, FrostVfx.ShellMode, age, fade, seed);
            foreach (MeshRenderer spike in spikes) FrostVfx.Apply(spike, FrostVfx.ShellMode, age, fade, seed + 3f);
            FrostVfx.Apply(ground, FrostVfx.GroundMode, age, fade, seed);
            // The ice climbs up the body as it forms.
            transform.localScale = new Vector3(1f, Mathf.Lerp(0.15f, 1f, grow), 1f);
        }

        private void LateUpdate()
        {
            age += Time.deltaTime;
            if (status == null && target != null) target.TryGetComponent(out status);
            bool frozen = target != null && target.IsAlive && status != null && status.IsFrozen;
            if (frozen) seen = true;
            if (!frozen && (seen || age > WaitForFreeze)) { Shatter(); return; }
            if (target != null) transform.position = target.transform.position + offset;
            float grow = 1f - Mathf.Pow(1f - Mathf.Clamp01(age / 0.22f), 3f);
            Apply(grow, 1f);
        }

        private void Shatter()
        {
            if (seen) CombatEffects.IceShatter(style, transform.position + Vector3.up * height * 0.5f, height);
            Destroy(gameObject);
        }
    }
}

using UnityEngine;
using UnityEngine.Rendering;

namespace Lightbringer.Visuals
{
    // A short lightning strike from the sky onto a point: a jagged main bolt that re-forks a few times,
    // side branches, a ground flash and a spark burst. Self-destructs after about one second.
    // Kept brief and bright so large battles stay readable.
    public sealed class LightningStrikeVfx : MonoBehaviour
    {
        private const float BoltLife = 0.45f;
        private const float TotalLife = 1.1f;
        private const float SkyHeight = 14f;
        private const int MainPoints = 18;
        private const int BranchPoints = 7;

        private LineRenderer main;
        private LineRenderer[] branches;
        private Transform flash;
        private Mesh flashMesh;
        private readonly Color[] flashColors = new Color[4];
        private Vector3 top, bottom;
        private float radius;
        private float age;
        private float nextReshape;
        private System.Random random;

        public LineRenderer MainBolt => main;
        public int BranchCount => branches.Length;

        // `strength` 1 = single-target staff bolt, larger values for area spells.
        public static LightningStrikeVfx Spawn(ArtStyleLibrary style, Vector3 impact, float areaRadius, float strength, Transform parent)
        {
            if (style == null || style.lightningMaterial == null) return null;
            GameObject root = new GameObject("Lightning Strike (VFX)");
            root.transform.SetParent(parent, true);
            root.transform.position = impact;
            LightningStrikeVfx strike = root.AddComponent<LightningStrikeVfx>();
            strike.Build(style, impact, areaRadius, strength);
            return strike;
        }

        private void Build(ArtStyleLibrary style, Vector3 impact, float areaRadius, float strength)
        {
            random = new System.Random(GetInstanceID());
            radius = Mathf.Max(0.6f, areaRadius);
            bottom = impact;
            top = impact + Vector3.up * SkyHeight + new Vector3(Range(-2f, 2f), 0f, Range(-2f, 2f));
            main = Line("Bolt", style.lightningMaterial, MainPoints, 0.32f * strength);
            branches = new LineRenderer[strength > 1.2f ? 4 : 2];
            for (int i = 0; i < branches.Length; i++)
                branches[i] = Line("Branch " + i, style.lightningMaterial, BranchPoints, 0.11f * strength);
            if (style.auraMotesMaterial != null)
            {
                flash = new GameObject("Ground Flash").transform;
                flash.SetParent(transform, false);
                flash.position = impact + Vector3.up * 0.05f;
                flashMesh = Instantiate(ProceduralMeshes.GroundQuad);
                flashMesh.hideFlags = HideFlags.DontSave;
                flash.gameObject.AddComponent<MeshFilter>().sharedMesh = flashMesh;
                MeshRenderer renderer = flash.gameObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = style.auraMotesMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                Sparks(style.auraMotesMaterial, impact, Mathf.RoundToInt(22 * strength));
            }
            Reshape();
            Tick(0f);
        }

        private LineRenderer Line(string name, Material material, int points, float width)
        {
            GameObject item = new GameObject(name);
            item.transform.SetParent(transform, false);
            LineRenderer line = item.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.positionCount = points;
            line.textureMode = LineTextureMode.Stretch;
            line.alignment = LineAlignment.View;
            line.numCapVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.widthMultiplier = width;
            line.widthCurve = new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.85f, 1f), new Keyframe(1f, 0.7f));
            return line;
        }

        private void Sparks(Material material, Vector3 impact, int count)
        {
            GameObject item = new GameObject("Sparks");
            item.SetActive(false);
            item.transform.SetParent(transform, false);
            item.transform.position = impact + Vector3.up * 0.1f;
            ParticleSystem system = item.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule settings = system.main;
            settings.loop = false;
            settings.duration = 0.2f;
            settings.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
            settings.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 7f);
            settings.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.14f);
            settings.startColor = new ParticleSystem.MinMaxGradient(Color.white, new Color(0.6f, 0.8f, 1f));
            settings.gravityModifier = 1.6f;
            settings.simulationSpace = ParticleSystemSimulationSpace.World;
            settings.playOnAwake = true;
            settings.maxParticles = 120;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.25f;
            ParticleSystemRenderer renderer = item.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            item.SetActive(true);
        }

        private float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);

        // Jagged bolt: midpoint-style random offsets that are largest mid-air and zero at both ends.
        private void Reshape()
        {
            Vector3 axis = (bottom - top).normalized;
            Vector3 side = Vector3.Cross(axis, Vector3.forward).sqrMagnitude > 0.01f
                ? Vector3.Cross(axis, Vector3.forward).normalized : Vector3.right;
            Vector3 depth = Vector3.Cross(axis, side);
            Vector3[] points = new Vector3[MainPoints];
            for (int i = 0; i < MainPoints; i++)
            {
                float t = i / (MainPoints - 1f);
                float amplitude = Mathf.Sin(t * Mathf.PI) * 1.1f;
                points[i] = Vector3.Lerp(top, bottom, t) + (side * Range(-1f, 1f) + depth * Range(-1f, 1f)) * amplitude;
            }
            points[0] = top;
            points[MainPoints - 1] = bottom;
            main.SetPositions(points);
            for (int b = 0; b < branches.Length; b++)
            {
                int from = Mathf.Clamp((int)(MainPoints * Range(0.35f, 0.8f)), 1, MainPoints - 2);
                Vector3 start = points[from];
                Vector3 direction = (Vector3.down * Range(0.6f, 1f) + side * Range(-1f, 1f) + depth * Range(-1f, 1f)).normalized;
                float length = Range(1.5f, 3f) * (radius > 1.5f ? 1.4f : 1f);
                Vector3[] branch = new Vector3[BranchPoints];
                for (int i = 0; i < BranchPoints; i++)
                {
                    float t = i / (BranchPoints - 1f);
                    branch[i] = start + direction * (length * t) + (side * Range(-1f, 1f) + depth * Range(-1f, 1f)) * (0.25f * t);
                }
                branches[b].SetPositions(branch);
            }
        }

        private void Update() => Tick(Time.deltaTime);

        private void OnDestroy()
        {
            if (flashMesh == null) return;
            if (Application.isPlaying) Destroy(flashMesh); else DestroyImmediate(flashMesh);
        }

        public void Tick(float delta)
        {
            age += delta;
            if (age < 0.2f && age >= nextReshape)
            {
                Reshape();
                nextReshape += 0.05f;
            }
            // Bright flash, quick decay; branches die first.
            float bolt = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.08f, BoltLife, age));
            float flicker = age < 0.2f ? (0.75f + 0.25f * Mathf.Sin(age * 120f)) : 1f;
            Color colour = new Color(1f, 1f, 1f, bolt * flicker);
            main.startColor = main.endColor = colour;
            foreach (LineRenderer branch in branches)
                branch.startColor = branch.endColor = new Color(1f, 1f, 1f, Mathf.Clamp01(bolt * 1.4f - 0.4f) * flicker);
            if (flash != null)
            {
                float grow = Mathf.Lerp(0.6f, 1f, Mathf.Clamp01(age / 0.15f));
                float size = radius * 2.6f * grow;
                flash.localScale = new Vector3(size, 1f, size);
                for (int i = 0; i < flashColors.Length; i++) flashColors[i] = new Color(1f, 1f, 1f, bolt);
                flashMesh.colors = flashColors;
                flash.gameObject.SetActive(bolt > 0.02f);
            }
            if (age >= TotalLife)
            {
                if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
            }
        }
    }
}

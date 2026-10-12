using UnityEngine;
using UnityEngine.Rendering;

namespace Lightbringer.Visuals
{
    // Lightweight procedural surfaces. The shader lives in Resources so standalone builds retain it.
    // Special-attack impacts (lightning, cleave, shockwave, charge, summon) are BossStrikeVfx.
    public sealed class BossVfx : MonoBehaviour
    {
        public enum Shape { Wave, Portal, WarningCircle, WarningCone, WarningLane, Crescent, Collapse, Aura }
        private Material material;
        private Mesh mesh;
        private Shape shape;
        private float age, lifetime, radius;
        private Transform[] shards;
        private Vector3[] velocities;
        private Light glow;
        private Material shroud;
        private Material energy;
        private LineRenderer[] filaments;

        public static BossVfx Spawn(Transform parent, Vector3 position, Shape shape, float radius, float lifetime)
        {
            Shader shader = UnityEngine.Resources.Load<Shader>("BossRift");
            if (shader == null) return null;
            var root = new GameObject("Boss " + shape + " (VFX)");
            root.transform.SetParent(parent, false); root.transform.position = position;
            BossVfx effect = root.AddComponent<BossVfx>();
            effect.shape = shape; effect.radius = radius; effect.lifetime = lifetime;
            effect.material = new Material(shader) { hideFlags = HideFlags.DontSave };
            effect.Build();
            return effect;
        }

        private void Build()
        {
            bool curtain = shape == Shape.Wave || shape == Shape.Portal;
            material.SetFloat("_Mode", curtain ? 1 : shape == Shape.Crescent ? 2 : shape == Shape.Collapse ? 3 : 0);
            material.SetColor("_Color", new Color(0.7f, 0.1f, 0.95f));
            if (shape == Shape.Aura)
            {
                material.SetFloat("_Mode", 7);
                MeshSurface(new[] { new Vector3(-1,0,-1), new Vector3(-1,0,1), new Vector3(1,0,1), new Vector3(1,0,-1) },
                    new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right }, new[] { 0,1,2,0,2,3 });
                SetRadius(radius);
                var veil = new GameObject("Midnight Violet Aura Veil");
                veil.transform.SetParent(transform, false);
                veil.transform.localScale = new Vector3(1.8f, .85f, 1.8f);
                veil.AddComponent<MeshFilter>().sharedMesh = ProceduralMeshes.AuraVeil;
                shroud = new Material(material) { name = "Boss Aura Veil", hideFlags = HideFlags.DontSave };
                shroud.SetFloat("_Mode", 8);
                MeshRenderer renderer = veil.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = shroud;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                return;
            }
            if (shape == Shape.Collapse) { BuildShards(); return; }
            const int segments = 96;
            if (curtain || shape == Shape.Crescent)
            {
                Vector3[] vertices = new Vector3[(segments + 1) * 2];
                Vector2[] uv = new Vector2[vertices.Length];
                int[] triangles = new int[segments * 6];
                for (int i = 0; i <= segments; i++)
                {
                    float t = i / (float)segments;
                    float a = curtain ? t * Mathf.PI * 2 : Mathf.Lerp(-1.35f, 1.35f, t);
                    if (curtain)
                    {
                        Vector3 radial = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                        vertices[i * 2] = radial;
                        vertices[i * 2 + 1] = radial * 1.08f + Vector3.up * (shape == Shape.Wave ? 5 : 12);
                    }
                    else
                    {
                        float taper = Mathf.Sin(t * Mathf.PI);
                        // Sweep across the travel plane. DarkCrescent reverses this local -Z-facing blade,
                        // putting the convex luminous edge ahead and the hollow/afterimages behind it.
                        Vector3 outer = new Vector3(Mathf.Sin(a) * radius, .35f, -Mathf.Cos(a) * radius * .55f);
                        vertices[i * 2] = outer;
                        vertices[i * 2 + 1] = outer + new Vector3(0, -.12f * taper, .85f * taper);
                    }
                    uv[i * 2] = new Vector2(t, 0); uv[i * 2 + 1] = new Vector2(t, 1);
                    if (i == segments) continue;
                    int k = i * 6, v = i * 2;
                    triangles[k] = v; triangles[k + 1] = v + 1; triangles[k + 2] = v + 2;
                    triangles[k + 3] = v + 2; triangles[k + 4] = v + 1; triangles[k + 5] = v + 3;
                }
                MeshSurface(vertices, uv, triangles);
                if (shape == Shape.Crescent) BuildBladeEdge(vertices);
                if (curtain) SetRadius(radius);
                if (shape == Shape.Portal)
                {
                    GameObject shell = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    shell.name = "Dissolving Dark Shroud";
                    Collider solid = shell.GetComponent<Collider>(); solid.enabled = false;
                    if (Application.isPlaying) Destroy(solid); else DestroyImmediate(solid);
                    shell.transform.SetParent(transform, false);
                    shell.transform.localPosition = Vector3.up * 2.5f;
                    shell.transform.localScale = new Vector3(1.2f, 6, 1.2f);
                    shroud = new Material(material) { hideFlags = HideFlags.DontSave };
                    shroud.SetFloat("_Mode", 5);
                    shell.GetComponent<Renderer>().sharedMaterial = shroud;
                    glow = new GameObject("Violet Rift Light").AddComponent<Light>();
                    glow.transform.SetParent(transform, false); glow.transform.localPosition = Vector3.up * 3;
                    glow.color = new Color(0.5f, 0.12f, 1); glow.range = 14; glow.shadows = LightShadows.None;
                }
            }
            else if (shape == Shape.WarningLane)
            {
                // Three narrow surfaces outline a rectangular, forward-facing danger corridor.
                Vector3[] v = { new Vector3(-2.4f,0,0), new Vector3(2.4f,0,0), new Vector3(-2.4f,0,radius), new Vector3(2.4f,0,radius) };
                MeshSurface(v, new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one }, new[] { 0,2,1,1,2,3 });
                material.SetFloat("_Mode", 4);
            }
            else
            {
                Vector3[] vertices = new Vector3[segments + 2];
                Vector2[] uv = new Vector2[vertices.Length];
                int[] triangles = new int[segments * 3]; uv[0] = Vector2.one * .5f;
                float arc = shape == Shape.WarningCone ? 110 : 360;
                for (int i = 0; i <= segments; i++)
                {
                    float a = Mathf.Lerp(-arc * .5f, arc * .5f, i / (float)segments) * Mathf.Deg2Rad;
                    Vector3 radial = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                    vertices[i + 1] = radial * radius; uv[i + 1] = new Vector2(radial.x, radial.z) * .5f + Vector2.one * .5f;
                    if (i < segments) { triangles[i * 3] = 0; triangles[i * 3 + 1] = i + 1; triangles[i * 3 + 2] = i + 2; }
                }
                MeshSurface(vertices, uv, triangles);
            }
        }

        private LineRenderer EnergyLine(string label, int points, float width, bool core = false)
        {
            if (energy == null)
            {
                energy = new Material(material) { name = "Boss Energy Filaments", hideFlags = HideFlags.DontSave };
                energy.SetFloat("_Mode", 6);
            }
            var line = new GameObject(label).AddComponent<LineRenderer>();
            line.transform.SetParent(transform, false);
            line.sharedMaterial = energy;
            line.useWorldSpace = false;
            line.positionCount = points;
            line.widthMultiplier = width;
            line.widthCurve = new AnimationCurve(new Keyframe(0, .2f), new Keyframe(.12f, 1), new Keyframe(.85f, .8f), new Keyframe(1, .05f));
            line.numCornerVertices = 3;
            line.numCapVertices = 3;
            line.textureMode = LineTextureMode.Stretch;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.lightProbeUsage = LightProbeUsage.Off;
            line.reflectionProbeUsage = ReflectionProbeUsage.Off;
            Color tint = core ? new Color(1.6f, .95f, 2f) : new Color(.65f, .12f, 1.25f);
            line.startColor = line.endColor = tint;
            return line;
        }

        private void BuildBladeEdge(Vector3[] vertices)
        {
            int count = vertices.Length / 2;
            filaments = new[] { EnergyLine("Blade Glow", count, .36f), EnergyLine("Blade Core", count, .065f, true) };
            for (int i = 0; i < count; i++)
                foreach (LineRenderer line in filaments) line.SetPosition(i, vertices[i * 2]);
            // A short afterimage of the complete blade, rather than a single thin streak from its centre.
            for (int layer = 0; layer < 2; layer++)
            {
                var echo = new GameObject("Blade Afterimage " + layer);
                echo.transform.SetParent(transform, false);
                echo.transform.localPosition = Vector3.forward * (.2f + layer * .25f);
                echo.transform.localScale = Vector3.one * (1 - .08f * (layer + 1));
                echo.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer renderer = echo.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                var block = new MaterialPropertyBlock();
                block.SetFloat("_Opacity", .2f / (layer + 1));
                renderer.SetPropertyBlock(block);
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        private void MeshSurface(Vector3[] vertices, Vector2[] uv, int[] triangles)
        {
            mesh = new Mesh { name = "Boss " + shape, hideFlags = HideFlags.DontSave };
            mesh.vertices = vertices; mesh.uv = uv; mesh.triangles = triangles; mesh.RecalculateBounds();
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        }

        private void BuildShards()
        {
            shards = new Transform[18]; velocities = new Vector3[shards.Length];
            for (int i = 0; i < shards.Length; i++)
            {
                GameObject shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Collider solid = shard.GetComponent<Collider>(); solid.enabled = false;
                if (Application.isPlaying) Destroy(solid); else DestroyImmediate(solid);
                shard.transform.SetParent(transform, false);
                float a = i * 2.4f;
                shard.transform.localPosition = new Vector3(Mathf.Sin(a) * 2, i % 4, Mathf.Cos(a) * 2);
                shard.transform.localScale = Vector3.one * (0.5f + (i % 3) * .4f);
                shard.GetComponent<Renderer>().sharedMaterial = material;
                shards[i] = shard.transform;
                velocities[i] = new Vector3(Mathf.Sin(a) * 5, 4 + i % 5, Mathf.Cos(a) * 5);
            }
        }

        public void SetRadius(float value) => transform.localScale = new Vector3(Mathf.Max(.01f, value), 1, Mathf.Max(.01f, value));
        public void SetProgress(float value) { if (material != null) material.SetFloat("_Progress", Mathf.Clamp01(value)); }

        private void Update()
        {
            float delta = Time.deltaTime;
            age += delta;
            material.SetFloat("_Age", age);
            if (energy != null)
            {
                energy.SetFloat("_Age", age);
                float fade = lifetime > 0 ? Mathf.Clamp01(1 - age / lifetime) : 1;
                energy.SetFloat("_Opacity", fade);
            }
            if (shroud != null)
            {
                shroud.SetFloat("_Age", age);
                shroud.SetFloat("_Progress", Mathf.Clamp01(age / Mathf.Max(.1f, lifetime * .72f)));
            }
            if (lifetime > 0)
            {
                float fade = Mathf.Clamp01((lifetime - age) / Mathf.Min(0.5f, lifetime * .5f));
                material.SetFloat("_Opacity", fade);
                if (shape == Shape.Portal) transform.Rotate(0, delta * 25, 0, Space.Self);
                if (glow != null) glow.intensity = fade * (2 + Mathf.Sin(age * 9) * .5f);
                if (age >= lifetime) { Remove(); return; }
            }
            if (shards == null) return;
            for (int i = 0; i < shards.Length; i++)
            {
                velocities[i] += Vector3.down * (9 * delta);
                Vector3 p = shards[i].localPosition + velocities[i] * delta;
                if (p.y < 0.2f) { p.y = .2f; velocities[i] = Vector3.zero; }
                shards[i].localPosition = p;
                if (velocities[i].sqrMagnitude > .01f) shards[i].Rotate(new Vector3(60, 35, 80) * delta);
            }
        }

        public void Remove()
        {
            gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
        }

        private void OnDestroy()
        {
            if (mesh != null) { if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); }
            if (material != null) { if (Application.isPlaying) Destroy(material); else DestroyImmediate(material); }
            if (shroud != null) { if (Application.isPlaying) Destroy(shroud); else DestroyImmediate(shroud); }
            if (energy != null) { if (Application.isPlaying) Destroy(energy); else DestroyImmediate(energy); }
        }
    }
}

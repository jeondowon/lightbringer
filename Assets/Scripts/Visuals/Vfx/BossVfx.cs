using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Lightbringer.Visuals
{
    // Lightweight procedural surfaces. The shaders live in Resources so standalone builds retain them.
    // Warnings use the Boss Telegraph shader: a darkened floor with a runic rim, thorns, glowing fissures and a charge
    // front, a curtain of corrupted flame along the edge, a pulsing light and rising motes, plus optional sky lances
    // and falling orbs. Special-attack impacts (lightning, cleave, shockwave, charge, summon) are BossStrikeVfx.
    public sealed class BossVfx : MonoBehaviour
    {
        public enum Shape { Wave, Portal, WarningCircle, WarningCone, WarningLane, Crescent, Collapse, Aura }
        public const float LaneHalfWidth = 2.4f;
        private static readonly int ModeId = Shader.PropertyToID("_Mode");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
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
        private ArtStyleLibrary style;
        private Material floorShade;
        private Mesh curtainMesh;
        private float progress, moteTimer, orbHeight;
        private Transform orb;
        private Renderer orbSurface, orbTrail;
        private MaterialPropertyBlock orbBlock, trailBlock;

        private bool IsWarning => IsTelegraph(shape);
        private static bool IsTelegraph(Shape value) => value == Shape.WarningCircle || value == Shape.WarningCone || value == Shape.WarningLane;

        public static BossVfx Spawn(Transform parent, Vector3 position, Shape shape, float radius, float lifetime, ArtStyleLibrary style = null)
        {
            Shader shader = UnityEngine.Resources.Load<Shader>(IsTelegraph(shape) ? "BossTelegraph" : "BossRift");
            if (shader == null) return null;
            var root = new GameObject("Boss " + shape + " (VFX)");
            root.transform.SetParent(parent, false); root.transform.position = position;
            BossVfx effect = root.AddComponent<BossVfx>();
            effect.shape = shape; effect.radius = radius; effect.lifetime = lifetime; effect.style = style;
            effect.material = new Material(shader) { hideFlags = HideFlags.DontSave };
            effect.Build();
            return effect;
        }

        private void Build()
        {
            if (IsWarning) { BuildTelegraph(); return; }
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
                DropCollider(shell);
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

        // The telegraph is built in metres (the root is never scaled): the shadow and glow floor layers, the flame
        // curtain along the boundary and a light under the area that swells with the wind-up.
        private void BuildTelegraph()
        {
            material.name = "Boss Telegraph Glow";
            material.SetFloat("_Shape", shape == Shape.WarningCone ? 1 : shape == Shape.WarningLane ? 2 : 0);
            material.SetFloat("_Radius", radius);
            material.SetFloat("_Arc", BossStrikeVfx.CleaveHalfArc * Mathf.Deg2Rad);
            material.SetFloat("_Width", LaneHalfWidth * 2);
            material.SetFloat("_Seed", Random.Range(0f, 10f));
            material.SetFloat(ModeId, 1);
            material.renderQueue = 3004;
            floorShade = new Material(material) { name = "Boss Telegraph Shadow", hideFlags = HideFlags.DontSave };
            floorShade.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            floorShade.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            floorShade.SetFloat(ModeId, 0);
            floorShade.renderQueue = 3003;
            mesh = FloorMesh();
            Part("Corruption Shadow", mesh, floorShade, -1);
            Part("Runic Telegraph", mesh, material, -1);
            float height = shape == Shape.WarningLane ? 1.1f : Mathf.Clamp(radius * .2f, .7f, 1.5f);
            curtainMesh = CurtainMesh(height);
            Part("Corruption Curtain", curtainMesh, material, 2);
            glow = new GameObject("Telegraph Light").AddComponent<Light>();
            glow.transform.SetParent(transform, false);
            float centre = shape == Shape.WarningCircle ? 0 : radius * (shape == Shape.WarningLane ? .5f : .45f);
            glow.transform.localPosition = new Vector3(0, 1.2f, centre);
            glow.type = LightType.Point;
            glow.color = Color.Lerp(BossStrikeVfx.Crimson, BossStrikeVfx.Violet, .45f);
            glow.range = Mathf.Clamp(radius * 1.4f, 4, 14);
            glow.shadows = LightShadows.None;
            glow.intensity = 0;
        }

        // A lance of falling corruption with a storm eye overhead, for strikes that come down from the sky.
        public void AddSkyLance(float height, float width)
        {
            if (!IsWarning) return;
            Part("Sky Lance", BossStrikeVfx.Cylinder, material, 3).transform.localScale = new Vector3(width, height, width);
            Part("Sky Lance Core", BossStrikeVfx.Cylinder, material, 3).transform.localScale = new Vector3(width * .35f, height, width * .35f);
            Renderer eye = Part("Storm Eye", ProceduralMeshes.GroundQuad, material, 4);
            eye.transform.localPosition = Vector3.up * height * .92f;
            eye.transform.localScale = new Vector3(width * 16, 1, width * 16);
        }

        // An abyss orb that drops from the sky and lands exactly as the wind-up completes.
        public void AddFallingOrb(float height, float size)
        {
            if (!IsWarning) return;
            orbHeight = height;
            GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "Abyss Orb";
            DropCollider(ball);
            ball.transform.SetParent(transform, false);
            ball.transform.localPosition = Vector3.up * height;
            ball.transform.localScale = Vector3.one * size;
            orb = ball.transform;
            orbSurface = ball.GetComponent<Renderer>();
            orbSurface.sharedMaterial = floorShade;
            orbSurface.shadowCastingMode = ShadowCastingMode.Off;
            orbSurface.receiveShadows = false;
            orbBlock = new MaterialPropertyBlock();
            orbTrail = Part("Abyss Orb Trail", BossStrikeVfx.Cylinder, material, 6);
            orbTrail.transform.SetParent(orb, false);
            orbTrail.transform.localScale = new Vector3(.42f, 8f / Mathf.Max(.1f, size), .42f);
            trailBlock = new MaterialPropertyBlock();
            PlaceOrb();
        }

        private Renderer Part(string label, Mesh surface, Material shared, float mode)
        {
            var item = new GameObject(label);
            item.transform.SetParent(transform, false);
            item.AddComponent<MeshFilter>().sharedMesh = surface;
            var renderer = item.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = shared;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            if (mode >= 0)
            {
                var block = new MaterialPropertyBlock();
                block.SetFloat(ModeId, mode);
                renderer.SetPropertyBlock(block);
            }
            return renderer;
        }

        // Circle and cone floors map uv to the radius around the caster; the lane maps uv.x across and uv.y along.
        private Mesh FloorMesh()
        {
            bool lane = shape == Shape.WarningLane;
            float half = lane ? LaneHalfWidth : radius;
            float near = shape == Shape.WarningCircle ? -radius : 0;
            Vector3[] corners = { new Vector3(-half, 0, near), new Vector3(-half, 0, radius), new Vector3(half, 0, radius), new Vector3(half, 0, near) };
            var uv = new Vector2[4];
            for (int i = 0; i < 4; i++)
                uv[i] = lane ? new Vector2(corners[i].x / (2 * half) + .5f, corners[i].z / radius)
                    : new Vector2(corners[i].x / (2 * radius) + .5f, corners[i].z / (2 * radius) + .5f);
            var surface = new Mesh { name = "Boss Telegraph Floor", hideFlags = HideFlags.DontSave };
            surface.vertices = corners;
            surface.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            surface.uv = uv;
            surface.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            surface.RecalculateBounds();
            return surface;
        }

        // A vertical strip along the outline: uv.x in metres along it, uv.y up; each segment has an outward normal.
        private Mesh CurtainMesh(float height)
        {
            const float inset = .04f;
            var outline = new List<Vector3>();
            if (shape == Shape.WarningLane)
            {
                float x = LaneHalfWidth - inset, z = radius - inset;
                outline.Add(new Vector3(-x, 0, .5f)); outline.Add(new Vector3(-x, 0, z));
                outline.Add(new Vector3(x, 0, z)); outline.Add(new Vector3(x, 0, .5f));
            }
            else
            {
                bool cone = shape == Shape.WarningCone;
                float arc = cone ? BossStrikeVfx.CleaveHalfArc : 180;
                int steps = cone ? 32 : 72;
                if (cone) outline.Add(Vector3.zero);
                for (int i = 0; i <= steps; i++)
                {
                    float a = Mathf.Lerp(-arc, arc, i / (float)steps) * Mathf.Deg2Rad;
                    outline.Add(new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * (radius - inset));
                }
                if (cone) outline.Add(Vector3.zero);
            }
            int count = outline.Count - 1;
            var vertices = new Vector3[count * 4];
            var normals = new Vector3[vertices.Length];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[count * 6];
            float along = 0;
            for (int s = 0; s < count; s++)
            {
                Vector3 a = outline[s], b = outline[s + 1];
                float length = Vector3.Distance(a, b);
                Vector3 outward = Vector3.Cross(Vector3.up, b - a).normalized;
                int v = s * 4;
                vertices[v] = a; vertices[v + 1] = a + Vector3.up * height;
                vertices[v + 2] = b; vertices[v + 3] = b + Vector3.up * height;
                uv[v] = new Vector2(along, 0); uv[v + 1] = new Vector2(along, 1);
                uv[v + 2] = new Vector2(along + length, 0); uv[v + 3] = new Vector2(along + length, 1);
                for (int n = 0; n < 4; n++) normals[v + n] = outward;
                int k = s * 6;
                triangles[k] = v; triangles[k + 1] = v + 1; triangles[k + 2] = v + 2;
                triangles[k + 3] = v + 2; triangles[k + 4] = v + 1; triangles[k + 5] = v + 3;
                along += length;
            }
            var surface = new Mesh { name = "Boss Telegraph Curtain", hideFlags = HideFlags.DontSave };
            surface.vertices = vertices; surface.normals = normals; surface.uv = uv; surface.triangles = triangles;
            surface.RecalculateBounds();
            return surface;
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
                DropCollider(shard);
                shard.transform.SetParent(transform, false);
                float a = i * 2.4f;
                shard.transform.localPosition = new Vector3(Mathf.Sin(a) * 2, i % 4, Mathf.Cos(a) * 2);
                shard.transform.localScale = Vector3.one * (0.5f + (i % 3) * .4f);
                shard.GetComponent<Renderer>().sharedMaterial = material;
                shards[i] = shard.transform;
                velocities[i] = new Vector3(Mathf.Sin(a) * 5, 4 + i % 5, Mathf.Cos(a) * 5);
            }
        }

        private static void DropCollider(GameObject item)
        {
            Collider solid = item.GetComponent<Collider>(); solid.enabled = false;
            if (Application.isPlaying) Destroy(solid); else DestroyImmediate(solid);
        }

        public void SetRadius(float value) => transform.localScale = new Vector3(Mathf.Max(.01f, value), 1, Mathf.Max(.01f, value));

        public void SetProgress(float value)
        {
            progress = Mathf.Clamp01(value);
            if (material != null) material.SetFloat("_Progress", progress);
            if (floorShade != null) floorShade.SetFloat("_Progress", progress);
        }

        private void Update()
        {
            float delta = Time.deltaTime;
            age += delta;
            material.SetFloat("_Age", age);
            if (IsWarning) { TickTelegraph(delta); return; }
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

        private void TickTelegraph(float delta)
        {
            floorShade.SetFloat("_Age", age);
            float urgency = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.65f, 1, progress));
            if (glow != null)
                glow.intensity = (.4f + 3.2f * progress * progress) * (1 + urgency * .35f * Mathf.Sin(age * Mathf.Lerp(5, 26, urgency)))
                    * Mathf.Clamp01(age / .2f);
            if (orb != null) PlaceOrb();
            if (style == null) return;
            moteTimer -= delta;
            if (moteTimer > 0) return;
            moteTimer = .05f;
            // Corruption motes seep up out of the area and embers flare off its edge, thickening with the wind-up.
            float area = shape == Shape.WarningLane ? LaneHalfWidth * 2 * radius
                : radius * radius * (shape == Shape.WarningCone ? BossStrikeVfx.CleaveHalfArc * Mathf.Deg2Rad : Mathf.PI);
            int count = Mathf.Clamp(Mathf.RoundToInt(area * .02f * (.4f + progress)), 1, 5);
            for (int i = 0; i < count; i++)
                CombatEffects.AuraMote(style, transform.TransformPoint(RandomPoint(false)), Random.value < .5f ? BossStrikeVfx.Crimson : BossStrikeVfx.Violet, 0);
            CombatEffects.Trail(style, transform.TransformPoint(RandomPoint(true) + Vector3.up * Random.Range(.1f, .5f)), BossStrikeVfx.Crimson);
            if (orb != null && progress > 0) CombatEffects.Fireball(style, orb.position, BossStrikeVfx.Violet, orb.localScale.x * .9f);
        }

        // The orb accelerates down so that it meets the ground as the wind-up completes; it fades in as it starts.
        private void PlaceOrb()
        {
            orb.localPosition = Vector3.up * (orbHeight * (1 - progress * progress) + orb.localScale.y * .35f);
            orb.Rotate(0, 140 * Time.deltaTime, 0, Space.Self);
            float fade = Mathf.SmoothStep(0, 1, progress / .12f);
            orbBlock.SetFloat(ModeId, 5); orbBlock.SetFloat(OpacityId, fade);
            orbSurface.SetPropertyBlock(orbBlock);
            trailBlock.SetFloat(ModeId, 6); trailBlock.SetFloat(OpacityId, fade);
            orbTrail.SetPropertyBlock(trailBlock);
        }

        private Vector3 RandomPoint(bool edge)
        {
            if (shape == Shape.WarningLane)
            {
                float x = edge ? (Random.value < .5f ? -LaneHalfWidth : LaneHalfWidth) : Random.Range(-LaneHalfWidth, LaneHalfWidth);
                return new Vector3(x, .1f, Random.Range(.5f, radius));
            }
            float arc = shape == Shape.WarningCone ? BossStrikeVfx.CleaveHalfArc : 180;
            float a = Random.Range(-arc, arc) * Mathf.Deg2Rad;
            float r = edge ? radius : radius * Mathf.Sqrt(Random.value);
            return new Vector3(Mathf.Sin(a) * r, .1f, Mathf.Cos(a) * r);
        }

        public void Remove()
        {
            gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
        }

        private void OnDestroy()
        {
            Release(mesh); Release(curtainMesh);
            Release(material); Release(shroud); Release(energy); Release(floorShade);
        }

        private static void Release(Object item)
        {
            if (item == null) return;
            if (Application.isPlaying) Destroy(item); else DestroyImmediate(item);
        }
    }
}

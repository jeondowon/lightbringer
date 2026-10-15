using System.Collections.Generic;
using Lightbringer.Combat;
using UnityEngine;
using UnityEngine.Rendering;

namespace Lightbringer.Visuals
{
    // The abyss core (FinalBattle.md): the Demon King's heart, fallen to the ruined stronghold. A floating cluster of
    // obsidian crystal whose crimson cracks beat like a heart (the same red as the eyes in the sky, and nowhere else on
    // the field), shards orbiting it, navy smoke coiling round it and up into the stream that joins it to the sky.
    // The beat quickens as health falls; a blow flashes the cracks and throws the shards out, charging draws them in, and
    // death shatters the crystal. Presentation only: health, collider and targeting stay on the core's Combatant.
    public sealed class AbyssCoreVisual : MonoBehaviour
    {
        // The crystal's tip, where the stream to the sky takes root (DemonKingSky.CoreTip).
        public const float Tip = 7.3f;
        private const float Float = 1.7f;
        private static readonly Color Heart = new Color(1, .07f, .04f);
        private static readonly Color Navy = new Color(.028f, .032f, .085f);
        private static readonly Color Indigo = new Color(.07f, .05f, .155f);

        private readonly List<Object> owned = new List<Object>();
        private Combatant core;
        private DemonKingSky sky;
        private Material crystal, shardStone, smoke, wisps, halo;
        private Transform cluster, shroud, coil;
        private Shard[] shards;
        private Light glow;
        private float clock, phase, beat, flash, jolt;

        private struct Shard
        {
            public Transform Item;
            public float Radius, Height, Angle, Speed, Bob;
            public Vector3 Spin;
        }

        public static AbyssCoreVisual Build(Transform parent, Combatant core, DemonKingSky sky)
        {
            Shader stone = UnityEngine.Resources.Load<Shader>("AbyssCore"), vapour = UnityEngine.Resources.Load<Shader>("DemonSmoke");
            if (stone == null || vapour == null) return null;
            var item = new GameObject("Abyss Core (Visual)");
            item.transform.SetParent(parent, false);
            AbyssCoreVisual visual = item.AddComponent<AbyssCoreVisual>();
            visual.core = core; visual.sky = sky;
            visual.Create(stone, vapour);
            if (core != null) { core.Damaged += visual.OnDamaged; core.Died += visual.OnDied; }
            return visual;
        }

        private void Create(Shader stone, Shader vapour)
        {
            crystal = Own(new Material(stone) { name = "Abyss Core Crystal" });
            shardStone = Own(new Material(stone) { name = "Abyss Core Shards" });
            shardStone.SetFloat("_Veins", .55f);
            smoke = Smoke(vapour, "Abyss Core Shroud", 0, 3010);
            smoke.SetFloat("_Billboard", 1);
            smoke.SetFloat("_Glow", .25f);
            wisps = Smoke(vapour, "Abyss Core Coil", 1, 3011);
            wisps.SetFloat("_Glow", .3f);
            halo = Smoke(vapour, "Abyss Core Heart Glow", 3, 3012);
            halo.SetFloat("_Billboard", 1);
            halo.SetFloat("_Push", 2.2f);
            var random = new System.Random(66013);

            cluster = new GameObject("Crystal Cluster").transform;
            cluster.SetParent(transform, false);
            cluster.localPosition = Vector3.up * Float;
            Solid("Heart Crystal", cluster, Crystal(7, 5.4f, 1.15f, random), crystal, Vector3.zero, Quaternion.identity);
            // Two lesser crystals grown out of its base, leaning away, so the silhouette is a cluster, not a gem.
            Solid("Lesser Crystal", cluster, Crystal(6, 3.1f, .62f, random), crystal, new Vector3(.75f, .25f, .3f), Quaternion.Euler(8, 0, -27));
            Solid("Lesser Crystal", cluster, Crystal(6, 2.5f, .5f, random), crystal, new Vector3(-.65f, .35f, -.35f), Quaternion.Euler(-12, 40, 31));

            shards = new Shard[7];
            for (int i = 0; i < shards.Length; i++)
            {
                Mesh mesh = Crystal(5, Lerp(random, .9f, 1.7f), Lerp(random, .22f, .38f), random);
                shards[i] = new Shard
                {
                    Item = Solid("Orbiting Shard", transform, mesh, shardStone, Vector3.zero, Quaternion.identity),
                    Radius = Lerp(random, 2.4f, 3.3f), Height = Lerp(random, 1.4f, 6.4f), Angle = i * Mathf.PI * 2 / shards.Length + Lerp(random, -.3f, .3f),
                    Speed = (i % 2 == 0 ? 1 : -1) * Lerp(random, .3f, .55f), Bob = Lerp(random, 0, 6.28f),
                    Spin = new Vector3(Lerp(random, -40, 40), Lerp(random, -60, 60), Lerp(random, -40, 40))
                };
            }

            // Smoke coiling round the foot of the cluster, always facing the camera.
            shroud = new GameObject("Smoke Shroud").transform;
            shroud.SetParent(transform, false);
            var puffs = new List<(Vector3, float, Color, float)>();
            for (int i = 0; i < 16; i++)
            {
                float angle = Lerp(random, 0, Mathf.PI * 2), radius = Lerp(random, 1.4f, 3.2f);
                Color tone = Color.Lerp(Navy, Indigo, Lerp(random, 0, .6f));
                tone.a = Lerp(random, .5f, .8f);
                puffs.Add((new Vector3(Mathf.Cos(angle) * radius, Lerp(random, .2f, 2.6f), Mathf.Sin(angle) * radius), Lerp(random, 2.4f, 4.2f), tone, (float)random.NextDouble()));
            }
            Billboards("Shroud Puffs", shroud, puffs, smoke, .55f);
            Billboards("Heart Glow", cluster, new List<(Vector3, float, Color, float)> { (Vector3.up * 2.6f, 9, Color.white, .5f) }, halo, 0);

            // Three dark bands spiral up round the crystal into the stream to the sky.
            coil = new GameObject("Smoke Coil").transform;
            coil.SetParent(transform, false);
            for (int i = 0; i < 3; i++) Helix(coil, i * Mathf.PI * 2 / 3, i * .31f);

            var lamp = new GameObject("Heart Light");
            lamp.transform.SetParent(cluster, false);
            lamp.transform.localPosition = Vector3.up * 2.6f;
            glow = lamp.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(1, .16f, .12f);
            glow.range = 11;
            glow.shadows = LightShadows.None;
            Tick(.01f);
        }

        private void Update() => Tick(Time.deltaTime);

        public void Tick(float delta)
        {
            if (delta <= 0) return;
            clock += delta;
            float health = core != null && core.MaximumHealth > 0 ? Mathf.Clamp01(core.CurrentHealth / core.MaximumHealth) : 1;
            float charge = sky != null ? sky.Charge : 0;
            // A double heartbeat that quickens as the Demon King weakens and while it gathers a spell.
            phase += delta * Mathf.Lerp(.75f, 1.55f, 1 - health) * (1 + charge * .7f);
            float cycle = phase - Mathf.Floor(phase);
            beat = Pulse(cycle) + .55f * Pulse(cycle - .2f);
            flash = Mathf.Max(0, flash - delta * 4);
            jolt = Mathf.Max(0, jolt - delta * 2.5f);

            Vector3 shake = new Vector3(Mathf.Sin(clock * 53), 0, Mathf.Cos(clock * 47)) * jolt * .12f;
            cluster.localPosition = Vector3.up * (Float + Mathf.Sin(clock * .8f) * .15f) + shake;
            cluster.localRotation = Quaternion.Euler(0, clock * (7 + charge * 30), 0);
            for (int i = 0; i < shards.Length; i++)
            {
                Shard shard = shards[i];
                shard.Angle += shard.Speed * delta * (1 + charge * 3);
                shards[i] = shard;
                float radius = shard.Radius * (1 - charge * .3f) + jolt * .9f;
                shard.Item.localPosition = new Vector3(Mathf.Cos(shard.Angle) * radius, shard.Height + Mathf.Sin(clock * .7f + shard.Bob) * .3f, Mathf.Sin(shard.Angle) * radius);
                shard.Item.localRotation = Quaternion.Euler(shard.Spin * clock);
            }
            shroud.localRotation = Quaternion.Euler(0, clock * 9, 0);
            coil.localRotation = Quaternion.Euler(0, -clock * (28 + charge * 60), 0);

            foreach (Material material in new[] { crystal, shardStone })
            {
                material.SetFloat("_Heart", beat);
                material.SetFloat("_Flash", flash);
                material.SetFloat("_Charge", charge);
            }
            smoke.SetFloat("_Charge", charge);
            wisps.SetFloat("_Glow", .3f + charge * .8f + flash * .6f);
            halo.SetColor("_Color", Heart * (.22f + beat * .3f + flash * .7f + charge * .35f));
            glow.intensity = 1.1f + beat * 2.2f + flash * 4 + charge * 1.5f;
        }

        private static float Pulse(float x) => x < 0 ? 0 : Mathf.Exp(-Sq(x / .07f));

        private void OnDamaged(Combatant unit, float amount, Combatant attacker)
        {
            if (amount <= 0 || unit.MaximumHealth <= 0) return;
            // Small blows from a whole army flicker; a heavy blow flares and throws the shards wide.
            float weight = Mathf.Clamp(amount / (unit.MaximumHealth * .015f), .3f, 1);
            flash = Mathf.Max(flash, weight);
            jolt = Mathf.Max(jolt, weight);
        }

        private void OnDied(Combatant unit)
        {
            if (!Application.isPlaying || unit.transform.parent == null) return;
            AbyssCoreShatter.Spawn(unit.transform.parent, cluster.position, cluster.rotation);
        }

        private Mesh Own(Mesh mesh) { mesh.hideFlags = HideFlags.DontSave; owned.Add(mesh); return mesh; }
        private Material Own(Material material) { material.hideFlags = HideFlags.DontSave; owned.Add(material); return material; }

        private Material Smoke(Shader shader, string label, float mode, int queue)
        {
            Material material = Own(new Material(shader) { name = label, renderQueue = queue });
            material.SetFloat("_Mode", mode);
            material.SetColor("_RimColor", new Color(.2f, .09f, .36f));
            material.SetColor("_GlowColor", new Color(.5f, .2f, .95f));
            return material;
        }

        private Mesh Crystal(int sides, float height, float radius, System.Random random) => Own(CrystalMesh(sides, height, radius, random));

        // A faceted crystal: bottom point, three irregular rings (narrow, widest, tapering), top point; flat shaded.
        public static Mesh CrystalMesh(int sides, float height, float radius, System.Random random)
        {
            float[] heights = { .2f, .55f, .82f }, widths = { .72f, 1, .58f };
            var rings = new Vector3[3, sides];
            for (int r = 0; r < 3; r++)
                for (int s = 0; s < sides; s++)
                {
                    float angle = (s + (r % 2) * .5f + Lerp(random, -.15f, .15f)) * Mathf.PI * 2 / sides;
                    float width = radius * widths[r] * Lerp(random, .85f, 1.12f);
                    rings[r, s] = new Vector3(Mathf.Cos(angle) * width, height * (heights[r] + Lerp(random, -.04f, .04f)), Mathf.Sin(angle) * width);
                }
            Vector3 bottom = Vector3.zero, top = new Vector3(Lerp(random, -.08f, .08f) * radius, height, Lerp(random, -.08f, .08f) * radius);
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            // The faces below are listed counter-clockwise from outside; Unity's front faces wind clockwise.
            void Face(Vector3 a, Vector3 b, Vector3 c)
            {
                triangles.Add(vertices.Count); vertices.Add(a);
                triangles.Add(vertices.Count); vertices.Add(c);
                triangles.Add(vertices.Count); vertices.Add(b);
            }
            for (int s = 0; s < sides; s++)
            {
                int n = (s + 1) % sides;
                Face(bottom, rings[0, n], rings[0, s]);
                for (int r = 0; r < 2; r++)
                {
                    Face(rings[r, s], rings[r, n], rings[r + 1, s]);
                    Face(rings[r, n], rings[r + 1, n], rings[r + 1, s]);
                }
                Face(rings[2, s], rings[2, n], top);
            }
            var mesh = new Mesh { name = "Abyss Crystal" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private Transform Solid(string label, Transform parent, Mesh mesh, Material material, Vector3 position, Quaternion rotation)
        {
            var item = new GameObject(label);
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            item.transform.localRotation = rotation;
            item.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = item.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return item.transform;
        }

        // Camera-facing quads (DemonSmoke _Billboard): TEXCOORD2 carries each centre and size.
        private void Billboards(string label, Transform parent, List<(Vector3 centre, float size, Color color, float seed)> quads, Material material, float drift)
        {
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var uv = new List<Vector2>();
            var data = new List<Vector4>();
            var centres = new List<Vector4>();
            var triangles = new List<int>();
            Vector2[] corners = { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            foreach (var quad in quads)
            {
                int start = vertices.Count;
                foreach (Vector2 corner in corners)
                {
                    vertices.Add(quad.centre + (Vector3)(corner * 2 - Vector2.one) * quad.size * .5f);
                    colors.Add(quad.color); uv.Add(corner);
                    data.Add(new Vector4(quad.seed, drift, .5f, 0));
                    centres.Add(new Vector4(quad.centre.x, quad.centre.y, quad.centre.z, quad.size));
                }
                triangles.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
            }
            Mesh mesh = Own(new Mesh { name = "Abyss Core " + label });
            mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetUVs(0, uv); mesh.SetUVs(1, data); mesh.SetUVs(2, centres);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            Bounds bounds = mesh.bounds;
            bounds.Expand(4);
            mesh.bounds = bounds;
            Transparent(label, parent, mesh, material);
        }

        // A dark band spiralling up from the ground round the crystal and narrowing into the stream.
        private void Helix(Transform parent, float offset, float seed)
        {
            const int segments = 40;
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var uv = new List<Vector2>();
            var data = new List<Vector4>();
            var triangles = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float angle = offset + t * Mathf.PI * 2.6f, radius = Mathf.Lerp(2.4f, .7f, t), width = Mathf.Lerp(1.1f, .35f, t);
                Vector3 centre = new Vector3(Mathf.Cos(angle) * radius, Mathf.Lerp(.3f, Tip + .4f, t), Mathf.Sin(angle) * radius);
                vertices.Add(centre - Vector3.up * width * .5f);
                vertices.Add(centre + Vector3.up * width * .5f);
                Color color = Navy;
                color.a = .75f * Mathf.SmoothStep(0, 1, t / .15f) * Mathf.SmoothStep(0, 1, (1 - t) / .2f);
                colors.Add(color); colors.Add(color);
                uv.Add(new Vector2(0, t)); uv.Add(new Vector2(1, t));
                data.Add(new Vector4(seed, 0, .6f, 0)); data.Add(new Vector4(seed, 0, .6f, 0));
                if (i == segments) break;
                int v = i * 2;
                triangles.AddRange(new[] { v, v + 2, v + 1, v + 1, v + 2, v + 3 });
            }
            Mesh mesh = Own(new Mesh { name = "Abyss Core Coil" });
            mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetUVs(0, uv); mesh.SetUVs(1, data); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            Transparent("Coil Band", parent, mesh, wisps);
        }

        private static void Transparent(string label, Transform parent, Mesh mesh, Material material)
        {
            var item = new GameObject(label);
            item.transform.SetParent(parent, false);
            item.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = item.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static float Sq(float value) => value * value;
        private static float Lerp(System.Random random, float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());

        private void OnDestroy()
        {
            if (core != null) { core.Damaged -= OnDamaged; core.Died -= OnDied; }
            foreach (Object item in owned)
                if (item != null) { if (Application.isPlaying) Destroy(item); else DestroyImmediate(item); }
            owned.Clear();
        }
    }

    // The core breaking: crystal fragments burst outward and tumble down, a crimson flash and a ring of smoke.
    public sealed class AbyssCoreShatter : MonoBehaviour
    {
        private const float Lifetime = 2.6f;
        private readonly List<Object> owned = new List<Object>();
        private readonly List<(Transform item, Vector3 velocity, Vector3 spin, float scale)> fragments = new List<(Transform, Vector3, Vector3, float)>();
        private Material stone, smoke, flare;
        private Transform burst, flash;
        private Light flareLight;
        private float age;

        public static void Spawn(Transform parent, Vector3 centre, Quaternion rotation)
        {
            Shader crystal = UnityEngine.Resources.Load<Shader>("AbyssCore"), vapour = UnityEngine.Resources.Load<Shader>("DemonSmoke");
            if (crystal == null || vapour == null) return;
            var item = new GameObject("Abyss Core Shatter (VFX)");
            item.transform.SetParent(parent, false);
            item.transform.position = centre;
            item.AddComponent<AbyssCoreShatter>().Create(crystal, vapour, rotation);
            Lightbringer.Audio.SoundEffects.World(Lightbringer.Audio.SoundCue.BossImpact, centre);
            CameraSystem.ThirdPersonCamera.Shake(centre, .7f);
        }

        private void Create(Shader crystal, Shader vapour, Quaternion rotation)
        {
            stone = Own(new Material(crystal) { name = "Abyss Shatter Stone" });
            smoke = Own(new Material(vapour) { name = "Abyss Shatter Smoke", renderQueue = 3010 });
            smoke.SetFloat("_Mode", 0); smoke.SetFloat("_Billboard", 1); smoke.SetFloat("_Glow", .4f);
            smoke.SetColor("_RimColor", new Color(.2f, .09f, .36f)); smoke.SetColor("_GlowColor", new Color(.5f, .2f, .95f));
            flare = Own(new Material(vapour) { name = "Abyss Shatter Flash", renderQueue = 3012 });
            flare.SetFloat("_Mode", 3); flare.SetFloat("_Billboard", 1);
            var random = new System.Random(4099);
            for (int i = 0; i < 16; i++)
            {
                Mesh mesh = Own(AbyssCoreVisual.CrystalMesh(5, Lerp(random, .5f, 1.5f), Lerp(random, .18f, .42f), random));
                var item = new GameObject("Crystal Fragment");
                item.transform.SetParent(transform, false);
                item.transform.localPosition = rotation * new Vector3(Lerp(random, -.8f, .8f), Lerp(random, .3f, 5), Lerp(random, -.8f, .8f));
                item.transform.localRotation = RandomRotation(random);
                item.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer renderer = item.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = stone;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                float angle = Lerp(random, 0, Mathf.PI * 2), speed = Lerp(random, 5, 11);
                fragments.Add((item.transform, new Vector3(Mathf.Cos(angle) * speed, Lerp(random, 4, 10), Mathf.Sin(angle) * speed),
                    new Vector3(Lerp(random, -400, 400), Lerp(random, -400, 400), Lerp(random, -400, 400)), 1));
            }
            var puffs = new List<(Vector3, float, Color, float)>();
            for (int i = 0; i < 14; i++)
            {
                float angle = i * Mathf.PI * 2 / 14;
                Color tone = Color.Lerp(new Color(.028f, .032f, .085f), new Color(.07f, .05f, .155f), Lerp(random, 0, .6f));
                tone.a = .75f;
                puffs.Add((new Vector3(Mathf.Cos(angle) * 1.2f, Lerp(random, .5f, 3.5f), Mathf.Sin(angle) * 1.2f), Lerp(random, 3, 5), tone, (float)random.NextDouble()));
            }
            burst = Quads("Smoke Burst", puffs, smoke);
            flash = Quads("Flash", new List<(Vector3, float, Color, float)> { (Vector3.up * 2.6f, 4, Color.white, .5f) }, flare);
            var host = new GameObject("Shatter Light");
            host.transform.SetParent(transform, false);
            host.transform.localPosition = Vector3.up * 2.6f;
            flareLight = host.AddComponent<Light>();
            flareLight.type = LightType.Point;
            flareLight.color = new Color(1, .2f, .14f);
            flareLight.range = 16;
            flareLight.shadows = LightShadows.None;
            Tick(.001f);
        }

        private void Update() => Tick(Time.deltaTime);

        private void Tick(float delta)
        {
            age += delta;
            for (int i = 0; i < fragments.Count; i++)
            {
                var fragment = fragments[i];
                fragment.velocity += Vector3.down * 14 * delta;
                Vector3 position = fragment.item.localPosition + fragment.velocity * delta;
                // Fragments skip once on the ground and lie there as they fade.
                if (position.y < .1f) { position.y = .1f; fragment.velocity = new Vector3(fragment.velocity.x * .4f, -fragment.velocity.y * .25f, fragment.velocity.z * .4f); fragment.spin *= .4f; }
                fragment.item.localPosition = position;
                fragment.item.localRotation *= Quaternion.Euler(fragment.spin * delta);
                fragment.item.localScale = Vector3.one * Mathf.Clamp01((Lifetime - age) / 1.2f);
                fragments[i] = fragment;
            }
            float burstT = Mathf.Clamp01(age / Lifetime);
            burst.localScale = Vector3.one * (1 + burstT * 2.6f);
            smoke.SetFloat("_Presence", 1 - burstT);
            smoke.SetColor("_Color", new Color(1, 1, 1, 1 - burstT));
            float flashT = Mathf.Clamp01(age / .45f);
            flash.localScale = Vector3.one * (1 + flashT * 3.5f);
            flare.SetColor("_Color", new Color(1, .08f, .05f) * 3 * (1 - flashT) * (1 - flashT));
            stone.SetFloat("_Flash", Mathf.Clamp01(1 - age * 2));
            stone.SetFloat("_Heart", Mathf.Clamp01(1 - age));
            flareLight.intensity = 9 * Mathf.Clamp01(1 - age / .7f);
            if (age >= Lifetime) Destroy(gameObject);
        }

        private Transform Quads(string label, List<(Vector3 centre, float size, Color color, float seed)> quads, Material material)
        {
            var vertices = new List<Vector3>(); var colors = new List<Color>(); var uv = new List<Vector2>();
            var data = new List<Vector4>(); var centres = new List<Vector4>(); var triangles = new List<int>();
            Vector2[] corners = { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            foreach (var quad in quads)
            {
                int start = vertices.Count;
                foreach (Vector2 corner in corners)
                {
                    vertices.Add(quad.centre + (Vector3)(corner * 2 - Vector2.one) * quad.size * .5f);
                    colors.Add(quad.color); uv.Add(corner);
                    data.Add(new Vector4(quad.seed, .3f, .5f, 0));
                    centres.Add(new Vector4(quad.centre.x, quad.centre.y, quad.centre.z, quad.size));
                }
                triangles.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
            }
            Mesh mesh = Own(new Mesh { name = "Abyss Shatter " + label });
            mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetUVs(0, uv); mesh.SetUVs(1, data); mesh.SetUVs(2, centres);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            Bounds bounds = mesh.bounds; bounds.Expand(6); mesh.bounds = bounds;
            var item = new GameObject(label);
            item.transform.SetParent(transform, false);
            item.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = item.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return item.transform;
        }

        private T Own<T>(T item) where T : Object { item.hideFlags = HideFlags.DontSave; owned.Add(item); return item; }
        private static Quaternion RandomRotation(System.Random random) =>
            Quaternion.Euler(Lerp(random, 0, 360), Lerp(random, 0, 360), Lerp(random, 0, 360));
        private static float Lerp(System.Random random, float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());

        private void OnDestroy()
        {
            foreach (Object item in owned) if (item != null) Destroy(item);
            owned.Clear();
        }
    }
}

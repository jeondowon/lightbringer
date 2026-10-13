using System.Collections.Generic;
using Lightbringer.Combat;
using UnityEngine;
using UnityEngine.Rendering;

namespace Lightbringer.Visuals
{
    // The Warlord's shockwave when the citadel falls: like the hero's staff shockwave, but violet and indigo and
    // running to the edge of the field. A terrain-following ring (glowing crest, dim wake, low curtain of dark light),
    // violet sparks along the front, and the summoned allies it catches thrown back as posed copies of their models
    // before they fade. Presentation only: BossEncounter owns the radius, the kills and their timing.
    public sealed class BossShockwaveVfx : MonoBehaviour
    {
        public static readonly Color Violet = new Color(0.62f, 0.3f, 1f);
        private static readonly Color Indigo = new Color(0.22f, 0.2f, 0.75f);
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int CentreId = Shader.PropertyToID("_Centre");
        // Ground band rows in metres behind the leading edge (negative is ahead of it); the wake fades over ~16 m.
        private static readonly float[] Rows = { -2.5f, -0.8f, 0f, 0.6f, 1.8f, 4f, 8f, 16f, 30f, 48f };
        private static readonly float[] CurtainRows = { 0f, 0.25f, 0.55f, 1f };
        private const int Segments = 320;
        private const float GroundLift = 0.12f, FieldFade = 14f, FadeSeconds = 0.9f;

        private struct Body
        {
            public Transform Item;
            public Vector3 Velocity, Spin;
            public float Floor, Age, RestAge, Size;
            public bool Resting, Fading;
        }

        private readonly List<Body> bodies = new List<Body>();
        private readonly List<Mesh> baked = new List<Mesh>();
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector4> uvs = new List<Vector4>();
        private readonly List<int> triangles = new List<int>();
        private MaterialPropertyBlock block;
        private ArtStyleLibrary style;
        private BattlefieldEnvironment environment;
        private Transform field;
        private Material ring, sparkMaterial;
        private Mesh mesh;
        private ParticleSystem sparks;
        private Light flash;
        private Vector3 centre;
        private float maxRadius, front, opacity = 1f, age, fadeAge = -1f;
        private bool built;

        public static BossShockwaveVfx Spawn(ArtStyleLibrary style, Transform field, Vector3 groundPoint, float maxRadius)
        {
            if (!Application.isPlaying || style == null) return null;
            Shader shader = UnityEngine.Resources.Load<Shader>("BossShockwave");
            if (shader == null) return null;
            var root = new GameObject("Boss Shockwave (VFX)");
            root.transform.SetParent(field, false);
            root.transform.position = groundPoint;
            BossShockwaveVfx vfx = root.AddComponent<BossShockwaveVfx>();
            vfx.style = style;
            vfx.field = field;
            vfx.centre = groundPoint;
            vfx.maxRadius = maxRadius;
            vfx.environment = field.GetComponentInChildren<BattlefieldEnvironment>();
            vfx.Build(shader);
            return vfx;
        }

        // The leading edge, kept on the radius that is killing allies.
        public void SetFront(float radius) => front = Mathf.Clamp(Mathf.Max(front, radius), 0f, maxRadius);

        private void Build(Shader shader)
        {
            ring = new Material(shader) { name = "Boss Shockwave", hideFlags = HideFlags.DontSave };
            ring.SetVector(CentreId, centre);
            ring.renderQueue = 3005;
            mesh = new Mesh { name = "Boss Shockwave Ring", hideFlags = HideFlags.DontSave, indexFormat = IndexFormat.UInt32 };
            mesh.MarkDynamic();
            var item = new GameObject("Shock Ring");
            item.transform.SetParent(transform, false);
            item.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = item.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = ring;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            sparks = Sparks();
            flash = new GameObject("Shockwave Flash").AddComponent<Light>();
            flash.transform.SetParent(transform, false);
            flash.transform.localPosition = Vector3.up * 4f;
            flash.type = LightType.Point;
            flash.color = Violet;
            flash.range = 45f;
            flash.shadows = LightShadows.None;
            flash.intensity = 0f;
            built = true;

            // The blast itself, the same vocabulary as the hero's staff shockwave.
            CombatEffects.Burst(style, centre + Vector3.up * 2f, Violet, 6f);
            CombatEffects.Ring(centre, Violet, 14f);
            Lightbringer.Audio.SoundEffects.World(Lightbringer.Audio.SoundCue.ShockwaveBoom, centre);
        }

        private ParticleSystem Sparks()
        {
            Material source = style.vfxMaterial;
            if (source == null) return null;
            sparkMaterial = new Material(source) { name = "Boss Shockwave Sparks", hideFlags = HideFlags.DontSave };
            sparkMaterial.SetFloat("_Shape", 0f);
            sparkMaterial.SetFloat("_Softness", 2.4f);
            sparkMaterial.SetFloat("_Additive", 1f);
            sparkMaterial.SetColor("_Color", new Color(3f, 3f, 3f));
            var item = new GameObject("Violet Sparks");
            item.SetActive(false);
            item.transform.SetParent(transform, false);
            var system = item.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = system.main;
            main.loop = true; // emission is off; looping keeps hand-emitted particles simulating
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 1500;
            main.startSpeed = 0f;
            main.gravityModifier = 0.45f;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;
            ParticleSystem.NoiseModule noise = system.noise;
            noise.enabled = true;
            noise.strength = 0.8f;
            noise.frequency = 0.7f;
            ParticleSystem.ColorOverLifetimeModule colour = system.colorOverLifetime;
            colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(new Color(0.92f, 0.82f, 1f), 0f), new GradientColorKey(Violet, 0.35f),
                    new GradientColorKey(Indigo, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.55f), new GradientAlphaKey(0f, 1f) });
            colour.color = gradient;
            var renderer = item.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = sparkMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.04f;
            renderer.lengthScale = 1.6f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            item.SetActive(true);
            system.Play();
            return system;
        }

        // ---------------------------------------------------------------- field

        private Vector3 FieldLocal(Vector3 world) => field != null ? field.InverseTransformPoint(world) : world;

        // How far outside the playable field a point lies (0 inside); the ring dissolves into the hills beyond it.
        private float Outside(Vector3 world)
        {
            Vector3 local = FieldLocal(world);
            return BattlefieldEnvironment.OutsideDistance(local.x, local.z);
        }

        private float GroundHeight(Vector3 world)
        {
            if (environment == null) return centre.y;
            Vector3 local = environment.transform.InverseTransformPoint(world);
            return environment.transform.TransformPoint(new Vector3(local.x, environment.SurfaceHeight(local.x, local.z), local.z)).y;
        }

        private static Vector3 Around(float angle) => new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));

        // ---------------------------------------------------------------- caught allies

        // Throws a posed copy of the ally's model back from the blast; the real unit is killed right after.
        public void Fling(Combatant unit)
        {
            if (unit == null || !built) return;
            Vector3 position = unit.transform.position;
            Vector3 outward = position - centre;
            outward.y = 0f;
            outward = outward.sqrMagnitude > 0.01f ? outward.normalized : Vector3.back;
            CombatEffects.Burst(style, position + Vector3.up * 0.4f, Violet, 1.2f);
            Transform visual = unit.transform.Find(UnitAppearance.StyledName);
            if (visual == null) visual = unit.transform.Find(UnitAppearance.GreyboxName);
            if (visual == null) return;

            var body = new GameObject("Flung " + unit.name).transform;
            body.SetParent(transform, true);
            body.SetPositionAndRotation(position, unit.transform.rotation);
            if (block == null) block = new MaterialPropertyBlock();
            int pieces = 0;
            foreach (Renderer source in visual.GetComponentsInChildren<Renderer>())
            {
                // Bodies, armour and weapons cast shadows; blessing glows and other VFX quads do not.
                if (!source.enabled || source.shadowCastingMode == ShadowCastingMode.Off) continue;
                Mesh shape;
                Vector3 scale = Vector3.one;
                if (source is SkinnedMeshRenderer skinned)
                {
                    if (skinned.sharedMesh == null) continue;
                    shape = new Mesh { name = "Flung Pose", hideFlags = HideFlags.DontSave };
                    skinned.BakeMesh(shape, true);
                    baked.Add(shape);
                }
                else if (source is MeshRenderer && source.TryGetComponent(out MeshFilter filter) && filter.sharedMesh != null)
                {
                    shape = filter.sharedMesh;
                    scale = source.transform.lossyScale;
                }
                else continue;
                var piece = new GameObject(source.name);
                piece.transform.SetParent(body, false);
                piece.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                piece.transform.localScale = scale;
                piece.AddComponent<MeshFilter>().sharedMesh = shape;
                var renderer = piece.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = source.sharedMaterials;
                // Team tint and other per-renderer overrides travel with the copy.
                source.GetPropertyBlock(block);
                if (!block.isEmpty) renderer.SetPropertyBlock(block);
                for (int i = 0; i < source.sharedMaterials.Length; i++)
                {
                    source.GetPropertyBlock(block, i);
                    if (!block.isEmpty) renderer.SetPropertyBlock(block, i);
                }
                renderer.shadowCastingMode = ShadowCastingMode.On;
                pieces++;
            }
            if (pieces == 0) { Destroy(body.gameObject); return; }

            // Hurled up and back, tumbling over backwards (away from the blast).
            Vector3 tumble = Vector3.Cross(Vector3.up, outward);
            bodies.Add(new Body
            {
                Item = body,
                Velocity = outward * Random.Range(8f, 13f) + Vector3.up * Random.Range(5.5f, 8.5f),
                Spin = tumble * Random.Range(380f, 620f) + Random.insideUnitSphere * 120f,
                Floor = GroundHeight(position) + 0.35f,
                Size = 1f
            });
        }

        private void MoveBodies(float delta)
        {
            for (int i = bodies.Count - 1; i >= 0; i--)
            {
                Body body = bodies[i];
                if (body.Item == null) { bodies.RemoveAt(i); continue; }
                body.Age += delta;
                Vector3 p = body.Item.position;
                if (!body.Resting)
                {
                    body.Velocity += Physics.gravity * (1.4f * delta);
                    p += body.Velocity * delta;
                    body.Item.Rotate(body.Spin * delta, Space.World);
                    body.Floor = GroundHeight(p) + 0.35f;
                    if (p.y <= body.Floor && body.Velocity.y < 0f)
                    {
                        p.y = body.Floor;
                        if (body.Velocity.y < -3f) CombatEffects.DustRing(p - Vector3.up * 0.3f, 0.5f, 5);
                        body.Velocity = new Vector3(body.Velocity.x * 0.45f, -body.Velocity.y * 0.25f, body.Velocity.z * 0.45f);
                        body.Spin *= 0.35f;
                        if (body.Velocity.y < 1.5f) { body.Resting = true; body.RestAge = body.Age; }
                    }
                }
                // A moment on the ground, then the fallen ally fades out as light like any other fallen ally.
                if (!body.Fading && ((body.Resting && body.Age - body.RestAge > 0.35f) || body.Age > 2.6f))
                {
                    body.Fading = true;
                    body.RestAge = body.Age;
                    CombatEffects.Death(style, p + Vector3.up * 0.3f, CombatEffects.HolyGold, 1f);
                }
                if (body.Fading)
                {
                    float shrink = 1f - Mathf.Clamp01((body.Age - body.RestAge) / 0.3f);
                    if (shrink <= 0f) { Destroy(body.Item.gameObject); bodies.RemoveAt(i); continue; }
                    body.Item.localScale = Vector3.one * shrink;
                }
                body.Item.position = p;
                bodies[i] = body;
            }
        }

        // ---------------------------------------------------------------- ring

        private void RebuildRing()
        {
            vertices.Clear();
            uvs.Clear();
            triangles.Clear();
            if (opacity <= 0f || front <= 0.05f) { mesh.Clear(); return; }
            float curtain = Mathf.Lerp(5.5f, 2.5f, front / Mathf.Max(maxRadius, 1f));
            int columns = Segments + 1;
            for (int c = 0; c < columns; c++)
            {
                Vector3 direction = Around(c * Mathf.PI * 2f / Segments);
                for (int r = 0; r < Rows.Length; r++)
                {
                    float radius = Mathf.Max(0f, front - Rows[r]);
                    Vector3 world = centre + direction * radius;
                    world.y = GroundHeight(world) + GroundLift;
                    vertices.Add(transform.InverseTransformPoint(world));
                    uvs.Add(new Vector4(Rows[r], 0f, 0f, 1f - Mathf.Clamp01(Outside(world) / FieldFade)));
                }
            }
            int curtainStart = vertices.Count;
            for (int c = 0; c < columns; c++)
            {
                Vector3 world = centre + Around(c * Mathf.PI * 2f / Segments) * front;
                float ground = GroundHeight(world) + GroundLift;
                float fade = 1f - Mathf.Clamp01(Outside(world) / FieldFade);
                for (int r = 0; r < CurtainRows.Length; r++)
                {
                    world.y = ground + CurtainRows[r] * curtain;
                    vertices.Add(transform.InverseTransformPoint(world));
                    uvs.Add(new Vector4(0f, CurtainRows[r], 1f, fade));
                }
            }
            Strip(0, Rows.Length);
            Strip(curtainStart, CurtainRows.Length);
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
        }

        private void Strip(int start, int rows)
        {
            for (int c = 0; c < Segments; c++)
                for (int r = 0; r < rows - 1; r++)
                {
                    int a = start + c * rows + r, b = a + rows;
                    triangles.Add(a); triangles.Add(b); triangles.Add(a + 1);
                    triangles.Add(a + 1); triangles.Add(b); triangles.Add(b + 1);
                }
        }

        // Violet sparks thrown up off the crest, only along the part of the front that crosses the field.
        private void EmitSparks(float delta)
        {
            if (sparks == null || opacity <= 0f || front <= 0.5f) return;
            int count = Mathf.Min(Mathf.RoundToInt(front * Mathf.PI * 2f * 0.5f * delta + Random.value), 40);
            for (int i = 0; i < count; i++)
            {
                Vector3 direction = Around(Random.Range(0f, Mathf.PI * 2f));
                Vector3 point = centre + direction * front;
                if (Outside(point) > 2f) continue;
                point.y = GroundHeight(point) + 0.2f;
                var emit = new ParticleSystem.EmitParams
                {
                    position = point,
                    velocity = direction * Random.Range(3f, 8f) + Vector3.up * Random.Range(3f, 9f),
                    startSize = Random.Range(0.05f, 0.12f), startLifetime = Random.Range(0.5f, 1f),
                    startColor = Color.white, randomSeed = (uint)Random.Range(1, int.MaxValue)
                };
                sparks.Emit(emit, 1);
            }
        }

        // ---------------------------------------------------------------- timeline

        private void Update()
        {
            float delta = Time.deltaTime;
            if (delta <= 0f || !built) return;
            age += delta;
            if (fadeAge < 0f && front >= maxRadius - 0.01f) fadeAge = 0f;
            if (fadeAge >= 0f) { fadeAge += delta; opacity = 1f - Mathf.Clamp01(fadeAge / FadeSeconds); }
            ring.SetFloat(OpacityId, opacity);
            if (flash != null) flash.intensity = 14f * Mathf.Clamp01(age / 0.05f) * Mathf.Exp(-age / 0.45f);
            RebuildRing();
            EmitSparks(delta);
            MoveBodies(delta);
            if ((opacity <= 0f && bodies.Count == 0 && age > 2f) || age > 14f) Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (ring != null) Destroy(ring);
            if (sparkMaterial != null) Destroy(sparkMaterial);
            if (mesh != null) Destroy(mesh);
            foreach (Mesh item in baked) if (item != null) Destroy(item);
            baked.Clear();
        }
    }
}

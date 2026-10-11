using System.Collections.Generic;
using Lightbringer.CameraSystem;
using Lightbringer.Combat;
using UnityEngine;
using UnityEngine.Rendering;

namespace Lightbringer.Visuals
{
    // Feedback on the attacking unit: dresses ranged shots (a fletched arrow with a faint streak, or a glowing
    // magic bolt with a light trail), bursts them on impact, and adds shock rings to charges and splash slams.
    // Presentation only; the Projectile and UnitCombat own timing and damage.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnitCombat))]
    public sealed class AttackVfx : MonoBehaviour
    {
        private const float StuckArrowLife = 1.4f;

        [SerializeField] private ArtStyleLibrary style;
        private UnitCombat combat;
        private bool allied;

        private static readonly Dictionary<long, Mesh> meshes = new Dictionary<long, Mesh>();
        private static readonly Dictionary<long, Material> trails = new Dictionary<long, Material>();

        public void Configure(ArtStyleLibrary library) => style = library;

        private void OnEnable()
        {
            combat = GetComponent<UnitCombat>();
            combat.ProjectileLaunched += OnLaunched;
            combat.StrikeLanded += OnStrikeLanded;
            combat.ChargeLanded += OnChargeLanded;
        }

        private void OnDisable()
        {
            if (combat == null) return;
            combat.ProjectileLaunched -= OnLaunched;
            combat.StrikeLanded -= OnStrikeLanded;
            combat.ChargeLanded -= OnChargeLanded;
        }

        private Color Tint => allied ? CombatEffects.AlliedLight : CombatEffects.Corruption;

        private void OnLaunched(Projectile shot)
        {
            if (!Application.isPlaying || style == null || !style.HasMaterials || shot == null) return;
            allied = shot.Faction == Faction.Allied;
            Material body = allied ? style.alliedMaterial : style.enemyMaterial;
            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(shot.transform, false);
            AddRenderer(visual, shot.Kind == ProjectileKind.Arrow ? ArrowMesh(style, allied) : BoltMesh(style, allied), body);
            TrailRenderer trail = AddTrail(shot.transform, shot.Kind);
            if (shot.Kind == ProjectileKind.Bolt)
            {
                visual.AddComponent<BoltFlicker>().Configure(style, Tint, HaloMaterial(style, Tint, allied));
                CombatEffects.Muzzle(style, shot.transform.position, Tint);
            }
            Color tint = Tint;
            bool bolt = shot.Kind == ProjectileKind.Bolt;
            float splash = bolt ? combat.SplashRadius : 0f;
            ArtStyleLibrary library = style;
            shot.Landed += (projectile, point, struck) =>
            {
                if (trail != null)
                {
                    // Let the streak fade out where the shot ended instead of vanishing with it.
                    trail.transform.SetParent(projectile.transform.parent, true);
                    trail.emitting = false;
                    Destroy(trail.gameObject, trail.time + 0.05f);
                }
                if (bolt)
                {
                    CombatEffects.Burst(library, point, tint, splash);
                    return;
                }
                // Arrows stick in what they hit for a moment (or in the ground where the target fell).
                visual.transform.SetParent(struck != null ? struck.transform : projectile.transform.parent, true);
                visual.transform.position = point + projectile.transform.forward * 0.18f;
                Destroy(visual, StuckArrowLife);
            };
        }

        private void OnStrikeLanded(Combatant target, Vector3 point)
        {
            // Splash blows (Warlord slam, Dragon breath) shake the ground around the impact.
            if (!Application.isPlaying || style == null || combat.SplashRadius <= 0f) return;
            bool friendly = GetComponent<Combatant>().Faction == Faction.Allied;
            Color color = friendly ? CombatEffects.HolyGold : CombatEffects.Corruption;
            CombatEffects.Ring(target != null ? target.transform.position : point, color, combat.SplashRadius);
            ThirdPersonCamera.Shake(point, 0.3f);
        }

        private void OnChargeLanded()
        {
            if (!Application.isPlaying || style == null) return;
            Vector3 front = transform.position + transform.forward * 1f;
            CombatEffects.Ring(front, CombatEffects.HolyGold, 1.8f);
            CombatEffects.Hit(style, front + Vector3.up * 0.4f, transform.forward, CombatEffects.SteelSpark, 2.5f, true);
            CombatEffects.DustRing(HeroSpellVfx.GroundBelow(front + Vector3.up * 0.5f), 0.8f, 8);
            ThirdPersonCamera.Shake(front, 0.5f);
        }

        private TrailRenderer AddTrail(Transform parent, ProjectileKind kind)
        {
            Material material = TrailMaterial(style);
            if (material == null) return null;
            GameObject item = new GameObject("Trail");
            item.transform.SetParent(parent, false);
            TrailRenderer trail = item.AddComponent<TrailRenderer>();
            trail.sharedMaterial = material;
            bool bolt = kind == ProjectileKind.Bolt;
            trail.time = bolt ? 0.3f : 0.16f;
            trail.minVertexDistance = 0.1f;
            trail.widthMultiplier = bolt ? 0.38f : 0.08f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
            trail.textureMode = LineTextureMode.Stretch;
            trail.alignment = LineAlignment.View;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            Color head = bolt ? Tint : (allied ? new Color(1f, 0.92f, 0.75f) : CombatEffects.Corruption);
            float strength = bolt ? 1f : 0.6f;
            Gradient gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(head, 0f), new GradientColorKey(head, 1f) },
                new[] { new GradientAlphaKey(strength, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = gradient;
            return trail;
        }

        private static void AddRenderer(GameObject target, Mesh mesh, Material material)
        {
            target.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = target.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private static Mesh haloQuad;
        private static readonly Dictionary<long, Material> halos = new Dictionary<long, Material>();

        // Unit quad in the XY plane (faces -Z toward a camera looking down +Z) with white vertex colours.
        public static Mesh HaloQuad
        {
            get
            {
                if (haloQuad != null) return haloQuad;
                haloQuad = new Mesh { name = "LB Halo Quad", hideFlags = HideFlags.DontSave };
                haloQuad.SetVertices(new List<Vector3> { new Vector3(-0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(0.5f, -0.5f, 0f) });
                haloQuad.SetUVs(0, new List<Vector2> { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) });
                haloQuad.SetColors(new List<Color> { Color.white, Color.white, Color.white, Color.white });
                haloQuad.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
                haloQuad.RecalculateBounds();
                return haloQuad;
            }
        }

        // Soft additive glow around a magic bolt, tinted per faction.
        private static Material HaloMaterial(ArtStyleLibrary style, Color tint, bool allied)
        {
            Material source = style.vfxMaterial != null ? style.vfxMaterial : style.auraMotesMaterial;
            if (source == null) return null;
            long key = ((long)style.GetInstanceID() << 2) | (allied ? 1L : 2L);
            if (halos.TryGetValue(key, out Material cached) && cached != null) return cached;
            Material material = new Material(source) { name = allied ? "LB Bolt Halo" : "LB Dark Bolt Halo", hideFlags = HideFlags.DontSave };
            if (material.HasProperty("_Shape")) material.SetFloat("_Shape", 0f);
            if (material.HasProperty("_Additive")) material.SetFloat("_Additive", 1f);
            material.SetFloat("_Softness", 2.2f);
            material.SetColor("_Color", tint * 1.6f);
            halos[key] = material;
            return material;
        }

        // Neutral white so the gradient on each trail decides its colour.
        private static Material TrailMaterial(ArtStyleLibrary style)
        {
            if (style.lightningMaterial == null) return null;
            long key = style.GetInstanceID();
            if (trails.TryGetValue(key, out Material cached) && cached != null) return cached;
            Material material = new Material(style.lightningMaterial) { name = "LB Shot Trail", hideFlags = HideFlags.DontSave };
            material.SetColor("_Color", new Color(1.4f, 1.4f, 1.4f));
            material.SetFloat("_CoreBoost", 0.6f);
            trails[key] = material;
            return material;
        }

        // A fletched arrow along +Z (tip at the pivot), oversized slightly so it reads at battle distance.
        private static Mesh ArrowMesh(ArtStyleLibrary s, bool allied)
        {
            long key = ((long)s.GetInstanceID() << 4) | (allied ? 1L : 2L);
            if (meshes.TryGetValue(key, out Mesh cached) && cached != null) return cached;
            SilhouetteFactory.Shapes b = new SilhouetteFactory.Shapes();
            Color shaft = allied ? new Color(0.62f, 0.48f, 0.3f) : s.corruptArmor;
            Color tip = allied ? s.steel : s.corruptGlow;
            Color fletch = allied ? s.royalBlue : s.corruptBody;
            b.Add(SilhouetteFactory.Shape.Cube, shaft, new Vector3(0f, 0f, -0.36f), new Vector3(0.03f, 0.03f, 0.64f));
            b.Cone(0f, tip, new Vector3(0f, 0f, -0.06f), new Vector3(0.07f, 0.14f, 0.07f), new Vector3(90f, 0f, 0f), 6,
                emission: allied ? 0f : 0.8f, spec: allied ? 1f : 0f);
            b.Add(SilhouetteFactory.Shape.Cube, fletch, new Vector3(0f, 0f, -0.62f), new Vector3(0.12f, 0.008f, 0.14f));
            b.Add(SilhouetteFactory.Shape.Cube, fletch, new Vector3(0f, 0f, -0.62f), new Vector3(0.008f, 0.12f, 0.14f));
            Mesh mesh = b.Bake(allied ? "LB Arrow" : "LB Enemy Arrow");
            meshes[key] = mesh;
            return mesh;
        }

        // A glowing orb in a faint shell of the caster's colour.
        private static Mesh BoltMesh(ArtStyleLibrary s, bool allied)
        {
            long key = ((long)s.GetInstanceID() << 4) | (allied ? 3L : 4L);
            if (meshes.TryGetValue(key, out Mesh cached) && cached != null) return cached;
            SilhouetteFactory.Shapes b = new SilhouetteFactory.Shapes();
            b.Add(SilhouetteFactory.Shape.Ball, allied ? s.lightGlow : s.corruptGlow, Vector3.zero, Vector3.one * 0.26f, emission: 1f);
            b.Add(SilhouetteFactory.Shape.Ball, Color.white, Vector3.zero, Vector3.one * 0.13f, emission: 1f);
            Mesh mesh = b.Bake(allied ? "LB Arcane Bolt" : "LB Dark Bolt");
            meshes[key] = mesh;
            return mesh;
        }
    }

    // Pulses and spins a magic bolt's orb while it flies, keeps a soft halo facing the camera and sheds
    // sparkles behind it.
    public sealed class BoltFlicker : MonoBehaviour
    {
        private const float TrailInterval = 0.025f;
        private float seed;
        private ArtStyleLibrary style;
        private Color tint;
        private Transform halo;
        private float trailTimer;

        public void Configure(ArtStyleLibrary library, Color color, Material haloMaterial)
        {
            style = library;
            tint = color;
            if (haloMaterial == null || halo != null) return;
            GameObject item = new GameObject("Halo");
            // Sibling of the orb so the orb's spin and pulse do not tumble the halo.
            item.transform.SetParent(transform.parent, false);
            item.transform.localScale = Vector3.one * 1.3f;
            item.AddComponent<MeshFilter>().sharedMesh = AttackVfx.HaloQuad;
            MeshRenderer renderer = item.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = haloMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            halo = item.transform;
        }

        private void OnEnable() => seed = Random.value * 10f;

        private void OnDestroy()
        {
            if (halo != null) Destroy(halo.gameObject);
        }

        private void Update()
        {
            float t = Time.time * 22f + seed;
            float pulse = 1f + 0.18f * Mathf.Sin(t) + 0.08f * Mathf.Sin(t * 2.7f);
            transform.localScale = Vector3.one * pulse;
            transform.localRotation = Quaternion.Euler(Time.time * 360f, Time.time * 220f, 0f);
            UnityEngine.Camera view = UnityEngine.Camera.main;
            if (halo != null && view != null)
            {
                halo.rotation = Quaternion.LookRotation(halo.position - view.transform.position, view.transform.up);
                halo.localScale = Vector3.one * (1.3f * (0.9f + 0.1f * pulse));
            }
            // Only a bolt still in flight sheds sparkles (the orb stays parented to its projectile until it lands).
            if (style == null || transform.parent == null || transform.parent.GetComponent<Projectile>() == null) return;
            trailTimer -= Time.deltaTime;
            if (trailTimer > 0f) return;
            trailTimer = TrailInterval;
            CombatEffects.Trail(style, transform.position, tint);
        }
    }
}

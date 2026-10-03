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
                visual.AddComponent<BoltFlicker>();
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
            CombatEffects.Hit(style, front + Vector3.up * 0.4f, transform.forward, CombatEffects.SteelSpark, 2.5f);
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
            trail.time = bolt ? 0.22f : 0.12f;
            trail.minVertexDistance = 0.12f;
            trail.widthMultiplier = bolt ? 0.32f : 0.07f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
            trail.textureMode = LineTextureMode.Stretch;
            trail.alignment = LineAlignment.View;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            Color head = bolt ? Tint : (allied ? new Color(1f, 0.92f, 0.75f) : CombatEffects.Corruption);
            float strength = bolt ? 1f : 0.45f;
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

    // Pulses and spins a magic bolt's orb while it flies.
    public sealed class BoltFlicker : MonoBehaviour
    {
        private float seed;
        private void OnEnable() => seed = Random.value * 10f;
        private void Update()
        {
            float t = Time.time * 22f + seed;
            transform.localScale = Vector3.one * (1f + 0.18f * Mathf.Sin(t) + 0.08f * Mathf.Sin(t * 2.7f));
            transform.localRotation = Quaternion.Euler(Time.time * 360f, Time.time * 220f, 0f);
        }
    }
}

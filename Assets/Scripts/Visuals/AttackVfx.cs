using System.Collections.Generic;
using Lightbringer.CameraSystem;
using Lightbringer.Combat;
using UnityEngine;
using UnityEngine.Rendering;

namespace Lightbringer.Visuals
{
    // Feedback on the attacking unit: dresses ranged shots (ShotVfx: a glinting arrow with an energy streak, or a
    // comet bolt with spiral sparks), lands them in impacts, and adds shock rings to charges and splash slams.
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
            if (shot.Kind == ProjectileKind.Arrow) DressArrow(shot);
            else DressBolt(shot);
        }

        // Fletched arrow with a flickering glint on its head and a keen streak behind it: holy gold for
        // troops, crimson with a wisp of smoke for enemies.
        private void DressArrow(Projectile shot)
        {
            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(shot.transform, false);
            AddRenderer(visual, ArrowMesh(style, allied), allied ? style.alliedMaterial : style.enemyMaterial);
            Renderer glint = ShotVfx.Billboard(shot.transform, "Arrowhead Glint", allied ? 0.5f : 0.42f, ShotVfx.GlintMode, allied);
            if (glint != null) glint.transform.localPosition = Vector3.back * 0.05f;
            TrailRenderer streak = ShotVfx.Trail(shot.transform, "Streak", allied, false, 0.2f, allied ? 0.15f : 0.14f);
            Color tint = allied ? CombatEffects.HolyGold : CombatEffects.Corruption;
            ArtStyleLibrary library = style;
            shot.Landed += (projectile, point, struck) =>
            {
                // The streak finishes fading at the contact instead of disappearing with the projectile.
                ReleaseTrail(streak, projectile.transform.parent);
                CombatEffects.ArrowImpact(library, point, projectile.transform.forward, tint, struck == null);
                // Arrows stick in what they hit for a moment (or in the ground where the target fell).
                visual.transform.SetParent(struck != null ? struck.transform : projectile.transform.parent, true);
                visual.transform.position = point + projectile.transform.forward * 0.18f;
                Destroy(visual, StuckArrowLife);
            };
        }

        // Comet of light (or corrupted fire) with two orbiting sparks drawing spiral trails around a plasma
        // ribbon. It lands in a rising column and a flash (enemy bolts add a corrupted ground splash).
        private void DressBolt(Projectile shot)
        {
            Transform root = shot.transform;
            Renderer head = ShotVfx.Billboard(root, "Bolt Head", allied ? 0.95f : 1.05f, ShotVfx.CometMode, allied, 2.6f);
            var orbit = new GameObject("Orbit").transform;
            orbit.SetParent(root, false);
            var trails = new List<TrailRenderer> { ShotVfx.Trail(root, "Ribbon", allied, true, 0.3f, allied ? 0.5f : 0.6f) };
            for (int i = 0; i < 2; i++)
            {
                Renderer spark = ShotVfx.Billboard(orbit, "Orbit Spark", 0.34f, ShotVfx.GlintMode, allied);
                if (spark == null) continue;
                spark.transform.localPosition = (i == 0 ? Vector3.right : Vector3.left) * 0.32f;
                trails.Add(ShotVfx.Trail(spark.transform, "Spiral", allied, false, 0.18f, 0.07f));
            }
            Light glow = ShotVfx.Glow(root, Vector3.zero, Tint, 4.5f, allied ? 2.4f : 2f);
            if (head != null) head.gameObject.AddComponent<ShotFlight>().Configure(style, Tint, head.transform, orbit, glow);
            CombatEffects.Muzzle(style, root.position, Tint);
            Color tint = Tint;
            bool friendly = allied;
            float splash = combat.SplashRadius;
            ArtStyleLibrary library = style;
            shot.Landed += (projectile, point, struck) =>
            {
                foreach (TrailRenderer trail in trails) ReleaseTrail(trail, projectile.transform.parent);
                CombatEffects.SpellImpact(library, point, tint, splash, friendly);
                SpellImpactVfx.Spawn(projectile.transform.parent, HeroSpellVfx.GroundBelow(point), splash, friendly);
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
            bool friendly = GetComponent<Combatant>().Faction == Faction.Allied;
            Color color = friendly ? CombatEffects.HolyGold : CombatEffects.Corruption;
            CombatEffects.Hit(style, front + Vector3.up * 0.4f, transform.forward, color, 2.5f, true);
            CombatEffects.Ring(front, color, 1.8f);
            CombatEffects.DustRing(HeroSpellVfx.GroundBelow(front + Vector3.up * 0.5f), 0.8f, 8);
            ThirdPersonCamera.Shake(front, 0.5f);
        }

        private static void ReleaseTrail(TrailRenderer trail, Transform parent)
        {
            if (trail == null) return;
            trail.transform.SetParent(parent, true);
            trail.emitting = false;
            Destroy(trail.gameObject, trail.time + 0.05f);
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

        // A fletched arrow along +Z (tip at the pivot), oversized slightly so it reads at battle distance.
        private static Mesh ArrowMesh(ArtStyleLibrary s, bool allied)
        {
            long key = ((long)s.GetInstanceID() << 4) | (allied ? 1L : 2L);
            if (meshes.TryGetValue(key, out Mesh cached) && cached != null) return cached;
            SilhouetteFactory.Shapes b = new SilhouetteFactory.Shapes();
            Color shaft = allied ? new Color(0.62f, 0.48f, 0.3f) : s.corruptArmor;
            // Troop arrowheads carry a little of the hero's light; enemy heads smoulder.
            Color tip = allied ? Color.Lerp(s.steel, s.lightGlow, 0.45f) : s.corruptGlow;
            Color fletch = allied ? s.royalBlue : s.corruptBody;
            b.Add(SilhouetteFactory.Shape.Cube, shaft, new Vector3(0f, 0f, -0.36f), new Vector3(0.03f, 0.03f, 0.64f));
            b.Cone(0f, tip, new Vector3(0f, 0f, -0.06f), new Vector3(0.07f, 0.14f, 0.07f), new Vector3(90f, 0f, 0f), 6,
                emission: allied ? 0.45f : 0.8f, spec: allied ? 1f : 0f);
            b.Add(SilhouetteFactory.Shape.Cube, fletch, new Vector3(0f, 0f, -0.62f), new Vector3(0.12f, 0.008f, 0.14f));
            b.Add(SilhouetteFactory.Shape.Cube, fletch, new Vector3(0f, 0f, -0.62f), new Vector3(0.008f, 0.12f, 0.14f));
            Mesh mesh = b.Bake(allied ? "LB Arrow" : "LB Enemy Arrow");
            meshes[key] = mesh;
            return mesh;
        }
    }
}

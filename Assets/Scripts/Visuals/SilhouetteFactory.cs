using System.Collections.Generic;
using UnityEngine;

namespace Lightbringer.Visuals
{
    public enum PartMotion { WingLeft, WingRight, Spin }

    public struct MovingPart
    {
        public Mesh Mesh;
        public Vector3 Pivot;
        public PartMotion Motion;
    }

    // One baked, single-material stylized silhouette in feet space (ground at y = 0, facing +Z).
    public sealed class Silhouette
    {
        public Mesh Body;
        public MovingPart[] Parts = new MovingPart[0];
        public float BobHeight;
        public float BobRate = 9f;
        public float LeanDegrees = 4f;
        public float HoverHeight;
    }

    // Readable Art Pass stand-ins built from the character sheet: role is legible from the
    // weapon and outline alone. They are replaced one VisualId at a time by real models.
    public static class SilhouetteFactory
    {
        private static readonly Dictionary<long, Silhouette> Cache = new Dictionary<long, Silhouette>();

        public static Silhouette Get(ArtStyleLibrary style, VisualId id)
        {
            long key = ((long)style.GetInstanceID() << 8) | (long)id;
            if (Cache.TryGetValue(key, out Silhouette cached) && cached.Body != null) return cached;
            Silhouette built = Build(style, id);
            Cache[key] = built;
            return built;
        }

        // Palette edits rebuild silhouettes for new spawns. During Play the old meshes stay alive
        // because troops already on the battlefield still render them.
        public static void ClearCache()
        {
            if (Application.isPlaying) { Cache.Clear(); return; }
            foreach (Silhouette silhouette in Cache.Values)
            {
                Release(silhouette.Body);
                foreach (MovingPart part in silhouette.Parts) Release(part.Mesh);
            }
            Cache.Clear();
        }

        private static void Release(Mesh mesh)
        {
            if (mesh == null) return;
            if (Application.isPlaying) Object.Destroy(mesh); else Object.DestroyImmediate(mesh);
        }

        private static Silhouette Build(ArtStyleLibrary s, VisualId id)
        {
            Shapes b = new Shapes();
            Silhouette result = new Silhouette { BobHeight = 0.05f };
            switch (id)
            {
                case VisualId.Hero: Hero(b, s); result.BobHeight = 0.04f; break;
                case VisualId.Swordsman: Swordsman(b, s); break;
                case VisualId.Archer: Archer(b, s); break;
                case VisualId.Shieldbearer: Shieldbearer(b, s); result.BobHeight = 0.035f; result.BobRate = 7f; break;
                case VisualId.Spearman: Spearman(b, s); break;
                case VisualId.Priest: Priest(b, s); result.BobHeight = 0.03f; break;
                case VisualId.Mage: Mage(b, s); result.BobHeight = 0.03f; break;
                case VisualId.Knight: Knight(b, s); result.BobHeight = 0.08f; result.BobRate = 7f; result.LeanDegrees = 2f; break;
                case VisualId.Dragon:
                    Dragon(b, s);
                    result.BobHeight = 0f; result.HoverHeight = 0.25f; result.LeanDegrees = 6f;
                    result.Parts = new[] { DragonWing(s, -1f), DragonWing(s, 1f) };
                    break;
                case VisualId.EnemyRaider: Raider(b, s); result.BobHeight = 0.06f; result.BobRate = 10f; result.LeanDegrees = 8f; break;
                case VisualId.EnemyArcher: EnemyArcher(b, s); break;
                case VisualId.EnemySwarm: Swarmling(b, s); result.HoverHeight = 0.12f; result.BobHeight = 0.07f; result.BobRate = 13f; result.LeanDegrees = 10f; break;
                case VisualId.EnemyBrute: Brute(b, s); result.BobHeight = 0.07f; result.BobRate = 6f; result.LeanDegrees = 3f; break;
                case VisualId.EnemyShaman: Shaman(b, s); result.BobHeight = 0.03f; break;
                case VisualId.EnemyBoss: Warlord(b, s); result.BobHeight = 0.04f; result.BobRate = 5f; result.LeanDegrees = 2f; break;
                case VisualId.EnemyStronghold:
                    Stronghold(b, s);
                    result.BobHeight = 0f; result.LeanDegrees = 0f;
                    result.Parts = new[] { Crystal(s) };
                    break;
                case VisualId.AlliedStronghold:
                    AlliedStronghold(b, s);
                    result.BobHeight = 0f; result.LeanDegrees = 0f;
                    result.Parts = new[] { LightCrystal(s) };
                    break;
            }
            result.Body = b.Bake("LB " + id);
            return result;
        }

        // ---------- Allied ----------

        private static void Hero(Shapes b, ArtStyleLibrary s)
        {
            // Mage commander (H = 2.0): white/blue/gold, dark thigh boots, long cape, glowing orb staff.
            b.Pair(Shape.Ball, s.leather, new Vector3(0.1f, 0.43f, 0f), new Vector3(0.15f, 0.86f, 0.15f));
            b.Cone(0.6f, s.ivory, new Vector3(0f, 0.72f, 0f), new Vector3(0.5f, 0.42f, 0.4f));
            b.Pair(Shape.Cube, s.royalBlue, new Vector3(0.2f, 0.72f, -0.12f), new Vector3(0.2f, 0.85f, 0.03f), new Vector3(-10f, 0f, 10f));
            b.Add(Shape.Cube, s.royalBlue, new Vector3(0f, 0.7f, -0.2f), new Vector3(0.34f, 0.9f, 0.03f), new Vector3(-12f, 0f, 0f));
            b.Add(Shape.Ball, s.ivory, new Vector3(0f, 1.35f, 0f), new Vector3(0.36f, 0.55f, 0.26f));
            b.Cone(1.1f, s.leather, new Vector3(0f, 1.08f, 0f), new Vector3(0.34f, 0.2f, 0.26f));
            b.Add(Shape.Cube, s.gold, new Vector3(0f, 1.18f, 0.13f), new Vector3(0.08f, 0.08f, 0.02f), spec: 1f);
            b.Pair(Shape.Ball, s.gold, new Vector3(0.24f, 1.6f, 0f), new Vector3(0.2f, 0.12f, 0.2f), spec: 1f);
            b.Add(Shape.Ball, s.royalBlue, new Vector3(0f, 1.62f, -0.03f), new Vector3(0.48f, 0.18f, 0.36f));
            b.Add(Shape.Cube, s.royalBlue, new Vector3(0f, 1.0f, -0.21f), new Vector3(0.62f, 1.25f, 0.04f), new Vector3(-8f, 0f, 0f));
            b.Add(Shape.Cube, s.gold, new Vector3(0f, 1.15f, -0.24f), new Vector3(0.14f, 0.2f, 0.01f), new Vector3(-8f, 0f, 45f), spec: 1f);
            b.Pair(Shape.Ball, s.ivory, new Vector3(0.26f, 1.32f, 0.03f), new Vector3(0.12f, 0.5f, 0.12f));
            b.Pair(Shape.Ball, s.leather, new Vector3(0.29f, 1.06f, 0.05f), new Vector3(0.1f, 0.12f, 0.1f));
            b.Add(Shape.Ball, s.skin, new Vector3(0f, 1.72f, 0f), new Vector3(0.1f, 0.12f, 0.1f));
            b.Add(Shape.Ball, s.skin, new Vector3(0f, 1.86f, 0.01f), new Vector3(0.26f, 0.29f, 0.27f));
            b.Add(Shape.Ball, s.hair, new Vector3(0f, 1.9f, -0.03f), new Vector3(0.3f, 0.3f, 0.3f));
            b.Add(Shape.Ball, s.hair, new Vector3(0f, 2.06f, -0.12f), new Vector3(0.16f, 0.16f, 0.16f));
            // Staff in the right hand, star crown and light orb well above the head for readability.
            Vector3 staff = new Vector3(0.34f, 0.05f, 0.14f);
            b.Cone(1f, s.leather, staff, new Vector3(0.05f, 2.0f, 0.05f));
            b.Cone(1.8f, s.gold, staff + Vector3.up * 1.95f, new Vector3(0.14f, 0.18f, 0.14f), spec: 1f);
            b.Add(Shape.Cube, s.gold, staff + Vector3.up * 2.2f, new Vector3(0.52f, 0.035f, 0.035f), spec: 1f);
            b.Add(Shape.Cube, s.gold, staff + Vector3.up * 2.2f, new Vector3(0.035f, 0.52f, 0.035f), spec: 1f);
            b.Add(Shape.Ball, s.lightGlow, staff + Vector3.up * 2.2f, Vector3.one * 0.22f, emission: 1f);
        }

        private static void Humanoid(Shapes b, Color legs, Color torso, Color arms, Color skin, float width = 1f)
        {
            b.Pair(Shape.Ball, legs, new Vector3(0.09f * width, 0.36f, 0f), new Vector3(0.14f * width, 0.72f, 0.14f));
            b.Add(Shape.Ball, torso, new Vector3(0f, 0.98f, 0f), new Vector3(0.36f * width, 0.58f, 0.26f * width));
            b.Pair(Shape.Ball, arms, new Vector3(0.23f * width, 1.0f, 0.02f), new Vector3(0.11f, 0.44f, 0.11f), new Vector3(0f, 0f, 12f));
            b.Add(Shape.Ball, skin, new Vector3(0f, 1.38f, 0.01f), Vector3.one * 0.24f);
        }

        private static void Swordsman(Shapes b, ArtStyleLibrary s)
        {
            Humanoid(b, s.leather, s.steel, s.steel, s.skin);
            b.Add(Shape.Cube, s.royalBlue, new Vector3(0f, 0.86f, 0.13f), new Vector3(0.28f, 0.5f, 0.04f));
            b.Cone(1f, s.gold, new Vector3(0f, 0.7f, 0f), new Vector3(0.38f, 0.05f, 0.28f), spec: 1f);
            b.Add(Shape.Ball, s.steel, new Vector3(0f, 1.43f, 0f), new Vector3(0.29f, 0.24f, 0.29f), spec: 0.8f);
            b.Add(Shape.Cube, s.royalBlue, new Vector3(0f, 1.57f, -0.01f), new Vector3(0.04f, 0.09f, 0.24f));
            // Sword raised forward on the right; round shield on the left.
            b.Add(Shape.Cube, s.steel, new Vector3(0.3f, 1.1f, 0.24f), new Vector3(0.055f, 0.62f, 0.015f), new Vector3(35f, 0f, 0f), spec: 1f);
            b.Add(Shape.Cube, s.gold, new Vector3(0.3f, 0.86f, 0.07f), new Vector3(0.18f, 0.035f, 0.045f), new Vector3(35f, 0f, 0f), spec: 1f);
            b.Cone(1f, s.royalBlue, new Vector3(-0.3f, 0.95f, 0.1f), new Vector3(0.44f, 0.04f, 0.44f), new Vector3(90f, 0f, 0f), 16);
            b.Add(Shape.Ball, s.gold, new Vector3(-0.3f, 0.95f, 0.15f), new Vector3(0.1f, 0.1f, 0.06f), spec: 1f);
        }

        private static void Archer(Shapes b, ArtStyleLibrary s)
        {
            Humanoid(b, s.leather, s.ivory, s.ivory, s.skin, 0.92f);
            b.Cone(0.8f, s.ivory, new Vector3(0f, 0.42f, 0f), new Vector3(0.4f, 0.36f, 0.3f));
            b.Cone(1f, s.gold, new Vector3(0f, 0.74f, 0f), new Vector3(0.35f, 0.04f, 0.26f), spec: 1f);
            b.Add(Shape.Ball, s.royalBlue, new Vector3(0f, 1.41f, -0.04f), new Vector3(0.3f, 0.29f, 0.3f));
            b.Cone(0.15f, s.royalBlue, new Vector3(0f, 1.3f, -0.05f), new Vector3(0.33f, 0.42f, 0.33f), new Vector3(-12f, 0f, 0f));
            b.Add(Shape.Ball, s.royalBlue, new Vector3(0f, 1.2f, -0.02f), new Vector3(0.4f, 0.12f, 0.32f));
            // Tall bow held out on the left: grip plus two swept limbs.
            Vector3 grip = new Vector3(-0.36f, 1.02f, 0.24f);
            b.Add(Shape.Cube, s.leather, grip, new Vector3(0.045f, 0.28f, 0.045f));
            b.Add(Shape.Cube, s.gold, grip + new Vector3(0f, 0.3f, -0.06f), new Vector3(0.035f, 0.4f, 0.035f), new Vector3(-24f, 0f, 0f), spec: 1f);
            b.Add(Shape.Cube, s.gold, grip + new Vector3(0f, -0.3f, -0.06f), new Vector3(0.035f, 0.4f, 0.035f), new Vector3(24f, 0f, 0f), spec: 1f);
            b.Cone(1.1f, s.leather, new Vector3(0.1f, 0.82f, -0.17f), new Vector3(0.12f, 0.46f, 0.12f), new Vector3(-15f, 0f, -15f));
        }

        private static void Shieldbearer(Shapes b, ArtStyleLibrary s)
        {
            Humanoid(b, s.steel, s.steel, s.steel, s.skin, 1.2f);
            b.Pair(Shape.Ball, s.gold, new Vector3(0.28f, 1.22f, 0f), new Vector3(0.2f, 0.14f, 0.2f), spec: 1f);
            b.Cone(0.9f, s.steel, new Vector3(0f, 1.24f, 0f), new Vector3(0.3f, 0.32f, 0.3f), spec: 0.8f);
            b.Add(Shape.Ball, s.steel, new Vector3(0f, 1.56f, 0f), new Vector3(0.29f, 0.12f, 0.29f), spec: 0.8f);
            b.Add(Shape.Cube, s.leather, new Vector3(0f, 1.42f, 0.14f), new Vector3(0.2f, 0.03f, 0.03f));
            b.Add(Shape.Cube, s.royalBlue, new Vector3(0f, 1.62f, 0f), new Vector3(0.05f, 0.1f, 0.26f));
            // Tower shield carried in front: blue field, gold rim and cross.
            Vector3 shield = new Vector3(-0.1f, 0.86f, 0.32f);
            b.Add(Shape.Cube, s.gold, shield, new Vector3(0.66f, 1.0f, 0.06f), spec: 1f);
            b.Add(Shape.Cube, s.royalBlue, shield + new Vector3(0f, 0f, 0.02f), new Vector3(0.58f, 0.92f, 0.06f));
            b.Add(Shape.Cube, s.gold, shield + new Vector3(0f, 0.02f, 0.055f), new Vector3(0.07f, 0.66f, 0.02f), spec: 1f);
            b.Add(Shape.Cube, s.gold, shield + new Vector3(0f, 0.14f, 0.055f), new Vector3(0.4f, 0.07f, 0.02f), spec: 1f);
            b.Add(Shape.Cube, s.steel, new Vector3(0.36f, 0.8f, 0.14f), new Vector3(0.05f, 0.5f, 0.015f), new Vector3(20f, 0f, 0f), spec: 1f);
        }

        private static void Spearman(Shapes b, ArtStyleLibrary s)
        {
            Humanoid(b, s.leather, s.steel, s.ivory, s.skin);
            b.Add(Shape.Cube, s.royalBlue, new Vector3(0f, 0.86f, 0.13f), new Vector3(0.28f, 0.5f, 0.04f));
            b.Cone(0.2f, s.steel, new Vector3(0f, 1.36f, 0f), new Vector3(0.3f, 0.3f, 0.3f), spec: 0.8f);
            b.Add(Shape.Ball, s.steel, new Vector3(0f, 1.38f, 0f), new Vector3(0.38f, 0.05f, 0.38f), spec: 0.8f);
            // Long spear levelled forward: its reach is the silhouette.
            Vector3 euler = new Vector3(62f, 0f, 0f);
            Vector3 direction = Quaternion.Euler(euler) * Vector3.up;
            Vector3 butt = new Vector3(0.27f, 0.35f, -0.45f);
            b.Cone(1f, s.leather, butt, new Vector3(0.045f, 2.1f, 0.045f), euler, 8);
            b.Cone(0f, s.steel, butt + direction * 2.1f, new Vector3(0.1f, 0.26f, 0.1f), euler, 8, spec: 1f);
            b.Add(Shape.Cube, s.royalBlue, butt + direction * 1.85f + new Vector3(0f, -0.08f, 0f), new Vector3(0.02f, 0.13f, 0.2f), euler);
        }

        private static void Priest(Shapes b, ArtStyleLibrary s)
        {
            b.Cone(0.45f, s.ivory, Vector3.zero, new Vector3(0.52f, 1.0f, 0.44f), segments: 14);
            b.Add(Shape.Ball, s.ivory, new Vector3(0f, 1.02f, 0f), new Vector3(0.32f, 0.5f, 0.24f));
            b.Pair(Shape.Cube, s.royalBlue, new Vector3(0.07f, 0.8f, 0.17f), new Vector3(0.07f, 0.72f, 0.02f), new Vector3(-12f, 0f, 0f));
            b.Cone(1f, s.gold, new Vector3(0f, 0.86f, 0f), new Vector3(0.3f, 0.04f, 0.24f), spec: 1f);
            b.Pair(Shape.Ball, s.ivory, new Vector3(0.22f, 1.0f, 0.03f), new Vector3(0.14f, 0.44f, 0.14f), new Vector3(0f, 0f, 14f));
            b.Add(Shape.Ball, s.skin, new Vector3(0f, 1.39f, 0.02f), Vector3.one * 0.23f);
            b.Add(Shape.Ball, s.ivory, new Vector3(0f, 1.43f, -0.03f), new Vector3(0.3f, 0.3f, 0.3f));
            Vector3 staff = new Vector3(0.3f, 0.05f, 0.12f);
            b.Cone(1f, s.gold, staff, new Vector3(0.04f, 1.55f, 0.04f), segments: 8, spec: 1f);
            b.Add(Shape.Cube, s.gold, staff + Vector3.up * 1.66f, new Vector3(0.24f, 0.035f, 0.035f), spec: 1f);
            b.Add(Shape.Cube, s.gold, staff + Vector3.up * 1.66f, new Vector3(0.035f, 0.3f, 0.035f), spec: 1f);
            b.Add(Shape.Ball, s.holyGlow, staff + Vector3.up * 1.66f, Vector3.one * 0.13f, emission: 1f);
        }

        private static void Mage(Shapes b, ArtStyleLibrary s)
        {
            b.Cone(0.45f, s.royalBlue, Vector3.zero, new Vector3(0.52f, 1.0f, 0.44f), segments: 14);
            b.Add(Shape.Cube, s.ivory, new Vector3(0f, 0.45f, 0.17f), new Vector3(0.13f, 0.82f, 0.02f), new Vector3(-12f, 0f, 0f));
            b.Add(Shape.Ball, s.royalBlue, new Vector3(0f, 1.02f, 0f), new Vector3(0.32f, 0.5f, 0.24f));
            b.Add(Shape.Ball, s.ivory, new Vector3(0f, 1.2f, -0.01f), new Vector3(0.42f, 0.2f, 0.34f));
            b.Add(Shape.Ball, s.skin, new Vector3(0f, 1.38f, 0.02f), Vector3.one * 0.23f);
            b.Cone(0.2f, s.royalBlue, new Vector3(0f, 1.26f, -0.04f), new Vector3(0.34f, 0.5f, 0.34f), new Vector3(-10f, 0f, 0f));
            // Arms raised forward, casting a floating rune orb.
            b.Pair(Shape.Ball, s.royalBlue, new Vector3(0.17f, 1.08f, 0.16f), new Vector3(0.11f, 0.4f, 0.11f), new Vector3(-70f, 0f, -10f));
            b.Add(Shape.Ball, s.lightGlow, new Vector3(0f, 1.1f, 0.48f), Vector3.one * 0.17f, emission: 1f);
            b.Cone(1f, s.gold, new Vector3(0f, 1.1f, 0.4f), new Vector3(0.36f, 0.015f, 0.36f), new Vector3(90f, 0f, 0f), 16, spec: 1f);
        }

        private static void Knight(Shapes b, ArtStyleLibrary s)
        {
            // Mounted: the horse doubles the footprint and makes the most expensive troop obvious.
            b.Add(Shape.Ball, s.ivory, new Vector3(0f, 0.98f, 0f), new Vector3(0.5f, 0.55f, 1.25f));
            b.Pair(Shape.Ball, s.ivory, new Vector3(0.16f, 0.36f, 0.42f), new Vector3(0.12f, 0.72f, 0.12f));
            b.Pair(Shape.Ball, s.ivory, new Vector3(0.16f, 0.36f, -0.42f), new Vector3(0.12f, 0.72f, 0.12f));
            b.Cone(0.7f, s.ivory, new Vector3(0f, 1.05f, 0.42f), new Vector3(0.28f, 0.62f, 0.32f), new Vector3(35f, 0f, 0f));
            b.Add(Shape.Ball, s.ivory, new Vector3(0f, 1.58f, 0.84f), new Vector3(0.22f, 0.24f, 0.46f), new Vector3(20f, 0f, 0f));
            b.Add(Shape.Cube, s.hair, new Vector3(0f, 1.42f, 0.58f), new Vector3(0.05f, 0.42f, 0.16f), new Vector3(35f, 0f, 0f));
            b.Cone(1.05f, s.royalBlue, new Vector3(0f, 0.58f, 0f), new Vector3(0.62f, 0.38f, 1.12f));
            b.Cone(1f, s.gold, new Vector3(0f, 0.56f, 0f), new Vector3(0.64f, 0.04f, 1.14f), spec: 1f);
            b.Cone(0.3f, s.hair, new Vector3(0f, 1.0f, -0.6f), new Vector3(0.14f, 0.55f, 0.14f), new Vector3(-150f, 0f, 0f), 8);
            // Rider.
            b.Add(Shape.Ball, s.steel, new Vector3(0f, 1.5f, -0.05f), new Vector3(0.34f, 0.5f, 0.26f), spec: 0.8f);
            b.Pair(Shape.Ball, s.gold, new Vector3(0.2f, 1.68f, -0.05f), new Vector3(0.18f, 0.12f, 0.18f), spec: 1f);
            b.Add(Shape.Ball, s.skin, new Vector3(0f, 1.88f, -0.04f), Vector3.one * 0.22f);
            b.Add(Shape.Ball, s.steel, new Vector3(0f, 1.92f, -0.05f), new Vector3(0.27f, 0.25f, 0.27f), spec: 0.8f);
            b.Add(Shape.Ball, s.royalBlue, new Vector3(0f, 2.1f, -0.12f), new Vector3(0.06f, 0.2f, 0.26f));
            b.Add(Shape.Cube, s.royalBlue, new Vector3(-0.3f, 1.45f, 0f), new Vector3(0.07f, 0.46f, 0.32f));
            Vector3 euler = new Vector3(78f, 0f, 0f);
            Vector3 direction = Quaternion.Euler(euler) * Vector3.up;
            Vector3 butt = new Vector3(0.3f, 1.3f, -0.4f);
            b.Cone(0.2f, s.steel, butt, new Vector3(0.1f, 2.5f, 0.1f), euler, 8, spec: 1f);
            b.Add(Shape.Cube, s.royalBlue, butt + direction * 2.1f + new Vector3(0f, -0.1f, 0f), new Vector3(0.02f, 0.16f, 0.26f), euler);
        }

        private static void Dragon(Shapes b, ArtStyleLibrary s)
        {
            // Final troop: long pearl-white body, glowing chest, gold horns and spine; wings are moving parts.
            b.Add(Shape.Ball, s.dragonScale, new Vector3(0f, 0.8f, 0f), new Vector3(1.3f, 1.1f, 2.6f));
            b.Add(Shape.Ball, s.ivory, new Vector3(0f, 0.62f, 0.05f), new Vector3(1.0f, 0.7f, 2.0f));
            b.Add(Shape.Ball, s.lightGlow, new Vector3(0f, 0.85f, 1.15f), new Vector3(0.55f, 0.55f, 0.35f), emission: 0.8f);
            b.Cone(0.55f, s.dragonScale, new Vector3(0f, 1.0f, 0.9f), new Vector3(0.7f, 1.3f, 0.75f), new Vector3(50f, 0f, 0f));
            b.Add(Shape.Ball, s.dragonScale, new Vector3(0f, 1.92f, 2.15f), new Vector3(0.55f, 0.45f, 0.95f), new Vector3(15f, 0f, 0f));
            b.Pair(Shape.Ball, s.lightGlow, new Vector3(0.17f, 2.0f, 2.42f), Vector3.one * 0.09f, emission: 1f);
            b.PairCone(0f, s.gold, new Vector3(0.15f, 2.05f, 1.95f), new Vector3(0.1f, 0.5f, 0.1f), new Vector3(-60f, 0f, -15f), spec: 1f);
            for (int i = 0; i < 4; i++)
                b.Cone(0f, s.gold, new Vector3(0f, 1.3f, 0.8f - i * 0.5f), new Vector3(0.14f, 0.32f, 0.22f), new Vector3(-20f, 0f, 0f), 6, spec: 1f);
            Vector3 point = new Vector3(0f, 0.8f, -1.1f);
            float[] pitch = { -100f, -80f, -95f };
            float[] length = { 1.2f, 1.0f, 0.9f };
            float[] width = { 0.6f, 0.36f, 0.2f };
            for (int i = 0; i < 3; i++)
            {
                Vector3 euler = new Vector3(pitch[i], 0f, 0f);
                b.Cone(i == 2 ? 0f : 0.6f, s.dragonScale, point, new Vector3(width[i], length[i], width[i]), euler, 10);
                point += Quaternion.Euler(euler) * Vector3.up * length[i];
            }
            b.Pair(Shape.Ball, s.dragonScale, new Vector3(0.45f, 0.3f, 0.6f), new Vector3(0.3f, 0.6f, 0.3f), new Vector3(30f, 0f, 0f));
            b.Pair(Shape.Ball, s.dragonScale, new Vector3(0.45f, 0.3f, -0.6f), new Vector3(0.3f, 0.6f, 0.3f), new Vector3(30f, 0f, 0f));
        }

        private static MovingPart DragonWing(ArtStyleLibrary s, float side)
        {
            // Built around its shoulder pivot; side -1 = left, +1 = right.
            Shapes b = new Shapes();
            b.Cone(0.3f, s.gold, Vector3.zero, new Vector3(0.14f, 2.7f, 0.14f), new Vector3(0f, 0f, -80f * side), 8, spec: 1f);
            b.Add(Shape.Cube, s.dragonMembrane, new Vector3(1.3f * side, 0.12f, -0.6f), new Vector3(2.5f, 0.04f, 1.3f), new Vector3(0f, 0f, 8f * side));
            b.Add(Shape.Cube, s.dragonMembrane, new Vector3(2.0f * side, 0.3f, -1.25f), new Vector3(1.3f, 0.04f, 0.8f), new Vector3(0f, -20f * side, 8f * side));
            return new MovingPart
            {
                Mesh = b.Bake(side < 0 ? "LB Dragon Wing L" : "LB Dragon Wing R"),
                Pivot = new Vector3(0.5f * side, 1.2f, 0.4f),
                Motion = side < 0 ? PartMotion.WingLeft : PartMotion.WingRight
            };
        }

        // Flame origins of the gate braziers in stronghold feet space (StrongholdVfx lights them).
        public static readonly Vector3[] AlliedBraziers = { new Vector3(-1.75f, 1.45f, 1.95f), new Vector3(1.75f, 1.45f, 1.95f) };
        public static readonly Vector3[] EnemyBraziers = { new Vector3(-1.8f, 1.5f, -1.95f), new Vector3(1.8f, 1.5f, -1.95f) };
        public const float AlliedCrystalHeight = 9.6f, EnemyCrystalHeight = 10.3f;

        private static void AlliedStronghold(Shapes b, ArtStyleLibrary s)
        {
            // Lightbringer bastion: ivory masonry on a stone plinth, round corner towers (open battlements in front,
            // blue tiled spires behind), a tiled central keep with a rose window, and a glowing gate with open
            // plank doors between banners and braziers. The gate faces +Z (the battlefield); the front stays
            // within the 6 x 4 x 4 objective box so troops reach the gate as before.
            Color stone = s.ivory, dark = s.ivory * 0.8f, wood = s.bark * 1.35f;
            b.Pattern = Surface.Masonry;
            b.Add(Shape.Cube, s.plazaStone * 0.92f, new Vector3(0f, 0.25f, -0.3f), new Vector3(7.6f, 0.5f, 4.6f));
            b.Add(Shape.Cube, s.plazaStone, new Vector3(0f, 0.09f, 2.12f), new Vector3(2.6f, 0.18f, 0.35f));
            b.Add(Shape.Cube, stone, new Vector3(0f, 2.3f, 0.1f), new Vector3(5.6f, 3.6f, 3.0f));
            b.Add(Shape.Cube, dark, new Vector3(0f, 0.8f, 0.1f), new Vector3(5.76f, 0.6f, 3.16f));
            for (int i = 0; i < 8; i++)
            {
                float x = -2.45f + i * 0.7f;
                b.Add(Shape.Cube, stone, new Vector3(x, 4.35f, 1.42f), new Vector3(0.42f, 0.5f, 0.3f));
                b.Add(Shape.Cube, stone, new Vector3(x, 4.35f, -1.22f), new Vector3(0.42f, 0.5f, 0.3f));
            }
            b.Pattern = Surface.None;
            b.Add(Shape.Cube, s.gold, new Vector3(0f, 4.12f, 0.1f), new Vector3(5.7f, 0.1f, 3.1f), spec: 1f);

            // Gate: light pouring out of an arch, voussoirs and a gold keystone, plank doors swung open.
            b.Add(Shape.Cube, s.lightGlow, new Vector3(0f, 1.5f, 1.63f), new Vector3(1.5f, 2.0f, 0.04f), emission: 0.85f);
            b.Cone(1f, s.lightGlow, new Vector3(0f, 2.5f, 1.61f), new Vector3(1.5f, 0.04f, 1.5f), new Vector3(90f, 0f, 0f), 16, emission: 0.85f);
            b.Pattern = Surface.Masonry;
            b.Pair(Shape.Cube, stone * 1.04f, new Vector3(0.88f, 1.5f, 1.66f), new Vector3(0.26f, 2.0f, 0.18f));
            for (int k = 0; k <= 6; k++)
            {
                float angle = k * 30f;
                Vector3 offset = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad), 0f) * 0.9f;
                b.Add(Shape.Cube, stone * 1.04f, new Vector3(0f, 2.5f, 1.67f) + offset, new Vector3(0.3f, 0.26f, 0.18f), new Vector3(0f, 0f, angle));
            }
            b.Pattern = Surface.None;
            b.Add(Shape.Cube, s.gold, new Vector3(0f, 3.42f, 1.72f), new Vector3(0.3f, 0.38f, 0.2f), spec: 1f);
            Vector3 hinge = new Vector3(-0.75f, 1.45f, 1.68f);
            Vector3 swing = new Vector3(Mathf.Cos(55f * Mathf.Deg2Rad), 0f, Mathf.Sin(55f * Mathf.Deg2Rad));
            b.Pattern = Surface.Planks;
            b.Pair(Shape.Cube, wood, hinge + swing * 0.36f, new Vector3(0.72f, 1.9f, 0.08f), new Vector3(0f, -55f, 0f));
            b.Pattern = Surface.None;
            foreach (float y in new[] { -0.6f, 0.6f })
                b.Pair(Shape.Cube, s.gold, hinge + swing * 0.36f + new Vector3(0f, y, 0f) + swing.z * 0.05f * Vector3.forward,
                    new Vector3(0.74f, 0.07f, 0.1f), new Vector3(0f, -55f, 0f), spec: 1f);

            // Banners either side of the gate with the winged-star emblem, and lit braziers in front.
            b.Pair(Shape.Cube, s.royalBlue, new Vector3(1.75f, 2.75f, 1.65f), new Vector3(0.75f, 2.0f, 0.05f));
            b.Pair(Shape.Cube, s.royalBlue, new Vector3(1.75f, 1.75f, 1.65f), new Vector3(0.53f, 0.53f, 0.05f), new Vector3(0f, 0f, 45f));
            b.Pair(Shape.Cube, s.gold, new Vector3(1.75f, 3.78f, 1.67f), new Vector3(0.95f, 0.07f, 0.07f), spec: 1f);
            b.Pair(Shape.Cube, s.gold, new Vector3(1.75f, 2.95f, 1.69f), new Vector3(0.32f, 0.32f, 0.03f), new Vector3(0f, 0f, 45f), spec: 1f);
            b.Pair(Shape.Cube, s.gold, new Vector3(1.75f, 2.95f, 1.69f), new Vector3(0.62f, 0.06f, 0.03f), spec: 1f);
            b.Pattern = Surface.Masonry;
            b.PairCone(0.75f, dark, new Vector3(1.75f, 0.5f, 1.95f), new Vector3(0.42f, 0.7f, 0.42f), segments: 8);
            b.Pattern = Surface.None;
            b.PairCone(1.6f, s.gold, new Vector3(1.75f, 1.2f, 1.95f), new Vector3(0.36f, 0.22f, 0.36f), segments: 10, spec: 1f);
            b.PairCone(1f, s.holyGlow, new Vector3(1.75f, 1.38f, 1.95f), new Vector3(0.52f, 0.05f, 0.52f), segments: 10, emission: 1f);

            // Corner towers.
            for (int x = -1; x <= 1; x += 2)
                for (int z = 0; z < 2; z++)
                {
                    bool front = z == 0;
                    float height = front ? 5.0f : 6.2f;
                    Vector3 foot = new Vector3(2.9f * x, 0.5f, front ? 1.15f : -1.45f);
                    b.Pattern = Surface.Masonry;
                    b.Cone(0.9f, stone, foot, new Vector3(1.8f, height, 1.8f), segments: 14);
                    b.Cone(1f, dark, foot + Vector3.up * 0.1f, new Vector3(1.9f, 0.5f, 1.9f), segments: 14);
                    b.Cone(1f, stone, foot + Vector3.up * (height - 0.3f), new Vector3(1.96f, 0.45f, 1.96f), segments: 14);
                    for (int c = 0; c < 8; c++)
                    {
                        Vector3 around = Quaternion.Euler(0f, c * 45f + 22.5f, 0f) * Vector3.forward;
                        b.Add(Shape.Cube, dark, foot + around * 0.86f + Vector3.up * (height - 0.42f), new Vector3(0.18f, 0.24f, 0.2f), new Vector3(0f, c * 45f + 22.5f, 0f));
                        if (front)
                            b.Add(Shape.Cube, stone, foot + around * 0.84f + Vector3.up * (height + 0.32f), new Vector3(0.36f, 0.4f, 0.26f), new Vector3(0f, c * 45f + 22.5f, 0f));
                    }
                    b.Pattern = Surface.None;
                    // Glowing arrow-slit windows facing the battlefield and outward.
                    foreach (Vector3 face in new[] { Vector3.forward, Vector3.right * x })
                        for (int w = 0; w < 2; w++)
                        {
                            Vector3 at = foot + face * 0.84f + Vector3.up * (height * (w == 0 ? 0.38f : 0.68f));
                            Vector3 yaw = new Vector3(0f, Mathf.Atan2(face.x, face.z) * Mathf.Rad2Deg, 0f);
                            b.Add(Shape.Cube, s.holyGlow, at, new Vector3(0.16f, 0.5f, 0.06f), yaw, emission: 0.8f);
                            b.Add(Shape.Ball, s.holyGlow, at + Vector3.up * 0.25f, new Vector3(0.16f, 0.16f, 0.06f), yaw, emission: 0.8f);
                        }
                    if (front)
                    {
                        // Open battlement with a tall pennant.
                        b.Cone(1f, s.gold, foot + Vector3.up * (height + 0.12f), new Vector3(0.08f, 1.9f, 0.08f), segments: 6, spec: 1f);
                        b.Add(Shape.Cube, s.royalBlue, foot + new Vector3(0.38f * -x, height + 1.68f, 0f), new Vector3(0.7f, 0.42f, 0.03f));
                        continue;
                    }
                    b.Cone(1f, s.gold, foot + Vector3.up * (height + 0.12f), new Vector3(2.04f, 0.1f, 2.04f), segments: 14, spec: 1f);
                    b.Pattern = Surface.RoofTiles;
                    b.Cone(0f, s.royalBlue, foot + Vector3.up * (height + 0.2f), new Vector3(2.2f, 2.6f, 2.2f), segments: 14);
                    b.Pattern = Surface.None;
                    b.Cone(0f, s.gold, foot + Vector3.up * (height + 2.7f), new Vector3(0.16f, 0.8f, 0.16f), segments: 6, spec: 1f);
                }

            // Central keep with a tiled pyramid roof, tall windows and a rose window over the gate.
            b.Pattern = Surface.Masonry;
            b.Add(Shape.Cube, stone, new Vector3(0f, 5.3f, -0.4f), new Vector3(2.8f, 2.4f, 2.4f));
            b.Pattern = Surface.None;
            b.Add(Shape.Cube, s.gold, new Vector3(0f, 6.52f, -0.4f), new Vector3(2.9f, 0.1f, 2.5f), spec: 1f);
            b.Pattern = Surface.RoofTiles;
            b.Cone(0f, s.royalBlue, new Vector3(0f, 6.55f, -0.4f), new Vector3(4.3f, 2.4f, 4.3f), new Vector3(0f, 45f, 0f), 4);
            b.Pattern = Surface.None;
            b.Cone(1f, s.gold, new Vector3(0f, 5.45f, 0.79f), new Vector3(1f, 0.03f, 1f), new Vector3(90f, 0f, 0f), 16, spec: 1f);
            b.Cone(1f, s.lightGlow, new Vector3(0f, 5.45f, 0.81f), new Vector3(0.8f, 0.03f, 0.8f), new Vector3(90f, 0f, 0f), 16, emission: 0.9f);
            b.Pair(Shape.Cube, s.holyGlow, new Vector3(0.95f, 5.2f, 0.81f), new Vector3(0.2f, 0.8f, 0.04f), emission: 0.8f);
            b.Pair(Shape.Cube, s.holyGlow, new Vector3(1.41f, 5.2f, -0.4f), new Vector3(0.04f, 0.8f, 0.2f), emission: 0.8f);
            b.Cone(0f, s.gold, new Vector3(0f, 8.9f, -0.4f), new Vector3(0.18f, 0.5f, 0.18f), segments: 6, spec: 1f);
        }

        private static MovingPart LightCrystal(ArtStyleLibrary s)
        {
            Shapes b = new Shapes();
            b.Cone(0f, s.lightGlow, Vector3.zero, new Vector3(0.9f, 1.4f, 0.9f), segments: 4, emission: 0.9f);
            b.Cone(0f, s.lightGlow, Vector3.zero, new Vector3(0.9f, 0.9f, 0.9f), new Vector3(180f, 0f, 0f), 4, emission: 0.9f);
            b.Cone(1f, s.gold, new Vector3(0f, -0.04f, 0f), new Vector3(1.25f, 0.08f, 1.25f), segments: 12, spec: 1f);
            return new MovingPart { Mesh = b.Bake("LB Allied Crystal"), Pivot = new Vector3(0f, AlliedCrystalHeight, -0.4f), Motion = PartMotion.Spin };
        }

        // ---------- Corrupted enemies ----------

        private static void Raider(Shapes b, ArtStyleLibrary s)
        {
            // Hunched, spiked, glowing eyes and a glowing cleaver edge: reads as hostile at distance.
            b.Pair(Shape.Ball, s.corruptBody, new Vector3(0.12f, 0.32f, 0f), new Vector3(0.15f, 0.62f, 0.15f), new Vector3(0f, 0f, 8f));
            b.Add(Shape.Ball, s.corruptArmor, new Vector3(0f, 0.95f, 0.06f), new Vector3(0.46f, 0.55f, 0.34f), new Vector3(20f, 0f, 0f));
            b.Add(Shape.Ball, s.bone, new Vector3(0f, 1.22f, 0.28f), new Vector3(0.24f, 0.24f, 0.28f));
            b.Pair(Shape.Ball, s.corruptGlow, new Vector3(0.06f, 1.25f, 0.41f), Vector3.one * 0.05f, emission: 1f);
            b.PairCone(0f, s.bone, new Vector3(0.09f, 1.3f, 0.25f), new Vector3(0.07f, 0.25f, 0.07f), new Vector3(-30f, 0f, -30f), 6);
            b.PairCone(0f, s.bone, new Vector3(0.24f, 1.16f, 0f), new Vector3(0.1f, 0.32f, 0.1f), new Vector3(0f, 0f, -35f), 6);
            b.Cone(0f, s.bone, new Vector3(0f, 1.18f, -0.12f), new Vector3(0.1f, 0.3f, 0.1f), new Vector3(-40f, 0f, 0f), 6);
            b.Pair(Shape.Ball, s.corruptBody, new Vector3(0.3f, 0.85f, 0.12f), new Vector3(0.12f, 0.55f, 0.12f), new Vector3(0f, 0f, 8f));
            b.Add(Shape.Cube, s.corruptStone, new Vector3(0.34f, 0.72f, 0.3f), new Vector3(0.08f, 0.5f, 0.22f), new Vector3(30f, 0f, 0f));
            b.Add(Shape.Cube, s.corruptGlow, new Vector3(0.34f, 0.66f, 0.41f), new Vector3(0.09f, 0.46f, 0.03f), new Vector3(30f, 0f, 0f), emission: 0.8f);
        }

        private static void EnemyArcher(Shapes b, ArtStyleLibrary s)
        {
            b.Pair(Shape.Ball, s.corruptBody, new Vector3(0.08f, 0.34f, 0f), new Vector3(0.12f, 0.68f, 0.12f));
            b.Cone(0.35f, s.corruptArmor, new Vector3(0f, 0.12f, 0f), new Vector3(0.5f, 1.15f, 0.42f), segments: 7);
            b.Add(Shape.Ball, s.corruptBody, new Vector3(0f, 1.34f, 0.05f), new Vector3(0.22f, 0.22f, 0.2f));
            b.Cone(0f, s.corruptArmor, new Vector3(0f, 1.2f, -0.02f), new Vector3(0.34f, 0.55f, 0.36f), new Vector3(-10f, 0f, 0f), 7);
            b.Pair(Shape.Ball, s.corruptGlow, new Vector3(0.05f, 1.36f, 0.15f), Vector3.one * 0.045f, emission: 1f);
            Vector3 grip = new Vector3(-0.32f, 1.0f, 0.22f);
            b.Add(Shape.Cube, s.bone, grip, new Vector3(0.04f, 0.26f, 0.04f));
            b.Add(Shape.Cube, s.bone, grip + new Vector3(0f, 0.28f, -0.07f), new Vector3(0.03f, 0.38f, 0.03f), new Vector3(-28f, 0f, 0f));
            b.Add(Shape.Cube, s.bone, grip + new Vector3(0f, -0.28f, -0.07f), new Vector3(0.03f, 0.38f, 0.03f), new Vector3(28f, 0f, 0f));
            b.Add(Shape.Cube, s.corruptGlow, grip + new Vector3(0.1f, 0f, 0.05f), new Vector3(0.02f, 0.02f, 0.45f), emission: 0.7f);
        }

        private static void Swarmling(Shapes b, ArtStyleLibrary s)
        {
            // Small corrupted wisp: smoky tapered tail, bone mask, big eyes and a flickering crest. Reads as many and weak.
            b.Cone(4f, s.corruptBody, new Vector3(0f, 0.2f, 0f), new Vector3(0.1f, 0.45f, 0.1f), segments: 7);
            b.Add(Shape.Ball, s.corruptBody, new Vector3(0f, 0.78f, 0f), new Vector3(0.42f, 0.4f, 0.38f));
            b.Add(Shape.Ball, s.bone, new Vector3(0f, 0.96f, 0.1f), new Vector3(0.26f, 0.24f, 0.22f));
            b.Pair(Shape.Ball, s.corruptGlow, new Vector3(0.06f, 0.98f, 0.2f), Vector3.one * 0.065f, emission: 1f);
            b.Cone(0f, s.corruptGlow, new Vector3(0f, 1.0f, -0.04f), new Vector3(0.2f, 0.3f, 0.2f), new Vector3(-20f, 0f, 0f), 6, emission: 0.9f);
            b.PairCone(0f, s.bone, new Vector3(0.2f, 0.74f, 0.1f), new Vector3(0.06f, 0.24f, 0.06f), new Vector3(60f, 0f, -20f), 6);
        }

        private static void Brute(Shapes b, ArtStyleLibrary s)
        {
            // Hulking ogre: wide armoured torso, small tusked head, shoulder spikes and a rune-cut stone club.
            b.Pair(Shape.Ball, s.corruptBody, new Vector3(0.2f, 0.42f, 0f), new Vector3(0.26f, 0.84f, 0.26f));
            b.Add(Shape.Ball, s.corruptArmor, new Vector3(0f, 1.25f, 0.05f), new Vector3(0.95f, 0.85f, 0.7f));
            b.Add(Shape.Ball, s.corruptBody, new Vector3(0f, 1.05f, 0.18f), new Vector3(0.7f, 0.55f, 0.5f));
            b.Add(Shape.Ball, s.bone, new Vector3(0f, 1.72f, 0.3f), new Vector3(0.32f, 0.3f, 0.32f));
            b.Pair(Shape.Ball, s.corruptGlow, new Vector3(0.07f, 1.76f, 0.45f), Vector3.one * 0.06f, emission: 1f);
            b.PairCone(0f, s.bone, new Vector3(0.08f, 1.64f, 0.42f), new Vector3(0.05f, 0.18f, 0.05f), new Vector3(-60f, 0f, 0f), 6);
            b.PairCone(0f, s.bone, new Vector3(0.42f, 1.58f, 0f), new Vector3(0.14f, 0.42f, 0.14f), new Vector3(0f, 0f, -30f), 6);
            b.Pair(Shape.Ball, s.corruptBody, new Vector3(0.55f, 1.1f, 0.1f), new Vector3(0.24f, 0.8f, 0.24f), new Vector3(0f, 0f, 10f));
            b.Cone(1.8f, s.leather, new Vector3(0.62f, 0.35f, 0.3f), new Vector3(0.1f, 0.8f, 0.1f), new Vector3(20f, 0f, 0f), 8);
            b.Add(Shape.Ball, s.corruptStone, new Vector3(0.62f, 0.32f, 0.22f), new Vector3(0.34f, 0.5f, 0.34f), new Vector3(20f, 0f, 0f));
            b.Add(Shape.Cube, s.corruptGlow, new Vector3(0.62f, 0.34f, 0.4f), new Vector3(0.05f, 0.3f, 0.02f), new Vector3(20f, 0f, 0f), emission: 0.8f);
        }

        private static void Shaman(Shapes b, ArtStyleLibrary s)
        {
            // Robed skull-masked healer with antlers and a tall staff topped by a glowing orb: the target to pick off.
            b.Cone(0.4f, s.corruptArmor, Vector3.zero, new Vector3(0.55f, 1.05f, 0.48f), segments: 7);
            b.Add(Shape.Ball, s.corruptBody, new Vector3(0f, 1.1f, 0.03f), new Vector3(0.32f, 0.42f, 0.26f));
            b.Add(Shape.Ball, s.bone, new Vector3(0f, 1.42f, 0.08f), Vector3.one * 0.22f);
            b.Pair(Shape.Ball, s.corruptGlow, new Vector3(0.05f, 1.44f, 0.18f), Vector3.one * 0.045f, emission: 1f);
            b.PairCone(0f, s.bone, new Vector3(0.08f, 1.5f, 0.02f), new Vector3(0.06f, 0.42f, 0.06f), new Vector3(-10f, 0f, -35f), 6);
            b.PairCone(0f, s.bone, new Vector3(0.2f, 1.66f, 0.02f), new Vector3(0.04f, 0.2f, 0.04f), new Vector3(-10f, 0f, 20f), 6);
            Vector3 staff = new Vector3(-0.32f, 0.05f, 0.18f);
            b.Cone(1f, s.bone, staff, new Vector3(0.05f, 1.75f, 0.05f), segments: 8);
            b.Add(Shape.Ball, s.corruptGlow, staff + Vector3.up * 1.88f, Vector3.one * 0.2f, emission: 1f);
            for (int i = 0; i < 3; i++)
                b.Cone(0f, s.bone, staff + Vector3.up * 1.72f, new Vector3(0.04f, 0.32f, 0.04f), new Vector3(-25f, i * 120f, 0f), 5);
        }

        private static void Warlord(Shapes b, ArtStyleLibrary s)
        {
            // Final boss (H ~ 3.4): armoured corrupted giant with a glowing core, horned crown, cape and greatsword.
            b.Pair(Shape.Ball, s.corruptArmor, new Vector3(0.3f, 0.62f, 0f), new Vector3(0.36f, 1.2f, 0.36f));
            b.PairCone(0.8f, s.corruptStone, new Vector3(0.3f, 0f, 0f), new Vector3(0.42f, 0.5f, 0.42f), segments: 8);
            b.Cone(1.3f, s.corruptArmor, new Vector3(0f, 0.95f, 0f), new Vector3(0.95f, 0.5f, 0.75f), segments: 8);
            b.Add(Shape.Cube, s.corruptArmor, new Vector3(0f, 1.6f, -0.42f), new Vector3(1.1f, 2.2f, 0.05f), new Vector3(-8f, 0f, 0f));
            b.Add(Shape.Ball, s.corruptArmor, new Vector3(0f, 1.85f, 0.05f), new Vector3(1.25f, 1.1f, 0.85f));
            b.Add(Shape.Ball, s.corruptGlow, new Vector3(0f, 1.9f, 0.42f), new Vector3(0.3f, 0.3f, 0.15f), emission: 1f);
            b.Pair(Shape.Ball, s.corruptStone, new Vector3(0.7f, 2.35f, 0f), new Vector3(0.55f, 0.4f, 0.55f));
            b.PairCone(0f, s.bone, new Vector3(0.75f, 2.5f, 0f), new Vector3(0.16f, 0.6f, 0.16f), new Vector3(0f, 0f, -25f), 6);
            b.Add(Shape.Ball, s.corruptBody, new Vector3(0f, 2.62f, 0.12f), new Vector3(0.4f, 0.42f, 0.4f));
            b.Pair(Shape.Ball, s.corruptGlow, new Vector3(0.09f, 2.65f, 0.3f), Vector3.one * 0.07f, emission: 1f);
            b.PairCone(0f, s.bone, new Vector3(0.15f, 2.75f, 0.05f), new Vector3(0.12f, 0.7f, 0.12f), new Vector3(-15f, 0f, -25f), 6);
            b.Pair(Shape.Ball, s.corruptArmor, new Vector3(0.75f, 1.75f, 0.15f), new Vector3(0.32f, 0.95f, 0.32f), new Vector3(0f, 0f, 12f));
            // Greatsword held forward in the right hand with a glowing edge.
            Vector3 euler = new Vector3(60f, 0f, 0f);
            Vector3 direction = Quaternion.Euler(euler) * Vector3.up;
            Vector3 hilt = new Vector3(0.82f, 1.3f, 0.25f);
            b.Add(Shape.Cube, s.corruptStone, hilt + direction * 1.0f, new Vector3(0.14f, 2.0f, 0.04f), euler);
            b.Add(Shape.Cube, s.corruptGlow, hilt + direction * 1.0f + new Vector3(0.075f, 0f, 0f), new Vector3(0.02f, 1.9f, 0.03f), euler, emission: 0.8f);
            b.Add(Shape.Cube, s.bone, hilt, new Vector3(0.45f, 0.06f, 0.08f), euler);
        }

        private static void Stronghold(Shapes b, ArtStyleLibrary s)
        {
            // Corrupted citadel on a jagged rock foundation: dark masonry with glowing mortar, spiked battlements,
            // angular towers with spiked tiled roofs, a twisted central spire, and a fanged gate maw under a skull,
            // with portcullis, tattered banners and corrupt braziers. The gate faces -Z (allies); the front stays
            // within the 6 x 4 x 4 objective box.
            Color stone = s.corruptStone, dark = s.corruptStone * 0.7f, armor = s.corruptArmor;
            b.Pattern = Surface.Rock;
            b.Add(Shape.Cube, dark, new Vector3(0f, 0.25f, 0.3f), new Vector3(7.6f, 0.5f, 4.6f));
            Vector3[] boulders =
            {
                new Vector3(-3.7f, 0.3f, -1.6f), new Vector3(3.8f, 0.25f, -1.5f), new Vector3(-3.9f, 0.3f, 1.2f), new Vector3(3.9f, 0.3f, 1.4f),
                new Vector3(-2.6f, 0.2f, 2.6f), new Vector3(2.3f, 0.2f, 2.7f), new Vector3(0.4f, 0.25f, 2.8f), new Vector3(-1.3f, 0.2f, 2.75f)
            };
            for (int i = 0; i < boulders.Length; i++)
            {
                // Rest on the ground: lift each chipped stone by most of its (noise-displaced) half height.
                Vector3 size = new Vector3(1.3f, 0.9f, 1.1f) * (0.8f + 0.1f * (i % 4));
                Vector3 rest = new Vector3(boulders[i].x, size.y * 0.62f - 0.05f, boulders[i].z);
                b.Piece(ProceduralMeshes.Faceted(i), dark * (0.9f + 0.05f * (i % 3)), rest, size, new Vector3(0f, i * 47f, 0f), groundY: 0f);
            }

            b.Pattern = Surface.CorruptMasonry;
            b.Add(Shape.Cube, stone, new Vector3(0f, 2.4f, -0.1f), new Vector3(5.6f, 3.8f, 3.0f));
            b.Add(Shape.Cube, dark, new Vector3(0f, 0.85f, -0.1f), new Vector3(5.76f, 0.7f, 3.16f));
            b.Pair(Shape.Cube, dark, new Vector3(1.4f, 1.8f, -1.72f), new Vector3(0.45f, 2.6f, 0.6f), new Vector3(-10f, 0f, 0f));
            b.Pattern = Surface.None;
            for (int i = 0; i < 9; i++)
            {
                float x = -2.5f + i * 0.625f;
                float tall = 0.55f + (i % 3) * 0.22f;
                b.Cone(0f, armor, new Vector3(x, 4.25f, -1.45f), new Vector3(0.38f, tall, 0.38f), new Vector3(-8f, 0f, 0f), 5);
                b.Cone(0f, armor, new Vector3(x, 4.25f, 1.25f), new Vector3(0.38f, tall, 0.38f), new Vector3(8f, 0f, 0f), 5);
            }
            b.Add(Shape.Cube, s.corruptGlow, new Vector3(0f, 4.27f, -0.1f), new Vector3(5.66f, 0.06f, 3.06f), emission: 0.7f);

            // Gate maw: glowing depth behind a portcullis, bone fangs around the arch, a horned skull above.
            b.Add(Shape.Cube, s.corruptBody * 0.4f, new Vector3(0f, 1.7f, -1.62f), new Vector3(1.9f, 2.5f, 0.06f));
            b.Add(Shape.Cube, s.corruptGlow, new Vector3(0f, 1.55f, -1.66f), new Vector3(1.5f, 2.1f, 0.04f), emission: 0.75f);
            b.Cone(1f, s.corruptGlow, new Vector3(0f, 2.6f, -1.68f), new Vector3(1.5f, 0.04f, 1.5f), new Vector3(90f, 0f, 0f), 12, emission: 0.75f);
            for (int i = -3; i <= 3; i++)
                b.Add(Shape.Cube, armor, new Vector3(i * 0.22f, 1.75f, -1.72f), new Vector3(0.07f, 2.5f, 0.07f));
            foreach (float y in new[] { 0.9f, 1.8f, 2.7f })
                b.Add(Shape.Cube, armor, new Vector3(0f, y, -1.74f), new Vector3(1.5f, 0.07f, 0.07f));
            for (int k = 0; k <= 6; k++)
            {
                float angle = k * 30f;
                Vector3 offset = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad), 0f) * 0.92f;
                b.Cone(0f, s.bone, new Vector3(0f, 2.6f, -1.76f) + offset, new Vector3(0.2f, 0.45f, 0.2f), new Vector3(0f, 0f, angle + 90f), 5);
            }
            b.Add(Shape.Ball, s.bone, new Vector3(0f, 3.75f, -1.68f), new Vector3(0.7f, 0.62f, 0.38f));
            b.Add(Shape.Cube, s.bone, new Vector3(0f, 3.4f, -1.72f), new Vector3(0.42f, 0.16f, 0.22f));
            b.Pair(Shape.Ball, s.corruptGlow, new Vector3(0.15f, 3.8f, -1.86f), new Vector3(0.17f, 0.14f, 0.06f), emission: 1f);
            b.PairCone(0f, s.bone, new Vector3(0.28f, 3.95f, -1.7f), new Vector3(0.16f, 0.65f, 0.16f), new Vector3(0f, 0f, -55f), 5);

            // Tattered banners with glowing sigils, and corrupt braziers.
            b.Pair(Shape.Cube, s.corruptBody, new Vector3(1.75f, 2.95f, -1.65f), new Vector3(0.7f, 1.8f, 0.05f));
            b.Pair(Shape.Cube, s.corruptBody, new Vector3(1.6f, 1.95f, -1.65f), new Vector3(0.28f, 0.45f, 0.05f), new Vector3(0f, 0f, 12f));
            b.Pair(Shape.Cube, s.corruptBody, new Vector3(1.92f, 1.92f, -1.65f), new Vector3(0.24f, 0.55f, 0.05f), new Vector3(0f, 0f, -8f));
            b.Pair(Shape.Cube, armor, new Vector3(1.75f, 3.88f, -1.67f), new Vector3(0.9f, 0.07f, 0.07f));
            b.Pair(Shape.Cube, s.corruptGlow, new Vector3(1.75f, 3.05f, -1.69f), new Vector3(0.26f, 0.26f, 0.03f), new Vector3(0f, 0f, 45f), emission: 0.9f);
            b.PairCone(0.6f, armor, new Vector3(1.8f, 0.5f, -1.95f), new Vector3(0.42f, 0.78f, 0.42f), segments: 6);
            b.PairCone(1.7f, armor * 0.8f, new Vector3(1.8f, 1.25f, -1.95f), new Vector3(0.36f, 0.22f, 0.36f), segments: 6);
            b.PairCone(1f, s.corruptGlow, new Vector3(1.8f, 1.43f, -1.95f), new Vector3(0.5f, 0.05f, 0.5f), segments: 8, emission: 1f);

            // Angular corner towers with spiked roofs, glowing slits and horns.
            for (int x = -1; x <= 1; x += 2)
                for (int z = 0; z < 2; z++)
                {
                    bool front = z == 0;
                    float height = front ? 5.4f : 6.6f;
                    Vector3 foot = new Vector3(2.95f * x, 0.5f, front ? -1.15f : 1.5f);
                    b.Pattern = Surface.CorruptMasonry;
                    b.Cone(0.75f, stone, foot, new Vector3(1.9f, height, 1.9f), new Vector3(0f, 30f, 0f), 6);
                    b.Cone(1.05f, dark, foot + Vector3.up * (height - 0.35f), new Vector3(1.55f, 0.4f, 1.55f), new Vector3(0f, 30f, 0f), 6);
                    b.Pattern = Surface.RoofTiles;
                    b.Cone(0f, armor, foot + Vector3.up * (height + 0.05f), new Vector3(2.0f, front ? 2.6f : 3.4f, 2.0f), new Vector3(0f, 30f, 0f), 6);
                    b.Pattern = Surface.None;
                    for (int h = 0; h < 2; h++)
                    {
                        Vector3 side = Quaternion.Euler(0f, h * 180f + 90f * x, 0f) * Vector3.forward;
                        b.Cone(0f, s.bone, foot + Vector3.up * (height + 0.4f) + side * 0.7f, new Vector3(0.18f, 0.9f, 0.18f),
                            new Vector3(0f, Mathf.Atan2(side.x, side.z) * Mathf.Rad2Deg, 0f) + new Vector3(60f, 0f, 0f), 5);
                    }
                    for (int w = 0; w < 2; w++)
                        b.Add(Shape.Cube, s.corruptGlow, foot + new Vector3(0f, height * (w == 0 ? 0.4f : 0.7f), -0.72f),
                            new Vector3(0.14f, 0.55f, 0.06f), emission: 0.9f);
                    b.Add(Shape.Cube, s.corruptGlow, foot + new Vector3(0.75f * x, height * 0.55f, 0f), new Vector3(0.06f, 0.55f, 0.14f), emission: 0.9f);
                }

            // Twisted central spire ringed with glowing bands.
            for (int k = 0; k < 3; k++)
            {
                float width = 2.3f * (1f - k * 0.22f);
                Vector3 foot = new Vector3(0f, 4.3f + k * 1.35f, 0.3f);
                b.Pattern = Surface.CorruptMasonry;
                b.Cone(0.72f, stone, foot, new Vector3(width, 1.4f, width), new Vector3(0f, k * 22f, 0f), 6);
                b.Pattern = Surface.None;
                b.Cone(1f, s.corruptGlow, foot + Vector3.up * 1.3f, new Vector3(width * 0.74f, 0.08f, width * 0.74f), new Vector3(0f, k * 22f, 0f), 6, emission: 0.9f);
            }
            b.Cone(0f, armor, new Vector3(0f, 8.35f, 0.3f), new Vector3(1.0f, 1.5f, 1.0f), new Vector3(0f, 66f, 0f), 6);

            // Corruption crystals bursting from the back corners and bone spikes along the front.
            Vector3[] crystals = { new Vector3(-3.6f, 0.3f, 2.3f), new Vector3(3.5f, 0.3f, 2.4f), new Vector3(-0.8f, 0.4f, 2.6f) };
            for (int i = 0; i < crystals.Length; i++)
                for (int c = 0; c < 3; c++)
                    b.Cone(0f, s.corruptGlow, crystals[i], new Vector3(0.45f, 1.6f - c * 0.35f, 0.45f) * (1f - i * 0.15f),
                        new Vector3(-20f + c * 22f, i * 70f + c * 120f, 15f - c * 12f), 4, emission: 0.85f);
            float[] spikes = { -3.1f, -2.4f, 2.4f, 3.1f };
            foreach (float x in spikes)
                b.Cone(0f, s.bone, new Vector3(x, 0.3f, -1.95f), new Vector3(0.28f, 1.3f, 0.28f), new Vector3(-35f, 0f, 0f), 6);
        }

        private static MovingPart Crystal(ArtStyleLibrary s)
        {
            Shapes b = new Shapes();
            b.Cone(0f, s.corruptGlow, Vector3.zero, new Vector3(1.1f, 1.6f, 1.1f), segments: 4, emission: 0.9f);
            b.Cone(0f, s.corruptGlow, Vector3.zero, new Vector3(1.1f, 1.1f, 1.1f), new Vector3(180f, 0f, 0f), 4, emission: 0.9f);
            return new MovingPart { Mesh = b.Bake("LB Stronghold Crystal"), Pivot = new Vector3(0f, EnemyCrystalHeight, 0.3f), Motion = PartMotion.Spin };
        }

        // ---------- Shape accumulation ----------

        internal enum Shape { Cube, Ball }

        // Procedural surface detail drawn by the toon shader from uv2.x (static props and buildings only).
        internal enum Surface { None = 0, Masonry = 1, RoofTiles = 2, Planks = 3, CorruptMasonry = 4, Rock = 5 }

        // Accumulates transformed primitives into one vertex-palette mesh (shared with the environment builder).
        internal sealed class Shapes
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<Vector3> normals = new List<Vector3>();
            private readonly List<Color> colors = new List<Color>();
            private readonly List<Vector2> surface = new List<Vector2>();
            private readonly List<Vector2> patterns = new List<Vector2>();
            private bool anyPattern;

            // Surface pattern stamped on every piece added while it is set.
            public Surface Pattern { get; set; }
            private readonly List<int> triangles = new List<int>();

            public void Add(Shape kind, Color color, Vector3 position, Vector3 scale, Vector3 euler = default,
                float emission = 0f, float spec = 0f)
                => Append(kind == Shape.Cube ? ProceduralMeshes.Box : ProceduralMeshes.Sphere, color, position, euler, scale, emission, spec);

            public void Pair(Shape kind, Color color, Vector3 position, Vector3 scale, Vector3 euler = default,
                float emission = 0f, float spec = 0f)
            {
                Add(kind, color, position, scale, euler, emission, spec);
                Add(kind, color, Mirror(position), scale, MirrorEuler(euler), emission, spec);
            }

            public void Cone(float topRatio, Color color, Vector3 basePosition, Vector3 scale, Vector3 euler = default,
                int segments = 12, float emission = 0f, float spec = 0f)
                => Append(ProceduralMeshes.Frustum(topRatio, segments), color, basePosition, euler, scale, emission, spec);

            public void PairCone(float topRatio, Color color, Vector3 basePosition, Vector3 scale, Vector3 euler = default,
                int segments = 12, float emission = 0f, float spec = 0f)
            {
                Cone(topRatio, color, basePosition, scale, euler, segments, emission, spec);
                Cone(topRatio, color, Mirror(basePosition), scale, MirrorEuler(euler), segments, emission, spec);
            }

            // Free-form piece for organic props. `sway` (0..1) marks how much the wind moves the top of the piece
            // (stored in uv1.y for the toon shader); `softCenter` bends normals toward a sphere around that point
            // so a clump of blobs shades as one soft canopy; `top` tints upward-facing surfaces (moss).
            // `groundY` bakes contact darkening into the lowest ~0.9 m so props sit on the ground (cheap AO).
            public void Piece(UnityEngine.Mesh source, Color color, Vector3 position, Vector3 scale, Vector3 euler = default,
                float sway = 0f, Vector3? softCenter = null, float softBlend = 0.7f, Color? top = null, float spec = 0f, float? groundY = null)
                => Append(source, color, position, euler, scale, 0f, spec, sway, softCenter, softBlend, top, groundY);

            private static Vector3 Mirror(Vector3 value) => new Vector3(-value.x, value.y, value.z);
            private static Vector3 MirrorEuler(Vector3 euler) => new Vector3(euler.x, -euler.y, -euler.z);

            private void Append(UnityEngine.Mesh source, Color color, Vector3 position, Vector3 euler, Vector3 scale,
                float emission, float spec, float sway = 0f, Vector3? softCenter = null, float softBlend = 0.7f, Color? top = null, float? groundY = null)
            {
                Matrix4x4 matrix = Matrix4x4.TRS(position, Quaternion.Euler(euler), scale);
                Matrix4x4 normalMatrix = matrix.inverse.transpose;
                Vector3[] sourceVertices = source.vertices;
                Vector3[] sourceNormals = source.normals;
                int[] sourceTriangles = source.triangles;
                int offset = vertices.Count;
                Color baked = new Color(color.r, color.g, color.b, Mathf.Clamp01(emission));
                for (int i = 0; i < sourceVertices.Length; i++)
                {
                    Vector3 point = matrix.MultiplyPoint3x4(sourceVertices[i]);
                    Vector3 normal = normalMatrix.MultiplyVector(sourceNormals[i]).normalized;
                    if (softCenter.HasValue)
                        normal = Vector3.Lerp(normal, (point - softCenter.Value).normalized, softBlend).normalized;
                    Color tinted = baked;
                    if (top.HasValue)
                    {
                        Color moss = Color.Lerp(baked, top.Value, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 0.85f, normal.y)));
                        tinted = new Color(moss.r, moss.g, moss.b, baked.a);
                    }
                    if (groundY.HasValue)
                    {
                        float occlusion = Mathf.Lerp(0.55f, 1f, Mathf.SmoothStep(0f, 1f, (point.y - groundY.Value) / 0.9f));
                        tinted = new Color(tinted.r * occlusion, tinted.g * occlusion, tinted.b * occlusion, tinted.a);
                    }
                    vertices.Add(point);
                    normals.Add(normal);
                    colors.Add(tinted);
                    surface.Add(new Vector2(spec, sway * Mathf.Clamp01(sourceVertices[i].y + 0.5f)));
                    // Metal trim and glowing pieces stay clean whatever pattern is set.
                    patterns.Add(new Vector2(spec > 0.5f || emission > 0f ? 0f : (float)Pattern, 0f));
                    anyPattern |= Pattern != Surface.None;
                }
                // Negative scale would flip winding; none of the silhouettes use it.
                foreach (int index in sourceTriangles) triangles.Add(offset + index);
            }

            public UnityEngine.Mesh Bake(string name)
            {
                UnityEngine.Mesh mesh = new UnityEngine.Mesh { name = name, hideFlags = HideFlags.DontSave };
                if (vertices.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetColors(colors);
                mesh.SetUVs(1, surface);
                if (anyPattern) mesh.SetUVs(2, patterns);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateBounds();
                mesh.UploadMeshData(false);
                return mesh;
            }
        }
    }
}

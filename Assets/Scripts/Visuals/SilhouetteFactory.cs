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

        private static void AlliedStronghold(Shapes b, ArtStyleLibrary s)
        {
            // Ivory keep with royal-blue roofs and gold trim; the glowing gate faces +Z (the battlefield).
            b.Add(Shape.Cube, s.ivory, new Vector3(0f, 1.6f, 0f), new Vector3(5.2f, 3.2f, 3.2f));
            b.Add(Shape.Cube, s.gold, new Vector3(0f, 3.25f, 0f), new Vector3(5.3f, 0.12f, 3.3f), spec: 1f);
            for (int i = 0; i < 6; i++)
            {
                float x = -2.3f + i * 0.92f;
                b.Add(Shape.Cube, s.ivory, new Vector3(x, 3.5f, 1.35f), new Vector3(0.5f, 0.45f, 0.5f));
                b.Add(Shape.Cube, s.ivory, new Vector3(x, 3.5f, -1.35f), new Vector3(0.5f, 0.45f, 0.5f));
            }
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 tower = new Vector3(2.6f * x, 0f, 1.5f * z);
                    b.Cone(0.9f, s.ivory, tower, new Vector3(1.4f, 4.4f, 1.4f), segments: 10);
                    b.Cone(1f, s.gold, tower + Vector3.up * 4.35f, new Vector3(1.45f, 0.12f, 1.45f), segments: 10, spec: 1f);
                    b.Cone(0f, s.royalBlue, tower + Vector3.up * 4.45f, new Vector3(1.7f, 1.9f, 1.7f), segments: 10);
                    b.Cone(0f, s.gold, tower + Vector3.up * 6.3f, new Vector3(0.12f, 0.5f, 0.12f), segments: 6, spec: 1f);
                }
            // Gate, banners and the winged-star crest toward the front.
            b.Add(Shape.Cube, s.lightGlow, new Vector3(0f, 0.9f, 1.62f), new Vector3(1.3f, 1.8f, 0.06f), emission: 0.6f);
            b.Add(Shape.Cube, s.gold, new Vector3(0f, 1.9f, 1.66f), new Vector3(1.6f, 0.22f, 0.14f), spec: 1f);
            b.Pair(Shape.Cube, s.royalBlue, new Vector3(1.4f, 2.0f, 1.64f), new Vector3(0.7f, 1.8f, 0.04f));
            b.Pair(Shape.Cube, s.gold, new Vector3(1.4f, 2.3f, 1.67f), new Vector3(0.12f, 0.6f, 0.02f), spec: 1f);
            b.Add(Shape.Cube, s.gold, new Vector3(0f, 2.75f, 1.66f), new Vector3(0.45f, 0.45f, 0.04f), new Vector3(0f, 0f, 45f), spec: 1f);
        }

        private static MovingPart LightCrystal(ArtStyleLibrary s)
        {
            Shapes b = new Shapes();
            b.Cone(0f, s.lightGlow, Vector3.zero, new Vector3(0.9f, 1.4f, 0.9f), segments: 4, emission: 0.9f);
            b.Cone(0f, s.lightGlow, Vector3.zero, new Vector3(0.9f, 0.9f, 0.9f), new Vector3(180f, 0f, 0f), 4, emission: 0.9f);
            return new MovingPart { Mesh = b.Bake("LB Allied Crystal"), Pivot = new Vector3(0f, 4.9f, 0f), Motion = PartMotion.Spin };
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
            // Ground-space fortress roughly matching the 6 x 4 x 4 objective collider; the gate faces -Z (allies).
            b.Add(Shape.Cube, s.corruptStone, new Vector3(0f, 1.7f, 0f), new Vector3(5.2f, 3.4f, 3.2f));
            for (int i = 0; i < 6; i++)
            {
                float x = -2.3f + i * 0.92f;
                b.Add(Shape.Cube, s.corruptStone, new Vector3(x, 3.6f, -1.35f), new Vector3(0.5f, 0.45f, 0.5f));
                b.Add(Shape.Cube, s.corruptStone, new Vector3(x, 3.6f, 1.35f), new Vector3(0.5f, 0.45f, 0.5f));
            }
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 tower = new Vector3(2.6f * x, 0f, 1.5f * z);
                    b.Cone(0.85f, s.corruptStone, tower, new Vector3(1.4f, 4.6f, 1.4f), segments: 8);
                    b.Cone(0f, s.corruptArmor, tower + Vector3.up * 4.6f, new Vector3(1.8f, 1.7f, 1.8f), segments: 8);
                    b.Add(Shape.Cube, s.corruptGlow, tower + new Vector3(0f, 3.4f, -0.62f), new Vector3(0.18f, 0.5f, 0.1f), emission: 0.9f);
                }
            b.Add(Shape.Cube, s.corruptGlow, new Vector3(0f, 0.9f, -1.62f), new Vector3(1.3f, 1.8f, 0.06f), emission: 0.7f);
            for (int i = -1; i <= 1; i++)
                b.Add(Shape.Cube, s.corruptArmor, new Vector3(i * 0.38f, 0.9f, -1.68f), new Vector3(0.1f, 1.8f, 0.1f));
            b.Add(Shape.Cube, s.corruptArmor, new Vector3(0f, 1.9f, -1.66f), new Vector3(1.6f, 0.25f, 0.14f));
            float[] spikes = { -2.1f, -1.3f, 1.3f, 2.1f };
            foreach (float x in spikes)
                b.Cone(0f, s.bone, new Vector3(x, 0f, -1.85f), new Vector3(0.28f, 1.3f, 0.28f), new Vector3(-35f, 0f, 0f), 6);
        }

        private static MovingPart Crystal(ArtStyleLibrary s)
        {
            Shapes b = new Shapes();
            b.Cone(0f, s.corruptGlow, Vector3.zero, new Vector3(1.1f, 1.6f, 1.1f), segments: 4, emission: 0.9f);
            b.Cone(0f, s.corruptGlow, Vector3.zero, new Vector3(1.1f, 1.1f, 1.1f), new Vector3(180f, 0f, 0f), 4, emission: 0.9f);
            return new MovingPart { Mesh = b.Bake("LB Stronghold Crystal"), Pivot = new Vector3(0f, 5.4f, 0f), Motion = PartMotion.Spin };
        }

        // ---------- Shape accumulation ----------

        internal enum Shape { Cube, Ball }

        // Accumulates transformed primitives into one vertex-palette mesh (shared with the environment builder).
        internal sealed class Shapes
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<Vector3> normals = new List<Vector3>();
            private readonly List<Color> colors = new List<Color>();
            private readonly List<Vector2> surface = new List<Vector2>();
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
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateBounds();
                mesh.UploadMeshData(false);
                return mesh;
            }
        }
    }
}

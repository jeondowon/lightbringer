using Lightbringer.Combat;

namespace Lightbringer.Units
{
    public enum EnemyKind { Raider, Archer, Swarm, Brute, Shaman, Boss }

    // Prototype enemy roster. Each role exists so a specific allied troop has a job:
    // Raider - Swordsman, Archer - Shieldbearer/Archer, Swarm - Mage splash, Brute - Spearman (Heavy),
    // Shaman - ranged troops picking off the healer, Boss - everything plus the hero.
    public static class EnemyCatalog
    {
        public const int Count = 6;
        public const int BossStage = 8;
        public static readonly string[] Names = { "약탈자", "적 궁병", "군집병", "파괴자", "주술사", "군주" };
        // First stage in which each kind can appear.
        private static readonly int[] FirstStage = { 1, 2, 3, 4, 5, BossStage };
        // Value at stage 0 plus growth per stage.
        private static readonly float[] BaseHealth = { 25, 18, 8, 90, 20, 800 };
        private static readonly float[] HealthGrowth = { 5, 4, 2, 18, 4, 100 };
        private static readonly float[] BaseDamage = { 3, 3, 1.5f, 8, 2, 24 };
        private static readonly float[] DamageGrowth = { 1, 1, 0.5f, 2, 0.5f, 2 };
        private static readonly float[] Ranges = { 1.25f, 6, 1.1f, 1.5f, 5, 2.2f };
        private static readonly float[] Intervals = { 1.2f, 1.4f, 0.8f, 1.8f, 1.6f, 1.2f };
        // The Warlord hunts the hero, so it moves at a bit over half the hero's 6 m/s: kiting works but never rests.
        private static readonly float[] Speeds = { 1.5f, 1.5f, 2.1f, 1f, 1.3f, 3.5f };
        private static readonly float[] Radii = { 0.35f, 0.35f, 0.3f, 0.5f, 0.35f, 0.75f };
        // Bonus EXP steps for tougher roles (x2 per step).
        private static readonly int[] Experience = { 0, 0, 0, 3, 2, 0 };

        public static int IntroStage(EnemyKind kind) => FirstStage[(int)kind];
        public static float MaxHealth(EnemyKind kind, int stage) => BaseHealth[(int)kind] + HealthGrowth[(int)kind] * stage;
        public static float AttackDamage(EnemyKind kind, int stage) => BaseDamage[(int)kind] + DamageGrowth[(int)kind] * stage;
        public static float Range(EnemyKind kind) => Ranges[(int)kind];
        public static float AttackInterval(EnemyKind kind) => Intervals[(int)kind];
        public static float Speed(EnemyKind kind) => Speeds[(int)kind];
        public static float Radius(EnemyKind kind) => Radii[(int)kind];
        // Wind-up before the blow lands, as a share of the attack motion. Brute and Warlord telegraph their slams.
        private static readonly float[] StrikePhases = { 0.35f, 0f, 0.25f, 0.5f, 0.3f, 0.55f };
        public static float StrikePhase(EnemyKind kind) => StrikePhases[(int)kind];
        public static ProjectileKind Projectile(EnemyKind kind) =>
            kind == EnemyKind.Archer ? ProjectileKind.Arrow
            : kind == EnemyKind.Shaman ? ProjectileKind.Bolt : ProjectileKind.None;
        public static float ProjectileSpeed(EnemyKind kind) => kind == EnemyKind.Archer ? 20f : 12f;
        public static float ProjectileArc(EnemyKind kind) => kind == EnemyKind.Archer ? 0.07f : 0f;
        // Heavy enemies take bonus damage from anti-heavy troops (Spearman).
        public static bool IsHeavy(EnemyKind kind) => kind == EnemyKind.Brute || kind == EnemyKind.Boss;
        public static float SplashRadius(EnemyKind kind) => kind == EnemyKind.Boss ? 2f : 0f;
        // Shaman heal pulse: amount per pulse, seconds between pulses, radius.
        public static float HealAmount(int stage) => 6f + stage;
        public const float HealInterval = 2f;
        public const float HealRadius = 6f;

        // EXP for an allied kill. Swarmlings come in numbers, so each is worth half a regular enemy.
        public static int ExperienceReward(EnemyKind kind, int stage)
        {
            if (kind == EnemyKind.Boss) return 60;
            int regular = 10 + stage * 2;
            return kind == EnemyKind.Swarm ? regular / 2 : regular + Experience[(int)kind] * 2;
        }

        // Squads per stage. Wave w on lane l uses squad (w + l) % count so parallel fronts differ.
        private const EnemyKind R = EnemyKind.Raider, A = EnemyKind.Archer, S = EnemyKind.Swarm,
            B = EnemyKind.Brute, H = EnemyKind.Shaman;
        private static readonly EnemyKind[][][] Squads =
        {
            new[] { new[] { R, R } },
            new[] { new[] { R, R }, new[] { R, A } },
            new[] { new[] { R, R, A }, new[] { S, S, S, R } },
            new[] { new[] { B, R, A }, new[] { S, S, S, R, R }, new[] { R, R, A, A } },
            new[] { new[] { B, R, H }, new[] { S, S, S, S, A }, new[] { R, R, A, H } },
            new[] { new[] { B, R, R, H }, new[] { S, S, S, S, S, A }, new[] { B, A, A, H } },
            new[] { new[] { B, B, R, H }, new[] { S, S, S, S, S, R, A }, new[] { B, R, A, A, H } },
            new[] { new[] { B, B, R, A, H }, new[] { S, S, S, S, S, S, B }, new[] { B, R, R, A, A, H } },
            new[] { new[] { R, R, A, A, H }, new[] { B, R, R, A }, new[] { S, S, S, S, A } },
            new[] { new[] { R, R, A, A, A }, new[] { B, R, A, H }, new[] { S, S, R, A, A } },
            new[] { new[] { B, B, R, H }, new[] { B, A, A, H }, new[] { R, R, A, H, H } },
            new[] { new[] { B, R, A, H }, new[] { S, S, S, S, S, A }, new[] { B, R, R, A, A } },
            new[] { new[] { S, S, S, S, S, S, A }, new[] { R, R, A, A, H }, new[] { B, R, S, S, S, H } },
            new[] { new[] { B, B, R, H }, new[] { S, S, S, S, S, A, A }, new[] { B, R, A, A, H } },
            new[] { new[] { B, B, A, H }, new[] { B, R, A, H, H }, new[] { S, S, S, S, R, A, A } },
            new[] { new[] { B, B, R, A, H }, new[] { S, S, S, S, S, S, B, A }, new[] { B, R, R, A, A, H } },
            // Act 3: larger mixed squads on every front, with the swarm and heavy roles growing toward the citadel.
            new[] { new[] { R, R, A, A, H }, new[] { B, R, R, A }, new[] { S, S, S, S, S, A } },
            new[] { new[] { B, R, A, A, H }, new[] { S, S, S, S, S, A, A }, new[] { R, R, R, A, H } },
            new[] { new[] { B, B, R, A, H }, new[] { R, R, A, A, H, H }, new[] { S, S, S, S, S, S, B } },
            new[] { new[] { S, S, S, S, S, S, S, A }, new[] { B, B, R, H }, new[] { S, S, S, S, R, A, H } },
            new[] { new[] { B, B, R, A, A, H }, new[] { S, S, S, S, S, S, B, A }, new[] { B, R, R, A, H, H } },
            new[] { new[] { B, B, B, A, H }, new[] { B, R, R, A, A, H }, new[] { S, S, S, S, S, S, B, B } },
            new[] { new[] { B, B, R, R, A, H }, new[] { S, S, S, S, S, S, B, A }, new[] { B, B, A, A, H, H } },
            new[] { new[] { B, B, R, A, A, H }, new[] { S, S, S, S, S, S, B, A }, new[] { B, R, R, A, A, H, H } },
        };

        public static EnemyKind[] Squad(int stage, int wave, int lane)
        {
            EnemyKind[][] options = Squads[UnityEngine.Mathf.Clamp(stage, 1, Squads.Length) - 1];
            return options[(wave + lane) % options.Length];
        }

        // The Warlord returns at the end of every act; the last stage adds the Demon King.
        public const int FinalStage = 24;
        public static bool HasBoss(int stage) => stage == BossStage || stage == 16 || stage == FinalStage;
        public static bool IsFinale(int stage) => stage == FinalStage;
        // The Demon King's one health pool, carried by the abyss core on the ground.
        public static float DemonCoreHealth(int stage) => MaxHealth(EnemyKind.Boss, stage) * 1.25f;

        public static bool AppearsIn(EnemyKind kind, int stage)
        {
            if (kind == EnemyKind.Boss) return HasBoss(stage);
            if (stage < 1) return false;
            foreach (EnemyKind[] squad in Squads[UnityEngine.Mathf.Clamp(stage, 1, Squads.Length) - 1])
                if (System.Array.IndexOf(squad, kind) >= 0) return true;
            return false;
        }
    }
}

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
        public static readonly string[] Names = { "Enemy Raider", "Enemy Archer", "Enemy Swarmling", "Enemy Brute", "Enemy Shaman", "Enemy Warlord" };
        // First stage in which each kind can appear.
        private static readonly int[] FirstStage = { 1, 2, 3, 4, 5, BossStage };
        // Value at stage 0 plus growth per stage.
        private static readonly float[] BaseHealth = { 25, 18, 8, 90, 20, 400 };
        private static readonly float[] HealthGrowth = { 5, 4, 2, 18, 4, 60 };
        private static readonly float[] BaseDamage = { 3, 3, 1.5f, 8, 2, 20 };
        private static readonly float[] DamageGrowth = { 1, 1, 0.5f, 2, 0.5f, 1 };
        private static readonly float[] Ranges = { 1.25f, 6, 1.1f, 1.5f, 5, 2.2f };
        private static readonly float[] Intervals = { 1.2f, 1.4f, 0.8f, 1.8f, 1.6f, 2f };
        private static readonly float[] Speeds = { 3, 3, 4.2f, 2, 2.6f, 1.8f };
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
        };

        public static EnemyKind[] Squad(int stage, int wave, int lane)
        {
            EnemyKind[][] options = Squads[UnityEngine.Mathf.Clamp(stage, 1, Squads.Length) - 1];
            return options[(wave + lane) % options.Length];
        }

        public static bool HasBoss(int stage) => stage == BossStage;

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

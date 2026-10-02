using System.Linq;
using Lightbringer.Combat;
using Lightbringer.Core;
using Lightbringer.Progression;
using Lightbringer.Units;
using UnityEngine;

namespace Lightbringer.EditorTools
{
    public static partial class GreyboxValidation
    {
        private static void ValidateEnemyRoster()
        {
            ValidateEnemyCatalog();
            ValidateEnemyRoles();
            ValidateEnemyWaves();
            ValidateBossStage();
        }

        private static void ValidateEnemyCatalog()
        {
            bool squadsValid = true;
            bool[,] seen = new bool[CampaignProgress.StageCount + 1, EnemyCatalog.Count];
            for (int stage = 1; stage <= CampaignProgress.StageCount; stage++)
                for (int wave = 0; wave < 3 + stage / 2; wave++)
                    for (int lane = 0; lane < 3; lane++)
                    {
                        EnemyKind[] squad = EnemyCatalog.Squad(stage, wave, lane);
                        // A squad (plus the boss on the final wave) must fit the nine spawn slots of one route.
                        squadsValid &= squad.Length > 0 && squad.Length <= 8;
                        foreach (EnemyKind kind in squad)
                        {
                            squadsValid &= kind != EnemyKind.Boss && EnemyCatalog.IntroStage(kind) <= stage;
                            seen[stage, (int)kind] = true;
                        }
                    }
            Check(squadsValid, "Enemy squads fit a route's spawn slots and only use roles introduced by that stage");
            Check(seen[2, (int)EnemyKind.Archer] && seen[3, (int)EnemyKind.Swarm] && seen[4, (int)EnemyKind.Brute]
                && seen[5, (int)EnemyKind.Shaman] && !seen[1, (int)EnemyKind.Archer],
                "Each enemy role first appears at its intro stage (Archer 2, Swarm 3, Brute 4, Shaman 5)");
            Check(EnemyCatalog.IsHeavy(EnemyKind.Brute) && EnemyCatalog.IsHeavy(EnemyKind.Boss) && !EnemyCatalog.IsHeavy(EnemyKind.Raider)
                && UnitCatalog.HeavyBonus(UnitKind.Spearman) == 2f && UnitCatalog.HeavyBonus(UnitKind.Swordsman) == 1f,
                "Brute and Boss are Heavy; only the Spearman carries the anti-Heavy bonus");
            Check(EnemyCatalog.MaxHealth(EnemyKind.Swarm, 3) < EnemyCatalog.MaxHealth(EnemyKind.Raider, 3)
                && EnemyCatalog.Speed(EnemyKind.Swarm) > EnemyCatalog.Speed(EnemyKind.Raider)
                && EnemyCatalog.MaxHealth(EnemyKind.Brute, 4) > EnemyCatalog.MaxHealth(EnemyKind.Raider, 4) * 3f
                && EnemyCatalog.Speed(EnemyKind.Brute) < EnemyCatalog.Speed(EnemyKind.Raider),
                "Swarmlings are weak and fast; Brutes are tough and slow");
        }

        private static void ValidateEnemyRoles()
        {
            Vector3 origin = new Vector3(-8000f, 1f, -8000f);
            Combatant attacker = CreateCombatUnit(origin, Faction.Allied, 10f);
            Combatant target = CreateCombatUnit(origin + Vector3.forward, Faction.Enemy);
            Combatant shaman = CreateCombatUnit(origin + new Vector3(2f, 0f, 1f), Faction.Enemy);
            try
            {
                UnitCombat combat = attacker.GetComponent<UnitCombat>();
                target.Configure(Faction.Enemy, 100f);
                target.IsHeavy = true;
                combat.ConfigureHeavyBonus(2f);
                Physics.SyncTransforms();
                combat.Tick(0.1f);
                Check(target.CurrentHealth == 80f, "Spearman bonus doubles damage against a Heavy enemy");
                target.IsHeavy = false;
                combat.Tick(1f);
                Check(target.CurrentHealth == 70f, "The anti-Heavy bonus does not apply to regular enemies");

                target.Invulnerable = true;
                combat.Tick(1f);
                Check(target.CurrentHealth == 70f && combat.Target != target && !target.TakeDamage(5f, attacker),
                    "A shielded objective ignores damage and is not chosen as a target");
                target.Invulnerable = false;

                attacker.TakeDamage(5f);
                float allyHealth = attacker.CurrentHealth;
                shaman.gameObject.AddComponent<UnitSupport>().Configure(8f, EnemyCatalog.HealInterval, EnemyCatalog.HealRadius);
                shaman.GetComponent<UnitSupport>().Tick(0.1f);
                Check(target.CurrentHealth == 78f && attacker.CurrentHealth == allyHealth,
                    "Enemy Shaman heals nearby enemies and never the allied troops");
            }
            finally
            {
                Object.DestroyImmediate(attacker.gameObject);
                Object.DestroyImmediate(target.gameObject);
                Object.DestroyImmediate(shaman.gameObject);
                Physics.SyncTransforms();
            }
        }

        private static CampaignSession StartValidationStage(int stage, Material material, out GameObject host)
        {
            host = new GameObject("Enemy roster validation host");
            CampaignSession session = host.AddComponent<CampaignSession>();
            CampaignProgress profile = new CampaignProgress();
            for (int cleared = 1; cleared < CampaignProgress.StageCount; cleared++) profile.CompleteStage(cleared);
            session.InitializeForValidation(profile);
            EditorAssets.ConfigureSession(session, material);
            Check(session.SelectStage(stage) && session.StartBattle(), "Validation can start campaign stage " + stage);
            Invoke(session.Battle.Objective, "OnEnable");
            return session;
        }

        private static Combatant[] LiveEnemies(PrototypeBattle battle) => battle.Root.GetComponentsInChildren<Combatant>()
            .Where(x => x.Faction == Faction.Enemy && x != battle.Objective.EnemyBase).ToArray();

        private static void ValidateEnemyWaves()
        {
            Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            GameObject host = null;
            try
            {
                PrototypeBattle battle = StartValidationStage(4, material, out host).Battle;
                EnemyWaveSpawner waves = battle.Waves;
                Physics.SyncTransforms();
                waves.Tick(0.1f);
                Combatant[] enemies = LiveEnemies(battle);
                Combatant brute = enemies.FirstOrDefault(x => x.name == EnemyCatalog.Names[(int)EnemyKind.Brute]);
                Combatant swarmling = enemies.FirstOrDefault(x => x.name == EnemyCatalog.Names[(int)EnemyKind.Swarm]);
                Check(enemies.Length == 8 && brute != null && swarmling != null
                    && enemies.Count(x => x.name == swarmling.name) == 3,
                    "Stage 4 first wave sends a Brute squad and a Swarm squad down different fronts");
                Check(brute.IsHeavy && brute.MaximumHealth == EnemyCatalog.MaxHealth(EnemyKind.Brute, 4)
                    && brute.GetComponent<CharacterController>().radius == EnemyCatalog.Radius(EnemyKind.Brute)
                    && !swarmling.IsHeavy && swarmling.MaximumHealth == EnemyCatalog.MaxHealth(EnemyKind.Swarm, 4),
                    "Spawned enemies take health, size and Heavy role from the catalog");
                Check(waves.TryGetKind(brute, out EnemyKind kind) && kind == EnemyKind.Brute
                    && waves.ExperienceFor(brute) > waves.ExperienceFor(enemies.First(x => x.name == EnemyCatalog.Names[0]))
                    && waves.ExperienceFor(swarmling) < waves.ExperienceFor(enemies.First(x => x.name == EnemyCatalog.Names[0])),
                    "Tougher enemies award more EXP; each Swarmling awards less");

                // Without movement the spawn slots fill up: later squads must wait in the queue, not vanish.
                int queued = enemies.Length;
                for (int wave = 1; wave < 3; wave++)
                {
                    waves.Tick(10f);
                    for (int lane = 0; lane < 2; lane++) queued += EnemyCatalog.Squad(4, wave, lane).Length;
                }
                int spawned = LiveEnemies(battle).Length;
                Check(waves.PendingCount > 0 && spawned + waves.PendingCount == queued,
                    "Crowded spawn slots delay enemies in a queue instead of dropping them");
                foreach (Combatant enemy in LiveEnemies(battle)) enemy.gameObject.SetActive(false);
                Physics.SyncTransforms();
                waves.Tick(0.6f);
                Check(waves.PendingCount == 0 && LiveEnemies(battle).Length + spawned == queued,
                    "Queued enemies spawn as soon as their route start clears");
            }
            finally
            {
                if (host != null) Object.DestroyImmediate(host);
                Object.DestroyImmediate(material);
                Physics.SyncTransforms();
            }
        }

        private static void ValidateBossStage()
        {
            Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            GameObject host = null;
            try
            {
                PrototypeBattle battle = StartValidationStage(EnemyCatalog.BossStage, material, out host).Battle;
                EnemyWaveSpawner waves = battle.Waves;
                Combatant stronghold = battle.Objective.EnemyBase;
                Check(waves.HasBoss && stronghold.Invulnerable && waves.Boss == null, "Final stage shields the enemy base until the boss falls");
                Check(!stronghold.TakeDamage(100000f, battle.Hero) && !battle.Objective.HasEnded,
                    "The shielded base cannot be destroyed early");
                for (int wave = 0; wave < waves.TotalWaves && waves.Boss == null; wave++)
                {
                    // Clear the route starts so every wave spawns at once.
                    foreach (Combatant enemy in LiveEnemies(battle)) enemy.gameObject.SetActive(false);
                    Physics.SyncTransforms();
                    waves.Tick(10f);
                }
                Combatant boss = waves.Boss;
                Check(boss != null && waves.WavesSpawned == waves.TotalWaves && boss.name == EnemyCatalog.Names[(int)EnemyKind.Boss]
                    && boss.IsHeavy && boss.MaximumHealth == EnemyCatalog.MaxHealth(EnemyKind.Boss, EnemyCatalog.BossStage)
                    && boss.GetComponent<CharacterController>().radius == EnemyCatalog.Radius(EnemyKind.Boss),
                    "The Warlord boss leads the final wave as a large Heavy enemy");
                // Three fronts start at x = -11, 0 and +11; the central one is x = 0.
                Check(Mathf.Abs(boss.transform.position.x) < 2f, "The boss enters on the central front");
                Check(waves.ExperienceFor(boss) == 60, "Defeating the boss awards objective-level EXP");
                boss.TakeDamage(100000f, battle.Hero);
                Check(waves.BossDefeated && !stronghold.Invulnerable && !battle.Objective.HasEnded,
                    "Defeating the boss lowers the base shield without ending the stage");
                stronghold.TakeDamage(100000f, battle.Hero);
                Check(battle.Objective.HasWon, "After the boss, destroying the base wins the final stage");
            }
            finally
            {
                if (host != null) Object.DestroyImmediate(host);
                Object.DestroyImmediate(material);
                Physics.SyncTransforms();
            }
        }
    }
}

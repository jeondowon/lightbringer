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
            ValidateImmediateNextWave();
            ValidateShieldedGateReinforcements();
            ValidateBossStage();
        }

        private static void ValidateEnemyCatalog()
        {
            bool squadsValid = true, smallSwarms = true, largeSwarms = false;
            bool[,] seen = new bool[CampaignProgress.StageCount + 1, EnemyCatalog.Count];
            for (int stage = 1; stage <= CampaignProgress.StageCount; stage++)
                for (int wave = 0; wave < 3 + stage / 2; wave++)
                    for (int lane = 0; lane < 3; lane++)
                    {
                        EnemyKind[] squad = EnemyCatalog.Squad(stage, wave, lane);
                        // Each assault squad fits the nine spawn slots of one route.
                        squadsValid &= squad.Length > 0 && squad.Length <= 8;
                        foreach (EnemyKind kind in squad)
                        {
                            squadsValid &= kind != EnemyKind.Boss && EnemyCatalog.IntroStage(kind) <= stage;
                            seen[stage, (int)kind] = true;
                        }
                        int swarm = squad.Count(member => member == EnemyKind.Swarm);
                        if (stage < EnemyCatalog.LargeSwarmStage) smallSwarms &= swarm <= EnemyCatalog.SmallSwarm;
                        else largeSwarms |= swarm > EnemyCatalog.SmallSwarm;
                    }
            Check(squadsValid, "Enemy squads fit a route's spawn slots and only use roles introduced by that stage");
            Check(seen[3, (int)EnemyKind.Archer] && seen[5, (int)EnemyKind.Swarm] && seen[7, (int)EnemyKind.Brute]
                && seen[10, (int)EnemyKind.Shaman] && !seen[2, (int)EnemyKind.Archer] && !seen[4, (int)EnemyKind.Swarm]
                && !seen[6, (int)EnemyKind.Brute] && !seen[9, (int)EnemyKind.Shaman],
                "Each enemy role first appears at its intro stage (Archer 3, Swarm 5, Brute 7, Shaman 10)");
            Check(smallSwarms && largeSwarms,
                "Swarm packs stay at four or fewer until the Mage arrives at stage 13, then grow");
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
            for (int cleared = 1; cleared < stage; cleared++) profile.CompleteStage(cleared);
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
                const int stage = 7;
                PrototypeBattle battle = StartValidationStage(stage, material, out host).Battle;
                EnemyWaveSpawner waves = battle.Waves;
                int lanes = battle.Paths.Length;
                Physics.SyncTransforms();
                waves.Tick(0.1f);
                Combatant[] enemies = LiveEnemies(battle);
                int firstWave = 0;
                for (int lane = 0; lane < lanes; lane++) firstWave += EnemyCatalog.Squad(stage, 0, lane).Length;
                Combatant brute = enemies.FirstOrDefault(x => x.name == EnemyCatalog.Names[(int)EnemyKind.Brute]);
                Combatant swarmling = enemies.FirstOrDefault(x => x.name == EnemyCatalog.Names[(int)EnemyKind.Swarm]);
                Check(lanes == 3 && enemies.Length == firstWave && brute != null && swarmling != null
                    && enemies.Count(x => x.name == swarmling.name) == 4,
                    "Stage 7 first wave sends a Brute squad and a Swarm squad down different fronts");
                Check(brute.IsHeavy && brute.MaximumHealth == EnemyCatalog.MaxHealth(EnemyKind.Brute, stage)
                    && brute.GetComponent<CharacterController>().radius == EnemyCatalog.Radius(EnemyKind.Brute)
                    && !swarmling.IsHeavy && swarmling.MaximumHealth == EnemyCatalog.MaxHealth(EnemyKind.Swarm, stage),
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
                    for (int lane = 0; lane < lanes; lane++) queued += EnemyCatalog.Squad(stage, wave, lane).Length;
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

        private static void ValidateImmediateNextWave()
        {
            Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            GameObject host = null;
            try
            {
                PrototypeBattle battle = StartValidationStage(1, material, out host).Battle;
                EnemyWaveSpawner waves = battle.Waves;
                GameObject obstruction = GameObject.CreatePrimitive(PrimitiveType.Cube);
                obstruction.transform.SetParent(battle.Root.transform);
                obstruction.transform.position = CampaignStages.EnemyRoutePoints(1, 0)[0] + new Vector3(0, 1, 1.3f);
                obstruction.transform.localScale = new Vector3(8, 4, 8);
                Physics.SyncTransforms();
                waves.Tick(0.01f);
                Check(waves.WavesSpawned == 1 && waves.PendingCount > 0 && LiveEnemies(battle).Length == 0,
                    "Blocked deployment retains the first wave in the spawn queue");
                waves.Tick(0.01f);
                Check(waves.WavesSpawned == 1, "Pending enemies prevent early advancement even on an empty battlefield");
                obstruction.SetActive(false);
                Physics.SyncTransforms();
                waves.Tick(0.6f);
                Combatant[] first = LiveEnemies(battle);
                Check(first.Length > 1 && waves.PendingCount == 0, "Unblocked first wave deploys before the interval expires");
                foreach (Combatant enemy in first.Skip(1)) enemy.TakeDamage(enemy.MaximumHealth);
                waves.Tick(0.01f);
                Check(waves.WavesSpawned == 1, "One surviving enemy preserves the normal wave interval");
                first[0].TakeDamage(first[0].MaximumHealth);
                waves.Tick(0f);
                waves.enabled = false;
                waves.Tick(1f);
                Check(waves.WavesSpawned == 1, "Paused or disabled spawners cannot advance a cleared wave");
                waves.enabled = true;
                for (int expected = 2; expected <= waves.TotalWaves; expected++)
                {
                    Physics.SyncTransforms();
                    waves.Tick(0.01f);
                    Check(waves.WavesSpawned == expected && LiveEnemies(battle).Length > 0
                        && battle.Objective.EnemyBase.Invulnerable,
                        "Clearing a wave immediately deploys the next wave while retaining the base shield");
                    foreach (Combatant enemy in LiveEnemies(battle)) enemy.TakeDamage(enemy.MaximumHealth);
                }
                Check(waves.AssaultCleared && !battle.Objective.EnemyBase.Invulnerable,
                    "Clearing the final immediate wave releases the stronghold shield");
                waves.Tick(10f);
                Check(waves.WavesSpawned == waves.TotalWaves && LiveEnemies(battle).Length == 0,
                    "The final wave never creates an extra assault");
            }
            finally
            {
                if (host != null) Object.DestroyImmediate(host);
                Object.DestroyImmediate(material);
                Physics.SyncTransforms();
            }
        }

        private static void ValidateShieldedGateReinforcements()
        {
            Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            try
            {
                foreach (int stage in new[] { 1, 3, 7 })
                {
                    GameObject host = null;
                    try
                    {
                        PrototypeBattle battle = StartValidationStage(stage, material, out host).Battle;
                        Physics.SyncTransforms();
                        battle.Waves.Tick(0.1f);
                        foreach (Combatant enemy in LiveEnemies(battle)) enemy.TakeDamage(enemy.MaximumHealth);
                        CharacterController template = battle.Root.transform.Find("Unit Template").GetComponent<CharacterController>();
                        UnitPathFollower[] allies = new UnitPathFollower[battle.Paths.Length];
                        for (int lane = 0; lane < allies.Length; lane++)
                        {
                            // Reproduce survivors already pressed against the protected wall.
                            CharacterController unit = Object.Instantiate(template,
                                new Vector3((lane - (allies.Length - 1) * 0.5f) * 2f, 0.85f, 75.5f),
                                Quaternion.identity, battle.Root.transform);
                            unit.GetComponent<Combatant>().Configure(Faction.Allied, 1000f);
                            allies[lane] = unit.GetComponent<UnitPathFollower>();
                            allies[lane].TryAssignPath(battle.Paths[lane]);
                            unit.gameObject.SetActive(true);
                        }
                        for (int step = 0; step < 200; step++)
                        {
                            Physics.SyncTransforms();
                            foreach (UnitPathFollower ally in allies) ally.Tick(0.05f);
                        }
                        foreach (UnitPathFollower ally in allies)
                        {
                            Vector3 deployment = ally.AssignedPath.GetPosition(ally.AssignedPath.Count - 2);
                            Check(ally.transform.position.z < deployment.z && !ally.HasReachedEnd,
                                "Shielded gate survivors return to their lane's approach in stage " + stage);
                            // Manual edit-mode controller moves can leave the overlap-query shape at its
                            // old position until a physics step. Re-register it before testing acquisition.
                            CharacterController controller = ally.GetComponent<CharacterController>();
                            controller.enabled = false;
                            controller.enabled = true;
                        }
                        Physics.SyncTransforms();
                        battle.Waves.Tick(10f);
                        foreach (UnitPathFollower ally in allies)
                        {
                            UnitCombat combat = ally.GetComponent<UnitCombat>();
                            combat.Tick(0.2f);
                            Check(combat.Target != null && combat.Target != battle.Objective.EnemyBase,
                                "Waiting allies detect the next assault in stage " + stage);
                        }
                        Combatant[] reinforcements = LiveEnemies(battle);
                        foreach (Combatant enemy in reinforcements) enemy.GetComponent<UnitCombat>().Tick(0.2f);
                        Check(reinforcements.Any(enemy => enemy.GetComponent<UnitCombat>().Target != null),
                            "Reinforcements detect waiting allies instead of walking past in stage " + stage);
                        foreach (Combatant enemy in reinforcements) enemy.TakeDamage(enemy.MaximumHealth);
                        battle.Objective.EnemyBase.Invulnerable = false;
                        foreach (UnitPathFollower ally in allies)
                        {
                            ally.ClearSteeringOverride();
                            float before = ally.transform.position.z;
                            ally.Tick(0.1f);
                            Check(ally.transform.position.z > before,
                                "Shield removal resumes the original lane's advance in stage " + stage);
                        }
                    }
                    finally
                    {
                        if (host != null) Object.DestroyImmediate(host);
                        Physics.SyncTransforms();
                    }
                }
            }
            finally { Object.DestroyImmediate(material); }
        }

        private static void ValidateBossStage()
        {
            Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            GameObject host = null;
            try
            {
                CampaignSession session = StartValidationStage(EnemyCatalog.BossStage, material, out host);
                PrototypeBattle battle = session.Battle;
                EnemyWaveSpawner waves = battle.Waves;
                Combatant stronghold = battle.Objective.EnemyBase;
                Check(waves.HasBoss && stronghold.Invulnerable && waves.Boss == null, "Final stage protects the stronghold until the regular assault is cleared");
                Check(!stronghold.TakeDamage(100000f, battle.Hero) && !battle.Objective.HasEnded,
                    "The shielded base cannot be destroyed early");
                ClearFinalAssault(battle);
                Check(waves.AssaultCleared && !stronghold.Invulnerable && waves.Boss == null,
                    "Clearing every assault unlocks the stronghold without spawning the boss");
                Invoke(battle.Hero.GetComponent<Lightbringer.Resources.FoodResource>(), "GenerateFood", 100f);
                Check(battle.Summoner.TrySummon(), "An allied soldier can be deployed before the rupture");
                Combatant ally = battle.Root.GetComponentsInChildren<Combatant>().First(x => x.Faction == Faction.Allied
                    && x != battle.Hero && x != battle.AlliedBase);
                // Edit-mode clones do not run Awake; initialize the soldier's nonserialized current health.
                ally.Configure(Faction.Allied, ally.MaximumHealth);
                float heroHp = battle.Hero.CurrentHealth, baseHp = battle.AlliedBase.CurrentHealth;
                // One EXP short, so the stronghold's reward is what levels the hero up.
                session.Progress.experience = session.Progress.ExperienceToNext - 1;
                stronghold.TakeDamage(100000f, battle.Hero);
                Check(!battle.Objective.HasEnded && battle.Encounter.IsCinematic && !battle.Summoner.enabled
                    && !battle.Abilities.enabled && !session.IsChoosing && session.Progress.pendingLevels > 0,
                    "Stronghold collapse starts the cinematic, blocks input and defers its level-up reward");
                Check(!battle.Summoner.TrySummon() && !battle.Hero.TakeDamage(1000), "Cinematic blocks deployment and protects the hero");
                battle.Encounter.Tick(BossEncounter.WaveSeconds);
                Check(!ally.IsAlive && battle.Encounter.FallenAllies == 1 && battle.Hero.CurrentHealth == heroHp
                    && battle.AlliedBase.CurrentHealth == baseHp, $"Rupture wipes summoned allies while preserving the hero and allied base [alive={ally.IsAlive}, fallen={battle.Encounter.FallenAllies}, hero={battle.Hero.CurrentHealth}/{heroHp}, base={battle.AlliedBase.CurrentHealth}/{baseHp}, phase={battle.Encounter.Phase}]");
                Combatant boss = waves.Boss;
                Check(boss != null && waves.WavesSpawned == waves.TotalWaves && boss.name == EnemyCatalog.Names[(int)EnemyKind.Boss]
                    && boss.IsHeavy && boss.MaximumHealth == EnemyCatalog.MaxHealth(EnemyKind.Boss, EnemyCatalog.BossStage)
                    && boss.GetComponent<CharacterController>().radius == EnemyCatalog.Radius(EnemyKind.Boss),
                    "The Warlord appears after the destroyed stronghold with its configured Heavy stats");
                Check(Mathf.Abs(boss.transform.position.x) < 2f, "The boss enters on the central front");
                Check(waves.ExperienceFor(boss) == 60 && boss.Invulnerable && !boss.GetComponent<BossCombat>().enabled,
                    "The arriving boss is protected and cannot attack during the dialogue");
                battle.Encounter.Tick(BossEncounter.ArrivalSeconds);
                Check(battle.Encounter.Phase == BossEncounterPhase.Dialogue && !battle.Objective.HasEnded,
                    "Arrival leads into hero/boss dialogue without awarding victory");
                FinishBossDialogue(battle);
                Check(session.IsChoosing && session.IsBattlePaused, "Deferred growth pauses the fight immediately after dialogue");
                while (session.IsChoosing) session.ChooseGrowth(0);
                Check(!boss.Invulnerable && !battle.Hero.Invulnerable && battle.Summoner.enabled && boss.GetComponent<BossCombat>().enabled,
                    "Dialogue restores hero input, summoning and the vulnerable boss");
                ValidateBossAttacks(session);
                Check(!battle.Objective.CompleteBossVictory(), "The story cannot award victory before the boss is defeated");
                int victories = 0, defeats = 0;
                battle.Objective.Completed += won => { if (won) victories++; };
                boss.Died += _ => defeats++;
                session.Progress.experience = session.Progress.ExperienceToNext - 1;
                boss.TakeDamage(100000f, battle.Hero);
                int goldBeforeClear = session.Progress.gold;
                Check(waves.BossDefeated && !battle.Objective.HasEnded && !session.Progress.IsComplete
                    && battle.Encounter.Phase == BossEncounterPhase.RetreatDialogue && victories == 0,
                    "Defeating the boss opens the retreat story without granting the stage victory");
                Check(session.Progress.pendingLevels > 0 && !session.IsChoosing && !session.CanPause
                    && !battle.Summoner.enabled && !session.ReturnToPreparation(),
                    "The retreat defers growth, blocks battle input and prevents leaving before completion");
                Check(!boss.TakeDamage(100000f, battle.Hero) && defeats == 1,
                    "Further hits cannot duplicate the boss defeat reward");
                float retreatHeroHp = battle.Hero.CurrentHealth, retreatBaseHp = battle.AlliedBase.CurrentHealth;
                battle.Hero.TakeDamage(100000); battle.AlliedBase.TakeDamage(100000);
                Check(battle.Hero.CurrentHealth == retreatHeroHp && battle.AlliedBase.CurrentHealth == retreatBaseHp,
                    "Hero and stronghold remain protected throughout the retreat story");
                Check(!battle.Encounter.AdvanceDialogue(), "A lethal-hit input cannot skip the first retreat line");
                for (int i = 0; i < BossEncounter.RetreatLines.Length; i++)
                {
                    battle.Encounter.Tick(.8f);
                    Check(battle.Encounter.AdvanceDialogue(), "Retreat dialogue advances with deliberate input");
                    Check(!battle.Objective.HasEnded, "Retreat dialogue does not expose the result screen");
                }
                battle.Encounter.Tick(BossEncounter.RetreatFadeSeconds * .5f);
                Check(!battle.Objective.HasEnded && session.Progress.gold == goldBeforeClear,
                    "The closing illustration fades before clear rewards or victory are granted");
                battle.Encounter.Tick(BossEncounter.RetreatFadeSeconds);
                Check(battle.Objective.HasWon && session.Progress.cleared[7] && session.Progress.unlockedStage == 9 && !session.Progress.IsComplete && victories == 1
                    && session.Progress.gold == goldBeforeClear + 500,
                    "Completing the retreat grants the stage victory and clear reward exactly once");
                battle.Encounter.Tick(10);
                Check(!battle.Objective.CompleteBossVictory() && victories == 1
                    && session.Progress.gold == goldBeforeClear + 500,
                    "Repeated completion cannot duplicate the clear reward");
            }
            finally
            {
                if (host != null) Object.DestroyImmediate(host);
                Object.DestroyImmediate(material);
                Physics.SyncTransforms();
            }
        }

        private static void ClearFinalAssault(PrototypeBattle battle)
        {
            for (int attempt = 0; attempt < 100 && !battle.Waves.AssaultCleared; attempt++)
            {
                battle.Waves.Tick(10f);
                foreach (Combatant enemy in LiveEnemies(battle)) enemy.TakeDamage(enemy.MaximumHealth * 10);
                Physics.SyncTransforms();
            }
            Check(battle.Waves.AssaultCleared, "Every queued assault soldier can spawn and be defeated");
        }

        private static void FinishBossDialogue(PrototypeBattle battle)
        {
            for (int i = 0; i < BossEncounter.Lines.Length; i++)
            { battle.Encounter.Tick(0.3f); Check(battle.Encounter.AdvanceDialogue(), "Boss dialogue line advances once"); }
        }

        private static void ValidateBossAttacks(CampaignSession session)
        {
            PrototypeBattle battle = session.Battle;
            Combatant boss = battle.Waves.Boss;
            BossCombat combat = boss.GetComponent<BossCombat>();
            battle.Hero.SetMaximumHealth(2000); battle.Hero.Heal(2000);
            Vector3 start = new Vector3(0, 0.9f, 10);
            boss.transform.position = start;
            battle.Hero.transform.position = start + Vector3.forward * 5;
            Physics.SyncTransforms();
            float hp = battle.Hero.CurrentHealth;
            combat.Tick(3);
            Check(combat.CurrentAttack == BossAttack.Crescent && combat.IsAttacking && battle.Hero.CurrentHealth == hp,
                "Crescent begins with a harmless readable wind-up");
            Check(session.SetPaused(true), "Boss wind-up supports the campaign pause");
            combat.Tick(10);
            Check(combat.SpecialsResolved == 0, "Paused boss cannot finish a telegraph or apply damage");
            session.SetPaused(false);
            combat.Tick(2);
            DarkCrescent crescent = battle.Root.GetComponentInChildren<DarkCrescent>();
            Check(crescent != null, "The sword swing launches an independent crescent projectile");
            crescent.Tick(0.4f);
            Check(battle.Hero.CurrentHealth == hp - DarkCrescent.Damage, "Swept crescent collision hits an allied hero");
            crescent.Tick(0.01f);
            Check(battle.Hero.CurrentHealth == hp - DarkCrescent.Damage, "A crescent cannot hit the same target twice");
            crescent.Tick(3);

            combat.Tick(5); // cleave
            hp = battle.Hero.CurrentHealth;
            battle.Hero.transform.position = start - Vector3.forward * 4;
            combat.Tick(2);
            Check(combat.CurrentAttack == BossAttack.Cleave && battle.Hero.CurrentHealth == hp,
                "Moving behind the boss dodges its locked forward cleave");
            battle.Hero.transform.position = start + Vector3.forward * 5;
            combat.Tick(5); // lightning locks old position
            battle.Hero.transform.position += Vector3.right * 8;
            combat.Tick(2);
            Check(combat.CurrentAttack == BossAttack.Lightning && battle.Hero.CurrentHealth == hp,
                "Lightning strikes its marked location instead of tracking a dodging hero");
            battle.Hero.transform.position = start + Vector3.forward * 5;
            combat.Tick(5); combat.Tick(2); combat.Tick(0.7f);
            Check(combat.CurrentAttack == BossAttack.Charge && battle.Hero.CurrentHealth < hp,
                "Charge moves along its telegraphed lane and damages crossed allies");
            hp = battle.Hero.CurrentHealth;
            battle.Hero.transform.position = boss.transform.position + Vector3.forward * 4;
            combat.Tick(5);
            battle.Hero.transform.position += Vector3.right * 12;
            combat.Tick(2);
            Check(combat.CurrentAttack == BossAttack.Shockwave && battle.Hero.CurrentHealth == hp,
                "Leaving the warned radius avoids the boss shockwave");
            combat.Tick(5); combat.Tick(2);
            Check(combat.CurrentAttack == BossAttack.Summon && LiveEnemies(battle).Length > 1,
                "The summoning special creates reinforcements through the wave registry");
            boss.TakeDamage(boss.MaximumHealth * .55f);
            Check(combat.IsEnraged && combat.SpecialsResolved == 6, "All six specials resolve and low health activates faster recovery");
        }
    }
}

using System;
using System.IO;
using System.Linq;
using Lightbringer.Combat;
using Lightbringer.Core;
using Lightbringer.Progression;
using Lightbringer.Resources;
using Lightbringer.UI;
using Lightbringer.Units;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Lightbringer.EditorTools
{
    public static partial class GreyboxValidation
    {
        private static void ValidateActThree()
        {
            ValidateActThreeMigration();
            ValidateActThreeScenery();
            Check(CampaignProgress.StageCount == 24 && EnemyCatalog.HasBoss(24) && EnemyCatalog.IsFinale(24)
                && !EnemyCatalog.HasBoss(23) && CampaignStages.ActOf(17) == 3 && CampaignStages.ActOf(24) == 3,
                "The campaign holds three acts of eight stages with the final battle at 24");
            Check(Enumerable.Range(17, 8).All(stage => CampaignStages.PathCount(stage) >= 2)
                && CampaignStages.PathCount(17) == 2 && CampaignStages.WaveCount(17) < CampaignStages.WaveCount(23),
                "Act 3 opens a step easier on two Paths, then holds three fronts toward the citadel");
            Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            try
            {
                for (int stage = 17; stage <= 24; stage++)
                {
                    GameObject host = null;
                    try
                    {
                        CampaignSession session = StartValidationStage(stage, material, out host);
                        PrototypeBattle battle = session.Battle;
                        Check(battle.Paths.Length == CampaignStages.PathCount(stage) && battle.Paths.All(path => path.IsValid),
                            $"Act 3 stage {stage} builds valid assigned paths");
                        for (int lane = 0; lane < battle.Paths.Length; lane++)
                        {
                            Vector3[] route = CampaignStages.RoutePoints(stage, lane);
                            Vector3[] reverse = CampaignStages.EnemyRoutePoints(stage, lane);
                            Check(route.All(p => Mathf.Abs(p.x) < BattlefieldLayout.HalfWidth && p.z >= BattlefieldLayout.MinZ && p.z <= BattlefieldLayout.MaxZ)
                                && reverse.Take(reverse.Length - 1).SequenceEqual(route.Take(route.Length - 1).Reverse()),
                                $"Stage {stage} lane {lane + 1} stays in bounds and enemies retrace its corridor");
                        }
                        PreparationView brief = new PreparationView(new VisualElement());
                        ClearFinalAssault(battle);
                        if (stage == 24) ValidateFinalBattle(session);
                        else
                        {
                            Check(battle.Encounter == null, $"Stage {stage} is a regular battle without a boss encounter");
                            battle.Objective.EnemyBase.TakeDamage(100000, battle.Hero);
                            Check(battle.Objective.HasWon && session.Progress.cleared[stage - 1] && session.Progress.unlockedStage == stage + 1,
                                $"Stage {stage} victory unlocks the next Act 3 battle");
                        }
                        while (session.IsChoosing) session.ChooseGrowth(0);
                        Check(session.ReturnToPreparation(), $"Stage {stage} returns to preparation");
                        brief.Refresh(session);
                        Check(brief.Visible && brief.DestinationText == CampaignStages.Name(session.SelectedStage),
                            $"Act 3 stage {stage} briefing renders after the battle");
                        CampaignMapView map = new CampaignMapView(new VisualElement(), null);
                        map.Refresh(session, true);
                        Check(map.ViewedAct == 3 && map.Select(stage) && map.Select(Mathf.Min(24, stage + 1)),
                            $"Stage {stage} is replayable on the Act 3 map and the next beacon is open");
                    }
                    finally { if (host != null) Object.DestroyImmediate(host); Physics.SyncTransforms(); }
                }
            }
            finally { Object.DestroyImmediate(material); }
        }

        private static void ValidateFinalBattle(CampaignSession session)
        {
            PrototypeBattle battle = session.Battle;
            BossEncounter encounter = battle.Encounter;
            Check(encounter != null && encounter.IsFinale && !encounter.IsIllustrated && encounter.KeepsArmy,
                "Stage 24 runs the final encounter over the live battlefield");
            Invoke(battle.Hero.GetComponent<FoodResource>(), "GenerateFood", 100f);
            Combatant ally = null;
            battle.Summoner.Summoned += unit => ally = unit;
            Check(battle.Summoner.TrySummon(), "An army can be present when the two darknesses arrive");
            // Edit-mode probes do not run Awake; initialize health as in the Act 2 arrival check.
            ally.Configure(Faction.Allied, ally.MaximumHealth);
            ally.transform.position = new Vector3(-6, .85f, BattlefieldLayout.EnemyBaseZ - 12);
            Physics.SyncTransforms();
            battle.Objective.EnemyBase.TakeDamage(100000, battle.Hero);
            encounter.Tick(.01f);
            encounter.Tick(BossEncounter.ArrivalSeconds);
            Combatant warlord = battle.Waves.Boss, core = encounter.DemonCore;
            Check(ally.IsAlive && encounter.FallenAllies == 0 && warlord != null && core != null
                && encounter.Phase == BossEncounterPhase.Dialogue && encounter.CurrentLines.SequenceEqual(BossEncounter.FinaleLines)
                && encounter.CurrentSpeakers.Length == encounter.CurrentLines.Length,
                $"Final arrival keeps the army, raises the Warlord and the abyss core, then speaks [allyAlive={ally.IsAlive}, fallen={encounter.FallenAllies}, warlord={warlord != null}, core={core != null}, phase={encounter.Phase}]");
            Check(core.Faction == Faction.Enemy && core.IsHeavy && core.StatusImmune && core.Invulnerable
                && core.MaximumHealth == EnemyCatalog.DemonCoreHealth(24) && battle.Waves.ExperienceFor(core) == 60
                && GroundGap(core.transform.position, battle.Objective.EnemyBase != null ? battle.Objective.EnemyBase.transform.position : core.transform.position) < .1f,
                "The Demon King's one health pool stands on the ruined stronghold, shielded during the dialogue");
            float pathEnd = battle.Paths.Min(path => GroundGap(path.GetPosition(path.Count - 1), core.transform.position));
            Check(pathEnd < 3f, "Every Path ends at the abyss core, so advancing troops can strike the Demon King");
            for (int i = 0; i < BossEncounter.FinaleLines.Length; i++)
            { encounter.Tick(.3f); Check(encounter.AdvanceDialogue(), "Final dialogue line advances once"); }
            while (session.IsChoosing) session.ChooseGrowth(0);
            DemonKingCombat demonKing = encounter.DemonKing;
            BossCombat lord = warlord.GetComponent<BossCombat>();
            Check(encounter.Phase == BossEncounterPhase.Fight && !core.Invulnerable && demonKing.enabled && lord.enabled
                && encounter.ObjectiveText.Contains("군주") && encounter.ObjectiveText.Contains("마왕"),
                "The fight starts with both bosses active and both named in the objective");

            battle.Hero.SetMaximumHealth(5000); battle.Hero.Heal(5000);
            // The Warlord stands within reach of every special; only BossCombat.Tick runs, so nothing else strikes.
            battle.Hero.transform.position = new Vector3(20, 1.05f, 20);
            warlord.transform.position = new Vector3(20, .85f, 15);
            Physics.SyncTransforms();
            lord.Tick(.01f);
            demonKing.Tick(DemonKingCombat.Gap + 4f);
            Check(demonKing.IsAttacking && demonKing.CurrentAttack == DemonKingAttack.DarkLightning && !lord.IsAttacking,
                "The Demon King opens with a dark lightning warning at the hero's position");
            lord.Tick(10f);
            Check(!lord.IsAttacking, "The Warlord holds his specials while the sky is casting");
            float before = battle.Hero.CurrentHealth;
            demonKing.Tick(DemonKingCombat.LightningWindup + .01f);
            float expected = DemonKingCombat.LightningDamage * (1 - Mathf.Clamp(battle.Hero.DamageReduction, 0, .8f));
            Check(!demonKing.IsAttacking && Mathf.Abs(before - battle.Hero.CurrentHealth - expected) < .01f && demonKing.SpecialsResolved == 1,
                "Dark lightning lands on its locked warning and the Demon King yields the next big attack");
            lord.Tick(.01f);
            Check(!lord.IsAttacking, "A short gap follows each sky attack before the Warlord may cast");
            demonKing.Tick(DemonKingCombat.Gap + .01f);
            ally.transform.position = new Vector3(0, .85f, 40);
            Physics.SyncTransforms();
            demonKing.Tick(6f);
            Check(demonKing.IsAttacking && demonKing.CurrentAttack == DemonKingAttack.AbyssRain,
                "The second sky attack rains on the most advanced allied front");
            float allyBefore = ally.CurrentHealth;
            demonKing.Tick(DemonKingCombat.RainWindup + DemonKingCombat.RainStagger * 4);
            Check(!demonKing.IsAttacking && ally.CurrentHealth < allyBefore, "Abyss rain lands in turn and presses the front");

            float gold = session.Progress.gold;
            core.TakeDamage(core.MaximumHealth * 10, battle.Hero);
            Check(!battle.Objective.HasEnded && lord.Bereaved && encounter.Phase == BossEncounterPhase.Fight
                && session.Progress.gold > gold && encounter.ObjectiveText.Contains("군주"),
                "Destroying the core first strengthens the Warlord without ending the battle");
            if (encounter.Sky != null) Check(encounter.Sky.Dissipating, "The sky darkness disperses when its core falls");
            warlord = battle.Waves.Boss;
            warlord.TakeDamage(warlord.MaximumHealth * 10, battle.Hero);
            Check(!battle.Objective.HasEnded && encounter.IsRetreating && encounter.CurrentLines.SequenceEqual(BossEncounter.FinaleEndLines)
                && !session.Progress.cleared[23],
                "With both darknesses down the ending plays before any reward");
            for (int i = 0; i < BossEncounter.FinaleEndLines.Length; i++) { encounter.Tick(.8f); encounter.AdvanceDialogue(); }
            encounter.Tick(BossEncounter.RetreatFadeSeconds);
            Check(battle.Objective.HasWon && session.Progress.IsComplete && session.Progress.unlockedStage == 24
                && session.LastResult.Contains("캠페인 완료"),
                "The final victory completes the 24-stage campaign");
        }

        private static float GroundGap(Vector3 a, Vector3 b) { a.y = b.y; return Vector3.Distance(a, b); }

        private static void ValidateActThreeScenery()
        {
            var art = UnityEditor.AssetDatabase.LoadAssetAtPath<Lightbringer.Visuals.ArtStyleLibrary>(ArtStyleSetup.LibraryPath);
            Check(art != null, "Act 3 environmental validation has the real art library");
            string before = JsonUtility.ToJson(art);
            Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            string[] expected = { "Abandoned Village House", "Guarded Stone Bridges", "Shattered Hill Sanctuary", "Dead Wood Watchtower",
                "Outer Field Encampment", "Charcoal Canyon Crags", "Outer Gate West Tower", "Final Citadel of the Demon King" };
            try
            {
                for (int stage = 17; stage <= 24; stage++)
                {
                    GameObject host = new GameObject("Act Three Scenery Probe");
                    try
                    {
                        PrototypeBattle battle = PrototypeBattleBuilder.Build(stage, new CampaignProgress(), EditorAssets.InputActions,
                            material, Shader.Find("Universal Render Pipeline/Unlit"), host.transform, art);
                        battle.Root.SetActive(true);
                        var scenery = battle.Root.GetComponent<Lightbringer.Visuals.ActTwoScenery>();
                        var terrain = battle.Root.GetComponentInChildren<Lightbringer.Visuals.BattlefieldEnvironment>(true);
                        Check(scenery != null && scenery.IsActThree && terrain != null && terrain.transform.Find(expected[stage - 17]) != null,
                            $"Act 3 stage {stage} builds its defining landmark: {expected[stage - 17]}");
                        Transform foliage = terrain.transform.Find("Surviving Olive Grey Foliage");
                        int living = foliage != null ? foliage.GetComponent<MeshFilter>().sharedMesh.vertexCount : 0;
                        Check(scenery.DeadTrees > 30 && scenery.Stumps > 10 && scenery.Landmarks >= 2 && scenery.Fissures > 5
                            && terrain.transform.Find("Dark Fissures and Ash") != null && terrain.transform.Find("Black Valley Mist") != null
                            && (stage == 17 || living == 0),
                            $"Act 3 stage {stage} is fully withered with fissures and black valley mist [trees={scenery.DeadTrees}, stumps={scenery.Stumps}, landmarks={scenery.Landmarks}, fissures={scenery.Fissures}]");
                        Check(scenery.Palette != art && scenery.Palette.grassLight.grayscale < .32f && scenery.Palette.fogColor.grayscale < .32f
                            && scenery.Palette.terrainMaterial != art.terrainMaterial
                            && scenery.Palette.terrainMaterial.GetColor("_CorruptGlow") == Color.black,
                            $"Act 3 stage {stage} owns a darker ash palette without neon ground cracks");
                        Check(terrain.GetComponentsInChildren<Collider>(true).Length == 1 && terrain.Terrain != null,
                            $"Act 3 stage {stage} dressing preserves the single terrain collision surface");
                        if (stage == 18) Check(scenery.Bridges == battle.Paths.Length,
                            "Every assigned stage 18 route has a stone bridge across the shallow river");
                        if (stage == 24)
                        {
                            var sky = battle.Root.GetComponentInChildren<Lightbringer.Visuals.DemonKingSky>(true);
                            Check(sky != null && !sky.Revealed && sky.Presence > 0 && sky.EyeOpening == 0
                                && sky.GetComponentsInChildren<Renderer>().Length >= 8 && sky.GetComponentsInChildren<Collider>(true).Length == 0,
                                "The final battlefield opens under the Demon King's storm bank with its eyes still closed");
                            sky.Reveal();
                            sky.Tick(4);
                            Check(sky.Revealed && sky.EyeOpening == 1 && sky.Presence == 1, "The darkness opens its crimson eyes when revealed");
                            sky.Dissipate();
                            for (int i = 0; i < 10; i++) sky.Tick(1);
                            Check(!sky.gameObject.activeSelf, "The darkness clears completely when the Demon King falls");
                        }
                    }
                    finally { Object.DestroyImmediate(host); }
                }
                Check(JsonUtility.ToJson(art) == before, "Building and destroying all Act 3 environments leaves shared art unchanged");
            }
            finally { Object.DestroyImmediate(material); }
        }

        private static void ValidateActThreeMigration()
        {
            string path = Path.Combine(Application.dataPath, "../Docs/Validation/ActThreeSave-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                foreach (int frontier in new[] { 1, 9, 16, 17 })
                {
                    CampaignProgress old = new CampaignProgress { version = 4, cleared = new bool[16], gold = 512 };
                    for (int i = 1; i < frontier; i++) old.CompleteStage(i);
                    old.unlockedStage = Mathf.Min(frontier, 16);
                    old.GainExperience(90);
                    File.WriteAllText(path, JsonUtility.ToJson(old));
                    CampaignSaveStore store = new CampaignSaveStore(path);
                    CampaignProgress loaded = store.Load();
                    Check(store.CanSave && loaded.IsValid() && loaded.version == CampaignProgress.Version && loaded.unlockedStage == frontier
                        && loaded.cleared.Length == 24 && loaded.cleared.Take(16).SequenceEqual(old.cleared) && !loaded.cleared.Skip(16).Any(x => x)
                        && loaded.gold == 512 && loaded.experience == old.experience && loaded.skillLevels.SequenceEqual(old.skillLevels),
                        $"Version 4 frontier {frontier} migrates to 24 stages without resetting progress");
                    Check(store.Save(loaded) && new CampaignSaveStore(path).Load().IsValid(), "Migrated Act 3 save round-trips");
                }
                CampaignProgress invalid = new CampaignProgress { version = 4, cleared = new bool[16], unlockedStage = 16 };
                invalid.cleared[15] = true;
                invalid.UpgradeSavedData();
                Check(!invalid.IsValid(), "Act 3 migration rejects a corrupt non-contiguous version 4 campaign");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
        }
    }
}

using System;
using System.IO;
using System.Linq;
using Lightbringer.Combat;
using Lightbringer.Core;
using Lightbringer.Player;
using Lightbringer.Progression;
using Lightbringer.Resources;
using Lightbringer.UI;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Lightbringer.EditorTools
{
    public static partial class GreyboxValidation
    {
        private static void ValidateActTwo()
        {
            ValidateActTwoMigration();
            ValidateActTwoScenery();
            Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            try
            {
                for (int stage = 9; stage <= 16; stage++)
                {
                    GameObject host = null;
                    try
                    {
                        CampaignSession session = StartValidationStage(stage, material, out host);
                        PrototypeBattle battle = session.Battle;
                        Check(battle.Paths.Length == CampaignStages.PathCount(stage) && battle.Paths.All(path => path.IsValid),
                            $"Act 2 stage {stage} builds valid assigned paths");
                        for (int lane = 0; lane < battle.Paths.Length; lane++)
                        {
                            Vector3[] route = CampaignStages.RoutePoints(stage, lane);
                            Vector3[] reverse = CampaignStages.EnemyRoutePoints(stage, lane);
                            Check(route.All(p => Mathf.Abs(p.x) < BattlefieldLayout.HalfWidth && p.z >= BattlefieldLayout.MinZ && p.z <= BattlefieldLayout.MaxZ)
                                && reverse.Take(reverse.Length - 1).SequenceEqual(route.Take(route.Length - 1).Reverse()),
                                $"Stage {stage} lane {lane + 1} stays in bounds and enemies retrace its corridor");
                        }
                        PreparationView brief = new PreparationView(new VisualElement());
                        // Preparation is shown after returning below; construction also validates the existing UI assets.
                        ClearFinalAssault(battle);
                        if (stage == 16) ValidateSecondWarlord(session);
                        else
                        {
                            battle.Objective.EnemyBase.TakeDamage(100000, battle.Hero);
                            Check(battle.Objective.HasWon && session.Progress.cleared[stage - 1]
                                && session.Progress.unlockedStage == stage + 1,
                                $"Stage {stage} victory unlocks the next Act 2 battle");
                            if (stage == 12) Check(session.Progress.IsLearned((int)SkillKind.Ice), "Stage 12 first clear teaches Ice");
                            if (stage == 14) Check(session.Progress.IsLearned((int)SkillKind.Flame), "Stage 14 first clear teaches Flame");
                        }
                        while (session.IsChoosing) session.ChooseGrowth(0);
                        Check(session.ReturnToPreparation(), $"Stage {stage} returns to preparation");
                        brief.Refresh(session);
                        Check(brief.Visible && brief.DestinationText == CampaignStages.Name(session.SelectedStage),
                            $"Act 2 stage {stage} briefing renders without troop reward indexing errors");
                        CampaignMapView map = new CampaignMapView(new VisualElement(), null);
                        map.Refresh(session, true);
                        Check(map.ViewedAct == (stage == 16 ? 3 : 2) && map.Select(stage) && map.Select(17) == (stage == 16),
                            $"Stage {stage} is replayable on the Act 2 map; Act 3 opens only after stage 16");
                    }
                    finally { if (host != null) Object.DestroyImmediate(host); Physics.SyncTransforms(); }
                }
            }
            finally { Object.DestroyImmediate(material); }
        }

        private static void ValidateActTwoScenery()
        {
            var art = UnityEditor.AssetDatabase.LoadAssetAtPath<Lightbringer.Visuals.ArtStyleLibrary>(ArtStyleSetup.LibraryPath);
            Check(art != null, "Act 2 environmental validation has the real art library");
            string before = JsonUtility.ToJson(art);
            float flowers = art.terrainMaterial.GetFloat("_FlowerAmount");
            Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            string[] expected = { "Roofless Pilgrim Sanctuary", "Broken Hero Monument", "Collapsed Watchtower",
                "Occupied Supply Depot", "Timber Storeyard", "Fortified Wagon Yard", "Guarded Stone Bridges",
                "Broken Siege Wall" };
            try
            {
                for (int stage = 9; stage <= 16; stage++)
                {
                    GameObject host = new GameObject("Act Two Scenery Probe");
                    try
                    {
                        PrototypeBattle battle = PrototypeBattleBuilder.Build(stage, new CampaignProgress(), EditorAssets.InputActions,
                            material, Shader.Find("Universal Render Pipeline/Unlit"), host.transform, art);
                        battle.Root.SetActive(true);
                        var scenery = battle.Root.GetComponent<Lightbringer.Visuals.ActTwoScenery>();
                        var terrain = battle.Root.GetComponentInChildren<Lightbringer.Visuals.BattlefieldEnvironment>(true);
                        Check(scenery != null && terrain != null && terrain.transform.Find(expected[stage - 9]) != null,
                            $"Atlas stage {stage} builds its defining landmark: {expected[stage - 9]}");
                        Check(scenery.DeadTrees > 30 && scenery.Stumps > 10 && scenery.Landmarks >= 2
                            && terrain.transform.Find("Scorched Earth and Abandoned Fields") != null
                            && (terrain.transform.Find("Low Cold River Mist") != null) == (stage == 15)
                            && terrain.transform.Find("Inland River Margin") == null,
                            $"Atlas stage {stage} has physical devastation and sparse woodland; only the stage 15 crossing has water and mist [trees={scenery.DeadTrees}, stumps={scenery.Stumps}, landmarks={scenery.Landmarks}]");
                        if (stage == 16)
                            Check(terrain.transform.Find("Standing Warlord Citadel Beyond the Gate") == null,
                                "Stage 16 shows no second citadel behind the enemy stronghold");
                        if (art.actTwoEnvironment != null && art.actTwoEnvironment.HasStructureMaterials && stage >= 12 && stage <= 14)
                            Check(scenery.PhotoStructures >= 2,
                                $"Atlas stage {stage} builds its depots from photographic stone, timber and bark [structures={scenery.PhotoStructures}]");
                        Check(scenery.Palette != art && scenery.Palette.terrainMaterial != art.terrainMaterial
                            && scenery.Palette.terrainMaterial.GetFloat("_FlowerAmount") == 0
                            && scenery.Palette.terrainMaterial.GetColor("_CorruptGlow") == Color.black,
                            $"Atlas stage {stage} owns its ash palette and removes flowers and neon ground cracks");
                        Check(terrain.GetComponentsInChildren<Collider>(true).Length == 1 && terrain.Terrain != null,
                            $"Atlas stage {stage} dressing preserves the single terrain collision surface");
                        if (art.actTwoEnvironment != null)
                        {
                            Check(scenery.ImportedTrees == scenery.DeadTrees && scenery.ImportedTrees > 30,
                                $"Atlas stage {stage} uses textured Dry Trees throughout its sparse woodland");
                            if (stage <= 11 || stage >= 15)
                                Check(scenery.ImportedLandmarks >= (stage == 15 ? 1 : 2),
                                    $"Atlas stage {stage} uses imported stone landmarks within reserved footprints");
                        }
                        if (stage == 15) Check(scenery.Bridges == battle.Paths.Length,
                            "Every assigned stage 15 route has a stone bridge across the shallow river");
                    }
                    finally { Object.DestroyImmediate(host); }
                }
                Check(JsonUtility.ToJson(art) == before && art.terrainMaterial.GetFloat("_FlowerAmount") == flowers,
                    "Building and destroying all Act 2 environments leaves shared Act 1 art unchanged");
            }
            finally { Object.DestroyImmediate(material); }
        }

        private static void ValidateSecondWarlord(CampaignSession session)
        {
            PrototypeBattle battle = session.Battle;
            Invoke(battle.Hero.GetComponent<FoodResource>(), "GenerateFood", 100f);
            Combatant ally = null;
            battle.Summoner.Summoned += unit => ally = unit;
            Check(battle.Summoner.TrySummon(), "An army can be present for the second Warlord arrival");
            ally.Configure(Faction.Allied, ally.MaximumHealth);
            ally.transform.position = new Vector3(0, .85f, BattlefieldLayout.EnemyBaseZ - 4);
            Physics.SyncTransforms();
            battle.Objective.EnemyBase.TakeDamage(100000, battle.Hero);
            battle.Encounter.Tick(.01f);
            Check(battle.Encounter.Phase == BossEncounterPhase.Rupture, "Second arrival opens with the rupture shockwave");
            battle.Encounter.Tick(BossEncounter.WaveSeconds);
            battle.Encounter.Tick(BossEncounter.ArrivalSeconds);
            Check(!ally.IsAlive && battle.Encounter.FallenAllies == 1 && battle.Encounter.IsRematch
                && battle.Encounter.Phase == BossEncounterPhase.Dialogue
                && battle.Encounter.CurrentLines.SequenceEqual(BossEncounter.RematchLines)
                && !battle.Encounter.CurrentLines.Any(said => said.Contains("용사")),
                "Second arrival wipes the army; the Warlord stays unnamed in Act 2");
            FinishBossDialogue(battle);
            while (session.IsChoosing) session.ChooseGrowth(0);
            BossCombat combat = battle.Waves.Boss.GetComponent<BossCombat>();
            battle.Hero.Invulnerable = true;
            battle.Hero.transform.position = battle.Waves.Boss.transform.position + Vector3.back * 10;
            Physics.SyncTransforms();
            combat.Tick(3);
            Check(combat.CurrentAttack == BossAttack.Crescent && combat.IsAttacking, "Rematch opens with a telegraphed crescent");
            combat.Tick(combat.Windup + .01f);
            combat.Tick(.66f);
            Check(combat.CurrentAttack == BossAttack.Charge && combat.IsAttacking && combat.Windup >= 1.25f,
                "Rematch links crescent into charge with a full dodge warning");
            battle.Waves.Boss.TakeDamage(100000, battle.Hero);
            Check(!battle.Objective.HasEnded && !session.Progress.cleared[15], "Second defeat waits for retreat before rewards");
            Check(battle.Encounter.CurrentLines.SequenceEqual(BossEncounter.RematchRetreatLines)
                && battle.Encounter.CurrentPictures.Contains(StoryCatalog.SwordCloseUp) && battle.Encounter.EscapeLine == 4,
                "Second retreat shows the sword her teacher carried before the Warlord leaves without an answer");
            for (int i = 0; i < BossEncounter.RematchRetreatLines.Length; i++) { battle.Encounter.Tick(.8f); battle.Encounter.AdvanceDialogue(); }
            battle.Encounter.Tick(BossEncounter.RetreatFadeSeconds);
            Check(battle.Objective.HasWon && !session.Progress.IsComplete && session.Progress.unlockedStage == 17
                && !session.Progress.IsLearned((int)SkillKind.Meteor) && session.LastResult.Contains("2막 완료"),
                "Second retreat completes Act 2 and opens Act 3; Meteor waits for stage 17");
        }

        private static void ValidateActTwoMigration()
        {
            string path = Path.Combine(Application.dataPath, "../Docs/Validation/ActTwoSave-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                foreach (int frontier in new[] { 1, 4, 8, 9 })
                {
                    CampaignProgress old = new CampaignProgress { version = 3, cleared = new bool[8], gold = 731 };
                    for (int i = 1; i < frontier; i++) old.CompleteStage(i);
                    old.unlockedStage = Mathf.Min(frontier, 8);
                    old.unitRanks[0] = 1;
                    old.GainExperience(150);
                    File.WriteAllText(path, JsonUtility.ToJson(old));
                    CampaignSaveStore store = new CampaignSaveStore(path);
                    CampaignProgress loaded = store.Load();
                    Check(store.CanSave && loaded.IsValid() && loaded.unlockedStage == frontier
                        && loaded.cleared.Length == CampaignProgress.StageCount && loaded.cleared.Take(8).SequenceEqual(old.cleared)
                        && loaded.gold == 731 && loaded.unitRanks[0] == 1 && loaded.experience == old.experience
                        && loaded.choices.SequenceEqual(old.choices) && loaded.skillLevels.SequenceEqual(old.skillLevels),
                        $"Version 3 frontier {frontier} migrates through the real save loader without resetting progress");
                    Check(store.Save(loaded) && new CampaignSaveStore(path).Load().IsValid(), "Migrated Act 2 save round-trips");
                }
                CampaignProgress invalid = new CampaignProgress { version = 3, cleared = new bool[8], unlockedStage = 8 };
                invalid.cleared[7] = true;
                invalid.UpgradeSavedData();
                Check(!invalid.IsValid(), "Act 2 migration rejects a corrupt non-contiguous old campaign");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
        }
    }
}

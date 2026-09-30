using System.Linq;
using Lightbringer.Combat;
using Lightbringer.Core;
using Lightbringer.Progression;
using Lightbringer.Resources;
using Lightbringer.Units;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lightbringer.EditorTools
{
    public static partial class GreyboxValidation
    {
        private static void ValidateCampaignIntegration()
        {
            Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            GameObject host = new GameObject("Campaign validation host");
            CampaignSession session = host.AddComponent<CampaignSession>();
            CampaignProgress profile = new CampaignProgress();
            session.InitializeForValidation(profile);
            session.Configure(AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions"),
                material, Shader.Find("Universal Render Pipeline/Unlit"));
            try
            {
                Check(!session.SelectStage(2) && session.StartBattle() && !session.StartBattle(),
                    "Campaign starts an unlocked stage once and rejects locked or duplicate starts");
                PrototypeBattle battle = session.Battle;
                Invoke(battle.Objective, "OnEnable");
                Check(battle.Paths.Length == 1 && battle.Summoner.UnlockedUnitCount == 1,
                    "First campaign stage begins with one Path and only the first troop");
                Check(!session.TryEquip(0, 1), "Equipment cannot change while a stage is active");
                Physics.SyncTransforms();
                battle.Waves.Tick(0.1f);
                Combatant[] enemies = battle.Root.GetComponentsInChildren<Combatant>().Where(x => x.Faction == Faction.Enemy
                    && x != battle.Objective.EnemyBase).ToArray();
                Check(enemies.Length >= 2 && battle.Waves.WavesSpawned == 1,
                    "Campaign spawns a wave of enemy units assigned to advancing routes");
                int expBefore = profile.experience;
                enemies[0].TakeDamage(1000, battle.Hero);
                Check(profile.experience == expBefore + 12, "An allied kill awards stage EXP exactly once");
                enemies[0].TakeDamage(1000, battle.Hero);
                Check(profile.experience == expBefore + 12, "A dead enemy cannot award duplicate EXP");
                profile.GainExperience(60);
                session.UpdatePause();
                Check(session.IsBattlePaused && !battle.Summoner.enabled && !battle.Waves.enabled,
                    "Pending permanent growth pauses the local battle and its spawning");
                int chosen = profile.choices[0];
                Check(session.ChooseGrowth(0) && profile.ranks[chosen] == 1 && !session.IsBattlePaused && battle.Summoner.enabled,
                    "Picking growth applies a permanent rank and resumes the same battle");
                battle.Objective.EnemyBase.TakeDamage(10000, battle.Hero);
                Check(battle.Objective.HasWon && profile.unlockedStage == 2 && profile.gold == 100,
                    "Real objective death drives campaign completion and first-clear rewards");
                while (session.IsChoosing) session.ChooseGrowth(0);
                Check(session.ReturnToPreparation() && session.Battle == null && profile.ranks[chosen] >= 1,
                    "Victory returns to preparation with permanent progress retained");
                Check(session.SelectStage(2) && session.StartBattle(), "Newly unlocked stage can be started immediately");
                battle = session.Battle;
                Invoke(battle.Objective, "OnEnable");
                battle.Hero.TakeDamage(10000);
                Check(battle.Objective.HasLost && profile.unlockedStage == 2 && profile.gold == 100,
                    "Defeat does not grant clear rewards or reset permanent progress");
                Check(session.ReturnToPreparation(), "Defeat returns to preparation for retry");

                for (int stage = 2; stage < CampaignProgress.StageCount; stage++) profile.CompleteStage(stage);
                session.SelectStage(8);
                Check(session.StartBattle() && session.Battle.Paths.Length == 3 && session.Battle.Summoner.UnlockedUnitCount == 8,
                    "Late campaign uses three Paths with every unlocked troop available");
                ValidateTroopRoles(session.Battle);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                UnityEngine.Object.DestroyImmediate(material);
                Physics.SyncTransforms();
            }
        }

        private static void ValidateTroopRoles(PrototypeBattle battle)
        {
            FoodResource food = battle.Hero.GetComponent<FoodResource>();
            Combatant spawned = null;
            battle.Summoner.Summoned += unit => spawned = unit;
            Combatant[] troops = new Combatant[UnitCatalog.Count];
            for (int i = 0; i < UnitCatalog.Count; i++)
            {
                Invoke(food, "GenerateFood", 100f);
                Check(battle.Summoner.TrySelectUnit(i) && battle.Summoner.TrySummon(), "Unlocked " + UnitCatalog.Names[i] + " can be deployed");
                troops[i] = spawned;
                Invoke(spawned, "Awake");
                spawned.transform.position = new Vector3(-35 + i * 3, 0.85f, -25);
                Physics.SyncTransforms();
            }
            troops[2].TakeDamage(20);
            Check(troops[2].CurrentHealth == 87, "Shieldbearer reduces incoming damage by its prototype armour value");
            troops[0].transform.position = troops[4].transform.position + Vector3.right;
            troops[0].TakeDamage(15);
            Physics.SyncTransforms();
            troops[4].GetComponent<UnitSupport>().Tick(0.1f);
            Check(troops[0].CurrentHealth == 25, "Priest heals a nearby allied soldier");
            Vector3 origin = troops[5].transform.position;
            Combatant target = CreateCombatUnit(origin + Vector3.forward * 5, Faction.Enemy);
            Combatant nearby = CreateCombatUnit(origin + new Vector3(1, 0, 5), Faction.Enemy);
            target.Configure(Faction.Enemy, 100); nearby.Configure(Faction.Enemy, 100);
            Physics.SyncTransforms();
            Invoke(troops[5].GetComponent<UnitCombat>(), "Tick", 0.1f);
            Check(target.CurrentHealth == 82 && nearby.CurrentHealth == 82, "Mage splash damages multiple opponents once each");
            float height = troops[7].transform.position.y;
            Invoke(troops[7].GetComponent<UnitPathFollower>(), "Tick", 0.5f);
            Check(troops[7].transform.position.y > height + 1f && troops[7].MaximumHealth == 300,
                "Final Dragon troop takes flight and has distinct large-unit stats");
        }
    }
}

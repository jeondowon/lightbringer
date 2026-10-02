using System.Linq;
using Lightbringer.Combat;
using Lightbringer.Core;
using Lightbringer.Player;
using Lightbringer.Progression;
using Lightbringer.Resources;
using Lightbringer.UI;
using Lightbringer.Units;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Lightbringer.EditorTools
{
    public static partial class GreyboxValidation
    {
        // The HUD views are built on a detached root: data binding is checked without a runtime panel.
        private static void ValidateHud()
        {
            Check(AssetDatabase.LoadAssetAtPath<StyleSheet>(CampaignHUD.StyleSheetPath) != null
                && AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(HudSetup.ThemePath) != null
                && HudSetup.EnsureAssets() != null && HudSetup.EnsureAssets().themeStyleSheet != null,
                "HUD style sheet, theme and PanelSettings assets exist");
            Check(BattleHudView.Evaluate(1, 5) == FrontStatus.Danger && BattleHudView.Evaluate(4, 3) == FrontStatus.Contested
                && BattleHudView.Evaluate(2, 0) == FrontStatus.Clear && BattleHudView.Evaluate(0, 2) == FrontStatus.Contested,
                "A front is flagged in danger only when enemies clearly outnumber our troops");
            ValidatePreparationView();

            Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            GameObject host = null;
            try
            {
                CampaignSession session = StartValidationStage(EnemyCatalog.BossStage, material, out host);
                PrototypeBattle battle = session.Battle;
                VisualElement root = new VisualElement();
                BattleHudView view = new BattleHudView(root);
                LevelUpView levelUp = new LevelUpView(root);
                ResultView result = new ResultView(root);
                view.Bind(battle);
                Check(view.FrontChipCount == 3 && view.TroopCardCount == 8, "Battle HUD shows one chip per front and one card per unlocked troop");

                FoodResource food = battle.Hero.GetComponent<FoodResource>();
                Invoke(food, "GenerateFood", 4f);
                view.Refresh(session);
                Check(view.FoodText.StartsWith(food.CurrentFood.ToString("0") + " /") && view.IsCardReady(0) && !view.IsCardReady(7)
                    && view.IsCardSelected((int)battle.Summoner.SelectedUnit),
                    "Troop cards light up exactly when Food covers their cost");
                Check(view.EnemyBaseShielded && !view.BossVisible, "Boss stage shows the shielded enemy stronghold before the Warlord arrives");
                Check(battle.Summoner.TryCyclePath() && RefreshAndRead(view, session).Contains("PATH 2"),
                    "The command bar names the Path that new troops will take");

                Physics.SyncTransforms();
                battle.Waves.Tick(0.1f);
                battle.Root.GetComponent<BattlefieldReadability>().Scan();
                view.Refresh(session);
                Check(view.FrontStatusOf(0) == FrontStatus.Danger, "An undefended front facing a full squad is flagged as in danger");

                ManaResource mana = battle.Hero.GetComponent<ManaResource>();
                mana.Configure(100f, 2f, true);
                Combatant enemy = battle.Root.GetComponentsInChildren<Combatant>()
                    .First(x => x.Faction == Faction.Enemy && x != battle.Objective.EnemyBase);
                enemy.transform.position = battle.Hero.transform.position + Vector3.forward * 4f;
                Physics.SyncTransforms();
                view.Refresh(session);
                Check(view.SlotName(0) == EquipmentCatalog.Names[battle.Abilities.Equipped(0)] && !view.IsSlotCooling(0),
                    "Ability slots show the equipped item and are ready before casting");
                Check(battle.Abilities.TryCast(0, enemy) && RefreshAndCooling(view, session, 0), "A cast ability shows its cooldown");

                session.Progress.GainExperience(1000);
                levelUp.Refresh(session);
                int pending = session.Progress.pendingLevels;
                Check(levelUp.Visible && levelUp.ChoiceText(0).Contains(GrowthCatalog.Titles[session.Progress.choices[0]]),
                    "Level-up cards appear with the offered permanent upgrades");
                Check(!levelUp.Confirm() && session.Progress.pendingLevels == pending, "CONFIRM does nothing until a card is selected");
                levelUp.Select(1);
                Check(levelUp.Selected == 1 && session.Progress.pendingLevels == pending, "Selecting a card does not apply it yet");
                Check(levelUp.Confirm() && session.Progress.pendingLevels == pending - 1, "Confirming the selected card applies one permanent upgrade");
                while (session.IsChoosing) levelUp.Choose(0);
                levelUp.Refresh(session);
                result.Refresh(session);
                Check(!levelUp.Visible && !result.Visible, "Overlays close once choices are done and the battle continues");

                battle.Objective.EnemyBase.Invulnerable = false;
                battle.Objective.EnemyBase.TakeDamage(1000000f, battle.Hero);
                while (session.IsChoosing) levelUp.Choose(0);
                result.Refresh(session);
                Check(result.Visible && result.Title == "VICTORY", "Victory screen appears when the stage is won");
                result.SetRating(4);
                Check(result.Rating == 4 && result.Return() && session.Battle == null, "The result screen records the rating and returns to preparation");
                view.Bind(session.Battle);
                Check(view.Root.ClassListContains("lb-hidden"), "Battle HUD hides in preparation");
            }
            finally
            {
                if (host != null) Object.DestroyImmediate(host);
                Object.DestroyImmediate(material);
                Physics.SyncTransforms();
            }
        }

        private static void ValidatePreparationView()
        {
            Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            GameObject host = new GameObject("Preparation view validation host");
            try
            {
                CampaignSession session = host.AddComponent<CampaignSession>();
                CampaignProgress profile = new CampaignProgress();
                profile.CompleteStage(1);
                profile.CompleteStage(2);
                session.InitializeForValidation(profile);
                EditorAssets.ConfigureSession(session, material);
                PreparationView view = new PreparationView(new VisualElement());
                view.Refresh(session);
                Check(view.Visible && view.StartText == "START STAGE 3" && view.IsStageSelected(3)
                    && view.IsStageEnabled(3) && !view.IsStageEnabled(4) && view.TroopCount == 3,
                    "Preparation opens on the newest stage, locks later stages and lists unlocked troops");
                Check(session.SelectStage(2) && RefreshAndSelected(view, session, 2), "Selecting a stage highlights it and retargets START");

                Check(view.ActiveSlot == 0 && !view.IsItemEnabled(1) && view.ItemNote(1).Contains("in Q")
                    && !view.IsItemEnabled(4) && view.ItemNote(4) == "LOCKED" && view.IsItemEnabled(2),
                    "Items worn in another slot or not yet owned cannot be equipped");
                Check(view.Equip(2) && profile.loadout[0] == 2 && view.SlotText(0) == EquipmentCatalog.Names[2],
                    "Clicking an item equips it into the active slot");
                view.SelectSlot(2);
                Check(view.ActiveSlot == 2 && view.Equip(0) && profile.loadout[2] == 0, "Choosing another slot redirects the next equip");

                Check(view.Start() && session.Battle != null && session.SelectedStage == 2, "START launches the selected stage");
                view.Refresh(session);
                Check(!view.Visible, "Preparation hides once the battle begins");
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(material);
                Physics.SyncTransforms();
            }
        }

        private static bool RefreshAndSelected(PreparationView view, CampaignSession session, int stage)
        {
            view.Refresh(session);
            return view.IsStageSelected(stage) && view.StartText == "START STAGE " + stage;
        }

        private static string RefreshAndRead(BattleHudView view, CampaignSession session)
        {
            view.Refresh(session);
            return view.PathHintText;
        }

        private static bool RefreshAndCooling(BattleHudView view, CampaignSession session, int slot)
        {
            view.Refresh(session);
            return view.IsSlotCooling(slot);
        }
    }
}

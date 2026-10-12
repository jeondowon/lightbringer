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
                view.Refresh(session);
                Check(view.MinimapSelectedPath == 0, "The battlefield map highlights the first Path at the start");
                Check(battle.Summoner.TryCyclePath() && RefreshAndRead(view, session).Contains("진로 2") && view.MinimapSelectedPath == 1,
                    "The battlefield map names and highlights the Path that new troops will take");

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

                ClearFinalAssault(battle);
                battle.Objective.EnemyBase.TakeDamage(1000000f, battle.Hero);
                BossDialogueView dialogue = new BossDialogueView(root);
                dialogue.Refresh(session);
                Check(dialogue.Visible && !session.IsChoosing, "Boss cinematic takes priority over pending growth UI");
                battle.Encounter.Tick(BossEncounter.WaveSeconds);
                battle.Encounter.Tick(BossEncounter.ArrivalSeconds);
                dialogue.Refresh(session);
                Check(dialogue.LineText == BossEncounter.Lines[0], "Boss dialogue displays the Korean encounter script");
                Check(dialogue.StoryVisible && dialogue.StoryImage != null,
                    "The encounter dialogue uses a bundled full-screen story illustration");
                Texture arrivalArt = dialogue.StoryImage;
                Vector3 dialogueCamera = battle.Camera.transform.position;
                battle.Encounter.Tick(1);
                Check(battle.Camera.transform.position == dialogueCamera, "Dialogue never moves the battlefield camera");
                var arrivalShots = new System.Collections.Generic.HashSet<Texture>();
                for (int i = 0; i < BossEncounter.Lines.Length; i++)
                {
                    dialogue.Refresh(session);
                    Check(dialogue.StoryImage != null && dialogue.LineText == BossEncounter.Lines[i],
                        "Every opening dialogue line has an imported illustration and matching caption");
                    arrivalShots.Add(dialogue.StoryImage);
                    battle.Encounter.Tick(.3f); battle.Encounter.AdvanceDialogue();
                }
                Check(arrivalShots.Count == 3, "The opening dialogue presents three distinct story shots");
                while (session.IsChoosing) levelUp.Choose(0);
                battle.Waves.Boss.TakeDamage(1000000f, battle.Hero);
                dialogue.Refresh(session);
                result.Refresh(session);
                levelUp.Refresh(session);
                Check(dialogue.StoryVisible && dialogue.StoryImage != null && dialogue.StoryImage != arrivalArt
                    && dialogue.LineText == BossEncounter.RetreatLines[0] && !result.Visible && !levelUp.Visible,
                    "The retreat illustration replaces the battle before results or growth can cover it");
                var retreatShots = new System.Collections.Generic.HashSet<Texture>();
                for (int i = 0; i < BossEncounter.RetreatLines.Length; i++)
                {
                    dialogue.Refresh(session);
                    Check(dialogue.StoryImage != null && dialogue.LineText == BossEncounter.RetreatLines[i]
                        && !battle.Objective.HasEnded,
                        "Every retreat shot remains visible with its caption before victory");
                    retreatShots.Add(dialogue.StoryImage);
                    battle.Encounter.Tick(.8f); battle.Encounter.AdvanceDialogue();
                }
                Check(retreatShots.Count == 3 && dialogue.StoryImage.name == "Aftermath_v1",
                    "The retreat shows three distinct shots and ends on the abandoned battlefield");
                battle.Encounter.Tick(BossEncounter.RetreatFadeSeconds);
                while (session.IsChoosing) levelUp.Choose(0);
                result.Refresh(session);
                Check(result.Visible && result.Title == "승리", "Victory screen appears when the stage is won");
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
                CampaignMapView map = new CampaignMapView(new VisualElement(), null);
                map.Refresh(session, true);
                Check(!map.Select(4) && session.SelectedStage == 3,
                    "Campaign map rejects locked beacons without changing the destination");
                Check(map.Select(1) && session.SelectedStage == 1 && map.Select(3),
                    "Campaign map allows replaying a lit beacon and selecting the current frontier");
                PreparationView view = new PreparationView(new VisualElement());
                view.Refresh(session);
                Check(view.Visible && view.StartText.Contains("봉화 03") && view.DestinationText == "봉화 03" && view.TroopCount == 3,
                    "Briefing shows the selected beacon and all unlocked troops");
                Check(session.SelectStage(2) && RefreshAndSelected(view, session, 2), "Selecting a stage highlights it and retargets START");
                view.Refresh(session, false);
                map.Refresh(session, true);
                Check(!view.Visible && session.SelectedStage == 2,
                    "Returning to the map hides preparation and preserves the selected destination");
                view.Refresh(session);

                Check(view.ActiveSlot == 0 && !view.IsItemEnabled(1) && view.ItemNote(1).Contains("Q 장착 중")
                    && !view.IsItemEnabled(4) && view.ItemNote(4) == "미해금" && view.IsItemEnabled(2),
                    "Items worn in another slot or not yet owned cannot be equipped");
                Check(view.SelectItem(2) && view.SelectedItem == 2 && profile.loadout[0] == 0,
                    "Inspecting an equipment candidate does not change the saved loadout");
                Check(!view.SelectItem(4) && !view.SelectItem(1) && view.SelectedItem == 2,
                    "Locked and already-used equipment cannot replace the inspected candidate");
                Check(view.EquipSelected() && profile.loadout[0] == 2 && view.SlotText(0) == EquipmentCatalog.Names[2],
                    "Confirming inspected equipment changes only the selected slot");
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
            return view.DestinationText == $"봉화 {stage:00}" && view.StartText.Contains($"봉화 {stage:00}");
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

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
            ValidateBattleExit();

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
                Check(view.SlotName(0) == SkillCatalog.Names[battle.Abilities.Equipped(0)] && !view.IsSlotCooling(0),
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

        private static void ValidateBattleExit()
        {
            float originalTimeScale = Time.timeScale;
            Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            GameObject host = null;
            try
            {
                CampaignSession session = StartValidationStage(1, material, out host);
                PrototypeBattle battle = session.Battle;
                PauseView pause = new PauseView(new VisualElement());
                pause.Refresh(session);
                Check(!session.EndBattle() && !pause.RequestEnd() && !pause.ConfirmEnd(),
                    "Battle exit requires a paused battle and an explicit confirmation");
                battle.Waves.Tick(0.1f);
                Combatant enemy = battle.Root.GetComponentsInChildren<Combatant>()
                    .First(x => x.Faction == Faction.Enemy && x != battle.Objective.EnemyBase);
                enemy.TakeDamage(100000f, battle.Hero);
                while (session.IsChoosing) session.ChooseGrowth(0);
                string progress = JsonUtility.ToJson(session.Progress);
                PlaytestRecord record = session.PlaytestRecord;
                Time.timeScale = 0.75f;
                Check(session.SetPaused(true), "Battle exit menu opens from a running battle");
                pause.Refresh(session);
                Check(pause.Visible && pause.RequestEnd() && pause.IsConfirmingEnd && Time.timeScale == 0f,
                    "Exit confirmation keeps the battlefield frozen");
                pause.CancelEnd();
                Check(!pause.IsConfirmingEnd && !pause.ConfirmEnd() && session.Battle == battle && session.IsUserPaused,
                    "Cancelling exit keeps the current battle and returns to the pause menu");
                pause.RequestEnd();
                Check(pause.Resume(), "Exit confirmation can resume without ending the battle");
                pause.Refresh(session);
                session.SetPaused(true);
                pause.Refresh(session);
                Check(!pause.IsConfirmingEnd, "Reopening pause clears the previous exit confirmation");
                Check(pause.RequestEnd() && pause.ConfirmEnd() && session.Battle == null && !pause.Visible,
                    "Confirmed exit removes the battle and closes the pause overlay");
                Check(!session.IsUserPaused && !session.IsBattlePaused && Time.timeScale == 0.75f
                    && UnityEngine.Cursor.lockState == CursorLockMode.None && UnityEngine.Cursor.visible,
                    "Leaving a paused battle restores time and the menu cursor");
                Check(JsonUtility.ToJson(session.Progress) == progress && record.result == "abandoned",
                    "Exit retains earned progress, grants no clear rewards and records an abandoned battle");
                Check(!session.EndBattle() && session.StartBattle() && !session.IsBattlePaused,
                    "Exit cannot repeat and the player can immediately start another battle");
            }
            finally
            {
                if (host != null) Object.DestroyImmediate(host);
                Object.DestroyImmediate(material);
                Time.timeScale = originalTimeScale;
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
                string beforeBrowsing = JsonUtility.ToJson(profile);
                Check(map.ShowAct(2) && map.ViewedAct == 2 && map.ShowAct(3) && map.ViewedAct == 3
                    && session.SelectedStage == 3 && JsonUtility.ToJson(profile) == beforeBrowsing,
                    "Browsing future act maps preserves the selected stage and all saved progress");
                for (int act = 2; act <= 3; act++)
                {
                    map.ShowAct(act);
                    var planned = map.Root.Query<Button>(className: "lb-beacon").ToList();
                    var labels = map.Root.Query<Label>(className: "lb-beacon-name").ToList();
                    Check(planned.Count == 8 && planned.TrueForAll(button => !button.ClassListContains("lb-hidden")
                        && button.ClassListContains("lb-beacon--planned") && !button.enabledSelf)
                        && labels[0].text == $"봉화 {(act - 1) * 8 + 1:00}" && labels[7].text == $"봉화 {act * 8:00}",
                        $"Act {act} shows eight numbered future beacons without allowing battle entry");
                }
                Check(!map.Select(9) && !map.ShowAct(0) && !map.ShowAct(4) && map.ViewedAct == 3,
                    "Atlas rejects unimplemented stages and invalid acts");
                map.Refresh(session, false);
                map.Refresh(session, true);
                Check(map.ViewedAct == 1 && session.SelectedStage == 3
                    && map.Root.Query<Button>(className: "lb-beacon").ToList().TrueForAll(button => !button.ClassListContains("lb-hidden")),
                    "Reopening the atlas restores the current act and its eight beacons");
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

                Check(view.ActiveSlot == 0 && !view.IsItemEnabled(1) && view.ItemNote(1).Contains("RMB 장착 중")
                    && !view.IsItemEnabled(4) && view.ItemNote(4) == "미습득" && view.IsItemEnabled(0),
                    "Skills worn in another slot or not yet learned cannot be equipped");
                Check(view.SelectItem(0) && view.SelectedItem == 0 && profile.loadout[0] == 0,
                    "Inspecting a skill does not change the saved loadout");
                Check(!view.SelectItem(4) && !view.SelectItem(1) && view.SelectedItem == 0,
                    "Unlearned and already-used skills cannot replace the inspected skill");
                view.SelectSlot(1);
                Check(view.ActiveSlot == 1 && view.EquipSelected() && profile.loadout[1] == SkillCatalog.Empty && view.SlotText(1) == "비어 있음",
                    "The equip button empties a slot that already holds the inspected skill");
                view.SelectSlot(2);
                Check(view.ActiveSlot == 2 && view.Equip(1) && profile.loadout[2] == 1 && view.SlotText(2) == SkillCatalog.Names[1],
                    "Choosing another slot redirects the next equip");

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

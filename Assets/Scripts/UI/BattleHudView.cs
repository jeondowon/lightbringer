using System.Collections.Generic;
using Lightbringer.Combat;
using Lightbringer.Core;
using Lightbringer.Player;
using Lightbringer.Resources;
using Lightbringer.Units;
using UnityEngine;
using UnityEngine.UIElements;

namespace Lightbringer.UI
{
    public enum FrontStatus { Clear, Contested, Danger }

    // In-battle HUD: fronts and bases on top, hero vitals bottom-left, abilities and troop cards bottom-centre.
    // Pure view: it reads the battle every frame and never changes gameplay state.
    public sealed class BattleHudView
    {
        private const float ToastSeconds = 2.5f;

        public readonly VisualElement Root;
        private readonly Label stage, waves, pathHint, toast;
        private readonly VisualElement fronts, boss, abilities, troops;
        private readonly HudBar alliedBase, enemyBase, bossBar, health, mana, food;
        private readonly List<FrontChip> frontChips = new List<FrontChip>();
        private readonly List<Slot> slots = new List<Slot>();
        private readonly List<Card> cards = new List<Card>();
        private readonly HealthBarOverlay healthBars;
        private PrototypeBattle battle;
        private BattlefieldReadability readability;
        private string lastSummonFeedback = "", lastAbilityFeedback = "";
        private float toastUntil;

        private struct FrontChip { public VisualElement Root; public Label Allies, Enemies; }
        private struct Slot { public VisualElement Root, Fill; public Label Name, Cost; }
        private struct Card { public VisualElement Root, Fill; public Label Cost; }

        public BattleHudView(VisualElement parent)
        {
            Root = Hud.Box(parent, "lb-hud");
            healthBars = new HealthBarOverlay(Root);

            VisualElement top = Hud.Box(Root, "lb-top");
            VisualElement row = Hud.Box(top, "lb-top-row", "lb-panel");
            stage = Hud.Text(row, "", "lb-stage");
            fronts = Hud.Box(row, "lb-top-row");
            waves = Hud.Text(row, "", "lb-waves");
            VisualElement bases = Hud.Box(top, "lb-bases");
            VisualElement ours = Hud.Box(bases, "lb-base", "lb-panel");
            Hud.Text(ours, "OUR STRONGHOLD", "lb-caption");
            alliedBase = new HudBar(ours, "lb-bar--ally");
            VisualElement theirs = Hud.Box(bases, "lb-base", "lb-panel");
            Hud.Text(theirs, "ENEMY STRONGHOLD", "lb-caption");
            enemyBase = new HudBar(theirs, "lb-bar--enemy");

            boss = Hud.Box(Root, "lb-boss", "lb-panel");
            Hud.Text(boss, "THE WARLORD", "lb-caption");
            bossBar = new HudBar(boss, "lb-bar--boss");

            Hud.Box(Root, "lb-crosshair");
            toast = Hud.Text(Root, "", "lb-toast");

            VisualElement vitals = Hud.Box(Root, "lb-vitals", "lb-panel");
            health = Vital(vitals, "HP", "lb-bar--hp");
            mana = Vital(vitals, "MANA", "lb-bar--mana");
            food = Vital(vitals, "FOOD", "lb-bar--food");

            VisualElement command = Hud.Box(Root, "lb-command");
            pathHint = Hud.Text(command, "", "lb-path-hint", "lb-panel");
            abilities = Hud.Box(command, "lb-abilities");
            troops = Hud.Box(command, "lb-troops");

            Hud.Text(Root, "WASD move   Mouse look   Wheel zoom\n1-8 summon   F repeat   Tab Path   Esc cursor", "lb-hint");
            Hud.IgnorePicking(Root);
        }

        private static HudBar Vital(VisualElement parent, string name, string modifier)
        {
            VisualElement row = Hud.Box(parent, "lb-vital-row");
            Hud.Text(row, name, "lb-vital-label");
            return new HudBar(row, modifier);
        }

        public int FrontChipCount => frontChips.Count;
        public int TroopCardCount => cards.Count;
        public string FoodText => food.Label;
        public string PathHintText => pathHint.text;
        public bool BossVisible => !boss.ClassListContains("lb-hidden");
        public bool EnemyBaseShielded => enemyBase.Root.ClassListContains("lb-bar--shielded");
        public bool IsCardReady(int index) => cards[index].Root.ClassListContains("lb-card--ready");
        public bool IsCardSelected(int index) => cards[index].Root.ClassListContains("lb-card--selected");
        public string SlotName(int index) => slots[index].Name.text;
        public bool IsSlotCooling(int index) => slots[index].Root.ClassListContains("lb-slot--cooling");
        public FrontStatus FrontStatusOf(int index) =>
            frontChips[index].Root.ClassListContains("lb-front--danger") ? FrontStatus.Danger
            : frontChips[index].Root.ClassListContains("lb-front--contested") ? FrontStatus.Contested : FrontStatus.Clear;

        // A front is in danger when the enemy clearly outnumbers our troops there.
        public static FrontStatus Evaluate(int allies, int enemies) =>
            enemies >= 3 && enemies > allies * 1.5f ? FrontStatus.Danger : enemies > 0 ? FrontStatus.Contested : FrontStatus.Clear;

        public void Bind(PrototypeBattle value)
        {
            battle = value;
            readability = battle != null ? battle.Root.GetComponent<BattlefieldReadability>() : null;
            lastSummonFeedback = lastAbilityFeedback = "";
            toastUntil = 0f;
            fronts.Clear(); frontChips.Clear();
            abilities.Clear(); slots.Clear();
            troops.Clear(); cards.Clear();
            healthBars.Clear();
            Hud.Show(Root, battle != null);
            if (battle == null) return;
            for (int i = 0; i < battle.Paths.Length; i++)
            {
                VisualElement chip = Hud.Box(fronts, "lb-front");
                Hud.Text(chip, "PATH " + (i + 1), "lb-front-name");
                frontChips.Add(new FrontChip { Root = chip, Allies = Hud.Text(chip, "", "lb-front-allies"), Enemies = Hud.Text(chip, "", "lb-front-enemies") });
            }
            string[] keys = { "LMB", "Q", "E" };
            for (int i = 0; i < EquipmentCatalog.Slots; i++)
            {
                VisualElement slot = Hud.Box(abilities, "lb-slot");
                VisualElement fill = Hud.Box(slot, "lb-fill");
                VisualElement head = Hud.Box(slot, "lb-head");
                Hud.Text(head, keys[i], "lb-key");
                slots.Add(new Slot { Root = slot, Fill = fill, Name = Hud.Text(head, "", "lb-name"), Cost = Hud.Text(slot, "", "lb-cost") });
            }
            for (int i = 0; i < battle.Summoner.UnlockedUnitCount; i++)
            {
                VisualElement card = Hud.Box(troops, "lb-card");
                VisualElement fill = Hud.Box(card, "lb-fill");
                VisualElement head = Hud.Box(card, "lb-head");
                Hud.Text(head, (i + 1).ToString(), "lb-key");
                Hud.Text(head, UnitCatalog.Names[i], "lb-name");
                cards.Add(new Card { Root = card, Fill = fill, Cost = Hud.Text(card, "", "lb-cost") });
            }
            Hud.IgnorePicking(Root);
        }

        public void Refresh(CampaignSession session)
        {
            if (battle == null || session == null) return;
            Hud.Set(stage, "STAGE " + session.SelectedStage);
            Hud.Set(waves, $"WAVE {battle.Waves.WavesSpawned}/{battle.Waves.TotalWaves}");
            RefreshFronts();
            RefreshBases();
            if (battle.Hero == null) return;
            FoodResource foodStore = battle.Hero.GetComponent<FoodResource>();
            ManaResource manaStore = battle.Hero.GetComponent<ManaResource>();
            health.Set(battle.Hero.CurrentHealth, battle.Hero.MaximumHealth, $"{battle.Hero.CurrentHealth:0} / {battle.Hero.MaximumHealth:0}");
            mana.Set(manaStore.Current, manaStore.Maximum, $"{manaStore.Current:0} / {manaStore.Maximum:0}");
            food.Set(foodStore.CurrentFood, foodStore.MaximumFood, $"{foodStore.CurrentFood:0} / {foodStore.MaximumFood:0}");
            RefreshAbilities(manaStore);
            RefreshTroops(foodStore);
            int selected = System.Array.IndexOf(battle.Paths, battle.Summoner.SelectedPath);
            Hud.Set(pathHint, battle.Paths.Length > 1 ? $"New troops  >  PATH {selected + 1}   [Tab]" : "New troops  >  PATH 1");
            RefreshToast();
        }

        public void RefreshHealthBars() => healthBars.Refresh(battle, readability);

        private void RefreshFronts()
        {
            int selected = System.Array.IndexOf(battle.Paths, battle.Summoner.SelectedPath);
            for (int i = 0; i < frontChips.Count; i++)
            {
                int allies = readability != null && i < readability.FrontCount ? readability.AlliesOn(i) : 0;
                int enemies = readability != null && i < readability.FrontCount ? readability.EnemiesOn(i) : 0;
                FrontStatus status = Evaluate(allies, enemies);
                FrontChip chip = frontChips[i];
                Hud.Set(chip.Allies, allies.ToString());
                Hud.Set(chip.Enemies, enemies.ToString());
                chip.Root.EnableInClassList("lb-front--selected", i == selected);
                chip.Root.EnableInClassList("lb-front--contested", status == FrontStatus.Contested);
                chip.Root.EnableInClassList("lb-front--danger", status == FrontStatus.Danger);
            }
        }

        private void RefreshBases()
        {
            Combatant ours = battle.AlliedBase, theirs = battle.Objective.EnemyBase;
            if (ours != null) alliedBase.Set(ours.CurrentHealth, ours.MaximumHealth, $"{ours.CurrentHealth:0} / {ours.MaximumHealth:0}");
            if (theirs != null)
            {
                enemyBase.Set(theirs.CurrentHealth, theirs.MaximumHealth,
                    theirs.Invulnerable ? "SHIELDED  -  defeat the Warlord" : $"{theirs.CurrentHealth:0} / {theirs.MaximumHealth:0}");
                enemyBase.Root.EnableInClassList("lb-bar--shielded", theirs.Invulnerable);
            }
            Combatant warlord = battle.Waves.Boss;
            bool bossAlive = warlord != null && warlord.IsAlive;
            Hud.Show(boss, bossAlive);
            if (bossAlive) bossBar.Set(warlord.CurrentHealth, warlord.MaximumHealth, $"{warlord.CurrentHealth:0} / {warlord.MaximumHealth:0}");
        }

        private void RefreshAbilities(ManaResource manaStore)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                Slot slot = slots[i];
                int id = battle.Abilities.Equipped(i);
                bool valid = id >= 0 && id < EquipmentCatalog.Count;
                bool active = valid && EquipmentCatalog.IsActive(id);
                Hud.Set(slot.Name, valid ? EquipmentCatalog.Names[id] : "-");
                Hud.Set(slot.Cost, active ? EquipmentCatalog.Cost(id).ToString("0") + " mana" : "passive");
                float remaining = battle.Abilities.CooldownRemaining(i);
                bool cooling = active && remaining > 0f;
                Hud.Fraction(slot.Fill, cooling ? remaining / EquipmentCatalog.Cooldown(id) : 0f, true);
                slot.Root.EnableInClassList("lb-slot--passive", !active);
                slot.Root.EnableInClassList("lb-slot--cooling", cooling);
                slot.Root.EnableInClassList("lb-slot--nomana", active && manaStore.Current < EquipmentCatalog.Cost(id));
            }
        }

        private void RefreshTroops(FoodResource foodStore)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                Card card = cards[i];
                float cost = battle.Summoner.CostOf((UnitKind)i);
                Hud.Set(card.Cost, cost.ToString("0.#") + " food");
                Hud.Fraction(card.Fill, cost > 0f ? foodStore.CurrentFood / cost : 1f, true);
                card.Root.EnableInClassList("lb-card--ready", foodStore.CurrentFood >= cost);
                card.Root.EnableInClassList("lb-card--selected", (int)battle.Summoner.SelectedUnit == i);
            }
        }

        // Summon and ability feedback appear briefly above the command bar.
        private void RefreshToast()
        {
            string summon = battle.Summoner.LastFeedback ?? "", ability = battle.Abilities.Feedback ?? "";
            if (summon != lastSummonFeedback) { lastSummonFeedback = summon; Toast(summon); }
            if (ability != lastAbilityFeedback) { lastAbilityFeedback = ability; Toast(ability); }
            Hud.Show(toast, Time.unscaledTime < toastUntil);
        }

        private void Toast(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            Hud.Set(toast, message);
            toastUntil = Time.unscaledTime + ToastSeconds;
        }
    }
}

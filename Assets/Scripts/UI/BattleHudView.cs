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

    // In-battle HUD: battlefield map top-left, fronts and bases on top, hero portrait and vitals bottom-left,
    // ability orbs bottom-centre, the troop roster down the right edge, key hints bottom-right.
    // Pure view: it reads the battle every frame and never changes gameplay state.
    // Art comes from Assets/UI/Art (Lightbringer/UI/Build Battle HUD Art).
    public sealed class BattleHudView
    {
        private const float ToastSeconds = 2.5f;
        private static readonly string[] SlotKeys = { "LMB", "Q", "E" };
        private static readonly Color CooldownShade = new Color(0.01f, 0.02f, 0.05f, 0.72f);

        public readonly VisualElement Root;
        private readonly Label stage, waves, pathHint, level, toast;
        private readonly VisualElement fronts, boss, abilities, troops, pathKey;
        private readonly HudBar alliedBase, enemyBase, bossBar, health, mana, food;
        private readonly List<FrontChip> frontChips = new List<FrontChip>();
        private readonly List<Slot> slots = new List<Slot>();
        private readonly List<Card> cards = new List<Card>();
        private readonly HealthBarOverlay healthBars;
        private readonly BattleMinimap minimap;
        private PrototypeBattle battle;
        private BattlefieldReadability readability;
        private string lastSummonFeedback = "", lastAbilityFeedback = "";
        private float toastUntil;

        private struct FrontChip { public VisualElement Root; public Label Allies, Enemies; }
        private class Slot { public VisualElement Root, Orb; public HudDial Dial; public Label Name, Cost, Timer; public int Shown = -1; }
        private struct Card { public VisualElement Root, Shade; public Label Cost; }

        public BattleHudView(VisualElement parent)
        {
            Root = Hud.Box(parent, "lb-hud");
            healthBars = new HealthBarOverlay(Root);
            Hud.Box(Root, "lb-hud-vignette");

            // The map also names the Path new troops will take; Tab cycles it.
            VisualElement mapPanel = Hud.Box(Root, "lb-minimap", "lb-hud-frame");
            Heading(mapPanel, "BATTLEFIELD");
            minimap = new BattleMinimap(mapPanel);
            VisualElement route = Hud.Box(mapPanel, "lb-minimap-route");
            Hud.Text(route, "NEW TROOPS", "lb-route-caption");
            pathHint = Hud.Text(route, "", "lb-display", "lb-path-hint");
            pathKey = Key(route, "TAB");

            VisualElement top = Hud.Box(Root, "lb-top");
            VisualElement row = Hud.Box(top, "lb-top-row", "lb-hud-panel");
            stage = Hud.Text(row, "", "lb-display", "lb-stage");
            Hud.Box(row, "lb-divider");
            fronts = Hud.Box(row, "lb-fronts");
            Hud.Box(row, "lb-divider");
            Hud.Box(row, "lb-hud-icon", "lb-hud-icon--wave");
            waves = Hud.Text(row, "", "lb-waves");
            VisualElement bases = Hud.Box(top, "lb-bases");
            alliedBase = Base(bases, "LIGHTBRINGER GATE", "lb-base--ally", "lb-bar--ally");
            enemyBase = Base(bases, "ENEMY STRONGHOLD", "lb-base--enemy", "lb-bar--enemy");

            boss = Hud.Box(Root, "lb-boss");
            VisualElement bossTitle = Hud.Box(boss, "lb-boss-title");
            Hud.Box(bossTitle, "lb-hud-icon", "lb-hud-icon--enemy");
            Hud.Text(bossTitle, "THE WARLORD", "lb-display", "lb-boss-name");
            Hud.Box(bossTitle, "lb-hud-icon", "lb-hud-icon--enemy");
            bossBar = new HudBar(boss, "lb-bar--boss");

            VisualElement crosshair = Hud.Box(Root, "lb-crosshair");
            Hud.Box(crosshair, "lb-crosshair-dot");
            toast = Hud.Text(Root, "", "lb-toast");

            // Hero: portrait medallion with level badge, then health, mana and food gauges.
            VisualElement vitals = Hud.Box(Root, "lb-vitals", "lb-hud-panel");
            VisualElement portrait = Hud.Box(vitals, "lb-portrait");
            VisualElement face = Hud.Box(portrait, "lb-portrait-face");
            Hud.Box(face, "lb-portrait-art");
            Hud.Box(portrait, "lb-portrait-ring");
            VisualElement badge = Hud.Box(portrait, "lb-level");
            level = Hud.Text(badge, "", "lb-display", "lb-level-value");
            VisualElement gauges = Hud.Box(vitals, "lb-gauges");
            Hud.Text(gauges, "MAGE COMMANDER", "lb-display", "lb-hero-name");
            health = Vital(gauges, "HP", "lb-bar--hp");
            mana = Vital(gauges, "MP", "lb-bar--mana");
            VisualElement foodRow = Hud.Box(gauges, "lb-vital-row");
            Hud.Box(foodRow, "lb-hud-icon", "lb-hud-icon--food", "lb-vital-icon");
            food = new HudBar(foodRow, "lb-bar--food");

            VisualElement command = Hud.Box(Root, "lb-command");
            abilities = Hud.Box(command, "lb-abilities");
            // Troop roster: one row per unlocked troop down the right edge.
            troops = Hud.Box(Root, "lb-troops");

            VisualElement hints = Hud.Box(Root, "lb-hints", "lb-hud-panel");
            Hint(hints, ("WASD", "Move"), ("Mouse", "Look"), ("Wheel", "Zoom"));
            Hint(hints, ("1-8", "Summon"), ("F", "Repeat"), ("Tab", "Path"), ("Esc", "Cursor"));
            Hud.IgnorePicking(Root);
        }

        private static void Heading(VisualElement parent, string title)
        {
            VisualElement head = Hud.Box(parent, "lb-hud-heading");
            Hud.Box(head, "lb-prep-bullet");
            Hud.Text(head, title, "lb-display", "lb-hud-title");
        }

        private static VisualElement Key(VisualElement parent, string key)
        {
            VisualElement cap = Hud.Box(parent, "lb-keycap");
            Hud.Text(cap, key, "lb-keycap-text");
            return cap;
        }

        private static void Hint(VisualElement parent, params (string key, string action)[] entries)
        {
            VisualElement row = Hud.Box(parent, "lb-hint-row");
            foreach ((string key, string action) in entries)
            {
                Key(row, key);
                Hud.Text(row, action, "lb-hint-text");
            }
        }

        private static HudBar Base(VisualElement parent, string title, string modifier, string barModifier)
        {
            VisualElement panel = Hud.Box(parent, "lb-base", modifier, "lb-hud-panel");
            VisualElement head = Hud.Box(panel, "lb-base-head");
            Hud.Box(head, "lb-hud-icon", "lb-hud-icon--castle");
            Hud.Text(head, title, "lb-base-title");
            return new HudBar(panel, barModifier);
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
        public int MinimapSelectedPath => minimap.SelectedPath;
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
            minimap.Bind(battle, readability);
            Hud.Show(Root, battle != null);
            if (battle == null) return;
            for (int i = 0; i < battle.Paths.Length; i++)
            {
                VisualElement chip = Hud.Box(fronts, "lb-front");
                Hud.Text(chip, (i + 1).ToString(), "lb-display", "lb-front-number");
                Hud.Box(chip, "lb-hud-icon", "lb-hud-icon--ally", "lb-front-icon");
                Label allies = Hud.Text(chip, "", "lb-front-allies");
                Hud.Box(chip, "lb-hud-icon", "lb-hud-icon--enemy", "lb-front-icon");
                frontChips.Add(new FrontChip { Root = chip, Allies = allies, Enemies = Hud.Text(chip, "", "lb-front-enemies") });
            }
            for (int i = 0; i < EquipmentCatalog.Slots; i++)
            {
                VisualElement slot = Hud.Box(abilities, "lb-slot");
                VisualElement socket = Hud.Box(slot, "lb-socket-orb");
                Hud.Box(socket, "lb-orb-glow");
                VisualElement orb = Hud.Box(socket, "lb-orb");
                HudDial dial = new HudDial(socket, "lb-orb-dial", CooldownShade);
                Hud.Box(socket, "lb-orb-ring");
                Label timer = Hud.Text(socket, "", "lb-orb-timer");
                Key(socket, SlotKeys[i]).AddToClassList("lb-orb-key");
                VisualElement caption = Hud.Box(slot, "lb-slot-caption");
                slots.Add(new Slot
                {
                    Root = slot, Orb = orb, Dial = dial, Timer = timer,
                    Name = Hud.Text(caption, "", "lb-name"), Cost = Hud.Text(caption, "", "lb-cost"),
                });
            }
            for (int i = 0; i < battle.Summoner.UnlockedUnitCount; i++)
            {
                VisualElement card = Hud.Box(troops, "lb-card", "lb-card--" + i);
                Hud.Box(card, "lb-card-halo");
                VisualElement portrait = Hud.Box(card, "lb-card-portrait");
                VisualElement body = Hud.Box(portrait, "lb-card-body");
                Hud.Box(body, "lb-card-art");
                VisualElement shade = Hud.Box(body, "lb-card-shade");
                Hud.Box(portrait, "lb-card-frame");
                Key(portrait, (i + 1).ToString()).AddToClassList("lb-card-key");
                VisualElement info = Hud.Box(card, "lb-card-info");
                Hud.Text(info, UnitCatalog.Names[i], "lb-card-name");
                VisualElement price = Hud.Box(info, "lb-card-price");
                Hud.Box(price, "lb-hud-icon", "lb-hud-icon--food", "lb-card-food");
                Label cost = Hud.Text(price, "", "lb-card-cost");
                cards.Add(new Card { Root = card, Shade = shade, Cost = cost });
            }
            Hud.IgnorePicking(Root);
        }

        public void Refresh(CampaignSession session)
        {
            if (battle == null || session == null) return;
            Hud.Set(stage, "STAGE " + session.SelectedStage);
            Hud.Set(waves, $"WAVE {battle.Waves.WavesSpawned}/{battle.Waves.TotalWaves}");
            if (session.Progress != null) Hud.Set(level, session.Progress.level.ToString());
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
            Hud.Set(pathHint, $"PATH {selected + 1}");
            Hud.Show(pathKey, battle.Paths.Length > 1);
            minimap.Refresh(selected);
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
                if (slot.Shown != id)
                {
                    // Each item has its own emblem orb.
                    if (slot.Shown >= 0) slot.Orb.RemoveFromClassList("lb-orb--" + slot.Shown);
                    if (valid) slot.Orb.AddToClassList("lb-orb--" + id);
                    slot.Shown = id;
                }
                Hud.Set(slot.Name, valid ? EquipmentCatalog.Names[id] : "-");
                Hud.Set(slot.Cost, active ? EquipmentCatalog.Cost(id).ToString("0") + " MP" : "PASSIVE");
                float remaining = battle.Abilities.CooldownRemaining(i);
                bool cooling = active && remaining > 0f;
                slot.Dial.Set(cooling ? remaining / EquipmentCatalog.Cooldown(id) : 0f);
                // Only cooldowns long enough to read get a countdown.
                Hud.Set(slot.Timer, cooling && EquipmentCatalog.Cooldown(id) >= 1f ? remaining.ToString(remaining < 1f ? "0.0" : "0") : "");
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
                Hud.Set(card.Cost, cost.ToString("0.#"));
                // The shade drains from the top as Food builds toward this troop's cost.
                Hud.Fraction(card.Shade, cost > 0f ? 1f - foodStore.CurrentFood / cost : 0f, true);
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

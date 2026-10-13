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

    // In-battle HUD: parchment battlefield map top-left, the base standoff and fronts on top, hero portrait and vitals bottom-left,
    // ability orbs bottom-centre, the troop roster down the right edge, key hints bottom-right.
    // Pure view: it reads the battle every frame and never changes gameplay state.
    // Art comes from Assets/UI/Art (Lightbringer/UI/Build Battle HUD Art).
    public sealed class BattleHudView
    {
        private const float ToastSeconds = 2.5f;
        private static readonly string[] SlotKeys = SkillCatalog.SlotKeys;
        private static readonly Color CooldownShade = new Color(0.01f, 0.02f, 0.05f, 0.72f);

        public readonly VisualElement Root;
        private readonly Label stage, waves, pathHint, level, toast;
        private readonly VisualElement fronts, boss, demonBoss, abilities, troops, pathKey;
        private readonly HudBar alliedBase, enemyBase, bossBar, demonBar, health, mana, food;
        private readonly List<FrontChip> frontChips = new List<FrontChip>();
        private readonly List<Slot> slots = new List<Slot>();
        private readonly List<Card> cards = new List<Card>();
        private readonly HealthBarOverlay healthBars;
        private readonly BattleMinimap minimap;
        private PrototypeBattle battle;
        private BattlefieldReadability readability;
        private string lastSummonFeedback = "", lastAbilityFeedback = "";
        private float toastUntil;

        private struct FrontChip { public VisualElement Root, AllyPush, EnemyPush; public Label Allies, Enemies; }
        private class Slot { public VisualElement Root, Orb; public HudDial Dial; public Label Name, Cost, Timer; public int Shown = -1; }
        private struct Card { public VisualElement Root, Shade; public Label Cost; }

        public BattleHudView(VisualElement parent)
        {
            Root = Hud.Box(parent, "lb-hud");
            healthBars = new HealthBarOverlay(Root);
            Hud.Box(Root, "lb-hud-vignette");

            // The map also names the Path new troops will take; Tab cycles it.
            // The battlefield map is a parchment page with a compass rose, set in a paper panel under its title.
            VisualElement mapPanel = Hud.Box(Root, "lb-minimap", "lb-hud-panel");
            Hud.Text(mapPanel, "전장 지도", "lb-minimap-title");
            VisualElement sheet = Hud.Box(mapPanel, "lb-minimap-sheet");
            minimap = new BattleMinimap(sheet);
            Hud.Box(sheet, "lb-minimap-compass");
            VisualElement route = Hud.Box(mapPanel, "lb-minimap-route");
            Hud.Text(route, "소환 진로", "lb-route-caption");
            pathHint = Hud.Text(route, "", "lb-display", "lb-path-hint");
            pathKey = Key(route, "TAB");

            // Standoff across the top on one ink band: our stronghold, the stage seal with the assault count, theirs.
            VisualElement top = Hud.Box(Root, "lb-top");
            VisualElement row = Hud.Box(top, "lb-top-row");
            alliedBase = Base(row, "아군 본진", "lb-base--ally", "lb-bar--ally");
            VisualElement centre = Hud.Box(row, "lb-top-centre");
            stage = Hud.Text(centre, "", "lb-display", "lb-stage");
            VisualElement waveRow = Hud.Box(centre, "lb-wave-row");
            Hud.Box(waveRow, "lb-hud-icon", "lb-hud-icon--wave");
            waves = Hud.Text(waveRow, "", "lb-waves");
            enemyBase = Base(row, "적군 본진", "lb-base--enemy", "lb-bar--enemy");
            fronts = Hud.Box(top, "lb-fronts");

            boss = BossPanel("군주", out bossBar);
            // Final battle only: the Demon King's health, carried by the abyss core.
            demonBoss = BossPanel("마왕 · 어둠의 핵", out demonBar);
            demonBoss.AddToClassList("lb-boss--second");

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
            Hud.Text(gauges, "마법 지휘관", "lb-display", "lb-hero-name");
            health = Vital(gauges, "HP", "lb-bar--hp");
            mana = Vital(gauges, "MP", "lb-bar--mana");
            VisualElement foodRow = Hud.Box(gauges, "lb-vital-row");
            Hud.Text(foodRow, "식량", "lb-vital-label");
            food = new HudBar(foodRow, "lb-bar--food");

            VisualElement command = Hud.Box(Root, "lb-command");
            abilities = Hud.Box(command, "lb-abilities");
            // Troop roster: one row per unlocked troop down the right edge.
            troops = Hud.Box(Root, "lb-troops");

            VisualElement hints = Hud.Box(Root, "lb-hints", "lb-hud-panel");
            Hint(hints, ("WASD", "이동"), ("마우스", "시점"), ("휠", "확대·축소"));
            Hint(hints, ("1-8", "소환"), ("F", "재소환"), ("Tab", "진로 변경"), ("Esc", "일시정지"));
            Hud.IgnorePicking(Root);
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
        public bool DemonKingVisible => !demonBoss.ClassListContains("lb-hidden");
        public bool EnemyBaseShielded => enemyBase.Root.ClassListContains("lb-bar--shielded");
        public bool IsCardReady(int index) => cards[index].Root.ClassListContains("lb-card--ready");
        public bool IsCardSelected(int index) => cards[index].Root.ClassListContains("lb-card--selected");
        public string SlotName(int index) => slots[index].Name.text;
        public bool IsSlotCooling(int index) => slots[index].Root.ClassListContains("lb-slot--cooling");
        public FrontStatus FrontStatusOf(int index) =>
            frontChips[index].Root.ClassListContains("lb-front--danger") ? FrontStatus.Danger
            : frontChips[index].Root.ClassListContains("lb-front--contested") ? FrontStatus.Contested : FrontStatus.Clear;

        // The seal shows a small stage label above the stage number.
        public static string StageSealText(int stage) =>
            $"<size=18><color=#CDB48C>stage</color></size>\n{stage}";

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
                // A front card reads like the minimap: the Path seal, then our troops and theirs pushing on a
                // tug-of-war gauge between them.
                VisualElement chip = Hud.Box(fronts, "lb-front");
                Hud.Text(chip, (i + 1).ToString(), "lb-display", "lb-front-number");
                Hud.Box(chip, "lb-hud-icon", "lb-hud-icon--ally", "lb-front-icon");
                Label allies = Hud.Text(chip, "", "lb-front-allies");
                VisualElement tug = Hud.Box(chip, "lb-front-tug");
                VisualElement allyPush = Hud.Box(tug, "lb-front-push", "lb-front-push--ally");
                VisualElement enemyPush = Hud.Box(tug, "lb-front-push", "lb-front-push--enemy");
                Hud.Box(tug, "lb-front-tug-mid");
                Label enemies = Hud.Text(chip, "", "lb-front-enemies");
                Hud.Box(chip, "lb-hud-icon", "lb-hud-icon--enemy", "lb-front-icon");
                frontChips.Add(new FrontChip { Root = chip, Allies = allies, Enemies = enemies, AllyPush = allyPush, EnemyPush = enemyPush });
            }
            for (int i = 0; i < SkillCatalog.Slots; i++)
            {
                VisualElement slot = Hud.Box(abilities, "lb-slot");
                VisualElement socket = Hud.Box(slot, "lb-socket-orb");
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
                VisualElement portrait = Hud.Box(card, "lb-card-portrait");
                VisualElement body = Hud.Box(portrait, "lb-card-body");
                Hud.Box(body, "lb-card-art");
                VisualElement shade = Hud.Box(body, "lb-card-shade");
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
            Hud.Set(stage, StageSealText(session.SelectedStage));
            Hud.Set(waves, battle.Encounter != null && battle.Waves.AssaultCleared ? battle.Encounter.ObjectiveText
                : $"공세 {battle.Waves.WavesSpawned}/{battle.Waves.TotalWaves}");
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
            Hud.Set(pathHint, $"진로 {selected + 1}");
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
                // Each side fills from its own end in proportion to its share; an empty front leaves the gauge bare.
                int total = allies + enemies;
                chip.AllyPush.style.width = Length.Percent(total > 0 ? 100f * allies / total : 0f);
                chip.EnemyPush.style.width = Length.Percent(total > 0 ? 100f * enemies / total : 0f);
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
                    theirs.Invulnerable ? "보호막 · 공세를 모두 격퇴하세요" : $"{theirs.CurrentHealth:0} / {theirs.MaximumHealth:0}");
                enemyBase.Root.EnableInClassList("lb-bar--shielded", theirs.Invulnerable);
            }
            if (battle.Objective.IsBaseDestroyed)
            {
                enemyBase.Set(0, 1, "적 본진 파괴됨");
                enemyBase.Root.EnableInClassList("lb-bar--shielded", false);
            }
            Combatant warlord = battle.Waves.Boss;
            bool bossAlive = warlord != null && warlord.IsAlive;
            Hud.Show(boss, bossAlive);
            if (bossAlive) bossBar.Set(warlord.CurrentHealth, warlord.MaximumHealth, $"{warlord.CurrentHealth:0} / {warlord.MaximumHealth:0}");
            Combatant core = battle.Encounter != null ? battle.Encounter.DemonCore : null;
            bool coreAlive = core != null && core.IsAlive;
            Hud.Show(demonBoss, coreAlive);
            // With the Warlord down, the Demon King's bar takes his place.
            demonBoss.EnableInClassList("lb-boss--second", bossAlive);
            if (coreAlive) demonBar.Set(core.CurrentHealth, core.MaximumHealth, $"{core.CurrentHealth:0} / {core.MaximumHealth:0}");
        }

        private VisualElement BossPanel(string title, out HudBar bar)
        {
            VisualElement panel = Hud.Box(Root, "lb-boss");
            VisualElement heading = Hud.Box(panel, "lb-boss-title");
            Hud.Box(heading, "lb-boss-ornament");
            Hud.Text(heading, title, "lb-display", "lb-boss-name");
            Hud.Box(heading, "lb-boss-ornament");
            bar = new HudBar(panel, "lb-bar--boss");
            return panel;
        }

        private void RefreshAbilities(ManaResource manaStore)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                Slot slot = slots[i];
                int id = battle.Abilities.Equipped(i);
                bool valid = SkillCatalog.IsSkill(id);
                bool active = valid;
                if (slot.Shown != id)
                {
                    // Each item has its own emblem orb.
                    if (slot.Shown >= 0) slot.Orb.RemoveFromClassList("lb-orb--" + slot.Shown);
                    if (valid) slot.Orb.AddToClassList("lb-orb--" + id);
                    slot.Shown = id;
                }
                Hud.Set(slot.Name, valid ? SkillCatalog.Names[id] : "비어 있음");
                Hud.Set(slot.Cost, active ? SkillCatalog.Cost(id).ToString("0") + " MP" : "");
                float remaining = battle.Abilities.CooldownRemaining(i);
                bool cooling = active && remaining > 0f;
                slot.Dial.Set(cooling ? remaining / SkillCatalog.Cooldown(id) : 0f);
                // Only cooldowns long enough to read get a countdown.
                Hud.Set(slot.Timer, cooling && SkillCatalog.Cooldown(id) >= 1f ? remaining.ToString(remaining < 1f ? "0.0" : "0") : "");
                slot.Root.EnableInClassList("lb-slot--passive", !active);
                slot.Root.EnableInClassList("lb-slot--cooling", cooling);
                slot.Root.EnableInClassList("lb-slot--nomana", active && manaStore.Current < SkillCatalog.Cost(id));
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

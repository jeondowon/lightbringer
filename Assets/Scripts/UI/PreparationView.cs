using Lightbringer.Core;
using Lightbringer.Player;
using Lightbringer.Progression;
using Lightbringer.Units;
using UnityEngine;
using UnityEngine.UIElements;

namespace Lightbringer.UI
{
    // Campaign preparation between battles: hero illustration, campaign route and briefing, three-slot loadout,
    // troops and permanent growth. Equipping is slot-first: pick LMB / Q / E, then click an item to place it there.
    // Art comes from Assets/UI/Art through style classes (Lightbringer/UI/Build Preparation Art).
    public sealed class PreparationView
    {
        private static readonly string[] SlotKeys = { "LMB", "Q", "E" };
        private const int MoteCount = 22;

        public readonly VisualElement Root;
        private readonly VisualElement hero, runeOuter, runeInner, runeBriefing, startGlow, routeFill;
        private readonly VisualElement enemies, rewards, growth;
        private readonly VisualElement[] mapPaths = new VisualElement[3];
        private readonly Label level, stageTitle, stageTag, briefingNote, status, mapCaption, mapStronghold;
        private readonly HudBar experience;
        private readonly Tile[] stages = new Tile[CampaignProgress.StageCount];
        private readonly Tile[] slots = new Tile[EquipmentCatalog.Slots];
        private readonly Tile[] items = new Tile[EquipmentCatalog.Count];
        private readonly VisualElement[] slotIcons = new VisualElement[EquipmentCatalog.Slots];
        private readonly Tile[] troops = new Tile[UnitCatalog.Count];
        private readonly Mote[] motes = new Mote[MoteCount];
        private readonly Button start;
        private CampaignSession session;
        private string shownGrowth = "", shownBriefing = "";

        private struct Tile { public VisualElement Root; public Label Main, Note; }
        private struct Mote { public VisualElement Element; public float X, Speed, Phase, Drift, Brightness; }

        public PreparationView(VisualElement parent)
        {
            Root = Hud.Box(parent, "lb-prep");

            // Backdrop: painted sky, slowly turning aura runes, the hero and drifting light.
            Hud.Box(Root, "lb-prep-bg");
            runeOuter = Hud.Box(Root, "lb-prep-rune");
            runeInner = Hud.Box(Root, "lb-prep-rune", "lb-prep-rune--inner");
            Hud.Box(Root, "lb-prep-hero-glow");
            hero = Hud.Box(Root, "lb-prep-hero");
            VisualElement moteLayer = Hud.Box(Root, "lb-prep-motes");
            System.Random random = new System.Random(11);
            for (int i = 0; i < MoteCount; i++)
            {
                VisualElement element = Hud.Box(moteLayer, "lb-prep-mote");
                float size = 5f + (float)random.NextDouble() * 9f;
                element.style.width = element.style.height = size;
                // Most light rises around the hero; a few motes drift across the whole screen.
                float x = i < 15 ? 0.02f + (float)random.NextDouble() * 0.34f : (float)random.NextDouble();
                motes[i] = new Mote
                {
                    Element = element, X = x, Speed = 0.025f + (float)random.NextDouble() * 0.04f, Phase = (float)random.NextDouble(),
                    Drift = 10f + (float)random.NextDouble() * 26f, Brightness = 0.35f + (float)random.NextDouble() * 0.6f,
                };
            }

            VisualElement header = Hud.Box(Root, "lb-prep-header");
            Hud.Box(header, "lb-prep-emblem");
            VisualElement titles = Hud.Box(header, "lb-prep-titles");
            Hud.Text(titles, "LIGHTBRINGER", "lb-display", "lb-prep-title");
            Hud.Text(titles, "CAMPAIGN PREPARATION", "lb-prep-subtitle");

            VisualElement plate = Hud.Box(Root, "lb-prep-frame", "lb-prep-plate");
            VisualElement medal = Hud.Box(plate, "lb-prep-medal");
            Hud.Text(medal, "LV", "lb-prep-medal-label");
            level = Hud.Text(medal, "", "lb-display", "lb-prep-medal-value");
            VisualElement identity = Hud.Box(plate, "lb-prep-identity");
            Hud.Text(identity, "MAGE COMMANDER", "lb-display", "lb-prep-hero-name");
            Hud.Text(identity, "Hero of the Lightbringers", "lb-prep-hero-role");
            experience = new HudBar(identity, "lb-bar--exp");

            VisualElement content = Hud.Box(Root, "lb-prep-content");
            VisualElement campaign = Hud.Box(content, "lb-prep-column", "lb-prep-column--campaign");
            VisualElement army = Hud.Box(content, "lb-prep-column", "lb-prep-column--army");

            VisualElement route = Hud.Box(Panel(campaign, "CAMPAIGN", "Choose your battlefield"), "lb-route");
            VisualElement track = Hud.Box(route, "lb-route-track");
            routeFill = Hud.Box(track, "lb-route-fill");
            for (int i = 0; i < stages.Length; i++)
            {
                int stage = i + 1;
                Button button = Clickable(route, () => session?.SelectStage(stage), "lb-stage");
                Hud.Box(button, "lb-stage-glow");
                VisualElement circle = Hud.Box(button, "lb-stage-circle");
                stages[i] = new Tile { Root = button, Main = Hud.Text(circle, stage.ToString(), "lb-display", "lb-stage-number"), Note = Hud.Text(button, "", "lb-stage-state") };
                Decorate(button);
            }

            VisualElement briefing = Panel(campaign, "BRIEFING", "", "lb-prep-panel--briefing");
            runeBriefing = Hud.Box(briefing, "lb-briefing-rune");
            VisualElement titleRow = Hud.Box(briefing, "lb-briefing-title-row");
            stageTitle = Hud.Text(titleRow, "", "lb-display", "lb-briefing-title");
            stageTag = Hud.Text(titleRow, "", "lb-briefing-tag");
            Hud.Text(briefing, "ENEMY FORCES", "lb-prep-caption");
            enemies = Hud.Box(briefing, "lb-chips");
            Hud.Text(briefing, "FIRST CLEAR REWARD", "lb-prep-caption");
            rewards = Hud.Box(briefing, "lb-rewards");
            mapCaption = Hud.Text(briefing, "", "lb-prep-caption");
            VisualElement map = Hud.Box(briefing, "lb-map");
            for (int i = 0; i < mapPaths.Length; i++)
            {
                mapPaths[i] = Hud.Box(map, "lb-map-path");
                Hud.Box(mapPaths[i], "lb-map-path-line");
                Hud.Text(mapPaths[i], "PATH " + (i + 1), "lb-map-path-label");
            }
            mapStronghold = Hud.Text(map, "", "lb-map-base", "lb-map-base--enemy");
            Hud.Text(map, "LIGHTBRINGER GATE", "lb-map-base", "lb-map-base--ally");
            Hud.IgnorePicking(map);
            briefingNote = Hud.Text(briefing, "", "lb-briefing-note");

            VisualElement launch = Hud.Box(campaign, "lb-launch");
            startGlow = Hud.Box(launch, "lb-launch-glow");
            start = Clickable(launch, () => Start(), "lb-display", "lb-launch-button");
            Hud.Text(campaign, "Prototype balance. First clears award a staff or ring; upgraded gear replaces lower levels.", "lb-prep-footnote");

            VisualElement loadout = Panel(army, "LOADOUT", "Choose a slot, then an item");
            VisualElement socketRow = Hud.Box(loadout, "lb-sockets");
            for (int i = 0; i < slots.Length; i++)
            {
                int slot = i;
                Button button = Clickable(socketRow, () => SelectSlot(slot), "lb-socket");
                Hud.Box(button, "lb-halo");
                slotIcons[i] = Hud.Box(Hud.Box(button, "lb-socket-ring"), "lb-icon");
                VisualElement text = Hud.Box(button, "lb-socket-text");
                Hud.Text(text, SlotKeys[i], "lb-socket-key");
                slots[i] = new Tile { Root = button, Main = Hud.Text(text, "", "lb-socket-name"), Note = Hud.Text(text, "", "lb-socket-note") };
                Decorate(button);
            }
            VisualElement armory = Hud.Box(loadout, "lb-armory");
            for (int i = 0; i < items.Length; i++)
            {
                int id = i;
                Button button = Clickable(armory, () => Equip(id), "lb-item");
                Hud.Box(button, "lb-halo");
                Hud.Box(button, "lb-icon", "lb-icon--" + i);
                items[i] = new Tile { Root = button, Main = Hud.Text(button, EquipmentCatalog.Names[i], "lb-item-name"), Note = Hud.Text(button, "", "lb-item-note") };
                Decorate(button);
            }

            VisualElement roster = Hud.Box(Panel(army, "TROOPS", "Every unlocked troop joins each battle"), "lb-troops-grid");
            for (int i = 0; i < troops.Length; i++)
            {
                VisualElement card = Hud.Box(roster, "lb-troop", "lb-troop--" + i);
                Hud.Box(card, "lb-troop-glow");
                Hud.Box(card, "lb-troop-art");
                Hud.Box(card, "lb-troop-fade");
                troops[i] = new Tile { Root = card, Main = Hud.Text(card, UnitCatalog.Names[i], "lb-troop-name"), Note = Hud.Text(card, "", "lb-troop-cost") };
                Hud.IgnorePicking(card);
            }

            growth = Hud.Box(Panel(army, "PERMANENT GROWTH", "", "lb-prep-panel--growth"), "lb-chips");
            status = Hud.Text(Root, "", "lb-prep-status");

            Hud.IgnorePicking(runeOuter); Hud.IgnorePicking(runeInner); Hud.IgnorePicking(hero);
            Hud.IgnorePicking(moteLayer); Hud.IgnorePicking(runeBriefing); Hud.IgnorePicking(startGlow);
            Hud.Show(Root, false);
        }

        public int ActiveSlot { get; private set; }
        public bool Visible => !Root.ClassListContains("lb-hidden");
        public string StartText => start.text;
        public int TroopCount { get { int count = 0; foreach (Tile troop in troops) if (!troop.Root.ClassListContains("lb-troop--locked")) count++; return count; } }
        public string SlotText(int slot) => slots[slot].Main.text;
        public bool IsStageEnabled(int stage) => stages[stage - 1].Root.enabledSelf;
        public bool IsStageSelected(int stage) => stages[stage - 1].Root.ClassListContains("lb-stage--selected");
        public bool IsItemEnabled(int id) => items[id].Root.enabledSelf;
        public string ItemNote(int id) => items[id].Note.text;

        private static VisualElement Panel(VisualElement parent, string title, string hint, params string[] classes)
        {
            VisualElement panel = Hud.Box(parent, "lb-prep-frame", "lb-prep-panel");
            foreach (string name in classes) panel.AddToClassList(name);
            VisualElement head = Hud.Box(panel, "lb-prep-panel-head");
            Hud.Box(head, "lb-prep-bullet");
            Hud.Text(head, title, "lb-display", "lb-prep-panel-title");
            if (!string.IsNullOrEmpty(hint)) Hud.Text(head, hint, "lb-prep-hint");
            return panel;
        }

        private static Button Clickable(VisualElement parent, System.Action click, params string[] classes)
        {
            Button button = new Button(click);
            foreach (string name in classes) button.AddToClassList(name);
            parent.Add(button);
            return button;
        }

        // Clicks on a button's decorations must reach the button.
        private static void Decorate(VisualElement button)
        {
            foreach (VisualElement child in button.Children()) Hud.IgnorePicking(child);
        }

        public void Refresh(CampaignSession value)
        {
            session = value;
            CampaignProgress profile = session != null ? session.Progress : null;
            bool visible = profile != null && session.Battle == null;
            Hud.Show(Root, visible);
            if (!visible) return;

            Hud.Set(level, profile.level.ToString());
            experience.Set(profile.experience, profile.ExperienceToNext, $"EXP {profile.experience} / {profile.ExperienceToNext}");

            Hud.Fraction(routeFill, (profile.unlockedStage - 1) / (float)(stages.Length - 1));
            for (int i = 0; i < stages.Length; i++)
            {
                int stage = i + 1;
                bool unlocked = stage <= profile.unlockedStage;
                Tile tile = stages[i];
                Hud.Set(tile.Note, !unlocked ? "LOCKED" : profile.cleared[i] ? "CLEARED" : stage == EnemyCatalog.BossStage ? "BOSS" : "NEW");
                tile.Root.SetEnabled(unlocked);
                tile.Root.EnableInClassList("lb-stage--selected", stage == session.SelectedStage);
                tile.Root.EnableInClassList("lb-stage--cleared", unlocked && profile.cleared[i]);
                tile.Root.EnableInClassList("lb-stage--new", unlocked && !profile.cleared[i]);
                tile.Root.EnableInClassList("lb-stage--boss", stage == EnemyCatalog.BossStage);
            }
            RefreshBriefing(profile, session.SelectedStage);

            for (int i = 0; i < slots.Length; i++)
            {
                int id = profile.loadout[i];
                Hud.Set(slots[i].Main, EquipmentCatalog.Names[id]);
                Hud.Set(slots[i].Note, $"+{profile.equipmentLevels[id]}  ·  {Describe(id)}");
                slots[i].Root.EnableInClassList("lb-socket--active", i == ActiveSlot);
                for (int k = 0; k < EquipmentCatalog.Count; k++) slotIcons[i].EnableInClassList("lb-icon--" + k, k == id);
            }

            for (int id = 0; id < items.Length; id++)
            {
                int owner = System.Array.IndexOf(profile.loadout, id);
                bool owned = profile.equipmentLevels[id] > 0;
                Tile tile = items[id];
                Hud.Set(tile.Note, !owned ? "LOCKED"
                    : owner >= 0 && owner != ActiveSlot ? $"+{profile.equipmentLevels[id]}  ·  in {SlotKeys[owner]}"
                    : $"+{profile.equipmentLevels[id]}  ·  {Describe(id)}");
                // An item already worn in another slot cannot be duplicated.
                tile.Root.SetEnabled(owned && (owner < 0 || owner == ActiveSlot));
                tile.Root.EnableInClassList("lb-item--equipped", owner == ActiveSlot);
                tile.Root.EnableInClassList("lb-item--used", owned && owner >= 0 && owner != ActiveSlot);
                tile.Root.EnableInClassList("lb-item--locked", !owned);
            }

            for (int i = 0; i < troops.Length; i++)
            {
                bool unlocked = i < profile.UnlockedUnits;
                troops[i].Root.EnableInClassList("lb-troop--locked", !unlocked);
                Hud.Set(troops[i].Note, unlocked ? UnitCatalog.Cost((UnitKind)i).ToString("0.#") + " FOOD" : "UNLOCK AT STAGE " + (i + 1));
            }
            RefreshGrowth(profile);

            Hud.Set(start, "START STAGE " + session.SelectedStage);
            start.SetEnabled(!session.IsChoosing);
            string message = string.IsNullOrEmpty(session.PlaytestStatus) ? session.SaveStatus
                : session.PlaytestStatus + " " + session.SaveStatus;
            Hud.Set(status, message ?? "");
            Hud.Show(status, !string.IsNullOrEmpty(message));

            Animate(Time.unscaledTime);
        }

        // The briefing only changes with the selected stage or its clear state.
        private void RefreshBriefing(CampaignProgress profile, int stage)
        {
            bool cleared = profile.cleared[stage - 1];
            string key = stage + "|" + cleared + "|" + profile.IsComplete + "|" + string.Join(",", profile.equipmentLevels);
            if (key == shownBriefing) return;
            shownBriefing = key;

            bool boss = EnemyCatalog.HasBoss(stage);
            Hud.Set(stageTitle, "STAGE " + stage);
            Hud.Set(stageTag, boss ? "BOSS BATTLE" : cleared ? "CLEARED" : "NEW BATTLE");
            stageTag.EnableInClassList("lb-briefing-tag--boss", boss);
            stageTag.EnableInClassList("lb-briefing-tag--cleared", cleared && !boss);

            enemies.Clear();
            for (int k = 0; k < EnemyCatalog.Count; k++)
            {
                EnemyKind kind = (EnemyKind)k;
                if (!EnemyCatalog.AppearsIn(kind, stage)) continue;
                VisualElement chip = Hud.Box(enemies, "lb-chip", "lb-chip--enemy");
                if (EnemyCatalog.IsHeavy(kind)) chip.AddToClassList("lb-chip--heavy");
                Hud.Box(chip, "lb-chip-dot");
                Hud.Text(chip, EnemyCatalog.Names[k].Replace("Enemy ", ""), "lb-chip-name");
                if (!EnemyCatalog.AppearsIn(kind, stage - 1)) Hud.Text(chip, "NEW", "lb-chip-badge");
            }

            rewards.Clear();
            if (cleared)
                Hud.Text(rewards, "Claimed. Replays award battle EXP only.", "lb-prep-hint");
            else
            {
                int item = CampaignProgress.ClearRewardItem(stage), rank = CampaignProgress.ClearRewardLevel(stage);
                VisualElement gear = Hud.Box(rewards, "lb-reward");
                Hud.Box(gear, "lb-reward-icon", "lb-icon", "lb-icon--" + item);
                Hud.Text(gear, EquipmentCatalog.Names[item] + " +" + rank, "lb-reward-value");
                if (profile.equipmentLevels[item] >= rank) Hud.Text(gear, "owned", "lb-prep-hint");
            }

            int paths = PrototypeBattleBuilder.PathCount(stage);
            Hud.Set(mapCaption, $"BATTLEFIELD  ·  {paths} {(paths == 1 ? "PATH" : "PATHS")}  ·  {EnemyWaveSpawner.WaveCount(stage)} WAVES");
            for (int i = 0; i < mapPaths.Length; i++)
            {
                Hud.Show(mapPaths[i], i < paths);
                mapPaths[i].style.left = Length.Percent(50f + (i - (paths - 1) * 0.5f) * 30f);
            }
            Hud.Set(mapStronghold, boss ? "WARLORD'S STRONGHOLD" : "ENEMY STRONGHOLD");
            mapStronghold.EnableInClassList("lb-map-base--boss", boss);

            Hud.Set(briefingNote, profile.IsComplete ? "Campaign complete. Every stage can be replayed."
                : boss ? "The enemy stronghold stays shielded until the Warlord falls." : "");
            Hud.Show(briefingNote, !string.IsNullOrEmpty(briefingNote.text));
        }

        // Growth only changes between battles; rebuild it when its contents change.
        private void RefreshGrowth(CampaignProgress profile)
        {
            string key = string.Join(",", profile.ranks);
            if (key == shownGrowth) return;
            shownGrowth = key;
            growth.Clear();
            for (int i = 0; i < profile.ranks.Length; i++)
            {
                if (profile.ranks[i] <= 0) continue;
                VisualElement chip = Hud.Box(growth, "lb-chip");
                Hud.Text(chip, CampaignProgress.GrowthNames[i], "lb-chip-name");
                Hud.Text(chip, "x" + profile.ranks[i], "lb-chip-detail");
            }
            if (growth.childCount == 0) Hud.Text(growth, "None yet. Level up in battle to choose upgrades.", "lb-prep-hint");
        }

        // Transform and opacity only: these animate without re-running layout.
        private void Animate(float time)
        {
            runeOuter.style.rotate = new Rotate(new Angle(time * 3f));
            runeInner.style.rotate = new Rotate(new Angle(-time * 5f));
            runeBriefing.style.rotate = new Rotate(new Angle(time * 6f));
            hero.style.translate = new Translate(0f, Mathf.Sin(time * 1.1f) * 6f);
            startGlow.style.opacity = 0.55f + 0.45f * Mathf.Sin(time * 2.4f);

            float width = Root.layout.width, height = Root.layout.height;
            if (!(width > 0f) || !(height > 0f)) { width = 1920f; height = 1080f; }
            foreach (Mote mote in motes)
            {
                float life = Mathf.Repeat(time * mote.Speed + mote.Phase, 1f);
                float x = mote.X * width + Mathf.Sin(time * 0.7f + mote.Phase * 6.283f) * mote.Drift;
                mote.Element.style.translate = new Translate(x, height * (1.04f - life * 1.1f));
                mote.Element.style.opacity = Mathf.Sin(life * Mathf.PI) * mote.Brightness;
            }
        }

        private static string Describe(int id) => EquipmentCatalog.IsActive(id) ? EquipmentCatalog.Cost(id).ToString("0") + " mana" : "passive";

        public void SelectSlot(int slot)
        {
            if (slot >= 0 && slot < slots.Length) ActiveSlot = slot;
            if (session != null) Refresh(session);
        }

        // Also used by validation, which has no panel to click in.
        public bool Equip(int id)
        {
            bool equipped = session != null && session.TryEquip(ActiveSlot, id);
            if (session != null) Refresh(session);
            return equipped;
        }

        public bool Start() => session != null && session.StartBattle();
    }
}

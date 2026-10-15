using Lightbringer.Core;
using Lightbringer.Player;
using Lightbringer.Progression;
using Lightbringer.Units;
using UnityEngine;
using UnityEngine.UIElements;

namespace Lightbringer.UI
{
    // Destination briefing and the four-slot skill loadout. Inspecting a skill never mutates the saved loadout.
    // Two columns: the mission briefing at left, the hero's skill equipment at right.
    public sealed class PreparationView
    {
        private static readonly string[] SlotKeys = SkillCatalog.SlotKeys;
        private static readonly string[] Effects = SkillCatalog.Descriptions;
        public readonly VisualElement Root;
        public event System.Action BackRequested;
        private readonly Label stageTitle, stageTag, objective, paths, waves, rewards, rewardNote, heroLevel, experience;
        private readonly Label inventoryHint, itemTitle, itemEffect, itemStats, currentItem, shardsLabel, feedback, status, purse, levelBadge;
        private readonly VisualElement enemies, rewardIcon, detailIcon, growth, growthPanel, experienceFill, landscape;
        private readonly Tile[] slots = new Tile[SkillCatalog.Slots];
        private readonly Tile[] items = new Tile[SkillCatalog.Count];
        private readonly Button start, equip, upgradeSkill, growthToggle;
        private readonly UnitUpgradeView upgradeView;
        private CampaignSession session;
        private string revision;
        private bool growthOpen;
        private int selectedItem = -1;
        private struct Tile { public VisualElement Root, Icon; public Label Main, Note; }

        public PreparationView(VisualElement parent)
        {
            Root = Hud.Box(parent, "lb-brief");
            Hud.IgnorePicking(Hud.Box(Root, "lb-brief-background"));
            Hud.IgnorePicking(Hud.Box(Root, "lb-brief-vignette"));
            // Header: the way back sits top-left, the light shard purse top-right.
            VisualElement header = Hud.Box(Root, "lb-brief-header");
            Button back = Hud.Button(header, "‹  지도", () => BackRequested?.Invoke(), "lb-button", "lb-button--back");
            back.tooltip = "선택한 봉화를 유지한 채 지도로 돌아갑니다.";
            VisualElement heading = Hud.Box(header, "lb-brief-heading-block");
            Hud.Text(heading, "출전 준비", "lb-brief-eyebrow");
            Hud.Text(heading, "전장으로 향하기 전에", "lb-brief-title");
            VisualElement actions = Hud.Box(header, "lb-brief-actions");
            VisualElement wallet = Hud.Box(actions, "lb-upg-purse");
            Hud.Box(wallet, "lb-coin");
            purse = Hud.Text(wallet, "0", "lb-upg-gold");
            wallet.tooltip = "보유한 빛의 조각 · 기술과 병력 강화에 사용합니다.";

            VisualElement body = Hud.Box(Root, "lb-brief-body");

            // Mission briefing.
            ScrollView mission = Scroll(body, "lb-brief-mission");
            landscape = Hud.Box(mission.contentContainer, "lb-brief-landscape");
            Hud.Text(landscape, "목적지", "lb-brief-eyebrow");
            stageTitle = Hud.Text(landscape, "", "lb-brief-destination");
            stageTag = Hud.Text(landscape, "", "lb-brief-tag");
            Section(mission.contentContainer, "전투 목표");
            objective = Hud.Text(mission.contentContainer, "", "lb-brief-objective");
            VisualElement metrics = Hud.Box(mission.contentContainer, "lb-brief-metrics");
            paths = Metric(metrics, "진격로"); waves = Metric(metrics, "적 공세");
            Section(mission.contentContainer, "등장하는 적");
            enemies = Hud.Box(mission.contentContainer, "lb-brief-enemies");
            Section(mission.contentContainer, "최초 승리 보상");
            VisualElement reward = Hud.Box(mission.contentContainer, "lb-brief-reward");
            rewardIcon = Hud.Box(reward, "lb-icon");
            VisualElement rewardText = Hud.Box(reward, "lb-brief-copy");
            rewards = Hud.Text(rewardText, "", "lb-brief-item-name");
            rewardNote = Hud.Text(rewardText, "", "lb-brief-muted");

            // Hero equipment: level, the four equipped skills, then the skill library with its inspector.
            ScrollView armory = Scroll(body, "lb-brief-armory");
            VisualElement identity = Hud.Box(armory.contentContainer, "lb-brief-identity");
            VisualElement badge = Hud.Box(identity, "lb-brief-level");
            levelBadge = Hud.Text(badge, "", "lb-brief-level-value");
            VisualElement heroText = Hud.Box(identity, "lb-brief-copy");
            heroLevel = Hud.Text(heroText, "", "lb-brief-hero-name");
            VisualElement track = Hud.Box(heroText, "lb-brief-xp");
            experienceFill = Hud.Box(track, "lb-brief-xp-fill");
            experience = Hud.Text(heroText, "", "lb-brief-muted");
            growthToggle = Hud.Button(identity, "영구 성장", ToggleGrowth, "lb-button", "lb-button--small");

            Section(armory.contentContainer, "장착 기술");
            VisualElement socketRow = Hud.Box(armory.contentContainer, "lb-brief-slots");
            for (int i = 0; i < slots.Length; i++)
            {
                int slot = i;
                Button button = Hud.Button(socketRow, "", () => SelectSlot(slot), "lb-brief-slot");
                VisualElement socket = Hud.Box(button, "lb-brief-socket");
                VisualElement icon = Hud.Box(socket, "lb-icon");
                Hud.Box(socket, "lb-brief-socket-ring");
                // A fixed slot whose skill is not learned yet shows a sealed socket marked "?".
                Hud.Text(socket, "?", "lb-brief-socket-seal");
                VisualElement text = Hud.Box(button, "lb-brief-copy");
                Hud.Text(text, SlotKeys[i], "lb-brief-key");
                slots[i] = new Tile { Root = button, Icon = icon,
                    Main = Hud.Text(text, "", "lb-brief-item-name"), Note = Hud.Text(text, "", "lb-brief-muted") };
                Decorate(button);
            }

            Section(armory.contentContainer, "기술 보관함");
            inventoryHint = Hud.Text(armory.contentContainer, "", "lb-brief-inventory-hint");
            VisualElement library = Hud.Box(armory.contentContainer, "lb-brief-library");
            VisualElement inventory = Hud.Box(library, "lb-brief-inventory");
            for (int i = 0; i < items.Length; i++)
            {
                int id = i;
                Button button = Hud.Button(inventory, "", () => SelectItem(id), "lb-brief-item");
                VisualElement icon = Hud.Box(button, "lb-icon", "lb-icon--" + i);
                VisualElement text = Hud.Box(button, "lb-brief-copy");
                items[i] = new Tile { Root = button, Icon = icon,
                    Main = Hud.Text(text, SkillCatalog.Names[i], "lb-brief-item-name"),
                    Note = Hud.Text(text, "", "lb-brief-muted") };
                button.tooltip = Effects[i]; Decorate(button);
            }
            VisualElement detail = Hud.Box(library, "lb-brief-inspect");
            VisualElement detailTop = Hud.Box(detail, "lb-brief-inspect-top");
            VisualElement detailSocket = Hud.Box(detailTop, "lb-brief-detail-socket");
            detailIcon = Hud.Box(detailSocket, "lb-icon", "lb-brief-detail-icon");
            VisualElement detailText = Hud.Box(detailTop, "lb-brief-copy");
            currentItem = Hud.Text(detailText, "", "lb-brief-muted");
            itemTitle = Hud.Text(detailText, "", "lb-brief-item-title");
            itemStats = Hud.Text(detail, "", "lb-brief-item-stats");
            itemEffect = Hud.Text(detail, "", "lb-brief-effect");
            shardsLabel = Hud.Text(detail, "", "lb-brief-shards");
            VisualElement buttons = Hud.Box(detail, "lb-brief-inspect-actions");
            equip = Hud.Button(buttons, "", () => EquipSelected(), "lb-button", "lb-brief-equip");
            upgradeSkill = Hud.Button(buttons, "", () => UpgradeSelected(), "lb-button", "lb-button--gold", "lb-brief-equip");
            feedback = Hud.Text(armory.contentContainer, "", "lb-brief-feedback");

            VisualElement footer = Hud.Box(Root, "lb-brief-footer");
            status = Hud.Text(footer, "", "lb-brief-muted", "lb-brief-status");
            start = Hud.Button(footer, "", () => OpenUpgrade(), "lb-atlas-primary", "lb-brief-start");

            growthPanel = Hud.Box(Root, "lb-brief-growth-overlay");
            VisualElement growthDialog = Hud.Box(growthPanel, "lb-brief-growth-dialog");
            VisualElement growthHeader = Hud.Box(growthDialog, "lb-brief-heading-row");
            Hud.Text(growthHeader, "영구 성장", "lb-brief-heading");
            Hud.Button(growthHeader, "닫기", ToggleGrowth, "lb-button", "lb-button--small");
            Hud.Text(growthDialog, "전투 중 선택한 성장 효과는 다음 전투에서도 유지됩니다.", "lb-brief-muted");
            Hud.Box(growthDialog, "lb-rule");
            growth = Scroll(growthDialog, "lb-brief-growth-list").contentContainer;
            Root.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Escape) return;
                if (upgradeView.IsOpen) upgradeView.Close(); else if (growthOpen) ToggleGrowth(); else BackRequested?.Invoke();
                evt.StopPropagation();
            });
            Root.RegisterCallback<FocusInEvent>(evt =>
            {
                if (growthOpen && evt.target is VisualElement target && !growthPanel.Contains(target))
                    growthPanel.Q<Button>().Focus();
            });
            upgradeView = new UnitUpgradeView(Root);
            upgradeView.Closed += () => { if (session != null) Refresh(session); start.Focus(); };
            upgradeView.StartRequested += () => Start();
            upgradeView.Upgraded += () => { if (session != null) Refresh(session); };
            Hud.Show(growthPanel, false); Hud.Show(Root, false);
        }

        public int ActiveSlot { get; private set; } = SkillCatalog.FirstFreeSlot;
        public int SelectedItem => selectedItem;
        public bool Visible => !Root.ClassListContains("lb-hidden");
        public string StartText => start.text;
        public int TroopCount => session?.Progress?.UnlockedUnits ?? 0;
        public string SlotText(int slot) => slots[slot].Main.text;
        public bool IsItemEnabled(int id) => items[id].Root.enabledSelf;
        public string ItemNote(int id) => items[id].Note.text;
        public string DestinationText => stageTitle.text;

        private static ScrollView Scroll(VisualElement parent, string className)
        {
            ScrollView scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList(className);
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
            parent.Add(scroll); return scroll;
        }
        private static void Section(VisualElement parent, string title)
        {
            VisualElement row = Hud.Box(parent, "lb-brief-section-row");
            Hud.Box(row, "lb-brief-section-mark");
            Hud.Text(row, title, "lb-brief-section");
            Hud.Box(row, "lb-brief-section-line");
        }
        private static Label Metric(VisualElement parent, string title)
        {
            VisualElement box = Hud.Box(parent, "lb-brief-metric");
            Hud.Text(box, title, "lb-brief-muted");
            return Hud.Text(box, "", "lb-brief-metric-value");
        }
        private static void Decorate(VisualElement button)
        { foreach (VisualElement child in button.Children()) Hud.IgnorePicking(child); }
        private static void Icon(VisualElement icon, int id)
        { for (int k = 0; k < SkillCatalog.Count; k++) icon.EnableInClassList("lb-icon--" + k, k == id); }

        public void Refresh(CampaignSession value, bool showPreparation = true)
        {
            session = value;
            CampaignProgress profile = session != null ? session.Progress : null;
            bool visible = showPreparation && profile != null && session.Battle == null;
            bool opening = visible && !Visible;
            Hud.Show(Root, visible);
            if (!visible) { growthOpen = false; Hud.Show(growthPanel, false); Hud.Show(upgradeView.Root, false); return; }
            if (opening) { Hud.Set(feedback, ""); slots[ActiveSlot].Root.Focus(); }
            if (!SkillCatalog.IsSkill(selectedItem)) selectedItem = DefaultSelection(profile, ActiveSlot);
            start.SetEnabled(!session.IsChoosing);
            Hud.Set(start, $"봉화 {session.SelectedStage:00} · 병력 준비  ›");
            string message = session.SaveStatus;
            Hud.Set(status, !string.IsNullOrEmpty(message) && message != "검증 세션" ? message : "영웅 기술을 정비한 뒤 병력 준비로 넘어갑니다.");
            string key = session.SelectedStage + "|" + profile.unlockedStage + "|" + profile.level + "|" + profile.experience
                + "|" + ActiveSlot + "|" + selectedItem + "|" + string.Join(",", profile.cleared)
                + "|" + string.Join(",", profile.skillLevels) + "|" + string.Join(",", profile.loadout) + "|" + string.Join(",", profile.ranks)
                + "|" + profile.gold + "|" + string.Join(",", profile.unitRanks);
            if (key == revision) return;
            revision = key;
            int stage = session.SelectedStage;
            bool cleared = profile.cleared[stage - 1], boss = EnemyCatalog.HasBoss(stage);
            int act = Mathf.Clamp((stage - 1) / 8 + 1, 1, 3);
            for (int i = 1; i <= 3; i++) Root.EnableInClassList("lb-brief--act" + i, i == act);
            landscape.EnableInClassList("lb-brief-landscape--cleared", cleared);
            Hud.Set(stageTitle, CampaignStages.Name(stage));
            bool finale = EnemyCatalog.IsFinale(stage);
            Hud.Set(objective, finale ? "공세를 격퇴하고 적 본진을 파괴하세요.\n어둠의 군주와 마왕(어둠의 핵)을 모두 쓰러뜨리세요."
                : boss ? "공세를 격퇴하고 적 본진을 파괴하세요.\n등장한 어둠의 군주를 격퇴하세요." : "적군 본진을 파괴하고\n봉화에 다시 빛을 밝혀주세요.");
            Hud.Set(stageTag, cleared ? "해방한 전장 · 재도전" : finale ? "미해방 지역 · 최종 전투" : boss ? "미해방 지역 · 보스 전투" : "미해방 지역 · 탈환 작전");
            Hud.Set(paths, PrototypeBattleBuilder.PathCount(stage) + "개");
            Hud.Set(waves, EnemyWaveSpawner.WaveCount(stage) + "회");
            enemies.Clear();
            for (int i = 0; i < EnemyCatalog.Count; i++)
            {
                if (!EnemyCatalog.AppearsIn((EnemyKind)i, stage)) continue;
                Hud.Text(enemies, EnemyCatalog.Names[i], "lb-brief-enemy");
            }
            if (finale) Hud.Text(enemies, "마왕", "lb-brief-enemy");
            int reward = SkillCatalog.RewardFor(stage);
            bool teaches = SkillCatalog.IsSkill(reward);
            Icon(rewardIcon, reward);
            rewardIcon.EnableInClassList("lb-icon--shard", !teaches);
            Hud.Set(rewards, teaches ? "새 기술 · " + SkillCatalog.Names[reward] : $"빛의 조각 +{100 + 50 * stage}");
            Hud.Set(rewardNote, cleared ? "획득 완료 · 재도전 시 전투 경험치와 빛의 조각 획득"
                : stage < UnitCatalog.Count ? $"다음 봉화와 {UnitCatalog.Names[stage]} 해금"
                : stage < CampaignProgress.StageCount ? "다음 봉화로 가는 길 해금" : "최종 전투 · 캠페인 완료");
            Hud.Set(heroLevel, "마법 지휘관");
            Hud.Set(levelBadge, profile.level.ToString());
            Hud.Set(experience, profile.IsMaxLevel ? "최대 레벨" : $"경험치 {profile.experience} / {profile.ExperienceToNext}");
            Hud.Fraction(experienceFill, profile.IsMaxLevel ? 1f : profile.experience / (float)Mathf.Max(1, profile.ExperienceToNext));
            Hud.Set(purse, profile.gold.ToString("N0"));
            Hud.Set(inventoryHint, $"배운 기술  ·  {SlotKeys[ActiveSlot]} 슬롯에 사용할 기술을 확인하세요");
            for (int i = 0; i < slots.Length; i++)
            {
                int id = profile.loadout[i]; Icon(slots[i].Icon, id);
                bool fixedSlot = SkillCatalog.IsFixedSlot(i), sealedSlot = fixedSlot && !SkillCatalog.IsSkill(id);
                Hud.Set(slots[i].Main, SkillCatalog.IsSkill(id) ? SkillCatalog.Names[id] : sealedSlot ? "???" : "비어 있음");
                Hud.Set(slots[i].Note, sealedSlot ? "미습득 기술" : fixedSlot ? $"고정 · Lv {profile.skillLevels[id]} · {SkillCatalog.Cost(id):0} MP"
                    : SkillCatalog.IsSkill(id) ? $"Lv {profile.skillLevels[id]} · {SkillCatalog.Cost(id):0} MP" : "배운 기술을 넣으세요");
                slots[i].Root.SetEnabled(!fixedSlot);
                slots[i].Root.EnableInClassList("lb-brief-slot--active", i == ActiveSlot);
                slots[i].Root.EnableInClassList("lb-brief-slot--empty", !SkillCatalog.IsSkill(id));
                slots[i].Root.EnableInClassList("lb-brief-slot--sealed", sealedSlot);
            }
            for (int id = 0; id < items.Length; id++)
            {
                int owner = System.Array.IndexOf(profile.loadout, id);
                bool owned = profile.skillLevels[id] > 0;
                bool fixedSkill = SkillCatalog.IsFixedSkill(id);
                Hud.Set(items[id].Note, !owned ? "미습득" : fixedSkill ? $"Lv {profile.skillLevels[id]} · {SlotKeys[SkillCatalog.FixedSlotOf(id)]} 고정"
                    : owner >= 0 ? $"Lv {profile.skillLevels[id]} · {SlotKeys[owner]} 장착 중" : $"Lv {profile.skillLevels[id]} · 장착 가능");
                // Fixed skills stay inspectable so they can be upgraded, though they never move slots.
                items[id].Root.SetEnabled(owned && (fixedSkill || owner < 0 || owner == ActiveSlot));
                items[id].Root.EnableInClassList("lb-brief-item--selected", id == selectedItem);
                items[id].Root.EnableInClassList("lb-brief-item--equipped", owner >= 0);
            }
            RefreshItem(profile);
            growth.Clear();
            Hud.Text(growth, $"오라 반경 {GrowthCatalog.AuraRadiusAt(profile.level):0.0}m  (레벨에 따라 증가)", "lb-brief-growth-row");
            for (int i = 0; i < profile.ranks.Length; i++)
                if (profile.ranks[i] > 0)
                    Hud.Text(growth, $"{GrowthCatalog.Effects[i]}  × {profile.ranks[i]} / {GrowthCatalog.MaxRank}", "lb-brief-growth-row");
            if (growth.childCount == 1) Hud.Text(growth, "아직 선택한 성장 효과가 없습니다.\n전투 중 레벨을 올려 첫 효과를 선택하세요.", "lb-brief-growth-row");
        }

        private void RefreshItem(CampaignProgress profile)
        {
            int id = selectedItem, worn = profile.loadout[ActiveSlot], level = profile.skillLevels[id];
            Icon(detailIcon, id);
            Hud.Set(currentItem, SkillCatalog.IsSkill(worn)
                ? $"현재 {SlotKeys[ActiveSlot]} 기술 · {SkillCatalog.Names[worn]} Lv {profile.skillLevels[worn]}"
                : $"현재 {SlotKeys[ActiveSlot]} 슬롯은 비어 있습니다");
            Hud.Set(itemTitle, SkillCatalog.Names[id] + " Lv " + level);
            Hud.Set(itemEffect, Effects[id]);
            Hud.Set(itemStats, $"소모 {SkillCatalog.Cost(id):0} MP   ·   재사용 {SkillCatalog.Cooldown(id):0.#}초   ·   위력 ×{SkillCatalog.Power(level):0.0#}");
            Hud.Set(shardsLabel, $"보유 빛의 조각  {profile.gold:N0}");
            int owner = System.Array.IndexOf(profile.loadout, id);
            bool fixedSkill = SkillCatalog.IsFixedSkill(id);
            Hud.Set(equip, fixedSkill ? SlotKeys[SkillCatalog.FixedSlotOf(id)] + " 고정" : owner == ActiveSlot ? "슬롯 비우기"
                : owner >= 0 ? SlotKeys[owner] + " 장착 중" : SlotKeys[ActiveSlot] + " 슬롯에 장착");
            equip.SetEnabled(!fixedSkill && level > 0 && (owner < 0 || owner == ActiveSlot));
            bool maxed = level >= SkillCatalog.MaxLevel;
            Hud.Set(upgradeSkill, level <= 0 ? "미습득" : maxed ? "최대 레벨"
                : profile.CanUpgradeSkill(id) ? $"강화  {SkillCatalog.UpgradeCost(id, level):N0}" : $"빛의 조각 부족  {SkillCatalog.UpgradeCost(id, level):N0}");
            upgradeSkill.SetEnabled(profile.CanUpgradeSkill(id));
        }

        // The skill in the slot, else the first learned one, so the inspector always has something to show.
        private static int DefaultSelection(CampaignProgress profile, int slot)
        {
            if (SkillCatalog.IsSkill(profile.loadout[slot])) return profile.loadout[slot];
            for (int i = 0; i < profile.skillLevels.Length; i++) if (profile.skillLevels[i] > 0) return i;
            return (int)SkillKind.LightBeam;
        }
        private void ToggleGrowth()
        {
            growthOpen = !growthOpen; Hud.Show(growthPanel, growthOpen);
            if (growthOpen) growthPanel.Q<Button>().Focus(); else growthToggle.Focus();
        }
        public void SelectSlot(int slot)
        {
            if (slot < 0 || slot >= slots.Length || SkillCatalog.IsFixedSlot(slot)) return;
            ActiveSlot = slot;
            selectedItem = session?.Progress != null ? DefaultSelection(session.Progress, slot) : -1;
            Hud.Set(feedback, ""); if (session != null) Refresh(session);
        }
        public bool SelectItem(int id)
        {
            if (session?.Progress == null || id < 0 || id >= items.Length || !IsItemEnabled(id)) return false;
            selectedItem = id; Hud.Set(feedback, ""); Refresh(session); return true;
        }
        // Equips the inspected skill, or empties the slot when that skill already sits in it.
        public bool EquipSelected() =>
            session?.Progress != null && session.Progress.loadout[ActiveSlot] == selectedItem ? Unequip() : Equip(selectedItem);
        public bool Equip(int id)
        {
            bool equipped = session != null && session.TryEquip(ActiveSlot, id);
            if (equipped) { selectedItem = id; Hud.Set(feedback, SkillCatalog.Names[id] + " 장착 완료"); }
            if (session != null) Refresh(session);
            return equipped;
        }
        public bool Unequip()
        {
            bool cleared = session != null && session.TryEquip(ActiveSlot, SkillCatalog.Empty);
            if (cleared) Hud.Set(feedback, SlotKeys[ActiveSlot] + " 슬롯을 비웠습니다");
            if (session != null) Refresh(session);
            return cleared;
        }
        public bool UpgradeSelected()
        {
            bool upgraded = session != null && session.TryUpgradeSkill(selectedItem);
            if (upgraded) Hud.Set(feedback, $"{SkillCatalog.Names[selectedItem]} Lv {session.Progress.skillLevels[selectedItem]} 강화 완료");
            if (session != null) Refresh(session);
            return upgraded;
        }
        public bool Start() => session != null && session.StartBattle();
        public bool OpenUpgrade()
        {
            if (session?.Progress == null || !Visible || growthOpen) return false;
            upgradeView.Open(session); return true;
        }
    }
}

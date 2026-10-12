using Lightbringer.Core;
using Lightbringer.Player;
using Lightbringer.Progression;
using Lightbringer.Units;
using UnityEngine;
using UnityEngine.UIElements;

namespace Lightbringer.UI
{
    // Destination briefing and three-slot loadout. Inspecting an item never mutates the saved loadout.
    public sealed class PreparationView
    {
        private static readonly string[] SlotKeys = { "LMB", "Q", "E" };
        private static readonly string[] Roles = { "근접 전투", "원거리 지원", "전선 유지", "중장갑 대응", "아군 회복", "광역 마법", "돌격", "최상위 병력" };
        private static readonly string[] Effects = {
            "조준한 적에게 빛을 내려 단일 대상을 공격합니다.", "영웅 주변 6m 안의 아군을 회복합니다.",
            "조준한 적을 중심으로 3m 안의 적을 공격합니다.", "장착 중 식량 생성 속도가 증가합니다.",
            "장착 중 최대 마나와 마나 회복 속도가 증가합니다.", "장착 중 영웅의 최대 체력이 증가합니다."
        };
        public readonly VisualElement Root;
        public event System.Action BackRequested;
        private readonly Label stageTitle, stageTag, objective, paths, waves, rewards, rewardNote, heroLevel, experience;
        private readonly Label inventoryHint, itemTitle, itemEffect, itemStats, currentItem, feedback, status, troopSummary;
        private readonly VisualElement enemies, rewardIcon, detailIcon, growth, growthPanel;
        private readonly Tile[] slots = new Tile[EquipmentCatalog.Slots];
        private readonly Tile[] items = new Tile[EquipmentCatalog.Count];
        private readonly Tile[] troops = new Tile[UnitCatalog.Count];
        private readonly Button start, equip, growthToggle;
        private CampaignSession session;
        private string revision;
        private bool growthOpen;
        private int selectedItem = -1;
        private struct Tile { public VisualElement Root, Icon; public Label Main, Note; }

        public PreparationView(VisualElement parent)
        {
            Root = Hud.Box(parent, "lb-brief");
            Hud.IgnorePicking(Hud.Box(Root, "lb-brief-background"));
            VisualElement header = Hud.Box(Root, "lb-brief-header");
            VisualElement heading = Hud.Box(header);
            Hud.Text(heading, "LIGHTBRINGER  /  출전 준비", "lb-brief-eyebrow");
            Hud.Text(heading, "전장으로 향하기 전에", "lb-brief-title");
            Button back = MakeButton(header, "← 지도로 돌아가기", () => BackRequested?.Invoke(), "lb-brief-back");
            back.tooltip = "선택한 봉화를 유지한 채 지도로 돌아갑니다.";

            VisualElement body = Hud.Box(Root, "lb-brief-body");
            ScrollView mission = Scroll(body, "lb-brief-mission");
            VisualElement landscape = Hud.Box(mission.contentContainer, "lb-brief-landscape");
            Hud.Text(landscape, "선택한 목적지", "lb-brief-eyebrow");
            stageTitle = Hud.Text(landscape, "", "lb-brief-destination");
            stageTag = Hud.Text(landscape, "", "lb-brief-tag");
            Hud.Text(mission.contentContainer, "전투 목표", "lb-brief-section");
            objective = Hud.Text(mission.contentContainer, "", "lb-brief-objective");
            VisualElement metrics = Hud.Box(mission.contentContainer, "lb-brief-metrics");
            paths = Metric(metrics, "진격로"); waves = Metric(metrics, "적 공세");
            Hud.Text(mission.contentContainer, "등장하는 적", "lb-brief-section");
            enemies = Hud.Box(mission.contentContainer, "lb-brief-enemies");
            Hud.Text(mission.contentContainer, "최초 승리 보상", "lb-brief-section");
            VisualElement reward = Hud.Box(mission.contentContainer, "lb-brief-reward");
            rewardIcon = Hud.Box(reward, "lb-icon");
            VisualElement rewardText = Hud.Box(reward, "lb-brief-copy");
            rewards = Hud.Text(rewardText, "", "lb-brief-item-name");
            rewardNote = Hud.Text(rewardText, "", "lb-brief-muted");

            ScrollView armory = Scroll(body, "lb-brief-armory");
            VisualElement identity = Hud.Box(armory.contentContainer, "lb-brief-identity");
            Hud.Box(identity, "lb-brief-portrait");
            VisualElement heroText = Hud.Box(identity, "lb-brief-copy");
            heroLevel = Hud.Text(heroText, "", "lb-brief-hero-name");
            experience = Hud.Text(heroText, "", "lb-brief-muted");
            growthToggle = MakeButton(identity, "영구 성장 보기", ToggleGrowth, "lb-brief-text-button");
            VisualElement headingRow = Hud.Box(armory.contentContainer, "lb-brief-heading-row");
            Hud.Text(headingRow, "장비 정비", "lb-brief-heading");
            Hud.Text(headingRow, "01  슬롯 선택    02  장비 확인    03  장착", "lb-brief-muted");
            VisualElement socketRow = Hud.Box(armory.contentContainer, "lb-brief-slots");
            for (int i = 0; i < slots.Length; i++)
            {
                int slot = i;
                Button button = MakeButton(socketRow, "", () => SelectSlot(slot), "lb-brief-slot");
                Hud.Text(button, SlotKeys[i], "lb-brief-key");
                VisualElement icon = Hud.Box(button, "lb-icon");
                VisualElement text = Hud.Box(button, "lb-brief-copy");
                slots[i] = new Tile { Root = button, Icon = icon,
                    Main = Hud.Text(text, "", "lb-brief-item-name"), Note = Hud.Text(text, "", "lb-brief-muted") };
                Decorate(button);
            }
            inventoryHint = Hud.Text(armory.contentContainer, "", "lb-brief-inventory-hint");
            VisualElement inventory = Hud.Box(armory.contentContainer, "lb-brief-inventory");
            for (int i = 0; i < items.Length; i++)
            {
                int id = i;
                Button button = MakeButton(inventory, "", () => SelectItem(id), "lb-brief-item");
                VisualElement icon = Hud.Box(button, "lb-icon", "lb-icon--" + i);
                VisualElement text = Hud.Box(button, "lb-brief-copy");
                items[i] = new Tile { Root = button, Icon = icon,
                    Main = Hud.Text(text, EquipmentCatalog.Names[i], "lb-brief-item-name"),
                    Note = Hud.Text(text, "", "lb-brief-muted") };
                button.tooltip = Effects[i]; Decorate(button);
            }
            VisualElement detail = Hud.Box(armory.contentContainer, "lb-brief-inspect");
            detailIcon = Hud.Box(detail, "lb-icon", "lb-brief-detail-icon");
            VisualElement detailText = Hud.Box(detail, "lb-brief-copy");
            currentItem = Hud.Text(detailText, "", "lb-brief-muted");
            itemTitle = Hud.Text(detailText, "", "lb-brief-item-title");
            itemEffect = Hud.Text(detailText, "", "lb-brief-effect");
            itemStats = Hud.Text(detailText, "", "lb-brief-item-stats");
            equip = MakeButton(detail, "", () => EquipSelected(), "lb-brief-equip");
            feedback = Hud.Text(armory.contentContainer, "", "lb-brief-feedback");

            VisualElement roster = Hud.Box(Root, "lb-brief-roster");
            VisualElement rosterHeading = Hud.Box(roster, "lb-brief-roster-heading");
            Hud.Text(rosterHeading, "함께할 병력", "lb-brief-section");
            troopSummary = Hud.Text(rosterHeading, "", "lb-brief-muted");
            VisualElement troopRow = Hud.Box(roster, "lb-brief-troops");
            for (int i = 0; i < troops.Length; i++)
            {
                VisualElement tile = Hud.Box(troopRow, "lb-brief-troop", "lb-brief-unit--" + i);
                Hud.Box(tile, "lb-brief-unit-art");
                VisualElement text = Hud.Box(tile, "lb-brief-copy");
                Label name = Hud.Text(text, UnitCatalog.Names[i], "lb-brief-troop-name");
                Hud.Text(text, Roles[i], "lb-brief-troop-role");
                troops[i] = new Tile { Root = tile, Main = name,
                    Note = Hud.Text(text, "", "lb-brief-troop-cost") };
                tile.tooltip = Roles[i];
            }
            VisualElement footer = Hud.Box(Root, "lb-brief-footer");
            status = Hud.Text(footer, "", "lb-brief-muted");
            start = MakeButton(footer, "", () => Start(), "lb-atlas-primary", "lb-brief-start");

            growthPanel = Hud.Box(Root, "lb-brief-growth-overlay");
            VisualElement growthDialog = Hud.Box(growthPanel, "lb-brief-growth-dialog");
            VisualElement growthHeader = Hud.Box(growthDialog, "lb-brief-heading-row");
            Hud.Text(growthHeader, "영구 성장", "lb-brief-heading");
            MakeButton(growthHeader, "닫기", ToggleGrowth, "lb-brief-text-button");
            Hud.Text(growthDialog, "전투 중 선택한 성장 효과는 다음 전투에서도 유지됩니다.", "lb-brief-muted");
            growth = Scroll(growthDialog, "lb-brief-growth-list").contentContainer;
            Root.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Escape) return;
                if (growthOpen) ToggleGrowth(); else BackRequested?.Invoke();
                evt.StopPropagation();
            });
            Root.RegisterCallback<FocusInEvent>(evt =>
            {
                if (growthOpen && evt.target is VisualElement target && !growthPanel.Contains(target))
                    growthPanel.Q<Button>().Focus();
            });
            Hud.Show(growthPanel, false); Hud.Show(Root, false);
        }

        public int ActiveSlot { get; private set; }
        public int SelectedItem => selectedItem;
        public bool Visible => !Root.ClassListContains("lb-hidden");
        public string StartText => start.text;
        public int TroopCount => session?.Progress?.UnlockedUnits ?? 0;
        public string SlotText(int slot) => slots[slot].Main.text;
        public bool IsItemEnabled(int id) => items[id].Root.enabledSelf;
        public string ItemNote(int id) => items[id].Note.text;
        public string DestinationText => stageTitle.text;

        private static Button MakeButton(VisualElement parent, string text, System.Action click, params string[] classes)
        {
            Button button = new Button(click) { text = text };
            foreach (string name in classes) button.AddToClassList(name);
            parent.Add(button); return button;
        }
        private static ScrollView Scroll(VisualElement parent, string className)
        {
            ScrollView scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList(className);
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
            parent.Add(scroll); return scroll;
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
        { for (int k = 0; k < EquipmentCatalog.Count; k++) icon.EnableInClassList("lb-icon--" + k, k == id); }

        public void Refresh(CampaignSession value, bool showPreparation = true)
        {
            session = value;
            CampaignProgress profile = session != null ? session.Progress : null;
            bool visible = showPreparation && profile != null && session.Battle == null;
            bool opening = visible && !Visible;
            Hud.Show(Root, visible);
            if (!visible) { growthOpen = false; Hud.Show(growthPanel, false); return; }
            if (opening) { Hud.Set(feedback, ""); slots[ActiveSlot].Root.Focus(); }
            if (selectedItem < 0 || selectedItem >= EquipmentCatalog.Count) selectedItem = profile.loadout[ActiveSlot];
            start.SetEnabled(!session.IsChoosing);
            Hud.Set(start, $"봉화 {session.SelectedStage:00} · 출전하기  →");
            string message = session.SaveStatus;
            Hud.Set(status, !string.IsNullOrEmpty(message) && message != "검증 세션" ? message : "장착한 장비와 영구 성장 효과가 전투에 적용됩니다.");
            string key = session.SelectedStage + "|" + profile.unlockedStage + "|" + profile.level + "|" + profile.experience
                + "|" + ActiveSlot + "|" + selectedItem + "|" + string.Join(",", profile.cleared)
                + "|" + string.Join(",", profile.equipmentLevels) + "|" + string.Join(",", profile.loadout) + "|" + string.Join(",", profile.ranks);
            if (key == revision) return;
            revision = key;
            int stage = session.SelectedStage;
            bool cleared = profile.cleared[stage - 1], boss = EnemyCatalog.HasBoss(stage);
            Hud.Set(stageTitle, $"봉화 {stage:00}");
            Hud.Set(stageTag, cleared ? "해방한 전장 · 재도전" : boss ? "미해방 지역 · 보스 전투" : "미해방 지역 · 탈환 작전");
            Hud.Set(objective, boss ? "군주를 처치해 보호막을 해제하고\n적군 본진을 파괴하세요." : "적군 본진을 파괴하고\n봉화에 다시 빛을 밝혀주세요.");
            Hud.Set(paths, PrototypeBattleBuilder.PathCount(stage) + "개");
            Hud.Set(waves, EnemyWaveSpawner.WaveCount(stage) + "회");
            enemies.Clear();
            for (int i = 0; i < EnemyCatalog.Count; i++)
            {
                if (!EnemyCatalog.AppearsIn((EnemyKind)i, stage)) continue;
                Hud.Text(enemies, EnemyCatalog.Names[i], "lb-brief-enemy");
            }
            int reward = CampaignProgress.ClearRewardItem(stage), rank = CampaignProgress.ClearRewardLevel(stage);
            Icon(rewardIcon, reward);
            Hud.Set(rewards, EquipmentCatalog.Names[reward] + " +" + rank);
            Hud.Set(rewardNote, cleared ? "획득 완료 · 재도전 시 전투 경험치 획득"
                : stage < CampaignProgress.StageCount ? $"다음 봉화와 {UnitCatalog.Names[stage]} 해금" : "캠페인의 마지막 전장");
            Hud.Set(heroLevel, "마법 지휘관   ·   LV " + profile.level);
            Hud.Set(experience, $"경험치 {profile.experience} / {profile.ExperienceToNext}");
            Hud.Set(inventoryHint, $"보유 장비  /  {SlotKeys[ActiveSlot]} 슬롯에 사용할 장비를 확인하세요");
            for (int i = 0; i < slots.Length; i++)
            {
                int id = profile.loadout[i]; Icon(slots[i].Icon, id);
                Hud.Set(slots[i].Main, EquipmentCatalog.Names[id]);
                Hud.Set(slots[i].Note, $"+{profile.equipmentLevels[id]} · " + (EquipmentCatalog.IsActive(id) ? $"{EquipmentCatalog.Cost(id):0} MP" : "지속 효과"));
                slots[i].Root.EnableInClassList("lb-brief-slot--active", i == ActiveSlot);
            }
            for (int id = 0; id < items.Length; id++)
            {
                int owner = System.Array.IndexOf(profile.loadout, id);
                bool owned = profile.equipmentLevels[id] > 0;
                Hud.Set(items[id].Note, !owned ? "미해금" : owner >= 0 ? $"+{profile.equipmentLevels[id]} · {SlotKeys[owner]} 장착 중" : $"+{profile.equipmentLevels[id]} · 장착 가능");
                items[id].Root.SetEnabled(owned && (owner < 0 || owner == ActiveSlot));
                items[id].Root.EnableInClassList("lb-brief-item--selected", id == selectedItem);
            }
            RefreshItem(profile);
            float costMultiplier = Mathf.Clamp(1f - profile.ranks[(int)GrowthKind.Leadership] * .025f, .5f, 1f);
            Hud.Set(troopSummary, $"해금 {profile.UnlockedUnits} / {UnitCatalog.Count} · 해금한 병종은 모두 소환할 수 있습니다");
            for (int i = 0; i < troops.Length; i++)
            {
                bool unlocked = i < profile.UnlockedUnits;
                troops[i].Root.EnableInClassList("lb-brief-troop--locked", !unlocked);
                Hud.Set(troops[i].Note, unlocked ? $"식량 {UnitCatalog.Cost((UnitKind)i) * costMultiplier:0.#}" : "미해금");
                troops[i].Root.tooltip = unlocked ? Roles[i] : $"봉화 {i:00} 최초 승리 후 해금";
            }
            growth.Clear();
            for (int i = 0; i < profile.ranks.Length; i++)
                if (profile.ranks[i] > 0) Hud.Text(growth, CampaignProgress.GrowthNames[i] + "  × " + profile.ranks[i], "lb-brief-growth-row");
            if (growth.childCount == 0) Hud.Text(growth, "아직 선택한 성장 효과가 없습니다.\n전투 중 레벨을 올려 첫 효과를 선택하세요.", "lb-brief-growth-row");
        }

        private void RefreshItem(CampaignProgress profile)
        {
            int id = selectedItem, worn = profile.loadout[ActiveSlot], rank = profile.equipmentLevels[id];
            Icon(detailIcon, id);
            Hud.Set(currentItem, $"현재 {SlotKeys[ActiveSlot]} 장비 · {EquipmentCatalog.Names[worn]} +{profile.equipmentLevels[worn]}");
            Hud.Set(itemTitle, EquipmentCatalog.Names[id] + " +" + rank);
            Hud.Set(itemEffect, Effects[id]);
            Hud.Set(itemStats, EquipmentCatalog.IsActive(id)
                ? $"소모 {EquipmentCatalog.Cost(id):0} MP   ·   재사용 {EquipmentCatalog.Cooldown(id):0.#}초"
                : id == (int)EquipmentKind.FoodRing ? $"초당 식량 +{rank}"
                : id == (int)EquipmentKind.ManaRing ? $"최대 MP +{rank * 15}   ·   초당 MP +{rank * 2}" : $"최대 HP +{rank * 30}");
            int owner = System.Array.IndexOf(profile.loadout, id);
            Hud.Set(equip, owner == ActiveSlot ? "현재 장착 중" : owner >= 0 ? SlotKeys[owner] + " 장착 중" : SlotKeys[ActiveSlot] + " 슬롯에 장착");
            equip.SetEnabled(rank > 0 && owner < 0);
        }
        private void ToggleGrowth()
        {
            growthOpen = !growthOpen; Hud.Show(growthPanel, growthOpen);
            if (growthOpen) growthPanel.Q<Button>().Focus(); else growthToggle.Focus();
        }
        public void SelectSlot(int slot)
        {
            if (slot < 0 || slot >= slots.Length) return;
            ActiveSlot = slot;
            selectedItem = session?.Progress != null ? session.Progress.loadout[slot] : -1;
            Hud.Set(feedback, ""); if (session != null) Refresh(session);
        }
        public bool SelectItem(int id)
        {
            if (session?.Progress == null || id < 0 || id >= items.Length || !IsItemEnabled(id)) return false;
            selectedItem = id; Hud.Set(feedback, ""); Refresh(session); return true;
        }
        public bool EquipSelected() => Equip(selectedItem);
        public bool Equip(int id)
        {
            bool equipped = session != null && session.TryEquip(ActiveSlot, id);
            if (equipped) { selectedItem = id; Hud.Set(feedback, EquipmentCatalog.Names[id] + " 장착 완료"); }
            if (session != null) Refresh(session);
            return equipped;
        }
        public bool Start() => session != null && session.StartBattle();
    }
}

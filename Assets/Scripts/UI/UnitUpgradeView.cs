using System;
using Lightbringer.Core;
using Lightbringer.Progression;
using Lightbringer.Units;
using UnityEngine;
using UnityEngine.UIElements;

namespace Lightbringer.UI
{
    // Light shard upgrade screen for the troops. Opens over the preparation screen; nothing here changes a battle in progress.
    public sealed class UnitUpgradeView
    {
        private static readonly string[] Descriptions = {
            "가장 먼저 전선에 서는 기본 병력입니다.\n가격이 저렴해 꾸준히 소환하기 좋습니다.",
            "멀리서 화살을 날려 전선을 지원합니다.\n체력이 낮으니 앞줄 병력 뒤에서 힘을 발휘합니다.",
            "방패로 피해를 크게 줄이며 전선을 지킵니다.\n느리지만 오래 버티는 든든한 벽입니다.",
            "긴 창으로 중장갑 적에게 큰 피해를 줍니다.\n보스와 거대한 적을 상대할 때 유용합니다.",
            "뒤에서 아군을 치유하는 후방 지원 병력입니다.\n공격은 하지 않지만 전선을 오래 유지시킵니다.",
            "광역 마법으로 모여 있는 적을 한꺼번에 공격합니다.\n군집한 적 앞에서 가장 빛납니다.",
            "빠르게 돌격해 충격과 함께 적을 밀쳐냅니다.\n멀리서 달려올수록 첫 일격이 강해집니다.",
            "불을 내뿜어 넓은 범위를 태우는 최상위 병력입니다.\n전장의 흐름을 바꾸는 한 수입니다."
        };
        private static readonly string[] StatNames = { "체력", "공격", "사거리", "속도" };
        public readonly VisualElement Root;
        public event Action Closed;
        public event Action Upgraded, StartRequested;
        private readonly Label gold, title, role, rank, cost, feedback, currentAttack, nextAttack, currentHealth, nextHealth;
        private readonly VisualElement pips, art, costRow;
        private readonly VisualElement[] bars = new VisualElement[StatNames.Length];
        private readonly Tile[] tiles = new Tile[UnitCatalog.Count];
        private readonly Button upgrade, begin;
        private CampaignSession session;
        private int selected;
        private struct Tile { public VisualElement Root; public Label Rank; public Label Note; }

        public bool IsOpen => !Root.ClassListContains("lb-hidden");
        public int Selected => selected;
        public string UpgradeText => upgrade.text;
        public string GoldText => gold.text;

        public UnitUpgradeView(VisualElement parent)
        {
            Root = Hud.Box(parent, "lb-upg");
            Hud.Box(Root, "lb-upg-dim");
            VisualElement dialog = Hud.Box(Root, "lb-upg-dialog");
            Hud.IgnorePicking(Hud.Box(dialog, "lb-upg-inner"));

            VisualElement header = Hud.Box(dialog, "lb-upg-header");
            VisualElement heading = Hud.Box(header);
            Hud.Text(heading, "LIGHTBRINGER", "lb-upg-eyebrow");
            Hud.Text(heading, "병력 준비", "lb-upg-heading");
            VisualElement right = Hud.Box(header, "lb-upg-header-right");
            VisualElement purse = Hud.Box(right, "lb-upg-purse");
            Hud.Box(purse, "lb-coin");
            gold = Hud.Text(purse, "0", "lb-upg-gold");
            Hud.Button(right, "← 영웅 준비", Close, "lb-upg-close");

            VisualElement body = Hud.Box(dialog, "lb-upg-body");
            VisualElement grid = Hud.Box(body, "lb-upg-grid");
            for (int i = 0; i < tiles.Length; i++)
            {
                int unit = i;
                Button tile = Hud.Button(grid, "", () => Select(unit), "lb-upg-tile");
                Hud.Box(tile, "lb-upg-art", "lb-upg-art--" + i);
                Label note = Hud.Text(tile, "", "lb-upg-tile-cost");
                Label count = Hud.Text(tile, "", "lb-upg-tile-rank");
                foreach (VisualElement child in tile.Children()) Hud.IgnorePicking(child);
                tiles[i] = new Tile { Root = tile, Rank = count, Note = note };
            }

            VisualElement panel = Hud.Box(body, "lb-upg-panel");
            VisualElement top = Hud.Box(panel, "lb-upg-panel-top");
            art = Hud.Box(top, "lb-upg-portrait");
            VisualElement identity = Hud.Box(top, "lb-upg-identity");
            title = Hud.Text(identity, "", "lb-upg-name");
            rank = Hud.Text(identity, "", "lb-upg-rank");
            pips = Hud.Box(identity, "lb-upg-pips");
            for (int i = 0; i < UnitCatalog.MaxRank; i++) Hud.Box(pips, "lb-upg-pip");
            VisualElement stats = Hud.Box(panel, "lb-upg-stats");
            VisualElement attack = Hud.Box(stats, "lb-upg-stat");
            Hud.Text(attack, "공격력", "lb-upg-stat-name");
            currentAttack = Hud.Text(attack, "", "lb-upg-stat-now");
            Hud.Text(attack, "→", "lb-upg-arrow");
            nextAttack = Hud.Text(attack, "", "lb-upg-stat-next");
            VisualElement health = Hud.Box(stats, "lb-upg-stat");
            Hud.Text(health, "체력", "lb-upg-stat-name");
            currentHealth = Hud.Text(health, "", "lb-upg-stat-now");
            Hud.Text(health, "→", "lb-upg-arrow");
            nextHealth = Hud.Text(health, "", "lb-upg-stat-next");
            role = Hud.Text(panel, "", "lb-upg-role");
            VisualElement meters = Hud.Box(panel, "lb-upg-meters");
            for (int i = 0; i < StatNames.Length; i++)
            {
                VisualElement row = Hud.Box(meters, "lb-upg-meter");
                Hud.Text(row, StatNames[i], "lb-upg-meter-name");
                VisualElement track = Hud.Box(row, "lb-upg-track");
                bars[i] = Hud.Box(track, "lb-upg-fill");
            }
            VisualElement buy = Hud.Box(panel, "lb-upg-buy");
            costRow = Hud.Box(buy, "lb-upg-cost");
            Hud.Box(costRow, "lb-coin", "lb-coin--large");
            cost = Hud.Text(costRow, "", "lb-upg-cost-value");
            upgrade = Hud.Button(buy, "강화", () => Upgrade(), "lb-atlas-primary", "lb-upg-button");
            feedback = Hud.Text(panel, "", "lb-upg-feedback");

            VisualElement footer = Hud.Box(dialog, "lb-upg-footer");
            Hud.Text(footer, "빛의 조각으로 병력을 강화한 뒤 출전하세요.", "lb-upg-footer-note");
            begin = Hud.Button(footer, "", () => StartRequested?.Invoke(), "lb-atlas-primary", "lb-upg-start");
            Root.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Escape) return;
                Close(); evt.StopPropagation();
            });
            Hud.Show(Root, false);
        }

        public string TileNote(int unit) => tiles[unit].Rank.text;
        public bool IsTileEnabled(int unit) => tiles[unit].Root.enabledSelf;

        public void Open(CampaignSession value)
        {
            session = value;
            if (session?.Progress == null) return;
            if (selected >= session.Progress.UnlockedUnits) selected = 0;
            Hud.Set(feedback, "");
            Hud.Show(Root, true);
            Refresh();
            upgrade.Focus();
        }

        public void Close()
        {
            if (!IsOpen) return;
            Hud.Show(Root, false);
            Closed?.Invoke();
        }

        public bool Select(int unit)
        {
            if (session?.Progress == null || unit < 0 || unit >= UnitCatalog.Count || unit >= session.Progress.UnlockedUnits) return false;
            selected = unit; Hud.Set(feedback, ""); Refresh(); return true;
        }

        public bool Upgrade()
        {
            if (session == null || !session.TryUpgradeUnit(selected)) return false;
            CampaignProgress profile = session.Progress;
            Hud.Set(feedback, $"{UnitCatalog.Names[selected]} {profile.unitRanks[selected]}단계 강화 완료");
            Refresh();
            Upgraded?.Invoke();
            return true;
        }

        public void Refresh()
        {
            CampaignProgress profile = session?.Progress;
            if (profile == null || !IsOpen) return;
            Hud.Set(gold, profile.gold.ToString("N0"));
            Hud.Set(begin, $"봉화 {session.SelectedStage:00} · 출전하기  →");
            float costMultiplier = Lightbringer.Player.HeroAbilities.SummonCostMultiplier(profile.ranks[(int)GrowthKind.Leadership]);
            for (int i = 0; i < tiles.Length; i++)
            {
                bool unlocked = i < profile.UnlockedUnits;
                tiles[i].Root.SetEnabled(unlocked);
                tiles[i].Root.EnableInClassList("lb-upg-tile--selected", i == selected);
                tiles[i].Root.EnableInClassList("lb-upg-tile--locked", !unlocked);
                tiles[i].Root.EnableInClassList("lb-upg-tile--maxed", unlocked && profile.unitRanks[i] >= UnitCatalog.MaxRank);
                Hud.Set(tiles[i].Rank, unlocked ? $"{profile.unitRanks[i]:00}/{UnitCatalog.MaxRank}" : "미해금");
                Hud.Set(tiles[i].Note, unlocked ? (UnitCatalog.Cost((UnitKind)i) * costMultiplier).ToString("0.#") : "");
            }

            UnitKind kind = (UnitKind)selected;
            int current = profile.unitRanks[selected];
            bool maxed = current >= UnitCatalog.MaxRank;
            int next = Mathf.Min(UnitCatalog.MaxRank, current + 1);
            for (int i = 0; i < UnitCatalog.Count; i++) art.EnableInClassList("lb-upg-art--" + i, i == selected);
            Hud.Set(title, UnitCatalog.Names[selected]);
            Hud.Set(rank, $"강화 {current:00} / {UnitCatalog.MaxRank}");
            for (int i = 0; i < pips.childCount; i++) pips[i].EnableInClassList("lb-upg-pip--on", i < current);
            float damage = UnitCatalog.AttackDamageAt(kind, current), nextDamage = UnitCatalog.AttackDamageAt(kind, next);
            float hp = UnitCatalog.MaxHealthAt(kind, current), nextHp = UnitCatalog.MaxHealthAt(kind, next);
            bool fights = UnitCatalog.AttackDamage(kind) > 0f;
            Hud.Set(currentAttack, fights ? damage.ToString("0.#") : "-");
            Hud.Set(nextAttack, !fights ? "-" : maxed ? "MAX" : $"{nextDamage:0.#}  (+{nextDamage - damage:0.#})");
            Hud.Set(currentHealth, hp.ToString("0.#"));
            Hud.Set(nextHealth, maxed ? "MAX" : $"{nextHp:0.#}  (+{nextHp - hp:0.#})");
            Hud.Set(role, Descriptions[selected]);
            Meter(0, hp, MaxOf(k => UnitCatalog.MaxHealthAt(k, UnitCatalog.MaxRank)));
            Meter(1, damage, MaxOf(k => UnitCatalog.AttackDamageAt(k, UnitCatalog.MaxRank)));
            Meter(2, UnitCatalog.Range(kind), MaxOf(UnitCatalog.Range));
            Meter(3, UnitCatalog.Speed(kind), MaxOf(UnitCatalog.Speed));
            Hud.Show(costRow, !maxed);
            Hud.Set(cost, maxed ? "" : UnitCatalog.UpgradeCost(kind, current).ToString("N0"));
            costRow.EnableInClassList("lb-upg-cost--short", !maxed && profile.gold < UnitCatalog.UpgradeCost(kind, current));
            Hud.Set(upgrade, maxed ? "최대 강화" : profile.CanUpgradeUnit(selected) ? "강화" : "빛의 조각 부족");
            upgrade.SetEnabled(profile.CanUpgradeUnit(selected));
        }

        private static float MaxOf(Func<UnitKind, float> value)
        {
            float best = 0.01f;
            for (int i = 0; i < UnitCatalog.Count; i++) best = Mathf.Max(best, value((UnitKind)i));
            return best;
        }

        private void Meter(int index, float value, float max) => Hud.Fraction(bars[index], value / max);
    }
}

using Lightbringer.Core;
using UnityEngine.UIElements;

namespace Lightbringer.UI
{
    // Victory / defeat screen with the playtest summary, a fun rating and a note, then back to preparation.
    public sealed class ResultView
    {
        public readonly VisualElement Root;
        private readonly Label title, result;
        private readonly Label destination;
        private readonly Label[] summary = new Label[3];
        private readonly VisualElement summaryRow;
        private readonly Foldout details;
        private readonly VisualElement stats;
        private readonly Label[] lines = new Label[3];
        private readonly Button[] ratings = new Button[6];
        private readonly TextField note;
        private CampaignSession session;
        private PrototypeBattle shownFor;
        public int Rating { get; private set; }

        public ResultView(VisualElement parent)
        {
            Root = Hud.Box(parent, "lb-overlay", "lb-flow", "lb-result");
            VisualElement dialog = Hud.Box(Root, "lb-flow-dialog");
            destination = Hud.Text(dialog, "", "lb-flow-eyebrow");
            title = Hud.Text(dialog, "", "lb-flow-title");
            result = Hud.Text(dialog, "", "lb-flow-description");
            summaryRow = Hud.Box(dialog, "lb-result-summary");
            string[] captions = { "전투 시간", "아군 소환", "적 처치" };
            for (int i = 0; i < summary.Length; i++)
            {
                VisualElement metric = Hud.Box(summaryRow, "lb-result-metric");
                Hud.Text(metric, captions[i], "lb-flow-muted");
                summary[i] = Hud.Text(metric, "", "lb-result-value");
            }
            details = new Foldout { text = "상세 기록 · 플레이 의견", value = false };
            details.AddToClassList("lb-result-details");
            dialog.Add(details);
            ScrollView scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("lb-result-scroll");
            details.Add(scroll);
            stats = Hud.Box(scroll.contentContainer, "lb-stats");
            Hud.Text(stats, "전투 기록", "lb-caption");
            for (int i = 0; i < lines.Length; i++) lines[i] = Hud.Text(stats, "", "lb-stat");
            Hud.Text(stats, "이번 전투는 얼마나 재미있었나요?", "lb-caption");
            VisualElement row = Hud.Box(stats, "lb-ratings");
            for (int i = 0; i < ratings.Length; i++)
            {
                int value = i;
                ratings[i] = new Button(() => SetRating(value)) { text = i == 0 ? "-" : i.ToString() };
                ratings[i].AddToClassList("lb-rating");
                row.Add(ratings[i]);
            }
            Hud.Text(stats, "재미있거나 지루했던 점, 어렵거나 불편했던 점을 남겨주세요.", "lb-caption");
            note = new TextField { multiline = true, maxLength = 400 };
            note.AddToClassList("lb-note");
            stats.Add(note);
            Hud.Button(dialog, "지도로 돌아가기", () => Return(), "lb-atlas-primary", "lb-flow-primary");
            Hud.Show(Root, false);
        }

        public bool Visible => !Root.ClassListContains("lb-hidden");
        public string Title => title.text;

        public void Refresh(CampaignSession value)
        {
            session = value;
            PrototypeBattle battle = session != null ? session.Battle : null;
            bool visible = battle != null && battle.Objective.HasEnded && !session.IsChoosing;
            Hud.Show(Root, visible);
            if (!visible || shownFor == battle) return;
            // Fill once per finished battle so the rating and note stay editable.
            shownFor = battle;
            bool won = battle.Objective.HasWon;
            Root.EnableInClassList("lb-result--defeat", !won);
            Hud.Set(destination, $"봉화 {session.SelectedStage:00}  /  전투 종료");
            Hud.Set(title, won ? "승리" : "패배");
            title.EnableInClassList("lb-title--defeat", !won);
            Hud.Set(result, session.BattleGold > 0 ? session.LastResult + $"\n획득 골드  +{session.BattleGold:N0}" : session.LastResult);
            PlaytestRecord record = session.PlaytestRecord;
            Hud.Show(stats, record != null);
            Hud.Show(summaryRow, record != null);
            Hud.Show(details, record != null);
            details.value = false;
            if (record != null)
            {
                int seconds = UnityEngine.Mathf.Max(0, UnityEngine.Mathf.RoundToInt(record.durationSeconds));
                Hud.Set(summary[0], $"{seconds / 60:00}:{seconds % 60:00}");
                Hud.Set(summary[1], record.alliesSummoned + "명");
                Hud.Set(summary[2], record.enemiesDefeated + "명");
                float wasted = record.foodProduced > 0f ? 100f * record.foodWasted / record.foodProduced : 0f;
                lines[0].text = $"전투 시간 {record.durationSeconds / 60f:0.0}분   |   아군 소환 {record.alliesSummoned}명 · 전사 {record.alliesLost}명   |   적 처치 {record.enemiesDefeated}명";
                lines[1].text = $"사용 식량 {record.foodSpent:0}   |   최대치 초과 손실 {wasted:0}%   |   영웅 최저 체력 {record.lowestHeroHealthPercent:0}%";
                lines[2].text = $"오라 내 평균 아군 {record.averageAlliesInAura:0.0}명   |   적 본진 남은 체력 {record.enemyBaseHealthPercent:0}%";
            }
            SetRating(0);
            note.value = "";
        }

        public void SetRating(int value)
        {
            Rating = value;
            for (int i = 0; i < ratings.Length; i++) ratings[i].EnableInClassList("lb-rating--on", i == value);
        }

        public bool Return()
        {
            if (session == null) return false;
            session.SetPlaytestFeedback(Rating, note.value);
            return session.ReturnToPreparation();
        }
    }
}

using Lightbringer.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Lightbringer.UI
{
    // Victory / defeat screen with the playtest summary, a fun rating and a note, then back to preparation.
    // Victory shows the result card in slowly turning lamplight; defeat over a dying crimson ember.
    public sealed class ResultView
    {
        private const float IntroSeconds = 0.7f;
        public readonly VisualElement Root;
        private readonly Label title, subtitle, result, reward;
        private readonly Label destination;
        private readonly Label[] summary = new Label[3];
        private readonly VisualElement summaryRow, rewardRow, rays, dialog, banner;
        private readonly Foldout details;
        private readonly VisualElement stats;
        private readonly Label[] lines = new Label[3];
        private readonly Button[] ratings = new Button[6];
        private readonly TextField note;
        private CampaignSession session;
        private PrototypeBattle shownFor;
        private float shownAt;
        public int Rating { get; private set; }

        public ResultView(VisualElement parent)
        {
            Root = Hud.Box(parent, "lb-overlay", "lb-flow", "lb-result");
            rays = Hud.Box(Root, "lb-result-rays");
            Hud.Box(Root, "lb-result-ember");
            VisualElement stack = Hud.Box(Root, "lb-result-stack");
            dialog = Hud.Box(stack, "lb-flow-dialog", "lb-result-dialog");
            destination = Hud.Text(dialog, "", "lb-flow-eyebrow");
            banner = Hud.Box(dialog, "lb-result-banner");
            title = Hud.Text(banner, "", "lb-flow-title");
            subtitle = Hud.Text(dialog, "", "lb-result-subtitle");
            Hud.Box(dialog, "lb-rule");
            result = Hud.Text(dialog, "", "lb-flow-description");
            rewardRow = Hud.Box(dialog, "lb-result-reward");
            Hud.Box(rewardRow, "lb-coin", "lb-coin--large");
            reward = Hud.Text(rewardRow, "", "lb-result-reward-value");
            summaryRow = Hud.Box(dialog, "lb-result-summary");
            string[] captions = { "전투 시간", "아군 소환", "적 처치" };
            string[] icons = { "lb-result-icon--time", "lb-result-icon--ally", "lb-result-icon--enemy" };
            for (int i = 0; i < summary.Length; i++)
            {
                VisualElement metric = Hud.Box(summaryRow, "lb-result-metric");
                Hud.Box(metric, "lb-result-icon", icons[i]);
                summary[i] = Hud.Text(metric, "", "lb-result-value");
                Hud.Text(metric, captions[i], "lb-flow-muted");
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
            Hud.Button(dialog, "지도로 돌아가기  ›", () => Return(), "lb-atlas-primary", "lb-flow-primary");
            Hud.IgnorePicking(rays);
            Hud.Show(Root, false);
        }

        public bool Visible => !Root.ClassListContains("lb-hidden");
        public string Title => title.text;

        public void Refresh(CampaignSession value)
        {
            session = value;
            PrototypeBattle battle = session != null ? session.Battle : null;
            bool visible = battle != null && battle.Objective.HasEnded && !session.IsChoosing
                && (battle.Beacon == null || !battle.Beacon.IsPresenting) && !session.HoldsResult;
            Hud.Show(Root, visible);
            if (visible && shownFor == battle) Animate();
            if (!visible || shownFor == battle) return;
            // Fill once per finished battle so the rating and note stay editable.
            shownFor = battle;
            shownAt = Time.unscaledTime;
            bool won = battle.Objective.HasWon;
            Root.EnableInClassList("lb-result--defeat", !won);
            Root.EnableInClassList("lb-result--victory", won);
            Hud.Set(destination, $"봉화 {session.SelectedStage:00}  ·  전투 종료");
            Hud.Set(title, won ? "승리" : "패배");
            Hud.Set(subtitle, won ? "VICTORY" : "DEFEAT");
            title.EnableInClassList("lb-title--defeat", !won);
            Hud.Set(result, session.LastResult);
            Hud.Show(rewardRow, session.BattleGold > 0);
            Hud.Set(reward, $"빛의 조각  +{session.BattleGold:N0}");
            PlaytestRecord record = session.PlaytestRecord;
            Hud.Show(stats, record != null);
            Hud.Show(summaryRow, record != null);
            Hud.Show(details, record != null);
            details.value = false;
            if (record != null)
            {
                int seconds = Mathf.Max(0, Mathf.RoundToInt(record.durationSeconds));
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
            Animate();
        }

        // Transform and opacity only: the card fades in, the title stamps in, the lamplight keeps turning.
        private void Animate()
        {
            // Edit-mode previews have no running clock: show the settled frame.
            float age = Application.isPlaying ? Time.unscaledTime - shownAt : 10f;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / IntroSeconds));
            float stamp = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((age - 0.15f) / 0.4f));
            dialog.style.opacity = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((age - 0.1f) / 0.45f));
            banner.style.scale = new Scale(Vector2.one * Mathf.Lerp(1.4f, 1f, stamp));
            banner.style.opacity = stamp;
            rays.style.rotate = new Rotate(new Angle(age * 7f % 360f, AngleUnit.Degree));
            rays.style.opacity = t * (0.75f + 0.25f * Mathf.Sin(age * 1.7f));
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

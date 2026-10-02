using Lightbringer.Core;
using UnityEngine.UIElements;

namespace Lightbringer.UI
{
    // Victory / defeat screen with the playtest summary, a fun rating and a note, then back to preparation.
    public sealed class ResultView
    {
        public readonly VisualElement Root;
        private readonly Label title, result;
        private readonly VisualElement stats;
        private readonly Label[] lines = new Label[3];
        private readonly Button[] ratings = new Button[6];
        private readonly TextField note;
        private CampaignSession session;
        private PrototypeBattle shownFor;
        public int Rating { get; private set; }

        public ResultView(VisualElement parent)
        {
            Root = Hud.Box(parent, "lb-overlay");
            VisualElement dialog = Hud.Box(Root, "lb-dialog", "lb-panel");
            title = Hud.Text(dialog, "", "lb-title");
            result = Hud.Text(dialog, "", "lb-subtitle");
            stats = Hud.Box(dialog, "lb-stats");
            Hud.Text(stats, "PLAYTEST", "lb-caption");
            for (int i = 0; i < lines.Length; i++) lines[i] = Hud.Text(stats, "", "lb-stat");
            Hud.Text(stats, "How fun was this stage?", "lb-caption");
            VisualElement row = Hud.Box(stats, "lb-ratings");
            for (int i = 0; i < ratings.Length; i++)
            {
                int value = i;
                ratings[i] = new Button(() => SetRating(value)) { text = i == 0 ? "-" : i.ToString() };
                ratings[i].AddToClassList("lb-rating");
                row.Add(ratings[i]);
            }
            Hud.Text(stats, "Note: what felt fun, slow, confusing or unfair?", "lb-caption");
            note = new TextField { multiline = true, maxLength = 400 };
            note.AddToClassList("lb-note");
            stats.Add(note);
            Button back = new Button(() => Return()) { text = "RETURN TO PREPARATION" };
            back.AddToClassList("lb-primary");
            dialog.Add(back);
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
            Hud.Set(title, won ? "VICTORY" : "DEFEAT");
            title.EnableInClassList("lb-title--defeat", !won);
            Hud.Set(result, session.LastResult);
            PlaytestRecord record = session.PlaytestRecord;
            Hud.Show(stats, record != null);
            if (record != null)
            {
                float wasted = record.foodProduced > 0f ? 100f * record.foodWasted / record.foodProduced : 0f;
                lines[0].text = $"{record.durationSeconds / 60f:0.0} min   |   Summoned {record.alliesSummoned}, lost {record.alliesLost}   |   Enemies defeated {record.enemiesDefeated}";
                lines[1].text = $"Food spent {record.foodSpent:0}   |   Wasted at cap {wasted:0}%   |   Lowest hero HP {record.lowestHeroHealthPercent:0}%";
                lines[2].text = $"Allies in aura (avg) {record.averageAlliesInAura:0.0}   |   Enemy base left {record.enemyBaseHealthPercent:0}%";
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

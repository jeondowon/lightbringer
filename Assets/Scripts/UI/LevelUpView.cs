using Lightbringer.Core;
using Lightbringer.Progression;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Lightbringer.UI
{
    // Permanent level-up: a full-screen overlay with three large blessing cards. Click a card or press 1 / 2 / 3 to
    // select it, then CONFIRM (or Enter) to take it.
    // Shown whenever choices are pending (the battle is paused). Art comes from Assets/UI/Art (Build Preparation Art).
    public sealed class LevelUpView
    {
        private const int CardCount = 3;
        // Number keys summon troops in battle; ignore them briefly so a held summon key cannot pick a card by accident.
        private const float KeyDelay = 0.45f;
        private const string SelectHint = "카드를 클릭하거나 1 · 2 · 3 키를 누르세요";
        private const string ConfirmHint = "확인 버튼 또는 Enter 키로 선택을 확정하세요";
        private static readonly string[] Categories = { "aura", "supply", "magic", "command", "hero" };

        public readonly VisualElement Root;
        private readonly Label subtitle, remaining, hint;
        private readonly Button confirm;
        private readonly Card[] cards = new Card[CardCount];
        private CampaignSession session;
        private string shownChoices = "";
        private float shownAt;
        private int selected = -1;

        private struct Card
        {
            public VisualElement Slot, Icon;
            public Button Button;
            public Label Category, Title, Description, Effect, Rank, Total;
        }

        public LevelUpView(VisualElement parent)
        {
            Root = Hud.Box(parent, "lb-levelup");
            Hud.Box(Root, "lb-levelup-veil");

            VisualElement header = Hud.Box(Root, "lb-levelup-header");
            VisualElement titleRow = Hud.Box(header, "lb-levelup-title-row");
            Hud.Text(titleRow, "더 강해진 빛", "lb-display", "lb-levelup-title");
            Hud.Box(header, "lb-rule", "lb-levelup-rule");
            subtitle = Hud.Text(header, "", "lb-levelup-subtitle");
            remaining = Hud.Text(header, "", "lb-levelup-remaining");

            VisualElement row = Hud.Box(Root, "lb-lvcards");
            for (int i = 0; i < CardCount; i++)
            {
                int index = i;
                Card card = new Card { Slot = Hud.Box(row, "lb-lvslot") };
                card.Button = Hud.Button(card.Slot, "", () => Select(index), "lb-lvcard");
                VisualElement medal = Hud.Box(card.Button, "lb-lvcard-medal");
                Hud.Text(medal, (i + 1).ToString(), "lb-display", "lb-lvcard-number");
                card.Category = Hud.Text(card.Button, "", "lb-lvcard-category");
                VisualElement well = Hud.Box(card.Button, "lb-lvcard-well");
                card.Icon = Hud.Box(well, "lb-growth-icon");
                card.Title = Hud.Text(card.Button, "", "lb-display", "lb-lvcard-title");
                card.Description = Hud.Text(card.Button, "", "lb-lvcard-description");
                card.Effect = Hud.Text(card.Button, "", "lb-display", "lb-lvcard-effect");
                VisualElement footer = Hud.Box(card.Button, "lb-lvcard-footer");
                card.Rank = Hud.Text(footer, "", "lb-lvcard-rank");
                card.Total = Hud.Text(footer, "", "lb-lvcard-total");
                foreach (VisualElement child in card.Button.Children()) Hud.IgnorePicking(child);
                cards[i] = card;
            }

            VisualElement prompt = Hud.Box(Root, "lb-levelup-prompt");
            hint = Hud.Text(prompt, SelectHint, "lb-levelup-prompt-hint");
            Hud.IgnorePicking(header); Hud.IgnorePicking(prompt);
            confirm = Hud.Button(Root, "이 축복 선택하기", () => Confirm(), "lb-atlas-primary", "lb-levelup-confirm");
            SetSelected(-1);
            Hud.Show(Root, false);
        }

        public bool Visible => !Root.ClassListContains("lb-hidden");
        public string ChoiceText(int index) => cards[index].Title.text + "\n" + cards[index].Effect.text;
        public int Selected => selected;

        public void Refresh(CampaignSession value)
        {
            session = value;
            CampaignProgress profile = session != null ? session.Progress : null;
            bool visible = profile != null && session.IsChoosing && profile.choices.Length == CardCount;
            Hud.Show(Root, visible);
            if (!visible) { shownChoices = ""; SetSelected(-1); return; }

            string key = string.Join(",", profile.choices) + "|" + profile.pendingLevels;
            if (key != shownChoices)
            {
                // A new set of cards deals in again.
                shownChoices = key;
                shownAt = Time.unscaledTime;
                int reached = profile.level - profile.pendingLevels + 1;
                Hud.Set(subtitle, $"레벨 {reached} · 영구 적용할 축복을 선택하세요");
                Hud.Set(remaining, profile.pendingLevels > 1 ? $"선택할 축복 {profile.pendingLevels}개 남음" : "");
                Hud.Show(remaining, profile.pendingLevels > 1);
                for (int i = 0; i < CardCount; i++) Fill(cards[i], profile.choices[i], profile.ranks[profile.choices[i]]);
                SetSelected(-1);
            }

            float time = Time.unscaledTime;
            Keyboard keyboard = Keyboard.current;
            if (time - shownAt > KeyDelay && keyboard != null)
            {
                for (int i = 0; i < CardCount; i++)
                    if (keyboard[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame
                        || keyboard[(Key)((int)Key.Numpad1 + i)].wasPressedThisFrame)
                        Select(i);
                if ((keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) && Confirm()) return;
            }
            Animate(time);
        }

        private static void Fill(Card card, int growth, int rank)
        {
            GrowthKind kind = (GrowthKind)growth;
            for (int k = 0; k < CampaignProgress.GrowthCount; k++)
                card.Icon.EnableInClassList("lb-growth-icon--" + (GrowthKind)k, k == growth);
            // The category tints the visible label and effect line.
            string category = GrowthCatalog.Categories[growth].ToLowerInvariant();
            foreach (string name in Categories) card.Button.EnableInClassList("lb-lvcard--" + name, name == category);
            bool maxed = GrowthCatalog.IsMaxed(kind, rank);
            Hud.Set(card.Category, GrowthCatalog.CategoryNames[growth]);
            Hud.Set(card.Title, GrowthCatalog.Titles[growth]);
            Hud.Set(card.Description, GrowthCatalog.Descriptions[growth]);
            Hud.Set(card.Effect, maxed ? "최대 단계 달성" : GrowthCatalog.Effects[growth]);
            // A first rank has no current bonus to compare against.
            Hud.Set(card.Rank, rank == 0 ? $"신규 · 최대 {GrowthCatalog.MaxRank}단계" : $"단계 {rank}  ›  {rank + 1} / {GrowthCatalog.MaxRank}");
            Hud.Set(card.Total, maxed ? GrowthCatalog.Total(kind, rank)
                : rank == 0 ? GrowthCatalog.Total(kind, 1) : $"{GrowthCatalog.Total(kind, rank)}  ›  {GrowthCatalog.Total(kind, rank + 1)}");
        }

        // Transform and opacity only: cards deal in from below one after another.
        private void Animate(float time)
        {
            float age = time - shownAt;
            for (int i = 0; i < CardCount; i++)
            {
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((age - i * 0.09f) / 0.38f));
                cards[i].Slot.style.opacity = t;
                cards[i].Slot.style.translate = new Translate(0f, (1f - t) * 16f);
            }
        }

        // Shows the cards fully dealt, for previews captured outside the Play-mode clock.

        // Marks a card; nothing is applied until Confirm.
        public void Select(int index)
        {
            if (index >= 0 && index < CardCount && Visible) SetSelected(index);
        }

        // Applies the selected card; the selection clears so the next hand starts unselected.
        public bool Confirm()
        {
            if (selected < 0 || !Choose(selected)) return false;
            SetSelected(-1);
            return true;
        }

        private void SetSelected(int index)
        {
            selected = index;
            for (int i = 0; i < CardCount; i++)
            {
                cards[i].Button.EnableInClassList("lb-lvcard--selected", i == index);
                cards[i].Button.EnableInClassList("lb-lvcard--dimmed", index >= 0 && i != index);
            }
            confirm.SetEnabled(index >= 0);
            Hud.Set(hint, index >= 0 ? ConfirmHint : SelectHint);
        }

        // Applies a card immediately. Also used by validation, which has no panel to click in.
        public bool Choose(int index) => session != null && session.ChooseGrowth(index);
    }
}

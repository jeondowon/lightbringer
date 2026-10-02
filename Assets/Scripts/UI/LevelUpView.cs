using System.Text.RegularExpressions;
using Lightbringer.Core;
using Lightbringer.Progression;
using UnityEngine.UIElements;

namespace Lightbringer.UI
{
    // Permanent level-up: three cards, one click. Shown whenever choices are pending (the battle is paused).
    public sealed class LevelUpView
    {
        public readonly VisualElement Root;
        private readonly Label subtitle;
        private readonly VisualElement choices;
        private readonly Button[] buttons = new Button[3];
        private CampaignSession session;
        private string shownChoices = "";

        public LevelUpView(VisualElement parent)
        {
            Root = Hud.Box(parent, "lb-overlay");
            VisualElement dialog = Hud.Box(Root, "lb-dialog", "lb-panel");
            Hud.Text(dialog, "LEVEL UP", "lb-title");
            subtitle = Hud.Text(dialog, "", "lb-subtitle");
            choices = Hud.Box(dialog, "lb-choices");
            for (int i = 0; i < buttons.Length; i++)
            {
                int index = i;
                buttons[i] = new Button(() => Choose(index));
                buttons[i].AddToClassList("lb-choice");
                choices.Add(buttons[i]);
            }
            Hud.Show(Root, false);
        }

        public bool Visible => !Root.ClassListContains("lb-hidden");
        public string ChoiceText(int index) => buttons[index].text;

        public void Refresh(CampaignSession value)
        {
            session = value;
            CampaignProgress profile = session != null ? session.Progress : null;
            bool visible = profile != null && session.IsChoosing && profile.choices.Length == buttons.Length;
            Hud.Show(Root, visible);
            if (!visible) { shownChoices = ""; return; }
            Hud.Set(subtitle, $"Choose one permanent upgrade. It stays with your hero for the whole campaign."
                + (profile.pendingLevels > 1 ? $"\n{profile.pendingLevels} choices remaining." : ""));
            string key = string.Join(",", profile.choices) + "|" + profile.pendingLevels;
            if (key == shownChoices) return;
            shownChoices = key;
            for (int i = 0; i < buttons.Length; i++)
            {
                int growth = profile.choices[i];
                int rank = profile.ranks[growth];
                buttons[i].text = $"{Title((GrowthKind)growth).ToUpperInvariant()}\n\n{CampaignProgress.GrowthNames[growth]}\nRank {rank} > {rank + 1}";
            }
        }

        // Also used by validation, which has no panel to click in.
        public bool Choose(int index) => session != null && session.ChooseGrowth(index);

        private static string Title(GrowthKind kind) => Regex.Replace(kind.ToString(), "(?<!^)([A-Z])", " $1");
    }
}

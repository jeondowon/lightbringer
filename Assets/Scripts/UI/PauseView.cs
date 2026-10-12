using Lightbringer.Core;
using UnityEngine.UIElements;

namespace Lightbringer.UI
{
    // Player pause: shown while the battle is paused with Esc / P. The button, Esc or P resumes.
    public sealed class PauseView
    {
        public readonly VisualElement Root;
        private CampaignSession session;
        private readonly Slider musicVolume;
        private readonly Label volumeValue;

        public PauseView(VisualElement parent)
        {
            Root = Hud.Box(parent, "lb-overlay", "lb-flow", "lb-pause-screen");
            VisualElement dialog = Hud.Box(Root, "lb-flow-dialog", "lb-pause-dialog");
            Hud.Text(dialog, "LIGHTBRINGER  /  전투 중", "lb-flow-eyebrow");
            Hud.Text(dialog, "일시정지", "lb-flow-title");
            Hud.Text(dialog, "잠시 숨을 고르세요. 전투는 멈춰 있습니다.", "lb-flow-description");
            VisualElement volumeHeader = Hud.Box(dialog, "lb-pause-volume-header");
            Hud.Text(volumeHeader, "배경음악", "lb-flow-muted");
            volumeValue = Hud.Text(volumeHeader, "", "lb-flow-muted");
            musicVolume = new Slider("배경음악 음량", 0f, 1f) { value = BattleMusic.Volume };
            musicVolume.AddToClassList("lb-pause-volume");
            musicVolume.RegisterValueChangedCallback(change =>
            {
                BattleMusic.Volume = change.newValue;
                Hud.Set(volumeValue, UnityEngine.Mathf.RoundToInt(change.newValue * 100) + "%");
            });
            dialog.Add(musicVolume);
            Hud.Text(dialog, "WASD  이동     마우스  시점     Tab  소환 진로\n1–8  병력 소환     F  재소환     LMB · Q · E  장비 사용", "lb-pause-controls");
            Foldout credits = new Foldout { text = "음악 크레딧", value = false };
            credits.AddToClassList("lb-pause-credits");
            Hud.Text(credits, BattleMusic.Credit, "lb-flow-muted");
            dialog.Add(credits);
            Button resume = new Button(() => Resume()) { text = "전투 재개" };
            resume.AddToClassList("lb-atlas-primary");
            resume.AddToClassList("lb-flow-primary");
            dialog.Add(resume);
            Hud.Text(dialog, "Esc 또는 P 키로도 돌아갈 수 있습니다", "lb-flow-footnote");
            Hud.Show(Root, false);
        }

        public bool Visible => !Root.ClassListContains("lb-hidden");

        public void Refresh(CampaignSession value)
        {
            session = value;
            bool visible = session != null && session.IsUserPaused;
            if (visible && !Visible)
            {
                musicVolume.SetValueWithoutNotify(BattleMusic.Volume);
                Hud.Set(volumeValue, UnityEngine.Mathf.RoundToInt(BattleMusic.Volume * 100) + "%");
            }
            Hud.Show(Root, visible);
        }

        public bool Resume() => session != null && session.SetPaused(false);
    }
}

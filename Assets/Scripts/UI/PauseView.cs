using Lightbringer.Core;
using UnityEngine.UIElements;

namespace Lightbringer.UI
{
    // Player pause: resume or confirm leaving the current battle while keeping permanent progress.
    // Sound settings at left, the control reference at right, actions along the bottom.
    public sealed class PauseView
    {
        private static readonly (string key, string action)[] Controls = {
            ("WASD", "이동"), ("마우스", "시점"), ("휠", "확대 · 축소"),
            ("1–8", "병력 소환"), ("F", "재소환"), ("Tab", "소환 진로 변경"),
            ("LMB · Q · E · RMB", "기술 사용"), ("Esc · P", "일시정지"),
        };

        public readonly VisualElement Root;
        private CampaignSession session;
        private readonly Slider musicVolume;
        private readonly Label volumeValue;
        private readonly Slider effectsVolume;
        private readonly Label effectsValue;
        private readonly VisualElement menu, endConfirmation;

        public PauseView(VisualElement parent)
        {
            Root = Hud.Box(parent, "lb-overlay", "lb-flow", "lb-pause-screen");
            VisualElement dialog = menu = Hud.Box(Root, "lb-flow-dialog", "lb-pause-dialog");
            Hud.Text(dialog, "LIGHTBRINGER  ·  전투 중", "lb-flow-eyebrow");
            Hud.Text(dialog, "일시정지", "lb-flow-title");
            Hud.Text(dialog, "잠시 숨을 고르세요. 전투는 멈춰 있습니다.", "lb-flow-description");
            Hud.Box(dialog, "lb-rule");

            VisualElement columns = Hud.Box(dialog, "lb-pause-columns");
            VisualElement sound = Hud.Box(columns, "lb-pause-section");
            Hud.Text(sound, "소리", "lb-pause-heading");
            musicVolume = Volume(sound, "배경음악", BattleMusic.Volume, out volumeValue);
            musicVolume.RegisterValueChangedCallback(change =>
            {
                BattleMusic.Volume = change.newValue;
                Hud.Set(volumeValue, UnityEngine.Mathf.RoundToInt(change.newValue * 100) + "%");
            });
            effectsVolume = Volume(sound, "효과음", Audio.SoundEffects.Volume, out effectsValue);
            effectsVolume.RegisterValueChangedCallback(change =>
            {
                Audio.SoundEffects.Volume = change.newValue;
                Hud.Set(effectsValue, UnityEngine.Mathf.RoundToInt(change.newValue * 100) + "%");
                Audio.SoundEffects.UI(Audio.SoundCue.Click);
            });
            Foldout credits = new Foldout { text = "음악 크레딧", value = false };
            credits.AddToClassList("lb-pause-credits");
            Hud.Text(credits, BattleMusic.Credit, "lb-flow-muted");
            sound.Add(credits);

            VisualElement keys = Hud.Box(columns, "lb-pause-section", "lb-pause-section--keys");
            Hud.Text(keys, "조작", "lb-pause-heading");
            foreach ((string key, string action) in Controls)
            {
                VisualElement row = Hud.Box(keys, "lb-pause-key-row");
                VisualElement cap = Hud.Box(row, "lb-keycap", "lb-pause-keycap");
                Hud.Text(cap, key, "lb-keycap-text");
                Hud.Text(row, action, "lb-pause-key-action");
            }

            VisualElement actions = Hud.Box(dialog, "lb-pause-actions");
            Hud.Button(actions, "전투 끝내기", () => RequestEnd(), "lb-pause-end");
            Hud.Button(actions, "전투 재개", () => Resume(), "lb-atlas-primary", "lb-flow-primary");
            Hud.Text(dialog, "Esc 또는 P 키로도 돌아갈 수 있습니다", "lb-flow-footnote");

            endConfirmation = Hud.Box(Root, "lb-flow-dialog", "lb-pause-dialog", "lb-pause-confirm");
            Hud.Text(endConfirmation, "LIGHTBRINGER  ·  전투 종료", "lb-flow-eyebrow");
            Hud.Text(endConfirmation, "전투를 끝낼까요?", "lb-flow-title");
            Hud.Box(endConfirmation, "lb-rule");
            Hud.Text(endConfirmation, "현재 전투를 종료하고 캠페인 지도로 돌아갑니다.\n획득한 경험치와 빛의 조각, 영구 성장은 유지됩니다.\n스테이지 클리어 및 승리 보상은 받을 수 없습니다.", "lb-flow-description");
            VisualElement confirmActions = Hud.Box(endConfirmation, "lb-pause-actions");
            Hud.Button(confirmActions, "전투 끝내기", () => ConfirmEnd(), "lb-pause-end");
            Hud.Button(confirmActions, "취소", () => CancelEnd(), "lb-atlas-primary", "lb-flow-primary");
            Hud.Text(endConfirmation, "Esc 또는 P 키를 누르면 전투를 재개합니다", "lb-flow-footnote");
            CancelEnd();
            Hud.Show(Root, false);
        }

        private static Slider Volume(VisualElement parent, string caption, float value, out Label readout)
        {
            VisualElement header = Hud.Box(parent, "lb-pause-volume-header");
            Hud.Text(header, caption, "lb-pause-volume-name");
            readout = Hud.Text(header, "", "lb-pause-volume-value");
            Slider slider = new Slider(caption + " 음량", 0f, 1f) { value = value };
            slider.AddToClassList("lb-pause-volume");
            parent.Add(slider);
            return slider;
        }

        public bool Visible => !Root.ClassListContains("lb-hidden");
        public bool IsConfirmingEnd => !endConfirmation.ClassListContains("lb-hidden");

        public void Refresh(CampaignSession value)
        {
            session = value;
            bool visible = session != null && session.IsUserPaused;
            if (!visible || !Visible) CancelEnd();
            if (visible && !Visible)
            {
                musicVolume.SetValueWithoutNotify(BattleMusic.Volume);
                Hud.Set(volumeValue, UnityEngine.Mathf.RoundToInt(BattleMusic.Volume * 100) + "%");
                effectsVolume.SetValueWithoutNotify(Audio.SoundEffects.Volume);
                Hud.Set(effectsValue, UnityEngine.Mathf.RoundToInt(Audio.SoundEffects.Volume * 100) + "%");
            }
            Hud.Show(Root, visible);
        }

        public bool Resume() => session != null && session.SetPaused(false);

        public bool RequestEnd()
        {
            if (session == null || !session.IsUserPaused || !session.CanPause) return false;
            Hud.Show(menu, false);
            Hud.Show(endConfirmation, true);
            return true;
        }

        public void CancelEnd()
        {
            Hud.Show(menu, true);
            Hud.Show(endConfirmation, false);
        }

        public bool ConfirmEnd()
        {
            if (!IsConfirmingEnd || session == null || !session.EndBattle()) return false;
            Refresh(session);
            return true;
        }
    }
}

using System;
using Lightbringer.Core;
using Lightbringer.Progression;
using UnityEngine;
using UnityEngine.UIElements;

namespace Lightbringer.UI
{
    // All marker coordinates share the atlas's fitted 16:9 canvas, including on ultrawide displays.
    public sealed class CampaignMapView
    {
        private const int StagesPerAct = 8, ActCount = 3;
        private static readonly string[] ActTitles = { "꺼진 변경", "옛 용사의 흔적", "마왕의 영역" };
        private static readonly string[] ActDescriptions = {
            "숲과 계곡의 봉화를 밝혀, 군주의 성채로",
            "옛 빛의 용사의 흔적을 따라, 군주의 거점과 보급로로",
            "황폐해진 땅을 넘어, 두 어둠이 기다리는 성채로"
        };
        // Sweep the western coast, northern valleys, central heartland and eastern frontier before
        // reaching the fortress. The atlas should read as reclaiming a region, not a diagonal shortcut.
        private static readonly Vector2[][] ActSites = {
            new[] {
                new Vector2(.19f, .72f), new Vector2(.28f, .39f),
                new Vector2(.30f, .19f), new Vector2(.59f, .18f),
                new Vector2(.43f, .56f), new Vector2(.83f, .55f),
                new Vector2(.75f, .33f), new Vector2(.883f, .135f)
            },
            new[] {
                new Vector2(.15f, .60f), new Vector2(.23f, .43f),
                new Vector2(.35f, .12f), new Vector2(.61f, .18f),
                new Vector2(.52f, .385f), new Vector2(.55f, .71f),
                new Vector2(.82f, .52f), new Vector2(.93f, .085f)
            },
            new[] {
                new Vector2(.17f, .70f), new Vector2(.24f, .36f),
                new Vector2(.38f, .17f), new Vector2(.59f, .20f),
                new Vector2(.47f, .49f), new Vector2(.70f, .70f),
                new Vector2(.85f, .39f), new Vector2(.84f, .15f)
            }
        };
        private Vector2[] Sites => ActSites[ViewedAct - 1];
        public readonly VisualElement Root;
        private readonly VisualElement canvas, roads;
        private readonly Button[] beacons = new Button[StagesPerAct];
        private readonly Label[] states = new Label[StagesPerAct], names = new Label[StagesPerAct];
        private readonly Button previousAct, nextAct;
        private readonly Label progress, selected, description, status, title, subtitle, eyebrow;
        private readonly Action openPreparation;
        private CampaignSession session;
        private string revision;
        private bool wasVisible;
        public int ViewedAct { get; private set; } = 1;

        public CampaignMapView(VisualElement parent, Action openPreparation, Action backToTitle = null)
        {
            this.openPreparation = openPreparation;
            Root = Hud.Box(parent, "lb-atlas");
            canvas = Hud.Box(Root, "lb-atlas-canvas");
            Hud.Box(canvas, "lb-atlas-paper");
            roads = Hud.Box(canvas, "lb-atlas-roads");
            roads.pickingMode = PickingMode.Ignore;
            roads.generateVisualContent += DrawRoads;
            Root.RegisterCallback<GeometryChangedEvent>(_ => FitCanvas());

            for (int i = 0; i < beacons.Length; i++)
            {
                int site = i;
                Button beacon = Hud.Button(null, "", () => { if (Select((ViewedAct - 1) * StagesPerAct + site + 1)) OpenPreparation(); });
                beacon.AddToClassList("lb-beacon");
                beacon.style.left = Length.Percent(Sites[i].x * 100);
                beacon.style.top = Length.Percent(Sites[i].y * 100);
                canvas.Add(beacon);
                Hud.Box(beacon, "lb-beacon-symbol");
                names[i] = Hud.Text(beacon, $"봉화 {i + 1:00}", "lb-beacon-name");
                states[i] = Hud.Text(beacon, "", "lb-beacon-state");
                foreach (VisualElement child in beacon.Children()) Hud.IgnorePicking(child);
                beacons[i] = beacon;
            }

            VisualElement header = Hud.Box(Root, "lb-atlas-header");
            VisualElement heading = Hud.Box(header);
            Hud.Text(heading, "LIGHTBRINGER", "lb-atlas-brand");
            title = Hud.Text(heading, "", "lb-atlas-title");
            subtitle = Hud.Text(heading, "", "lb-atlas-subtitle");
            // Keep the northeast corner clear for the fortress beacon in every act.
            progress = Hud.Text(heading, "", "lb-atlas-progress");
            Hud.IgnorePicking(header);

            previousAct = Hud.Button(canvas, "<", () => ShowAct(ViewedAct - 1), "lb-atlas-nav", "lb-atlas-nav--previous");
            nextAct = Hud.Button(canvas, ">", () => ShowAct(ViewedAct + 1), "lb-atlas-nav", "lb-atlas-nav--next");

            VisualElement footer = Hud.Box(Root, "lb-atlas-footer");
            VisualElement detail = Hud.Box(footer, "lb-atlas-detail");
            eyebrow = Hud.Text(detail, "선택한 목적지", "lb-atlas-eyebrow");
            selected = Hud.Text(detail, "", "lb-atlas-selected");
            description = Hud.Text(detail, "", "lb-atlas-description");
            VisualElement action = Hud.Box(footer, "lb-atlas-action");
            if (backToTitle != null)
            {
                Hud.Button(action, "← 타이틀로", backToTitle, "lb-prologue-button");
            }
            status = Hud.Text(Root, "", "lb-atlas-status");
            Hud.Show(Root, false);
        }

        private void FitCanvas()
        {
            float width = Root.contentRect.width, height = Root.contentRect.height;
            float scale = Mathf.Min(width / 16f, height / 9f);
            canvas.style.width = scale * 16f;
            canvas.style.height = scale * 9f;
            canvas.style.left = (width - scale * 16f) * .5f;
            canvas.style.top = (height - scale * 9f) * .5f;
        }

        public bool Select(int stage)
        {
            if (session == null || !session.SelectStage(stage)) return false;
            ViewedAct = Mathf.Clamp((stage - 1) / StagesPerAct + 1, 1, ActCount);
            Refresh(session, true);
            return true;
        }

        // Browsing future regions must not select a stage or mutate campaign progress.
        public bool ShowAct(int act)
        {
            if (act < 1 || act > ActCount || session == null || session.Battle != null) return false;
            ViewedAct = act;
            Refresh(session, wasVisible);
            return true;
        }

        private void OpenPreparation()
        {
            if (session != null && session.Battle == null && !session.IsChoosing)
                openPreparation?.Invoke();
        }

        public void Refresh(CampaignSession value, bool visible)
        {
            session = value;
            CampaignProgress profile = session != null ? session.Progress : null;
            bool show = visible && profile != null && session.Battle == null;
            Hud.Show(Root, show);
            if (show && !wasVisible)
                ViewedAct = Mathf.Clamp((session.SelectedStage - 1) / StagesPerAct + 1, 1, ActCount);
            wasVisible = show;
            if (!show) return;
            Hud.Set(status, session.SaveStatus ?? "");
            string key = ViewedAct + ":" + session.SelectedStage + ":" + profile.unlockedStage + ":" + string.Join(",", profile.cleared);
            if (key == revision) return;
            revision = key;
            int first = (ViewedAct - 1) * StagesPerAct;
            int available = Mathf.Clamp(CampaignProgress.StageCount - first, 0, StagesPerAct);
            for (int i = 0; i < ActCount; i++)
            {
                Root.EnableInClassList($"lb-atlas--act{i + 1}", ViewedAct == i + 1);
            }
            Hud.Show(previousAct, ViewedAct > 1);
            Hud.Show(nextAct, ViewedAct < ActCount);
            previousAct.SetEnabled(ViewedAct > 1);
            nextAct.SetEnabled(ViewedAct < ActCount);
            previousAct.tooltip = ViewedAct > 1 ? $"이전 지역 · {ActTitles[ViewedAct - 2]}" : "";
            nextAct.tooltip = ViewedAct < ActCount ? $"다음 지역 · {ActTitles[ViewedAct]}" : "";
            Hud.Set(title, $"{ViewedAct}막 · {ActTitles[ViewedAct - 1]}");
            Hud.Set(subtitle, ActDescriptions[ViewedAct - 1]);
            int count = 0;
            for (int i = 0; i < beacons.Length; i++)
            {
                beacons[i].style.left = Length.Percent(Sites[i].x * 100);
                beacons[i].style.top = Length.Percent(Sites[i].y * 100);
                int index = first + i;
                bool implemented = i < available;
                bool lit = implemented && profile.cleared[index], unlocked = implemented && index < profile.unlockedStage;
                if (lit) count++;
                Hud.Set(names[i], $"봉화 {index + 1:00}");
                beacons[i].SetEnabled(unlocked);
                beacons[i].EnableInClassList("lb-beacon--lit", lit);
                beacons[i].EnableInClassList("lb-beacon--ready", unlocked && !lit);
                beacons[i].EnableInClassList("lb-beacon--planned", !implemented);
                beacons[i].EnableInClassList("lb-beacon--selected", index + 1 == session.SelectedStage);
                Hud.Set(states[i], !implemented ? "준비 중" : lit ? "밝혀진 봉화" : unlocked ? "탈환 가능" : "미해방");
                beacons[i].tooltip = !implemented ? "이 봉화의 전투는 준비 중입니다."
                    : lit ? "해방한 전장에 다시 도전합니다." : unlocked ? "이 봉화를 탈환할 수 있습니다." : "앞선 봉화를 탈환하면 길이 열립니다.";
            }
            Hud.Set(progress, available == 0 ? $"봉화 {first + 1:00}–{first + StagesPerAct:00} · 준비 중"
                : $"밝혀진 봉화   {count:00} / {available:00}");
            bool selectedHere = session.SelectedStage > first && session.SelectedStage <= first + available;
            Hud.Set(eyebrow, selectedHere ? "선택한 목적지" : "다음 여정");
            Hud.Set(selected, selectedHere ? $"봉화 {session.SelectedStage:00}" : ActTitles[ViewedAct - 1]);
            Hud.Set(description, available == 0 ? "아직 열리지 않은 여정입니다. 이 지역의 전투는 준비 중입니다."
                : !selectedHere ? "봉화를 선택해 출전을 준비하세요."
                : profile.cleared[session.SelectedStage - 1] ? "빛을 되찾은 전장입니다. 다시 도전할 수 있습니다."
                : "아직 빛을 잃은 봉화입니다. 전장을 돌파하고 이 땅을 되찾으세요.");
            roads.MarkDirtyRepaint();
        }

        private void DrawRoads(MeshGenerationContext context)
        {
            if (session?.Progress == null) return;
            Painter2D p = context.painter2D;
            Vector2 size = roads.contentRect.size;
            int first = (ViewedAct - 1) * StagesPerAct;
            for (int i = 0; i < StagesPerAct - 1; i++)
            {
                bool reached = first + i < session.Progress.cleared.Length && session.Progress.cleared[first + i];
                Vector2 a = Vector2.Scale(Sites[i], size), b = Vector2.Scale(Sites[i + 1], size);
                p.strokeColor = reached ? new Color(.82f, .72f, .49f, .7f) : new Color(.72f, .76f, .75f, .25f);
                p.lineWidth = reached ? 1.6f : 1f;
                // Short dashes keep the terrain readable and distinguish the route from rivers.
                int segments = Mathf.Max(2, Mathf.CeilToInt(Vector2.Distance(a, b) / 12f));
                for (int j = 0; j < segments; j++)
                {
                    p.BeginPath();
                    p.MoveTo(Vector2.Lerp(a, b, (j + .15f) / segments));
                    p.LineTo(Vector2.Lerp(a, b, (j + .65f) / segments));
                    p.Stroke();
                }
            }
        }
    }
}

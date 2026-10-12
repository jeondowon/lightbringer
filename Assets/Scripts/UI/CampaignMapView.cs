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
        private static readonly Vector2[] Sites = {
            new Vector2(.19f, .72f), new Vector2(.30f, .60f),
            new Vector2(.40f, .67f), new Vector2(.46f, .47f),
            new Vector2(.57f, .54f), new Vector2(.64f, .34f),
            new Vector2(.73f, .42f), new Vector2(.80f, .24f)
        };
        public readonly VisualElement Root;
        private readonly VisualElement canvas, roads;
        private readonly Button[] beacons = new Button[CampaignProgress.StageCount];
        private readonly Label[] states = new Label[CampaignProgress.StageCount];
        private readonly Label progress, selected, description, status;
        private readonly Action openPreparation;
        private CampaignSession session;
        private string revision;

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
                int stage = i + 1;
                Button beacon = Hud.Button(null, "", () => { if (Select(stage)) OpenPreparation(); });
                beacon.AddToClassList("lb-beacon");
                beacon.style.left = Length.Percent(Sites[i].x * 100);
                beacon.style.top = Length.Percent(Sites[i].y * 100);
                canvas.Add(beacon);
                Hud.Box(beacon, "lb-beacon-symbol");
                Hud.Text(beacon, $"봉화 {stage:00}", "lb-beacon-name");
                states[i] = Hud.Text(beacon, "", "lb-beacon-state");
                foreach (VisualElement child in beacon.Children()) Hud.IgnorePicking(child);
                beacons[i] = beacon;
            }

            VisualElement header = Hud.Box(Root, "lb-atlas-header");
            VisualElement heading = Hud.Box(header);
            Hud.Text(heading, "LIGHTBRINGER", "lb-atlas-brand");
            Hud.Text(heading, "잊힌 봉화의 땅", "lb-atlas-title");
            Hud.Text(heading, "꺼진 봉화를 따라, 빛을 되찾는 여정", "lb-atlas-subtitle");
            progress = Hud.Text(header, "", "lb-atlas-progress");
            Hud.IgnorePicking(header);

            VisualElement footer = Hud.Box(Root, "lb-atlas-footer");
            VisualElement detail = Hud.Box(footer, "lb-atlas-detail");
            Hud.Text(detail, "선택한 목적지", "lb-atlas-eyebrow");
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
            Refresh(session, true);
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
            Hud.Show(Root, visible && profile != null && session.Battle == null);
            if (!visible || profile == null || session.Battle != null) return;
            Hud.Set(status, session.SaveStatus ?? "");
            string key = session.SelectedStage + ":" + profile.unlockedStage + ":" + string.Join(",", profile.cleared);
            if (key == revision) return;
            revision = key;
            int count = 0;
            for (int i = 0; i < beacons.Length; i++)
            {
                bool lit = profile.cleared[i], unlocked = i < profile.unlockedStage;
                if (lit) count++;
                beacons[i].SetEnabled(unlocked);
                beacons[i].EnableInClassList("lb-beacon--lit", lit);
                beacons[i].EnableInClassList("lb-beacon--ready", unlocked && !lit);
                beacons[i].EnableInClassList("lb-beacon--selected", i + 1 == session.SelectedStage);
                Hud.Set(states[i], lit ? "밝혀진 봉화" : unlocked ? "탈환 가능" : "미해방");
                beacons[i].tooltip = lit ? "해방한 전장에 다시 도전합니다." : unlocked ? "이 봉화를 탈환할 수 있습니다." : "앞선 봉화를 탈환하면 길이 열립니다.";
            }
            Hud.Set(progress, $"밝혀진 봉화   {count:00} / {beacons.Length:00}");
            Hud.Set(selected, $"봉화 {session.SelectedStage:00}");
            Hud.Set(description, profile.cleared[session.SelectedStage - 1]
                ? "빛을 되찾은 전장입니다. 다시 도전할 수 있습니다."
                : "아직 빛을 잃은 봉화입니다. 전장을 돌파하고 이 땅을 되찾으세요.");
            roads.MarkDirtyRepaint();
        }

        private void DrawRoads(MeshGenerationContext context)
        {
            if (session?.Progress == null) return;
            Painter2D p = context.painter2D;
            Vector2 size = roads.contentRect.size;
            for (int i = 0; i < Sites.Length - 1; i++)
            {
                bool reached = session.Progress.cleared[i];
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

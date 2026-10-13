using System.Collections.Generic;
using Lightbringer.Combat;
using Lightbringer.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Lightbringer.UI
{
    // Top-down battlefield map drawn as a treasure map: ink border, trees and hills around the roads, dashed routes,
    // the enemy stronghold marked with a red X and ours as a little castle. The Path new troops will take is the red
    // route. Dots show live troops, the gold arrow is the hero. Enemy stronghold at the top, ours at the bottom.
    internal sealed class BattleMinimap
    {
        private const float Padding = 5f, MinimumWidth = 40f;
        private const float RepaintInterval = 1f / 15f;
        private static readonly Color Ink = new Color(0.24f, 0.14f, 0.06f, 0.9f);
        private static readonly Color InkFaint = new Color(0.3f, 0.19f, 0.09f, 0.3f);
        private static readonly Color RouteIdle = new Color(0.32f, 0.2f, 0.09f, 0.62f);
        private static readonly Color RouteSelected = new Color(0.68f, 0.1f, 0.05f);
        private static readonly Color RouteWash = new Color(0.75f, 0.2f, 0.08f, 0.16f);
        private static readonly Color AllyDot = new Color(0.12f, 0.24f, 0.58f);
        private static readonly Color EnemyDot = new Color(0.7f, 0.1f, 0.05f);
        private static readonly Color AllyCastle = new Color(0.16f, 0.28f, 0.6f);
        private static readonly Color MarkRed = new Color(0.62f, 0.07f, 0.03f);
        private static readonly Color Shield = new Color(0.42f, 0.28f, 0.72f);
        private static readonly Color Hero = new Color(1f, 0.83f, 0.35f);

        public readonly VisualElement Map;
        private readonly List<Label> labels = new List<Label>();
        private readonly List<Vector3> scenery = new List<Vector3>(); // x, y, kind (0 tree, 1 hill)
        private PrototypeBattle battle;
        private BattlefieldReadability readability;
        private Vector2 worldCentre, worldSize;
        private int selected = -1;
        private float repaintTimer;

        public BattleMinimap(VisualElement parent)
        {
            Map = Hud.Box(parent, "lb-minimap-map");
            Map.generateVisualContent += Draw;
            Map.RegisterCallback<GeometryChangedEvent>(_ => { PlaceLabels(); PlaceScenery(); });
        }

        public int SelectedPath => selected;

        public void Bind(PrototypeBattle value, BattlefieldReadability source)
        {
            battle = value;
            readability = source;
            foreach (Label label in labels) label.RemoveFromHierarchy();
            labels.Clear();
            scenery.Clear();
            selected = -1;
            if (battle == null) return;
            ComputeBounds();
            for (int i = 0; i < battle.Paths.Length; i++)
                labels.Add(Hud.Text(Map, (i + 1).ToString(), "lb-minimap-label"));
            PlaceLabels();
            PlaceScenery();
        }

        public void Refresh(int selectedPath)
        {
            if (battle == null) return;
            if (selectedPath != selected)
            {
                selected = selectedPath;
                for (int i = 0; i < labels.Count; i++) labels[i].EnableInClassList("lb-minimap-label--selected", i == selected);
                repaintTimer = 0f; // A new Path selection shows at once.
            }
            // Troops and the hero move every frame, but a minimap dot moves under a pixel per frame:
            // redraw (re-tessellating hundreds of dots) at 15 Hz instead of every frame.
            repaintTimer -= Time.unscaledDeltaTime;
            if (repaintTimer > 0f) return;
            repaintTimer = RepaintInterval;
            Map.MarkDirtyRepaint();
        }

        private void ComputeBounds()
        {
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            void Include(Vector3 point)
            {
                min = Vector2.Min(min, new Vector2(point.x, point.z));
                max = Vector2.Max(max, new Vector2(point.x, point.z));
            }
            foreach (var path in battle.Paths)
                for (int i = 0; i < path.Count; i++) Include(path.GetPosition(i));
            if (battle.AlliedBase != null) Include(battle.AlliedBase.transform.position);
            if (battle.Beacon != null) Include(battle.Beacon.transform.position);
            if (battle.Objective != null && battle.Objective.EnemyBase != null) Include(battle.Objective.EnemyBase.transform.position);
            worldCentre = (min + max) * 0.5f;
            worldSize = max - min + Vector2.one * Padding * 2f;
            // A single straight Path would otherwise be stretched into a thin strip.
            worldSize.x = Mathf.Max(worldSize.x, MinimumWidth);
        }

        private float Scale
        {
            get
            {
                Rect area = Map.contentRect;
                return worldSize.x > 0f && worldSize.y > 0f ? Mathf.Min(area.width / worldSize.x, area.height / worldSize.y) : 0f;
            }
        }

        private Vector2 ToMap(Vector3 world)
        {
            Rect area = Map.contentRect;
            float scale = Scale;
            return new Vector2(area.center.x + (world.x - worldCentre.x) * scale, area.center.y - (world.z - worldCentre.y) * scale);
        }

        // Each Path's number is a badge on the Path where troops join it.
        private void PlaceLabels()
        {
            if (battle == null || float.IsNaN(Map.contentRect.width)) return;
            for (int i = 0; i < labels.Count && i < battle.Paths.Length; i++)
            {
                Vector2 point = ToMap(battle.Paths[i].GetPosition(0));
                labels[i].style.left = point.x;
                labels[i].style.top = point.y;
            }
        }

        // Trees and hills fill the land away from the roads and strongholds; fixed per battle so they never flicker.
        private void PlaceScenery()
        {
            scenery.Clear();
            Rect area = Map.contentRect;
            if (battle == null || float.IsNaN(area.width) || area.width < 20f || Scale <= 0f) return;
            List<Vector2> road = new List<Vector2>();
            foreach (var path in battle.Paths)
            {
                Vector2 previous = ToMap(battle.DeployPoint != null ? battle.DeployPoint.position : path.GetPosition(0));
                for (int i = 0; i < path.Count; i++)
                {
                    Vector2 next = ToMap(path.GetPosition(i));
                    for (int k = 0; k <= 6; k++) road.Add(Vector2.Lerp(previous, next, k / 6f));
                    previous = next;
                }
            }
            if (battle.AlliedBase != null) road.Add(ToMap(battle.AlliedBase.transform.position));
            if (battle.Objective != null && battle.Objective.EnemyBase != null) road.Add(ToMap(battle.Objective.EnemyBase.transform.position));
            const float Step = 15f, Clearance = 13f, Border = 9f;
            int row = 0;
            for (float y = area.yMin + Border + 4f; y < area.yMax - Border; y += Step * 0.8f, row++)
                for (float x = area.xMin + Border + (row % 2) * Step * 0.5f; x < area.xMax - Border; x += Step)
                {
                    float jx = (Hash(x, y, 1) - 0.5f) * Step * 0.7f, jy = (Hash(x, y, 2) - 0.5f) * Step * 0.6f;
                    Vector2 point = new Vector2(x + jx, y + jy);
                    if (point.x < area.xMin + Border || point.x > area.xMax - Border || point.y > area.yMax - Border) continue;
                    if (Hash(x, y, 3) < 0.5f) continue;
                    bool clear = true;
                    foreach (Vector2 r in road) if ((r - point).sqrMagnitude < Clearance * Clearance) { clear = false; break; }
                    if (!clear) continue;
                    scenery.Add(new Vector3(point.x, point.y, Hash(x, y, 4) < 0.18f ? 1f : 0f));
                }
        }

        private static float Hash(float x, float y, int seed)
        {
            uint h = (uint)(Mathf.RoundToInt(x * 7f) * 374761393 + Mathf.RoundToInt(y * 13f) * 668265263 + seed * 1442695041);
            h = (h ^ (h >> 13)) * 1274126177u;
            return (h ^ (h >> 16)) / (float)uint.MaxValue;
        }

        private void Draw(MeshGenerationContext context)
        {
            if (battle == null || Scale <= 0f) return;
            Painter2D painter = context.painter2D;
            painter.lineJoin = LineJoin.Round;
            painter.lineCap = LineCap.Round;
            DrawBorder(painter);
            DrawScenery(painter);

            for (int i = 0; i < battle.Paths.Length; i++)
                if (i != selected) StrokeRoute(painter, i, RouteIdle, 1.8f, 5f, 4f);
            if (selected >= 0 && selected < battle.Paths.Length)
            {
                StrokeRoute(painter, selected, RouteWash, 9f, 0f, 0f);
                StrokeRoute(painter, selected, RouteSelected, 3f, 8f, 4f);
            }

            if (battle.AlliedBase != null) DrawCastle(painter, ToMap(battle.AlliedBase.transform.position));
            if (battle.Objective != null && battle.Objective.EnemyBase != null)
                DrawTreasureX(painter, ToMap(battle.Objective.EnemyBase.transform.position), battle.Objective.EnemyBase.Invulnerable);

            DrawUnits(painter);
            if (battle.Beacon != null)
            {
                Vector2 point = ToMap(battle.Beacon.transform.position);
                painter.BeginPath();
                painter.MoveTo(point + new Vector2(0f, -6f));
                painter.LineTo(point + new Vector2(4f, 0f));
                painter.LineTo(point + new Vector2(0f, 6f));
                painter.LineTo(point + new Vector2(-4f, 0f));
                painter.ClosePath();
                painter.fillColor = battle.Beacon.IsLit ? Hero : new Color(0.55f, 0.45f, 0.32f);
                painter.Fill();
                painter.strokeColor = Ink;
                painter.lineWidth = 1.2f;
                painter.Stroke();
            }
            DrawHero(painter);
        }

        // Hand-ruled double frame inside the parchment.
        private void DrawBorder(Painter2D painter)
        {
            Rect r = Map.contentRect;
            foreach ((float inset, float width) in new[] { (2f, 1.4f), (5f, 0.6f) })
            {
                painter.BeginPath();
                painter.MoveTo(new Vector2(r.xMin + inset, r.yMin + inset));
                painter.LineTo(new Vector2(r.xMax - inset, r.yMin + inset));
                painter.LineTo(new Vector2(r.xMax - inset, r.yMax - inset));
                painter.LineTo(new Vector2(r.xMin + inset, r.yMax - inset));
                painter.ClosePath();
                painter.strokeColor = Ink;
                painter.lineWidth = width;
                painter.Stroke();
            }
        }

        private void DrawScenery(Painter2D painter)
        {
            painter.strokeColor = InkFaint;
            painter.lineWidth = 1f;
            foreach (Vector3 item in scenery)
            {
                Vector2 p = new Vector2(item.x, item.y);
                painter.BeginPath();
                if (item.z > 0.5f)
                {
                    // Hill: a peak with a shading stroke on its right flank.
                    painter.MoveTo(p + new Vector2(-6f, 3f));
                    painter.LineTo(p + new Vector2(0f, -4f));
                    painter.LineTo(p + new Vector2(6f, 3f));
                    painter.MoveTo(p + new Vector2(1.5f, -1.5f));
                    painter.LineTo(p + new Vector2(3.5f, 2f));
                }
                else
                {
                    // Tree: round crown on a short trunk.
                    painter.MoveTo(p + new Vector2(3f, -2f));
                    painter.Arc(p + new Vector2(0f, -2f), 3f, new Angle(0f, AngleUnit.Degree), new Angle(360f, AngleUnit.Degree));
                    painter.MoveTo(p + new Vector2(0f, 1f));
                    painter.LineTo(p + new Vector2(0f, 4f));
                }
                painter.Stroke();
            }
        }

        // Troops walk from the deploy gate to the start of their Path, so the route begins at the gate.
        private void StrokeRoute(Painter2D painter, int index, Color colour, float width, float dash, float gap)
        {
            var path = battle.Paths[index];
            List<Vector2> points = new List<Vector2> { ToMap(battle.DeployPoint != null ? battle.DeployPoint.position : path.GetPosition(0)) };
            for (int i = 0; i < path.Count; i++) points.Add(ToMap(path.GetPosition(i)));
            painter.strokeColor = colour;
            painter.lineWidth = width;
            painter.BeginPath();
            if (dash <= 0f)
            {
                painter.MoveTo(points[0]);
                for (int i = 1; i < points.Count; i++) painter.LineTo(points[i]);
                painter.Stroke();
                return;
            }
            float carried = 0f;
            bool drawing = true;
            for (int i = 1; i < points.Count; i++)
            {
                Vector2 a = points[i - 1], b = points[i];
                float length = Vector2.Distance(a, b), at = 0f;
                while (at < length)
                {
                    float run = Mathf.Min((drawing ? dash : gap) - carried, length - at);
                    Vector2 from = Vector2.Lerp(a, b, at / length), to = Vector2.Lerp(a, b, (at + run) / length);
                    if (drawing) { painter.MoveTo(from); painter.LineTo(to); }
                    at += run;
                    carried += run;
                    if (carried >= (drawing ? dash : gap) - 0.001f) { carried = 0f; drawing = !drawing; }
                }
            }
            painter.Stroke();
        }

        private static void DrawCastle(Painter2D painter, Vector2 c)
        {
            painter.BeginPath();
            painter.MoveTo(c + new Vector2(-7f, 5f));
            painter.LineTo(c + new Vector2(-7f, -5f));
            painter.LineTo(c + new Vector2(-4.5f, -5f));
            painter.LineTo(c + new Vector2(-4.5f, -2.5f));
            painter.LineTo(c + new Vector2(-1.2f, -2.5f));
            painter.LineTo(c + new Vector2(-1.2f, -5f));
            painter.LineTo(c + new Vector2(1.2f, -5f));
            painter.LineTo(c + new Vector2(1.2f, -2.5f));
            painter.LineTo(c + new Vector2(4.5f, -2.5f));
            painter.LineTo(c + new Vector2(4.5f, -5f));
            painter.LineTo(c + new Vector2(7f, -5f));
            painter.LineTo(c + new Vector2(7f, 5f));
            painter.ClosePath();
            painter.fillColor = AllyCastle;
            painter.Fill();
            painter.strokeColor = Ink;
            painter.lineWidth = 1.2f;
            painter.Stroke();
        }

        // X marks the spot: the enemy stronghold, ringed in violet while its shield holds.
        private static void DrawTreasureX(Painter2D painter, Vector2 c, bool shielded)
        {
            painter.BeginPath();
            painter.MoveTo(c + new Vector2(10f, 0f));
            painter.Arc(c, 10f, new Angle(0f, AngleUnit.Degree), new Angle(360f, AngleUnit.Degree));
            painter.strokeColor = shielded ? Shield : new Color(MarkRed.r, MarkRed.g, MarkRed.b, 0.55f);
            painter.lineWidth = shielded ? 2.2f : 1.2f;
            painter.Stroke();
            painter.BeginPath();
            painter.MoveTo(c + new Vector2(-6.5f, -6.5f));
            painter.LineTo(c + new Vector2(6.5f, 6.5f));
            painter.MoveTo(c + new Vector2(6.5f, -6.5f));
            painter.LineTo(c + new Vector2(-6.5f, 6.5f));
            painter.strokeColor = MarkRed;
            painter.lineWidth = 3.4f;
            painter.Stroke();
        }

        private void DrawUnits(Painter2D painter)
        {
            if (readability == null) return;
            IReadOnlyList<Combatant> units = readability.Units;
            Combatant boss = battle.Waves != null ? battle.Waves.Boss : null;
            foreach (Faction faction in new[] { Faction.Allied, Faction.Enemy })
            {
                painter.BeginPath();
                bool any = false;
                foreach (Combatant unit in units)
                {
                    if (unit == null || !unit.IsAlive || unit.Faction != faction || unit == boss || IsLandmark(unit)) continue;
                    AddDot(painter, ToMap(unit.transform.position), 2.6f);
                    any = true;
                }
                if (!any) continue;
                painter.fillColor = faction == Faction.Allied ? AllyDot : EnemyDot;
                painter.Fill();
            }
            if (boss != null && boss.IsAlive)
            {
                painter.BeginPath();
                AddDot(painter, ToMap(boss.transform.position), 5.5f);
                painter.fillColor = EnemyDot;
                painter.Fill();
                painter.strokeColor = Ink;
                painter.lineWidth = 1.5f;
                painter.Stroke();
            }
        }

        private bool IsLandmark(Combatant unit) =>
            unit == battle.Hero || unit == battle.AlliedBase || (battle.Objective != null && unit == battle.Objective.EnemyBase);

        private static void AddDot(Painter2D painter, Vector2 centre, float radius)
        {
            painter.MoveTo(centre + new Vector2(radius, 0f));
            painter.Arc(centre, radius, new Angle(0f, AngleUnit.Degree), new Angle(360f, AngleUnit.Degree));
            painter.ClosePath();
        }

        private void DrawHero(Painter2D painter)
        {
            if (battle.Hero == null || !battle.Hero.IsAlive) return;
            Vector2 centre = ToMap(battle.Hero.transform.position);
            Vector3 forward = battle.Hero.transform.forward;
            Vector2 facing = new Vector2(forward.x, -forward.z);
            facing = facing.sqrMagnitude > 0.001f ? facing.normalized : Vector2.down;
            Vector2 side = new Vector2(-facing.y, facing.x);
            painter.BeginPath();
            painter.MoveTo(centre + facing * 8f);
            painter.LineTo(centre - facing * 5f + side * 5.5f);
            painter.LineTo(centre - facing * 2.5f);
            painter.LineTo(centre - facing * 5f - side * 5.5f);
            painter.ClosePath();
            painter.fillColor = Hero;
            painter.Fill();
            painter.strokeColor = Ink;
            painter.lineWidth = 1.5f;
            painter.Stroke();
        }
    }
}

using System.Collections.Generic;
using Lightbringer.Combat;
using Lightbringer.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Lightbringer.UI
{
    // Top-down battlefield map: enemy stronghold at the top, ours at the bottom. Every Path is traced from the
    // deploy gate; the Path new troops will take is drawn in gold. Dots show live troops, the arrow is the hero.
    internal sealed class BattleMinimap
    {
        private const float Padding = 5f, MinimumWidth = 40f;
        private static readonly Color PathIdle = new Color(0.93f, 0.9f, 0.82f, 0.32f);
        private static readonly Color PathSelected = new Color(1f, 0.82f, 0.38f);
        private static readonly Color PathGlow = new Color(1f, 0.82f, 0.38f, 0.22f);
        private static readonly Color AllyDot = new Color(0.55f, 0.8f, 1f);
        private static readonly Color EnemyDot = new Color(0.95f, 0.36f, 0.3f);
        private static readonly Color AllyBase = new Color(0.3f, 0.5f, 0.9f);
        private static readonly Color EnemyBase = new Color(0.75f, 0.2f, 0.2f);
        private static readonly Color Outline = new Color(0.02f, 0.03f, 0.07f, 0.9f);
        private static readonly Color Hero = new Color(1f, 0.9f, 0.55f);

        public readonly VisualElement Map;
        private readonly List<Label> labels = new List<Label>();
        private PrototypeBattle battle;
        private BattlefieldReadability readability;
        private Vector2 worldCentre, worldSize;
        private int selected = -1;

        public BattleMinimap(VisualElement parent)
        {
            Map = Hud.Box(parent, "lb-minimap-map");
            Map.generateVisualContent += Draw;
            Map.RegisterCallback<GeometryChangedEvent>(_ => PlaceLabels());
        }

        public int SelectedPath => selected;

        public void Bind(PrototypeBattle value, BattlefieldReadability source)
        {
            battle = value;
            readability = source;
            foreach (Label label in labels) label.RemoveFromHierarchy();
            labels.Clear();
            selected = -1;
            if (battle == null) return;
            ComputeBounds();
            for (int i = 0; i < battle.Paths.Length; i++)
                labels.Add(Hud.Text(Map, (i + 1).ToString(), "lb-minimap-label"));
            PlaceLabels();
        }

        public void Refresh(int selectedPath)
        {
            if (battle == null) return;
            if (selectedPath != selected)
            {
                selected = selectedPath;
                for (int i = 0; i < labels.Count; i++) labels[i].EnableInClassList("lb-minimap-label--selected", i == selected);
            }
            // Troops and the hero move every frame.
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

        private void Draw(MeshGenerationContext context)
        {
            if (battle == null || Scale <= 0f) return;
            Painter2D painter = context.painter2D;
            painter.lineJoin = LineJoin.Round;
            painter.lineCap = LineCap.Round;

            if (battle.Objective != null && battle.Objective.EnemyBase != null)
                DrawBase(painter, battle.Objective.EnemyBase.transform, EnemyBase, battle.Objective.EnemyBase.Invulnerable);
            if (battle.AlliedBase != null) DrawBase(painter, battle.AlliedBase.transform, AllyBase, false);

            for (int i = 0; i < battle.Paths.Length; i++)
                if (i != selected) StrokePath(painter, i, PathIdle, 2.5f);
            if (selected >= 0 && selected < battle.Paths.Length)
            {
                StrokePath(painter, selected, PathGlow, 9f);
                StrokePath(painter, selected, PathSelected, 3.5f);
            }

            DrawUnits(painter);
            DrawHero(painter);
        }

        private void DrawBase(Painter2D painter, Transform stronghold, Color colour, bool shielded)
        {
            Vector2 centre = ToMap(stronghold.position);
            Vector2 half = new Vector2(stronghold.lossyScale.x, stronghold.lossyScale.z) * Scale * 0.5f;
            painter.BeginPath();
            painter.MoveTo(centre + new Vector2(-half.x, -half.y));
            painter.LineTo(centre + new Vector2(half.x, -half.y));
            painter.LineTo(centre + new Vector2(half.x, half.y));
            painter.LineTo(centre + new Vector2(-half.x, half.y));
            painter.ClosePath();
            painter.fillColor = colour;
            painter.Fill();
            painter.strokeColor = shielded ? new Color(0.7f, 0.85f, 1f) : new Color(1f, 0.85f, 0.5f, 0.8f);
            painter.lineWidth = shielded ? 2.5f : 1.5f;
            painter.Stroke();
        }

        // Troops walk from the deploy gate to the start of their Path, so the trace begins at the gate.
        private void StrokePath(Painter2D painter, int index, Color colour, float width)
        {
            var path = battle.Paths[index];
            painter.BeginPath();
            painter.MoveTo(ToMap(battle.DeployPoint != null ? battle.DeployPoint.position : path.GetPosition(0)));
            for (int i = 0; i < path.Count; i++) painter.LineTo(ToMap(path.GetPosition(i)));
            painter.strokeColor = colour;
            painter.lineWidth = width;
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
                painter.strokeColor = Hero;
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
            painter.strokeColor = Outline;
            painter.lineWidth = 1.5f;
            painter.Stroke();
        }
    }
}

using System.Collections.Generic;
using Lightbringer.Combat;
using Lightbringer.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Lightbringer.UI
{
    // Small health bars over damaged units, nearest to the camera first. Pooled; positions follow the camera.
    internal sealed class HealthBarOverlay
    {
        private const int MaxBars = 48;
        private const float HeadHeight = 1.25f;
        private readonly VisualElement layer;
        private readonly List<VisualElement> bars = new List<VisualElement>();
        private readonly List<VisualElement> fills = new List<VisualElement>();

        public HealthBarOverlay(VisualElement parent) => layer = Hud.Box(parent, "lb-healthbars");

        public void Clear()
        {
            foreach (VisualElement bar in bars) Hud.Show(bar, false);
        }

        public void Refresh(PrototypeBattle battle, BattlefieldReadability source)
        {
            int shown = 0;
            IPanel panel = layer.panel;
            if (battle != null && battle.Camera != null && source != null && panel != null)
            {
                IReadOnlyList<Combatant> units = source.DamagedUnits;
                for (int i = 0; i < units.Count && shown < MaxBars; i++)
                {
                    Combatant unit = units[i];
                    if (unit == null || !unit.IsAlive) continue;
                    Vector3 world = unit.transform.position + Vector3.up * HeadHeight;
                    Vector3 viewport = battle.Camera.WorldToViewportPoint(world);
                    if (viewport.z <= 0f || viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f) continue;
                    Vector2 position = RuntimePanelUtils.CameraTransformWorldToPanel(panel, world, battle.Camera);
                    VisualElement bar = Get(shown++);
                    bar.style.left = position.x;
                    bar.style.top = position.y;
                    bar.EnableInClassList("lb-unit-bar--ally", unit.Faction == Faction.Allied);
                    bar.EnableInClassList("lb-unit-bar--enemy", unit.Faction == Faction.Enemy);
                    Hud.Fraction(fills[shown - 1], unit.CurrentHealth / unit.MaximumHealth);
                    Hud.Show(bar, true);
                }
            }
            for (int i = shown; i < bars.Count; i++) Hud.Show(bars[i], false);
        }

        private VisualElement Get(int index)
        {
            while (bars.Count <= index)
            {
                VisualElement bar = Hud.Box(layer, "lb-unit-bar");
                fills.Add(Hud.Box(bar, "lb-unit-bar-fill"));
                Hud.IgnorePicking(bar);
                bars.Add(bar);
            }
            return bars[index];
        }
    }
}

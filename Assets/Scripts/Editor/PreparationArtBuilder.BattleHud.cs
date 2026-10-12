using System;
using UnityEditor;
using UnityEngine;

namespace Lightbringer.EditorTools
{
    // Status icons used by the battle HUD. Panels, gauges and key caps are styled directly in USS.
    public static partial class PreparationArtBuilder
    {
        private const string HudRequest = "Docs/Validation/BuildBattleHudArt.request";

        [InitializeOnLoadMethod]
        private static void RegisterBattleHud() => EditorRequests.Register(HudRequest, BuildBattleHud);

        [MenuItem("Lightbringer/UI/Build Battle HUD Art")]
        public static void BuildBattleHud()
        {
            Written.Clear();
            SaveBattleHudArt();
            ImportWritten();
            Debug.Log("Battle HUD art built: " + Written.Count + " textures in " + Folder);
        }

        private static void SaveBattleHudArt()
        {
            Save("Hud_IconAlly", ShieldIcon(64), false);
            Save("Hud_IconEnemy", SkullIcon(64), false);
            Save("Hud_IconWave", SwordsIcon(64), false);
        }

        private static Color Alpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

        // Brushed metal: bright toward the top-left light, dark toward the bottom-right.
        private static Color Metal(float x, float y, float cx, float cy, float r)
        {
            float t = Mathf.Clamp01(0.5f + ((x - cx) * 0.45f + (y - cy)) / (2.6f * r));
            Color c = t < 0.45f ? Color.Lerp(new Color(1f, 0.93f, 0.7f), Gold, t / 0.45f) : Color.Lerp(Gold, DarkGold, (t - 0.45f) / 0.55f);
            return Alpha(c, 1f);
        }

        private static Canvas ShieldIcon(int size)
        {
            Canvas canvas = new Canvas(size, size);
            Func<float, float, float> shield = (x, y) => Mathf.Min(BoxSdf(x, y, 32f, 22f, 20f, 13f, 3f),
                ConvexSdf(x, y, new Vector2(12f, 30f), new Vector2(52f, 30f), new Vector2(32f, 58f)));
            canvas.Shape(0, 0, size, size, (x, y) => shield(x, y) - 2.5f, (x, y) => Metal(x, y, 32f, 32f, 26f), 2f, 0.4f);
            canvas.Shape(0, 0, size, size, shield, (x, y) => Alpha(Vertical(new Color(0.55f, 0.75f, 1f), new Color(0.12f, 0.25f, 0.65f), y, 9f, 58f), 1f));
            canvas.Shape(0, 0, size, size, (x, y) => StarSdf(x, y, 32f, 28f, 13f, 2.5f, 4, 0f), (x, y) => new Color(1f, 0.95f, 0.78f), 2f, 0.5f);
            return canvas;
        }

        private static Canvas SkullIcon(int size)
        {
            Canvas canvas = new Canvas(size, size);
            Func<float, float, float> skull = (x, y) =>
            {
                float head = Mathf.Min(EllipseSdf(x, y, 32f, 27f, 21f, 19f), BoxSdf(x, y, 32f, 46f, 11f, 9f, 3f));
                float eyes = Mathf.Min(EllipseSdf(x, y, 24f, 30f, 6f, 6.5f), EllipseSdf(x, y, 40f, 30f, 6f, 6.5f));
                float nose = DiamondSdf(x, y, 32f, 40f, 2.6f, 3.5f);
                float teeth = Mathf.Min(BoxSdf(x, y, 27.5f, 52f, 0.8f, 4f, 0f), BoxSdf(x, y, 36.5f, 52f, 0.8f, 4f, 0f));
                return Mathf.Max(head, -Mathf.Min(eyes, Mathf.Min(nose, teeth)));
            };
            canvas.Shape(0, 0, size, size, (x, y) => skull(x, y) - 2.2f, (x, y) => new Color(0.25f, 0.02f, 0.04f, 1f), 2f, 0.4f);
            canvas.Shape(0, 0, size, size, skull, (x, y) => Alpha(Vertical(new Color(1f, 0.86f, 0.82f), new Color(0.85f, 0.3f, 0.3f), y, 8f, 58f), 1f));
            return canvas;
        }

        private static Canvas SwordsIcon(int size)
        {
            Canvas canvas = new Canvas(size, size);
            for (int s = -1; s <= 1; s += 2)
            {
                Vector2 tip = new Vector2(32f + s * 22f, 8f), pommel = new Vector2(32f - s * 20f, 56f);
                Vector2 dir = (pommel - tip).normalized, side = new Vector2(-dir.y, dir.x);
                Vector2 guard = tip + dir * 38f;
                canvas.Segment(tip, guard, 3.2f, new Color(0.1f, 0.08f, 0.06f), 1.5f, 0.4f);
                canvas.Segment(tip + dir * 2f, guard, 2.2f, new Color(0.9f, 0.92f, 0.98f), 0f, 0f);
                canvas.Segment(guard - side * 8f, guard + side * 8f, 2.4f, Gold, 1f, 0.3f);
                canvas.Segment(guard, pommel, 2.2f, new Color(0.45f, 0.3f, 0.15f), 0f, 0f);
                canvas.Shape(pommel.x - 5, pommel.y - 5, pommel.x + 5, pommel.y + 5, (x, y) => Len(x - pommel.x, y - pommel.y) - 3.2f, (x, y) => BrightGold);
            }
            return canvas;
        }

    }
}

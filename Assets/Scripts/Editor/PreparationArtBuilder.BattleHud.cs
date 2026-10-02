using System;
using UnityEditor;
using UnityEngine;

namespace Lightbringer.EditorTools
{
    // Battle HUD art (Hud_*.png): smoked-glass panels, metal-framed gauges, spiked ability orb rings with glowing
    // emblems, the hero portrait ring, troop card frames, key caps and small status icons.
    public static partial class PreparationArtBuilder
    {
        private const string HudRequest = "Docs/Validation/BuildBattleHudArt.request";
        private static readonly Color Navy = new Color(0.05f, 0.07f, 0.13f);
        private static readonly Color[] OrbColours =
        {
            new Color(1f, 0.82f, 0.42f), new Color(0.36f, 0.95f, 0.58f), new Color(0.66f, 0.52f, 1f),
            new Color(1f, 0.66f, 0.26f), new Color(0.36f, 0.62f, 1f), new Color(1f, 0.32f, 0.38f),
        };

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
            Save("Hud_Panel", Panel(72, false), false);
            Save("Hud_FramePanel", Panel(112, true), false);
            Save("Hud_BarFrame", BarFrame(64, 32), false);
            Save("Hud_BarFill", BarFill(4, 64), false);
            Save("Hud_BarGloss", BarGloss(4, 64), false);
            Save("Hud_OrbRing", OrbRing(192), false);
            for (int i = 0; i < OrbColours.Length; i++) Save("Hud_Orb_" + i, Orb(160, i), false);
            Save("Hud_PortraitRing", PortraitRing(224), false);
            Save("Hud_CardFrame", TroopFrame(96), false);
            Save("Hud_Keycap", Keycap(40), false);
            Save("Hud_Banner", Banner(256, 56), false);
            Save("Hud_IconAlly", ShieldIcon(64), false);
            Save("Hud_IconEnemy", SkullIcon(64), false);
            Save("Hud_IconWave", SwordsIcon(64), false);
            Save("Hud_IconCastle", CastleIcon(64), false);
        }

        private static Color Alpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

        // Brushed metal: bright toward the top-left light, dark toward the bottom-right.
        private static Color Metal(float x, float y, float cx, float cy, float r)
        {
            float t = Mathf.Clamp01(0.5f + ((x - cx) * 0.45f + (y - cy)) / (2.6f * r));
            Color c = t < 0.45f ? Color.Lerp(new Color(1f, 0.93f, 0.7f), Gold, t / 0.45f) : Color.Lerp(Gold, DarkGold, (t - 0.45f) / 0.55f);
            return Alpha(c, 1f);
        }

        // ---------- Panels ----------

        // 9-slice smoked-glass panel with a soft drop shadow (slice 22 / 30). The framed variant adds gold corner brackets.
        private static Canvas Panel(int size, bool framed)
        {
            Canvas canvas = new Canvas(size, size);
            float c = size * 0.5f, half = c - 7f;
            canvas.Shape(0, 0, size, size, (x, y) => BoxSdf(x, y, c, c + 1.5f, half, half, 9f), (x, y) => new Color(0f, 0f, 0f, 0.4f), 3f, 0.5f);
            canvas.Shape(0, 0, size, size, (x, y) => BoxSdf(x, y, c, c, half, half, 8f), (x, y) =>
            {
                Color fill = Vertical(new Color(0.11f, 0.13f, 0.2f), new Color(0.025f, 0.03f, 0.06f), y, c - half, c + half);
                return Alpha(fill, 0.84f);
            });
            canvas.Shape(0, 0, size, size, (x, y) => Mathf.Abs(BoxSdf(x, y, c, c, half, half, 8f)) - 0.6f,
                (x, y) => Alpha(Vertical(new Color(0.95f, 0.8f, 0.52f), new Color(0.42f, 0.32f, 0.18f), y, c - half, c + half), 0.8f));
            canvas.Shape(0, 0, size, size, (x, y) => Mathf.Abs(BoxSdf(x, y, c, c, half - 2f, half - 2f, 6.5f)) - 0.45f,
                (x, y) => new Color(1f, 1f, 1f, y < c - half + 6f ? 0.16f : 0.035f));
            if (!framed) return canvas;
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                {
                    Vector2 corner = new Vector2(c + sx * (half - 3.5f), c + sy * (half - 3.5f));
                    canvas.Segment(corner, corner + new Vector2(-sx * 22f, 0f), 1.3f, BrightGold, 2f, 0.3f);
                    canvas.Segment(corner, corner + new Vector2(0f, -sy * 22f), 1.3f, BrightGold, 2f, 0.3f);
                    Vector2 gem = new Vector2(c + sx * half, c + sy * half);
                    canvas.Shape(gem.x - 8, gem.y - 8, gem.x + 8, gem.y + 8, (x, y) => DiamondSdf(x, y, gem.x, gem.y, 4.5f, 4.5f),
                        (x, y) => BrightGold, 3f, 0.45f);
                }
            return canvas;
        }

        // ---------- Gauges ----------

        // 9-slice gauge housing (slice 12 x 12): bevelled gold rim around a dark recessed trough.
        private static Canvas BarFrame(int w, int h)
        {
            Canvas canvas = new Canvas(w, h);
            float cx = w * 0.5f, cy = h * 0.5f, hx = cx - 2f, hy = cy - 2f;
            canvas.Shape(0, 0, w, h, (x, y) => BoxSdf(x, y, cx, cy + 1f, hx, hy, 5f), (x, y) => new Color(0f, 0f, 0f, 0.5f), 2f, 0.5f);
            canvas.Shape(0, 0, w, h, (x, y) => BoxSdf(x, y, cx, cy, hx, hy, 5f),
                (x, y) => Alpha(Vertical(new Color(1f, 0.9f, 0.62f), new Color(0.38f, 0.25f, 0.09f), y, cy - hy, cy + hy), 1f));
            canvas.Shape(0, 0, w, h, (x, y) => BoxSdf(x, y, cx, cy, hx - 2.5f, hy - 2.5f, 3f), (x, y) =>
            {
                // Recessed trough: darker under the top lip.
                float depth = Mathf.Clamp01((y - (cy - hy + 2.5f)) / 6f);
                return Alpha(Color.Lerp(new Color(0f, 0f, 0.01f), new Color(0.04f, 0.05f, 0.09f), depth), 0.95f);
            });
            return canvas;
        }

        // Tinted per gauge: deep at the bottom, full colour above the middle, a pale band near the top.
        private static Canvas BarFill(int w, int h)
        {
            Canvas canvas = new Canvas(w, h);
            for (int y = 0; y < h; y++)
            {
                float t = (y + 0.5f) / h;
                float v = t < 0.18f ? Mathf.Lerp(0.82f, 1f, t / 0.18f) : t < 0.5f ? 1f : Mathf.Lerp(1f, 0.55f, (t - 0.5f) / 0.5f);
                for (int x = 0; x < w; x++) canvas.Set(x, y, new Color(v, v, v, 1f));
            }
            return canvas;
        }

        private static Canvas BarGloss(int w, int h)
        {
            Canvas canvas = new Canvas(w, h);
            for (int y = 0; y < h; y++)
            {
                float t = (y + 0.5f) / h;
                // A pale sheen on the upper half and a thin shadow along the bottom edge.
                float a = t < 0.42f ? 0.42f * Mathf.SmoothStep(1f, 0f, t / 0.42f) : 0f;
                Color c = t > 0.88f ? new Color(0f, 0f, 0f, 0.25f) : new Color(1f, 1f, 1f, a);
                for (int x = 0; x < w; x++) canvas.Set(x, y, c);
            }
            return canvas;
        }

        // ---------- Ability orbs ----------

        // Spiked gold ring laid over an orb; the centre stays clear.
        private static Canvas OrbRing(int size)
        {
            Canvas canvas = new Canvas(size, size);
            float c = size * 0.5f;
            const float inner = 70f, outer = 80f;
            canvas.Ring(c, c + 1.5f, outer, 3f, new Color(0f, 0f, 0f, 0.45f), 3f, 0.5f);
            for (int i = 0; i < 8; i++)
            {
                bool major = i % 2 == 0;
                float a = i * Mathf.PI / 4f - Mathf.PI / 2f;
                Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a)), side = new Vector2(-dir.y, dir.x);
                Vector2 centre = new Vector2(c, c);
                float reach = major ? 95f : 88f, width = major ? 11f : 7f;
                Vector2 p0 = centre + dir * (outer - 4f) + side * width, p1 = centre + dir * reach, p2 = centre + dir * (outer - 4f) - side * width;
                canvas.Shape(Mathf.Min(p0.x, Mathf.Min(p1.x, p2.x)), Mathf.Min(p0.y, Mathf.Min(p1.y, p2.y)),
                    Mathf.Max(p0.x, Mathf.Max(p1.x, p2.x)), Mathf.Max(p0.y, Mathf.Max(p1.y, p2.y)),
                    (x, y) => ConvexSdf(x, y, p0, p1, p2), (x, y) => Metal(x, y, c, c, outer));
            }
            canvas.Shape(0, 0, size, size, (x, y) => Mathf.Abs(Len(x - c, y - c) - (inner + outer) * 0.5f) - (outer - inner) * 0.5f,
                (x, y) => Metal(x, y, c, c, outer));
            canvas.Ring(c, c, outer - 3.5f, 0.6f, new Color(1f, 0.95f, 0.8f, 0.55f), 0f, 0f);
            canvas.Ring(c, c, inner + 0.5f, 1.4f, new Color(0.12f, 0.08f, 0.03f, 1f), 0f, 0f);
            canvas.Ring(c, c, inner - 1.5f, 0.9f, Alpha(BrightGold, 0.6f), 2f, 0.3f);
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI / 2f + Mathf.PI / 4f;
                float gx = c + Mathf.Cos(a) * 75f, gy = c + Mathf.Sin(a) * 75f;
                canvas.Shape(gx - 6, gy - 6, gx + 6, gy + 6, (x, y) => Len(x - gx, y - gy) - 3.2f,
                    (x, y) => Color.Lerp(new Color(0.75f, 0.88f, 1f), new Color(0.15f, 0.3f, 0.75f), Len(x - gx + 1f, y - gy + 1f) / 4f));
            }
            return canvas;
        }

        // Glowing sphere with an emblem per equipment item (Light/Healing/Rune staff, Food/Mana/Vitality ring).
        private static Canvas Orb(int size, int kind)
        {
            Canvas canvas = new Canvas(size, size);
            float c = size * 0.5f, r = size * 0.5f - 2f;
            Color tone = OrbColours[kind];
            canvas.Shape(0, 0, size, size, (x, y) => Len(x - c, y - c) - r, (x, y) =>
            {
                float d = Len(x - c, y - c + r * 0.12f) / r;
                Color core = Color.Lerp(tone * 0.9f, tone * 0.4f + Navy * 0.5f, Mathf.Pow(Mathf.Clamp01(d), 1.3f));
                core = Color.Lerp(core, new Color(0.01f, 0.015f, 0.04f), Mathf.SmoothStep(0.78f, 1.05f, d) * 0.65f);
                return Alpha(core, 1f);
            });
            Color emblem = Color.Lerp(tone, Color.white, 0.55f);
            switch (kind)
            {
                case 0:
                    canvas.Shape(0, 0, size, size, (x, y) => StarSdf(x, y, c, c, 50f, 7f, 4, 0f), (x, y) => Color.Lerp(Color.white, tone, Len(x - c, y - c) / 50f), 7f, 0.75f);
                    canvas.Shape(0, 0, size, size, (x, y) => StarSdf(x, y, c, c, 30f, 5f, 4, Mathf.PI / 4f), (x, y) => Alpha(emblem, 0.9f), 4f, 0.5f);
                    canvas.Ring(c, c, 36f, 1.4f, Alpha(emblem, 0.75f), 3f, 0.35f);
                    break;
                case 1:
                    canvas.Shape(0, 0, size, size, (x, y) => Mathf.Min(BoxSdf(x, y, c, c, 11f, 34f, 4f), BoxSdf(x, y, c, c, 34f, 11f, 4f)),
                        (x, y) => Color.Lerp(Color.white, emblem, Len(x - c, y - c) / 34f), 8f, 0.7f);
                    for (int i = 0; i < 4; i++)
                    {
                        float a = i * Mathf.PI / 2f + Mathf.PI / 4f;
                        float sx = c + Mathf.Cos(a) * 42f, sy = c + Mathf.Sin(a) * 42f;
                        canvas.Shape(sx - 10, sy - 10, sx + 10, sy + 10, (x, y) => StarSdf(x, y, sx, sy, 8f, 1.5f, 4, 0f), (x, y) => Alpha(emblem, 0.9f), 3f, 0.5f);
                    }
                    break;
                case 2:
                    canvas.Ring(c, c, 40f, 1.6f, Alpha(emblem, 0.85f), 4f, 0.45f);
                    canvas.Shape(0, 0, size, size, (x, y) => Mathf.Abs(DiamondSdf(x, y, c, c, 30f, 42f)) - 2.2f, (x, y) => emblem, 5f, 0.6f);
                    canvas.Shape(0, 0, size, size, (x, y) => DiamondSdf(x, y, c, c, 11f, 17f), (x, y) => Color.white, 7f, 0.8f);
                    canvas.Segment(new Vector2(c - 30f, c), new Vector2(c + 30f, c), 1.3f, Alpha(emblem, 0.8f), 2f, 0.3f);
                    break;
                default:
                    // Rings: a gold band set with the gem colour.
                    canvas.Shape(0, 0, size, size, (x, y) => Mathf.Abs(EllipseSdf(x, y, c, c + 10f, 32f, 24f)) - 5.5f,
                        (x, y) => Metal(x, y, c, c + 10f, 32f), 5f, 0.35f);
                    canvas.Shape(0, 0, size, size, (x, y) => DiamondSdf(x, y, c, c - 16f, 15f, 19f),
                        (x, y) => Alpha(Color.Lerp(Color.white, tone, x < c ? 0.35f : 0.85f) * (y > c - 16f ? 0.8f : 1f), 1f), 8f, 0.7f);
                    canvas.Shape(0, 0, size, size, (x, y) => DiamondSdf(x, y, c - 4f, c - 22f, 3.5f, 5f), (x, y) => new Color(1f, 1f, 1f, 0.9f));
                    break;
            }
            // Glass highlight and a soft rim from below.
            canvas.Shape(0, 0, size, size, (x, y) => RotatedEllipseSdf(x, y, c - r * 0.3f, c - r * 0.48f, r * 0.42f, r * 0.2f, -0.45f),
                (x, y) => new Color(1f, 1f, 1f, 0.22f * Mathf.Clamp01(1f - Len(x - (c - r * 0.3f), y - (c - r * 0.48f)) / (r * 0.42f))));
            canvas.Shape(0, 0, size, size, (x, y) => Mathf.Abs(Len(x - c, y - c) - (r - 3f)) - 1.5f,
                (x, y) => Alpha(tone, 0.55f * Mathf.Clamp01((y - c) / r + 0.2f)));
            return canvas;
        }

        // ---------- Portrait and cards ----------

        private static Canvas PortraitRing(int size)
        {
            Canvas canvas = new Canvas(size, size);
            float c = size * 0.5f;
            const float inner = 83f, outer = 94f;
            canvas.Ring(c, c + 2f, outer, 4f, new Color(0f, 0f, 0f, 0.5f), 4f, 0.5f);
            // Crest above the ring.
            Vector2 top = new Vector2(c, c - outer);
            canvas.Shape(top.x - 24, top.y - 20, top.x + 24, top.y + 14, (x, y) => DiamondSdf(x, y, top.x, top.y - 1f, 15f, 15f),
                (x, y) => Metal(x, y, top.x, top.y, 15f));
            canvas.Shape(top.x - 10, top.y - 10, top.x + 10, top.y + 10, (x, y) => DiamondSdf(x, y, top.x, top.y - 1f, 6.5f, 8f),
                (x, y) => Color.Lerp(new Color(0.8f, 0.9f, 1f), new Color(0.15f, 0.32f, 0.8f), Len(x - top.x + 2f, y - top.y + 3f) / 8f), 3f, 0.4f);
            canvas.Shape(0, 0, size, size, (x, y) => Mathf.Abs(Len(x - c, y - c) - (inner + outer) * 0.5f) - (outer - inner) * 0.5f,
                (x, y) => Metal(x, y, c, c, outer));
            canvas.Ring(c, c, outer - 3.5f, 0.7f, new Color(1f, 0.95f, 0.8f, 0.6f), 0f, 0f);
            canvas.Ring(c, c, (inner + outer) * 0.5f, 0.6f, new Color(0.35f, 0.22f, 0.06f, 0.8f), 0f, 0f);
            canvas.Ring(c, c, inner + 0.5f, 1.6f, new Color(0.1f, 0.06f, 0.02f, 1f), 0f, 0f);
            canvas.Ring(c, c, inner - 1.8f, 1f, Alpha(BrightGold, 0.55f), 2f, 0.3f);
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI / 6f;
                float gx = c + Mathf.Cos(a) * (inner + outer) * 0.5f, gy = c + Mathf.Sin(a) * (inner + outer) * 0.5f;
                canvas.Shape(gx - 4, gy - 4, gx + 4, gy + 4, (x, y) => Len(x - gx, y - gy) - 1.6f, (x, y) => new Color(1f, 0.95f, 0.75f, 0.9f));
            }
            return canvas;
        }

        // 9-slice troop card frame (slice 24), clear inside so the portrait shows through.
        private static Canvas TroopFrame(int size)
        {
            Canvas canvas = new Canvas(size, size);
            float c = size * 0.5f, half = c - 3f;
            canvas.Shape(0, 0, size, size, (x, y) => Mathf.Abs(BoxSdf(x, y, c, c, half + 0.8f, half + 0.8f, 7f)) - 0.8f, (x, y) => new Color(0f, 0f, 0f, 0.85f));
            canvas.Shape(0, 0, size, size, (x, y) => Mathf.Abs(BoxSdf(x, y, c, c, half - 1f, half - 1f, 6f)) - 1.3f,
                (x, y) => Alpha(Vertical(new Color(0.95f, 0.82f, 0.55f), new Color(0.42f, 0.3f, 0.12f), y, 0f, size), 1f));
            canvas.Shape(0, 0, size, size, (x, y) => Mathf.Abs(BoxSdf(x, y, c, c, half - 4.5f, half - 4.5f, 4f)) - 0.5f,
                (x, y) => new Color(1f, 0.92f, 0.7f, 0.25f));
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                {
                    Vector2 corner = new Vector2(c + sx * (half - 1f), c + sy * (half - 1f));
                    canvas.Shape(corner.x - 10, corner.y - 10, corner.x + 10, corner.y + 10, (x, y) => DiamondSdf(x, y, corner.x, corner.y, 5f, 5f),
                        (x, y) => Metal(x, y, corner.x, corner.y, 5f), 2f, 0.4f);
                }
            return canvas;
        }

        // ---------- Small parts ----------

        // 9-slice key cap (slice 10): raised dark key with a light rim and a deeper bottom edge.
        private static Canvas Keycap(int size)
        {
            Canvas canvas = new Canvas(size, size);
            float c = size * 0.5f, half = c - 3f;
            canvas.Shape(0, 0, size, size, (x, y) => BoxSdf(x, y, c, c + 1.5f, half, half, 5f), (x, y) => new Color(0.01f, 0.01f, 0.02f, 0.9f));
            canvas.Shape(0, 0, size, size, (x, y) => BoxSdf(x, y, c, c - 0.5f, half, half - 1f, 5f),
                (x, y) => Alpha(Vertical(new Color(0.27f, 0.29f, 0.36f), new Color(0.09f, 0.1f, 0.14f), y, c - half, c + half), 0.95f));
            canvas.Shape(0, 0, size, size, (x, y) => Mathf.Abs(BoxSdf(x, y, c, c - 0.5f, half, half - 1f, 5f)) - 0.5f,
                (x, y) => new Color(0.85f, 0.85f, 0.9f, y < c ? 0.55f : 0.25f));
            return canvas;
        }

        // Toast ribbon (slice 70 left/right): dark band fading at both ends between two gold hairlines.
        private static Canvas Banner(int w, int h)
        {
            Canvas canvas = new Canvas(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w, edge = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Min(u, 1f - u) / 0.27f));
                    bool line = y == 2 || y == h - 3;
                    Color c = line ? Alpha(BrightGold, 0.85f * edge) : new Color(0.02f, 0.03f, 0.07f, 0.78f * edge);
                    canvas.Set(x, y, c);
                }
            return canvas;
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

        private static Canvas CastleIcon(int size)
        {
            Canvas canvas = new Canvas(size, size);
            // Low crenellated wall, a tall central keep with a pointed roof, and an arched gate.
            Func<float, float, float> keep = (x, y) =>
            {
                float wall = BoxSdf(x, y, 32f, 45f, 25f, 13f, 0f);
                float crenels = float.MaxValue;
                for (int i = 0; i < 4; i++) crenels = Mathf.Min(crenels, BoxSdf(x, y, 10f + i * 14.7f, 30f, 3f, 3f, 0f));
                float tower = BoxSdf(x, y, 32f, 31f, 10f, 15f, 0f);
                float roof = ConvexSdf(x, y, new Vector2(19f, 17f), new Vector2(45f, 17f), new Vector2(32f, 2f));
                float gate = Mathf.Min(BoxSdf(x, y, 32f, 53f, 6f, 6f, 0f), Len(x - 32f, y - 47f) - 6f);
                float window = BoxSdf(x, y, 32f, 27f, 2f, 4f, 1f);
                return Mathf.Max(Mathf.Min(Mathf.Min(wall, crenels), Mathf.Min(tower, roof)), -Mathf.Min(gate, window));
            };
            canvas.Shape(0, 0, size, size, (x, y) => keep(x, y) - 2f, (x, y) => new Color(0.05f, 0.04f, 0.03f, 1f), 1.5f, 0.4f);
            canvas.Shape(0, 0, size, size, keep, (x, y) => Alpha(Vertical(new Color(1f, 0.95f, 0.85f), new Color(0.62f, 0.58f, 0.55f), y, 4f, 58f), 1f));
            return canvas;
        }
    }
}

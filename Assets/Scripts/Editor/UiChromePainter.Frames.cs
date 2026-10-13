using System;

namespace Lightbringer.EditorTools
{
    // Chamfered surfaces: dark lacquer with restrained gold edges. Buttons are painted at the exact height the USS
    // gives them, so their 9-slice never stretches vertically and the side lozenges stay true.
    public static partial class UiChromePainter
    {
        public const int ButtonPrimary = 0, ButtonSecondary = 1, ButtonDanger = 2;

        // Rectangle with 45-degree cut corners.
        public static float Chamfer(float x, float y, float cx, float cy, float hx, float hy, float cut)
        {
            float ax = Math.Abs(x - cx), ay = Math.Abs(y - cy);
            return Math.Max(Math.Max(ax - hx, ay - hy), (ax + ay - (hx + hy - cut)) * 0.7071f);
        }

        public static UiImage Button(int w, int h, int style)
        {
            UiImage img = new UiImage(w, h);
            float m = 3f, cut = h * 0.24f, cx = w * 0.5f, cy = h * 0.5f, hx = cx - m, hy = cy - m;
            Sdf body = delegate(float x, float y) { return Chamfer(x, y, cx, cy, hx, hy, cut); };
            Rgba top, bottom, edge;
            if (style == ButtonPrimary) { top = new Rgba(0.24f, 0.16f, 0.07f); bottom = new Rgba(0.08f, 0.05f, 0.025f); edge = Gold; }
            else if (style == ButtonDanger) { top = new Rgba(0.3f, 0.08f, 0.07f); bottom = new Rgba(0.11f, 0.03f, 0.03f); edge = new Rgba(0.86f, 0.42f, 0.34f); }
            else { top = new Rgba(0.13f, 0.17f, 0.27f); bottom = new Rgba(0.045f, 0.06f, 0.11f); edge = new Rgba(0.78f, 0.66f, 0.42f); }
            Glow(img, delegate(float x, float y) { return Chamfer(x, y, cx, cy + 1.5f, hx, hy, cut); }, new Rgba(0f, 0f, 0f, 0.55f), 1.6f);
            Shape(img, body, delegate(float x, float y)
            {
                float t = Clamp01((y - m) / (h - 2f * m));
                Rgba c = Rgba.Lerp(top, bottom, (float)Math.Pow(t, 0.8));
                // Warm light pooled along the middle of the primary button.
                float pool = (float)Math.Exp(-Math.Pow((t - 0.55f) / 0.32f, 2)) * (style == ButtonPrimary ? 0.16f : 0.05f);
                return new Rgba(c.R + pool * 1f, c.G + pool * 0.72f, c.B + pool * 0.3f, 0.97f);
            });
            // Rim: bright along the top edge, deeper below.
            Shape(img, delegate(float x, float y) { return Math.Abs(body(x, y)) - 0.75f; }, delegate(float x, float y)
            {
                float t = Clamp01((y - m) / (h - 2f * m));
                return style == ButtonPrimary ? Metal(0.1f + t * 0.8f) : Rgba.Lerp(edge, edge.Times(0.55f), t).Alpha(0.9f);
            });
            Stroke(img, delegate(float x, float y) { return Chamfer(x, y, cx, cy, hx - 3.5f, hy - 3.5f, cut - 1.5f); }, 0.45f,
                edge.Alpha(style == ButtonPrimary ? 0.32f : 0.16f));
            Shape(img, delegate(float x, float y) { return Segment(x, y, m + cut + 2f, m + 1.6f, w - m - cut - 2f, m + 1.6f, 0.5f); },
                delegate { return new Rgba(1f, 0.95f, 0.8f, style == ButtonPrimary ? 0.4f : 0.14f); });
            // Lozenges on the left and right points.
            for (int s = -1; s <= 1; s += 2)
            {
                float px = cx + s * (hx - 0.5f), size = style == ButtonPrimary ? 4.5f : 3.4f;
                Shape(img, delegate(float x, float y) { return Diamond(x, y, px, cy, size + 1f, size + 1f); }, delegate { return new Rgba(0.1f, 0.06f, 0.02f); });
                Shape(img, delegate(float x, float y) { return Diamond(x, y, px, cy, size, size); },
                    delegate(float x, float y) { return style == ButtonPrimary ? Metal((y - cy + size) / (2f * size)) : edge; });
            }
            return img;
        }

        // Lacquered panel: dark gradient, gold rim brightest along the top, accent strokes inside the cut corners.
        public static UiImage Panel(int size, float cut, float rim)
        {
            UiImage img = new UiImage(size, size);
            float m = 4f, c = size * 0.5f, half = c - m;
            Sdf body = delegate(float x, float y) { return Chamfer(x, y, c, c, half, half, cut); };
            Glow(img, delegate(float x, float y) { return Chamfer(x, y, c, c + 1.5f, half, half, cut); }, new Rgba(0f, 0f, 0f, 0.45f), 2f);
            Shape(img, body, delegate(float x, float y)
            {
                float t = Clamp01((y - m) / (size * 0.5f));
                Rgba fill = Rgba.Lerp(new Rgba(0.09f, 0.11f, 0.175f), new Rgba(0.04f, 0.05f, 0.09f), t);
                return fill.Times(1f + (Hash((int)x, (int)y, 5) - 0.5f) * 0.04f).Alpha(0.95f);
            });
            Shape(img, delegate(float x, float y) { return Math.Abs(body(x, y)) - 0.6f; }, delegate(float x, float y)
            {
                float t = Clamp01((y - m) / (size - 2f * m));
                return Gold.Alpha(rim * Lerp(1f, 0.45f, t));
            });
            Stroke(img, delegate(float x, float y) { return Chamfer(x, y, c, c, half - 4f, half - 4f, cut - 1.6f); }, 0.4f, Gold.Alpha(rim * 0.2f));
            for (int i = 0; i < 4; i++)
            {
                float sx = i % 2 == 0 ? -1f : 1f, sy = i < 2 ? -1f : 1f;
                // Inner stroke parallel to each cut corner.
                float ax = c + sx * (half - cut - 1f), ay = c + sy * (half - 5f), bx = c + sx * (half - 5f), by = c + sy * (half - cut - 1f);
                Shape(img, delegate(float x, float y) { return Segment(x, y, ax, ay, bx, by, 0.7f); }, delegate { return BrightGold.Alpha(rim * 0.9f); });
            }
            return img;
        }

        // Selectable tile. The active variant glows gold from its rim inward.
        public static UiImage Card(int size, bool active)
        {
            UiImage img = new UiImage(size, size);
            float m = 3f, c = size * 0.5f, half = c - m, cut = 7f;
            Sdf body = delegate(float x, float y) { return Chamfer(x, y, c, c, half, half, cut); };
            Shape(img, body, delegate(float x, float y)
            {
                float t = Clamp01((y - m) / (size - 2f * m));
                Rgba fill = active ? Rgba.Lerp(new Rgba(0.2f, 0.15f, 0.07f), new Rgba(0.09f, 0.07f, 0.04f), t)
                    : Rgba.Lerp(new Rgba(0.085f, 0.105f, 0.165f), new Rgba(0.04f, 0.05f, 0.085f), t);
                float rimGlow = active ? (float)Math.Exp(body(x, y) / 5f) * 0.35f : 0f;
                return new Rgba(fill.R + rimGlow, fill.G + rimGlow * 0.75f, fill.B + rimGlow * 0.3f, 0.94f);
            });
            Stroke(img, body, active ? 1.1f : 0.6f, active ? BrightGold : Gold.Alpha(0.34f));
            return img;
        }

        // Toast strip: dark band fading out at both ends with gold hairlines.
        public static UiImage Toast(int w, int h)
        {
            UiImage img = new UiImage(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w, fade = Smooth(0f, 0.28f, u) * Smooth(1f, 0.72f, u);
                    bool line = y == 1 || y == h - 2;
                    float band = Smooth(0f, 3f, Math.Min(y, h - 1 - y));
                    Rgba c = line ? Gold.Alpha(0.75f * fade) : new Rgba(0.03f, 0.04f, 0.08f, 0.82f * fade * band);
                    img.Set(x, y, c);
                }
            return img;
        }
    }
}

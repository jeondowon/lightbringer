using System;

namespace Lightbringer.EditorTools
{
    // Gilded navy chrome shared by the campaign screens and the battle HUD. Panels, frames, buttons and the ribbon
    // are 9-slice textures: the USS gives each one its slice widths.
    public static partial class UiChromePainter
    {
        // Light shard: a luminous gold-white crystal, the campaign currency.
        public static UiImage Shard(int size)
        {
            UiImage img = new UiImage(size, size);
            float k = size / 64f;
            float[] p = { 31 * k, 5 * k, 44 * k, 21 * k, 41 * k, 49 * k, 29 * k, 60 * k, 19 * k, 45 * k, 20 * k, 19 * k };
            Sdf crystal = delegate(float x, float y) { return Convex(x, y, p); };
            Glow(img, crystal, new Rgba(1f, 0.82f, 0.4f, 0.55f), 3.2f * k);
            Shape(img, delegate(float x, float y) { return crystal(x, y) - 1.2f * k; }, delegate { return new Rgba(0.42f, 0.27f, 0.08f); });
            Shape(img, crystal, delegate(float x, float y)
            {
                // Facets split along the long axis; the left side catches the light.
                float side = (x - 31 * k) * (60 - 5) * k - (y - 5 * k) * (29 - 31) * k;
                Rgba lit = Vertical(new Rgba(1f, 0.99f, 0.9f), new Rgba(1f, 0.85f, 0.5f), y, 5 * k, 60 * k);
                Rgba shade = Vertical(new Rgba(1f, 0.82f, 0.42f), new Rgba(0.78f, 0.52f, 0.18f), y, 5 * k, 60 * k);
                return side < 0f ? lit : shade;
            });
            Shape(img, delegate(float x, float y) { return Segment(x, y, 31 * k, 7 * k, 29 * k, 58 * k, 0.5f * k); }, delegate { return new Rgba(1f, 1f, 1f, 0.6f); });
            Shape(img, delegate(float x, float y) { return Star(x, y, 17 * k, 14 * k, 9 * k, 1.2f * k, 4, 0f, 8f); }, delegate { return new Rgba(1f, 1f, 0.95f); });
            return img;
        }

        // Gold ring for portraits and ability sockets; the centre stays transparent.
        public static UiImage Medallion(int size, int studs)
        {
            UiImage img = new UiImage(size, size);
            float c = size * 0.5f, r = c - 8f, band = size * 0.045f;
            Glow(img, delegate(float x, float y) { return Math.Abs(Circle(x, y, c, c + 1.5f, r - band)) - band; }, new Rgba(0f, 0f, 0f, 0.5f), 2f);
            Shape(img, delegate(float x, float y) { return Math.Abs(Circle(x, y, c, c, r - band)) - band - 0.8f; }, delegate { return DarkGold; });
            Shape(img, delegate(float x, float y) { return Math.Abs(Circle(x, y, c, c, r - band)) - band; },
                delegate(float x, float y) { return Metal((y - c + r) / (2f * r)); });
            Stroke(img, delegate(float x, float y) { return Circle(x, y, c, c, r - band * 1.1f); }, 0.5f, new Rgba(1f, 0.95f, 0.78f, 0.6f));
            Stroke(img, delegate(float x, float y) { return Circle(x, y, c, c, r - band * 2f - 1.5f); }, 0.9f, new Rgba(0.02f, 0.03f, 0.06f, 0.8f));
            for (int i = 0; i < studs; i++)
            {
                double a = -Math.PI / 2 + i * Math.PI * 2 / studs;
                float sx = c + (float)Math.Cos(a) * (r - band), sy = c + (float)Math.Sin(a) * (r - band);
                float s = band * (i % 2 == 0 ? 1.45f : 0.9f);
                Shape(img, delegate(float x, float y) { return Diamond(x, y, sx, sy, s + 1f, s + 1f); }, delegate { return DarkGold; });
                Shape(img, delegate(float x, float y) { return Diamond(x, y, sx, sy, s, s); },
                    delegate(float x, float y) { return Metal((y - sy + s) / (2f * s)); });
            }
            return img;
        }

        // Outline helper: band of the given half-width around an SDF's zero contour.
        public static void Stroke(UiImage img, Sdf sdf, float halfWidth, Rgba colour)
        {
            Shape(img, delegate(float x, float y) { return Math.Abs(sdf(x, y)) - halfWidth; }, delegate { return colour; });
        }
    }
}

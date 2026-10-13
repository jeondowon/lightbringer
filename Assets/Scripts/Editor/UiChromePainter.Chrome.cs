using System;

namespace Lightbringer.EditorTools
{
    // Gilded navy chrome shared by the campaign screens and the battle HUD. Panels, frames, buttons and the ribbon
    // are 9-slice textures: the USS gives each one its slice widths.
    public static partial class UiChromePainter
    {
        // Horizontal gloss tinted by the bar colour.
        public static UiImage Gloss(int w, int h)
        {
            UiImage img = new UiImage(w, h);
            for (int y = 0; y < h; y++)
            {
                float t = (y + 0.5f) / h;
                float k = t < 0.18f ? Lerp(0.95f, 1f, t / 0.18f) : t < 0.45f ? Lerp(1f, 0.8f, (t - 0.18f) / 0.27f) : Lerp(0.8f, 0.58f, (t - 0.45f) / 0.55f);
                float sheen = Smooth(0.3f, 0.1f, Math.Abs(t - 0.22f)) * 0.18f;
                for (int x = 0; x < w; x++) img.Set(x, y, new Rgba(Math.Min(1f, k + sheen), Math.Min(1f, k + sheen), Math.Min(1f, k + sheen), 1f));
            }
            return img;
        }

        // Ornamental rule: fading lines either side of a gold lozenge.
        public static UiImage Divider(int w, int h)
        {
            UiImage img = new UiImage(w, h);
            float c = w * 0.5f, cy = h * 0.5f;
            Shape(img, delegate(float x, float y) { return Math.Abs(y - cy) - 0.9f; }, delegate(float x, float y)
            {
                float d = Math.Abs(x - c);
                return BrightGold.Alpha(Smooth(c - 4f, c * 0.3f, d) * Smooth(14f, 26f, d) * 0.9f);
            });
            for (int s = -1; s <= 1; s += 2)
            {
                float side = s;
                Shape(img, delegate(float x, float y) { return Diamond(x, y, c + side * 20f, cy, 4f, 4f); }, delegate { return Gold; });
                Shape(img, delegate(float x, float y) { return Circle(x, y, c + side * 31f, cy, 1.6f); }, delegate { return Gold.Alpha(0.8f); });
            }
            Shape(img, delegate(float x, float y) { return Diamond(x, y, c, cy, 9f, h * 0.42f) - 0.5f; }, delegate { return DarkGold; });
            Shape(img, delegate(float x, float y) { return Diamond(x, y, c, cy, 7.5f, h * 0.36f); },
                delegate(float x, float y) { return Metal((y - cy + h * 0.36f) / (h * 0.72f)); });
            return img;
        }

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

        // Soft light rays for the victory screen; rotated at runtime.
        public static UiImage Rays(int size)
        {
            UiImage img = new UiImage(size, size);
            float c = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - c, dy = y + 0.5f - c, r = Len(dx, dy) / c;
                    double a = Math.Atan2(dy, dx);
                    float beam = (float)Math.Pow(0.5 + 0.5 * Math.Cos(a * 18), 16) * 0.7f + (float)Math.Pow(0.5 + 0.5 * Math.Cos(a * 7 + 1.3), 28) * 0.45f;
                    float fade = (float)Math.Pow(Smooth(0.7f, 0.06f, r), 1.4) * Smooth(0.04f, 0.2f, r);
                    float core = Smooth(0.45f, 0f, r) * 0.35f;
                    float alpha = Clamp01(beam * fade * 0.8f + core);
                    img.Set(x, y, new Rgba(1f, 0.9f, 0.62f, alpha));
                }
            return img;
        }

        // Victory: sunburst, laurel and a lit beacon flame. Defeat: cracked iron with the flame gone out.
        public static UiImage Crest(int size, bool victory)
        {
            UiImage img = new UiImage(size, size);
            float k = size / 512f, c = size * 0.5f, cy = c + 6f * k;
            Paint metal = delegate(float x, float y) { float t = (y - cy + 200f * k) / (400f * k); return victory ? Metal(t) : Iron(t); };
            Rgba edge = victory ? DarkGold : new Rgba(0.05f, 0.05f, 0.07f);
            if (victory)
            {
                // Two laurel branches rising from the base: a curved stem with paired leaves along it.
                const int Nodes = 10;
                float rr = 200f * k;
                for (int s = -1; s <= 1; s += 2)
                {
                    for (int i = 0; i < Nodes; i++)
                    {
                        double a0 = Math.PI / 2 + s * (0.42 + i * 0.2), a1 = Math.PI / 2 + s * (0.42 + (i + 1) * 0.2);
                        float ax = c + (float)Math.Cos(a0) * rr, ay = cy + (float)Math.Sin(a0) * rr;
                        float bx = c + (float)Math.Cos(a1) * rr, by = cy + (float)Math.Sin(a1) * rr;
                        float stem = (3.2f - i * 0.2f) * k;
                        Shape(img, delegate(float x, float y) { return Segment(x, y, ax, ay, bx, by, stem + 1.4f * k); }, delegate { return edge; });
                        Shape(img, delegate(float x, float y) { return Segment(x, y, ax, ay, bx, by, stem); }, metal);
                    }
                    for (int i = 0; i <= Nodes; i++)
                    {
                        double a = Math.PI / 2 + s * (0.42 + i * 0.2);
                        float nx = c + (float)Math.Cos(a) * rr, ny = cy + (float)Math.Sin(a) * rr;
                        float tx = -(float)Math.Sin(a) * s, ty = (float)Math.Cos(a) * s;
                        float along = (float)Math.Atan2(ty, tx), leaf = (28f - i * 1.2f) * k;
                        for (int side = -1; side <= 1; side += 2)
                        {
                            if (i == Nodes && side > 0) continue;
                            float ang = i == Nodes ? along : along + side * 0.72f;
                            float lx = nx + (float)Math.Cos(ang) * leaf * 0.92f, ly = ny + (float)Math.Sin(ang) * leaf * 0.92f;
                            Shape(img, delegate(float x, float y) { return RotatedEllipse(x, y, lx, ly, leaf, leaf * 0.36f, ang) - 1.6f * k; }, delegate { return edge; });
                            Shape(img, delegate(float x, float y) { return RotatedEllipse(x, y, lx, ly, leaf, leaf * 0.36f, ang); }, metal);
                            float vx = (float)Math.Cos(ang) * leaf * 0.75f, vy = (float)Math.Sin(ang) * leaf * 0.75f;
                            Shape(img, delegate(float x, float y) { return Segment(x, y, lx - vx, ly - vy, lx + vx, ly + vy, 0.7f * k); }, delegate { return DarkGold.Alpha(0.7f); });
                        }
                    }
                }
            }
            Sdf star = delegate(float x, float y) { return Star(x, y, c, cy, 186f * k, 118f * k, 8, -(float)Math.PI / 2, 3f); };
            Glow(img, star, victory ? new Rgba(1f, 0.85f, 0.45f, 0.4f) : new Rgba(0.45f, 0.05f, 0.05f, 0.35f), 10f * k);
            Shape(img, delegate(float x, float y) { return star(x, y) - 3f * k; }, delegate { return edge; });
            Shape(img, star, metal);
            Sdf disc = delegate(float x, float y) { return Circle(x, y, c, cy, 112f * k); };
            Shape(img, delegate(float x, float y) { return disc(x, y) - 10f * k; }, delegate { return edge; });
            Shape(img, delegate(float x, float y) { return disc(x, y) - 8f * k; }, metal);
            Shape(img, disc, delegate(float x, float y)
            {
                Rgba top = victory ? new Rgba(0.16f, 0.22f, 0.38f) : new Rgba(0.1f, 0.06f, 0.07f);
                Rgba bottom = victory ? new Rgba(0.03f, 0.05f, 0.11f) : new Rgba(0.03f, 0.02f, 0.03f);
                return Vertical(top, bottom, y, cy - 112f * k, cy + 112f * k);
            });
            // Brazier.
            Paint brazierPaint = delegate(float x, float y) { return victory ? Metal((y - cy - 30f * k) / (60f * k)) : Iron((y - cy - 30f * k) / (60f * k)); };
            Shape(img, delegate(float x, float y) { return Convex(x, y, c - 46f * k, cy + 28f * k, c + 46f * k, cy + 28f * k, c + 26f * k, cy + 62f * k, c - 26f * k, cy + 62f * k); }, brazierPaint);
            Shape(img, delegate(float x, float y) { return Box(x, y, c, cy + 76f * k, 8f * k, 14f * k, 2f * k); }, brazierPaint);
            Shape(img, delegate(float x, float y) { return Box(x, y, c, cy + 92f * k, 30f * k, 4f * k, 2f * k); }, brazierPaint);
            if (victory)
            {
                Sdf flame = delegate(float x, float y)
                {
                    float body = Circle(x, y, c, cy - 2f * k, 30f * k);
                    float tip = Convex(x, y, c - 27f * k, cy - 14f * k, c, cy - 96f * k, c + 27f * k, cy - 14f * k);
                    return Math.Max(Math.Min(body, tip), y - (cy + 26f * k));
                };
                Glow(img, flame, new Rgba(1f, 0.85f, 0.45f, 0.75f), 12f * k);
                Shape(img, flame, delegate(float x, float y) { return Vertical(new Rgba(1f, 1f, 0.92f), new Rgba(1f, 0.7f, 0.25f), y, cy - 90f * k, cy + 26f * k); });
                Shape(img, delegate(float x, float y) { return Circle(x, y, c, cy + 2f * k, 12f * k); }, delegate { return new Rgba(1f, 1f, 1f, 0.9f); });
            }
            else
            {
                Shape(img, delegate(float x, float y) { return Ellipse(x, y, c, cy + 22f * k, 30f * k, 8f * k); }, delegate { return new Rgba(0.55f, 0.1f, 0.05f, 0.85f); });
                // Thin smoke curl where the flame was.
                for (int i = 0; i < 18; i++)
                {
                    float t = i / 17f, sx = c + (float)Math.Sin(t * 5.5f) * 14f * k, sy = cy + 10f * k - t * 100f * k;
                    float rr = (5f + t * 9f) * k;
                    Shape(img, delegate(float x, float y) { return Circle(x, y, sx, sy, rr); }, delegate { return new Rgba(0.5f, 0.48f, 0.52f, 0.16f * (1f - t)); });
                }
                float[] crack = { 0.18f, -0.62f, 0.05f, -0.3f, 0.12f, -0.05f, -0.04f, 0.2f, 0.06f, 0.45f, -0.08f, 0.72f };
                for (int i = 0; i + 3 < crack.Length; i += 2)
                {
                    float ax = c + crack[i] * 200f * k, ay = cy + crack[i + 1] * 200f * k, bx = c + crack[i + 2] * 200f * k, by = cy + crack[i + 3] * 200f * k;
                    Shape(img, delegate(float x, float y) { return Segment(x, y, ax, ay, bx, by, 2.6f * k); }, delegate { return new Rgba(0.02f, 0.02f, 0.03f); });
                    Shape(img, delegate(float x, float y) { return Segment(x, y, ax + 2.5f * k, ay, bx + 2.5f * k, by, 0.8f * k); }, delegate { return new Rgba(0.6f, 0.15f, 0.08f, 0.8f); });
                }
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

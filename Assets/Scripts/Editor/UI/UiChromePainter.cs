using System;

namespace Lightbringer.EditorTools
{
    // Straight-alpha colour used by the UI chrome painter.
    public struct Rgba
    {
        public float R, G, B, A;

        public Rgba(float r, float g, float b, float a = 1f) { R = r; G = g; B = b; A = a; }

        public static Rgba Lerp(Rgba a, Rgba b, float t)
        {
            t = UiChromePainter.Clamp01(t);
            return new Rgba(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t, a.A + (b.A - a.A) * t);
        }

        public Rgba Alpha(float a) { return new Rgba(R, G, B, a); }
        public Rgba Times(float k) { return new Rgba(R * k, G * k, B * k, A); }
    }

    // RGBA float image with rows running top to bottom.
    public sealed class UiImage
    {
        public readonly int Width, Height;
        public readonly float[] Px;

        public UiImage(int width, int height) { Width = width; Height = height; Px = new float[width * height * 4]; }

        public Rgba Get(int x, int y)
        {
            int i = (y * Width + x) * 4;
            return new Rgba(Px[i], Px[i + 1], Px[i + 2], Px[i + 3]);
        }

        public void Set(int x, int y, Rgba c)
        {
            int i = (y * Width + x) * 4;
            Px[i] = c.R; Px[i + 1] = c.G; Px[i + 2] = c.B; Px[i + 3] = c.A;
        }

        public void Over(int x, int y, Rgba c, float coverage)
        {
            float a = UiChromePainter.Clamp01(c.A * coverage);
            if (a <= 0f) return;
            int i = (y * Width + x) * 4;
            float da = Px[i + 3], outA = a + da * (1f - a);
            if (outA <= 0f) return;
            float keep = da * (1f - a);
            Px[i] = (c.R * a + Px[i] * keep) / outA;
            Px[i + 1] = (c.G * a + Px[i + 1] * keep) / outA;
            Px[i + 2] = (c.B * a + Px[i + 2] * keep) / outA;
            Px[i + 3] = outA;
        }
    }

    public delegate float Sdf(float x, float y);
    public delegate Rgba Paint(float x, float y);

    // Procedural UI chrome for the campaign screens: treasure-map parchment, gilded panels, buttons and crests.
    // Pure C# (no UnityEngine) in C# 5 syntax so the same code can be run and inspected outside the Editor.
    public static partial class UiChromePainter
    {
        public static readonly Rgba Gold = new Rgba(0.86f, 0.69f, 0.36f);
        public static readonly Rgba BrightGold = new Rgba(1f, 0.91f, 0.64f);
        public static readonly Rgba DarkGold = new Rgba(0.42f, 0.28f, 0.11f);
        public static readonly Rgba Paper = new Rgba(0.91f, 0.82f, 0.63f);
        public static readonly Rgba Ink = new Rgba(0.22f, 0.13f, 0.07f);
        public static readonly Rgba RedInk = new Rgba(0.64f, 0.12f, 0.07f);

        // ---------- Math ----------

        public static float Clamp01(float v) { return v < 0f ? 0f : v > 1f ? 1f : v; }
        public static float Lerp(float a, float b, float t) { return a + (b - a) * Clamp01(t); }
        public static float Smooth(float e0, float e1, float v) { float t = Clamp01((v - e0) / (e1 - e0)); return t * t * (3f - 2f * t); }
        public static float Len(float x, float y) { return (float)Math.Sqrt(x * x + y * y); }

        public static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                return (h ^ (h >> 16)) / (float)uint.MaxValue;
            }
        }

        public static float Noise(float x, float y, int seed)
        {
            int ix = (int)Math.Floor(x), iy = (int)Math.Floor(y);
            float fx = x - ix, fy = y - iy;
            fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
            float a = Hash(ix, iy, seed), b = Hash(ix + 1, iy, seed), c = Hash(ix, iy + 1, seed), d = Hash(ix + 1, iy + 1, seed);
            return a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy;
        }

        public static float Fbm(float x, float y, int octaves, int seed)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Noise(x, y, seed + i * 31) * amp;
                norm += amp; amp *= 0.5f; x *= 2.03f; y *= 2.03f;
            }
            return sum / norm;
        }

        // ---------- Signed distances (negative inside) ----------

        public static float Box(float x, float y, float cx, float cy, float hx, float hy, float r)
        {
            float qx = Math.Abs(x - cx) - hx + r, qy = Math.Abs(y - cy) - hy + r;
            return Len(Math.Max(qx, 0f), Math.Max(qy, 0f)) + Math.Min(Math.Max(qx, qy), 0f) - r;
        }

        public static float Circle(float x, float y, float cx, float cy, float r) { return Len(x - cx, y - cy) - r; }

        public static float Diamond(float x, float y, float cx, float cy, float rx, float ry)
        {
            return (Math.Abs(x - cx) / rx + Math.Abs(y - cy) / ry - 1f) * Math.Min(rx, ry) * 0.7071f;
        }

        public static float Ellipse(float x, float y, float cx, float cy, float rx, float ry)
        {
            float nx = (x - cx) / rx, ny = (y - cy) / ry;
            return ((float)Math.Sqrt(nx * nx + ny * ny) - 1f) * Math.Min(rx, ry);
        }

        public static float Segment(float x, float y, float ax, float ay, float bx, float by, float halfWidth)
        {
            float px = x - ax, py = y - ay, vx = bx - ax, vy = by - ay;
            float h = Clamp01((px * vx + py * vy) / Math.Max(vx * vx + vy * vy, 1e-5f));
            return Len(px - vx * h, py - vy * h) - halfWidth;
        }

        // Convex polygon given as x0,y0,x1,y1,... in either winding.
        public static float Convex(float x, float y, params float[] p)
        {
            int n = p.Length / 2;
            float area = 0f;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                area += p[i * 2] * p[j * 2 + 1] - p[j * 2] * p[i * 2 + 1];
            }
            float sign = area > 0f ? 1f : -1f, distance = float.MinValue;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                float ex = p[j * 2] - p[i * 2], ey = p[j * 2 + 1] - p[i * 2 + 1], len = Math.Max(Len(ex, ey), 1e-5f);
                float nx = ey / len * sign, ny = -ex / len * sign;
                distance = Math.Max(distance, (x - p[i * 2]) * nx + (y - p[i * 2 + 1]) * ny);
            }
            return distance;
        }

        public static float Star(float x, float y, float cx, float cy, float outer, float inner, int points, float rotation, float sharpness = 6f)
        {
            float dx = x - cx, dy = y - cy;
            float k = Math.Abs((float)Math.Cos((Math.Atan2(dy, dx) - rotation) * points * 0.5f));
            return (Len(dx, dy) - Lerp(inner, outer, (float)Math.Pow(k, sharpness))) * 0.8f;
        }

        // ---------- Drawing ----------

        public static void Shape(UiImage image, float x0, float y0, float x1, float y1, Sdf sdf, Paint paint)
        {
            int ix0 = Math.Max(0, (int)x0 - 2), iy0 = Math.Max(0, (int)y0 - 2);
            int ix1 = Math.Min(image.Width, (int)x1 + 3), iy1 = Math.Min(image.Height, (int)y1 + 3);
            for (int y = iy0; y < iy1; y++)
                for (int x = ix0; x < ix1; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f, d = sdf(px, py);
                    float a = Clamp01(0.5f - d);
                    if (a <= 0.001f) continue;
                    image.Over(x, y, paint(px, py), a);
                }
        }

        public static void Shape(UiImage image, Sdf sdf, Paint paint) { Shape(image, 0, 0, image.Width, image.Height, sdf, paint); }

        public static void Fill(UiImage image, Sdf sdf, Rgba colour) { Shape(image, sdf, delegate { return colour; }); }

        // Soft halo or drop shadow outside a shape.
        public static void Glow(UiImage image, Sdf sdf, Rgba colour, float radius)
        {
            for (int y = 0; y < image.Height; y++)
                for (int x = 0; x < image.Width; x++)
                {
                    float d = sdf(x + 0.5f, y + 0.5f);
                    float a = d <= 0f ? 1f : (float)Math.Exp(-d / radius);
                    if (a > 0.003f) image.Over(x, y, colour, a);
                }
        }

        public static Rgba Vertical(Rgba top, Rgba bottom, float y, float y0, float y1) { return Rgba.Lerp(top, bottom, (y - y0) / (y1 - y0)); }

        // Polished gold lit from above: bright rim on top, warm mid, dark lower edge.
        public static Rgba Metal(float t)
        {
            t = Clamp01(t);
            if (t < 0.18f) return Rgba.Lerp(new Rgba(1f, 0.97f, 0.82f), BrightGold, t / 0.18f);
            if (t < 0.55f) return Rgba.Lerp(BrightGold, Gold, (t - 0.18f) / 0.37f);
            return Rgba.Lerp(Gold, DarkGold, (t - 0.55f) / 0.45f);
        }
    }
}

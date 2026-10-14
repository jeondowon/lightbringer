using System;

namespace Lightbringer.EditorTools
{
    // Ink compass rose for the battle minimap (the map page itself is StoryMapPage).
    public static partial class UiChromePainter
    {
        public static UiImage Compass(int size)
        {
            UiImage img = new UiImage(size, size);
            float k = size / 512f, c = size * 0.5f;
            Rgba ink = Ink.Alpha(0.88f), paper = new Rgba(0.95f, 0.88f, 0.71f, 0.92f);
            Fill(img, delegate(float x, float y) { return Circle(x, y, c, c, 226f * k); }, new Rgba(0.93f, 0.84f, 0.64f, 0.35f));
            Stroke(img, delegate(float x, float y) { return Circle(x, y, c, c, 230f * k); }, 2.2f * k, ink);
            Stroke(img, delegate(float x, float y) { return Circle(x, y, c, c, 214f * k); }, 1.1f * k, ink);
            Stroke(img, delegate(float x, float y) { return Circle(x, y, c, c, 150f * k); }, 1f * k, ink.Alpha(0.6f));
            for (int i = 0; i < 72; i++)
            {
                double a = i * Math.PI / 36;
                float r0 = (i % 9 == 0 ? 200f : i % 3 == 0 ? 206f : 209f) * k;
                float ax = c + (float)Math.Cos(a) * r0, ay = c + (float)Math.Sin(a) * r0;
                float bx = c + (float)Math.Cos(a) * 214f * k, by = c + (float)Math.Sin(a) * 214f * k;
                Shape(img, delegate(float x, float y) { return Segment(x, y, ax, ay, bx, by, 0.9f * k); }, delegate { return ink; });
            }
            // Diagonal points first, then the cardinal points over them.
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < 4; i++)
                {
                    double a = -Math.PI / 2 + i * Math.PI / 2 + (pass == 0 ? Math.PI / 4 : 0);
                    float tip = (pass == 0 ? 128f : 196f) * k, width = (pass == 0 ? 20f : 30f) * k;
                    float dx = (float)Math.Cos(a), dy = (float)Math.Sin(a), sx = -dy, sy = dx;
                    float tx = c + dx * tip, ty = c + dy * tip, w1x = c + sx * width + dx * width, w1y = c + sy * width + dy * width;
                    float w2x = c - sx * width + dx * width, w2y = c - sy * width + dy * width;
                    bool north = pass == 1 && i == 0;
                    Shape(img, delegate(float x, float y) { return Convex(x, y, c, c, tx, ty, w1x, w1y) - 0.8f * k; }, delegate { return ink; });
                    Shape(img, delegate(float x, float y) { return Convex(x, y, c, c, w2x, w2y, tx, ty) - 0.8f * k; }, delegate { return ink; });
                    Rgba dark = north ? RedInk : Ink;
                    Shape(img, delegate(float x, float y) { return Convex(x, y, c, c, tx, ty, w1x, w1y) + 0.6f * k; }, delegate { return dark; });
                    Shape(img, delegate(float x, float y) { return Convex(x, y, c, c, w2x, w2y, tx, ty) + 0.6f * k; }, delegate { return north ? new Rgba(0.92f, 0.62f, 0.5f) : paper; });
                }
            Fill(img, delegate(float x, float y) { return Circle(x, y, c, c, 16f * k); }, Ink);
            Fill(img, delegate(float x, float y) { return Circle(x, y, c, c, 12f * k); }, LampGold);
            // The N above the north point.
            float nx = c, ny = c - 222f * k + 2f * k, nh = 0f;
            nh = 9f * k;
            float nw = 7f * k;
            Fill(img, delegate(float x, float y)
            {
                return Math.Min(Math.Min(Segment(x, y, nx - nw, ny + nh, nx - nw, ny - nh, 1.6f * k), Segment(x, y, nx - nw, ny - nh, nx + nw, ny + nh, 1.6f * k)),
                    Segment(x, y, nx + nw, ny + nh, nx + nw, ny - nh, 1.6f * k));
            }, new Rgba(0.98f, 0.92f, 0.78f));
            return img;
        }
    }
}

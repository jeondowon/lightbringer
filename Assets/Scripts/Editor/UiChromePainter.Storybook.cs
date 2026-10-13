using System;

namespace Lightbringer.EditorTools
{
    // Storybook chrome, matching the prologue and title paintings: matte ink-night paper with a gouache stipple,
    // hand-drawn ink and faded-gold lines that wobble and overshoot at the corners, and gold kept for light and focus.
    // Panels and cards are drawn for -unity-slice-type: tiled, so their paper and lines repeat seamlessly with
    // period (size - 2 * slice); the USS slice widths must match the constants below.
    public static partial class UiChromePainter
    {
        public const int StoryPanelSize = 192, StoryPanelSlice = 32;
        public const int StoryDialogSize = 256, StoryDialogSlice = 48;
        public const int StoryCardSize = 128, StoryCardSlice = 24;

        // Palette sampled from the storybook paintings (Docs/ArtPass/ArtDirection_Storybook.md).
        public static readonly Rgba NightInk = Hex(0x1F2028);
        public static readonly Rgba NightInkLight = Hex(0x2A2A32);
        public static readonly Rgba Slate = Hex(0x565863);
        public static readonly Rgba Umber = Hex(0x3E2F27);
        public static readonly Rgba Parchment = Hex(0xB59672);
        public static readonly Rgba LampGold = Hex(0xDCB074);
        public static readonly Rgba LampGlow = Hex(0xF8D294);
        public static readonly Rgba Crimson = Hex(0x8A3A34);
        public static readonly Rgba InkBlack = Hex(0x0E0F14);

        public static Rgba Hex(int rgb)
        {
            return new Rgba(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);
        }

        public static int Mod(int a, int n) { int m = a % n; return m < 0 ? m + n : m; }

        // Value noise that repeats every `period` lattice cells.
        public static float PNoise(float x, float y, int period, int seed)
        {
            int ix = (int)Math.Floor(x), iy = (int)Math.Floor(y);
            float fx = x - ix, fy = y - iy;
            fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
            int x0 = Mod(ix, period), x1 = Mod(ix + 1, period), y0 = Mod(iy, period), y1 = Mod(iy + 1, period);
            float a = Hash(x0, y0, seed), b = Hash(x1, y0, seed), c = Hash(x0, y1, seed), d = Hash(x1, y1, seed);
            return a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy;
        }

        public static float PFbm(float x, float y, int period, int octaves, int seed)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += PNoise(x, y, period, seed + i * 31) * amp;
                norm += amp; amp *= 0.5f; x *= 2f; y *= 2f; period *= 2;
            }
            return sum / norm;
        }

        // Matte gouache paper: broad washes, mottling and a two-pixel stipple, periodic over `period` pixels
        // measured from `origin` so tiled slices join without seams.
        public static Rgba StoryPaper(float x, float y, Rgba baseColour, int origin, int period, int seed)
        {
            float u = (x - origin) / period, v = (y - origin) / period;
            float wash = PFbm(u * 2f, v * 2f, 2, 3, seed);
            float mottle = PFbm(u * 8f, v * 8f, 8, 2, seed + 7);
            int px = Mod((int)Math.Floor(x - origin), period), py = Mod((int)Math.Floor(y - origin), period);
            float stipple = Hash(px / 2, py / 2, seed + 3), fine = Hash(px, py, seed + 5);
            // Broken gouache: lighter flecks where the paper tooth shows through, darker where pigment pooled.
            float tooth = PNoise(u * 24f, v * 24f, 24, seed + 9);
            float fleck = Smooth(0.9f, 0.97f, Hash(px / 2, py / 2, seed + 13)) * Smooth(0.45f, 0.7f, tooth);
            float k = 1f + (wash - 0.5f) * 0.3f + (mottle - 0.5f) * 0.24f + (stipple - 0.5f) * 0.2f + (fine - 0.5f) * 0.06f
                + (tooth - 0.5f) * 0.16f + fleck * 0.35f;
            // Gouache settles warmer in the darker pools.
            Rgba c = baseColour.Times(k);
            return Rgba.Lerp(c, new Rgba(c.R * 1.1f, c.G * 1.0f, c.B * 0.88f, c.A), Clamp01((0.55f - wash) * 2f));
        }

        // A hand-drawn straight line: its centre drifts, its width breathes and the ink thins in places.
        // horizontal: the line runs along x at `at`, from a to b. Wobble repeats every `period` pixels from `origin`.
        public static void InkLine(UiImage img, bool horizontal, float at, float a, float b, float width, Rgba colour,
            int origin, int period, int seed)
        {
            float lo = Math.Min(a, b), hi = Math.Max(a, b);
            float pad = width * 2f + 3f;
            float x0 = horizontal ? lo - 2f : at - pad, x1 = horizontal ? hi + 2f : at + pad;
            float y0 = horizontal ? at - pad : lo - 2f, y1 = horizontal ? at + pad : hi + 2f;
            Sdf sdf = delegate(float x, float y)
            {
                float along = horizontal ? x : y, across = horizontal ? y : x;
                float t = (along - origin) / period * 4f;
                float drift = (PNoise(t, 3.5f, 4, seed) - 0.5f) * 1.1f + (PNoise(t * 4f, 7.5f, 16, seed + 1) - 0.5f) * 0.35f;
                float half = width * (0.75f + 0.5f * PNoise(t * 2f, 1.5f, 8, seed + 2));
                half *= Smooth(0f, 5f, along - lo) * Smooth(0f, 5f, hi - along) * 0.7f + 0.3f;
                return Math.Max(Math.Abs(across - at - drift) - half, Math.Max(lo - along, along - hi));
            };
            Shape(img, x0, y0, x1, y1, sdf, delegate(float x, float y)
            {
                float along = horizontal ? x : y;
                float dry = PNoise((along - origin) / period * 12f, 5.5f, 12, seed + 4);
                return colour.Alpha(colour.A * (0.72f + 0.28f * Smooth(0.25f, 0.6f, dry)));
            });
        }

        // Rectangle of ink lines inset by `inset`, overshooting each corner like a pen-ruled frame.
        public static void InkFrame(UiImage img, int size, float inset, float overshoot, float width, Rgba colour,
            int origin, int period, int seed)
        {
            float lo = inset, hi = size - inset;
            InkLine(img, true, lo, lo - overshoot, hi + overshoot, width, colour, origin, period, seed);
            InkLine(img, true, hi, lo - overshoot, hi + overshoot, width, colour, origin, period, seed + 10);
            InkLine(img, false, lo, lo - overshoot, hi + overshoot, width, colour, origin, period, seed + 20);
            InkLine(img, false, hi, lo - overshoot, hi + overshoot, width, colour, origin, period, seed + 30);
        }

        // The four-point beacon star from the logo flourish.
        public static void LampStar(UiImage img, float cx, float cy, float radius, Rgba colour, bool glow)
        {
            Sdf star = delegate(float x, float y) { return Star(x, y, cx, cy, radius, radius * 0.16f, 4, 0f, 7f); };
            if (glow) Glow(img, star, colour.Alpha(0.35f), radius * 0.35f);
            Shape(img, cx - radius - 2f, cy - radius - 2f, cx + radius + 2f, cy + radius + 2f, star, delegate { return colour; });
        }

        // Paper panel. ornate adds the double rule and corner stars used by dialogs.
        public static UiImage StoryPanel(int size, int slice, bool ornate)
        {
            UiImage img = new UiImage(size, size);
            int period = size - 2 * slice;
            float m = 3f;
            Sdf body = delegate(float x, float y)
            {
                float rough = (PNoise((x + y - 2 * slice) / period * 6f, 2.5f, 6, 41) - 0.5f) * 1.6f;
                return Box(x, y, size * 0.5f, size * 0.5f, size * 0.5f - m, size * 0.5f - m, 2f) + rough;
            };
            Shape(img, body, delegate(float x, float y)
            {
                // Edges darken like paint dragged to the rim of the sheet.
                float rim = Smooth(-14f, 0f, body(x, y));
                Rgba paper = StoryPaper(x, y, NightInk, slice, period, 17);
                return Rgba.Lerp(paper, InkBlack, rim * 0.55f).Alpha(0.95f);
            });
            float outer = ornate ? 12f : 10f;
            // Ink contour first, then the faded-gold rule drawn just inside it.
            InkFrame(img, size, outer - 2.2f, 0f, 1.3f, InkBlack.Alpha(0.75f), slice, period, 91);
            InkFrame(img, size, outer, ornate ? 7f : 5f, ornate ? 1.25f : 1.05f, Parchment.Alpha(0.85f), slice, period, 3);
            if (ornate)
            {
                InkFrame(img, size, outer + 6f, 0f, 0.55f, Parchment.Alpha(0.45f), slice, period, 63);
                float s = outer + 0.5f;
                LampStar(img, s, s, 9f, LampGold, false);
                LampStar(img, size - s, s, 9f, LampGold, false);
                LampStar(img, s, size - s, 9f, LampGold, false);
                LampStar(img, size - s, size - s, 9f, LampGold, false);
            }
            else
            {
                InkFrame(img, size, outer + 4f, 0f, 0.4f, Parchment.Alpha(0.22f), slice, period, 63);
            }
            return img;
        }

        // Selectable tile: a lighter sheet with a slate rule; active turns the rule to lamp gold with a warm wash.
        public static UiImage StoryCard(int size, int slice, bool active)
        {
            UiImage img = new UiImage(size, size);
            int period = size - 2 * slice;
            float m = 2f;
            Sdf body = delegate(float x, float y)
            {
                float rough = (PNoise((x + y - 2 * slice) / period * 4f, 4.5f, 4, 47) - 0.5f) * 1.2f;
                return Box(x, y, size * 0.5f, size * 0.5f, size * 0.5f - m, size * 0.5f - m, 1.5f) + rough;
            };
            Rgba baseColour = active ? Hex(0x33281F) : NightInkLight;
            Shape(img, body, delegate(float x, float y)
            {
                float d = body(x, y);
                Rgba paper = StoryPaper(x, y, baseColour, slice, period, active ? 29 : 23);
                if (active)
                {
                    // Lamplight seeping in from the rule.
                    float warm = (float)Math.Exp(d / 9f) * 0.22f;
                    paper = new Rgba(paper.R + warm * 0.86f, paper.G + warm * 0.6f, paper.B + warm * 0.3f, paper.A);
                }
                return Rgba.Lerp(paper, InkBlack, Smooth(-8f, 0f, d) * 0.4f).Alpha(0.93f);
            });
            InkFrame(img, size, 4.5f, 0f, 1f, InkBlack.Alpha(0.6f), slice, period, 93);
            InkFrame(img, size, 6.5f, active ? 3f : 0f, active ? 1.05f : 0.75f, active ? LampGold : Slate.Times(1.45f).Alpha(0.9f), slice, period, 5);
            if (active)
            {
                InkFrame(img, size, 10f, 0f, 0.4f, LampGold.Alpha(0.45f), slice, period, 71);
                LampStar(img, 6.5f, 6.5f, 5f, LampGlow, true);
                LampStar(img, size - 6.5f, size - 6.5f, 5f, LampGlow, true);
            }
            return img;
        }

        // Banner-strip button at its USS height: umber (primary), night ink (secondary) or crimson ink (danger),
        // matte paper, a ruled border and small stars at both ends. No metal or gloss.
        public static UiImage StoryButton(int w, int h, int style)
        {
            UiImage img = new UiImage(w, h);
            float cx = w * 0.5f, cy = h * 0.5f, m = 3f, notch = h * 0.22f;
            Rgba fill, line;
            if (style == ButtonPrimary) { fill = Hex(0x4A3626); line = LampGold; }
            else if (style == ButtonDanger) { fill = Hex(0x4A1F1C); line = Hex(0xC8796C); }
            else { fill = NightInkLight; line = Parchment; }
            // Ends cut into shallow swallowtail points like a ribbon.
            Sdf body = delegate(float x, float y)
            {
                float box = Box(x, y, cx, cy, cx - m, cy - m, 1f);
                float ax = Math.Min(x - m, w - m - x);
                float tail = (notch - Math.Abs(y - cy) * 0.9f) - ax;
                float rough = (Noise(x * 0.25f, y * 0.25f, 51) - 0.5f) * 0.9f;
                return Math.Max(box, tail) + rough;
            };
            Glow(img, delegate(float x, float y) { return body(x, y - 1.5f); }, new Rgba(0f, 0f, 0f, 0.5f), 1.5f);
            Shape(img, body, delegate(float x, float y)
            {
                Rgba paper = StoryPaper(x, y, fill, 0, 64, 31 + style);
                return Rgba.Lerp(paper, InkBlack, Smooth(-7f, 0f, body(x, y)) * 0.5f).Alpha(0.97f);
            });
            float inset = 5.5f, lineAlpha = style == ButtonSecondary ? 0.6f : 0.85f;
            InkLine(img, true, inset, notch + inset + 2f, w - notch - inset - 2f, 0.7f, line.Alpha(lineAlpha), 0, 64, 7 + style);
            InkLine(img, true, h - inset, notch + inset + 2f, w - notch - inset - 2f, 0.7f, line.Alpha(lineAlpha), 0, 64, 17 + style);
            // Only the primary call to action carries the beacon stars; on narrow back/secondary buttons they would
            // crowd the label, so those get a small ink dot in each swallowtail instead.
            for (int s = -1; s <= 1; s += 2)
            {
                if (style == ButtonPrimary)
                {
                    LampStar(img, cx + s * (cx - m - notch - 7f), cy, h * 0.13f, LampGlow, true);
                    continue;
                }
                float dx = cx + s * (cx - m - notch - 5f);
                Shape(img, delegate(float x, float y) { return Circle(x, y, dx, cy, 1.6f); }, delegate { return line.Alpha(0.75f); });
            }
            return img;
        }

        // Ruled divider: a tapering pen line with the beacon star and two dots in the middle.
        public static UiImage StoryDivider(int w, int h)
        {
            UiImage img = new UiImage(w, h);
            float c = w * 0.5f, cy = h * 0.5f;
            for (int s = -1; s <= 1; s += 2)
            {
                float side = s;
                Shape(img, delegate(float x, float y)
                {
                    float d = Math.Abs(x - c);
                    float half = Lerp(0.95f, 0.2f, (d - 16f) / (c - 20f));
                    float drift = (Noise(x * 0.04f, 1.5f, 9) - 0.5f) * 0.8f;
                    return Math.Max(Math.Abs(y - cy - drift) - half, Math.Max(16f - d, d - (c - 4f))) + (side * (x - c) < 0f ? 99f : 0f);
                }, delegate(float x, float y) { return Parchment.Alpha(0.85f * Smooth(c - 4f, c * 0.35f, Math.Abs(x - c))); });
                Shape(img, delegate(float x, float y) { return Circle(x, y, c + side * 22f, cy, 1.7f); }, delegate { return Parchment; });
            }
            LampStar(img, c, cy, h * 0.42f, LampGold, true);
            return img;
        }

        // Matte gauge fill (tinted in USS): horizontal brush streaks, slightly denser paint along the top.
        public static UiImage StoryGaugeFill(int w, int h)
        {
            UiImage img = new UiImage(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float t = (y + 0.5f) / h;
                    float streak = Noise(x * 0.02f, y * 0.45f, 13) - 0.5f;
                    float grain = Hash(x / 2, y / 2, 14) - 0.5f;
                    float k = 0.86f + streak * 0.2f + grain * 0.06f + Smooth(0.5f, 0.05f, t) * 0.08f - Smooth(0.6f, 1f, t) * 0.12f;
                    img.Set(x, y, new Rgba(k, k, k, 1f));
                }
            return img;
        }

        // Toast: a dry-brush ink wash with ragged ends and faded-gold rules.
        public static UiImage StoryToast(int w, int h)
        {
            UiImage img = new UiImage(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w;
                    float ragged = (Noise(x * 0.05f, y * 0.4f, 21) - 0.5f) * 0.12f;
                    float fade = Smooth(0f, 0.22f, u + ragged) * Smooth(1f, 0.78f, u - ragged);
                    float band = Smooth(0f, 4f, Math.Min(y, h - 1 - y) + ragged * 20f);
                    Rgba c = StoryPaper(x, y, NightInk, 0, 64, 19);
                    img.Set(x, y, c.Alpha(0.9f * fade * band));
                }
            InkLine(img, true, 2.5f, w * 0.12f, w * 0.88f, 0.55f, Parchment.Alpha(0.7f), 0, 128, 23);
            InkLine(img, true, h - 2.5f, w * 0.12f, w * 0.88f, 0.55f, Parchment.Alpha(0.7f), 0, 128, 27);
            return img;
        }

        // Soft lamplight behind the victory emblem (the old sunburst is gone).
        public static UiImage StoryGlow(int size)
        {
            UiImage img = new UiImage(size, size);
            float c = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float r = Len(x + 0.5f - c, y + 0.5f - c) / c;
                    float a = (float)Math.Pow(Smooth(1f, 0f, r), 2.6) * 0.5f + Smooth(0.35f, 0f, r) * 0.2f;
                    float mottle = Fbm(x * 0.02f, y * 0.02f, 3, 33);
                    // Dither by a couple of 8-bit steps so the long falloff never bands.
                    float dither = (Hash(x, y, 35) - 0.5f) * 2.5f / 255f;
                    img.Set(x, y, new Rgba(1f, 0.82f, 0.52f, Clamp01(a * (0.8f + mottle * 0.4f) + dither)));
                }
            return img;
        }

        // Stage seal at the top of the battle HUD: a dark paper disc in a hand-ruled faded-gold ring, with the
        // beacon star at the four points. Matte, so it no longer reads as a metal medal.
        public static UiImage StorySeal(int size)
        {
            UiImage img = new UiImage(size, size);
            float c = size * 0.5f, k = size / 256f, r = 100f * k;
            Sdf disc = delegate(float x, float y) { return Circle(x, y, c, c, r) + (Noise(x * 0.08f, y * 0.08f, 95) - 0.5f) * 2f * k; };
            Glow(img, delegate(float x, float y) { return disc(x, y - 3f * k); }, new Rgba(0f, 0f, 0f, 0.55f), 6f * k);
            Shape(img, disc, delegate(float x, float y)
            {
                Rgba paper = StoryPaper(x, y, NightInk, 0, 128, 97);
                // Faint lamplight pooled in the upper half, where the stage name sits.
                float warm = Smooth(r, 0f, Len(x - c, y - c * 0.92f)) * 0.12f;
                paper = new Rgba(paper.R + warm, paper.G + warm * 0.75f, paper.B + warm * 0.4f, 1f);
                return Rgba.Lerp(paper, InkBlack, Smooth(-16f * k, 0f, disc(x, y)) * 0.6f);
            });
            for (int ring = 0; ring < 3; ring++)
            {
                float rr = r - (ring == 0 ? 7f : ring == 1 ? 13f : -1.5f) * k;
                float hw = (ring == 0 ? 2.2f : ring == 1 ? 0.8f : 1.4f) * k;
                Rgba col = ring == 0 ? LampGold.Alpha(0.9f) : ring == 1 ? Parchment.Alpha(0.45f) : InkBlack.Alpha(0.8f);
                int seed = 101 + ring * 7;
                Shape(img, delegate(float x, float y)
                {
                    float a = ((float)Math.Atan2(y - c, x - c) + 3.1416f) / 6.2832f;
                    float wob = (PNoise(a * 20f, 2.5f, 20, seed) - 0.5f) * 1.6f * k;
                    return Math.Abs(Len(x - c, y - c) - rr - wob) - hw * (0.7f + 0.6f * PNoise(a * 10f, 4.5f, 10, seed + 1));
                }, delegate { return col; });
            }
            for (int i = 0; i < 4; i++)
            {
                double a = -Math.PI / 2 + i * Math.PI / 2;
                float sx = c + (float)Math.Cos(a) * (r - 7f * k), sy = c + (float)Math.Sin(a) * (r - 7f * k);
                Shape(img, delegate(float x, float y) { return Circle(x, y, sx, sy, 7f * k); }, delegate { return InkBlack; });
                LampStar(img, sx, sy, (i == 0 ? 13f : 9f) * k, i == 0 ? LampGlow : LampGold, i == 0);
            }
            return img;
        }

        // Top HUD band behind the two stronghold gauges: an ink wash with brushed ends between two faded-gold rules.
        // The band fills the middle `band` pixels; the transparent margins let the seal overhang it.
        public static UiImage StoryHudBanner(int w, int h, int band)
        {
            UiImage img = new UiImage(w, h);
            float top = (h - band) * 0.5f, bottom = top + band;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w;
                    // Ends dissolve like a dry wash: a soft fade that wanders gently with height, no streaks.
                    float ragged = (Noise(3.5f, y * 0.06f, 111) - 0.5f) * 0.03f;
                    float fade = Smooth(0f, 0.1f, u + ragged) * Smooth(1f, 0.9f, u - ragged);
                    float edge = Math.Min(y + 0.5f - top, bottom - y - 0.5f) + (Noise(x * 0.1f, y * 0.1f, 113) - 0.5f) * 3f;
                    float inside = Smooth(0f, 3f, edge);
                    if (inside <= 0f || fade <= 0f) continue;
                    Rgba paper = StoryPaper(x, y, NightInk, 0, 128, 115);
                    paper = Rgba.Lerp(paper, InkBlack, Smooth(10f, 0f, edge) * 0.5f);
                    img.Set(x, y, paper.Alpha(0.9f * fade * inside));
                }
            for (int s = 0; s < 2; s++)
            {
                float ly = s == 0 ? top + 4.5f : bottom - 4.5f;
                Shape(img, delegate(float x, float y)
                {
                    float drift = (Noise(x * 0.02f, ly, 117 + s) - 0.5f) * 1.2f;
                    return Math.Abs(y - ly - drift) - 0.75f;
                }, delegate(float x, float y)
                {
                    float u = (x + 0.5f) / w;
                    return Parchment.Alpha(0.8f * Smooth(0.08f, 0.25f, u) * Smooth(0.92f, 0.75f, u));
                });
            }
            return img;
        }

        // Battle minimap page: muted parchment with soft mottling and a darker rim, framed by the HUD panel.
        // The map view rules its own ink border on top.
        public static UiImage StoryMapPage(int w, int h, int seed)
        {
            UiImage img = new UiImage(w, h);
            Rgba baseColour = Rgba.Lerp(Paper, Parchment, 0.45f);
            Sdf page = delegate(float x, float y)
            {
                return Box(x, y, w * 0.5f, h * 0.5f, w * 0.5f - 1f, h * 0.5f - 1f, 1.5f) + (Noise(x * 0.2f, y * 0.2f, seed) - 0.5f) * 1.2f;
            };
            Shape(img, page, delegate(float x, float y)
            {
                float mottle = Fbm(x * 0.03f, y * 0.03f, 4, seed + 1), stain = Fbm(x * 0.012f + 4f, y * 0.012f, 3, seed + 2);
                float grain = Hash((int)x / 2, (int)y / 2, seed + 3) - 0.5f;
                Rgba c = baseColour.Times(1f + (mottle - 0.5f) * 0.18f + grain * 0.06f);
                c = Rgba.Lerp(c, Hex(0x8A6A48), Smooth(0.58f, 0.75f, stain) * 0.25f);
                // Rim darkened like old paper.
                return Rgba.Lerp(c, Hex(0x5A4430), Smooth(-22f, 0f, page(x, y)) * 0.55f);
            });
            return img;
        }

        // Small paper tag for the HUD chips (assault count, fronts): drawn at its USS height and 9-sliced 10 px.
        // kind 0 plain, 1 selected (lamp-gold rule), 2 alert (crimson wash and rule).
        public static UiImage StoryTag(int w, int h, int kind)
        {
            UiImage img = new UiImage(w, h);
            Sdf body = delegate(float x, float y)
            {
                return Box(x, y, w * 0.5f, h * 0.5f, w * 0.5f - 1.5f, h * 0.5f - 1.5f, 1.5f) + (Noise(x * 0.3f, y * 0.3f, 121) - 0.5f) * 0.8f;
            };
            Rgba fill = kind == 2 ? Hex(0x4A1E1C) : kind == 1 ? Hex(0x30271E) : NightInkLight;
            Rgba rule = kind == 2 ? Hex(0xC8796C) : kind == 1 ? LampGold : Parchment.Alpha(0.6f);
            Shape(img, body, delegate(float x, float y)
            {
                Rgba paper = StoryPaper(x, y, fill, 0, 64, 123 + kind);
                return Rgba.Lerp(paper, InkBlack, Smooth(-5f, 0f, body(x, y)) * 0.5f).Alpha(0.92f);
            });
            InkLine(img, true, 3.5f, 4f, w - 4f, 0.55f, rule, 0, 64, 125);
            InkLine(img, true, h - 3.5f, 4f, w - 4f, 0.55f, rule, 0, 64, 127);
            InkLine(img, false, 3.5f, 4f, h - 4f, 0.55f, rule, 0, 64, 129);
            InkLine(img, false, w - 3.5f, 4f, h - 4f, 0.55f, rule, 0, 64, 131);
            return img;
        }

        // HUD status icons as small gouache drawings with an ink contour: 0 ally shield, 1 enemy skull, 2 crossed swords.
        public static UiImage StoryIcon(int size, int kind)
        {
            UiImage img = new UiImage(size, size);
            float k = size / 64f;
            if (kind == 0)
            {
                Sdf shield = delegate(float x, float y)
                {
                    return Math.Min(Box(x, y, 32f * k, 18.5f * k, 21f * k, 10.5f * k, 3f * k),
                        Convex(x, y, 11f * k, 28f * k, 53f * k, 28f * k, 32f * k, 59f * k));
                };
                Shape(img, delegate(float x, float y) { return shield(x, y) - 3.2f * k; }, delegate { return InkBlack; });
                Shape(img, shield, delegate(float x, float y) { return StoryPaper(x, y, Vertical(Hex(0x8AA2C8), Hex(0x445C88), y, 8f * k, 58f * k), 0, 64, 133); });
                Shape(img, delegate(float x, float y) { return Math.Abs(shield(x, y) + 3.5f * k) - 0.9f * k; }, delegate { return Parchment.Alpha(0.85f); });
                LampStar(img, 32f * k, 29f * k, 13f * k, LampGlow, false);
            }
            else if (kind == 1)
            {
                Sdf skull = delegate(float x, float y)
                {
                    float head = Math.Min(Ellipse(x, y, 32f * k, 27f * k, 21f * k, 19f * k), Box(x, y, 32f * k, 46f * k, 11f * k, 9f * k, 3f * k));
                    float eyes = Math.Min(Ellipse(x, y, 24f * k, 30f * k, 6f * k, 6.5f * k), Ellipse(x, y, 40f * k, 30f * k, 6f * k, 6.5f * k));
                    float nose = Diamond(x, y, 32f * k, 40f * k, 2.6f * k, 3.5f * k);
                    float teeth = Math.Min(Box(x, y, 27.5f * k, 52f * k, 0.9f * k, 4f * k, 0f), Box(x, y, 36.5f * k, 52f * k, 0.9f * k, 4f * k, 0f));
                    return Math.Max(head, -Math.Min(eyes, Math.Min(nose, teeth)));
                };
                Shape(img, delegate(float x, float y) { return Math.Min(Ellipse(x, y, 32f * k, 27f * k, 21f * k, 19f * k), Box(x, y, 32f * k, 46f * k, 11f * k, 9f * k, 3f * k)) - 3f * k; }, delegate { return InkBlack; });
                Shape(img, skull, delegate(float x, float y) { return StoryPaper(x, y, Vertical(Hex(0xE2A08E), Hex(0xA8463C), y, 8f * k, 56f * k), 0, 64, 135); });
            }
            else
            {
                for (int s = -1; s <= 1; s += 2)
                {
                    float tx = 32f * k + s * 22f * k, ty = 8f * k, px = 32f * k - s * 20f * k, py = 56f * k;
                    float dx = px - tx, dy = py - ty, len = Len(dx, dy);
                    dx /= len; dy /= len;
                    float gx = tx + dx * 38f * k, gy = ty + dy * 38f * k, sx = -dy * 9f * k, sy = dx * 9f * k;
                    Shape(img, delegate(float x, float y) { return Segment(x, y, tx, ty, gx, gy, 3.6f * k); }, delegate { return InkBlack; });
                    Shape(img, delegate(float x, float y) { return Segment(x, y, gx - sx, gy - sy, gx + sx, gy + sy, 3.4f * k); }, delegate { return InkBlack; });
                    Shape(img, delegate(float x, float y) { return Segment(x, y, gx, gy, px, py, 3.4f * k); }, delegate { return InkBlack; });
                    Shape(img, delegate(float x, float y) { return Segment(x, y, tx + dx * 2f * k, ty + dy * 2f * k, gx, gy, 2f * k); }, delegate { return Hex(0xE6DCC8); });
                    Shape(img, delegate(float x, float y) { return Segment(x, y, gx - sx, gy - sy, gx + sx, gy + sy, 1.8f * k); }, delegate { return LampGold; });
                    Shape(img, delegate(float x, float y) { return Segment(x, y, gx, gy, px, py, 1.8f * k); }, delegate { return Hex(0x765B4A); });
                    Shape(img, delegate(float x, float y) { return Circle(x, y, px, py, 3.4f * k); }, delegate { return LampGold; });
                }
            }
            return img;
        }
    }
}

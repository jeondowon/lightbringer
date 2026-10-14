using System;

namespace Lightbringer.EditorTools
{
    public static partial class UiChromePainter
    {
        // Atlas markers at twice their displayed size. Reuse the chrome's dry ink and gouache,
        // Icon geometry only; campaign lighting is drawn at runtime, never baked into these textures.
        public static UiImage StoryBeacon(int state)
        {
            UiImage img = new UiImage(148, 176);
            Rgba stone = Hex(0x77716A);
            Rgba edge = state == 0 ? Hex(0xADA294) : Parchment;

            BeaconPaint(img, delegate(float x, float y) { return Convex(x, y, 26, 153, 117, 151, 129, 165, 19, 166); }, stone, edge, 51);
            BeaconPaint(img, delegate(float x, float y) { return Convex(x, y, 37, 140, 108, 138, 119, 153, 27, 155); }, stone, edge, 52);
            BeaconPaint(img, delegate(float x, float y) { return Convex(x, y, 56, 104, 91, 103, 99, 139, 48, 141); }, stone, edge, 53);
            InkLine(img, true, 123, 53, 95, 0.7f, InkBlack, 0, 148, 81);
            InkLine(img, false, 75, 124, 138, 0.7f, InkBlack, 0, 148, 82);
            // Heavy, irregular stone bowl: no polished metal or crystal gradients.
            BeaconPaint(img, delegate(float x, float y) { return Convex(x, y, 39, 88, 111, 87, 98, 108, 55, 109); }, Hex(0x645348), edge, 54);
            BeaconPaint(img, delegate(float x, float y) { return Ellipse(x, y, 75, 88, 37, 8); }, InkBlack, edge, 55);
            if (state == 2)
            {
                Sdf crystal = delegate(float x, float y) { return Convex(x, y, 74, 24, 96, 56, 74, 89, 52, 56); };
                BeaconPaint(img, crystal, LampGold, Umber, 56);
                Shape(img, delegate(float x, float y) { return Convex(x, y, 74, 28, 93, 56, 74, 85); },
                    delegate(float x, float y) { return StoryPaper(x, y, LampGlow, 0, 148, 57); });
            }
            else if (state == 1)
            {
                // A parchment star invites the player to restore the still-unlit beacon.
                LampStar(img, 74, 57, 20, InkBlack, false);
                LampStar(img, 74, 57, 17, LampGlow, false);
                InkLine(img, false, 74, 73, 83, 0.7f, LampGold, 0, 148, 59);
            }
            else
            {
                InkLine(img, true, 87, 55, 94, 1.6f, Slate, 0, 148, 60);
            }
            return img;
        }

        private static void BeaconPaint(UiImage img, Sdf shape, Rgba colour, Rgba rim, int seed)
        {
            Sdf rough = delegate(float x, float y) { return shape(x, y) + (Noise(x * 0.18f, y * 0.18f, seed) - 0.5f) * 1.8f; };
            Shape(img, delegate(float x, float y) { return rough(x, y) - 1.8f; }, delegate { return InkBlack; });
            Shape(img, rough, delegate(float x, float y)
            {
                Rgba paper = StoryPaper(x, y, colour, 0, 148, seed);
                float line = Smooth(-2.2f, -0.6f, rough(x, y));
                return Rgba.Lerp(paper, rim, line * 0.65f);
            });
        }
    }
}

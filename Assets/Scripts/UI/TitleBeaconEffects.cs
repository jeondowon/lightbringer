using UnityEngine;
using UnityEngine.UIElements;

namespace Lightbringer.UI
{
    // Paints over the title illustration; coordinates follow its centred scale-and-crop background.
    internal sealed class TitleBeaconEffects : VisualElement
    {
        private static readonly Vector2 Lantern = new Vector2(1245f, 266f);
        private static readonly Vector2 HillLight = new Vector2(1190f, 754f);
        private static readonly Vector2 StaffGem = new Vector2(1104f, 632f);

        // Burning spots in the valley: village ruins, the forest hollow and the far hills (artwork pixels).
        private static readonly (Vector2 position, float size, int embers)[] Fires =
        {
            (new Vector2(688f, 655f), 1f, 22),
            (new Vector2(772f, 668f), 0.85f, 16),
            (new Vector2(884f, 632f), 0.9f, 18),
            (new Vector2(430f, 604f), 0.8f, 16),
            (new Vector2(655f, 472f), 0.5f, 9),
            (new Vector2(1440f, 480f), 0.5f, 9),
        };

        private const int ForegroundEmbers = 8;

        private readonly SpriteFx fx = new SpriteFx();
        private float elapsed, repaintTime;

        public TitleBeaconEffects()
        {
            name = "Title Beacon Light and Embers";
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = style.right = style.top = style.bottom = 0f;
            style.overflow = Overflow.Hidden;
            generateVisualContent += Draw;
        }

        public void Tick(float deltaTime)
        {
            elapsed += deltaTime;
            repaintTime += deltaTime;
            if (repaintTime < 1f / 60f) return;
            repaintTime = 0f;
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext context)
        {
            if (!fx.Begin(contentRect, elapsed, true)) return;

            for (int site = 0; site < Fires.Length; site++)
                fx.Fire(Fires[site].position, Fires[site].size, Fires[site].embers, site);

            fx.Beacon(Lantern, 1f, 1f, 0);
            fx.Spill(HillLight, new Vector2(280f, 52f), new Color(1f, 0.72f, 0.36f), 0.26f, 0);

            // The hero's staff, plus its faint blue light on her shoulder and cloak.
            float pulse = 0.82f + 0.12f * Mathf.Sin(elapsed * 1.6f);
            fx.Glow(StaffGem + new Vector2(-14f, 26f), new Vector2(30f, 44f), new Color(0.4f, 0.62f, 1f, 0.12f * pulse));
            fx.MagicGem(StaffGem, 1f, 0);

            // A few embers carried across the foreground give the scene depth.
            for (int i = 0; i < ForegroundEmbers; i++)
            {
                float seed = SpriteFx.Hash(i + 211);
                fx.Ember(new Vector2(260f + seed * 1250f, 980f), i + 211, 1.5f, 420f + seed * 180f, 300f, 8f, true);
            }

            fx.End(context);
        }
    }
}

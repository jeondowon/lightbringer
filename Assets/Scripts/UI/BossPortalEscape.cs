using UnityEngine;
using UnityEngine.UIElements;

namespace Lightbringer.UI
{
    // The defeated lord is seized by ink tendrils from the portal behind him, resists for a beat, then is
    // dragged in: his painted cutout is warped toward the portal mouth, nearest parts first so the rest
    // stretches after them, darkening and dissolving as it falls in. A clean plate fills the space he leaves.
    public sealed class BossPortalEscape : VisualElement
    {
        public const float Duration = 3.2f;
        // Artwork rectangle of the lord cutout; the clean plate is patched over the same area.
        public static readonly RectInt LordRect = new RectInt(940, 215, 640, 480);
        public const float PlateFeather = 18;
        private const float PullStart = .9f, PullSpread = .35f, PullSeconds = 1f, GulpAt = 2.2f;
        private const float NearDistance = 120, FarDistance = 470;
        private const int Columns = 64, Rows = 46, PathPoints = 24, CoilPoints = 16;
        // The dark heart of the portal painted in Retreat_v1.
        private static readonly Vector2 Mouth = new Vector2(1325, 300);
        // Each tendril lashes out of the portal and coils round one part of the lord.
        private static readonly Vector2[] TendrilFrom =
        {
            new Vector2(1250, 150), new Vector2(1190, 90), new Vector2(1420, 210),
            new Vector2(1440, 400), new Vector2(1300, 560), new Vector2(1500, 520)
        };
        private static readonly Vector2[] TendrilTo =
        {
            new Vector2(1160, 290), new Vector2(1040, 275), new Vector2(1300, 330),
            new Vector2(1265, 445), new Vector2(1180, 585), new Vector2(1440, 590)
        };
        private static readonly float[] TendrilDelay = { .12f, .3f, .05f, .2f, .38f, .26f };
        private static readonly float[] TendrilBend = { -60, 70, 40, -50, 60, -35 };
        // Trailing points whose smoke streams into the portal while he is dragged.
        private static readonly Vector2[] Trails = { new Vector2(1005, 630), new Vector2(1180, 615), new Vector2(1480, 615) };

        private readonly Texture2D empty, lord;
        private readonly SpriteFx back = new SpriteFx(), front = new SpriteFx();
        private readonly Vector2[][] tendrils = new Vector2[6][];
        private readonly Vector2[][] trails = new Vector2[3][];
        private float elapsed;
        public bool Running { get; private set; }
        public bool Complete => Running && elapsed >= Duration;
        public bool Ready => empty != null && lord != null;
        public bool LordRemoved => Running && elapsed >= PullStart + PullSpread + PullSeconds;

        public BossPortalEscape()
        {
            name = "Boss Portal Escape";
            empty = UnityEngine.Resources.Load<Texture2D>("BossStory/Retreat_Empty_v1");
            lord = UnityEngine.Resources.Load<Texture2D>("BossStory/Retreat_Lord_v1");
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = style.right = style.top = style.bottom = 0;
            style.overflow = Overflow.Hidden;
            for (int i = 0; i < tendrils.Length; i++) tendrils[i] = new Vector2[PathPoints + CoilPoints];
            for (int i = 0; i < trails.Length; i++) trails[i] = new Vector2[3];
            generateVisualContent += Draw;
        }

        public bool Begin()
        {
            if (!Ready || Running) return false;
            elapsed = 0; Running = true; MarkDirtyRepaint(); return true;
        }
        public void Stop() { if (!Running) return; Running = false; MarkDirtyRepaint(); }
        public void Tick(float delta) { if (!Running) return; elapsed += Mathf.Max(0, delta); MarkDirtyRepaint(); }
        private static float Ease(float t) => Mathf.SmoothStep(0, 1, Mathf.Clamp01(t));
        private static float EaseOut(float t) { t = 1 - Mathf.Clamp01(t); return 1 - t * t * t; }
        private static float EaseIn(float t) { t = Mathf.Clamp01(t); return t * t * t; }
        private static float Pulse(float t, float at, float width) { float x = (t - at) / width; return Mathf.Exp(-x * x); }
        // How firmly the tendrils hold him, from first contact until the pull.
        private float Grip => Ease((elapsed - .25f) / .6f);

        // Where a point of the painted lord is drawn now, and how far it has been dragged in (0–1).
        private Vector2 Deform(Vector2 rest, out float pull)
        {
            Vector2 toMouth = rest - Mouth;
            float distance = toMouth.magnitude;
            pull = EaseIn((elapsed - PullStart - Mathf.InverseLerp(NearDistance, FarDistance, distance) * PullSpread) / PullSeconds);
            // He braces away from the portal while the bindings tighten, trembling against them.
            Vector2 away = distance > 1 ? toMouth / distance : Vector2.zero;
            float brace = Mathf.Sin(Mathf.PI * Mathf.Clamp01((elapsed - .45f) / .6f)) * 9 * (1 - pull);
            float strain = Grip * (1 - pull) * (1.4f * Mathf.Sin(elapsed * 41) + .8f * Mathf.Sin(elapsed * 67 + 1.3f));
            // Dragged in along a tightening spiral; nearer parts start first, so the rest stretches behind.
            float twist = -.45f * pull * Mathf.Sqrt(pull);
            float cos = Mathf.Cos(twist), sin = Mathf.Sin(twist);
            Vector2 spiral = new Vector2(toMouth.x * cos - toMouth.y * sin, toMouth.x * sin + toMouth.y * cos) * (1 - pull);
            return Mouth + spiral + away * brace + new Vector2(strain, strain * .4f);
        }

        private void Draw(MeshGenerationContext context)
        {
            if (!Running || !Ready || contentRect.width <= 0 || contentRect.height <= 0) return;
            DrawPlate(context);
            if (!back.Begin(contentRect, elapsed, false)) return;
            front.Begin(contentRect, elapsed, false);
            DrawPortal();
            back.End(context);
            if (!LordRemoved) DrawLord(context);
            DrawTendrils();
            DrawTrails();
            front.End(context);
        }

        // The portal wakes, draws breath through the pull, gulps as the last of him falls in, then settles.
        private void DrawPortal()
        {
            float gulp = Pulse(elapsed, GulpAt, .14f);
            float power = (.55f * Ease(elapsed / .5f) + .45f * Ease((elapsed - PullStart) / 1.2f)) * (1 - Ease((elapsed - 2.45f) / .7f));
            float breath = 1 + .04f * Mathf.Sin(elapsed * 5.5f) - .06f * gulp;
            // Only a faint violet haze: the painted heart must stay black, and deepens as it draws breath.
            back.Glow(Mouth, new Vector2(330, 370) * breath, new Color(.52f, .24f, .86f, .03f * power + .06f * gulp));
            back.Glow(Mouth, new Vector2(190, 260) * breath, new Color(.02f, .005f, .04f, .5f * power + .2f * gulp));
            back.Inflow(Mouth, new Vector2(270, 320), 46, new Color(.66f, .4f, 1f), 1.25f, power, 77);
            // The gulp: a small violet flash deep in the mouth as the last of him falls in.
            front.Glow(Mouth, new Vector2(60, 85) * (1 + Ease((elapsed - GulpAt + .1f) / .4f)), new Color(.8f, .6f, 1f, .4f * gulp));
        }

        private void DrawPlate(MeshGenerationContext context)
        {
            const int columns = 40, rows = 32;
            // The cutout covers the lord exactly, so the plate can fade in beneath him without a visible change.
            float shown = Ease(elapsed / .35f);
            MeshWriteData mesh = context.Allocate((columns + 1) * (rows + 1), columns * rows * 6, empty);
            for (int y = 0; y <= rows; y++)
            for (int x = 0; x <= columns; x++)
            {
                Vector2 point = new Vector2(LordRect.x + LordRect.width * x / (float)columns, LordRect.y + LordRect.height * y / (float)rows);
                float edge = Mathf.Min(Mathf.Min(point.x - LordRect.xMin, LordRect.xMax - point.x),
                    Mathf.Min(point.y - LordRect.yMin, LordRect.yMax - point.y));
                Vector2 at = back.At(point);
                mesh.SetNextVertex(new Vertex { position = new Vector3(at.x, at.y, Vertex.nearZ),
                    uv = new Vector2(point.x / 1672, 1 - point.y / 941), tint = new Color(1, 1, 1, shown * Ease(edge / PlateFeather)) });
            }
            WriteGrid(mesh, columns, rows);
        }

        private void DrawLord(MeshGenerationContext context)
        {
            MeshWriteData mesh = context.Allocate((Columns + 1) * (Rows + 1), Columns * Rows * 6, lord);
            float grip = Grip;
            for (int y = 0; y <= Rows; y++)
            for (int x = 0; x <= Columns; x++)
            {
                Vector2 uv = new Vector2((float)x / Columns, (float)y / Rows);
                Vector2 rest = new Vector2(LordRect.x + LordRect.width * uv.x, LordRect.y + LordRect.height * uv.y);
                Vector2 at = back.At(Deform(rest, out float pull));
                // Shadow from the portal floods him as he is held, then he dissolves into ink on the way in.
                float grain = Mathf.PerlinNoise(rest.x * .021f, rest.y * .021f);
                float alpha = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.35f, .8f, pull + (grain - .5f) * .4f));
                float dark = Mathf.Max(.25f * grip, Mathf.SmoothStep(0, 1, pull / .45f));
                Color tint = Color.Lerp(Color.white, new Color(.17f, .09f, .25f), dark);
                tint.a = alpha;
                mesh.SetNextVertex(new Vertex { position = new Vector3(at.x, at.y, Vertex.nearZ), uv = new Vector2(uv.x, 1 - uv.y), tint = tint });
            }
            WriteGrid(mesh, Columns, Rows);
        }

        private static void WriteGrid(MeshWriteData mesh, int columns, int rows)
        {
            for (int y = 0; y < rows; y++)
            for (int x = 0; x < columns; x++)
            {
                int a = y * (columns + 1) + x, b = a + columns + 1;
                mesh.SetNextIndex((ushort)a); mesh.SetNextIndex((ushort)(a + 1)); mesh.SetNextIndex((ushort)(b + 1));
                mesh.SetNextIndex((ushort)a); mesh.SetNextIndex((ushort)(b + 1)); mesh.SetNextIndex((ushort)b);
            }
        }

        private void DrawTendrils()
        {
            for (int i = 0; i < tendrils.Length; i++)
            {
                float reach = EaseOut((elapsed - TendrilDelay[i]) / .38f);
                if (reach <= 0) continue;
                Vector2 anchor = Deform(TendrilTo[i], out float pull);
                // Once he is moving the tendrils have done their work: they sink back into the dark with him.
                float strength = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.08f, .4f, pull));
                if (strength <= 0) continue;
                Vector2 from = TendrilFrom[i], toward = (from - anchor).normalized;
                // The coil wraps round the anchor, starting on the side facing the portal.
                Vector2 radii = new Vector2(30, 16) * (1 - .7f * pull);
                float start = Mathf.Atan2(toward.y / radii.y, toward.x / radii.x);
                Vector2 end = anchor + Vector2.Scale(new Vector2(Mathf.Cos(start), Mathf.Sin(start)), radii);
                Vector2 control = (from + end) * .5f + new Vector2(-toward.y, toward.x) * (TendrilBend[i] + 10 * Mathf.Sin(elapsed * 3 + i));
                Vector2[] path = tendrils[i];
                for (int k = 0; k < PathPoints; k++)
                {
                    float u = reach * k / (PathPoints - 1);
                    path[k] = (1 - u) * (1 - u) * from + 2 * (1 - u) * u * control + u * u * end;
                }
                float coil = Ease((elapsed - TendrilDelay[i] - .3f) / .3f) * 1.7f * Mathf.PI;
                for (int k = 0; k < CoilPoints; k++)
                {
                    float angle = start + coil * (k + 1) / CoilPoints;
                    path[PathPoints + k] = reach < 1 ? path[PathPoints - 1]
                        : anchor + Vector2.Scale(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)), radii);
                }
                front.VoidTendril(path, 20, strength, 300 + i);
            }
        }

        // Smoke torn from his trailing edge streams into the portal while he is dragged.
        private void DrawTrails()
        {
            float strength = Ease((elapsed - PullStart) / .3f) * (1 - Ease((elapsed - GulpAt) / .4f));
            if (strength <= 0) return;
            for (int i = 0; i < trails.Length; i++)
            {
                Vector2 tail = Deform(Trails[i], out _);
                trails[i][0] = tail;
                trails[i][1] = Vector2.Lerp(tail, Mouth, .5f) + new Vector2(0, 40);
                trails[i][2] = Mouth;
                front.DarkFlow(trails[i], 14, 380, 1.6f, strength, 400 + i);
            }
        }
    }
}

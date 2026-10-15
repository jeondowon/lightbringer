using System.Collections.Generic;
using Lightbringer.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Lightbringer.Visuals
{
    // The Demon King (Docs/GameDesign/FinalBattle.md, prologue 00D): a colossus of navy and violet smoke risen behind the
    // final citadel, crowned like the prologue figure, its mantle spilling past the walls into the storm. The only red on
    // it is the two eyes, set in hollow sockets under a furrowed brow, so nothing else reads as an eye. Built on the Demon
    // Smoke shader: churning puffs whose head bulges toward the field for parallax, frayed ribbons for the crown, brow and
    // tendrils, wisps that drift across the face, ink streams climbing from the ground into the mantle and a stream from
    // the abyss core to the chin. Presentation only; damage and attacks live in DemonKingCombat.
    public sealed class DemonKingSky : MonoBehaviour
    {
        // Behind the citadel (its rear wall is near z 144) and above its towers, the central spire rising between the eyes.
        private const float HeadZ = BattlefieldLayout.EnemyBaseZ + 80, BodyZ = HeadZ + 14, BankZ = HeadZ + 44;
        private const float EyeSpacing = 12.5f, EyeRoll = 13;
        public static readonly Vector3 EyeCentre = new Vector3(0, 75, HeadZ - 11.5f);
        private static readonly Vector3 FormPivot = new Vector3(0, 60, HeadZ);
        private const float DormantPresence = .55f, FadeRate = .6f;
        private static readonly Color Abyss = new Color(.012f, .014f, .04f);
        private static readonly Color Navy = new Color(.028f, .032f, .085f);
        private static readonly Color Indigo = new Color(.07f, .05f, .155f);
        private static readonly Color Violet = new Color(.12f, .05f, .21f);
        private static readonly Color EyeColor = new Color(1.8f, .08f, .04f);

        private readonly List<Object> owned = new List<Object>();
        private readonly List<Ribbon> ribbons = new List<Ribbon>();
        private Material bank, body, face, strands, halo, eye, veil;
        private Material[] smoke;
        private Transform form, bankLayer, eyes;
        private Ribbon coreStream;
        private Transform core;
        private float presence = DormantPresence, presenceTarget = DormantPresence, open, openTarget;
        private float charge, flinch, clock, surge = -1;
        // Height of the stream's root above the core (the crystal's tip).
        private const float CoreTip = AbyssCoreVisual.Tip, SurgeSeconds = .55f;
        public bool Revealed => openTarget > 0;
        public bool Dissipating { get; private set; }
        public float Presence => presence;
        public float EyeOpening => open;
        public float Charge => charge;

        private struct Puff
        {
            public Vector3 Centre;
            public Vector2 Size;
            public float Roll, Drift, Rim, Seed;
            public Color Color;
        }

        private sealed class Ribbon
        {
            public Mesh Mesh;
            public Transform Parent, Follow;
            public Vector3 From, Control, To, Drift;
            public float WidthFrom, WidthTo, Phase, Alpha = 1, Rim, Sway, FadeIn, FadeOut, Speed, Wave, WaveCount;
            public Color Tint;
            public Vector3[] Vertices;
            public Color[] Colors;
            public List<Vector4> Data;
            public float Surge = -1;
            public bool Lit;
        }

        public static DemonKingSky Build(ArtStyleLibrary art, Transform root)
        {
            Shader shader = UnityEngine.Resources.Load<Shader>("DemonSmoke");
            if (shader == null) return null;
            var item = new GameObject("Demon King Sky Darkness");
            item.transform.SetParent(root, false);
            DemonKingSky sky = item.AddComponent<DemonKingSky>();
            sky.Create(shader);
            return sky;
        }

        private void Create(Shader shader)
        {
            // Back to front: storm bank, body, face shadows, frayed strands, eye glow, eyes, wisps across the face.
            bank = NewMaterial(shader, "Demon King Storm Bank", 0, 3001);
            body = NewMaterial(shader, "Demon King Smoke Body", 0, 3002);
            face = NewMaterial(shader, "Demon King Face Shadow", 0, 3003);
            strands = NewMaterial(shader, "Demon King Smoke Strands", 1, 3004);
            halo = NewMaterial(shader, "Demon King Eye Glow", 3, 3005);
            eye = NewMaterial(shader, "Demon King Eye", 2, 3006);
            veil = NewMaterial(shader, "Demon King Face Wisps", 1, 3007);
            face.SetFloat("_Glow", 0);
            // Every strand hangs from the form, so its gather point is the eyes in the form's space.
            strands.SetVector("_Focus", EyeCentre - FormPivot);
            veil.SetVector("_Focus", EyeCentre - FormPivot);
            smoke = new[] { body, face, strands, veil };
            var random = new System.Random(2417);

            form = new GameObject("Smoke Form").transform;
            form.SetParent(transform, false);
            form.localPosition = FormPivot;
            BuildBank(random);
            BuildBody(random);
            BuildFace(random);
            BuildStrands();

            eyes = new GameObject("Crimson Eyes").transform;
            eyes.SetParent(form, false);
            eyes.localPosition = EyeCentre - FormPivot;
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 at = new Vector3(side * EyeSpacing, 0, 0);
                // Inner corners lowered; the left lid is mirrored so both thicken toward the bridge.
                Single("Eye Glow", eyes, at + Vector3.forward * .3f, new Vector2(26, 10), side * EyeRoll, new Color(1, 1, 1, .6f), halo);
                Single("Eye", eyes, at, new Vector2(-side * 13, 4.4f), side * EyeRoll, Color.white, eye);
            }
            eyes.localScale = new Vector3(1, .02f, 1);
            Apply();
        }

        // Signed distance (metres, positive inside) to the silhouette in the form's plane.
        private static float Figure(float x, float y)
        {
            float ax = Mathf.Abs(x);
            // Head: a broad cranium narrowing to a jaw that sinks into the mantle.
            float width = y >= 72 ? 23 : Mathf.Lerp(12.5f, 23, Mathf.InverseLerp(44, 72, y));
            float height = y >= 74 ? 26 : 30;
            float head = (1 - Mathf.Sqrt(Sq(x / width) + Sq((y - 74) / height))) * Mathf.Min(width, height);
            float neck = Mathf.Min(16 - ax, Mathf.Min(y - 30, 64 - y));
            // Mantle: shoulders slump outward from the neck, a hump over each, tips sinking past the walls.
            float top = 63 - .17f * ax + 7 * Mathf.Exp(-Sq((ax - 58) / 15)) - Mathf.Max(0, ax - 95) * .5f;
            float mantle = Mathf.Min(Mathf.Min(top - y, y - 16), 122 - ax);
            return Mathf.Max(head, Mathf.Max(neck, mantle));
        }

        // The head bulges toward the battlefield; the mantle bows forward around the neck.
        private static float Depth(float x, float y)
        {
            float ax = Mathf.Abs(x);
            float dome = Mathf.Sqrt(Mathf.Max(0, 1 - Sq(x / 25) - Sq((y - 76) / 30)));
            float weight = Mathf.Clamp01((y - 46) / 14) * Mathf.Clamp01((30 - ax) / 8);
            return Mathf.Lerp(BodyZ - 8 * Mathf.Max(0, 1 - ax / 120), HeadZ - 11 * dome, weight);
        }

        private void BuildBody(System.Random random)
        {
            var puffs = new List<Puff>();
            const float step = 5.5f;
            for (float gy = 16; gy <= 104; gy += step)
                for (float gx = -128; gx <= 128; gx += step)
                {
                    float x = gx + Lerp(random, -2.4f, 2.4f), y = gy + Lerp(random, -2.4f, 2.4f);
                    float d = Figure(x, y);
                    if (d < -1.5f) continue;
                    // The lower mantle sinks into the storm behind the walls as broad soft lumps, without a lit hem.
                    bool deep = d > 9 || y < 30;
                    if (deep && random.NextDouble() < .55) continue;
                    float size = deep ? Lerp(random, 17, 26) : d > 3 ? Lerp(random, 11, 17) : Lerp(random, 6.5f, 11);
                    // Only edges facing up catch the violet light; the core is the darkest smoke in the sky.
                    float edge = deep ? 0 : 1 - Mathf.Clamp01(d / 7);
                    float facing = Mathf.Clamp01((Figure(x, y - 2) - Figure(x, y + 2)) * .3f);
                    Color tone = deep ? Color.Lerp(Abyss, Navy, Lerp(random, .2f, 1)) : Color.Lerp(Navy, Indigo, Lerp(random, 0, .35f));
                    tone *= Lerp(random, .85f, 1.1f);
                    tone = Color.Lerp(tone, Violet, edge * facing * .5f);
                    // The lowest lumps dissolve into the storm behind the walls.
                    tone.a = (deep ? .95f : Lerp(random, .78f, .95f)) * Mathf.Clamp01((y - 14) / 14);
                    puffs.Add(new Puff
                    {
                        Centre = new Vector3(x, y, Depth(x, y) + Lerp(random, -1.5f, 1.5f)),
                        Size = new Vector2(size * Lerp(random, 1, 1.3f), size), Roll = Lerp(random, 0, 360), Color = tone,
                        Drift = Lerp(random, .3f, 1.1f) * (deep ? .6f : 1), Rim = edge * (.15f + .85f * facing), Seed = (float)random.NextDouble()
                    });
                }
            // Far to near so the bulging head draws over the mantle behind it.
            puffs.Sort((a, b) => b.Centre.z.CompareTo(a.Centre.z));
            Layer("Smoke Body", form, -FormPivot, puffs, body);
        }

        private void BuildFace(System.Random random)
        {
            var puffs = new List<Puff>();
            float z = EyeCentre.z + .8f;
            for (int side = -1; side <= 1; side += 2)
            {
                // Hollow sockets: the eyes burn inside the darkest smoke on the body.
                puffs.Add(Still(new Vector3(side * EyeSpacing, EyeCentre.y + .3f, z), new Vector2(20, 10.5f), side * EyeRoll, Abyss, .97f, 0, random));
                puffs.Add(Still(new Vector3(side * (EyeSpacing + 3), EyeCentre.y - 2, z + .3f), new Vector2(14, 8), side * EyeRoll, Abyss, .7f, 0, random));
                // Brow crest lit from above, furrowed down toward the bridge.
                for (int i = 0; i < 5; i++)
                {
                    float t = i / 4f;
                    var at = new Vector3(side * Mathf.Lerp(3.5f, 23, t), Mathf.Lerp(80.2f, 85.5f, t) + Lerp(random, -.4f, .4f), z - .6f);
                    puffs.Add(Still(at, new Vector2(Lerp(random, 7, 9.5f), Lerp(random, 5, 6.5f)), Lerp(random, 0, 360), Color.Lerp(Indigo, Violet, .55f), .85f, 1, random));
                }
                // A faint cheekbone below each socket, no nose or mouth.
                puffs.Add(Still(new Vector3(side * 14.5f, 67.5f, z + .5f), new Vector2(13, 6.5f), -side * 8, Indigo, .45f, .6f, random));
            }
            puffs.Add(Still(new Vector3(0, EyeCentre.y + 2, z + .2f), new Vector2(9, 12), 0, Abyss, .75f, 0, random));
            Layer("Face Shadow", form, -FormPivot, puffs, face);
        }

        private Puff Still(Vector3 centre, Vector2 size, float roll, Color color, float alpha, float rim, System.Random random)
        {
            color.a = alpha;
            return new Puff { Centre = centre, Size = size, Roll = roll, Color = color, Drift = .08f, Rim = rim, Seed = (float)random.NextDouble() };
        }

        private void BuildBank(System.Random random)
        {
            var puffs = new List<Puff>();
            for (int i = 0; i < 110; i++)
            {
                float x = Lerp(random, -195, 195);
                // Heaviest around the figure, lifting toward the flanks.
                float y = 26 + Mathf.Pow((float)random.NextDouble(), .85f) * 140 + Mathf.Abs(x) * .08f;
                float size = Lerp(random, 30, 58);
                // A shade lighter and thinner than the body, so the colossus stands out of it as a silhouette.
                Color tone = Color.Lerp(Navy, Indigo, Lerp(random, .1f, .55f)) * Lerp(random, 1.1f, 1.5f);
                tone.a = Lerp(random, .35f, .6f);
                puffs.Add(new Puff
                {
                    Centre = new Vector3(x, y, Lerp(random, -12, 12)), Size = new Vector2(size * Lerp(random, 1.2f, 1.7f), size),
                    Roll = Lerp(random, 0, 360), Color = tone, Drift = Lerp(random, 1.5f, 3.2f), Rim = Lerp(random, .05f, .2f), Seed = (float)random.NextDouble()
                });
            }
            puffs.Sort((a, b) => b.Centre.z.CompareTo(a.Centre.z));
            bankLayer = Layer("Storm Bank", transform, new Vector3(0, 0, BankZ), puffs, bank);
        }

        private void BuildStrands()
        {
            // Crown: jagged smoke spires rising off the cranium, as in the prologue.
            float[] spikes = { 0, 8, -8, 15, -15, 20.5f, -20.5f };
            float[] tips = { 141, 128, 129, 117, 116, 106, 107 };
            for (int i = 0; i < spikes.Length; i++)
            {
                float x = spikes[i], baseY = 74 + 26 * Mathf.Sqrt(Mathf.Max(0, 1 - Sq(x / 23))) - 4;
                Vector3 from = new Vector3(x, baseY, Depth(x, baseY) + 1);
                Vector3 to = new Vector3(x * 1.28f, tips[i], from.z + 2);
                NewRibbon(form, from, Vector3.Lerp(from, to, .5f) + new Vector3(x * .08f, 0, 0), to, 8.5f - Mathf.Abs(x) * .12f, .5f,
                    i * 1.7f, Navy, .7f, 1.3f, .04f, .4f);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                // A furrowed brow arching over each socket, heavy at the bridge and fraying at the temple.
                var inner = new Vector3(side * 3.5f, 79.4f, EyeCentre.z);
                var outer = new Vector3(side * 21, 83.5f, EyeCentre.z - .5f);
                NewRibbon(form, inner, new Vector3(side * 12, 84.5f, EyeCentre.z - .3f), outer, 5, 1.4f, side * 2.3f, Navy, .5f, .25f, .1f, .35f);
                // Smoke rising off the shoulders and the cranium.
                for (int i = 0; i < 6; i++)
                {
                    float x = side * (i < 2 ? 9 + i * 9 : 32 + (i - 2) * 19);
                    float ax = Mathf.Abs(x);
                    float y = ax < 22 ? 74 + 26 * Mathf.Sqrt(Mathf.Max(0, 1 - Sq(x / 23))) - 3
                        : 63 - .17f * ax + 7 * Mathf.Exp(-Sq((ax - 58) / 15)) - Mathf.Max(0, ax - 95) * .5f - 2;
                    var from = new Vector3(x, y, Depth(x, y) - 1);
                    float rise = 14 + i % 3 * 6;
                    NewRibbon(form, from, from + new Vector3(side * 2, rise * .5f, 0), from + new Vector3(side * (3 + i % 2 * 4), rise, 0),
                        4.5f, 1, side * (1.9f + i * .8f), Navy, .6f, 2.4f, .12f, .55f).Alpha = .6f;
                }
                // Tendrils pour off the shoulders and sweep out across the sky.
                // Each undulates at its own length and pace, like the ink of the prologue rather than ribs.
                float[] reach = { 160, 138, 118 }, lift = { 118, 62, 18 }, wave = { 9, 6, 4.5f };
                for (int i = 0; i < 3; i++)
                {
                    var from = new Vector3(side * (44 + i * 20), 62 - i * 7, BodyZ - 6);
                    var control = new Vector3(side * (96 + i * 6), 112 - i * 22, BodyZ - 8);
                    var to = new Vector3(side * reach[i], lift[i], BodyZ - 2);
                    Ribbon tendril = NewRibbon(form, from, control, to, 13 - i * 2.5f, 1.5f, side * (3.1f + i * 1.7f), Navy, .55f, 3.5f, .15f, .4f);
                    tendril.Wave = wave[i];
                    tendril.WaveCount = 1.2f + i * .4f;
                }
                // Ink streams climb from the ground beside the citadel into the mantle.
                for (int i = 0; i < 2; i++)
                {
                    var from = new Vector3(side * (74 + i * 9), 0, BattlefieldLayout.EnemyBaseZ + 40 + i * 8);
                    var control = new Vector3(side * (86 + i * 6), 26 + i * 4, BodyZ - 20);
                    var to = new Vector3(side * (52 + i * 16), 48 - i * 4, BodyZ - 4);
                    NewRibbon(form, from, control, to, 4, 8, side * 1.3f + i, new Color(.022f, .022f, .062f), .4f, 2.2f, .12f, .3f);
                }
            }
            // Wisps drift across the face, now and then veiling the eyes.
            Ribbon low = NewRibbon(form, new Vector3(-36, 71, EyeCentre.z - 3), new Vector3(0, 76, EyeCentre.z - 4),
                new Vector3(36, 69, EyeCentre.z - 3), 3.5f, 5, .4f, Navy, .5f, .6f, .3f, .3f, veil);
            low.Alpha = .32f; low.Drift = new Vector3(16, 1.5f, 0); low.Speed = .045f;
            Ribbon high = NewRibbon(form, new Vector3(34, 83, EyeCentre.z - 3.5f), new Vector3(0, 80, EyeCentre.z - 4),
                new Vector3(-34, 84, EyeCentre.z - 3.5f), 3, 4, 2.6f, Navy, .5f, .6f, .3f, .3f, veil);
            high.Alpha = .26f; high.Drift = new Vector3(-14, 1, 0); high.Speed = .037f;
        }

        // The stream from the abyss core on the ground up into the chin.
        public void BindCore(Transform value)
        {
            core = value;
            if (coreStream != null || core == null) return;
            coreStream = NewRibbon(form, Vector3.zero, Vector3.zero, new Vector3(0, 47, HeadZ - 4), 1.6f, 4.5f, 0,
                new Color(.022f, .022f, .062f), .5f, 2.2f, .12f, .25f);
            coreStream.Follow = core;
            coreStream.Alpha = 0;
        }

        public void Reveal() { if (!Dissipating) { presenceTarget = 1; openTarget = 1; } }
        public void SetCharge(float value) => charge = Mathf.Clamp01(value);
        // A blow on the core surges up its stream first; the eyes flinch when it reaches the sky.
        public void Flinch()
        {
            if (coreStream != null && coreStream.Alpha > .5f) { if (surge < 0) surge = 0; }
            else flinch = 1;
        }
        public void Dissipate() { Dissipating = true; presenceTarget = 0; openTarget = 0; charge = 0; }

        private void Update() => Tick(Time.deltaTime);

        public void Tick(float delta)
        {
            if (delta <= 0) return;
            clock += delta;
            presence = Mathf.MoveTowards(presence, presenceTarget, delta * FadeRate * (Dissipating ? .5f : 1));
            open = Mathf.MoveTowards(open, openTarget, delta * (Dissipating ? .8f : .7f));
            flinch = Mathf.Max(0, flinch - delta * 3.5f);
            if (surge >= 0 && (surge += delta / SurgeSeconds) >= 1) { surge = -1; flinch = 1; }
            if (coreStream != null) coreStream.Surge = surge;
            // Slow breathing and a faint sway keep the colossus alive; the storm drifts at its own pace for depth.
            float breathe = Mathf.Sin(clock * .23f) * .012f - charge * .015f;
            form.localScale = new Vector3(1 + breathe, 1 + breathe * .6f, 1);
            form.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(clock * .11f) * .6f + Mathf.Sin(clock * 40) * flinch * .25f);
            bankLayer.localPosition = new Vector3(Mathf.Sin(clock * .05f) * 4, Mathf.Sin(clock * .035f) * 1.5f * (1 + charge), BankZ);
            if (coreStream != null) coreStream.Alpha = Mathf.MoveTowards(coreStream.Alpha, core != null && core.gameObject.activeInHierarchy ? 1 : 0, delta * 1.5f);
            foreach (Ribbon ribbon in ribbons) Shape(ribbon);
            Apply();
            if (Dissipating && presence <= 0 && open <= 0) gameObject.SetActive(false);
        }

        private void Apply()
        {
            // The storm stays while the form is still gathering; both erode away when the Demon King falls.
            bank.SetFloat("_Presence", Mathf.Clamp01(presence * 1.8f));
            bank.SetColor("_Color", new Color(1, 1, 1, Mathf.Clamp01(presence * 4)));
            float currents = (.16f + .05f * Mathf.Sin(clock * .9f)) * (1 + charge * 4) + flinch * .9f;
            bank.SetFloat("_Glow", currents * .35f);
            foreach (Material material in smoke)
            {
                material.SetFloat("_Presence", presence);
                material.SetColor("_Color", new Color(1, 1, 1, Mathf.Clamp01(presence * 3)));
                material.SetFloat("_Charge", charge);
                material.SetFloat("_Flinch", flinch);
                if (material != face) material.SetFloat("_Glow", currents);
            }
            bank.SetFloat("_Charge", charge);
            float glare = (1 + charge * 1.8f) * (1 - flinch * .75f * (Mathf.Sin(clock * 60) * .5f + .5f));
            float lit = Mathf.Clamp01(open * 1.5f);
            eye.SetColor("_Color", EyeColor * glare * lit);
            halo.SetColor("_Color", new Color(1, .06f, .03f) * .7f * glare * lit);
            eyes.localScale = new Vector3(1 + charge * .15f, Mathf.Max(.02f, open * (1 + charge * .3f)), 1);
            eyes.localPosition = EyeCentre - FormPivot + new Vector3(Mathf.Sin(clock * 40) * flinch * .5f, Mathf.Sin(clock * .3f) * .25f, 0);
        }

        private Ribbon NewRibbon(Transform parent, Vector3 from, Vector3 control, Vector3 to, float widthFrom, float widthTo, float phase,
            Color tint, float rim, float sway, float fadeIn, float fadeOut, Material material = null)
        {
            const int segments = 28;
            Vector3 offset = parent == form ? FormPivot : Vector3.zero;
            var ribbon = new Ribbon
            {
                Parent = parent, From = from - offset, Control = control - offset, To = to - offset, WidthFrom = widthFrom, WidthTo = widthTo,
                Phase = phase, Tint = tint, Rim = rim, Sway = sway, FadeIn = fadeIn, FadeOut = fadeOut,
                Vertices = new Vector3[(segments + 1) * 2], Colors = new Color[(segments + 1) * 2]
            };
            ribbon.Mesh = new Mesh { name = "Demon King Smoke Ribbon", hideFlags = HideFlags.DontSave };
            ribbon.Mesh.MarkDynamic();
            owned.Add(ribbon.Mesh);
            var uv = new Vector2[ribbon.Vertices.Length];
            var data = new List<Vector4>();
            var triangles = new int[segments * 6];
            float seed = Mathf.Repeat(phase * .37f, 1);
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                uv[i * 2] = new Vector2(0, t); uv[i * 2 + 1] = new Vector2(1, t);
                data.Add(new Vector4(seed, 0, rim, 0)); data.Add(new Vector4(seed, 0, rim, 0));
                if (i == segments) break;
                int v = i * 2, k = i * 6;
                triangles[k] = v; triangles[k + 1] = v + 2; triangles[k + 2] = v + 1;
                triangles[k + 3] = v + 1; triangles[k + 4] = v + 2; triangles[k + 5] = v + 3;
            }
            Shape(ribbon);
            ribbon.Mesh.uv = uv;
            ribbon.Mesh.SetUVs(1, data);
            ribbon.Data = data;
            ribbon.Mesh.triangles = triangles;
            var item = new GameObject("Smoke Strand");
            item.transform.SetParent(parent, false);
            item.AddComponent<MeshFilter>().sharedMesh = ribbon.Mesh;
            AddRenderer(item, material != null ? material : strands);
            ribbons.Add(ribbon);
            return ribbon;
        }

        // A tapering strip that sways along a quadratic curve; its ends fade into the smoke they leave and join.
        private void Shape(Ribbon ribbon)
        {
            int segments = ribbon.Vertices.Length / 2 - 1;
            Vector3 drift = ribbon.Drift * Mathf.Sin(clock * ribbon.Speed + ribbon.Phase);
            Vector3 from = ribbon.From + drift, control = ribbon.Control + drift, to = ribbon.To + drift;
            if (ribbon.Follow != null)
            {
                from = ribbon.Parent.InverseTransformPoint(ribbon.Follow.position) + Vector3.up * CoreTip;
                control = Vector3.Lerp(from, to, .5f) + new Vector3(0, 8, -6);
            }
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                Vector3 sway = new Vector3(Mathf.Sin(clock * .6f + ribbon.Phase + t * 4), Mathf.Sin(clock * .4f + ribbon.Phase * 2 + t * 3) * .4f,
                    Mathf.Cos(clock * .45f + ribbon.Phase + t * 3) * .5f) * ribbon.Sway * t;
                Vector3 point = Bezier(from, control, to, t) + sway;
                Vector3 tangent = Bezier(from, control, to, Mathf.Min(1, t + .02f)) - Bezier(from, control, to, Mathf.Max(0, t - .02f));
                Vector3 side = Vector3.Cross(tangent, Vector3.forward);
                side = side.sqrMagnitude > .0001f ? side.normalized : Vector3.right;
                // A travelling undulation, growing toward the free end.
                point += side * Mathf.Sin((t * ribbon.WaveCount - clock * .05f) * Mathf.PI * 2 + ribbon.Phase) * ribbon.Wave * t;
                float width = Mathf.Lerp(ribbon.WidthFrom, ribbon.WidthTo, t) * (1 + Mathf.Sin(clock * 1.1f + ribbon.Phase * 3 + t * 9) * .15f) * (1 + flinch * .4f);
                float fade = (ribbon.FadeIn > 0 ? Mathf.SmoothStep(0, 1, t / ribbon.FadeIn) : 1) * Mathf.SmoothStep(0, 1, (1 - t) / ribbon.FadeOut);
                ribbon.Vertices[i * 2] = point - side * width * .5f;
                ribbon.Vertices[i * 2 + 1] = point + side * width * .5f;
                Color color = ribbon.Tint;
                color.a = fade * ribbon.Alpha;
                ribbon.Colors[i * 2] = ribbon.Colors[i * 2 + 1] = color;
            }
            ribbon.Mesh.vertices = ribbon.Vertices;
            ribbon.Mesh.colors = ribbon.Colors;
            ribbon.Mesh.RecalculateBounds();
            if (ribbon.Data == null || (ribbon.Surge < 0 && !ribbon.Lit)) return;
            // The surge is a bright band carried from the root to the tip.
            ribbon.Lit = ribbon.Surge >= 0;
            for (int i = 0; i < ribbon.Data.Count; i++)
            {
                Vector4 value = ribbon.Data[i];
                value.w = ribbon.Lit ? Mathf.Exp(-Sq((i / 2 / (float)segments - ribbon.Surge) / .08f)) : 0;
                ribbon.Data[i] = value;
            }
            ribbon.Mesh.SetUVs(1, ribbon.Data);
        }

        private static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t) =>
            (1 - t) * (1 - t) * a + 2 * (1 - t) * t * b + t * t * c;

        private Transform Layer(string label, Transform parent, Vector3 position, List<Puff> puffs, Material material)
        {
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var uv = new List<Vector2>();
            var data = new List<Vector4>();
            var triangles = new List<int>();
            foreach (Puff puff in puffs)
            {
                Quaternion roll = Quaternion.Euler(0, 0, puff.Roll);
                Vector3 right = roll * Vector3.right * puff.Size.x * .5f, up = roll * Vector3.up * puff.Size.y * .5f;
                int start = vertices.Count;
                vertices.Add(puff.Centre - right - up); vertices.Add(puff.Centre + right - up);
                vertices.Add(puff.Centre + right + up); vertices.Add(puff.Centre - right + up);
                uv.Add(Vector2.zero); uv.Add(Vector2.right); uv.Add(Vector2.one); uv.Add(Vector2.up);
                for (int c = 0; c < 4; c++) { colors.Add(puff.Color); data.Add(new Vector4(puff.Seed, puff.Drift, puff.Rim, 0)); }
                triangles.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
            }
            var mesh = new Mesh { name = "Demon King " + label, hideFlags = HideFlags.DontSave };
            mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetUVs(0, uv); mesh.SetUVs(1, data); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            // Room for the shader's drift and gathering.
            Bounds bounds = mesh.bounds;
            bounds.Expand(12);
            mesh.bounds = bounds;
            owned.Add(mesh);
            var item = new GameObject(label);
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            item.AddComponent<MeshFilter>().sharedMesh = mesh;
            AddRenderer(item, material);
            Vector3 focus = item.transform.InverseTransformPoint(transform.TransformPoint(EyeCentre));
            material.SetVector("_Focus", focus);
            return item.transform;
        }

        private void Single(string label, Transform parent, Vector3 position, Vector2 size, float roll, Color color, Material material)
        {
            var puffs = new List<Puff> { new Puff { Centre = Vector3.zero, Size = size, Roll = roll, Color = color, Seed = position.x > 0 ? .7f : .2f } };
            Layer(label, parent, position, puffs, material);
        }

        private static void AddRenderer(GameObject item, Material material)
        {
            MeshRenderer renderer = item.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private Material NewMaterial(Shader shader, string label, float mode, int queue)
        {
            var material = new Material(shader) { name = label, hideFlags = HideFlags.DontSave, renderQueue = queue };
            material.SetFloat("_Mode", mode);
            material.SetColor("_RimColor", new Color(.2f, .09f, .36f));
            material.SetColor("_GlowColor", new Color(.5f, .2f, .95f));
            owned.Add(material);
            return material;
        }

        private static float Sq(float value) => value * value;
        private static float Lerp(System.Random random, float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());

        private void OnDestroy()
        {
            foreach (Object item in owned)
                if (item != null) { if (Application.isPlaying) Destroy(item); else DestroyImmediate(item); }
            owned.Clear();
        }
    }
}

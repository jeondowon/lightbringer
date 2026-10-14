using System.Collections.Generic;
using Lightbringer.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Lightbringer.Visuals
{
    // The Demon King (Docs/GameDesign/FinalBattle.md): no body, a storm bank over the final citadel with ink
    // streams gathering into it and two crimson eyes. Greybox prototype on the VFX Shape shader: layered puffs drift at
    // different speeds for depth, ribbons sway, a stream links the abyss core on the ground to the sky.
    // Presentation only; damage and attacks live in DemonKingCombat.
    public sealed class DemonKingSky : MonoBehaviour
    {
        // Above and behind the final citadel; the bank's lower edge sits near the horizon of the battle camera.
        public static readonly Vector3 Anchor = new Vector3(0, 0, BattlefieldLayout.EnemyBaseZ + 44);
        public static readonly Vector3 EyeCentre = Anchor + new Vector3(0, 34, -10);
        private const float DormantPresence = .55f, FadeRate = .6f;
        private static readonly Color CloudTint = new Color(.075f, .06f, .1f);
        private static readonly Color EyeColor = new Color(4.2f, .38f, .26f);

        private readonly List<Object> owned = new List<Object>();
        private readonly List<Ribbon> ribbons = new List<Ribbon>();
        private Material cloud, veins, ink, eye;
        private Transform[] layers;
        private Transform eyes;
        private Vector3[] layerRest;
        private Transform core;
        private Ribbon coreStream;
        private float presence = DormantPresence, presenceTarget = DormantPresence, open, openTarget;
        private float charge, flinch, clock;
        public bool Revealed => openTarget > 0;
        public bool Dissipating { get; private set; }
        public float Presence => presence;
        public float EyeOpening => open;

        private sealed class Ribbon
        {
            public Mesh Mesh;
            public Vector3 From, Control, To;
            public float WidthFrom, WidthTo, Phase, Alpha;
            public Transform Follow;
        }

        public static DemonKingSky Build(ArtStyleLibrary art, Transform root)
        {
            if (art == null || art.vfxMaterial == null) return null;
            var item = new GameObject("Demon King Sky Darkness");
            item.transform.SetParent(root, false);
            DemonKingSky sky = item.AddComponent<DemonKingSky>();
            sky.Create(art);
            return sky;
        }

        private void Create(ArtStyleLibrary art)
        {
            cloud = Copy(art.vfxMaterial, "Demon King Cloud", 6, 0, 1.15f, Color.white);
            veins = Copy(art.vfxMaterial, "Demon King Crimson Veins", 6, 1, 1.6f, new Color(.55f, .05f, .08f));
            ink = Copy(art.vfxMaterial, "Demon King Ink Stream", 0, 0, 1.25f, Color.white);
            eye = Copy(art.vfxMaterial, "Demon King Eye", 0, 1, 2.4f, EyeColor);
            // Draw back to front: far bank, crimson veins inside it, mid and near banks, then the eyes on top.
            cloud.renderQueue = 3001; veins.renderQueue = 3002; ink.renderQueue = 3003; eye.renderQueue = 3005;
            var random = new System.Random(2417);
            float[] depth = { 22, 6, -8 };
            layers = new Transform[depth.Length];
            layerRest = new Vector3[depth.Length];
            for (int layer = 0; layer < depth.Length; layer++)
            {
                var quads = new List<(Vector3, Vector2, float, Color)>();
                int count = layer == 0 ? 40 : 30;
                for (int i = 0; i < count; i++)
                {
                    float x = Lerp(random, -125, 125);
                    // Heaviest above the citadel, thinning toward the flanks.
                    float crown = 1 - Mathf.Abs(x) / 125;
                    float size = Lerp(random, 22, 44) * (layer == 2 ? .8f : 1);
                    float y = Mathf.Max(size * .5f + 4, Lerp(random, 14, 46) + crown * 16 - layer * 4);
                    float shade = Lerp(random, .7f, 1.3f);
                    float alpha = (layer == 0 ? .55f : .78f) * Lerp(random, .75f, 1);
                    quads.Add((new Vector3(x, y, Lerp(random, -4, 4)), new Vector2(size * Lerp(random, 1.1f, 1.6f), size),
                        Lerp(random, 0, 360), new Color(CloudTint.r * shade, CloudTint.g * shade, CloudTint.b * shade, alpha)));
                }
                layers[layer] = Layer("Storm Bank " + (layer + 1), Anchor + Vector3.forward * depth[layer], quads, cloud);
                layerRest[layer] = layers[layer].localPosition;
            }
            var glow = new List<(Vector3, Vector2, float, Color)>();
            for (int i = 0; i < 12; i++)
                glow.Add((new Vector3(Lerp(random, -60, 60), Lerp(random, 24, 50), 0), new Vector2(Lerp(random, 14, 26), Lerp(random, 6, 10)),
                    Lerp(random, -25, 25), new Color(1, 1, 1, Lerp(random, .35f, .7f))));
            Layer("Crimson Veins", Anchor + Vector3.forward * 14, glow, veins);

            // Ink streams rise from either side of the citadel and gather into the cloud, as in the prologue.
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < 2; i++)
                    ribbons.Add(NewRibbon(new Vector3(side * (16 + i * 9), 0, BattlefieldLayout.EnemyBaseZ + 20 + i * 6),
                        new Vector3(side * (26 + i * 6), 18 + i * 4, Anchor.z - 6),
                        EyeCentre + new Vector3(side * (8 + i * 5), 6 + i * 5, 4), 3.6f - i, .9f, side * 1.3f + i));

            eyes = new GameObject("Crimson Eyes").transform;
            eyes.SetParent(transform, false);
            eyes.localPosition = EyeCentre;
            for (int side = -1; side <= 1; side += 2)
            {
                // Narrow slanted slits with inner corners lowered, inside a faint ember halo.
                Quad("Eye Halo", eyes, new Vector3(side * 6, 0, .5f), new Vector2(17, 7), side * 10, new Color(1, 1, 1, .18f));
                Quad("Eye", eyes, new Vector3(side * 6, 0, 0), new Vector2(8.5f, 1.5f), side * 13, Color.white);
            }
            eyes.localScale = new Vector3(1, 0.02f, 1);
            Apply();
        }

        // The stream from the abyss core on the ground up into the darkness.
        public void BindCore(Transform value)
        {
            core = value;
            if (coreStream != null || core == null) return;
            coreStream = NewRibbon(Vector3.zero, Vector3.zero, EyeCentre + new Vector3(0, -6, 2), 1.4f, 3.4f, 0);
            coreStream.Follow = core;
            coreStream.Alpha = 0;
            ribbons.Add(coreStream);
        }

        public void Reveal() { if (!Dissipating) { presenceTarget = 1; openTarget = 1; } }
        public void SetCharge(float value) => charge = Mathf.Clamp01(value);
        public void Flinch() => flinch = 1;
        public void Dissipate() { Dissipating = true; presenceTarget = 0; openTarget = 0; charge = 0; }

        private void Update() => Tick(Time.deltaTime);

        public void Tick(float delta)
        {
            if (delta <= 0) return;
            clock += delta;
            presence = Mathf.MoveTowards(presence, presenceTarget, delta * FadeRate * (Dissipating ? .5f : 1));
            open = Mathf.MoveTowards(open, openTarget, delta * (Dissipating ? .8f : .7f));
            flinch = Mathf.Max(0, flinch - delta * 3.5f);
            for (int i = 0; i < layers.Length; i++)
            {
                // Each bank drifts at its own pace so the darkness keeps depth when the camera moves.
                float speed = .05f + i * .035f;
                layers[i].localPosition = layerRest[i] + new Vector3(Mathf.Sin(clock * speed + i * 2.1f) * (3 + i * 2),
                    Mathf.Sin(clock * speed * .7f + i) * 1.2f * (1 + charge), 0);
                float breathe = 1 + Mathf.Sin(clock * .23f + i) * .015f - charge * .02f * i;
                layers[i].localScale = new Vector3(breathe, breathe, 1);
            }
            if (coreStream != null) coreStream.Alpha = Mathf.MoveTowards(coreStream.Alpha, core != null && core.gameObject.activeInHierarchy ? 1 : 0, delta * 1.5f);
            foreach (Ribbon ribbon in ribbons) Shape(ribbon);
            Apply();
            if (Dissipating && presence <= 0 && open <= 0) gameObject.SetActive(false);
        }

        private void Apply()
        {
            cloud.SetColor("_Color", new Color(1, 1, 1, presence));
            ink.SetColor("_Color", new Color(1, 1, 1, presence));
            float pulse = .65f + .35f * Mathf.Sin(clock * 1.7f);
            veins.SetColor("_Color", new Color(.55f, .05f, .08f) * presence * (pulse + charge * 1.6f) * (1 - flinch * .6f));
            float glare = (1 + charge * 1.8f) * (1 - flinch * .75f * (Mathf.Sin(clock * 60) * .5f + .5f));
            eye.SetColor("_Color", EyeColor * glare * Mathf.Clamp01(open * 1.5f));
            eyes.localScale = new Vector3(1 + charge * .18f, Mathf.Max(.02f, open * (1 + charge * .3f)), 1);
            eyes.localPosition = EyeCentre + new Vector3(Mathf.Sin(clock * 40) * flinch * .5f, Mathf.Sin(clock * .3f) * .8f, 0);
        }

        private Ribbon NewRibbon(Vector3 from, Vector3 control, Vector3 to, float widthFrom, float widthTo, float phase)
        {
            var ribbon = new Ribbon { From = from, Control = control, To = to, WidthFrom = widthFrom, WidthTo = widthTo, Phase = phase, Alpha = 1 };
            ribbon.Mesh = new Mesh { name = "Demon King Ink Ribbon", hideFlags = HideFlags.DontSave };
            ribbon.Mesh.MarkDynamic();
            owned.Add(ribbon.Mesh);
            var item = new GameObject("Ink Stream");
            item.transform.SetParent(transform, false);
            item.AddComponent<MeshFilter>().sharedMesh = ribbon.Mesh;
            AddRenderer(item, ink);
            Shape(ribbon);
            return ribbon;
        }

        // A tapering strip that sways along a quadratic curve and frays into the cloud at its top.
        private void Shape(Ribbon ribbon)
        {
            const int segments = 28;
            Vector3 from = ribbon.From, control = ribbon.Control;
            if (ribbon.Follow != null)
            {
                from = transform.InverseTransformPoint(ribbon.Follow.position) + Vector3.up * 3.5f;
                control = Vector3.Lerp(from, ribbon.To, .5f) + new Vector3(0, 6, -8);
            }
            var vertices = new Vector3[(segments + 1) * 2];
            var colors = new Color[vertices.Length];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[segments * 6];
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                Vector3 sway = new Vector3(Mathf.Sin(clock * .6f + ribbon.Phase + t * 4) * 2.2f * t, 0, Mathf.Cos(clock * .45f + ribbon.Phase + t * 3) * 1.5f * t);
                Vector3 point = Bezier(from, control, ribbon.To, t) + sway;
                Vector3 tangent = Bezier(from, control, ribbon.To, Mathf.Min(1, t + .02f)) - Bezier(from, control, ribbon.To, Mathf.Max(0, t - .02f));
                Vector3 side = Vector3.Cross(tangent, Vector3.forward);
                side = side.sqrMagnitude > .0001f ? side.normalized : Vector3.right;
                float width = Mathf.Lerp(ribbon.WidthFrom, ribbon.WidthTo, t) * (1 + Mathf.Sin(clock * 1.3f + ribbon.Phase * 3 + t * 9) * .18f) * (1 + flinch * .4f);
                // Fade in from the ground and fray out into the cloud.
                float alpha = Mathf.SmoothStep(0, 1, t / .12f) * Mathf.SmoothStep(0, 1, (1 - t) / .25f) * ribbon.Alpha * .85f;
                vertices[i * 2] = point - side * width * .5f;
                vertices[i * 2 + 1] = point + side * width * .5f;
                uv[i * 2] = new Vector2(0, .5f); uv[i * 2 + 1] = new Vector2(1, .5f);
                Color color = new Color(.05f, .035f, .07f, alpha);
                colors[i * 2] = colors[i * 2 + 1] = color;
                if (i == segments) break;
                int v = i * 2, k = i * 6;
                triangles[k] = v; triangles[k + 1] = v + 2; triangles[k + 2] = v + 1;
                triangles[k + 3] = v + 1; triangles[k + 4] = v + 2; triangles[k + 5] = v + 3;
            }
            ribbon.Mesh.Clear();
            ribbon.Mesh.vertices = vertices; ribbon.Mesh.colors = colors; ribbon.Mesh.uv = uv; ribbon.Mesh.triangles = triangles;
            ribbon.Mesh.RecalculateBounds();
        }

        private static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t) =>
            (1 - t) * (1 - t) * a + 2 * (1 - t) * t * b + t * t * c;

        private Transform Layer(string label, Vector3 position, List<(Vector3 centre, Vector2 size, float roll, Color color)> quads, Material material)
        {
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            foreach (var quad in quads)
            {
                Quaternion roll = Quaternion.Euler(0, 0, quad.roll);
                Vector3 right = roll * Vector3.right * quad.size.x * .5f, up = roll * Vector3.up * quad.size.y * .5f;
                int start = vertices.Count;
                vertices.Add(quad.centre - right - up); vertices.Add(quad.centre + right - up);
                vertices.Add(quad.centre + right + up); vertices.Add(quad.centre - right + up);
                uv.Add(Vector2.zero); uv.Add(Vector2.right); uv.Add(Vector2.one); uv.Add(Vector2.up);
                for (int c = 0; c < 4; c++) colors.Add(quad.color);
                triangles.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
            }
            var mesh = new Mesh { name = "Demon King " + label, hideFlags = HideFlags.DontSave };
            mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            owned.Add(mesh);
            var item = new GameObject(label);
            item.transform.SetParent(transform, false);
            item.transform.localPosition = position;
            item.AddComponent<MeshFilter>().sharedMesh = mesh;
            AddRenderer(item, material);
            return item.transform;
        }

        private void Quad(string label, Transform parent, Vector3 position, Vector2 size, float roll, Color color)
        {
            var quads = new List<(Vector3, Vector2, float, Color)> { (Vector3.zero, size, roll, color) };
            Transform item = Layer(label, position, quads, eye);
            item.SetParent(parent, false);
            item.localPosition = position;
        }

        private static void AddRenderer(GameObject item, Material material)
        {
            MeshRenderer renderer = item.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private Material Copy(Material source, string label, float shape, float additive, float softness, Color color)
        {
            var material = new Material(source) { name = label, hideFlags = HideFlags.DontSave };
            material.SetFloat("_Shape", shape);
            material.SetFloat("_Additive", additive);
            material.SetFloat("_Softness", softness);
            material.SetColor("_Color", color);
            owned.Add(material);
            return material;
        }

        private static float Lerp(System.Random random, float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());

        private void OnDestroy()
        {
            foreach (Object item in owned)
                if (item != null) { if (Application.isPlaying) Destroy(item); else DestroyImmediate(item); }
            owned.Clear();
        }
    }
}

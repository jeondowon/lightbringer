using System.Collections.Generic;
using UnityEngine;

namespace Lightbringer.Visuals
{
    // Where a troop or enemy magic bolt lands: a corrupted ground splash (enemies only), a short
    // rising column, and a flash of light; the shared particle bursts come from CombatEffects.SpellImpact.
    // Kept smaller and shorter than hero and boss spells so a battle full of casters stays readable.
    public sealed class SpellImpactVfx : MonoBehaviour
    {
        private struct Layer
        {
            public Renderer Renderer;
            public float Mode, Life, Seed;
            public Vector3 From, To;
            public float Grow;
        }

        private readonly List<Layer> layers = new List<Layer>();
        private bool allied;
        private float age, lifetime;
        private Light flash;
        private float flashPeak, flashDecay;

        public static SpellImpactVfx Spawn(Transform parent, Vector3 ground, float radius, bool allied)
        {
            if (!Application.isPlaying || !ShotVfx.Ready) return null;
            var root = new GameObject(allied ? "Light Bolt Impact (VFX)" : "Corrupted Bolt Impact (VFX)");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(ground, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            var effect = root.AddComponent<SpellImpactVfx>();
            effect.allied = allied;
            effect.Build(allied ? Mathf.Clamp(radius, 1.2f, 4f) : Mathf.Clamp(radius, 1.1f, 4f));
            effect.Tick(0f);
            return effect;
        }

        private void Build(float radius)
        {
            lifetime = allied ? 0.85f : 1.05f;
            Vector3 decal = new Vector3(radius * 2.2f, 1f, radius * 2.2f);
            // Light bolts leave no ground rune circle: it read as the hero aura appearing at the impact.
            if (!allied)
                Add("Impact Sigil", ProceduralMeshes.GroundQuad, ShotVfx.SigilMode, lifetime, Vector3.up * 0.05f, decal, decal, 0f);
            if (allied)
                Add("Light Column", BossStrikeVfx.Cylinder, ShotVfx.ColumnMode, 0.42f, Vector3.zero,
                    new Vector3(radius * 0.28f, 3.4f, radius * 0.28f), new Vector3(radius * 0.5f, 2.6f, radius * 0.5f), 0.3f);
            else
                Add("Corrupted Flames", BossStrikeVfx.Cylinder, ShotVfx.ColumnMode, 0.55f, Vector3.zero,
                    new Vector3(radius * 0.3f, 1.2f, radius * 0.3f), new Vector3(radius * 0.62f, 2.1f, radius * 0.62f), 0.35f);
            Color tint = allied ? CombatEffects.AlliedLight : CombatEffects.Corruption;
            flash = ShotVfx.Glow(transform, Vector3.up * 1.2f, tint, radius * 3.2f, 0f);
            flashPeak = allied ? 6f : 5f;
            flashDecay = allied ? 0.35f : 0.45f;
            if (!allied) CombatEffects.Smoke(transform.position, radius * 0.45f, 6);
        }

        private void Add(string label, Mesh mesh, float mode, float life, Vector3 local, Vector3 from, Vector3 to, float grow)
        {
            Renderer renderer = ShotVfx.Surface(transform, label, mesh);
            if (renderer == null) return;
            renderer.transform.localPosition = local;
            renderer.transform.localScale = from;
            layers.Add(new Layer { Renderer = renderer, Mode = mode, Life = life, Seed = Random.value * 10f, From = from, To = to, Grow = grow });
        }

        private void Update() => Tick(Time.deltaTime);

        private void Tick(float delta)
        {
            age += Mathf.Max(0f, delta);
            foreach (Layer layer in layers)
            {
                bool visible = age <= layer.Life;
                layer.Renderer.enabled = visible;
                if (!visible) continue;
                ShotVfx.Apply(layer.Renderer, layer.Mode, allied, 1f, 1f, layer.Seed, age, layer.Life);
                if (layer.Grow <= 0f) continue;
                float t = Mathf.Clamp01(age / layer.Grow);
                layer.Renderer.transform.localScale = Vector3.Lerp(layer.From, layer.To, 1f - (1f - t) * (1f - t) * (1f - t));
            }
            if (flash != null)
            {
                float rise = Mathf.Clamp01(age / 0.03f);
                float fall = Mathf.Clamp01(1f - age / flashDecay);
                flash.intensity = flashPeak * rise * fall * fall;
            }
            if (age >= lifetime) Destroy(gameObject);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Lightbringer.Visuals
{
    // Living details on a stronghold silhouette: brazier flames with embers at the gate, light motes circling
    // the allied crystal, or corrupt embers and low smoke around the enemy citadel. A few looping particle
    // systems per stronghold; owns its materials and releases them with the battlefield.
    public sealed class StrongholdVfx : MonoBehaviour
    {
        private readonly List<Material> owned = new List<Material>();
        private ArtStyleLibrary style;

        public int SystemCount { get; private set; }

        // `visualRoot` is the stronghold's styled visual (feet space, world metres).
        public static StrongholdVfx Attach(Transform visualRoot, bool allied, ArtStyleLibrary library)
        {
            if (visualRoot == null || library == null || library.vfxMaterial == null) return null;
            GameObject item = new GameObject("Stronghold Ambience (VFX)");
            item.transform.SetParent(visualRoot, false);
            StrongholdVfx vfx = item.AddComponent<StrongholdVfx>();
            vfx.Build(allied, library);
            return vfx;
        }

        private void Build(bool allied, ArtStyleLibrary library)
        {
            style = library;
            Color core = allied ? new Color(1f, 0.85f, 0.5f) : new Color(1f, 0.35f, 0.75f);
            Color edge = allied ? new Color(1f, 0.45f, 0.12f) : new Color(0.55f, 0.1f, 0.6f);
            Material flame = Make(5f, 1.6f, true);
            Material dot = Make(0f, 1.8f, true);
            Material puff = Make(6f, 1.2f, false);
            // Imported gates have no procedural brazier bowls; keep their smoke and embers without floating flames.
            bool imported = library.FindOverride(allied ? VisualId.AlliedStronghold : VisualId.EnemyStronghold) != null;
            Vector3[] braziers = imported ? System.Array.Empty<Vector3>()
                : allied ? SilhouetteFactory.AlliedBraziers : SilhouetteFactory.EnemyBraziers;
            foreach (Vector3 brazier in braziers)
            {
                Flames(brazier, flame, core, edge);
                Embers(brazier + Vector3.up * 0.2f, dot, core);
            }
            if (allied)
            {
                Vector3 crystal = new Vector3(0f, SilhouetteFactory.AlliedCrystalHeight, -0.4f);
                if (imported)
                {
                    // The imported keep is shorter than the procedural one. Anchor motes at its crown.
                    float top = 0f;
                    foreach (Renderer renderer in transform.parent.GetComponentsInChildren<Renderer>())
                        top = Mathf.Max(top, transform.InverseTransformPoint(renderer.bounds.max).y);
                    crystal = new Vector3(0f, Mathf.Max(0f, top - 0.4f), 0f);
                }
                Orbit(crystal, dot, style.lightGlow * 1.4f);
            }
            else
            {
                Rising(new Vector3(0f, 3f, 0.3f), new Vector3(6.5f, 4f, 4f), dot, style.corruptGlow * 1.3f);
                Smoke(new Vector3(0f, 0.4f, 0.3f), puff, new Color(0.18f, 0.12f, 0.22f, 0.5f));
            }
        }

        private Material Make(float shape, float softness, bool additive)
        {
            Material material = new Material(style.vfxMaterial) { name = "LB Stronghold VFX " + shape, hideFlags = HideFlags.DontSave };
            material.SetFloat("_Shape", shape);
            material.SetFloat("_Softness", softness);
            material.SetFloat("_Additive", additive ? 1f : 0f);
            material.SetColor("_Color", additive ? new Color(1.5f, 1.5f, 1.5f) : Color.white);
            if (!additive) material.renderQueue = 2990;
            owned.Add(material);
            return material;
        }

        private ParticleSystem CreateSystem(string name, Vector3 local, Material material, int capacity, out ParticleSystem.MainModule main)
        {
            GameObject item = new GameObject(name);
            item.SetActive(false);
            item.transform.SetParent(transform, false);
            item.transform.localPosition = local;
            ParticleSystem system = item.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            main = system.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = capacity;
            main.startSpeed = 0f;
            ParticleSystemRenderer renderer = item.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            SystemCount++;
            return system;
        }

        private static void Fade(ParticleSystem system, Color from, Color to, float fadeIn)
        {
            ParticleSystem.ColorOverLifetimeModule colour = system.colorOverLifetime;
            colour.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(from, 0f), new GradientColorKey(to, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, fadeIn), new GradientAlphaKey(0f, 1f) });
            colour.color = gradient;
        }

        private static void Size(ParticleSystem system, float from, float to)
        {
            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, from, 1f, to));
        }

        private static void Play(ParticleSystem system)
        {
            system.gameObject.SetActive(true);
            system.Play();
        }

        // Tongues of fire licking up from a brazier bowl; the shape shader animates each tongue.
        private void Flames(Vector3 local, Material material, Color core, Color edge)
        {
            ParticleSystem system = CreateSystem("Brazier Flames", local, material, 40, out ParticleSystem.MainModule main);
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.75f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.45f, 0.7f);
            main.startRotation = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 26f;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.rotation = new Vector3(90f, 0f, 0f);
            shape.radius = 0.12f;
            ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.9f, 1.5f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);
            Fade(system, core, edge, 0.15f);
            Size(system, 1f, 0.35f);
            Play(system);
        }

        private void Embers(Vector3 local, Material material, Color color)
        {
            ParticleSystem system = CreateSystem("Brazier Embers", local, material, 40, out ParticleSystem.MainModule main);
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.08f);
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 7f;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f;
            ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);
            velocity.y = new ParticleSystem.MinMaxCurve(1f, 2.2f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);
            ParticleSystem.NoiseModule noise = system.noise;
            noise.enabled = true;
            noise.strength = 0.6f;
            noise.frequency = 1.2f;
            Fade(system, Color.white, color, 0.1f);
            Play(system);
        }

        // Light motes circling the allied crystal in a slow spiral.
        private void Orbit(Vector3 local, Material material, Color color)
        {
            ParticleSystem system = CreateSystem("Crystal Motes", local, material, 80, out ParticleSystem.MainModule main);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 14f;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.rotation = new Vector3(90f, 0f, 0f);
            shape.radius = 1.4f;
            shape.radiusThickness = 0.2f;
            ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            // Every axis in one curve mode (Unity rejects mixed modes).
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(-0.25f, 0.35f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.orbitalX = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.orbitalY = new ParticleSystem.MinMaxCurve(0.9f, 1.3f);
            velocity.orbitalZ = new ParticleSystem.MinMaxCurve(0f, 0f);
            Fade(system, Color.white, color, 0.25f);
            Play(system);
        }

        // Corrupt embers drifting up around the enemy citadel.
        private void Rising(Vector3 local, Vector3 box, Material material, Color color)
        {
            ParticleSystem system = CreateSystem("Corrupt Embers", local, material, 120, out ParticleSystem.MainModule main);
            main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 3.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 22f;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = box;
            ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.6f, 1.3f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);
            ParticleSystem.NoiseModule noise = system.noise;
            noise.enabled = true;
            noise.strength = 0.5f;
            noise.frequency = 0.6f;
            Fade(system, Color.white, color, 0.2f);
            Play(system);
        }

        // Low dark haze rolling around the citadel foundation (alpha blended).
        private void Smoke(Vector3 local, Material material, Color color)
        {
            ParticleSystem system = CreateSystem("Corrupt Haze", local, material, 40, out ParticleSystem.MainModule main);
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 6f);
            main.startSize = new ParticleSystem.MinMaxCurve(2.2f, 3.6f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = color;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 5f;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(9f, 0.3f, 6.5f);
            ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.02f, 0.12f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);
            ParticleSystem.RotationOverLifetimeModule spin = system.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);
            Fade(system, Color.white, Color.white, 0.3f);
            Play(system);
        }

        private void OnDestroy()
        {
            foreach (Material material in owned)
                if (material != null)
                {
                    if (Application.isPlaying) Destroy(material); else DestroyImmediate(material);
                }
            owned.Clear();
        }
    }
}

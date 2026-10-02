using Lightbringer.Combat;
using UnityEngine;
using UnityEngine.Rendering;

namespace Lightbringer.Visuals
{
    // Fire breath for the rigged Dragon: a flame stream from the mouth bone to the target, timed to the
    // breath clip's thrust, plus a burst of flames where it lands. Damage stays owned by UnitCombat
    // (applied when Attacked fires); this is presentation only.
    [DisallowMultipleComponent]
    public sealed class DragonBreathVfx : MonoBehaviour
    {
        private const float Delay = 0.12f;
        private const float Duration = 0.5f;
        private const float Lifetime = 0.45f;

        [SerializeField] private Transform mouth;
        [SerializeField] private Material fireMaterial;

        private UnitCombat combat;
        private ParticleSystem flame, splash;
        private Vector3 target;
        private float startAt = float.MaxValue, stopAt = float.MinValue, splashAt = float.MaxValue;

        public bool IsBreathing => flame != null && flame.isEmitting;

        public void Configure(Transform mouthPoint, Material material)
        {
            mouth = mouthPoint;
            fireMaterial = material;
        }

        private void OnEnable()
        {
            combat = GetComponentInParent<UnitCombat>();
            if (combat != null) combat.Attacked += OnAttacked;
        }

        private void OnDisable()
        {
            if (combat != null) combat.Attacked -= OnAttacked;
            combat = null;
            startAt = splashAt = float.MaxValue;
            if (flame != null) flame.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void OnAttacked()
        {
            if (mouth == null || fireMaterial == null || combat == null || combat.Target == null) return;
            target = combat.Target.GetAimPoint(mouth.position);
            startAt = Time.time + Delay;
            stopAt = startAt + Duration;
            splashAt = startAt + Lifetime * 0.7f;
        }

        private void Update()
        {
            float now = Time.time;
            if (now >= startAt && now < stopAt)
            {
                if (flame == null) flame = BuildFlame();
                Vector3 toTarget = target - mouth.position;
                if (toTarget.sqrMagnitude > 0.01f) flame.transform.rotation = Quaternion.LookRotation(toTarget);
                ParticleSystem.MainModule main = flame.main;
                float speed = Mathf.Clamp(toTarget.magnitude / Lifetime, 6f, 24f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.85f, speed * 1.1f);
                if (!flame.isEmitting) flame.Play();
            }
            else if (flame != null && flame.isEmitting && now >= stopAt)
            {
                flame.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                startAt = float.MaxValue;
            }
            if (now >= splashAt)
            {
                splashAt = float.MaxValue;
                if (splash == null) splash = BuildSplash();
                splash.transform.position = HeroSpellVfx.GroundBelow(target) + Vector3.up * 0.1f;
                splash.Play();
            }
        }

        private ParticleSystem BuildFlame()
        {
            ParticleSystem system = Create("Fire Breath (VFX)", mouth);
            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(Lifetime * 0.85f, Lifetime);
            main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.55f);
            main.gravityModifier = -0.25f;
            main.maxParticles = 200;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 140f;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 9f;
            shape.radius = 0.08f;
            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(1f, 3.2f)));
            Colour(system);
            return system;
        }

        private ParticleSystem BuildSplash()
        {
            ParticleSystem system = Create("Fire Splash (VFX)", transform);
            ParticleSystem.MainModule main = system.main;
            main.loop = false;
            main.duration = 0.2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.85f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
            main.gravityModifier = -0.4f;
            main.maxParticles = 80;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)45) });
            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 1.6f;
            Colour(system);
            return system;
        }

        private ParticleSystem Create(string name, Transform parent)
        {
            var item = new GameObject(name);
            item.SetActive(false);
            item.transform.SetParent(parent, false);
            ParticleSystem system = item.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystemRenderer renderer = item.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = fireMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            item.SetActive(true);
            return system;
        }

        // White-hot at the mouth, orange body, dark red smoke edge.
        private static void Colour(ParticleSystem system)
        {
            ParticleSystem.ColorOverLifetimeModule colour = system.colorOverLifetime;
            colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.95f, 0.7f), 0f), new GradientColorKey(new Color(1f, 0.55f, 0.15f), 0.35f),
                        new GradientColorKey(new Color(0.8f, 0.18f, 0.05f), 0.75f), new GradientColorKey(new Color(0.25f, 0.05f, 0.02f), 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
            colour.color = gradient;
        }
    }
}

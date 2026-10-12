using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Lightbringer.Visuals
{
    // Title screen backdrop: one lit beacon in a dark ruined field, a red corruption glow far behind it,
    // drifting light motes and a slow camera sway. Built from the Art Pass props; hidden during battles.
    public sealed class TitleStage : MonoBehaviour
    {
        private static readonly Vector3 Origin = new Vector3(2400f, 0f, 2400f);
        private const float BeaconScale = 1.7f;

        private ArtStyleLibrary style;
        private Camera stageCamera;
        private Light beaconLight, corruptionLight;
        private Vector3 lookTarget;
        private float clock, beaconHeight;
        private bool saved;
        private AmbientMode savedAmbientMode;
        private Color savedAmbient, savedFogColor;
        private bool savedFog;
        private FogMode savedFogMode;
        private float savedFogDensity;

        public static TitleStage Build(ArtStyleLibrary style, Transform parent)
        {
            if (style == null || style.battlefieldProps == null) return null;
            var root = new GameObject("Title Stage");
            root.SetActive(false);
            root.transform.SetParent(parent, false);
            root.transform.position = Origin;
            var stage = root.AddComponent<TitleStage>();
            stage.style = style;
            stage.Populate();
            return stage;
        }

        public void SetVisible(bool visible)
        {
            if (gameObject.activeSelf == visible) return;
            if (visible) { Capture(); ApplyMood(); }
            else Restore();
            gameObject.SetActive(visible);
        }

        private void OnDestroy() { if (saved && gameObject.activeSelf) Restore(); }

        private void Populate()
        {
            BattlefieldPropProfile props = style.battlefieldProps;

            // Ground: the battlefield material, swallowed by darkness and fog at a short distance.
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(transform, false);
            ground.transform.localScale = new Vector3(30f, 1f, 30f);
            Destroy(ground.GetComponent<Collider>());
            Renderer groundRenderer = ground.GetComponent<Renderer>();
            if (style.groundMaterial != null) groundRenderer.sharedMaterial = style.groundMaterial;
            groundRenderer.shadowCastingMode = ShadowCastingMode.Off;

            beaconHeight = props.Find(BattlefieldPropKind.AlliedBeacon)?.size.y * BeaconScale ?? 4f;
            Place(props, BattlefieldPropKind.AlliedBeacon, new Vector3(0f, 0f, 0f), 0f, BeaconScale);
            Place(props, BattlefieldPropKind.AlliedMonument, new Vector3(-6.5f, 0f, 3f), 25f, 1f);
            Place(props, BattlefieldPropKind.RuinArch, new Vector3(-3f, 0f, 14f), 8f, 1.4f);
            Place(props, BattlefieldPropKind.RuinColumn, new Vector3(7f, 0f, 6f), -20f, 1.1f);
            Place(props, BattlefieldPropKind.RuinColumn, new Vector3(-10f, 0f, 9f), 40f, 1f);
            Place(props, BattlefieldPropKind.RuinColumn, new Vector3(11f, 0f, 17f), 70f, 0.9f);
            // Far behind the ruins: the corruption the beacon holds back.
            Place(props, BattlefieldPropKind.EnemyCrystal, new Vector3(-9f, 0f, 38f), 10f, 2.4f);
            Place(props, BattlefieldPropKind.EnemyCrystal, new Vector3(12f, 0f, 44f), -30f, 3f);
            Place(props, BattlefieldPropKind.EnemyAltar, new Vector3(2f, 0f, 50f), 180f, 2.6f);

            beaconLight = MakeLight("Beacon Light", new Vector3(0f, beaconHeight * 0.85f, 0f),
                Color.Lerp(style.holyGlow, style.lightGlow, 0.35f), 7f, 34f, LightShadows.None);
            corruptionLight = MakeLight("Corruption Glow", new Vector3(0f, 6f, 46f), style.corruptGlow, 9f, 34f, LightShadows.None);

            var moon = new GameObject("Moonlight").AddComponent<Light>();
            moon.transform.SetParent(transform, false);
            moon.type = LightType.Directional;
            moon.transform.rotation = Quaternion.Euler(52f, 24f, 0f);
            moon.color = new Color(0.45f, 0.55f, 0.85f);
            moon.intensity = 0.35f;
            moon.shadows = LightShadows.None;

            CreateMotes("Beacon Embers", new Vector3(0f, beaconHeight * 0.9f, 0f), 0.6f, 55,
                new Vector2(0.35f, 0.9f), new Vector2(3.5f, 6.5f), new Vector2(0.04f, 0.1f),
                new Color(2.2f, 1.5f, 0.7f), new Color(1.2f, 1.7f, 2.4f));
            CreateMotes("Light Motes", new Vector3(0f, 0.5f, 6f), 16f, 70,
                new Vector2(0.05f, 0.18f), new Vector2(5f, 9f), new Vector2(0.03f, 0.08f),
                new Color(0.8f, 1.1f, 1.8f), new Color(1.4f, 1.1f, 0.7f));
            CreateMotes("Corruption Ash", new Vector3(0f, 0.5f, 40f), 24f, 45,
                new Vector2(0.2f, 0.6f), new Vector2(6f, 10f), new Vector2(0.06f, 0.14f),
                new Color(1.6f, 0.2f, 0.35f), new Color(0.9f, 0.15f, 0.6f));

            var cameraObject = new GameObject("Title Camera");
            cameraObject.transform.SetParent(transform, false);
            stageCamera = cameraObject.AddComponent<Camera>();
            stageCamera.depth = 5f;
            stageCamera.fieldOfView = 36f;
            stageCamera.nearClipPlane = 0.2f;
            stageCamera.farClipPlane = 140f;
            stageCamera.clearFlags = CameraClearFlags.SolidColor;
            stageCamera.backgroundColor = new Color(0.012f, 0.018f, 0.032f);
            if (style.postProcessing != null)
            {
                var volume = new GameObject("Title Post Processing").AddComponent<Volume>();
                volume.transform.SetParent(transform, false);
                volume.isGlobal = true;
                volume.priority = 10f;
                volume.sharedProfile = style.postProcessing;
                stageCamera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            }
            lookTarget = transform.position + new Vector3(-2.4f, beaconHeight * 0.55f, 0f);
            Sway();
        }

        private void Place(BattlefieldPropProfile props, BattlefieldPropKind kind, Vector3 local, float yaw, float scale)
        {
            BattlefieldPropAsset asset = props.Find(kind);
            if (asset == null) return;
            GameObject instance = Instantiate(asset.prefab, transform);
            instance.name = kind.ToString();
            instance.transform.localPosition = local;
            instance.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            instance.transform.localScale = Vector3.one * scale;
        }

        private Light MakeLight(string name, Vector3 local, Color color, float intensity, float range, LightShadows shadows)
        {
            var light = new GameObject(name).AddComponent<Light>();
            light.transform.SetParent(transform, false);
            light.transform.localPosition = local;
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = shadows;
            return light;
        }

        private void CreateMotes(string name, Vector3 local, float radius, int count, Vector2 rise, Vector2 life,
            Vector2 size, Color colorA, Color colorB)
        {
            Material material = style.auraMotesMaterial != null ? style.auraMotesMaterial : style.vfxMaterial;
            if (material == null) return;
            var item = new GameObject(name);
            item.SetActive(false);
            item.transform.SetParent(transform, false);
            item.transform.localPosition = local;
            var system = item.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.prewarm = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startColor = new ParticleSystem.MinMaxGradient(colorA, colorB);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = count * 2;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = count / Mathf.Max(0.5f, (life.x + life.y) * 0.5f);
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.rotation = new Vector3(90f, 0f, 0f);
            shape.radius = radius;
            shape.radiusThickness = 1f;
            ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);
            velocity.y = new ParticleSystem.MinMaxCurve(rise.x, rise.y);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);
            ParticleSystem.ColorOverLifetimeModule fade = system.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0f, 1f) });
            fade.color = new ParticleSystem.MinMaxGradient(gradient);
            var renderer = item.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            item.SetActive(true);
        }

        private void Capture()
        {
            savedAmbientMode = RenderSettings.ambientMode;
            savedAmbient = RenderSettings.ambientLight;
            savedFog = RenderSettings.fog;
            savedFogMode = RenderSettings.fogMode;
            savedFogColor = RenderSettings.fogColor;
            savedFogDensity = RenderSettings.fogDensity;
            saved = true;
        }

        private void ApplyMood()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.035f, 0.05f, 0.085f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.014f, 0.02f, 0.036f);
            RenderSettings.fogDensity = 0.021f;
        }

        private void Restore()
        {
            if (!saved) return;
            RenderSettings.ambientMode = savedAmbientMode;
            RenderSettings.ambientLight = savedAmbient;
            RenderSettings.fog = savedFog;
            RenderSettings.fogMode = savedFogMode;
            RenderSettings.fogColor = savedFogColor;
            RenderSettings.fogDensity = savedFogDensity;
            saved = false;
        }

        private void Update()
        {
            clock += Time.unscaledDeltaTime;
            Sway();
            // Slow breathing plus a faint flicker so the beacon feels alive, not a static lamp.
            float breath = 1f + 0.08f * Mathf.Sin(clock * 1.3f) + 0.05f * Mathf.Sin(clock * 7.1f + 1.7f);
            if (beaconLight != null) beaconLight.intensity = 7f * breath;
            if (corruptionLight != null) corruptionLight.intensity = 9f * (1f + 0.1f * Mathf.Sin(clock * 0.7f));
        }

        private void Sway()
        {
            if (stageCamera == null) return;
            Vector3 offset = new Vector3(Mathf.Sin(clock * 0.09f) * 3.2f, 2.1f + Mathf.Sin(clock * 0.06f) * 0.35f,
                -15.5f + Mathf.Sin(clock * 0.05f) * 1.4f);
            stageCamera.transform.position = transform.position + offset;
            stageCamera.transform.rotation = Quaternion.LookRotation(lookTarget - stageCamera.transform.position, Vector3.up);
        }
    }
}

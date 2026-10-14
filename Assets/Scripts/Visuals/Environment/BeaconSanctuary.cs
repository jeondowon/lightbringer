using System.Collections.Generic;
using Lightbringer.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Lightbringer.Visuals
{
    // Stage-one landmark and victory presentation. Never changes combat or progression rules.
    public sealed class BeaconSanctuary : MonoBehaviour
    {
        public static readonly Vector2 Centre = new Vector2(-13f, Core.BattlefieldLayout.CentreZ);
        public const float PresentationDuration = 4.5f;
        private readonly List<Material> owned = new List<Material>();
        private readonly List<Renderer> runes = new List<Renderer>();
        private StageObjective objective;
        private Camera battleCamera;
        private Material glow;
        private Transform core;
        private LineRenderer pulse;
        private Light radiance;
        private Transform halo;
        private Mesh haloMesh;
        private Material haloMaterial;
        private Renderer ground;
        private MaterialPropertyBlock groundLight;
        private float elapsed;
        private Vector3 cameraStart;
        private Quaternion rotationStart;
        public bool IsLit { get; private set; }
        public bool IsPresenting => IsLit && elapsed < PresentationDuration && isActiveAndEnabled;

        public static BeaconSanctuary Build(Transform parent, GameObject beacon, BattlefieldEnvironment terrain)
        {
            GameObject root = new GameObject("Beacon Light and Sanctuary");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(Centre.x, terrain.SurfaceHeight(Centre.x, Centre.y), Centre.y);
            BeaconSanctuary sanctuary = root.AddComponent<BeaconSanctuary>();
            sanctuary.ground = terrain.Terrain.GetComponent<Renderer>();
            sanctuary.Populate(beacon);
            return sanctuary;
        }

        public void Configure(StageObjective target, Camera camera)
        {
            if (objective != null) objective.Completed -= OnCompleted;
            objective = target;
            battleCamera = camera;
            if (objective != null) objective.Completed += OnCompleted;
        }

        private Material Make(Color color, bool emissive = false)
        {
            Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"))
                { name = "Beacon Sanctuary", hideFlags = HideFlags.DontSave, enableInstancing = true };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.25f);
            if (emissive) material.EnableKeyword("_EMISSION");
            owned.Add(material);
            return material;
        }

        private GameObject Shape(string label, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            GameObject item = GameObject.CreatePrimitive(type);
            item.name = label;
            item.transform.SetParent(transform, false);
            item.transform.localPosition = position;
            item.transform.localScale = scale;
            Collider collider = item.GetComponent<Collider>();
            collider.enabled = false;
            if (Application.isPlaying) Destroy(collider); else DestroyImmediate(collider);
            item.GetComponent<Renderer>().sharedMaterial = material;
            return item;
        }

        private void Populate(GameObject beacon)
        {
            groundLight = new MaterialPropertyBlock();
            Material trim = Make(new Color(0.48f, 0.43f, 0.30f));
            glow = Make(new Color(0.10f, 0.08f, 0.045f), true);
            // Low, decorative paving: the traversable terrain remains flat and has no added colliders.
            Shape("Beacon Plinth", PrimitiveType.Cylinder, new Vector3(0f, 0.07f, 0f), new Vector3(3.6f, 0.07f, 3.6f), trim);
            // Reuse the authored beacon; only the local copy is darkened, leaving the title prop intact.
            foreach (Renderer renderer in beacon.GetComponentsInChildren<Renderer>())
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null) continue;
                    Material copy = new Material(materials[i]) { hideFlags = HideFlags.DontSave };
                    if (copy.HasProperty("_BaseColor")) copy.SetColor("_BaseColor", new Color(0.5f, 0.52f, 0.47f));
                    copy.DisableKeyword("_EMISSION");
                    owned.Add(copy);
                    materials[i] = copy;
                }
                renderer.sharedMaterials = materials;
            }
            core = Shape("Dormant Light Core", PrimitiveType.Sphere, new Vector3(0f, 7.35f, 0f), new Vector3(0.95f, 1.45f, 0.95f), glow).transform;
            for (int level = 0; level < 7; level++)
                foreach (int side in new[] { -1, 1 })
                {
                    GameObject rune = Shape("Ascending Rune", PrimitiveType.Cube,
                        Quaternion.Euler(0f, 35f, 0f) * new Vector3(side * 0.23f, 0.8f + level * 0.65f, -0.49f),
                        new Vector3(0.055f, 0.18f, 0.025f), glow);
                    rune.transform.localRotation = Quaternion.Euler(0f, 35f, 35f * side);
                    runes.Add(rune.GetComponent<Renderer>());
                }
            GameObject lightObject = new GameObject("Beacon Golden Radiance");
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.localPosition = new Vector3(0f, 6.6f, 0f);
            radiance = lightObject.AddComponent<Light>();
            radiance.type = LightType.Point;
            radiance.color = new Color(1f, 0.76f, 0.38f);
            radiance.range = 27f;
            radiance.intensity = 0f;
            radiance.shadows = LightShadows.None;
            Shader haloShader = Shader.Find("Lightbringer/VFX Shape");
            if (haloShader != null)
            {
                haloMaterial = new Material(haloShader) { name = "Beacon Soft Radiance", hideFlags = HideFlags.DontSave };
                haloMaterial.SetFloat("_Shape", 0f);
                haloMaterial.SetFloat("_Softness", 2f);
                haloMaterial.SetFloat("_Additive", 1f);
                owned.Add(haloMaterial);
                GameObject haze = Shape("Golden Halo", PrimitiveType.Quad, core.localPosition, Vector3.one * 4.5f, haloMaterial);
                halo = haze.transform;
                MeshFilter filter = haze.GetComponent<MeshFilter>();
                haloMesh = Instantiate(filter.sharedMesh);
                haloMesh.hideFlags = HideFlags.DontSave;
                Color[] colors = new Color[haloMesh.vertexCount];
                for (int i = 0; i < colors.Length; i++) colors[i] = Color.white;
                haloMesh.colors = colors;
                filter.sharedMesh = haloMesh;
                haze.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            GameObject ring = new GameObject("Consecration Wave");
            ring.transform.SetParent(transform, false);
            pulse = ring.AddComponent<LineRenderer>();
            pulse.useWorldSpace = false;
            pulse.loop = true;
            pulse.positionCount = 96;
            pulse.widthMultiplier = 0.06f;
            pulse.sharedMaterial = glow;
            pulse.shadowCastingMode = ShadowCastingMode.Off;
            pulse.enabled = false;
            ApplyLight(0f);
        }

        private void OnCompleted(bool victory)
        {
            if (!victory || IsLit) return;
            IsLit = true;
            elapsed = 0f;
            if (battleCamera != null)
            {
                cameraStart = battleCamera.transform.position;
                rotationStart = battleCamera.transform.rotation;
            }
        }

        private void Update()
        {
            if (IsPresenting) AdvancePresentation(Time.unscaledDeltaTime);
        }

        // Explicit delta also permits deterministic Editor checks and preview renders.
        public void AdvancePresentation(float seconds)
        {
            if (!IsLit) return;
            elapsed = Mathf.Min(PresentationDuration, elapsed + Mathf.Max(0f, seconds));
            float amount = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.7f, 3.1f, elapsed));
            ApplyLight(amount);
            if (battleCamera != null)
            {
                float blend = Mathf.SmoothStep(0f, 1f, elapsed / 2f);
                Vector3 destination = transform.TransformPoint(new Vector3(12f, 7f, -17f));
                Quaternion rotation = Quaternion.LookRotation(transform.TransformPoint(new Vector3(0f, 4f, 0f)) - destination);
                battleCamera.transform.SetPositionAndRotation(Vector3.Lerp(cameraStart, destination, blend),
                    Quaternion.Slerp(rotationStart, rotation, blend));
                if (halo != null)
                {
                    halo.rotation = battleCamera.transform.rotation;
                    halo.position = core.position - battleCamera.transform.forward * 0.85f;
                }
            }
            pulse.enabled = elapsed > 2.5f && elapsed < PresentationDuration;
            if (pulse.enabled)
            {
                float t = Mathf.InverseLerp(2.5f, PresentationDuration, elapsed);
                pulse.widthMultiplier = 0.16f * (1f - t);
                for (int i = 0; i < pulse.positionCount; i++)
                {
                    float angle = i * Mathf.PI * 2f / pulse.positionCount;
                    pulse.SetPosition(i, new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * Mathf.Lerp(1f, 18f, t) + Vector3.up * 0.1f);
                }
            }
        }

        private void ApplyLight(float amount)
        {
            glow.SetColor("_EmissionColor", new Color(1f, 0.64f, 0.2f) * Mathf.Lerp(0.035f, 7f, amount));
            radiance.intensity = 12f * amount;
            if (haloMaterial != null) haloMaterial.SetColor("_Color", new Color(1f, 0.6f, 0.16f) * (amount * 1.3f));
            if (ground != null)
            {
                ground.GetPropertyBlock(groundLight);
                Vector3 p = transform.position;
                groundLight.SetVector("_BeaconLight", new Vector4(p.x, p.y, p.z, amount * 2f));
                ground.SetPropertyBlock(groundLight);
            }
            core.localScale = new Vector3(0.95f, 1.45f, 0.95f) * Mathf.Lerp(0.6f, 1f, amount);
            for (int i = 0; i < runes.Count; i++) runes[i].enabled = !IsLit || elapsed > i / 2 * 0.12f;
        }

        public static void BuildCampLantern(Transform parent, Vector3 position)
        {
            GameObject root = new GameObject("Camp Lantern");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            BeaconSanctuary lantern = root.AddComponent<BeaconSanctuary>();
            Material iron = lantern.Make(new Color(0.16f, 0.14f, 0.11f));
            Material ember = lantern.Make(new Color(0.8f, 0.42f, 0.12f), true);
            ember.SetColor("_EmissionColor", new Color(1f, 0.5f, 0.1f) * 1.5f);
            lantern.Shape("Iron Post", PrimitiveType.Cylinder, new Vector3(0f, 0.7f, 0f), new Vector3(0.12f, 0.7f, 0.12f), iron);
            lantern.Shape("Lantern", PrimitiveType.Cube, new Vector3(0f, 1.5f, 0f), new Vector3(0.28f, 0.38f, 0.28f), ember);
            lantern.Shape("Lantern Roof", PrimitiveType.Cube, new Vector3(0f, 1.72f, 0f), new Vector3(0.4f, 0.07f, 0.4f), iron);
            lantern.enabled = false;
        }

        private void OnDestroy()
        {
            if (objective != null) objective.Completed -= OnCompleted;
            if (haloMesh != null)
            {
                if (Application.isPlaying) Destroy(haloMesh); else DestroyImmediate(haloMesh);
            }
            foreach (Material material in owned)
                if (material != null)
                {
                    if (Application.isPlaying) Destroy(material); else DestroyImmediate(material);
                }
        }
    }
}

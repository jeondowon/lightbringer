using System.Collections.Generic;
using Lightbringer.Combat;
using UnityEngine;
using UnityEngine.Rendering;

namespace Lightbringer.Visuals
{
    // A foot rune and a low circular veil show the hero aura's reach without recolouring the model.
    // Presentation only; HeroAura and UnitCombat own the bonus. Healers (combat disabled) show it too.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnitCombat))]
    public sealed class AuraBlessingVisual : MonoBehaviour
    {
        private const float CheckInterval = 0.1f;
        private const float MoteInterval = 0.2f;
        private const float FadeTime = 0.18f;
        private const float MarkDiameter = 1.6f;
        private const float VeilDiameter = 1.35f;
        private const float VeilHeight = 0.65f;
        private const float MoteSpread = 0.4f;
        // Large bodies get a modestly larger blessing: footprints from SmallBody to LargeBody (body length in
        // metres; mounted Knight ~3, Dragon ~4.8) scale it from 1x up to MaxSizeFactor.
        private const float SmallBody = 1.5f, LargeBody = 4.8f, MaxSizeFactor = 1.3f;

        private static readonly Dictionary<long, Material> markMaterials = new Dictionary<long, Material>();

        [SerializeField] private ArtStyleLibrary style;
        [SerializeField] private float feetOffset = -0.8f;

        private UnitCombat combat;
        private Combatant health;
        private Transform mark;
        private Transform veil;
        private float markAmount;
        private float checkTimer, moteTimer;
        // Centre of a large body's footprint in the unit's frame, and the size multiplier from MeasureBody.
        private Vector3 bodyCenter;
        private float sizeFactor = 1f;

        public bool IsShowing { get; private set; }

        public void Configure(ArtStyleLibrary library, float feet, Transform visual)
        {
            style = library;
            feetOffset = feet;
            MeasureBody(visual);
        }

        private Color Tint => style != null ? (Color)style.auraColor : CombatEffects.AlliedLight;
        private Vector3 Feet => transform.position + Vector3.up * feetOffset + transform.rotation * bodyCenter;

        private void OnEnable()
        {
            combat = GetComponent<UnitCombat>();
            health = GetComponent<Combatant>();
            checkTimer = 0f;
        }

        private void OnDisable()
        {
            IsShowing = false;
            markAmount = 0f;
            if (mark != null) mark.gameObject.SetActive(false);
            if (veil != null) veil.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (mark != null) Destroy(mark.gameObject);
            if (veil != null) Destroy(veil.gameObject);
        }

        private void Update()
        {
            if (!Application.isPlaying || style == null || combat == null) return;
            float delta = Time.deltaTime;
            checkTimer -= delta;
            if (checkTimer <= 0f)
            {
                checkTimer = CheckInterval;
                // Fighters show the bonus they actually receive; a Priest (combat disabled) shows standing inside.
                IsShowing = combat.enabled ? combat.isActiveAndEnabled && combat.IsEmpowered
                    : Aura.HeroAura.AnyCovers(health);
            }
            markAmount = Mathf.MoveTowards(markAmount, IsShowing ? 1f : 0f, delta / FadeTime);
            UpdateMark();
            UpdateVeil();
            if (!IsShowing) return;
            moteTimer -= delta;
            if (moteTimer > 0f) return;
            // Large bodies spread more motes over their wider footprint.
            moteTimer = MoteInterval / sizeFactor * Random.Range(0.7f, 1.3f);
            CombatEffects.AuraMote(style, Feet, Tint, MoteSpread * sizeFactor);
        }

        // Measures the largest body renderer (props such as spears stay out) from its mesh bounds, so the
        // result is independent of the current animation pose and hit squash.
        private void MeasureBody(Transform visual)
        {
            bodyCenter = Vector3.zero;
            sizeFactor = 1f;
            if (visual == null) return;
            Mesh bodyMesh = null;
            Transform bodyTransform = null;
            float largest = 0f;
            foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                Mesh mesh = renderer is SkinnedMeshRenderer skin ? skin.sharedMesh
                    : renderer.TryGetComponent(out MeshFilter filter) ? filter.sharedMesh : null;
                if (mesh == null) continue;
                Vector3 size = Vector3.Scale(mesh.bounds.size, renderer.transform.lossyScale);
                float volume = Mathf.Abs(size.x * size.y * size.z);
                if (volume <= largest) continue;
                largest = volume;
                bodyMesh = mesh;
                bodyTransform = renderer.transform;
            }
            if (bodyMesh == null) return;
            Bounds local = bodyMesh.bounds;
            Quaternion toUnit = Quaternion.Inverse(transform.rotation);
            Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = local.center + Vector3.Scale(local.extents,
                    new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                Vector3 p = toUnit * (bodyTransform.TransformPoint(corner) - transform.position);
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }
            float footprint = Mathf.Max(max.x - min.x, max.z - min.z);
            if (footprint <= SmallBody) return;
            // Only large bodies grow and re-centre under their middle; infantry stay as before.
            sizeFactor = Mathf.Lerp(1f, MaxSizeFactor, Mathf.InverseLerp(SmallBody, LargeBody, footprint));
            bodyCenter = new Vector3((min.x + max.x) * 0.5f, 0f, (min.z + max.z) * 0.5f);
        }

        private void UpdateMark()
        {
            if (markAmount <= 0.001f)
            {
                if (mark != null && mark.gameObject.activeSelf) mark.gameObject.SetActive(false);
                return;
            }
            if (mark == null && !CreateMark()) return;
            if (!mark.gameObject.activeSelf) mark.gameObject.SetActive(true);
            // Pop in slightly oversized, settle, and keep turning slowly in world space.
            float eased = 1f - (1f - markAmount) * (1f - markAmount);
            float size = MarkDiameter * sizeFactor * Mathf.Lerp(1.35f, 1f, eased) * eased;
            mark.SetPositionAndRotation(Feet + Vector3.up * 0.04f, Quaternion.Euler(0f, Time.time * 44.8f, 0f));
            mark.localScale = LocalScale(new Vector3(size, 1f, size));
        }

        private bool CreateMark()
        {
            Material material = MarkMaterial(style);
            if (material == null) return false;
            GameObject item = new GameObject("Aura Blessing Mark");
            // Under the unit (not its styled visual) so hit squash and flashes leave it alone.
            item.transform.SetParent(transform, false);
            item.AddComponent<MeshFilter>().sharedMesh = ProceduralMeshes.GlowQuad;
            MeshRenderer renderer = item.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            mark = item.transform;
            return true;
        }

        private void UpdateVeil()
        {
            if (markAmount <= 0.001f)
            {
                if (veil != null) veil.gameObject.SetActive(false);
                return;
            }
            if (veil == null)
            {
                if (style.auraBlessingMaterial == null) return;
                GameObject item = new GameObject("Aura Lower Leg Veil");
                item.transform.SetParent(transform, false);
                item.AddComponent<MeshFilter>().sharedMesh = ProceduralMeshes.AuraVeil;
                MeshRenderer renderer = item.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = style.auraBlessingMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                veil = item.transform;
            }
            veil.gameObject.SetActive(true);
            veil.SetPositionAndRotation(Feet + Vector3.up * 0.025f, Quaternion.identity);
            veil.localScale = LocalScale(new Vector3(VeilDiameter, VeilHeight, VeilDiameter) * (sizeFactor * markAmount));
        }

        private Vector3 LocalScale(Vector3 worldScale)
        {
            Vector3 parentScale = transform.lossyScale;
            return new Vector3(worldScale.x / Mathf.Max(0.001f, Mathf.Abs(parentScale.x)),
                worldScale.y / Mathf.Max(0.001f, Mathf.Abs(parentScale.y)),
                worldScale.z / Mathf.Max(0.001f, Mathf.Abs(parentScale.z)));
        }

        private static Material MarkMaterial(ArtStyleLibrary library)
        {
            if (library == null || library.auraRunesMaterial == null) return null;
            long key = library.GetInstanceID();
            if (markMaterials.TryGetValue(key, out Material cached) && cached != null) return cached;
            Material material = new Material(library.auraRunesMaterial) { name = "LB Aura Blessing Mark", hideFlags = HideFlags.DontSave };
            material.SetFloat("_Intensity", 0.45f);
            material.SetFloat("_LineWidth", 0.007f);
            material.SetFloat("_FillAlpha", 0.025f);
            material.renderQueue = 3000 - 5;
            markMaterials[key] = material;
            return material;
        }
    }
}

using System.Collections.Generic;
using Lightbringer.Combat;
using UnityEngine;
using UnityEngine.Rendering;

namespace Lightbringer.Visuals
{
    // Shows that an allied troop is empowered by the hero aura, whatever shader its model uses:
    // - an additive Aura Blessing shell appended to its renderers (rim glow, driven by the global aura sphere),
    // - a rotating rune mark at its feet while the bonus applies,
    // - a short blessing flash on entering the aura and light motes trickling up while it stays.
    // Presentation only; HeroAura and UnitCombat own the bonus.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnitCombat))]
    public sealed class AuraBlessingVisual : MonoBehaviour
    {
        private const float CheckInterval = 0.1f;
        private const float MoteInterval = 0.2f;
        private const float FadeTime = 0.18f;
        private const float MarkDiameter = 1.6f;

        private static readonly Dictionary<long, Material> markMaterials = new Dictionary<long, Material>();

        [SerializeField] private ArtStyleLibrary style;
        [SerializeField] private float feetOffset = -0.8f;

        private UnitCombat combat;
        private Transform mark;
        private float markAmount;
        private float checkTimer, moteTimer;

        public bool IsShowing { get; private set; }
        public int ShellCount { get; private set; }

        public void Configure(ArtStyleLibrary library, float feet, Transform visual)
        {
            style = library;
            feetOffset = feet;
            if (library != null) ShellCount = AppendShell(visual, library.auraBlessingMaterial);
        }

        // An extra material re-draws a renderer's last sub-mesh, so only single sub-mesh renderers get the shell
        // (troop models and their props use one material each).
        public static int AppendShell(Transform visual, Material shell)
        {
            if (visual == null || shell == null) return 0;
            int count = 0;
            foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer) continue;
                Material[] current = renderer.sharedMaterials;
                if (current.Length != 1 || current[0] == shell || SubMeshes(renderer) > 1) continue;
                renderer.sharedMaterials = new[] { current[0], shell };
                count++;
            }
            return count;
        }

        private static int SubMeshes(Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer skinned) return skinned.sharedMesh != null ? skinned.sharedMesh.subMeshCount : 1;
            return renderer.TryGetComponent(out MeshFilter filter) && filter.sharedMesh != null ? filter.sharedMesh.subMeshCount : 1;
        }

        private Color Tint => style != null ? (Color)style.auraColor : CombatEffects.AlliedLight;
        private Vector3 Feet => transform.position + Vector3.up * feetOffset;

        private void OnEnable()
        {
            combat = GetComponent<UnitCombat>();
            checkTimer = 0f;
        }

        private void OnDisable()
        {
            IsShowing = false;
            markAmount = 0f;
            if (mark != null) mark.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (mark != null) Destroy(mark.gameObject);
        }

        private void Update()
        {
            if (!Application.isPlaying || style == null || combat == null) return;
            float delta = Time.deltaTime;
            checkTimer -= delta;
            if (checkTimer <= 0f)
            {
                checkTimer = CheckInterval;
                bool empowered = combat.isActiveAndEnabled && combat.IsEmpowered;
                if (empowered && !IsShowing) CombatEffects.Blessing(style, Feet, Tint);
                IsShowing = empowered;
            }
            markAmount = Mathf.MoveTowards(markAmount, IsShowing ? 1f : 0f, delta / FadeTime);
            UpdateMark();
            if (!IsShowing) return;
            moteTimer -= delta;
            if (moteTimer > 0f) return;
            moteTimer = MoteInterval * Random.Range(0.7f, 1.3f);
            CombatEffects.AuraMote(style, Feet, Tint, 0.4f);
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
            float size = MarkDiameter * Mathf.Lerp(1.35f, 1f, eased) * eased;
            mark.SetPositionAndRotation(Feet + Vector3.up * 0.04f, Quaternion.Euler(0f, Time.time * 35f, 0f));
            mark.localScale = new Vector3(size, 1f, size);
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

        private static Material MarkMaterial(ArtStyleLibrary library)
        {
            if (library == null || library.vfxMaterial == null) return null;
            long key = library.GetInstanceID();
            if (markMaterials.TryGetValue(key, out Material cached) && cached != null) return cached;
            Material material = new Material(library.vfxMaterial) { name = "LB Aura Blessing Mark", hideFlags = HideFlags.DontSave };
            material.SetFloat("_Shape", 4f);
            material.SetFloat("_Additive", 1f);
            material.SetFloat("_GlyphCount", 10f);
            material.SetColor("_Color", library.auraColor * 0.75f);
            material.renderQueue = 3000 - 5;
            markMaterials[key] = material;
            return material;
        }
    }
}

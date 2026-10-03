using System.Collections.Generic;
using Lightbringer.CameraSystem;
using Lightbringer.Combat;
using Lightbringer.Player;
using UnityEngine;

namespace Lightbringer.Visuals
{
    // Hit feedback on the receiving unit: a brief white flash, a squash of the visual, sparks at the contact
    // point (or a soft burst for magic), camera weight for big blows near the hero, and motes when it falls.
    // Visual only; gameplay transforms and colliders are never touched.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Combatant))]
    public sealed class HitReaction : MonoBehaviour
    {
        private const float FlashTime = 0.12f;
        private const float SquashTime = 0.16f;
        private const string ToonShader = "Lightbringer/Toon";

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private ArtStyleLibrary style;
        [SerializeField] private Transform visualRoot;
        [Tooltip("Objectives flash but do not squash.")]
        [SerializeField] private bool squash = true;

        private struct Slot
        {
            public Renderer Renderer;
            public int Index;
            public bool Toon;
            public Color Base;
            public Color Emission;
        }

        private Combatant self;
        private readonly List<Slot> slots = new List<Slot>();
        private MaterialPropertyBlock block;
        private bool cached;
        private float flash = -1f, squashAge = -1f, squashStrength;
        private Vector3 baseScale = Vector3.one;
        private Vector3 hitDirection;

        public bool IsFlashing => flash >= 0f;

        public void Configure(ArtStyleLibrary library, Transform visual, bool canSquash)
        {
            style = library;
            visualRoot = visual;
            squash = canSquash;
            if (visual != null) baseScale = visual.localScale;
            cached = false;
        }

        private void OnEnable()
        {
            self = GetComponent<Combatant>();
            self.Damaged += OnDamaged;
        }

        private void OnDisable()
        {
            if (self != null) self.Damaged -= OnDamaged;
            if (flash >= 0f || squashAge >= 0f) Restore();
        }

        private void OnDamaged(Combatant unit, float amount, Combatant attacker)
        {
            if (!Application.isPlaying) return;
            Vector3 centre = unit.transform.position;
            Vector3 from = attacker != null ? attacker.transform.position : centre - transform.forward;
            hitDirection = centre - from;
            hitDirection.y = 0f;
            hitDirection = hitDirection.sqrMagnitude > 0.0001f ? hitDirection.normalized : -transform.forward;
            float weight = Mathf.Clamp(amount / 14f, 0.4f, 2.5f);

            ProjectileKind shot = attacker != null && attacker.TryGetComponent(out UnitCombat combat)
                ? combat.ProjectileType : ProjectileKind.None;
            bool alliedAttacker = attacker == null || attacker.Faction == Faction.Allied;
            Vector3 contact = ContactPoint(unit, from);
            // Spell impacts are drawn by the shooter's burst; a melee blow or arrow sprays sparks here.
            if (shot == ProjectileKind.Bolt)
                CombatEffects.Muzzle(style, contact, alliedAttacker ? CombatEffects.AlliedLight : CombatEffects.Corruption);
            else
                CombatEffects.Hit(style, contact, hitDirection, alliedAttacker ? CombatEffects.SteelSpark : CombatEffects.Corruption,
                    shot == ProjectileKind.Arrow ? weight * 0.6f : weight);

            // Big blows (charges, breath, slams, area spells) and any hit on the hero give the view some weight.
            float shake = Mathf.Clamp01((amount - 12f) / 45f) * 0.7f;
            if (GetComponent<HeroAbilities>() != null) shake = Mathf.Max(shake, 0.15f + amount / 60f);
            if (shake > 0f) ThirdPersonCamera.Shake(centre, shake);

            if (unit.CurrentHealth <= 0f)
            {
                Restore();
                CombatEffects.Death(style, centre, unit.Faction == Faction.Allied ? CombatEffects.HolyGold : CombatEffects.Corruption,
                    Size());
                return;
            }
            flash = 0f;
            squashAge = 0f;
            squashStrength = Mathf.Lerp(0.07f, 0.16f, Mathf.InverseLerp(0.4f, 2.5f, weight));
            Apply(1f);
        }

        // Where the blow meets the body: the target's surface toward the attacker, at chest height.
        private static Vector3 ContactPoint(Combatant unit, Vector3 from)
        {
            Vector3 aim = unit.GetAimPoint(from);
            Vector3 towards = from - aim;
            towards.y = 0f;
            float radius = unit.TryGetComponent(out CharacterController body) ? body.radius : 0f;
            return aim + (towards.sqrMagnitude > 0.0001f ? towards.normalized * radius : Vector3.zero) + Vector3.up * 0.15f;
        }

        private float Size()
        {
            Cache();
            Bounds bounds = default;
            bool any = false;
            foreach (Slot slot in slots)
            {
                if (slot.Renderer == null || slot.Index != 0) continue;
                if (!any) { bounds = slot.Renderer.bounds; any = true; }
                else bounds.Encapsulate(slot.Renderer.bounds);
            }
            return any ? Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z) : 1f;
        }

        private void Update()
        {
            float delta = Time.deltaTime;
            if (flash >= 0f)
            {
                flash += delta;
                if (flash >= FlashTime) { flash = -1f; Apply(0f); }
                else Apply(1f - flash / FlashTime);
            }
            if (squashAge >= 0f && squash && visualRoot != null)
            {
                squashAge += delta;
                float t = Mathf.Clamp01(squashAge / SquashTime);
                // Snap squashed on contact, spring back with a little overshoot.
                float pulse = Mathf.Sin(t * Mathf.PI * 1.5f) * (1f - t) * squashStrength;
                visualRoot.localScale = Vector3.Scale(baseScale, new Vector3(1f + pulse, 1f - pulse, 1f + pulse));
                if (t >= 1f) { squashAge = -1f; visualRoot.localScale = baseScale; }
            }
            else if (squashAge >= 0f) squashAge = -1f;
        }

        private void Restore()
        {
            flash = -1f;
            squashAge = -1f;
            Apply(0f);
            if (visualRoot != null && squash) visualRoot.localScale = baseScale;
        }

        private void Cache()
        {
            if (cached) return;
            cached = true;
            slots.Clear();
            Transform source = visualRoot != null ? visualRoot : transform;
            foreach (Renderer renderer in source.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer) continue;
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material material = materials[i];
                    if (material == null) continue;
                    bool toon = material.shader != null && material.shader.name == ToonShader;
                    slots.Add(new Slot
                    {
                        Renderer = renderer, Index = i, Toon = toon,
                        Base = material.HasProperty(BaseColorId) ? material.GetColor(BaseColorId) : Color.white,
                        Emission = material.HasProperty(EmissionId) ? material.GetColor(EmissionId) : Color.black,
                    });
                }
            }
        }

        // Toon materials add emission directly; URP Lit models lift their base colour toward white instead
        // (Lit emission needs a keyword a property block cannot switch on).
        private void Apply(float amount)
        {
            Cache();
            if (block == null) block = new MaterialPropertyBlock();
            foreach (Slot slot in slots)
            {
                if (slot.Renderer == null) continue;
                block.Clear();
                if (amount > 0f)
                {
                    if (slot.Toon) block.SetColor(EmissionId, slot.Emission + Color.white * (0.9f * amount));
                    else block.SetColor(BaseColorId, Color.Lerp(slot.Base, new Color(2.2f, 2.2f, 2.2f, slot.Base.a), amount));
                }
                // An empty block removes the override, keeping the renderer SRP-batcher friendly between hits.
                slot.Renderer.SetPropertyBlock(block, slot.Index);
            }
        }
    }
}

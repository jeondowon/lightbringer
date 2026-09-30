using System.Collections.Generic;
using Lightbringer.Combat;
using Lightbringer.Resources;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lightbringer.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Combatant), typeof(ManaResource))]
    public sealed class HeroAbilities : MonoBehaviour
    {
        [SerializeField] private UnityEngine.Camera aimCamera;
        [SerializeField] private int[] equipment = { 0, 1, 3 };
        [SerializeField] private int[] levels = { 1, 1, 0, 1, 0, 0 };
        private readonly float[] cooldowns = new float[EquipmentCatalog.Slots];
        private readonly HashSet<Combatant> hits = new HashSet<Combatant>();
        private Combatant self;
        private ManaResource mana;
        private float damageMultiplier = 1f;
        private int[] growthRanks = new int[8];
        private bool capturedBaseStats;
        private float baseHealth, baseFoodCapacity, baseFoodProduction, baseManaCapacity, baseManaRecovery, baseAuraRadius, baseAuraBonus;
        public string Feedback { get; private set; } = "";
        public int Equipped(int slot) => slot >= 0 && slot < equipment.Length ? equipment[slot] : -1;
        public float CooldownRemaining(int slot) => cooldowns[slot];
        public void ConfigureCamera(UnityEngine.Camera camera) => aimCamera = camera;
        public void SetDamageMultiplier(float multiplier) => damageMultiplier = Mathf.Max(1f, multiplier);

        private void Awake()
        {
            self = GetComponent<Combatant>();
            mana = GetComponent<ManaResource>();
        }

        private void Start() => ApplyProgression(growthRanks);

        public void ApplyProgression(int[] ranks)
        {
            if (self == null || mana == null) Awake();
            FoodResource food = GetComponent<FoodResource>();
            Lightbringer.Aura.HeroAura aura = GetComponent<Lightbringer.Aura.HeroAura>();
            if (!capturedBaseStats)
            {
                capturedBaseStats = true;
                baseHealth = self.MaximumHealth;
                baseFoodCapacity = food != null ? food.MaximumFood : 100;
                baseFoodProduction = food != null ? food.ProductionPerSecond : 5;
                baseManaCapacity = mana.Maximum; baseManaRecovery = mana.RecoveryPerSecond;
                baseAuraRadius = aura != null ? aura.Radius : 6;
                baseAuraBonus = aura != null ? aura.AttackBonus : 0.25f;
            }
            growthRanks = ranks != null && ranks.Length == 8 ? (int[])ranks.Clone() : new int[8];
            int foodRing = RingLevel(EquipmentKind.FoodRing);
            int manaRing = RingLevel(EquipmentKind.ManaRing);
            int lifeRing = RingLevel(EquipmentKind.VitalityRing);
            food?.Configure(baseFoodCapacity + growthRanks[3] * 10, baseFoodProduction + growthRanks[1] + foodRing);
            mana.Configure(baseManaCapacity + growthRanks[3] * 10 + manaRing * 15, baseManaRecovery + growthRanks[2] * 2 + manaRing * 2);
            self.SetMaximumHealth(baseHealth + growthRanks[7] * 20 + lifeRing * 30);
            aura?.Configure(baseAuraRadius + growthRanks[0] * 0.6f, baseAuraBonus + growthRanks[5] * 0.05f);
            GetComponent<Lightbringer.Units.UnitSummoner>()?.SetCostMultiplier(1f - growthRanks[4] * 0.025f);
            damageMultiplier = 1 + growthRanks[6] * 0.1f;
        }

        private int RingLevel(EquipmentKind kind)
        {
            foreach (int id in equipment) if (id == (int)kind) return levels[id];
            return 0;
        }

        public bool ConfigureLoadout(int[] selected, int[] ownedLevels)
        {
            if (selected == null || selected.Length != EquipmentCatalog.Slots
                || ownedLevels == null || ownedLevels.Length != EquipmentCatalog.Count)
                return false;
            HashSet<int> unique = new HashSet<int>();
            foreach (int id in selected)
                if (id < 0 || id >= EquipmentCatalog.Count || ownedLevels[id] <= 0 || !unique.Add(id))
                    return false;
            equipment = (int[])selected.Clone();
            levels = (int[])ownedLevels.Clone();
            return true;
        }

        private void Update()
        {
            Tick(Time.deltaTime);
            if (!Application.isFocused || Cursor.lockState != CursorLockMode.Locked || Time.timeScale <= 0)
                return;
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) TryCast(0, FindAimedTarget());
            if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame) TryCast(1, FindAimedTarget());
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) TryCast(2, FindAimedTarget());
        }

        public void Tick(float delta)
        {
            if (!isActiveAndEnabled || delta <= 0) return;
            for (int i = 0; i < cooldowns.Length; i++) cooldowns[i] = Mathf.Max(0, cooldowns[i] - delta);
        }

        public bool TryCast(int slot, Combatant target)
        {
            if (self == null || mana == null) Awake();
            if (!isActiveAndEnabled || !self.IsAlive || slot < 0 || slot >= equipment.Length || cooldowns[slot] > 0)
                return false;
            int id = equipment[slot];
            if (!EquipmentCatalog.IsActive(id)) { Feedback = "This slot contains a passive ring."; return false; }
            if (id != (int)EquipmentKind.HealingStaff && !CanAttack(target))
            { Feedback = "Aim at a visible enemy within 15 metres."; return false; }
            if (!mana.TrySpend(EquipmentCatalog.Cost(id))) { Feedback = "Not enough Mana."; return false; }
            cooldowns[slot] = EquipmentCatalog.Cooldown(id);
            float power = (1f + 0.2f * (levels[id] - 1)) * damageMultiplier;
            if (id == (int)EquipmentKind.LightStaff)
                target.TakeDamage(18f * power, self);
            else if (id == (int)EquipmentKind.HealingStaff)
            {
                foreach (Combatant unit in Collect(transform.position, 6f))
                    if (unit.IsAlive && unit.Faction == self.Faction) unit.Heal(25f * power);
            }
            else
            {
                Vector3 centre = target.transform.position;
                foreach (Combatant unit in Collect(centre, 3f))
                    if (CanAttack(unit)) unit.TakeDamage(30f * power, self);
            }
            Feedback = EquipmentCatalog.Names[id];
            return true;
        }

        private IEnumerable<Combatant> Collect(Vector3 position, float radius)
        {
            hits.Clear();
            foreach (Collider collider in Physics.OverlapSphere(position, radius, ~0, QueryTriggerInteraction.Ignore))
            {
                Combatant unit = collider.GetComponentInParent<Combatant>();
                if (unit != null && unit.gameObject.scene == gameObject.scene) hits.Add(unit);
            }
            return hits;
        }

        private bool CanAttack(Combatant target)
        {
            if (target == null || !target.IsAlive || target.Faction == self.Faction || target.gameObject.scene != gameObject.scene)
                return false;
            Vector3 offset = target.GetAimPoint(transform.position) - transform.position;
            return offset.sqrMagnitude <= 225f && (!Physics.Raycast(transform.position, offset.normalized,
                out RaycastHit hit, offset.magnitude, ~0, QueryTriggerInteraction.Ignore)
                || hit.collider.GetComponentInParent<Combatant>() == target);
        }

        private Combatant FindAimedTarget()
        {
            if (self == null) Awake();
            Vector3 direction = aimCamera != null ? aimCamera.transform.forward : transform.forward;
            Combatant best = null;
            float bestDot = 0.82f;
            foreach (Combatant candidate in Collect(transform.position, 15f))
            {
                Vector3 offset = candidate.GetAimPoint(transform.position) - transform.position;
                float dot = Vector3.Dot(Vector3.ProjectOnPlane(offset, Vector3.up).normalized,
                    Vector3.ProjectOnPlane(direction, Vector3.up).normalized);
                if (dot > bestDot && CanAttack(candidate)) { bestDot = dot; best = candidate; }
            }
            return best;
        }
    }
}

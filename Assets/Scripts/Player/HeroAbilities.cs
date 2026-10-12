using System.Collections.Generic;
using Lightbringer.Aura;
using Lightbringer.Combat;
using Lightbringer.Progression;
using Lightbringer.Resources;
using Lightbringer.Units;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lightbringer.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Combatant), typeof(ManaResource))]
    public sealed class HeroAbilities : MonoBehaviour
    {
        public const float LightStaffWindup = 0.15f;
        public const float LightStaffDescent = 0.075f;
        public const float LightStaffImpactDelay = LightStaffWindup + LightStaffDescent;
        // Shockwave and Ice land when the staff reaches its contact pose in the Slam and Thrust clips.
        public const float ShockwaveImpactDelay = 0.46f;
        public const float ShockwaveRadius = 4.5f;
        public const float ShockwaveDamage = 20f;
        public const float ShockwaveStun = 1.5f;
        public const float ShockwavePush = 3f;
        public const float IceImpactDelay = 0.3f;
        public const float IceDamage = 10f;
        public const float IceFreeze = 3f;
        private struct Delayed { public SkillKind Skill; public Combatant Target; public float Power, Remaining; }
        private readonly List<Delayed> delayed = new List<Delayed>();
        private readonly List<(Combatant target, float damage, float remaining)> pendingLightHits =
            new List<(Combatant, float, float)>();
        [SerializeField] private UnityEngine.Camera aimCamera;
        [SerializeField] private int[] equipment = { (int)SkillKind.LightBeam, SkillCatalog.Empty, SkillCatalog.Empty };
        [SerializeField] private int[] levels = { 1, 0, 0, 0, 0 };
        private readonly float[] cooldowns = new float[SkillCatalog.Slots];
        private readonly HashSet<Combatant> hits = new HashSet<Combatant>();
        private readonly PhysicsQueryBuffer query = new PhysicsQueryBuffer();
        private Combatant self;
        private ManaResource mana;
        private float damageMultiplier = 1f;
        private int[] growthRanks = new int[CampaignProgress.GrowthCount];
        private int heroLevel = 1;
        private bool capturedBaseStats;
        private int inputEnabledFrame = -1;
        private float baseHealth, baseFoodCapacity, baseFoodProduction, baseManaCapacity, baseManaRecovery, baseAuraBonus;
        public string Feedback { get; private set; } = "";
        // Raised after a successful cast with the equipment slot; visuals play the cast animation.
        public event System.Action<int> Casted;
        // Raised for offensive spells with the impact point and area radius; visuals draw the strike.
        public event System.Action<int, Vector3, float> SpellLanded;
        public int Equipped(int slot) => slot >= 0 && slot < equipment.Length ? equipment[slot] : -1;
        public float CooldownRemaining(int slot) => cooldowns[slot];
        public void ConfigureCamera(UnityEngine.Camera camera) => aimCamera = camera;

        private void Awake()
        {
            self = GetComponent<Combatant>();
            mana = GetComponent<ManaResource>();
        }

        private void Start() => ApplyProgression(growthRanks, heroLevel);
        private void OnEnable() => inputEnabledFrame = Time.frameCount;

        // Growth card ranks plus the hero level, which alone sets the aura radius.
        public void ApplyProgression(int[] ranks, int level = 1)
        {
            if (self == null || mana == null) Awake();
            FoodResource food = GetComponent<FoodResource>();
            HeroAura aura = GetComponent<HeroAura>();
            UnitSummoner summoner = GetComponent<UnitSummoner>();
            if (!capturedBaseStats)
            {
                capturedBaseStats = true;
                baseHealth = self.MaximumHealth;
                baseFoodCapacity = food != null ? food.MaximumFood : 100;
                baseFoodProduction = food != null ? food.ProductionPerSecond : 5;
                baseManaCapacity = mana.Maximum; baseManaRecovery = mana.RecoveryPerSecond;
                baseAuraBonus = aura != null ? aura.AttackBonus : 0.25f;
            }
            growthRanks = ranks != null && ranks.Length == CampaignProgress.GrowthCount
                ? (int[])ranks.Clone() : new int[CampaignProgress.GrowthCount];
            heroLevel = Mathf.Clamp(level, 1, CampaignProgress.MaxLevel);
            int capacity = Rank(GrowthKind.Capacity) * GrowthCatalog.CapacityPerRank;
            if (food != null) food.Configure(baseFoodCapacity + capacity,
                baseFoodProduction + Rank(GrowthKind.FoodProduction) * GrowthCatalog.FoodProductionPerRank);
            mana.Configure(baseManaCapacity + capacity,
                baseManaRecovery + Rank(GrowthKind.ManaRecovery) * GrowthCatalog.ManaRecoveryPerRank);
            self.SetMaximumHealth(baseHealth + Rank(GrowthKind.HeroVitality) * GrowthCatalog.VitalityPerRank);
            if (aura != null) aura.Configure(GrowthCatalog.AuraRadiusAt(heroLevel),
                baseAuraBonus + Rank(GrowthKind.AuraBuff) * GrowthCatalog.AuraBuffPerRank);
            if (summoner != null) summoner.SetCostMultiplier(SummonCostMultiplier(Rank(GrowthKind.Leadership)));
            damageMultiplier = 1 + Rank(GrowthKind.HeroPower) * GrowthCatalog.HeroPowerPerRank;
        }

        public static float SummonCostMultiplier(int leadershipRank) =>
            1f - Mathf.Clamp(leadershipRank, 0, GrowthCatalog.MaxRank) * GrowthCatalog.LeadershipPerRank;

        private int Rank(GrowthKind kind) => Mathf.Clamp(growthRanks[(int)kind], 0, GrowthCatalog.MaxRankOf(kind));

        // Slots may be empty (SkillCatalog.Empty); every other entry must be a learned skill used once.
        public bool ConfigureLoadout(int[] selected, int[] ownedLevels)
        {
            if (selected == null || selected.Length != SkillCatalog.Slots
                || ownedLevels == null || ownedLevels.Length != SkillCatalog.Count)
                return false;
            HashSet<int> unique = new HashSet<int>();
            foreach (int id in selected)
                if (id != SkillCatalog.Empty && (!SkillCatalog.IsSkill(id) || ownedLevels[id] <= 0 || !unique.Add(id)))
                    return false;
            equipment = (int[])selected.Clone();
            levels = (int[])ownedLevels.Clone();
            return true;
        }

        private void Update()
        {
            Tick(Time.deltaTime);
            if (!Application.isFocused || Cursor.lockState != CursorLockMode.Locked || Time.timeScale <= 0 || Time.frameCount == inputEnabledFrame)
                return;
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) TryCast(0, FindAimedTarget());
            if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame) TryCast(1, FindAimedTarget());
            if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame) TryCast(2, FindAimedTarget());
        }

        public void Tick(float delta)
        {
            if (!isActiveAndEnabled || delta <= 0) return;
            for (int i = 0; i < cooldowns.Length; i++) cooldowns[i] = Mathf.Max(0, cooldowns[i] - delta);
            for (int i = pendingLightHits.Count - 1; i >= 0; i--)
            {
                var hit = pendingLightHits[i];
                hit.remaining -= delta;
                if (hit.remaining > 0)
                {
                    pendingLightHits[i] = hit;
                    continue;
                }
                // Remove before damage: a lethal hit can end the battle and disable this component.
                pendingLightHits.RemoveAt(i);
                if (self != null && self.IsAlive && hit.target != null && hit.target.IsAlive
                    && hit.target.Faction != self.Faction && hit.target.gameObject.scene == gameObject.scene)
                    hit.target.TakeDamage(hit.damage, self);
                if (!isActiveAndEnabled) break;
            }
            for (int i = delayed.Count - 1; i >= 0 && isActiveAndEnabled; i--)
            {
                if (i >= delayed.Count) continue;
                Delayed pending = delayed[i];
                pending.Remaining -= delta;
                if (pending.Remaining > 0f) { delayed[i] = pending; continue; }
                // Remove before resolving: a lethal hit can end the battle and disable this component.
                delayed.RemoveAt(i);
                if (self != null && self.IsAlive) Resolve(pending);
            }
        }

        // The moment a delayed skill lands.
        private void Resolve(Delayed pending)
        {
            int id = (int)pending.Skill;
            if (pending.Skill == SkillKind.Shockwave)
            {
                Vector3 centre = transform.position;
                SpellLanded?.Invoke(id, centre, ShockwaveRadius);
                // Collect reuses one buffer, so copy the enemies before damaging any of them.
                var struck = new List<Combatant>();
                foreach (Combatant unit in Collect(centre, ShockwaveRadius))
                    if (unit != null && unit.IsAlive && !unit.Invulnerable && unit.Faction != self.Faction) struck.Add(unit);
                foreach (Combatant unit in struck)
                {
                    if (unit == null || !unit.IsAlive) continue;
                    StatusEffects.Stun(unit, ShockwaveStun);
                    StatusEffects.Push(unit, centre, ShockwavePush);
                    unit.TakeDamage(ShockwaveDamage * pending.Power, self);
                    if (!isActiveAndEnabled) return;
                }
            }
            else if (pending.Skill == SkillKind.Ice)
            {
                Combatant target = pending.Target;
                if (target == null || !target.IsAlive || target.Faction == self.Faction || target.gameObject.scene != gameObject.scene) return;
                SpellLanded?.Invoke(id, target.GetAimPoint(transform.position), 1.2f);
                StatusEffects.Stun(target, IceFreeze, freeze: true);
                target.TakeDamage(IceDamage * pending.Power, self);
            }
        }

        private void OnDisable()
        {
            pendingLightHits.Clear();
            delayed.Clear();
        }

        public bool TryCast(int slot, Combatant target)
        {
            if (self == null || mana == null) Awake();
            if (!isActiveAndEnabled || !self.IsAlive || slot < 0 || slot >= equipment.Length || cooldowns[slot] > 0)
                return false;
            int id = equipment[slot];
            if (!SkillCatalog.IsSkill(id)) return false;
            if (SkillCatalog.NeedsTarget(id) && !CanAttack(target))
            { Feedback = "15m 이내에 보이는 적을 조준하세요."; return RejectCast(); }
            if (!mana.TrySpend(SkillCatalog.Cost(id))) { Feedback = "마나가 부족합니다."; return RejectCast(); }
            cooldowns[slot] = SkillCatalog.Cooldown(id);
            float power = SkillCatalog.Power(levels[id]) * damageMultiplier;
            if (id == (int)SkillKind.LightBeam)
            {
                pendingLightHits.Add((target, 18f * power, LightStaffImpactDelay));
                SpellLanded?.Invoke(id, target.GetAimPoint(transform.position), 0.6f);
            }
            else if (id == (int)SkillKind.Heal)
            {
                foreach (Combatant unit in Collect(transform.position, 6f))
                    if (unit.IsAlive && unit.Faction == self.Faction) unit.Heal(25f * power);
            }
            else if (id == (int)SkillKind.Shockwave)
                delayed.Add(new Delayed { Skill = SkillKind.Shockwave, Power = power, Remaining = ShockwaveImpactDelay });
            else if (id == (int)SkillKind.Ice)
                delayed.Add(new Delayed { Skill = SkillKind.Ice, Target = target, Power = power, Remaining = IceImpactDelay });
            Feedback = SkillCatalog.Names[id];
            Casted?.Invoke(slot);
            return true;
        }

        private static bool RejectCast()
        {
            Lightbringer.Audio.SoundEffects.UI(Lightbringer.Audio.SoundCue.Denied);
            return false;
        }

        private IEnumerable<Combatant> Collect(Vector3 position, float radius)
        {
            hits.Clear();
            int count = query.Overlap(position, radius);
            for (int i = 0; i < count; i++)
            {
                Combatant unit = query.Items[i].GetComponentInParent<Combatant>();
                if (unit != null && unit.gameObject.scene == gameObject.scene) hits.Add(unit);
            }
            return hits;
        }

        private bool CanAttack(Combatant target)
        {
            if (target == null || !target.IsAlive || target.Invulnerable || target.Faction == self.Faction
                || target.gameObject.scene != gameObject.scene)
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

using System;
using UnityEngine;

namespace Lightbringer.Combat
{
    public enum Faction { Allied, Enemy }

    [DisallowMultipleComponent]
    public sealed class Combatant : MonoBehaviour
    {
        [SerializeField] private Faction faction = Faction.Allied;
        [SerializeField, Min(1f)] private float maximumHealth = 30f;
        [Tooltip("Optional solid box for large objectives. Units otherwise target the transform centre.")]
        [SerializeField] private BoxCollider attackSurface;

        public Faction Faction => faction;
        public float MaximumHealth => maximumHealth;
        public float CurrentHealth { get; private set; }
        public bool IsAlive => isActiveAndEnabled && CurrentHealth > 0f;
        public event Action<Combatant> Died;
        public Combatant LastAttacker { get; private set; }
        public float DamageReduction { get; set; }
        // Greybox faction tint. Styled visuals carry their own palette and turn this off.
        public bool UseTeamTint { get; set; } = true;
        public void SetAttackSurface(BoxCollider surface) => attackSurface = surface;

        public bool Heal(float amount)
        {
            if (!IsAlive || amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount)) return false;
            CurrentHealth = Mathf.Min(maximumHealth, CurrentHealth + amount);
            return true;
        }

        public void SetMaximumHealth(float value)
        {
            float gained = Mathf.Max(0f, value - maximumHealth);
            maximumHealth = Mathf.Max(1, value);
            if (IsAlive) CurrentHealth = Mathf.Min(maximumHealth, CurrentHealth + gained);
        }

        public void Configure(Faction team, float health)
        {
            faction = team;
            maximumHealth = Mathf.Max(1f, health);
            CurrentHealth = maximumHealth;
            RefreshTeamColor();
        }

        public Vector3 GetAimPoint(Vector3 attackerPosition) =>
            attackSurface != null && attackSurface.enabled && !attackSurface.isTrigger
                && attackSurface.gameObject.activeInHierarchy
                ? attackSurface.ClosestPoint(attackerPosition) : transform.position;

        private void Awake() => CurrentHealth = maximumHealth;
        private void OnEnable() => RefreshTeamColor();

        public bool TakeDamage(float amount, Combatant attacker = null)
        {
            if (!IsAlive || amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount))
                return false;
            LastAttacker = attacker;
            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount * (1f - Mathf.Clamp(DamageReduction, 0f, 0.8f)));
            if (CurrentHealth == 0f)
            {
                // Remove the collider/target immediately; Destroy completes at the end of the frame.
                gameObject.SetActive(false);
                Died?.Invoke(this);
                if (Application.isPlaying)
                    Destroy(gameObject);
            }
            return true;
        }

        public void RefreshTeamColor()
        {
            if (!UseTeamTint) return;
            Color color = faction == Faction.Allied
                ? new Color(0.15f, 0.45f, 1f) : new Color(0.9f, 0.15f, 0.1f);
            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            foreach (Renderer visual in GetComponentsInChildren<Renderer>(true))
            {
                visual.GetPropertyBlock(properties);
                properties.SetColor("_BaseColor", color);
                properties.SetColor("_Color", color);
                visual.SetPropertyBlock(properties);
            }
        }

        private void OnValidate() => maximumHealth = Mathf.Max(1f, maximumHealth);
    }
}

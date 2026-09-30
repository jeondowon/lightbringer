using UnityEngine;

namespace Lightbringer.Combat
{
    public enum Faction { Allied, Enemy }

    [DisallowMultipleComponent]
    public sealed class Combatant : MonoBehaviour
    {
        [SerializeField] private Faction faction = Faction.Allied;
        [SerializeField, Min(1f)] private float maximumHealth = 30f;

        public Faction Faction => faction;
        public float CurrentHealth { get; private set; }
        public bool IsAlive => isActiveAndEnabled && CurrentHealth > 0f;

        private void Awake() => CurrentHealth = maximumHealth;
        private void OnEnable() => RefreshTeamColor();

        public bool TakeDamage(float amount)
        {
            if (!IsAlive || amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount))
                return false;
            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            if (CurrentHealth == 0f)
            {
                // Remove the collider/target immediately; Destroy completes at the end of the frame.
                gameObject.SetActive(false);
                if (Application.isPlaying)
                    Destroy(gameObject);
            }
            return true;
        }

        public void RefreshTeamColor()
        {
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

using Lightbringer.Combat;
using Lightbringer.Units;
using UnityEngine;

namespace Lightbringer.Visuals
{
    // Priests heal with soft green light and rising motes; enemy Shamans retain corruption glyphs.
    // Presentation only; UnitSupport owns the heal.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnitSupport))]
    public sealed class SupportVfx : MonoBehaviour
    {
        [SerializeField] private ArtStyleLibrary style;
        [SerializeField] private float feetOffset = -0.8f;
        private UnitSupport support;

        public void Configure(ArtStyleLibrary library, float feet)
        {
            style = library;
            feetOffset = feet;
        }

        private void OnEnable()
        {
            support = GetComponent<UnitSupport>();
            support.Pulsed += OnPulsed;
        }

        private void OnDisable()
        {
            if (support != null) support.Pulsed -= OnPulsed;
        }

        private void OnPulsed()
        {
            if (!Application.isPlaying || style == null) return;
            bool allied = TryGetComponent(out Combatant self) && self.Faction == Faction.Allied;
            Color color = allied ? CombatEffects.HealLight : CombatEffects.Corruption;
            CombatEffects.Heal(style, transform.position + Vector3.up * feetOffset, Mathf.Min(support.Radius, 6f), color, patterned: !allied);
        }
    }
}

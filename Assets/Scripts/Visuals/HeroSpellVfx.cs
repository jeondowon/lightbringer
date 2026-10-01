using System.Collections.Generic;
using Lightbringer.Combat;
using Lightbringer.Player;
using UnityEngine;

namespace Lightbringer.Visuals
{
    // Turns the hero's offensive spells into lightning strikes from above, timed to the staff-raise
    // of the cast animation. Damage timing is unchanged; this is presentation only.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(HeroAbilities))]
    public sealed class HeroSpellVfx : MonoBehaviour
    {
        private const float StrikeDelay = 0.15f;
        private const float AreaStrength = 1.6f;

        [SerializeField] private ArtStyleLibrary style;
        private HeroAbilities abilities;
        private readonly List<(float time, Vector3 point, float radius, float strength)> pending =
            new List<(float, Vector3, float, float)>();

        public void Configure(ArtStyleLibrary library) => style = library;

        private void OnEnable()
        {
            abilities = GetComponent<HeroAbilities>();
            abilities.SpellLanded += OnSpellLanded;
        }

        private void OnDisable()
        {
            if (abilities != null) abilities.SpellLanded -= OnSpellLanded;
            pending.Clear();
        }

        private void OnSpellLanded(int equipment, Vector3 impact, float radius)
        {
            float strength = equipment == (int)EquipmentKind.RuneStaff ? AreaStrength : 1f;
            pending.Add((Time.time + StrikeDelay, GroundBelow(impact), radius, strength));
        }

        private void Update()
        {
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                if (Time.time < pending[i].time) continue;
                LightningStrikeVfx.Spawn(style, pending[i].point, pending[i].radius, pending[i].strength, transform.parent);
                pending.RemoveAt(i);
            }
        }

        // The bolt runs through the target down to the ground under it (units and objectives are skipped).
        public static Vector3 GroundBelow(Vector3 point)
        {
            RaycastHit[] hits = Physics.RaycastAll(point + Vector3.up * 0.2f, Vector3.down, 8f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            Vector3 ground = point + Vector3.down * 0.85f;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.GetComponentInParent<Combatant>() != null || hit.distance >= best) continue;
                best = hit.distance;
                ground = hit.point;
            }
            return ground;
        }
    }
}

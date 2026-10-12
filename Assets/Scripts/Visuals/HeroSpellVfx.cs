using System.Collections.Generic;
using Lightbringer.Combat;
using Lightbringer.Player;
using UnityEngine;

namespace Lightbringer.Visuals
{
    // Turns offensive spells into sky strikes, timed to the staff-raise
    // of the cast animation. Light Staff damage and visuals share the same windup/descent timing.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(HeroAbilities))]
    public sealed class HeroSpellVfx : MonoBehaviour
    {
        private const float StrikeDelay = 0.15f;
        private const float AreaStrength = 1.6f;
        private static readonly Color IceBlue = new Color(0.55f, 0.88f, 1f);

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
            if (equipment == (int)SkillKind.LightBeam)
            {
                // Start the clock at cast time, not in a later Update after a queued delay.
                CelestialStrikeVfx.Spawn(style, GroundBelow(impact), transform.parent, HeroAbilities.LightStaffWindup);
                return;
            }
            if (equipment == (int)SkillKind.Shockwave)
            {
                // The staff has just struck the ground: a golden shock ring, flare and a skirt of dust.
                Vector3 ground = GroundBelow(impact);
                CombatEffects.Burst(style, ground + Vector3.up * 0.3f, CombatEffects.HolyGold, radius * 0.5f);
                CombatEffects.Ring(ground, CombatEffects.HolyGold, radius);
                CombatEffects.DustRing(ground, radius * 0.7f, 24);
                return;
            }
            if (equipment == (int)SkillKind.Ice)
            {
                CombatEffects.SpellImpact(style, impact, IceBlue, radius, true);
                return;
            }
            float strength = radius > 1.5f ? AreaStrength : 1f;
            pending.Add((Time.time + StrikeDelay, GroundBelow(impact), radius, strength));
        }

        private void Update()
        {
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                if (Time.time < pending[i].time) continue;
                LightningStrikeVfx.Spawn(style, pending[i].point, pending[i].radius, pending[i].strength, transform.parent);
                // Shock ring, flare and embers where the bolt meets the ground; a dust skirt for area spells.
                CombatEffects.Burst(style, pending[i].point + Vector3.up * 0.3f, CombatEffects.AlliedLight, pending[i].radius);
                if (pending[i].strength > 1.2f) CombatEffects.DustRing(pending[i].point, Mathf.Max(1f, pending[i].radius * 0.6f), 12);
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

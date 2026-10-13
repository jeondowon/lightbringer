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

        private static readonly Color MeteorFire = new Color(1f, 0.5f, 0.14f);

        [SerializeField] private ArtStyleLibrary style;
        private HeroAbilities abilities;
        private readonly List<(float start, float delay, Vector3 point)> meteors = new List<(float, float, Vector3)>();
        private readonly List<(float time, Vector3 point, float radius, float strength)> pending =
            new List<(float, Vector3, float, float)>();

        public void Configure(ArtStyleLibrary library) => style = library;

        private void OnEnable()
        {
            abilities = GetComponent<HeroAbilities>();
            abilities.SpellLanded += OnSpellLanded;
            abilities.SpellTargeted += OnSpellTargeted;
        }

        private void OnDisable()
        {
            if (abilities != null)
            {
                abilities.SpellLanded -= OnSpellLanded;
                abilities.SpellTargeted -= OnSpellTargeted;
            }
            pending.Clear();
            meteors.Clear();
        }

        // Meteor: a warning mark on the ground now, the fireball falling in over the delay.
        private void OnSpellTargeted(int equipment, Vector3 point, float radius, float delay)
        {
            if (equipment != (int)SkillKind.Meteor) return;
            CombatEffects.Warning(style, point, MeteorFire, radius, delay);
            meteors.Add((Time.time, delay, point));
        }

        private void OnSpellLanded(int equipment, Vector3 impact, float radius)
        {
            if (equipment == (int)SkillKind.Meteor)
            {
                // A wide blast: a core burst, ring after ring out to the edge, a ring of smaller bursts, dust and smoke.
                CombatEffects.Burst(style, impact + Vector3.up * 0.4f, MeteorFire, radius);
                CombatEffects.Burst(style, impact + Vector3.up * 1.2f, Color.white, radius * 0.5f);
                CombatEffects.Ring(impact, MeteorFire, radius);
                CombatEffects.Ring(impact, CombatEffects.HolyGold, radius * 0.6f);
                for (int i = 0; i < 8; i++)
                {
                    Vector3 around = Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward * radius * 0.6f;
                    CombatEffects.Burst(style, impact + around + Vector3.up * 0.3f, MeteorFire, 2.5f);
                }
                CombatEffects.DustRing(impact, radius * 0.7f, 48);
                CombatEffects.Smoke(impact, radius * 0.7f, 28);
                CameraSystem.ThirdPersonCamera.Shake(impact, 1f);
                Lightbringer.Audio.SoundEffects.World(Lightbringer.Audio.SoundCue.MeteorImpact, impact);
                return;
            }
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
                Lightbringer.Audio.SoundEffects.World(Lightbringer.Audio.SoundCue.ShockwaveBoom, ground);
                return;
            }
            if (equipment == (int)SkillKind.Ice)
            {
                CombatEffects.SpellImpact(style, impact, IceBlue, radius, true);
                Lightbringer.Audio.SoundEffects.World(Lightbringer.Audio.SoundCue.IceMagic, impact);
                return;
            }
            float strength = radius > 1.5f ? AreaStrength : 1f;
            pending.Add((Time.time + StrikeDelay, GroundBelow(impact), radius, strength));
        }

        private void Update()
        {
            for (int i = meteors.Count - 1; i >= 0; i--)
            {
                float t = (Time.time - meteors[i].start) / Mathf.Max(0.01f, meteors[i].delay);
                if (t >= 1f) { meteors.RemoveAt(i); continue; }
                // It enters the sky above and to the side of the mark, and speeds up as it falls.
                Vector3 position = meteors[i].point + Vector3.Lerp(new Vector3(-10f, 45f, -6f), Vector3.up * 0.3f, t * t);
                CombatEffects.Fireball(style, position, MeteorFire, 5f);
            }
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

using System.Collections.Generic;
using Lightbringer.Core;
using Lightbringer.Visuals;
using UnityEngine;

namespace Lightbringer.Combat
{
    // Swept collision prevents the fast energy blade skipping victims at low frame rates.
    public sealed class DarkCrescent : MonoBehaviour
    {
        public const float Speed = 14, Range = 35, HitRadius = 2.4f, Damage = 34;
        private PrototypeBattle battle;
        private Combatant owner;
        private ArtStyleLibrary art;
        private Vector3 direction;
        private float travelled, trailTimer, furrowTimer;
        private Light glow;
        private readonly HashSet<Combatant> hit = new HashSet<Combatant>();

        public static DarkCrescent Launch(PrototypeBattle battle, Combatant owner, Vector3 position, Vector3 direction,
            ArtStyleLibrary art = null)
        {
            var item = new GameObject("암흑 초승달 검기");
            item.transform.SetParent(battle.Root.transform, false);
            item.transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction));
            DarkCrescent wave = item.AddComponent<DarkCrescent>();
            wave.battle = battle; wave.owner = owner; wave.direction = direction.normalized; wave.art = art;
            if (Application.isPlaying)
            {
                BossVfx visual = BossVfx.Spawn(item.transform, position, BossVfx.Shape.Crescent, HitRadius, -1);
                // Reverse the blade's presentation only; travel and swept collision still face the target.
                if (visual != null) visual.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                // The blade lights the ground it passes over.
                wave.glow = new GameObject("Crescent Glow").AddComponent<Light>();
                wave.glow.transform.SetParent(item.transform, false);
                wave.glow.transform.localPosition = Vector3.up * 0.6f;
                wave.glow.type = LightType.Point;
                wave.glow.color = Color.Lerp(BossStrikeVfx.Violet, BossStrikeVfx.Crimson, 0.25f);
                wave.glow.range = 8f;
                wave.glow.intensity = 5f;
                wave.glow.shadows = LightShadows.None;
                CombatEffects.Muzzle(art, position + direction.normalized * 1.2f, BossStrikeVfx.Violet);
            }
            return wave;
        }

        private void Update() => Tick(Time.deltaTime);

        public void Tick(float delta)
        {
            if (!isActiveAndEnabled || delta <= 0) return;
            if (battle == null || battle.Objective.HasEnded || owner == null || !owner.IsAlive) { Remove(); return; }
            Vector3 before = transform.position;
            float step = Mathf.Min(Range - travelled, delta * Speed);
            Vector3 after = before + direction * step;
            transform.position = after; travelled += step;
            if (Application.isPlaying) Trail(delta);
            foreach (Combatant target in battle.Root.GetComponentsInChildren<Combatant>())
            {
                if (!target.IsAlive || target.Faction == owner.Faction || hit.Contains(target)) continue;
                if (DistanceToSegment(target.GetAimPoint(after), before, after) > HitRadius) continue;
                hit.Add(target);
                if (Application.isPlaying)
                    CombatEffects.Hit(art, target.GetAimPoint(after), direction, BossStrikeVfx.Violet, 2f, true);
                target.TakeDamage(Damage, owner);
                if (battle.Objective.HasEnded) break;
            }
            if (travelled >= Range || battle.Objective.HasEnded)
            {
                if (Application.isPlaying && travelled >= Range)
                    CombatEffects.Burst(art, transform.position, BossStrikeVfx.Violet, 1.4f);
                Remove();
            }
        }

        // Violet motes shed along the blade's width, and a glow that dims over the last stretch of travel.
        private void Trail(float delta)
        {
            if (glow != null) glow.intensity = 5f * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(Range * 0.7f, Range, travelled)));
            // The blade gouges a smouldering furrow into the ground as it passes.
            furrowTimer -= delta;
            if (furrowTimer <= 0f)
            {
                furrowTimer = 0.11f;
                Vector3 ground = transform.position;
                ground.y = battle.Root.transform.position.y + 0.02f;
                BossStrikeVfx.Spawn(art, battle.Root.transform, ground, direction, BossStrikeVfx.Kind.ChargeTrail, 1.5f);
            }
            trailTimer -= delta;
            if (trailTimer > 0f) return;
            trailTimer = 0.03f;
            Vector3 side = Vector3.Cross(Vector3.up, direction);
            for (int i = 0; i < 3; i++)
            {
                Vector3 point = transform.position + side * UnityEngine.Random.Range(-HitRadius * 0.9f, HitRadius * 0.9f)
                    - direction * UnityEngine.Random.Range(0.1f, 0.6f) + Vector3.up * UnityEngine.Random.Range(0.2f, 0.9f);
                CombatEffects.Trail(art, point, i == 0 ? BossStrikeVfx.Crimson : BossStrikeVfx.Violet);
            }
        }

        public static float DistanceToSegment(Vector3 point, Vector3 a, Vector3 b)
        {
            point.y = a.y = b.y = 0;
            Vector3 line = b - a;
            float t = line.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector3.Dot(point - a, line) / line.sqrMagnitude) : 0;
            return Vector3.Distance(point, a + line * t);
        }

        private void Remove()
        {
            gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
        }
    }
}

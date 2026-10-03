using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lightbringer.Combat
{
    public enum ProjectileKind { None, Arrow, Bolt }

    // A ranged attack in flight (arrow, magic bolt). It homes on its target's aim point and deals the shooter's
    // damage, plus splash around the impact, on arrival, so the hit and its feedback land together. It lives
    // beside the units (not under its shooter), so a shot already loosed still lands if the shooter dies.
    // Visuals hang their meshes and trails under this transform.
    [DisallowMultipleComponent]
    public sealed class Projectile : MonoBehaviour
    {
        private static readonly List<Projectile> active = new List<Projectile>();
        private static readonly PhysicsQueryBuffer splashQuery = new PhysicsQueryBuffer();
        private static readonly HashSet<Combatant> splashTargets = new HashSet<Combatant>();

        private Combatant attacker;
        private Vector3 start, end;
        private float duration, age, arc;
        private float damage, heavyMultiplier, splashRadius;
        private int splashMask;

        public ProjectileKind Kind { get; private set; }
        public Faction Faction { get; private set; }
        public Combatant Target { get; private set; }
        public float Progress => duration > 0f ? Mathf.Clamp01(age / duration) : 1f;
        public bool HasLanded { get; private set; }
        public static int InFlightCount { get { int count = 0; foreach (Projectile shot in active) if (shot != null && !shot.HasLanded) count++; return count; } }
        // Impact point and the struck target (null when it died mid-flight).
        public event Action<Projectile, Vector3, Combatant> Landed;

        public static Projectile Launch(ProjectileKind kind, Combatant shooter, Vector3 origin, Combatant target,
            float speed, float arcRatio, float damage, float heavyMultiplier, float splashRadius, int splashMask)
        {
            GameObject item = new GameObject(kind + " (Projectile)");
            item.transform.SetParent(shooter.transform.parent, false);
            item.transform.position = origin;
            Projectile shot = item.AddComponent<Projectile>();
            shot.Kind = kind;
            shot.attacker = shooter;
            shot.Faction = shooter.Faction;
            shot.Target = target;
            shot.start = origin;
            shot.end = target.GetAimPoint(origin);
            float distance = Vector3.Distance(origin, shot.end);
            shot.duration = Mathf.Clamp(distance / Mathf.Max(1f, speed), 0.08f, 1.5f);
            shot.arc = distance * Mathf.Max(0f, arcRatio);
            shot.damage = damage;
            shot.heavyMultiplier = Mathf.Max(1f, heavyMultiplier);
            shot.splashRadius = Mathf.Max(0f, splashRadius);
            shot.splashMask = splashMask;
            item.transform.rotation = Quaternion.LookRotation(shot.Heading(0f));
            active.Add(shot);
            return shot;
        }

        // Validations run without Play-mode updates; this advances every shot in flight.
        public static void TickAll(float delta)
        {
            // Edit-mode objects skip OnDestroy, so shots removed with their battle can leave stale entries.
            active.RemoveAll(shot => shot == null);
            for (int i = active.Count - 1; i >= 0; i--)
                if (i < active.Count && active[i] != null) active[i].Tick(delta);
        }

        private void OnDestroy() => active.Remove(this);

        private void Update() => Tick(Time.deltaTime);

        public void Tick(float delta)
        {
            if (HasLanded || delta <= 0f) return;
            age += delta;
            // Follow a living target; once it falls, the shot finishes at its last known point.
            if (Target != null && Target.IsAlive) end = Target.GetAimPoint(start);
            float t = Progress;
            Vector3 position = PointAt(t);
            transform.position = position;
            Vector3 heading = Heading(t);
            if (heading.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(heading);
            if (t >= 1f) Land();
        }

        private Vector3 PointAt(float t) => Vector3.Lerp(start, end, t) + Vector3.up * (arc * 4f * t * (1f - t));
        private Vector3 Heading(float t) => (end - start) + Vector3.up * (arc * 4f * (1f - 2f * t));

        private void Land()
        {
            HasLanded = true;
            Combatant source = attacker != null ? attacker : null;
            Combatant struck = Target != null && Target.IsAlive ? Target : null;
            if (struck != null && !struck.TakeDamage(DamageAgainst(struck), source)) struck = null;
            if (splashRadius > 0f)
            {
                splashTargets.Clear();
                int count = splashQuery.Overlap(end, splashRadius, splashMask);
                for (int i = 0; i < count; i++)
                {
                    Combatant other = splashQuery.Items[i].GetComponentInParent<Combatant>();
                    if (other == null || other == Target || !other.IsAlive || other.Invulnerable || other.Faction == Faction
                        || other.gameObject.scene != gameObject.scene || !splashTargets.Add(other))
                        continue;
                    other.TakeDamage(DamageAgainst(other), source);
                }
            }
            Landed?.Invoke(this, end, struck);
            active.Remove(this);
            if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
        }

        private float DamageAgainst(Combatant target) => target.IsHeavy ? damage * heavyMultiplier : damage;
    }
}

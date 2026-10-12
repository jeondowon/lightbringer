using Lightbringer.Units;
using UnityEngine;

namespace Lightbringer.Combat
{
    // Timed control effects on a unit. A stun holds it in place and cancels its swing; a freeze is a stun that also
    // holds its animation; a push slides it away over a short time. Units whose Combatant.StatusImmune is set (the
    // Boss) ignore all of it. The component is added on first use and driven by the battle like the other
    // simulation components (BattleSimulation pauses it).
    // Implemented by visuals that hold a unit's animation while it is frozen.
    public interface IFreezeListener { void SetFrozen(bool frozen); }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Combatant))]
    public sealed class StatusEffects : MonoBehaviour
    {
        public const float PushDuration = 0.25f;

        private float stunRemaining, freezeRemaining;
        private Vector3 pushDirection;
        private float pushRemaining, pushSpeed;
        private bool held, frozen;
        private CharacterController body;

        public bool IsStunned => stunRemaining > 0f;
        public bool IsFrozen => freezeRemaining > 0f;

        // Holds the target in place for `seconds`; `freeze` also marks it frozen for the same time.
        public static bool Stun(Combatant target, float seconds, bool freeze = false)
        {
            if (!CanAffect(target) || seconds <= 0f || float.IsNaN(seconds)) return false;
            StatusEffects effects = Of(target);
            effects.stunRemaining = Mathf.Max(effects.stunRemaining, seconds);
            if (freeze) effects.freezeRemaining = Mathf.Max(effects.freezeRemaining, seconds);
            effects.Refresh();
            return true;
        }

        // Slides the target `distance` metres away from `from`. Units without a CharacterController stand their ground.
        public static bool Push(Combatant target, Vector3 from, float distance)
        {
            if (!CanAffect(target) || distance <= 0f || float.IsNaN(distance)
                || !target.TryGetComponent(out CharacterController controller) || !controller.enabled)
                return false;
            Vector3 away = target.transform.position - from;
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f) away = -target.transform.forward;
            StatusEffects effects = Of(target);
            effects.body = controller;
            effects.pushDirection = away.normalized;
            effects.pushRemaining = distance;
            effects.pushSpeed = distance / PushDuration;
            return true;
        }

        public static bool CanAffect(Combatant target) =>
            target != null && target.IsAlive && !target.StatusImmune && !target.Invulnerable;

        private static StatusEffects Of(Combatant target) =>
            target.TryGetComponent(out StatusEffects effects) ? effects : target.gameObject.AddComponent<StatusEffects>();

        private void Update() => Tick(Time.deltaTime);

        public void Tick(float delta)
        {
            if (delta <= 0f || !isActiveAndEnabled) return;
            if (stunRemaining > 0f || freezeRemaining > 0f)
            {
                stunRemaining = Mathf.Max(0f, stunRemaining - delta);
                freezeRemaining = Mathf.Max(0f, freezeRemaining - delta);
                Refresh();
            }
            if (pushRemaining <= 0f) return;
            float step = Mathf.Min(pushRemaining, pushSpeed * delta);
            pushRemaining -= step;
            if (body != null && body.enabled) body.Move(pushDirection * step);
        }

        // Tells the movement, combat and support scripts whether they are held, and the visuals whether it is frozen.
        private void Refresh()
        {
            bool nowHeld = stunRemaining > 0f, nowFrozen = freezeRemaining > 0f;
            if (nowHeld != held)
            {
                held = nowHeld;
                if (TryGetComponent(out UnitPathFollower follower)) follower.IsHeld = held;
                if (TryGetComponent(out UnitCombat combat)) combat.SetHeld(held);
                if (TryGetComponent(out UnitSupport support)) support.IsHeld = held;
            }
            if (nowFrozen != frozen)
            {
                frozen = nowFrozen;
                foreach (IFreezeListener listener in GetComponentsInChildren<IFreezeListener>(true)) listener.SetFrozen(frozen);
            }
        }
    }
}

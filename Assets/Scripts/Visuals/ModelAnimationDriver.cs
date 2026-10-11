using Lightbringer.Player;
using Lightbringer.Combat;
using Lightbringer.Units;
using UnityEngine;

namespace Lightbringer.Visuals
{
    // Drives an imported model's legacy Animation from gameplay: idle/move by the owner's speed and a
    // one-shot action clip when the hero casts, a unit attacks or a healer pulses. Visual only; the gameplay
    // transform is never moved.
    [DisallowMultipleComponent]
    public sealed class ModelAnimationDriver : MonoBehaviour
    {
        private const string IdleState = "LB Idle", MoveState = "LB Move", ActionState = "LB Action";
        private Animation player;
        private Transform owner;
        private HeroAbilities abilities;
        private UnitCombat combat;
        private UnitSupport support;
        private float moveClipSpeed = 4f;
        private Vector3 lastPosition;
        private float smoothedSpeed;
        private bool hasMove, hasAction, hasIdle;
        // Cached states: indexing the Animation by name every frame is a string lookup per unit.
        private AnimationState idle, move, action;
        private AnimationState baseState;
        private PropStabilizer[] props = new PropStabilizer[0];

        public void Configure(VisualOverride source)
        {
            player = GetComponent<Animation>();
            if (player == null) player = gameObject.AddComponent<Animation>();
            player.playAutomatically = false;
            player.cullingType = AnimationCullingType.BasedOnRenderers;
            hasIdle = AddClip(source.idleClip, IdleState, WrapMode.Loop);
            hasMove = AddClip(source.moveClip, MoveState, WrapMode.Loop);
            hasAction = AddClip(source.actionClip, ActionState, WrapMode.Once);
            moveClipSpeed = Mathf.Max(0.1f, source.moveClipSpeed);
            props = GetComponentsInChildren<PropStabilizer>(true);
            idle = hasIdle ? player[IdleState] : null;
            move = hasMove ? player[MoveState] : null;
            action = hasAction ? player[ActionState] : null;
            if (hasAction)
            {
                action.layer = 1;
                // Long casts are compressed so the hero reads responsive at short cooldowns.
                action.speed = Mathf.Max(1f, source.actionClip.length / 1.2f);
            }
            player.Stop();
            baseState = null;
            if (hasIdle) { player.Play(IdleState); baseState = idle; }
        }

        private bool AddClip(AnimationClip clip, string state, WrapMode wrap)
        {
            if (clip == null) return false;
            if (!clip.legacy)
            {
                Debug.LogWarning($"{clip.name} is not a legacy clip; glTFast imports must use the Legacy animation method.", this);
                return false;
            }
            player.AddClip(clip, state);
            player[state].wrapMode = wrap;
            return true;
        }

        private void OnEnable()
        {
            owner = transform.parent != null ? transform.parent.parent : null;
            if (owner != null) lastPosition = owner.position;
            abilities = GetComponentInParent<HeroAbilities>();
            if (abilities != null) abilities.Casted += OnCasted;
            combat = GetComponentInParent<UnitCombat>();
            if (combat != null) combat.Attacked += OnAttacked;
            support = GetComponentInParent<UnitSupport>();
            if (support != null) support.Pulsed += OnSupported;
        }

        private void OnDisable()
        {
            if (abilities != null) abilities.Casted -= OnCasted;
            abilities = null;
            if (combat != null) combat.Attacked -= OnAttacked;
            combat = null;
            if (support != null) support.Pulsed -= OnSupported;
            support = null;
        }

        private void OnCasted(int slot)
        {
            if (!hasAction) return;
            player.Stop(ActionState);
            player.CrossFade(ActionState, 0.08f);
        }

        private void OnAttacked()
        {
            if (!hasAction || player == null) return;
            action.speed = action.length / Mathf.Max(0.1f, combat.ActionDuration);
            player.Stop(ActionState);
            player.CrossFade(ActionState, 0.05f);
        }

        // Healers play their action clip on each pulse that mended someone.
        private void OnSupported()
        {
            if (!hasAction || player == null) return;
            action.speed = action.length / Mathf.Max(0.1f, support.Interval * 0.9f);
            player.Stop(ActionState);
            player.CrossFade(ActionState, 0.05f);
        }

        private void LateUpdate()
        {
            float delta = Time.deltaTime;
            if (player == null || owner == null || delta <= 0f) return;
            Vector3 step = owner.position - lastPosition;
            step.y = 0f;
            lastPosition = owner.position;
            smoothedSpeed = Mathf.Lerp(smoothedSpeed, step.magnitude / delta, 1f - Mathf.Exp(-delta * 10f));
            bool moving = smoothedSpeed > 0.3f;
            if (props.Length > 0)
            {
                bool casting = hasAction && action != null && action.enabled;
                foreach (PropStabilizer prop in props) if (prop != null) prop.SetCasting(casting);
            }
            AnimationState target = moving && hasMove ? move : hasIdle ? idle : null;
            if (target == move && move != null)
                move.speed = Mathf.Clamp(smoothedSpeed / moveClipSpeed, 0.5f, 1.6f);
            // Cross-fade only on a change (or if the state was stopped); re-issuing it every frame was a
            // per-unit cost with no visible effect.
            if (target != null && (target != baseState || !target.enabled))
            {
                player.CrossFade(target == move ? MoveState : IdleState, target == move ? 0.2f : 0.25f);
                baseState = target;
            }
        }
    }
}

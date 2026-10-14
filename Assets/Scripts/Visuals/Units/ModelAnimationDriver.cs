using System.Collections.Generic;
using Lightbringer.Player;
using Lightbringer.Combat;
using Lightbringer.Units;
using UnityEngine;

namespace Lightbringer.Visuals
{
    // Drives an imported model's legacy Animation from gameplay: idle/move by the owner's speed and a
    // one-shot action clip when the hero casts, a unit attacks or a healer pulses. The hero plays the clip of the
    // skill it cast (VisualOverride.skillMotions) and falls back to the action clip. Visual only; the gameplay
    // transform is never moved.
    [DisallowMultipleComponent]
    public sealed class ModelAnimationDriver : MonoBehaviour, IFreezeListener
    {
        private const string IdleState = "LB Idle", MoveState = "LB Move", ActionState = "LB Action", SkillState = "LB Skill ";
        private struct SkillEntry { public string Name; public AnimationState State; public bool Authored; }

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
        private readonly Dictionary<int, SkillEntry> skills = new Dictionary<int, SkillEntry>();
        // The action state that last started and whether its clip aims the staff itself.
        private AnimationState current;
        private bool currentAuthored;

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
            skills.Clear();
            if (source.skillMotions != null)
                foreach (SkillMotion motion in source.skillMotions)
                {
                    if (motion == null || motion.clip == null) continue;
                    string name = SkillState + (int)motion.skill;
                    if (!AddClip(motion.clip, name, WrapMode.Once)) continue;
                    AnimationState state = player[name];
                    state.layer = 1;
                    state.speed = Mathf.Max(0.1f, motion.speed);
                    skills[(int)motion.skill] = new SkillEntry { Name = name, State = state, Authored = motion.authored };
                }
            player.Stop();
            baseState = null;
            current = null;
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

        // A frozen unit holds its pose: an Animation that is not enabled does not sample.
        public void SetFrozen(bool frozen)
        {
            if (player != null) player.enabled = !frozen;
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
            if (player == null) return;
            int skill = abilities != null ? abilities.Equipped(slot) : SkillCatalog.Empty;
            if (skills.TryGetValue(skill, out SkillEntry entry))
            {
                StopActions();
                player.CrossFade(entry.Name, 0.08f);
                current = entry.State;
                currentAuthored = entry.Authored;
                return;
            }
            if (!hasAction) return;
            StopActions();
            player.CrossFade(ActionState, 0.08f);
            current = action;
            currentAuthored = false;
        }

        // Only one action clip plays at a time; layer 1 states would otherwise blend with each other.
        private void StopActions()
        {
            if (hasAction) player.Stop(ActionState);
            foreach (SkillEntry entry in skills.Values) player.Stop(entry.Name);
        }

        private void OnAttacked()
        {
            PlayAction(combat.ActionDuration);
        }

        public void PlayAction(float duration)
        {
            if (!hasAction || player == null) return;
            action.speed = action.length / Mathf.Max(0.1f, duration);
            player.Stop(ActionState);
            player.CrossFade(ActionState, 0.05f);
            current = action;
            currentAuthored = false;
        }

        // Healers play their action clip on each pulse that mended someone.
        private void OnSupported()
        {
            if (!hasAction || player == null) return;
            action.speed = action.length / Mathf.Max(0.1f, support.Interval * 0.9f);
            player.Stop(ActionState);
            player.CrossFade(ActionState, 0.05f);
            current = action;
            currentAuthored = false;
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
                bool casting = current != null && current.enabled;
                foreach (PropStabilizer prop in props)
                {
                    if (prop == null) continue;
                    prop.SetCasting(casting && !currentAuthored);
                    prop.SetAuthored(casting && currentAuthored);
                }
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

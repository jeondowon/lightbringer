using Lightbringer.CameraSystem;
using Lightbringer.Combat;
using UnityEngine;

namespace Lightbringer.Core
{
    [DisallowMultipleComponent]
    public sealed class StageObjective : MonoBehaviour
    {
        [SerializeField] private Combatant enemyBase;
        [SerializeField] private Transform battlefieldRoot;
        [SerializeField] private ThirdPersonCamera battleCamera;
        [SerializeField] private Combatant hero;

        public Combatant EnemyBase => enemyBase;
        public bool HasWon { get; private set; }
        public bool HasLost { get; private set; }
        public bool HasEnded => HasWon || HasLost;
        public event System.Action<bool> Completed;
        private Combatant subscribedBase;
        private Combatant subscribedHero;
        [Tooltip("Optional allied stronghold; its destruction is a defeat (as is the hero's death).")]
        [SerializeField] private Combatant alliedBase;
        private Combatant subscribedAlliedBase;
        public Combatant AlliedBase => alliedBase;

        public void Configure(Combatant objective, Combatant player, Transform root, ThirdPersonCamera camera, Combatant allied = null)
        {
            OnDisable();
            enemyBase = objective; hero = player; battlefieldRoot = root; battleCamera = camera; alliedBase = allied;
            if (isActiveAndEnabled) OnEnable();
        }

        private void OnEnable()
        {
            subscribedBase = enemyBase;
            if (subscribedBase != null)
                subscribedBase.Died += OnBaseDestroyed;
            subscribedHero = hero;
            if (subscribedHero != null) subscribedHero.Died += OnHeroDefeated;
            subscribedAlliedBase = alliedBase;
            if (subscribedAlliedBase != null) subscribedAlliedBase.Died += OnAlliedBaseLost;
        }

        private void OnDisable()
        {
            if (subscribedBase != null)
                subscribedBase.Died -= OnBaseDestroyed;
            subscribedBase = null;
            if (subscribedHero != null) subscribedHero.Died -= OnHeroDefeated;
            subscribedHero = null;
            if (subscribedAlliedBase != null) subscribedAlliedBase.Died -= OnAlliedBaseLost;
            subscribedAlliedBase = null;
        }

        private void Start()
        {
            if (enemyBase == null || enemyBase.Faction != Faction.Enemy || battlefieldRoot == null)
            {
                Debug.LogError("StageObjective needs an enemy base and a battlefield root.", this);
                enabled = false;
            }
        }

        private void OnBaseDestroyed(Combatant defeated)
        {
            if (HasEnded || !isActiveAndEnabled || defeated != enemyBase
                || defeated.Faction != Faction.Enemy || defeated.CurrentHealth > 0f
                || battlefieldRoot == null)
                return;

            HasWon = true;
            StopBattle();
            Completed?.Invoke(true);
            Debug.Log("Victory! The enemy base has been destroyed.", this);
        }

        private void OnAlliedBaseLost(Combatant defeated)
        {
            if (HasEnded || !isActiveAndEnabled || defeated != alliedBase || battlefieldRoot == null) return;
            HasLost = true;
            StopBattle();
            Completed?.Invoke(false);
            Debug.Log("Defeat. The allied stronghold has fallen.", this);
        }

        private void OnHeroDefeated(Combatant defeated)
        {
            if (HasEnded || !isActiveAndEnabled || defeated != hero || battlefieldRoot == null) return;
            HasLost = true;
            StopBattle();
            Completed?.Invoke(false);
            Debug.Log("Defeat. Return to preparation to retry.", this);
        }

        private void StopBattle()
        {
            // Stop only this battlefield; never change the global time scale.
            foreach (MonoBehaviour behaviour in battlefieldRoot.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (BattleSimulation.IsSimulated(behaviour))
                    behaviour.enabled = false;
            }
            if (battleCamera != null)
                battleCamera.enabled = false;
            if (Application.isPlaying)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
    }
}

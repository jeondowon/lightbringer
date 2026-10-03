using System.Collections.Generic;
using Lightbringer.CameraSystem;
using Lightbringer.Combat;
using Lightbringer.Player;
using Lightbringer.Resources;
using Lightbringer.Units;
using UnityEngine;

namespace Lightbringer.Core
{
    // Local suspension for level-up choices. Preserve previously-disabled components.
    public sealed class BattleSimulation
    {
        private readonly List<MonoBehaviour> suspended = new List<MonoBehaviour>();
        public bool IsPaused { get; private set; }
        public void Pause(Transform root)
        {
            if (IsPaused || root == null) return;
            IsPaused = true;
            suspended.Clear();
            foreach (MonoBehaviour component in root.GetComponentsInChildren<MonoBehaviour>())
            {
                if (component.enabled && IsSimulated(component))
                { suspended.Add(component); component.enabled = false; }
            }
            if (Application.isPlaying) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        }

        // Components that advance the battle: resources, movement, combat, spawning, hero input and camera.
        public static bool IsSimulated(MonoBehaviour component) =>
            component is UnitCombat || component is UnitPathFollower || component is UnitSummoner
            || component is PlayerMovement || component is HeroAbilities || component is ThirdPersonCamera
            || component is FoodResource || component is ManaResource || component is EnemyWaveSpawner
            || component is UnitSupport || component is Projectile;

        public void Resume(bool battleEnded)
        {
            if (!battleEnded)
                foreach (MonoBehaviour component in suspended) if (component != null) component.enabled = true;
            suspended.Clear();
            IsPaused = false;
        }
    }
}

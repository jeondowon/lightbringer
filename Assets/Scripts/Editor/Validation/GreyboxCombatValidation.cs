using Lightbringer.Combat;
using Lightbringer.Units;
using UnityEditor;
using UnityEngine;

namespace Lightbringer.EditorTools
{
    public static partial class GreyboxValidation
    {
        private static void ValidateCombat()
        {
            Vector3 origin = new Vector3(30000f, 0.85f, 30000f);
            Combatant health = CreateCombatUnit(origin, Faction.Allied);
            Check(health.IsAlive && health.CurrentHealth == 30f, "Combat unit starts alive with configured health");
            Check(!health.TakeDamage(-1f) && !health.TakeDamage(0f) && !health.TakeDamage(float.NaN)
                && !health.TakeDamage(float.PositiveInfinity) && health.CurrentHealth == 30f,
                "Invalid damage cannot change health");
            Check(health.TakeDamage(10f) && health.CurrentHealth == 20f, "Damage reduces health exactly");
            Check(health.TakeDamage(100f) && health.CurrentHealth == 0f && !health.gameObject.activeSelf,
                "Lethal damage clamps health to zero and immediately deactivates the unit");
            Check(!health.IsAlive && !health.TakeDamage(1f), "Dead units cannot receive damage again");

            origin += Vector3.right * 30f;
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.position = origin - Vector3.up * 0.85f;
            ground.transform.localScale = Vector3.one * 4f;
            Combatant ally = CreateCombatUnit(origin, Faction.Allied);
            Combatant friend = CreateCombatUnit(origin + Vector3.right * 1.1f, Faction.Allied);
            UnitCombat combat = ally.GetComponent<UnitCombat>();
            int attacks = 0;
            combat.Attacked += () => attacks++;
            UnitPathFollower movement = ally.GetComponent<UnitPathFollower>();
            var path = CreatePath(origin - Vector3.up * 0.85f, new[] { Vector3.forward * 10f });
            movement.TryAssignPath(path);
            Physics.SyncTransforms();
            Invoke(combat, "Tick", 0.1f);
            Check(combat.Target == null && friend.CurrentHealth == 30f, "Nearby allied units are never attacked");

            Combatant enemy = CreateCombatUnit(origin + Vector3.forward * 20f, Faction.Enemy);
            Physics.SyncTransforms();
            Invoke(combat, "Tick", 0.1f);
            Check(combat.Target == null, "Enemies outside the detection radius are ignored");
            enemy.transform.position = origin + Vector3.forward * 6f;
            Physics.SyncTransforms();
            Invoke(combat, "Tick", 0.1f);
            Check(combat.Target == enemy && enemy.CurrentHealth == 30f, "Visible enemy is acquired without out-of-range damage");
            Invoke(movement, "Tick", 0.1f);
            Check(ally.transform.position.z > origin.z + 0.28f, "Acquired enemy is approached using the movement controller");

            enemy.transform.position = ally.transform.position + Vector3.forward * 1.1f;
            Physics.SyncTransforms();
            Invoke(combat, "Tick", 0f);
            Check(enemy.CurrentHealth == 30f, "Paused combat does not deal damage");
            Check(attacks == 0, "Paused and out-of-range combat does not trigger attack animation");
            Invoke(combat, "Tick", 0.1f);
            Check(enemy.CurrentHealth == 20f, "In-range attack deals the configured damage");
            Invoke(combat, "Tick", 0.1f);
            Check(enemy.CurrentHealth == 20f, "Attack cooldown prevents damage every frame");
            Check(attacks == 1, "One hit triggers one attack animation; cooldown does not retrigger it");
            Invoke(combat, "Tick", 0.71f);
            Check(enemy.CurrentHealth == 10f, "Attack resumes after the interval expires");
            Invoke(combat, "Tick", 0.81f);
            Check(!enemy.IsAlive && combat.Target == null, "Killing an enemy clears the combat target immediately");
            float beforeResume = ally.transform.position.z;
            Invoke(movement, "Tick", 0.1f);
            Check(ally.transform.position.z > beforeResume && movement.AssignedPath == path,
                "Victorious soldier resumes forward movement on its original Path");

            origin += Vector3.right * 30f;
            Combatant blockedAlly = CreateCombatUnit(origin, Faction.Allied);
            Combatant blockedEnemy = CreateCombatUnit(origin + Vector3.forward * 1.1f, Faction.Enemy);
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = origin + Vector3.forward * 0.55f;
            wall.transform.localScale = new Vector3(4f, 4f, 0.1f);
            Physics.SyncTransforms();
            UnitCombat blockedCombat = blockedAlly.GetComponent<UnitCombat>();
            Invoke(blockedCombat, "Tick", 1f);
            Check(blockedCombat.Target == null && blockedEnemy.CurrentHealth == 30f,
                "Enemies hidden behind walls cannot be acquired or attacked");
            wall.SetActive(false);
            Physics.SyncTransforms();
            Invoke(blockedCombat, "Tick", 1f);
            Check(blockedCombat.Target == blockedEnemy && blockedEnemy.CurrentHealth == 20f,
                "Enemy can be acquired when the obstruction is removed");
            wall.SetActive(true);
            Physics.SyncTransforms();
            Invoke(blockedCombat, "Tick", 1f);
            Check(blockedEnemy.CurrentHealth == 20f, "An existing target is not damaged through a new obstruction");
            wall.SetActive(false);
            blockedEnemy.transform.position = origin + Vector3.forward * 20f;
            Physics.SyncTransforms();
            Invoke(blockedCombat, "Tick", 0.1f);
            Check(blockedCombat.Target == null, "Targets leaving detection range are released");

            origin += Vector3.right * 30f;
            Combatant victim = CreateCombatUnit(origin, Faction.Allied);
            Combatant attacker = CreateCombatUnit(origin + Vector3.forward * 1.1f, Faction.Enemy, 4f);
            Physics.SyncTransforms();
            Invoke(attacker.GetComponent<UnitCombat>(), "Tick", 0.1f);
            Check(victim.CurrentHealth == 26f, "Enemy units retaliate against allied units");
            victim.gameObject.SetActive(false);
            Invoke(attacker.GetComponent<UnitCombat>(), "Tick", 0.1f);
            Check(attacker.GetComponent<UnitCombat>().Target == null, "Deactivated targets are released safely");

            ValidateFullDuel(origin + Vector3.right * 30f);
        }

        private static void ValidateFullDuel(Vector3 origin)
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.position = origin - Vector3.up * 0.85f;
            ground.transform.localScale = Vector3.one * 4f;
            Combatant ally = CreateCombatUnit(origin, Faction.Allied);
            Combatant enemy = CreateCombatUnit(origin + Vector3.forward * 8f, Faction.Enemy, 4f);
            var path = CreatePath(origin - Vector3.up * 0.85f, new[] { Vector3.forward * 16f });
            UnitPathFollower allyMovement = ally.GetComponent<UnitPathFollower>();
            UnitPathFollower enemyMovement = enemy.GetComponent<UnitPathFollower>();
            UnitCombat allyCombat = ally.GetComponent<UnitCombat>();
            UnitCombat enemyCombat = enemy.GetComponent<UnitCombat>();
            allyMovement.TryAssignPath(path);
            SetFloat(enemyCombat, "attackInterval", 1.2f);
            for (int i = 0; i < 400; i++)
            {
                Physics.SyncTransforms();
                Invoke(allyCombat, "Tick", 0.05f);
                Invoke(enemyCombat, "Tick", 0.05f);
                Invoke(allyMovement, "Tick", 0.05f);
                Invoke(enemyMovement, "Tick", 0.05f);
            }
            Check(ally.IsAlive && ally.CurrentHealth < 30f && !enemy.IsAlive,
                "Simulated duel includes mutual approach, exchanged damage and enemy defeat");
            Check(allyMovement.HasReachedEnd && allyMovement.AssignedPath == path,
                "After a full duel the surviving soldier reaches the original Path objective");
        }

        private static Combatant CreateCombatUnit(Vector3 position, Faction faction, float damage = 10f)
        {
            GameObject unit = new GameObject("Validation " + faction);
            unit.transform.position = position;
            CharacterController controller = unit.AddComponent<CharacterController>();
            controller.height = 1.6f;
            controller.radius = 0.35f;
            controller.skinWidth = 0.03f;
            controller.minMoveDistance = 0f;
            unit.AddComponent<UnitPathFollower>();
            Combatant health = unit.AddComponent<Combatant>();
            SerializedObject serialized = new SerializedObject(health);
            serialized.FindProperty("faction").enumValueIndex = (int)faction;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Invoke(health, "Awake");
            UnitCombat combat = unit.AddComponent<UnitCombat>();
            SetFloat(combat, "attackDamage", damage);
            Invoke(combat, "Awake");
            return health;
        }

        private static void SetFloat(UnityEngine.Object target, string property, float value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(property).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}

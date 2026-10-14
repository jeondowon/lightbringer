using Lightbringer.Combat;
using Lightbringer.Core;
using Lightbringer.Resources;
using Lightbringer.Units;
using UnityEditor;
using UnityEngine;

namespace Lightbringer.EditorTools
{
    public static partial class GreyboxValidation
    {
        private static void ValidateObjective()
        {
            Vector3 origin = new Vector3(5000f, 0f, 5000f);
            GameObject battlefield = new GameObject("Validation Battlefield");
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.position = origin;
            ground.transform.localScale = Vector3.one * 4f;
            ground.transform.SetParent(battlefield.transform);
            GameObject baseObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseObject.transform.position = origin + Vector3.up * 2f;
            baseObject.transform.localScale = Vector3.one * 4f;
            baseObject.transform.SetParent(battlefield.transform);
            Combatant enemyBase = baseObject.AddComponent<Combatant>();
            SerializedObject settings = new SerializedObject(enemyBase);
            settings.FindProperty("faction").enumValueIndex = (int)Faction.Enemy;
            settings.FindProperty("maximumHealth").floatValue = 200f;
            settings.ApplyModifiedPropertiesWithoutUndo();
            SetReference(enemyBase, "attackSurface", baseObject.GetComponent<BoxCollider>());
            Invoke(enemyBase, "Awake");
            Combatant soldier = CreateCombatUnit(origin + new Vector3(0f, 0.85f, -12f), Faction.Allied);
            soldier.transform.SetParent(battlefield.transform);
            UnitCombat combat = soldier.GetComponent<UnitCombat>();
            UnitPathFollower movement = soldier.GetComponent<UnitPathFollower>();
            movement.TryAssignPath(CreatePath(origin, new[] { Vector3.zero }));
            FoodResource food = battlefield.AddComponent<FoodResource>();
            Invoke(food, "Awake");
            Invoke(food, "GenerateFood", 10f);
            UnitSummoner summoner = battlefield.AddComponent<UnitSummoner>();
            FoodResource outsideFood = new GameObject("Validation Other Battlefield").AddComponent<FoodResource>();

            StageObjective objective = battlefield.AddComponent<StageObjective>();
            SetReference(objective, "enemyBase", enemyBase);
            SetReference(objective, "battlefieldRoot", battlefield.transform);
            Invoke(objective, "OnEnable");
            int deaths = 0;
            enemyBase.Died += _ => deaths++;
            float timeScale = Time.timeScale;
            Check(!objective.HasWon && enemyBase.CurrentHealth == 200f,
                "Stage starts with a living 200 HP enemy base and no victory");
            Combatant ordinaryEnemy = CreateCombatUnit(origin + Vector3.right * 15f, Faction.Enemy);
            ordinaryEnemy.TakeDamage(100f);
            Check(!objective.HasWon, "Defeating an ordinary enemy does not win the stage");
            baseObject.SetActive(false);
            Check(!objective.HasWon && deaths == 0, "Hiding the base is not a destruction or victory");
            baseObject.SetActive(true);
            Physics.SyncTransforms();
            Check(Vector3.Distance(enemyBase.GetAimPoint(origin + new Vector3(0f, 1f, -5f)),
                    origin + new Vector3(0f, 1f, -2f)) < 0.01f,
                "Large objective aim point lies on its nearest solid surface");

            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = origin + new Vector3(0f, 2f, -4f);
            wall.transform.localScale = new Vector3(8f, 4f, 0.5f);
            soldier.transform.position = origin + new Vector3(0f, 0.85f, -6f);
            Physics.SyncTransforms();
            Invoke(combat, "Tick", 0.1f);
            Check(combat.Target == null && enemyBase.CurrentHealth == 200f,
                "A wall blocks acquisition of a large enemy base");
            wall.SetActive(false);
            soldier.transform.position = origin + new Vector3(0f, 0.85f, -12f);
            for (int i = 0; i < 650 && !objective.HasWon; i++)
            {
                Physics.SyncTransforms();
                Invoke(combat, "Tick", 0.05f);
                Invoke(movement, "Tick", 0.05f);
            }
            Check(objective.HasWon && enemyBase.CurrentHealth == 0f && deaths == 1,
                "Soldier follows its Path, approaches the solid base and destroys it to win exactly once");
            Check(soldier.transform.position.z < origin.z - 2f && !baseObject.activeSelf,
                "Soldier attacks from outside the large base and destruction removes its collider");
            Check(!combat.enabled && !movement.enabled && !summoner.enabled && !food.enabled,
                "Victory stops battlefield combat, movement, summoning and Food generation");
            Check(!summoner.TrySummon() && !food.TrySpend(1f) && food.CurrentFood == 50f,
                "Victory rejects further summons and resource spending");
            Vector3 stoppedPosition = soldier.transform.position;
            Invoke(movement, "Tick", 1f);
            Check(soldier.transform.position == stoppedPosition, "Soldier stays stopped after victory");
            Check(!enemyBase.TakeDamage(10f) && deaths == 1,
                "Repeated damage to a destroyed base cannot repeat its death event");
            Check(Time.timeScale == timeScale && outsideFood.enabled,
                "Victory leaves global time and unrelated battlefield components unchanged");

            // Removing the objective component must unsubscribe its death listener.
            GameObject detachedObject = new GameObject("Validation Detached Objective");
            StageObjective detached = detachedObject.AddComponent<StageObjective>();
            Combatant otherBase = CreateCombatUnit(origin + Vector3.right * 20f, Faction.Enemy);
            SetReference(detached, "enemyBase", otherBase);
            SetReference(detached, "battlefieldRoot", detachedObject.transform);
            Invoke(detached, "OnEnable");
            Invoke(detached, "OnDisable");
            otherBase.TakeDamage(100f);
            Check(!detached.HasWon, "Unsubscribed objective cannot react to a later base death");
        }
    }
}

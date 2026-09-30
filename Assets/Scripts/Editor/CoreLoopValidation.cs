using Lightbringer.Combat;
using Lightbringer.Units;
using Lightbringer.Resources;
using UnityEngine;

namespace Lightbringer.EditorTools
{
    public static partial class GreyboxValidation
    {
        private static void ValidateSelection()
        {
            Vector3 origin = new Vector3(6000, 0, 6000);
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.position = origin;
            GameObject hero = new GameObject("Selection hero");
            hero.transform.position = origin + Vector3.up;
            FoodResource food = hero.AddComponent<FoodResource>();
            Invoke(food, "Awake");
            Invoke(food, "GenerateFood", 20f);
            GameObject template = new GameObject("Selection template");
            template.SetActive(false);
            CharacterController controller = template.AddComponent<CharacterController>();
            controller.height = 1.6f;
            controller.radius = 0.35f;
            template.AddComponent<UnitPathFollower>();
            template.AddComponent<Combatant>();
            template.AddComponent<UnitCombat>();
            Transform soldiers = new GameObject("Selection soldiers").transform;
            UnitSummoner summoner = hero.AddComponent<UnitSummoner>();
            SetReference(summoner, "food", food);
            SetReference(summoner, "soldierTemplate", controller);
            SetReference(summoner, "soldiersParent", soldiers);
            var first = CreatePath(origin, new[] { Vector3.forward * 4 });
            var second = CreatePath(origin, new[] { Vector3.right * 4 });
            summoner.ConfigureSelection(new[] { first, second }, 2);
            Check(summoner.TrySummon(), "First Path can summon a swordsman");
            Check(summoner.TrySelectPath(1) && summoner.TrySelectUnit(1) && summoner.TrySummon(),
                "Player can switch Path and summon an archer");
            Check(soldiers.GetChild(0).GetComponent<UnitPathFollower>().AssignedPath == first
                && soldiers.GetChild(1).GetComponent<UnitPathFollower>().AssignedPath == second,
                "Path selection affects only newly summoned soldiers");
            Check(food.CurrentFood == 75f && soldiers.GetChild(1).GetComponent<Combatant>().MaximumHealth == 22f,
                "Archer costs 15 Food and has its own stats");
            Check(!summoner.TrySelectPath(-1) && !summoner.TrySelectPath(2) && !summoner.TrySelectUnit(2)
                && summoner.SelectedPath == second && summoner.SelectedUnit == UnitKind.Archer,
                "Invalid Path and locked unit selection are rejected without changing selection");
            Combatant archer = soldiers.GetChild(1).GetComponent<Combatant>();
            Invoke(archer, "Awake");
            soldiers.GetChild(0).position = origin + Vector3.right * 4;
            archer.transform.position = origin + Vector3.up;
            Combatant enemy = CreateCombatUnit(origin + Vector3.up + Vector3.forward * 5, Faction.Enemy);
            Physics.SyncTransforms();
            Invoke(archer.GetComponent<UnitCombat>(), "Tick", 0.1f);
            Check(enemy.CurrentHealth == 23f, "Archer attacks at range while melee stats stay unchanged");
        }
    }
}

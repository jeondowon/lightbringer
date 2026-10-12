using Lightbringer.Combat;
using Lightbringer.Core;
using Lightbringer.Player;
using Lightbringer.Resources;
using UnityEngine;

namespace Lightbringer.EditorTools
{
    public static partial class GreyboxValidation
    {
        private static void ValidateHeroSystems()
        {
            Vector3 origin = new Vector3(7000, 1, 7000);
            GameObject heroObject = new GameObject("Ability hero");
            heroObject.transform.position = origin;
            heroObject.AddComponent<CapsuleCollider>();
            Combatant hero = heroObject.AddComponent<Combatant>();
            hero.Configure(Faction.Allied, 120);
            ManaResource mana = heroObject.AddComponent<ManaResource>();
            Invoke(mana, "Awake");
            FoodResource food = heroObject.AddComponent<FoodResource>();
            HeroAbilities abilities = heroObject.AddComponent<HeroAbilities>();
            abilities.ApplyProgression(new int[8]);
            Invoke(food, "GenerateFood", 1f);
            Check(food.CurrentFood == 6f, "Equipped Food Ring applies its passive production bonus");
            Combatant enemy = CreateCombatUnit(origin + Vector3.forward * 5, Faction.Enemy);
            enemy.Configure(Faction.Enemy, 100);
            Physics.SyncTransforms();
            Check(abilities.TryCast(0, enemy) && enemy.CurrentHealth == 100 && mana.Current == 92,
                "Light Staff spends Mana on cast but waits for the descending light before damage");
            abilities.Tick(HeroAbilities.LightStaffImpactDelay - 0.005f);
            Check(enemy.CurrentHealth == 100, "Light Staff does not damage the enemy before ground impact");
            abilities.Tick(0);
            Check(enemy.CurrentHealth == 100, "Paused Light Staff timing cannot deal early damage");
            abilities.Tick(0.006f);
            Check(enemy.CurrentHealth == 82, "Light Staff deals damage when the light reaches the ground");
            abilities.Tick(0.02f);
            Check(enemy.CurrentHealth == 82, "Light Staff impact deals damage only once");
            Check(!abilities.TryCast(0, enemy) && mana.Current == 92,
                "Hero ability cooldown prevents extra damage and extra Mana spending");
            abilities.Tick(1);
            float manaBefore = mana.Current;
            Check(!abilities.TryCast(0, hero) && !abilities.TryCast(0, null) && mana.Current == manaBefore,
                "Invalid and friendly offensive targets never spend Mana");
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = origin + Vector3.forward * 2;
            wall.transform.localScale = Vector3.one * 2;
            Physics.SyncTransforms();
            Check(!abilities.TryCast(0, enemy) && mana.Current == manaBefore,
                "Hero abilities cannot damage an enemy through a wall");
            wall.SetActive(false);
            hero.TakeDamage(50);
            Physics.SyncTransforms();
            Check(abilities.TryCast(1, null) && hero.CurrentHealth == 95 && mana.Current == 67,
                "Healing Staff spends Mana and heals nearby allied health");
            Check(!hero.Heal(float.NaN) && !hero.Heal(-1) && hero.Heal(100) && hero.CurrentHealth == 120,
                "Healing rejects invalid input and clamps to maximum health");
            Check(!abilities.ConfigureLoadout(new[] { 0, 0, 3 }, new[] { 1, 1, 1, 1, 1, 1 })
                && !abilities.ConfigureLoadout(new[] { 0, 1, 2 }, new[] { 1, 1, 0, 1, 0, 0 }),
                "Equipment enforces three distinct owned items");
            Check(abilities.ConfigureLoadout(new[] { 0, 2, 5 }, new[] { 2, 1, 1, 1, 1, 2 }),
                "Owned staff upgrades and passive equipment can be equipped");
            abilities.ApplyProgression(new int[8]);
            Check(hero.MaximumHealth == 180, "Vitality Ring level increases maximum hero health");
            abilities.ApplyProgression(new[] { 1, 1, 1, 1, 1, 1, 1, 1 });
            Check(hero.MaximumHealth == 200 && food.MaximumFood == 110 && food.ProductionPerSecond == 6
                && mana.Maximum == 110 && mana.RecoveryPerSecond == 2.5f,
                "Permanent resource and hero ranks apply alongside equipment bonuses");
            abilities.ApplyProgression(new[] { 1, 1, 1, 1, 1, 1, 1, 1 });
            Check(hero.MaximumHealth == 200 && food.MaximumFood == 110 && food.ProductionPerSecond == 6,
                "Reapplying saved growth does not stack or overwrite base-stat tuning");
            abilities.ApplyProgression(new int[8]);
            abilities.Tick(10);
            Combatant second = CreateCombatUnit(origin + new Vector3(1, 0, 5), Faction.Enemy);
            second.Configure(Faction.Enemy, 100);
            Physics.SyncTransforms();
            Check(abilities.TryCast(1, enemy) && enemy.CurrentHealth == 52 && second.CurrentHealth == 70,
                "Rune Staff damages multiple nearby enemies once each");
            mana.TrySpend(mana.Current);
            abilities.Tick(10);
            Check(!abilities.TryCast(0, enemy) && enemy.CurrentHealth == 52,
                "Insufficient Mana prevents hero damage");
            mana.Tick(100);
            Check(mana.Current == mana.Maximum && !mana.TrySpend(float.NaN) && !mana.TrySpend(-1),
                "Mana regeneration is capped and invalid costs are rejected");

            Check(abilities.TryCast(0, enemy), "Light Staff can queue another impact after cooldown");
            Invoke(abilities, "OnDisable");
            abilities.Tick(1);
            Check(enemy.CurrentHealth == 52, "Disabling abilities cancels pending Light Staff damage");
            Combatant doomed = CreateCombatUnit(origin + new Vector3(-2, 0, 5), Faction.Enemy);
            doomed.Configure(Faction.Enemy, 100);
            Physics.SyncTransforms();
            Check(abilities.TryCast(0, doomed), "Light Staff accepts a living target before impact");
            doomed.TakeDamage(1000);
            abilities.Tick(1);
            Check(doomed.CurrentHealth == 0 && enemy.CurrentHealth == 52,
                "A target killed before impact receives no second hit or redirected damage");

            GameObject battle = new GameObject("Defeat battle");
            heroObject.transform.SetParent(battle.transform);
            StageObjective objective = battle.AddComponent<StageObjective>();
            SetReference(objective, "hero", hero);
            SetReference(objective, "enemyBase", enemy);
            SetReference(objective, "battlefieldRoot", battle.transform);
            Invoke(objective, "OnEnable");
            int results = 0;
            objective.Completed += _ => results++;
            hero.TakeDamage(1000, enemy);
            Check(objective.HasLost && !objective.HasWon && !abilities.enabled && !mana.enabled && results == 1,
                "Hero death ends the stage as defeat and stops hero abilities and Mana");
            enemy.TakeDamage(1000);
            Check(!objective.HasWon && results == 1 && !hero.Heal(100),
                "A completed defeat cannot turn into victory or revive the dead hero");
        }
    }
}

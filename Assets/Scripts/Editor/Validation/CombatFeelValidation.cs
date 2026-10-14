using Lightbringer.Combat;
using Lightbringer.Core;
using Lightbringer.Units;
using UnityEngine;

namespace Lightbringer.EditorTools
{
    public static partial class GreyboxValidation
    {
        // Hits land on the weapon's contact (melee) or the shot's arrival (ranged), not at the start of the swing.
        private static void ValidateCombatFeel()
        {
            Check(UnitCatalog.Projectile(UnitKind.Archer) == ProjectileKind.Arrow && UnitCatalog.Projectile(UnitKind.Mage) == ProjectileKind.Bolt
                && EnemyCatalog.Projectile(EnemyKind.Archer) == ProjectileKind.Arrow && EnemyCatalog.Projectile(EnemyKind.Shaman) == ProjectileKind.Bolt
                && UnitCatalog.Projectile(UnitKind.Swordsman) == ProjectileKind.None && EnemyCatalog.Projectile(EnemyKind.Brute) == ProjectileKind.None,
                "Archers shoot arrows and casters shoot bolts; melee troops strike directly");
            Check(UnitCatalog.StrikePhase(UnitKind.Swordsman) > 0f && UnitCatalog.StrikePhase(UnitKind.Archer) == 0f
                && EnemyCatalog.StrikePhase(EnemyKind.Brute) > EnemyCatalog.StrikePhase(EnemyKind.Raider),
                "Melee blows land mid-swing, arrows leave at once, and Brutes telegraph slower slams");

            Vector3 origin = new Vector3(8000f, 1f, -8000f);
            Combatant attacker = CreateCombatUnit(origin, Faction.Allied, 10f);
            Combatant target = CreateCombatUnit(origin + Vector3.forward, Faction.Enemy);
            Combatant neighbour = CreateCombatUnit(origin + new Vector3(-1.2f, 0f, 6f), Faction.Enemy);
            Combatant archer = CreateCombatUnit(origin + new Vector3(5f, 0f, 5f), Faction.Allied, 7f);
            GameObject probe = new GameObject("Validation Projectile Probe");
            try
            {
                UnitCombat combat = attacker.GetComponent<UnitCombat>();
                combat.Configure(10f, 1.25f, 1f);
                combat.ConfigureStrike(0.4f);
                target.Configure(Faction.Enemy, 100f);
                neighbour.Configure(Faction.Enemy, 100f);
                int swings = 0, blows = 0, damagedEvents = 0;
                combat.Attacked += () => swings++;
                combat.StrikeLanded += (_, __) => blows++;
                target.Damaged += (_, amount, source) => { if (amount == 10f && source == attacker) damagedEvents++; };
                Physics.SyncTransforms();

                combat.Tick(0.1f);
                Check(swings == 1 && combat.IsStrikePending && target.CurrentHealth == 100f,
                    "A melee swing starts without dealing damage at once");
                combat.Tick(combat.StrikeDelay * 0.5f);
                Check(target.CurrentHealth == 100f, "The blow has not landed halfway through the wind-up");
                combat.Tick(combat.StrikeDelay);
                Check(target.CurrentHealth == 90f && blows == 1 && damagedEvents == 1 && !combat.IsStrikePending,
                    "The blow lands at the contact point of the swing and reports the hit");

                // The target steps out of reach during the next wind-up: the blow whiffs.
                combat.Tick(1f);
                target.transform.position = origin + Vector3.forward * 4f;
                Physics.SyncTransforms();
                combat.Tick(1f);
                Check(target.CurrentHealth == 90f && blows == 1, "A target that slips out of reach mid-swing is missed");

                // Ranged: the arrow flies, deals its damage on arrival, and splash lands around the impact.
                UnitCombat bow = archer.GetComponent<UnitCombat>();
                bow.Configure(7f, 7f, 1f);
                bow.ConfigureStrike(0f, ProjectileKind.Arrow, 20f, 0.07f);
                target.transform.position = origin + Vector3.forward * 5f;
                Physics.SyncTransforms();
                Projectile launched = null;
                bow.ProjectileLaunched += shot => launched = shot;
                bow.Tick(0.1f);
                Check(launched != null && target.CurrentHealth == 90f && launched.Target != null,
                    "An archer looses an arrow at its target instead of hitting instantly");
                Vector3 start = launched.transform.position;
                launched.Tick(0.05f);
                Check(launched != null && launched.transform.position != start && target.CurrentHealth == 90f,
                    "The arrow travels before it lands");
                Projectile.TickAll(2f);
                Check(target.CurrentHealth == 83f && Projectile.InFlightCount == 0, "The arrow deals its damage on arrival");

                bow.ConfigureSplash(2.5f);
                bow.ConfigureStrike(0f, ProjectileKind.Bolt, 15f);
                bow.Tick(1f);
                // A shot already loosed still lands after its shooter falls.
                archer.TakeDamage(1000f);
                Projectile.TickAll(2f);
                Check(target.CurrentHealth == 76f && neighbour.CurrentHealth == 93f,
                    "A bolt lands after its shooter falls and splashes enemies around the impact");
                Check(BattleSimulation.IsSimulated(probe.AddComponent<Projectile>()),
                    "Shots in flight pause with the battle during level-up choices");
            }
            finally
            {
                // Every shot has landed (and destroyed itself) by now; only the probe remains.
                Object.DestroyImmediate(probe);
                if (attacker != null) Object.DestroyImmediate(attacker.gameObject);
                if (target != null) Object.DestroyImmediate(target.gameObject);
                if (neighbour != null) Object.DestroyImmediate(neighbour.gameObject);
                if (archer != null) Object.DestroyImmediate(archer.gameObject);
                Physics.SyncTransforms();
            }
        }
    }
}

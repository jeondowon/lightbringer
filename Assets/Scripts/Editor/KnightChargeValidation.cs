using Lightbringer.Combat;
using Lightbringer.Units;
using UnityEngine;

namespace Lightbringer.EditorTools
{
    public static partial class GreyboxValidation
    {
        private static void ValidateKnightCharge()
        {
            Check(UnitCatalog.MaxHealth(UnitKind.Knight) > UnitCatalog.MaxHealth(UnitKind.Shieldbearer)
                && UnitCatalog.AttackDamage(UnitKind.Knight) > UnitCatalog.AttackDamage(UnitKind.Mage)
                && UnitCatalog.MaxHealth(UnitKind.Knight) < UnitCatalog.MaxHealth(UnitKind.Dragon)
                && UnitCatalog.AttackDamage(UnitKind.Knight) < UnitCatalog.AttackDamage(UnitKind.Dragon),
                "Knight is the toughest, hardest-hitting troop below the Dragon");

            Vector3 origin = new Vector3(-8000f, 1f, 8000f);
            Combatant knight = CreateCombatUnit(origin, Faction.Allied, 10f);
            Combatant target = CreateCombatUnit(origin + Vector3.forward, Faction.Enemy);
            Combatant neighbour = CreateCombatUnit(origin + new Vector3(1f, 0f, 1.6f), Faction.Enemy);
            try
            {
                UnitCombat combat = knight.GetComponent<UnitCombat>();
                combat.ConfigureCharge(UnitCatalog.ChargeDistance, UnitCatalog.ChargeMultiplier, UnitCatalog.ChargeRadius, UnitCatalog.ChargeKnockback);
                target.Configure(Faction.Enemy, 100f);
                neighbour.Configure(Faction.Enemy, 100f);
                int charges = 0;
                combat.ChargeLanded += () => charges++;

                // Ride in from 6 m back: the first Tick only records the start, the second measures the ride.
                knight.transform.position = origin + Vector3.back * 6f;
                Physics.SyncTransforms();
                combat.Tick(0.1f);
                Check(!combat.IsChargeReady && target.CurrentHealth == 100f, "A Knight that has not moved has no charge ready");
                knight.transform.position = origin;
                Physics.SyncTransforms();
                combat.Tick(0.1f);
                Check(target.CurrentHealth == 100f - 10f * UnitCatalog.ChargeMultiplier && charges == 1,
                    "A charged Knight's first strike deals bonus damage");
                Check(neighbour.CurrentHealth == 90f, "The charge impact also strikes an enemy beside the target");
                Check(target.transform.position.z > origin.z + 1f + UnitCatalog.ChargeKnockback * 0.5f,
                    "The charge shoves a regular enemy back");

                // Standing still: the next strike is a normal one.
                target.transform.position = origin + Vector3.forward;
                Physics.SyncTransforms();
                combat.Tick(1f);
                Check(!combat.IsChargeReady && target.CurrentHealth == 75f - 10f && charges == 1,
                    "Without riding again the Knight attacks normally");

                // Heavy enemies take the charge damage but are not shoved.
                target.IsHeavy = true;
                knight.transform.position = origin + Vector3.back * 6f;
                Physics.SyncTransforms();
                combat.Tick(1f);
                knight.transform.position = origin;
                Physics.SyncTransforms();
                Vector3 before = target.transform.position;
                combat.Tick(1f);
                Check(charges == 2 && target.CurrentHealth == 65f - 10f * UnitCatalog.ChargeMultiplier
                    && Vector3.Distance(target.transform.position, before) < 0.01f,
                    "Heavy enemies take charge damage but hold their ground");
            }
            finally
            {
                Object.DestroyImmediate(knight.gameObject);
                Object.DestroyImmediate(target.gameObject);
                Object.DestroyImmediate(neighbour.gameObject);
                Physics.SyncTransforms();
            }
        }
    }
}

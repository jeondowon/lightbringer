using Lightbringer.Aura;
using Lightbringer.Combat;
using UnityEngine;

namespace Lightbringer.EditorTools
{
    public static partial class GreyboxValidation
    {
        private static void ValidateAura()
        {
            Vector3 origin = new Vector3(40000f, 0.85f, 40000f);
            GameObject hero = new GameObject("Validation Aura Hero");
            hero.transform.position = origin;
            HeroAura aura = hero.AddComponent<HeroAura>();
            Combatant ally = CreateCombatUnit(origin + Vector3.right * 2f, Faction.Allied);
            Combatant enemy = CreateCombatUnit(ally.transform.position + Vector3.forward * 1.1f, Faction.Enemy);
            UnitCombat combat = ally.GetComponent<UnitCombat>();
            Physics.SyncTransforms();
            Invoke(aura, "RefreshRecipients");
            Check(combat.BaseDamage == 10f && combat.EffectiveDamage == 12.5f,
                "Allied unit inside the aura gains 25 percent damage without changing base damage");
            Check(enemy.GetComponent<UnitCombat>().EffectiveDamage == 10f,
                "Enemy unit inside the hero aura receives no bonus");
            GameObject extraCollider = new GameObject("Extra Collider");
            extraCollider.transform.SetParent(ally.transform, false);
            extraCollider.AddComponent<SphereCollider>().radius = 0.1f;
            Physics.SyncTransforms();
            for (int i = 0; i < 10; i++)
                Invoke(aura, "RefreshRecipients");
            Check(combat.EffectiveDamage == 12.5f, "Repeated scans and multiple colliders cannot stack the same aura");
            Invoke(combat, "Tick", 0.1f);
            Check(enemy.CurrentHealth == 17.5f, "An actual attack inside the aura deals 12.5 damage");
            hero.transform.position = origin + Vector3.right * 20f;
            Check(combat.EffectiveDamage == 10f, "Moving the hero away immediately restores base damage before the next scan");
            Invoke(combat, "Tick", 0.81f);
            Check(enemy.CurrentHealth == 7.5f, "An actual attack outside the aura deals the original ten damage");
            Invoke(aura, "RefreshRecipients");
            hero.transform.position = origin;
            Invoke(aura, "RefreshRecipients");
            Check(combat.EffectiveDamage == 12.5f, "Re-entering the aura restores exactly one bonus");

            ally.transform.position = origin + Vector3.right * 6.1f;
            Check(combat.EffectiveDamage == 10f, "Unit outside the radius is not buffed despite collider overlap");
            ally.transform.position = origin + Vector3.right * 6f;
            Check(combat.EffectiveDamage == 12.5f, "A unit centre exactly on the radius is included");
            ally.transform.position = origin + Vector3.up * 7f;
            Check(combat.EffectiveDamage == 10f, "Aura uses 3D distance rather than an unlimited vertical cylinder");
            ally.transform.position = origin + Vector3.right * 2f;
            SetFloat(aura, "radius", 1f);
            Check(combat.EffectiveDamage == 10f, "Shrinking the radius removes the bonus immediately");
            SetFloat(aura, "radius", 6f);
            SetFloat(aura, "attackBonus", 0.5f);
            Check(combat.EffectiveDamage == 15f, "Inspector bonus changes affect damage without accumulating stats");
            SetFloat(aura, "attackBonus", 0.25f);

            GameObject secondHero = new GameObject("Validation Second Aura");
            secondHero.transform.position = origin;
            HeroAura second = secondHero.AddComponent<HeroAura>();
            SetFloat(second, "attackBonus", 0.5f);
            Physics.SyncTransforms();
            Invoke(second, "RefreshRecipients");
            Check(combat.EffectiveDamage == 15f, "Overlapping aura sources use the strongest bonus, not multiplication");
            second.enabled = false;
            Invoke(second, "OnDisable");
            Check(combat.EffectiveDamage == 12.5f, "Disabling one aura preserves the other active source");
            aura.enabled = false;
            Invoke(aura, "OnDisable");
            Check(combat.EffectiveDamage == 10f, "Disabling the hero aura removes its bonus");
            aura.enabled = true;
            Invoke(aura, "RefreshRecipients");
            Check(combat.EffectiveDamage == 12.5f, "Re-enabling the hero aura reapplies its bonus");
            SetFloat(aura, "radius", 0f);
            Invoke(aura, "RefreshRecipients");
            Check(combat.EffectiveDamage == 10f, "Zero radius affects no units");
            SetFloat(aura, "radius", 6f);
            Invoke(aura, "RefreshRecipients");

            AuraRangeVisual visual = hero.AddComponent<AuraRangeVisual>();
            SetReference(visual, "ringShader", Shader.Find("Universal Render Pipeline/Unlit"));
            Invoke(visual, "OnEnable");
            try
            {
                LineRenderer ring = hero.GetComponentInChildren<LineRenderer>();
                Check(ring != null && ring.loop && ring.positionCount == 96 && ring.sharedMaterial != null,
                    "Aura creates a closed runtime ring with its referenced URP shader");
                Check(Mathf.Abs(ring.GetPosition(0).x - hero.transform.position.x - 6f) < 0.01f,
                    "Visible ring radius matches the gameplay radius");
                SetFloat(aura, "radius", 8f);
                hero.transform.position += Vector3.forward * 2f;
                Invoke(visual, "LateUpdate");
                Check(Mathf.Abs(ring.GetPosition(0).x - hero.transform.position.x - 8f) < 0.01f
                    && Mathf.Abs(ring.GetPosition(0).z - hero.transform.position.z) < 0.01f,
                    "Visible ring follows the hero and live radius changes");
                aura.enabled = false;
                Invoke(visual, "LateUpdate");
                Check(!ring.enabled, "Disabling gameplay aura hides the ring");
            }
            finally
            {
                Invoke(visual, "OnDisable");
            }
            Check(hero.GetComponentInChildren<LineRenderer>(true) == null,
                "Disabling aura visualization removes the runtime ring");
            UnityEngine.Object.DestroyImmediate(hero);
            Check(combat.EffectiveDamage == 10f, "Destroying the aura source leaves no permanent damage bonus");
        }
    }
}

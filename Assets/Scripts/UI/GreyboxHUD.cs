using Lightbringer.Core;
using Lightbringer.Resources;
using Lightbringer.Units;
using Lightbringer.Combat;
using Lightbringer.Player;
using UnityEngine;

namespace Lightbringer.UI
{
    [DisallowMultipleComponent]
    public sealed class GreyboxHUD : MonoBehaviour
    {
        [SerializeField] private StageObjective objective;
        [SerializeField] private FoodResource food;
        [SerializeField] private UnitSummoner summoner;
        [SerializeField] private Combatant hero;
        [SerializeField] private ManaResource mana;
        [SerializeField] private HeroAbilities abilities;

        private void OnGUI()
        {
            if (objective == null)
                return;

            GUILayout.BeginArea(new Rect(16f, 16f, 390f, 310f), GUI.skin.box);
            GUILayout.Label("LIGHTBRINGER | GREYBOX");
            if (hero != null) GUILayout.Label($"Hero: {hero.CurrentHealth:F0} / {hero.MaximumHealth:F0} HP");
            if (mana != null) GUILayout.Label($"Mana: {mana.Current:F0} / {mana.Maximum:F0}");
            if (food != null)
                GUILayout.Label($"Food: {food.CurrentFood:F0} / {food.MaximumFood:F0}");
            if (objective.HasWon)
                GUILayout.Label("Enemy base: destroyed");
            else if (objective.EnemyBase != null)
                GUILayout.Label($"Enemy base: {objective.EnemyBase.CurrentHealth:F0} / {objective.EnemyBase.MaximumHealth:F0} HP");
            GUILayout.Label("WASD: Move   Mouse: Look   F: Summon");
            GUILayout.Label("Support your soldiers with the hero aura.");
            if (summoner != null)
            {
                GUILayout.Label($"Path: {summoner.SelectedPath?.name} (1 / 2 / 3)");
                GUILayout.Label($"Tab: {UnitCatalog.Names[(int)summoner.SelectedUnit]} | Food {summoner.SelectedCost:F0}");
            }
            if (abilities != null)
            {
                string[] keys = { "LMB", "Q", "E" };
                for (int i = 0; i < EquipmentCatalog.Slots; i++)
                    GUILayout.Label($"{keys[i]}: {EquipmentCatalog.Names[abilities.Equipped(i)]} ({abilities.CooldownRemaining(i):F1}s)");
                GUILayout.Label(abilities.Feedback);
            }
            GUILayout.EndArea();

            if (!objective.HasEnded)
                return;
            float width = Mathf.Min(380f, Screen.width - 16f);
            GUILayout.BeginArea(new Rect((Screen.width - width) * 0.5f,
                (Screen.height - 120f) * 0.5f, width, 120f), GUI.skin.box);
            GUILayout.Label(objective.HasWon ? "VICTORY" : "DEFEAT");
            GUILayout.Label(objective.HasWon ? "Your soldiers destroyed the enemy base." : "The hero has fallen.");
            GUILayout.Label("Stop Play mode, then Play again to retry.");
            GUILayout.EndArea();
        }
    }
}

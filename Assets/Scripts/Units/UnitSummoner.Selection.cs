using Lightbringer.Pathing;
using UnityEngine;

namespace Lightbringer.Units
{
    public sealed partial class UnitSummoner
    {
        [SerializeField] private WaypointPath[] availablePaths = new WaypointPath[0];
        [SerializeField, Range(1, UnitCatalog.Count)] private int unlockedUnitCount = 2;
        private UnitKind selectedUnit;
        private float costMultiplier = 1f;
        public WaypointPath SelectedPath => selectedPath;
        public UnitKind SelectedUnit => selectedUnit;
        public int UnlockedUnitCount => unlockedUnitCount;
        public int PathCount => availablePaths.Length;
        public float SelectedCost => CostOf(selectedUnit);
        public float CostOf(UnitKind kind) => (kind == UnitKind.Swordsman ? foodCost : UnitCatalog.Cost(kind)) * costMultiplier;
        public void SetCostMultiplier(float multiplier) => costMultiplier = Mathf.Clamp(multiplier, 0.5f, 1f);
        public event System.Action<Lightbringer.Combat.Combatant> Summoned;
        public void Configure(Lightbringer.Resources.FoodResource resource, CharacterController template,
            Transform parent, WaypointPath[] paths, int unlocked)
        {
            food = resource; soldierTemplate = template; soldiersParent = parent;
            ConfigureSelection(paths, unlocked);
        }

        public bool TrySelectPath(int index)
        {
            if (!isActiveAndEnabled || index < 0 || index >= availablePaths.Length
                || availablePaths[index] == null || !availablePaths[index].IsValid)
                return false;
            selectedPath = availablePaths[index];
            return true;
        }

        // Select a troop by its number key and deploy it immediately (one unit per press).
        public bool TrySummonKind(int index)
        {
            if (!isActiveAndEnabled || index < 0 || index >= UnitCatalog.Count) return false;
            if (index >= unlockedUnitCount)
            {
                LastFeedback = UnitCatalog.Names[index] + " is not unlocked yet.";
                return false;
            }
            return TrySelectUnit(index) && TrySummon();
        }

        // Tab: assign later deployments to the next Path. Existing units keep their Path.
        public bool TryCyclePath()
        {
            if (availablePaths.Length <= 1) return false;
            int current = System.Array.IndexOf(availablePaths, selectedPath);
            for (int step = 1; step <= availablePaths.Length; step++)
                if (TrySelectPath((current + step) % availablePaths.Length)) return true;
            return false;
        }

        public bool TrySelectUnit(int index)
        {
            if (!isActiveAndEnabled || index < 0 || index >= unlockedUnitCount || index >= UnitCatalog.Count)
                return false;
            selectedUnit = (UnitKind)index;
            return true;
        }

        public void ConfigureSelection(WaypointPath[] paths, int unlocked)
        {
            availablePaths = paths ?? new WaypointPath[0];
            unlockedUnitCount = Mathf.Clamp(unlocked, 1, UnitCatalog.Count);
            if ((int)selectedUnit >= unlockedUnitCount)
                selectedUnit = UnitKind.Swordsman;
            if (availablePaths.Length > 0)
                selectedPath = availablePaths[0];
        }
    }
}

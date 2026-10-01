using UnityEngine;

namespace Lightbringer.Resources
{
    [DisallowMultipleComponent]
    public sealed class FoodResource : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float startingFood = 0f;
        [SerializeField, Min(0f)] private float maximumFood = 100f;
        [SerializeField, Min(0f)] private float foodPerSecond = 5f;

        public float CurrentFood { get; private set; }
        public float MaximumFood => maximumFood;
        public float ProductionPerSecond => foodPerSecond;
        // Playtest counters: production lost at the cap signals idle Food.
        public float TotalProduced { get; private set; }
        public float TotalWasted { get; private set; }
        public float TotalSpent { get; private set; }

        public void Configure(float capacity, float production)
        {
            maximumFood = Mathf.Max(0f, capacity);
            foodPerSecond = Mathf.Max(0f, production);
            CurrentFood = Mathf.Min(CurrentFood, maximumFood);
        }

        private void Awake()
        {
            CurrentFood = Mathf.Clamp(startingFood, 0f, maximumFood);
        }

        private void Update()
        {
            GenerateFood(Time.deltaTime);
        }

        private void GenerateFood(float elapsedSeconds)
        {
            float produced = foodPerSecond * elapsedSeconds;
            float next = Mathf.Clamp(CurrentFood + produced, 0f, maximumFood);
            TotalProduced += produced;
            TotalWasted += Mathf.Max(0f, produced - (next - CurrentFood));
            CurrentFood = next;
        }

        public bool CanAfford(float amount)
        {
            return isActiveAndEnabled && !float.IsNaN(amount) && !float.IsInfinity(amount)
                && amount >= 0f && CurrentFood >= amount;
        }

        public bool TrySpend(float amount)
        {
            if (!CanAfford(amount))
                return false;
            CurrentFood -= amount;
            TotalSpent += amount;
            return true;
        }

        private void OnValidate()
        {
            maximumFood = Mathf.Max(0f, maximumFood);
            startingFood = Mathf.Clamp(startingFood, 0f, maximumFood);
            foodPerSecond = Mathf.Max(0f, foodPerSecond);
        }
    }
}

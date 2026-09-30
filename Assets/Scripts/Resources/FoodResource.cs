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
            CurrentFood = Mathf.Clamp(CurrentFood + foodPerSecond * elapsedSeconds, 0f, maximumFood);
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

using UnityEngine;

namespace Lightbringer.Resources
{
    [DisallowMultipleComponent]
    public sealed class ManaResource : MonoBehaviour
    {
        [SerializeField, Min(1)] private float maximum = 100f;
        [SerializeField, Min(0)] private float recovery = 8f;
        public float Current { get; private set; }
        public float Maximum => maximum;
        public float RecoveryPerSecond => recovery;
        private void Awake() => Current = maximum;
        private void Update() => Tick(Time.deltaTime);
        public void Tick(float delta)
        {
            if (isActiveAndEnabled && delta > 0 && !float.IsInfinity(delta))
                Current = Mathf.Min(maximum, Current + recovery * delta);
        }
        public bool TrySpend(float amount)
        {
            if (!isActiveAndEnabled || float.IsNaN(amount) || float.IsInfinity(amount) || amount < 0 || Current < amount)
                return false;
            Current -= amount;
            return true;
        }
        public void Configure(float capacity, float regeneration, bool refill = false)
        {
            maximum = Mathf.Max(1f, capacity);
            recovery = Mathf.Max(0f, regeneration);
            Current = refill ? maximum : Mathf.Min(Current, maximum);
        }
    }
}

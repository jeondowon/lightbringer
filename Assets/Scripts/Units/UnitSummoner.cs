using Lightbringer.Resources;
using Lightbringer.Pathing;
using Lightbringer.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lightbringer.Units
{
    [DisallowMultipleComponent]
    public sealed class UnitSummoner : MonoBehaviour
    {
        [SerializeField] private FoodResource food;
        [Tooltip("Inactive unit-scale soldier template with a centered CharacterController and UnitPathFollower.")]
        [SerializeField] private CharacterController soldierTemplate;
        [SerializeField] private Transform soldiersParent;
        [SerializeField] private WaypointPath selectedPath;
        [SerializeField, Min(0f)] private float foodCost = 10f;
        [SerializeField, Min(1f)] private float spawnDistance = 2.5f;
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField] private LayerMask blockingMask = ~0;
        [SerializeField] private InputAction summonAction =
            new InputAction("Summon Soldier", InputActionType.Button, "<Keyboard>/f");

        private int summonedCount;

        private void OnEnable()
        {
            if (food == null || soldierTemplate == null || soldiersParent == null
                || soldierTemplate.gameObject.activeSelf || !soldierTemplate.enabled
                || soldierTemplate.isTrigger || soldierTemplate.GetComponent<UnitPathFollower>() == null
                || selectedPath == null || !selectedPath.IsValid
                || soldierTemplate.center != Vector3.zero
                || soldierTemplate.transform.lossyScale != Vector3.one
                || soldiersParent.lossyScale != Vector3.one)
            {
                Debug.LogError("UnitSummoner needs Food, a valid Path, a unit-scale Soldiers parent, and an inactive centered CharacterController template with UnitPathFollower. Run Greybox setup.", this);
                enabled = false;
                return;
            }
            summonAction.Enable();
        }

        private void OnDisable() => summonAction.Disable();
        private void OnDestroy() => summonAction.Dispose();

        private void Update()
        {
            if (Application.isFocused && Cursor.lockState == CursorLockMode.Locked
                && Time.timeScale > 0f && summonAction.WasPressedThisFrame())
                TrySummon();
        }

        public bool TrySummon()
        {
            if (!isActiveAndEnabled || food == null || soldierTemplate == null || soldiersParent == null
                || selectedPath == null || !selectedPath.IsValid
                || soldierTemplate.GetComponent<UnitPathFollower>() == null)
                return false;
            if (!food.CanAfford(foodCost))
            {
                Debug.Log($"Not enough Food: {food.CurrentFood:F1} / {foodCost:F1}.", this);
                return false;
            }
            if (!TryFindSpawnPosition(out Vector3 position))
            {
                Debug.Log("No free ground nearby. Move to an open area to summon. Food was not spent.", this);
                return false;
            }

            // Keep the instance inactive until payment succeeds.
            CharacterController soldier = Instantiate(soldierTemplate, position,
                Quaternion.Euler(0f, transform.eulerAngles.y, 0f), soldiersParent);
            if (!soldier.GetComponent<UnitPathFollower>().TryAssignPath(selectedPath) || !food.TrySpend(foodCost))
            {
                Destroy(soldier.gameObject);
                return false;
            }
            soldier.name = $"Greybox Soldier {++summonedCount}";
            soldier.gameObject.SetActive(true);
            // Make consecutive spawns see this collider even before the next physics step.
            Physics.SyncTransforms();
            Debug.Log($"Soldier summoned. Food: {food.CurrentFood:F1} / {food.MaximumFood:F1}.", this);
            return true;
        }

        private bool TryFindSpawnPosition(out Vector3 position)
        {
            Physics.SyncTransforms();
            float radius = soldierTemplate.radius;
            float halfHeight = Mathf.Max(soldierTemplate.height * 0.5f, radius);
            float segment = halfHeight - radius;
            // Search around the hero, starting in front; the instance receives its Path before activation.
            for (int i = 0; i < 12; i++)
            {
                Vector3 offset = Quaternion.Euler(0f, i * 30f, 0f) * transform.forward * spawnDistance;
                Vector3 origin = transform.position + offset + Vector3.up * 3f;
                if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 8f,
                    groundMask, QueryTriggerInteraction.Ignore) || Vector3.Dot(hit.normal, Vector3.up) < 0.95f)
                    continue;
                // Never stack soldiers on top of another unit or the hero.
                if (hit.collider.GetComponentInParent<UnitSummoner>() != null
                    || hit.collider.GetComponentInParent<Combatant>() != null
                    || hit.collider.transform.IsChildOf(soldiersParent))
                    continue;
                Vector3 center = hit.point + Vector3.up * (halfHeight + 0.05f);
                if (Physics.CheckCapsule(center + Vector3.up * segment, center - Vector3.up * segment,
                    radius, blockingMask, QueryTriggerInteraction.Ignore))
                    continue;
                position = center;
                return true;
            }
            position = default;
            return false;
        }

        private void OnValidate()
        {
            foodCost = Mathf.Max(0f, foodCost);
            spawnDistance = Mathf.Max(1f, spawnDistance);
        }
    }
}

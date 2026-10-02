using Lightbringer.Resources;
using Lightbringer.Pathing;
using Lightbringer.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lightbringer.Units
{
    [DisallowMultipleComponent]
    public sealed partial class UnitSummoner : MonoBehaviour
    {
        [SerializeField] private FoodResource food;
        [Tooltip("Inactive unit-scale soldier template with a centered CharacterController and UnitPathFollower.")]
        [SerializeField] private CharacterController soldierTemplate;
        [SerializeField] private Transform soldiersParent;
        [SerializeField] private WaypointPath selectedPath;
        [SerializeField, Min(0f)] private float foodCost = 10f;
        [SerializeField, Min(1f)] private float spawnDistance = 2.5f;
        [Tooltip("Where new troops appear (the allied base gate). Empty = around the summoner.")]
        [SerializeField] private Transform spawnAnchor;
        public void ConfigureSpawnAnchor(Transform anchor) => spawnAnchor = anchor;
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField] private LayerMask blockingMask = ~0;
        [SerializeField] private InputAction summonAction =
            new InputAction("Summon Soldier", InputActionType.Button, "<Keyboard>/f");

        private int summonedCount;
        public string LastFeedback { get; private set; } = "";

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
                Debug.LogError("UnitSummoner needs Food, a valid Path, a unit-scale Soldiers parent, and an inactive centered CharacterController template with UnitPathFollower.", this);
                enabled = false;
                return;
            }
            summonAction.Enable();
        }

        private void OnDisable() => summonAction.Disable();
        private void OnDestroy() => summonAction.Dispose();

        private void Update()
        {
            if (Application.isFocused && Cursor.lockState == CursorLockMode.Locked && Time.timeScale > 0f
                && Keyboard.current != null)
            {
                // 1-8 (or numpad 1-8) deploy that troop directly; Tab moves new deployments to the next Path.
                for (int i = 0; i < UnitCatalog.Count; i++)
                    if (Keyboard.current[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame
                        || Keyboard.current[(Key)((int)Key.Numpad1 + i)].wasPressedThisFrame)
                        TrySummonKind(i);
                if (Keyboard.current.tabKey.wasPressedThisFrame) TryCyclePath();
            }
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
            float cost = SelectedCost;
            if (!food.CanAfford(cost))
            {
                LastFeedback = $"Not enough Food: {food.CurrentFood:F1} / {cost:F1}.";
                return false;
            }
            if (!TryFindSpawnPosition(out Vector3 position))
            {
                LastFeedback = "No free ground nearby. Move to an open area.";
                return false;
            }

            // Keep the instance inactive until payment succeeds.
            CharacterController soldier = Instantiate(soldierTemplate, position,
                Quaternion.Euler(0f, SpawnOrigin.eulerAngles.y, 0f), soldiersParent);
            if (!soldier.GetComponent<UnitPathFollower>().TryAssignPath(selectedPath) || !food.TrySpend(cost))
            {
                Destroy(soldier.gameObject);
                return false;
            }
            if (selectedUnit != UnitKind.Swordsman)
            {
                soldier.GetComponent<Combatant>()?.Configure(Faction.Allied, UnitCatalog.MaxHealth(selectedUnit));
                soldier.GetComponent<UnitCombat>()?.Configure(UnitCatalog.AttackDamage(selectedUnit), UnitCatalog.Range(selectedUnit), 1f);
                soldier.GetComponent<UnitPathFollower>().ConfigureSpeed(UnitCatalog.Speed(selectedUnit));
                Transform visual = soldier.transform.Find("Visual");
                if (visual != null) visual.localScale = UnitCatalog.VisualScale(selectedUnit);
                if (selectedUnit == UnitKind.Shieldbearer) soldier.GetComponent<Combatant>().DamageReduction = 0.35f;
                if (selectedUnit == UnitKind.Priest)
                {
                    soldier.GetComponent<UnitCombat>().enabled = false;
                    soldier.gameObject.AddComponent<UnitSupport>();
                }
                if (selectedUnit == UnitKind.Mage || selectedUnit == UnitKind.Dragon)
                    soldier.GetComponent<UnitCombat>().ConfigureSplash(selectedUnit == UnitKind.Dragon ? 4f : 2.5f);
                if (selectedUnit == UnitKind.Dragon) soldier.GetComponent<UnitPathFollower>().ConfigureFlight(3.5f);
            }
            if (soldier.TryGetComponent(out Lightbringer.Visuals.UnitAppearance appearance))
                appearance.Apply(Lightbringer.Visuals.ArtStyleLibrary.ForUnit(selectedUnit));
            soldier.name = $"{UnitCatalog.Names[(int)selectedUnit]} {++summonedCount}";
            soldier.GetComponent<UnitPathFollower>().ConfigureCrowdAvoidance(true);
            soldier.gameObject.SetActive(true);
            Summoned?.Invoke(soldier.GetComponent<Combatant>());
            // Make consecutive spawns see this collider even before the next physics step.
            Physics.SyncTransforms();
            LastFeedback = UnitCatalog.Names[(int)selectedUnit] + " deployed.";
            return true;
        }

        private Transform SpawnOrigin => spawnAnchor != null ? spawnAnchor : transform;

        private bool TryFindSpawnPosition(out Vector3 position)
        {
            Physics.SyncTransforms();
            float radius = soldierTemplate.radius;
            float halfHeight = Mathf.Max(soldierTemplate.height * 0.5f, radius);
            float segment = halfHeight - radius;
            // Search around the spawn point (base gate, or the hero), starting in front and widening in rings
            // so a crowded gate still finds room; the instance receives its Path before activation.
            Transform spawn = SpawnOrigin;
            for (int i = 0; i < 36; i++)
            {
                float ring = 1f + (i / 12) * 0.6f;
                Vector3 offset = Quaternion.Euler(0f, (i % 12) * 30f + (i / 12) * 15f, 0f) * spawn.forward * (spawnDistance * ring);
                Vector3 origin = spawn.position + offset + Vector3.up * 3f;
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

using UnityEngine;
using UnityEngine.InputSystem;

namespace Lightbringer.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMovement : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private InputActionAsset inputActions;

        [Header("Movement")]
        [SerializeField, Min(0f)] private float moveSpeed = 6f;
        [Tooltip("Maximum turn speed in degrees per second.")]
        [SerializeField, Min(0f)] private float rotationSpeed = 540f;
        [SerializeField] private float gravity = -25f;

        private CharacterController controller;
        private InputAction moveAction;
        private float verticalSpeed;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
        }

        private void OnEnable()
        {
            verticalSpeed = 0f;
            InputAction source = inputActions != null
                ? inputActions.FindAction("Player/Move", false) : null;
            if (source == null || cameraTransform == null)
            {
                Debug.LogError("PlayerMovement requires a camera and an input asset with Player/Move.", this);
                enabled = false;
                return;
            }

            // Own only this action's lifetime; never disable the shared project asset.
            moveAction = source.Clone();
            moveAction.bindingMask = InputBinding.MaskByGroup("Keyboard&Mouse");
            moveAction.Enable();
        }

        private void OnDisable()
        {
            moveAction?.Dispose();
            moveAction = null;
        }

        private void Update()
        {
            if (!controller.enabled || cameraTransform == null)
                return;

            Vector2 input = Application.isFocused
                ? Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f) : Vector2.zero;
            Vector3 forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 direction = forward * input.y + right * input.x;

            if (direction.sqrMagnitude > 0.0001f)
            {
                Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, rotation, rotationSpeed * Time.deltaTime);
            }

            if (controller.isGrounded && verticalSpeed < 0f)
                verticalSpeed = -2f;

            verticalSpeed += gravity * Time.deltaTime;
            CollisionFlags collisions = controller.Move(
                (direction * moveSpeed + Vector3.up * verticalSpeed) * Time.deltaTime);
            if ((collisions & CollisionFlags.Below) != 0 && verticalSpeed < 0f)
                verticalSpeed = -2f;
            if ((collisions & CollisionFlags.Above) != 0 && verticalSpeed > 0f)
                verticalSpeed = 0f;
        }

        private void OnValidate()
        {
            moveSpeed = Mathf.Max(0f, moveSpeed);
            rotationSpeed = Mathf.Max(0f, rotationSpeed);
            gravity = Mathf.Min(-0.01f, gravity);
        }
    }
}

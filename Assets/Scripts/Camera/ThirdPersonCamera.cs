using UnityEngine;
using UnityEngine.InputSystem;

namespace Lightbringer.CameraSystem
{
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnityEngine.Camera))]
    public sealed class ThirdPersonCamera : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform target;
        [SerializeField] private InputActionAsset inputActions;

        [Header("Framing")]
        [SerializeField, Min(0.1f)] private float distance = 9f;
        [Tooltip("Orbit pivot height above the target's transform position.")]
        [SerializeField] private float height = 1f;
        [SerializeField] private float initialPitch = 30f;
        [SerializeField] private float minPitch = 10f;
        [SerializeField] private float maxPitch = 70f;

        [Header("Mouse")]
        [Tooltip("Degrees per pixel of mouse movement.")]
        [SerializeField, Min(0f)] private float sensitivity = 0.15f;

        private InputAction lookAction;
        private float yaw;
        private float pitch;
        private CursorLockMode previousLockMode;
        private bool previousCursorVisible;
        private bool ownsCursor;

        private void OnEnable()
        {
            InputAction source = inputActions != null
                ? inputActions.FindAction("Player/Look", false) : null;
            if (target == null || source == null)
            {
                Debug.LogError("ThirdPersonCamera requires a target and an input asset with Player/Look.", this);
                enabled = false;
                return;
            }

            lookAction = source.Clone();
            lookAction.bindingMask = InputBinding.MaskByGroup("Keyboard&Mouse");
            lookAction.Enable();
            yaw = transform.eulerAngles.y;
            pitch = Mathf.Clamp(initialPitch, minPitch, maxPitch);
            previousLockMode = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            ownsCursor = true;
            SetCursorLocked(true);
            UpdatePose();
        }

        private void OnDisable()
        {
            lookAction?.Dispose();
            lookAction = null;
            if (ownsCursor)
            {
                Cursor.lockState = previousLockMode;
                Cursor.visible = previousCursorVisible;
                ownsCursor = false;
            }
        }

        private void Update()
        {
            if (!Application.isFocused || target == null)
                return;

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                SetCursorLocked(false);
                return;
            }

            if (Cursor.lockState != CursorLockMode.Locked)
            {
                if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                    SetCursorLocked(true);
                return; // Ignore the delta from the click that recaptures the cursor.
            }

            // Mouse delta already represents this frame's displacement: no deltaTime here.
            Vector2 delta = lookAction.ReadValue<Vector2>() * sensitivity;
            yaw = Mathf.Repeat(yaw + delta.x, 360f);
            pitch = Mathf.Clamp(pitch - delta.y, minPitch, maxPitch);
            // Movement reads this frame's camera orientation before the late follow pass.
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        private void LateUpdate()
        {
            if (target != null)
                UpdatePose();
        }

        private void UpdatePose()
        {
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 pivot = target.position + Vector3.up * height;
            transform.SetPositionAndRotation(pivot - rotation * Vector3.forward * distance, rotation);
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused && ownsCursor)
                SetCursorLocked(false);
        }

        private static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private void OnValidate()
        {
            distance = Mathf.Max(0.1f, distance);
            sensitivity = Mathf.Max(0f, sensitivity);
            minPitch = Mathf.Clamp(minPitch, -80f, 80f);
            maxPitch = Mathf.Clamp(maxPitch, minPitch, 80f);
            initialPitch = Mathf.Clamp(initialPitch, minPitch, maxPitch);
        }
    }
}

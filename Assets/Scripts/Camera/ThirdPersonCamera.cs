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
        [Tooltip("Negative values look up toward the horizon and sky.")]
        [SerializeField] private float minPitch = -35f;
        [SerializeField] private float maxPitch = 75f;

        [Header("Mouse")]
        [Tooltip("Degrees per pixel of mouse movement.")]
        [SerializeField, Min(0f)] private float sensitivity = 0.15f;

        private InputAction lookAction;
        private float yaw;
        private float pitch;
        private CursorLockMode previousLockMode;
        private bool previousCursorVisible;
        private bool ownsCursor;
        private bool hasPose;
        private RaycastHit[] obstructionHits = new RaycastHit[16];
        public void Configure(Transform followTarget, InputActionAsset actions)
        { target = followTarget; inputActions = actions; }

        // Impact shake: heavy blows near the hero nudge the view. Trauma decays quickly and is squared,
        // so small hits barely register while charges and slams read as weight. Never moves the orbit pivot.
        private static readonly System.Collections.Generic.List<ThirdPersonCamera> active =
            new System.Collections.Generic.List<ThirdPersonCamera>();
        private const float ShakeRange = 28f;
        private float trauma;
        private float shakeSeed;
        public float Trauma => trauma;

        // `strength` 0-1; falls off with distance from the followed hero so far-away fronts stay calm.
        public static void Shake(Vector3 source, float strength)
        {
            foreach (ThirdPersonCamera camera in active)
            {
                if (camera.target == null) continue;
                float falloff = 1f - Mathf.Clamp01(Vector3.Distance(source, camera.target.position) / ShakeRange);
                // Strongest wins: a breath or slam striking many units at once does not stack into a quake.
                camera.trauma = Mathf.Max(camera.trauma, Mathf.Clamp01(strength * falloff * falloff));
            }
        }

        private void ApplyShake()
        {
            float delta = Time.deltaTime;
            if (trauma <= 0f || delta <= 0f) return;
            trauma = Mathf.Max(0f, trauma - delta * 2.2f);
            float amount = trauma * trauma;
            shakeSeed += delta * 28f;
            float yawJitter = (Mathf.PerlinNoise(shakeSeed, 0.1f) - 0.5f) * 2f;
            float pitchJitter = (Mathf.PerlinNoise(0.7f, shakeSeed) - 0.5f) * 2f;
            transform.rotation *= Quaternion.Euler(pitchJitter * amount * 2.5f, yawJitter * amount * 2.5f, yawJitter * amount * 1.5f);
            transform.position += transform.up * (pitchJitter * amount * 0.18f);
        }

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
            if (!hasPose)
            {
                yaw = transform.eulerAngles.y;
                pitch = Mathf.Clamp(initialPitch, minPitch, maxPitch);
                hasPose = true;
            }
            previousLockMode = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            ownsCursor = true;
            SetCursorLocked(true);
            UpdatePose();
            if (!active.Contains(this)) active.Add(this);
        }

        private void OnDisable()
        {
            active.Remove(this);
            trauma = 0f;
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
            if (Mouse.current != null)
                distance = Mathf.Clamp(distance - Mathf.Clamp(Mouse.current.scroll.ReadValue().y, -1f, 1f), 5f, 20f);
            yaw = Mathf.Repeat(yaw + delta.x, 360f);
            pitch = Mathf.Clamp(pitch - delta.y, minPitch, maxPitch);
            // Movement reads this frame's camera orientation before the late follow pass.
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        private void LateUpdate()
        {
            if (target == null) return;
            UpdatePose();
            ApplyShake();
        }

        private void UpdatePose()
        {
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 pivot = target.position + Vector3.up * height;
            Vector3 direction = -(rotation * Vector3.forward);
            int count;
            while (true)
            {
                count = Physics.SphereCastNonAlloc(pivot, 0.25f, direction, obstructionHits, distance, ~0, QueryTriggerInteraction.Ignore);
                if (count < obstructionHits.Length) break;
                obstructionHits = new RaycastHit[obstructionHits.Length * 2];
            }
            float visibleDistance = distance;
            for (int i = 0; i < count; i++)
            {
                Collider collider = obstructionHits[i].collider;
                if (collider.transform.IsChildOf(target) || collider.GetComponentInParent<Lightbringer.Units.UnitPathFollower>() != null)
                    continue;
                visibleDistance = Mathf.Min(visibleDistance, Mathf.Max(0.3f, obstructionHits[i].distance - 0.1f));
            }
            transform.SetPositionAndRotation(pivot + direction * visibleDistance, rotation);
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

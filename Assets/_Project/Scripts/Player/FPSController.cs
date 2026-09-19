using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace LateSubmission.Player
{
    /// <summary>
    /// Self-contained first-person controller supporting WASD movement,
    /// mouse look with vertical pitch clamping, sprint, gravity, and cursor locking.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FPSController : MonoBehaviour
    {
        [Header("Movement Speeds")]
        [SerializeField] private float _walkSpeed = 3.5f;
        [SerializeField] private float _sprintSpeed = 6.0f;
        [SerializeField] private float _gravity = -18.0f;

        [Header("Look Sensitivity")]
        [SerializeField] private float _mouseSensitivity = 2.0f;
        [SerializeField] private float _upDownLookLimit = 85.0f;

        [Header("References")]
        [SerializeField] private Camera _playerCamera;

        private CharacterController _characterController;
        private Vector3 _moveDirection;
        private float _verticalRotation = 0f;
        private float _verticalVelocity = 0f;
        private bool _isCursorLocked = true;

        public bool IsSprinting { get; private set; }
        public float CurrentSpeed { get; private set; }

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            if (_playerCamera == null)
            {
                _playerCamera = GetComponentInChildren<Camera>();
            }
        }

        private void Start()
        {
            SetCursorLock(true);

            if (!string.IsNullOrEmpty(LateSubmission.Core.SceneTransitionManager.TargetSpawnPointName))
            {
                var targetSpawn = GameObject.Find(LateSubmission.Core.SceneTransitionManager.TargetSpawnPointName);
                if (targetSpawn != null)
                {
                    Teleport(targetSpawn.transform.position, targetSpawn.transform.rotation);
                }
                LateSubmission.Core.SceneTransitionManager.TargetSpawnPointName = null;
            }
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            if (_characterController != null)
            {
                _characterController.enabled = false;
            }
            transform.position = position;
            transform.rotation = Quaternion.Euler(0, rotation.eulerAngles.y, 0);
            _pitch = 0f;
            if (_playerCamera != null)
            {
                _playerCamera.transform.localRotation = Quaternion.identity;
            }
            if (_characterController != null)
            {
                _characterController.enabled = true;
            }
        }

        private void Update()
        {
            HandleCursorToggle();

            if (_isCursorLocked)
            {
                HandleLook();
                HandleMovement();
            }
        }

        private void HandleLook()
        {
            float mouseX = 0f;
            float mouseY = 0f;

#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                Vector2 delta = Mouse.current.delta.ReadValue() * 0.1f * _mouseSensitivity;
                mouseX = delta.x;
                mouseY = delta.y;
            }
#endif
            if (Mathf.Approximately(mouseX, 0f) && Mathf.Approximately(mouseY, 0f))
            {
                mouseX = Input.GetAxis("Mouse X") * _mouseSensitivity;
                mouseY = Input.GetAxis("Mouse Y") * _mouseSensitivity;
            }

            // Horizontal rotation (Player body yaw)
            transform.Rotate(Vector3.up * mouseX);

            // Vertical rotation (Camera pitch clamped)
            _verticalRotation -= mouseY;
            _verticalRotation = Mathf.Clamp(_verticalRotation, -_upDownLookLimit, _upDownLookLimit);

            if (_playerCamera != null)
            {
                _playerCamera.transform.localRotation = Quaternion.Euler(_verticalRotation, 0f, 0f);
            }
        }

        private void HandleMovement()
        {
            float horizontal = 0f;
            float vertical = 0f;
            bool sprintPressed = false;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (Keyboard.current.wKey.isPressed) vertical += 1f;
                if (Keyboard.current.sKey.isPressed) vertical -= 1f;
                if (Keyboard.current.dKey.isPressed) horizontal += 1f;
                if (Keyboard.current.aKey.isPressed) horizontal -= 1f;
                sprintPressed = Keyboard.current.leftShiftKey.isPressed;
            }
#endif
            if (Mathf.Approximately(horizontal, 0f) && Mathf.Approximately(vertical, 0f))
            {
                horizontal = Input.GetAxisRaw("Horizontal");
                vertical = Input.GetAxisRaw("Vertical");
            }
            if (!sprintPressed)
            {
                sprintPressed = Input.GetKey(KeyCode.LeftShift);
            }

            IsSprinting = sprintPressed && vertical > 0.1f;
            float targetSpeed = IsSprinting ? _sprintSpeed : _walkSpeed;

            Vector3 moveInput = (transform.right * horizontal + transform.forward * vertical).normalized;
            Vector3 movement = moveInput * targetSpeed;

            // Gravity
            if (_characterController.isGrounded)
            {
                if (_verticalVelocity < 0f)
                {
                    _verticalVelocity = -2f;
                }
            }
            else
            {
                _verticalVelocity += _gravity * Time.deltaTime;
            }

            movement.y = _verticalVelocity;
            _characterController.Move(movement * Time.deltaTime);

            Vector3 horizontalVel = new Vector3(_characterController.velocity.x, 0, _characterController.velocity.z);
            CurrentSpeed = horizontalVel.magnitude;
        }

        private void HandleCursorToggle()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                SetCursorLock(false);
            }
            else if (Input.GetMouseButtonDown(0) && !_isCursorLocked)
            {
                SetCursorLock(true);
            }
        }

        public void SetCursorLock(bool locked)
        {
            _isCursorLocked = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}

using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace LateSubmission.Player
{
    /// <summary>
    /// Advanced First-Person Controller tailored for Late Submission.
    /// Streamlined for atmospheric survival horror: jumping and crouching removed to eliminate
    /// ceiling collisions, obstacle sticking, and doorway height clipping glitches.
    /// Character height is strictly tuned to 1.55m (safely lower than Brazilian door clearances).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FPSController : MonoBehaviour
    {
        [Header("Movement Speeds")]
        [SerializeField] private float _walkSpeed = 3.5f;
        [SerializeField] private float _sprintSpeed = 6.0f;
        [SerializeField] private float _gravity = -18.0f;

        [Header("Controller Dimensions (Safe Clearance for Doors)")]
        [SerializeField] private float _characterHeight = 1.55f;
        [SerializeField] private float _characterRadius = 0.28f;
        [SerializeField] private float _characterSkinWidth = 0.03f;
        [SerializeField] private float _characterStepOffset = 0.15f;
        [SerializeField] private float _standingCameraY = 1.45f;

        [Header("Procedural Head Bob")]
        [SerializeField] private bool _enableHeadBob = true;
        [SerializeField] private float _bobFrequency = 10.0f;
        [SerializeField] private float _bobWalkAmplitude = 0.025f;
        [SerializeField] private float _bobSprintAmplitude = 0.045f;

        [Header("Camera Tilt & Lean")]
        [SerializeField] private bool _enableCameraTilt = true;
        [SerializeField] private float _strafeTiltAmount = 1.8f;
        [SerializeField] private float _turnTiltAmount = 0.8f;
        [SerializeField] private float _tiltSmoothness = 8.0f;
        [SerializeField] private float _maxTiltAngle = 3.0f;

        [Header("Dynamic FOV")]
        [SerializeField] private bool _enableDynamicFov = true;
        [SerializeField] private float _normalFov = 60f;
        [SerializeField] private float _sprintFov = 67f;
        [SerializeField] private float _fovTransitionSpeed = 6.0f;

        [Header("Camera Smoothing & Inertia")]
        [SerializeField] private bool _enableCameraSmoothing = true;
        [Range(10f, 40f)] [SerializeField] private float _cameraWeight = 24.0f;

        [Header("Look Sensitivity")]
        [SerializeField] private float _mouseSensitivity = 2.0f;
        [SerializeField] private float _upDownLookLimit = 85.0f;

        [Header("References")]
        [SerializeField] private Camera _playerCamera;

        private CharacterController _characterController;
        private Camera _cameraComponent;
        private float _verticalRotation = 0f;
        private float _verticalVelocity = 0f;
        private bool _isCursorLocked = true;
        private bool _wasGrounded = true;

        private float _bobTimer = 0f;
        private float _lastBobX = 0f;
        private float _lastBobY = 0f;
        private float _currentTilt = 0f;
        private float _smoothMouseX = 0f;
        private float _smoothMouseY = 0f;

        public bool IsSprinting { get; private set; }
        // Crouching and Jump removed to prevent getting stuck; properties kept for backward compatibility
        public bool IsCrouching => false;
        public bool IsGrounded => _characterController != null && _characterController.isGrounded;
        public float CurrentSpeed { get; private set; }

        public event Action OnJumped;
        public event Action<float> OnLanded;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            if (_playerCamera == null)
            {
                _playerCamera = GetComponentInChildren<Camera>();
            }

            if (_characterController != null)
            {
                ApplyDimensions();
            }

            if (_playerCamera != null)
            {
                _cameraComponent = _playerCamera.GetComponent<Camera>();
                _playerCamera.transform.localPosition = new Vector3(0f, _standingCameraY, 0f);
                if (_cameraComponent != null)
                {
                    _normalFov = _cameraComponent.fieldOfView;
                }
            }
        }

        private void ApplyDimensions()
        {
            if (_characterController == null) return;
            _characterController.height = _characterHeight;
            _characterController.radius = _characterRadius;
            _characterController.skinWidth = _characterSkinWidth;
            _characterController.stepOffset = _characterStepOffset;
            _characterController.center = new Vector3(0f, _characterHeight * 0.5f, 0f);
        }

        private void Start()
        {
            SetCursorLock(true);
            ApplyDimensions();

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
            _verticalRotation = 0f;
            _verticalVelocity = 0f;
            _bobTimer = 0f;
            _currentTilt = 0f;
            _lastBobX = 0f;
            _lastBobY = 0f;
            _smoothMouseX = 0f;
            _smoothMouseY = 0f;

            if (_playerCamera != null)
            {
                _playerCamera.transform.localRotation = Quaternion.identity;
                _playerCamera.transform.localPosition = new Vector3(0f, _standingCameraY, 0f);
            }
            if (_characterController != null)
            {
                ApplyDimensions();
                _characterController.enabled = true;
            }
        }

        private void Update()
        {
            var hud = LateSubmission.UI.HUDController.Instance;
            if (hud != null && hud.IsNoteOpen)
            {
                // Pause player motion and look, reset camera effects gently
                if (_playerCamera != null)
                {
                    _playerCamera.transform.localPosition = Vector3.Lerp(
                        _playerCamera.transform.localPosition,
                        new Vector3(0f, _standingCameraY, 0f),
                        Time.deltaTime * 8f
                    );
                    _playerCamera.transform.localRotation = Quaternion.Euler(_verticalRotation, 0f, 0f);
                }
                return;
            }

            HandleCursorToggle();

            if (_isCursorLocked)
            {
                HandleLook();
                HandleMovement();
                HandleCameraEffects();
                HandleDynamicFov();
            }
        }

        private void HandleLook()
        {
            float rawMouseX = 0f;
            float rawMouseY = 0f;

#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                Vector2 delta = Mouse.current.delta.ReadValue() * 0.1f * _mouseSensitivity;
                rawMouseX = delta.x;
                rawMouseY = delta.y;
            }
#endif
            if (Mathf.Approximately(rawMouseX, 0f) && Mathf.Approximately(rawMouseY, 0f))
            {
                try
                {
                    rawMouseX = Input.GetAxis("Mouse X") * _mouseSensitivity;
                    rawMouseY = Input.GetAxis("Mouse Y") * _mouseSensitivity;
                }
                catch {}
            }

            // Smooth camera inertia
            if (_enableCameraSmoothing)
            {
                float smoothFactor = Mathf.Clamp01(Time.deltaTime * _cameraWeight);
                _smoothMouseX = Mathf.Lerp(_smoothMouseX, rawMouseX, smoothFactor);
                _smoothMouseY = Mathf.Lerp(_smoothMouseY, rawMouseY, smoothFactor);
            }
            else
            {
                _smoothMouseX = rawMouseX;
                _smoothMouseY = rawMouseY;
            }

            // Horizontal rotation (Player body yaw)
            transform.Rotate(Vector3.up * _smoothMouseX);

            // Vertical rotation (Camera pitch clamped)
            _verticalRotation -= _smoothMouseY;
            _verticalRotation = Mathf.Clamp(_verticalRotation, -_upDownLookLimit, _upDownLookLimit);

            if (_playerCamera != null)
            {
                _playerCamera.transform.localRotation = Quaternion.Euler(_verticalRotation, 0f, _currentTilt);
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
                try
                {
                    horizontal = Input.GetAxisRaw("Horizontal");
                    vertical = Input.GetAxisRaw("Vertical");
                }
                catch {}
            }
            if (!sprintPressed)
            {
                try
                {
                    sprintPressed = Input.GetKey(KeyCode.LeftShift);
                }
                catch {}
            }

            IsSprinting = sprintPressed && vertical > 0.1f;
            float targetSpeed = IsSprinting ? _sprintSpeed : _walkSpeed;

            Vector3 moveInput = (transform.right * horizontal + transform.forward * vertical).normalized;
            Vector3 movement = moveInput * targetSpeed;

            // Landing detection & Gravity
            bool isGrounded = _characterController.isGrounded;
            if (isGrounded)
            {
                if (!_wasGrounded)
                {
                    float impactSpeed = Mathf.Abs(_verticalVelocity);
                    if (impactSpeed > 2.0f)
                    {
                        OnLanded?.Invoke(impactSpeed);
                    }
                }

                if (_verticalVelocity < 0f)
                {
                    _verticalVelocity = -2f;
                }
            }
            else
            {
                _verticalVelocity += _gravity * Time.deltaTime;
            }

            _wasGrounded = isGrounded;

            movement.y = _verticalVelocity;
            _characterController.Move(movement * Time.deltaTime);

            Vector3 horizontalVel = new Vector3(_characterController.velocity.x, 0, _characterController.velocity.z);
            CurrentSpeed = horizontalVel.magnitude;

            // Camera Tilt update (Strafe + Turn Inertia)
            if (_enableCameraTilt)
            {
                float strafeTilt = -horizontal * _strafeTiltAmount;
                float turnTilt = -_smoothMouseX * _turnTiltAmount;
                float targetTilt = Mathf.Clamp(strafeTilt + turnTilt, -_maxTiltAngle, _maxTiltAngle);
                if (IsSprinting) targetTilt *= 1.25f;

                _currentTilt = Mathf.Lerp(_currentTilt, targetTilt, Time.deltaTime * _tiltSmoothness);
            }
            else
            {
                _currentTilt = 0f;
            }
        }

        private void HandleCameraEffects()
        {
            if (_playerCamera == null) return;

            float bobX = 0f;
            float bobY = 0f;

            if (_enableHeadBob && IsGrounded && CurrentSpeed > 0.15f)
            {
                float currentFreq = _bobFrequency * (IsSprinting ? 1.35f : 1.0f);
                float currentAmp = IsSprinting ? _bobSprintAmplitude : _bobWalkAmplitude;

                _bobTimer += Time.deltaTime * currentFreq;
                bobX = Mathf.Cos(_bobTimer * 0.5f) * (currentAmp * 0.7f);
                bobY = Mathf.Sin(_bobTimer) * currentAmp;

                _lastBobX = bobX;
                _lastBobY = bobY;
            }
            else
            {
                _bobTimer = 0f;
                _lastBobX = Mathf.Lerp(_lastBobX, 0f, Time.deltaTime * 6f);
                _lastBobY = Mathf.Lerp(_lastBobY, 0f, Time.deltaTime * 6f);
                bobX = _lastBobX;
                bobY = _lastBobY;
            }

            _playerCamera.transform.localPosition = new Vector3(bobX, _standingCameraY + bobY, 0f);
        }

        private void HandleDynamicFov()
        {
            if (!_enableDynamicFov || _cameraComponent == null) return;

            float targetFov = IsSprinting ? _sprintFov : _normalFov;
            _cameraComponent.fieldOfView = Mathf.Lerp(_cameraComponent.fieldOfView, targetFov, Time.deltaTime * _fovTransitionSpeed);
        }

        private void HandleCursorToggle()
        {
            bool escapePressed = false;
            bool clickPressed = false;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                escapePressed = true;
            }
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                clickPressed = true;
            }
#endif
            if (!escapePressed)
            {
                try
                {
                    if (Input.GetKeyDown(KeyCode.Escape)) escapePressed = true;
                }
                catch {}
            }
            if (!clickPressed)
            {
                try
                {
                    if (Input.GetMouseButtonDown(0)) clickPressed = true;
                }
                catch {}
            }

            if (escapePressed)
            {
                SetCursorLock(false);
            }
            else if (clickPressed && !_isCursorLocked)
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

using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace LateSubmission.Weapon
{
    /// <summary>
    /// Procedural first-person viewmodel controller.
    /// Delivers smooth weapon sway from mouse delta, rhythmic walking bobbing,
    /// and spring-based recoil kickback without requiring character arm rigs.
    /// </summary>
    public class ViewmodelController : MonoBehaviour
    {
        [Header("Sway Settings")]
        [SerializeField] private float _swayAmount = 0.02f;
        [SerializeField] private float _maxSwayAmount = 0.05f;
        [SerializeField] private float _swaySmooth = 8.0f;

        [Header("Bobbing Settings")]
        [SerializeField] private float _bobSpeed = 9.0f;
        [SerializeField] private float _bobAmountX = 0.012f;
        [SerializeField] private float _bobAmountY = 0.018f;

        [Header("Recoil Impulse")]
        [SerializeField] private float _recoilBack = 0.06f;
        [SerializeField] private float _recoilUp = 0.03f;
        [SerializeField] private float _recoilSnappiness = 16f;
        [SerializeField] private float _recoilReturnSpeed = 10f;

        private Vector3 _initialLocalPos;
        private Quaternion _initialLocalRot;
        private float _bobTimer;
        private Vector3 _targetRecoilPos;
        private Vector3 _currentRecoilPos;
        private CharacterController _characterController;

        private void Awake()
        {
            _initialLocalPos = transform.localPosition;
            _initialLocalRot = transform.localRotation;
        }

        public void SetRestingTransform(Vector3 localPos, Vector3 localScale)
        {
            transform.localPosition = localPos;
            transform.localScale = localScale;
            _initialLocalPos = localPos;
            _initialLocalRot = transform.localRotation;
        }

        private void Start()
        {
            var player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                _characterController = player.GetComponent<CharacterController>();
            }
        }

        private void Update()
        {
            UpdateSway();
            UpdateBobbing();
            UpdateRecoil();
        }

        private void UpdateSway()
        {
            float mouseX = 0f;
            float mouseY = 0f;

#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                Vector2 delta = Mouse.current.delta.ReadValue();
                mouseX = delta.x * 0.05f;
                mouseY = delta.y * 0.05f;
            }
#endif
            try
            {
                if (Mathf.Approximately(mouseX, 0f) && Mathf.Approximately(mouseY, 0f))
                {
                    mouseX = Input.GetAxis("Mouse X");
                    mouseY = Input.GetAxis("Mouse Y");
                }
            }
            catch {}

            float moveX = Mathf.Clamp(-mouseX * _swayAmount, -_maxSwayAmount, _maxSwayAmount);
            float moveY = Mathf.Clamp(-mouseY * _swayAmount, -_maxSwayAmount, _maxSwayAmount);

            Vector3 finalSwayPos = new Vector3(moveX, moveY, 0f);
            transform.localPosition = Vector3.Lerp(
                transform.localPosition,
                _initialLocalPos + finalSwayPos + _currentRecoilPos,
                Time.deltaTime * _swaySmooth
            );
        }

        private void UpdateBobbing()
        {
            if (_characterController == null) return;

            Vector3 horizontalVel = new Vector3(_characterController.velocity.x, 0, _characterController.velocity.z);
            float speed = horizontalVel.magnitude;

            if (speed > 0.2f && _characterController.isGrounded)
            {
                _bobTimer += Time.deltaTime * _bobSpeed * Mathf.Clamp(speed / 3.0f, 0.8f, 1.6f);
                float waveX = Mathf.Cos(_bobTimer) * _bobAmountX;
                float waveY = Mathf.Sin(_bobTimer * 2.0f) * _bobAmountY;

                transform.localPosition += new Vector3(waveX, waveY, 0f);
            }
            else
            {
                _bobTimer = 0f;
            }
        }

        private void UpdateRecoil()
        {
            _targetRecoilPos = Vector3.Lerp(_targetRecoilPos, Vector3.zero, Time.deltaTime * _recoilReturnSpeed);
            _currentRecoilPos = Vector3.Lerp(_currentRecoilPos, _targetRecoilPos, Time.deltaTime * _recoilSnappiness);
        }

        public void AddRecoil()
        {
            _targetRecoilPos += new Vector3(0f, _recoilUp, -_recoilBack);
        }
    }
}

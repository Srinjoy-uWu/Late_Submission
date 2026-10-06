using UnityEngine;
using LateSubmission.Attention;
using LateSubmission.Inventory;
using LateSubmission.Weapon;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace LateSubmission.Player
{
    /// <summary>
    /// Controls handheld flashlight toggle and emits continuous AttentionEvent(Flashlight).
    /// Defers spotlight rendering to HeldItemController when present so only one spotlight is active,
    /// and strictly requires ItemType.Flashlight in InventoryManager before allowing use.
    /// </summary>
    public class FlashlightController : MonoBehaviour
    {
        [Header("Light")]
        [SerializeField] private Light _spotlight;
        [SerializeField] private bool _startsOn = false;

        [Header("Attention Emission")]
        [SerializeField] private float _lightNoiseRate = 3.0f;
        [SerializeField] private float _emissionInterval = 1.0f;

        [Header("Audio")]
        [SerializeField] private AudioClip _toggleSfx;

        private bool _isOn;
        private float _timer;

        public bool IsOn => HeldItemController.Instance != null ? HeldItemController.Instance.IsFlashlightOn : _isOn;

        private bool HasCollectedFlashlight =>
            InventoryManager.Instance != null && InventoryManager.Instance.HasItemType(ItemType.Flashlight);

        private void Awake()
        {
            if (_spotlight == null) _spotlight = GetComponentInChildren<Light>();
            _isOn = false;
            _startsOn = false;
            if (_spotlight != null) _spotlight.enabled = false;
        }

        private void Start()
        {
            _isOn = false;
            if (_spotlight != null) _spotlight.enabled = false;
        }

        private void Update()
        {
            // If HeldItemController is present on the player camera, it handles [F] input and the spotlight beam.
            if (HeldItemController.Instance != null)
            {
                if (_spotlight != null && _spotlight != HeldItemController.Instance.FlashlightSpotlight && _spotlight.enabled)
                {
                    _spotlight.enabled = false;
                }

                _isOn = HeldItemController.Instance.IsFlashlightOn;
                if (_isOn)
                {
                    EmitLightAttention();
                }
                return;
            }

            bool togglePressed = false;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
            {
                togglePressed = true;
            }
#endif
            if (!togglePressed)
            {
                try
                {
                    if (Input.GetKeyDown(KeyCode.F))
                    {
                        togglePressed = true;
                    }
                }
                catch {}
            }

            if (togglePressed)
            {
                if (!HasCollectedFlashlight)
                {
                    return;
                }
                Toggle();
            }

            if (_isOn)
            {
                EmitLightAttention();
            }
        }

        private void EmitLightAttention()
        {
            _timer += Time.deltaTime;
            if (_timer >= _emissionInterval)
            {
                _timer = 0f;
                AttentionManager.Emit(transform.position, _lightNoiseRate, AttentionType.Flashlight);
            }
        }

        public void Toggle()
        {
            if (!HasCollectedFlashlight && !_isOn)
            {
                return;
            }

            _isOn = !_isOn;
            if (_spotlight != null && HeldItemController.Instance == null)
            {
                _spotlight.enabled = _isOn;
            }

            if (_toggleSfx != null && HeldItemController.Instance == null)
            {
                AudioSource.PlayClipAtPoint(_toggleSfx, transform.position);
            }
        }
    }
}

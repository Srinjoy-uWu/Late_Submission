using UnityEngine;
using LateSubmission.Attention;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace LateSubmission.Player
{
    /// <summary>
    /// Controls handheld flashlight toggle and emits continuous AttentionEvent(Flashlight).
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

        public bool IsOn => _isOn;

        private void Awake()
        {
            if (_spotlight == null) _spotlight = GetComponentInChildren<Light>();
            _isOn = _startsOn;
            if (_spotlight != null) _spotlight.enabled = _isOn;
        }

        private void Update()
        {
            bool togglePressed = false;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
            {
                togglePressed = true;
            }
#endif
            if (Input.GetKeyDown(KeyCode.F))
            {
                togglePressed = true;
            }

            if (togglePressed)
            {
                Toggle();
            }

            if (_isOn)
            {
                _timer += Time.deltaTime;
                if (_timer >= _emissionInterval)
                {
                    _timer = 0f;
                    AttentionManager.Emit(transform.position, _lightNoiseRate, AttentionType.Flashlight);
                }
            }
        }

        public void Toggle()
        {
            _isOn = !_isOn;
            if (_spotlight != null) _spotlight.enabled = _isOn;

            if (_toggleSfx != null)
            {
                AudioSource.PlayClipAtPoint(_toggleSfx, transform.position);
            }
        }
    }
}

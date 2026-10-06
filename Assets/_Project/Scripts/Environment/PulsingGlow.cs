using UnityEngine;

namespace LateSubmission.Environment
{
    /// <summary>
    /// Creates a breathing/pulsing emission and light effect for atmospheric beacons like the
    /// red glowing crafting station in Mechatronics Lab.
    /// </summary>
    public class PulsingGlow : MonoBehaviour
    {
        [Header("Light Pulse")]
        [SerializeField] private Light _pointLight;
        [SerializeField] private float _minIntensity = 0.8f;
        [SerializeField] private float _maxIntensity = 2.4f;
        [SerializeField] private float _pulseSpeed = 2.2f;

        [Header("Renderer Pulse")]
        [SerializeField] private Renderer _targetRenderer;
        [SerializeField] private Color _glowColor = new Color(1.0f, 0.1f, 0.1f, 1.0f);
        [SerializeField] private float _minEmission = 1.0f;
        [SerializeField] private float _maxEmission = 3.5f;

        private MaterialPropertyBlock _propBlock;
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private void Awake()
        {
            if (_pointLight == null) _pointLight = GetComponent<Light>();
            if (_targetRenderer == null) _targetRenderer = GetComponent<Renderer>();
            _propBlock = new MaterialPropertyBlock();
        }

        private void Update()
        {
            float wave = (Mathf.Sin(Time.time * _pulseSpeed) + 1.0f) * 0.5f;

            if (_pointLight != null)
            {
                _pointLight.intensity = Mathf.Lerp(_minIntensity, _maxIntensity, wave);
            }

            if (_targetRenderer != null)
            {
                float emissionFactor = Mathf.Lerp(_minEmission, _maxEmission, wave);
                Color finalColor = _glowColor * Mathf.LinearToGammaSpace(emissionFactor);

                _targetRenderer.GetPropertyBlock(_propBlock);
                _propBlock.SetColor(EmissionColorId, finalColor);
                _targetRenderer.SetPropertyBlock(_propBlock);
            }
        }
    }
}

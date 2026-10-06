using System.Collections;
using UnityEngine;
using LateSubmission.Attention;

namespace LateSubmission.Environment
{
    public enum FlickerPattern
    {
        Erratic,         // Chaotic fluorescent ballast failure (frequent dips, micro-spikes)
        PulseBlink,      // Rhythmic emergency beacon / heartbeat blink
        BlackoutStrobe,  // Mostly pitch black with rare, violent flash bursts
        SubtleHum,       // Subtle low-frequency voltage fluctuation
        Off              // Dead fixture, burned out completely
    }

    /// <summary>
    /// Controls dynamic flickering, blinking, and strobing on lights and their emissive materials.
    /// Delivers atmospheric horror lighting for corridors, abandoned rooms, and hazard zones.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class FlickeringLight : MonoBehaviour
    {
        [Header("Target Components")]
        [SerializeField] private Light _light;
        [SerializeField] private Renderer _emissiveRenderer;
        [SerializeField] private int _materialIndex = 0;
        [SerializeField] private string _emissionProperty = "_EmissionColor";

        [Header("Pattern Settings")]
        [SerializeField] private FlickerPattern _pattern = FlickerPattern.Erratic;
        [SerializeField] private float _baseIntensity = 1.0f;
        [SerializeField] private float _minIntensity = 0.0f;
        [SerializeField] private float _maxIntensity = 1.3f;
        [SerializeField] private float _flickerFrequency = 18.0f;

        [Header("Blackout Behavior")]
        [Range(0f, 1f)]
        [SerializeField] private float _blackoutProbability = 0.12f;
        [SerializeField] private float _minBlackoutDuration = 0.1f;
        [SerializeField] private float _maxBlackoutDuration = 0.6f;

        [Header("Pulse Settings (for PulseBlink)")]
        [SerializeField] private float _pulseSpeed = 2.0f;

        [Header("Audio & Horror Effects")]
        [SerializeField] private AudioSource _buzzAudio;
        [SerializeField] private bool _emitAttentionOnSpike = false;
        [SerializeField] private float _spikeNoiseStrength = 3.0f;

        private Material _targetMaterial;
        private Color _baseEmissionColor = Color.white;
        private bool _isBlackout = false;
        private float _perlinNoiseOffset;
        private float _blackoutTimer = 0f;

        public FlickerPattern Pattern
        {
            get => _pattern;
            set => _pattern = value;
        }

        public Light TargetLight => _light;

        private void Awake()
        {
            if (_light == null)
            {
                _light = GetComponent<Light>();
            }

            if (_light != null && _baseIntensity <= 0f)
            {
                _baseIntensity = _light.intensity;
            }

            _perlinNoiseOffset = Random.Range(0f, 1000f);

            if (_emissiveRenderer == null)
            {
                _emissiveRenderer = GetComponent<Renderer>();
            }

            if (_emissiveRenderer != null)
            {
                var mats = _emissiveRenderer.materials;
                if (_materialIndex >= 0 && _materialIndex < mats.Length)
                {
                    _targetMaterial = mats[_materialIndex];
                    if (_targetMaterial.HasProperty(_emissionProperty))
                    {
                        _baseEmissionColor = _targetMaterial.GetColor(_emissionProperty);
                        if (_baseEmissionColor == Color.black)
                        {
                            _baseEmissionColor = _light != null ? _light.color : Color.white;
                        }
                    }
                }
            }
        }

        private void OnEnable()
        {
            if (_pattern == FlickerPattern.Off)
            {
                ApplyIntensity(0f);
            }
        }

        private void Update()
        {
            if (_light == null) return;

            switch (_pattern)
            {
                case FlickerPattern.Off:
                    ApplyIntensity(0f);
                    break;

                case FlickerPattern.Erratic:
                    UpdateErraticFlicker();
                    break;

                case FlickerPattern.PulseBlink:
                    UpdatePulseBlink();
                    break;

                case FlickerPattern.BlackoutStrobe:
                    UpdateBlackoutStrobe();
                    break;

                case FlickerPattern.SubtleHum:
                    UpdateSubtleHum();
                    break;
            }
        }

        private void UpdateErraticFlicker()
        {
            if (_isBlackout)
            {
                _blackoutTimer -= Time.deltaTime;
                if (_blackoutTimer <= 0f)
                {
                    _isBlackout = false;
                }
                ApplyIntensity(0f);
                return;
            }

            // Chance to drop into a sudden blackout
            if (Random.value < _blackoutProbability * Time.deltaTime * 3f)
            {
                _isBlackout = true;
                _blackoutTimer = Random.Range(_minBlackoutDuration, _maxBlackoutDuration);
                ApplyIntensity(0f);
                return;
            }

            // High frequency perlin noise + random jitter
            float t = Time.time * _flickerFrequency + _perlinNoiseOffset;
            float noise = Mathf.PerlinNoise(t, 0f);
            float jitter = Random.Range(-0.25f, 0.25f);
            float factor = Mathf.Clamp01(noise + jitter);

            float currentIntensity = Mathf.Lerp(_minIntensity, _maxIntensity, factor) * _baseIntensity;
            ApplyIntensity(currentIntensity);

            // Handle buzzing sound volume modulation
            if (_buzzAudio != null)
            {
                _buzzAudio.volume = Mathf.Clamp01(factor);
            }
        }

        private void UpdatePulseBlink()
        {
            // Rhythmic square or sinusoidal pulse
            float wave = Mathf.Sin(Time.time * _pulseSpeed * Mathf.PI * 2f);
            float factor = wave > 0.1f ? 1.0f : (wave > -0.2f ? 0.2f : 0.0f);
            float currentIntensity = factor * _baseIntensity;
            ApplyIntensity(currentIntensity);
        }

        private void UpdateBlackoutStrobe()
        {
            // Mostly black, with occasional brief flash
            float cycle = Time.time % 4.0f;
            float factor = 0f;

            if (cycle < 0.08f || (cycle > 0.15f && cycle < 0.20f))
            {
                factor = Random.Range(0.8f, 1.3f);
                if (_emitAttentionOnSpike && Random.value < 0.1f)
                {
                    AttentionManager.Emit(transform.position, _spikeNoiseStrength, AttentionType.Door);
                }
            }

            ApplyIntensity(factor * _baseIntensity);
        }

        private void UpdateSubtleHum()
        {
            float t = Time.time * 6f + _perlinNoiseOffset;
            float factor = 0.9f + Mathf.PerlinNoise(t, 0f) * 0.2f;
            ApplyIntensity(factor * _baseIntensity);
        }

        private void ApplyIntensity(float intensity)
        {
            if (_light != null)
            {
                _light.intensity = intensity;
                _light.enabled = intensity > 0.001f;
            }

            if (_targetMaterial != null)
            {
                float visualFactor = Mathf.Clamp01(intensity);
                if (_targetMaterial.HasProperty(_emissionProperty))
                {
                    if (intensity <= 0.001f)
                    {
                        _targetMaterial.SetColor(_emissionProperty, Color.black);
                    }
                    else
                    {
                        Color targetEmission = _baseEmissionColor * (visualFactor * 0.25f);
                        _targetMaterial.SetColor(_emissionProperty, targetEmission);
                    }
                }

                if (_targetMaterial.HasProperty("_BaseColor"))
                {
                    Color darkOff = new Color(0.03f, 0.03f, 0.04f, 1f);
                    Color dimOn = new Color(0.55f, 0.60f, 0.65f, 1f);
                    _targetMaterial.SetColor("_BaseColor", intensity <= 0.001f ? darkOff : Color.Lerp(darkOff, dimOn, visualFactor));
                }
            }
        }

        public void Configure(FlickerPattern pattern, float baseIntensity, float minIntensity, float maxIntensity, float frequency)
        {
            _pattern = pattern;
            _baseIntensity = baseIntensity;
            _minIntensity = minIntensity;
            _maxIntensity = maxIntensity;
            _flickerFrequency = frequency;
        }
    }
}

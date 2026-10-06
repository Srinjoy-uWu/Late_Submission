using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LateSubmission.Attention;

namespace LateSubmission.Environment
{
    /// <summary>
    /// Interactive institutional wall light switch that temporarily powers on room lights
    /// before violently sputtering, buzzing, and tripping the circuit breaker back into pitch darkness.
    /// Emits noise that can attract lurking threats.
    /// </summary>
    public class SpookyTimedLightSwitch : MonoBehaviour, LateSubmission.Interaction.IInteractable
    {
        [Header("Target Fixtures")]
        [Tooltip("Lights that turn on when the switch is flipped.")]
        [SerializeField] private List<Light> _targetLights = new List<Light>();

        [Tooltip("Optional emissive renderers to light up.")]
        [SerializeField] private List<Renderer> _emissiveRenderers = new List<Renderer>();

        [Header("Timing Settings")]
        [Tooltip("How long the lights stay on before tripping (seconds).")]
        [SerializeField] private float _onDuration = 18.0f;

        [Tooltip("Time before lights cut out when violent flickering starts (seconds).")]
        [SerializeField] private float _spasmDuration = 3.5f;

        [Tooltip("Cooldown time after breaker trips before switch can be flipped again (seconds).")]
        [SerializeField] private float _cooldownDuration = 4.0f;

        [Header("Light Power Levels")]
        [SerializeField] private float _targetIntensityMultiplier = 1.0f;

        [Header("Master Lighting Bypass")]
        [Tooltip("If true (default), activates horror timed breaker trip. If false, room lights stay on steadily.")]
        [SerializeField] private bool _enableDarkBreaker = true;

        [Header("Atmosphere")]
        [SerializeField] private string _switchPrompt = "Flip Light Switch [E]";
        [SerializeField] private string _cooldownPrompt = "Breaker Tripped...";
        [SerializeField] private float _switchNoise = 6.5f;

        private bool _isOn = false;
        private bool _inCooldown = false;
        private Coroutine _timerCoroutine;
        private AudioSource _audioSource;
        private Dictionary<Light, float> _originalIntensities = new Dictionary<Light, float>();

        // Procedural audio clips to ensure 100% reliability without missing external assets
        private static AudioClip _clickSound;
        private static AudioClip _spasmSound;
        private static AudioClip _popSound;

        private void Awake()
        {
            if (!_enableDarkBreaker)
            {
                // Normal lighting mode: Keep all lights ON at full brightness
                foreach (var l in _targetLights)
                {
                    if (l != null) l.enabled = true;
                }
                SetEmissives(true);
                return;
            }

            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
                _audioSource.spatialBlend = 1.0f;
                _audioSource.minDistance = 1.5f;
                _audioSource.maxDistance = 16f;
                _audioSource.playOnAwake = false;
            }

            InitAudioClips();
            RecordAndTurnOffLights();
        }

        private void RecordAndTurnOffLights()
        {
            _originalIntensities.Clear();
            foreach (var l in _targetLights)
            {
                if (l != null)
                {
                    _originalIntensities[l] = l.intensity > 0 ? l.intensity : 1.0f;
                    l.enabled = false;
                }
            }

            SetEmissives(false);
        }

        public void AddTargetLight(Light l, float originalIntensity = -1f)
        {
            if (l != null && !_targetLights.Contains(l))
            {
                _targetLights.Add(l);
                _originalIntensities[l] = originalIntensity > 0 ? originalIntensity : (l.intensity > 0 ? l.intensity : 1.0f);
                l.enabled = _isOn;
            }
        }

        public void AddEmissiveRenderer(Renderer r)
        {
            if (r != null && !_emissiveRenderers.Contains(r))
            {
                _emissiveRenderers.Add(r);
            }
        }

        public bool CanInteract(LateSubmission.Interaction.Interactor interactor)
        {
            return !_isOn && !_inCooldown;
        }

        public string GetInteractionText()
        {
            if (_inCooldown) return _cooldownPrompt;
            if (_isOn) return "";
            return _switchPrompt;
        }

        public void Interact(LateSubmission.Interaction.Interactor interactor)
        {
            if (_isOn || _inCooldown) return;

            if (_timerCoroutine != null)
            {
                StopCoroutine(_timerCoroutine);
            }

            _timerCoroutine = StartCoroutine(LightCycleRoutine());
        }

        private IEnumerator LightCycleRoutine()
        {
            _isOn = true;

            // 1. Heavy switch click
            PlaySound(_clickSound, 0.85f);

            // 2. Audible ballast hum & lights ignite
            foreach (var kvp in _originalIntensities)
            {
                if (kvp.Key != null)
                {
                    kvp.Key.enabled = true;
                    kvp.Key.intensity = kvp.Value * _targetIntensityMultiplier;
                }
            }
            SetEmissives(true);

            // Alert nearby monster to sound & light
            AttentionManager.Emit(transform.position, _switchNoise, AttentionType.Interaction);

            // 3. Steady illuminated duration
            float steadyTime = Mathf.Max(0.5f, _onDuration - _spasmDuration);
            yield return new WaitForSeconds(steadyTime);

            // 4. Violent ballast spasm & flickering (warning phase)
            PlaySound(_spasmSound, 0.9f);
            float elapsedSpasm = 0f;
            while (elapsedSpasm < _spasmDuration)
            {
                elapsedSpasm += Time.deltaTime;

                // Erratic strobe flicker
                bool flickerOn = Random.value > 0.35f;
                float flickerMultiplier = flickerOn ? Random.Range(0.2f, 1.4f) : 0.0f;

                foreach (var kvp in _originalIntensities)
                {
                    if (kvp.Key != null)
                    {
                        kvp.Key.enabled = flickerOn;
                        kvp.Key.intensity = kvp.Value * _targetIntensityMultiplier * flickerMultiplier;
                    }
                }
                SetEmissives(flickerOn);

                yield return new WaitForSeconds(Random.Range(0.03f, 0.09f));
            }

            // 5. Loud electrical breaker POP / fuse trip -> PITCH DARKNESS
            PlaySound(_popSound, 1.0f);
            AttentionManager.Emit(transform.position, _switchNoise * 1.25f, AttentionType.Machine);

            foreach (var kvp in _originalIntensities)
            {
                if (kvp.Key != null)
                {
                    kvp.Key.enabled = false;
                }
            }
            SetEmissives(false);

            _isOn = false;
            _inCooldown = true;

            // 6. Cooldown before breaker resets
            yield return new WaitForSeconds(_cooldownDuration);
            _inCooldown = false;
        }

        private void SetEmissives(bool active)
        {
            Color c = active ? new Color(0.8f, 0.95f, 0.9f) : Color.black;
            foreach (var r in _emissiveRenderers)
            {
                if (r != null)
                {
                    var block = new MaterialPropertyBlock();
                    r.GetPropertyBlock(block);
                    block.SetColor("_EmissionColor", c);
                    r.SetPropertyBlock(block);
                }
            }
        }

        private void PlaySound(AudioClip clip, float volume)
        {
            if (_audioSource != null && clip != null)
            {
                _audioSource.PlayOneShot(clip, volume);
            }
        }

        private static void InitAudioClips()
        {
            if (_clickSound != null) return;

            // Synthesize crisp mechanical switch click
            int sampleRate = 44100;
            _clickSound = AudioClip.Create("SwitchClick", sampleRate / 10, 1, sampleRate, false);
            float[] clickData = new float[sampleRate / 10];
            for (int i = 0; i < clickData.Length; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 90f);
                float wave = Mathf.Sin(2f * Mathf.PI * 720f * t) + 0.5f * Mathf.Sin(2f * Mathf.PI * 1800f * t);
                clickData[i] = wave * env * 0.8f;
            }
            _clickSound.SetData(clickData, 0);

            // Synthesize buzzing fluorescent spasm
            int spasmSamples = (int)(sampleRate * 2.5f);
            _spasmSound = AudioClip.Create("BallastSpasm", spasmSamples, 1, sampleRate, false);
            float[] spasmData = new float[spasmSamples];
            for (int i = 0; i < spasmData.Length; i++)
            {
                float t = (float)i / sampleRate;
                float hum = Mathf.Sin(2f * Mathf.PI * 120f * t) * 0.4f + Mathf.Sin(2f * Mathf.PI * 240f * t) * 0.25f;
                float crackle = (Random.value * 2f - 1f) * (Random.value > 0.85f ? 0.6f : 0.05f);
                spasmData[i] = (hum + crackle) * 0.5f;
            }
            _spasmSound.SetData(spasmData, 0);

            // Synthesize loud fuse breaker pop
            int popSamples = sampleRate / 3;
            _popSound = AudioClip.Create("BreakerPop", popSamples, 1, sampleRate, false);
            float[] popData = new float[popSamples];
            for (int i = 0; i < popData.Length; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 35f);
                float noise = (Random.value * 2f - 1f);
                float thud = Mathf.Sin(2f * Mathf.PI * 90f * t);
                popData[i] = (noise * 0.6f + thud * 0.7f) * env;
            }
            _popSound.SetData(popData, 0);
        }
    }
}

using System;
using System.Collections;
using UnityEngine;
using LateSubmission.Attention;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace LateSubmission.Weapon
{
    /// <summary>
    /// Improvised Tactical Photonic Blaster.
    /// Combines the Sci-Fi Pistol chassis with an underslung tactical flashlight.
    /// Supports flashlight illumination [F] and high-intensity photonic stun blasts [LMB].
    /// </summary>
    public class MergedLightPistol : MonoBehaviour
    {
        [Header("Flashlight Settings")]
        [SerializeField] private Light _flashlightSpotlight;
        [SerializeField] private bool _startsFlashlightOn = true;
        [SerializeField] private AudioClip _toggleFlashlightSfx;
        [SerializeField] private float _lightNoiseRate = 2.5f;

        [Header("Photonic Stun Blaster (Combat)")]
        [SerializeField] private int _maxCharges = 3;
        [SerializeField] private int _currentCharges = 3;
        [SerializeField] private float _range = 28f;
        [SerializeField] private float _staggerDuration = 3.5f;
        [SerializeField] private LayerMask _hitMask = ~0;
        [SerializeField] private Transform _muzzlePoint;
        [SerializeField] private LineRenderer _tracerLine;
        [SerializeField] private ParticleSystem _muzzleFlash;
        [SerializeField] private Light _muzzleLensGlow;
        [SerializeField] private AudioClip _fireSfx;
        [SerializeField] private AudioClip _emptyClickSfx;
        [SerializeField] private float _fireNoise = 50f;

        [Header("Audio")]
        [SerializeField] private AudioSource _audioSource;

        [Header("Viewmodel Placement")]
        [SerializeField] private Vector3 _viewmodelLocalPosition = new Vector3(0.26f, -0.30f, 0.48f);
        [SerializeField] private Vector3 _viewmodelLocalScale = new Vector3(0.58f, 0.58f, 0.58f);

        private bool _isFlashlightOn;
        private float _lightNoiseTimer;
        private Camera _cam;
        private ViewmodelController _viewmodelController;

        private static Texture2D _softParticleTex;
        private static Material _runtimeParticleMat;
        private static Material _runtimeTracerMat;
        private static AudioClip _fallbackFireSfx;
        private static AudioClip _fallbackEmptyClickSfx;

        public int CurrentCharges => _currentCharges;
        public int MaxCharges => _maxCharges;
        public bool IsFlashlightOn => _isFlashlightOn;

        public event Action<int> OnChargesChanged;
        public event Action<bool> OnFlashlightToggled;

        private void Awake()
        {
            _cam = Camera.main;
            if (_audioSource == null) _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.spatialBlend = 0f;

            _viewmodelController = GetComponent<ViewmodelController>();
            ApplyViewmodelPlacement();
            DisableViewmodelColliders();
            ConfigureVisualEffects();

            _isFlashlightOn = _startsFlashlightOn;
            if (_flashlightSpotlight != null)
            {
                _flashlightSpotlight.enabled = _isFlashlightOn;
            }

            if (_tracerLine != null)
            {
                _tracerLine.enabled = false;
            }
        }

        private void OnEnable()
        {
            ApplyViewmodelPlacement();
            ConfigureVisualEffects();
        }

        private void ApplyViewmodelPlacement()
        {
            if (_viewmodelController == null)
                _viewmodelController = GetComponent<ViewmodelController>();

            if (_viewmodelController != null)
            {
                _viewmodelController.SetRestingTransform(_viewmodelLocalPosition, _viewmodelLocalScale);
            }
            else
            {
                transform.localPosition = _viewmodelLocalPosition;
                transform.localScale = _viewmodelLocalScale;
            }
        }

        private void DisableViewmodelColliders()
        {
            // Viewmodel attached to PlayerCamera should never have active physics colliders
            Collider[] cols = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] != null)
                {
                    cols[i].enabled = false;
                }
            }
        }

        private void ConfigureVisualEffects()
        {
            EnsureRuntimeMaterialsAndAudio();

            if (_tracerLine != null)
            {
                _tracerLine.sharedMaterial = _runtimeTracerMat;
                _tracerLine.startWidth = 0.014f;
                _tracerLine.endWidth = 0.004f;
                _tracerLine.numCapVertices = 2;

                Gradient grad = new Gradient();
                grad.SetKeys(
                    new GradientColorKey[]
                    {
                        new GradientColorKey(new Color(0.75f, 0.98f, 1.0f), 0.0f),
                        new GradientColorKey(new Color(0.15f, 0.78f, 1.0f), 1.0f)
                    },
                    new GradientAlphaKey[]
                    {
                        new GradientAlphaKey(0.92f, 0.0f),
                        new GradientAlphaKey(0.18f, 1.0f)
                    }
                );
                _tracerLine.colorGradient = grad;
            }

            if (_muzzleFlash != null)
            {
                _muzzleFlash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                var psr = _muzzleFlash.GetComponent<ParticleSystemRenderer>();
                if (psr != null)
                {
                    psr.sharedMaterial = _runtimeParticleMat;
                    psr.trailMaterial = _runtimeParticleMat;
                    psr.renderMode = ParticleSystemRenderMode.Billboard;
                }

                var main = _muzzleFlash.main;
                main.playOnAwake = false;
                main.loop = false;
                main.duration = 0.14f;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.2f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.014f, 0.032f);
                main.startColor = new ParticleSystem.MinMaxGradient(
                    new Color(0.75f, 0.97f, 1.0f, 0.95f),
                    new Color(0.20f, 0.80f, 1.0f, 0.85f)
                );
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = 24;

                var emission = _muzzleFlash.emission;
                emission.enabled = false; // We trigger a clean single burst via Emit() on fire

                var shape = _muzzleFlash.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 18f;
                shape.radius = 0.008f;
            }
        }

        private static void EnsureRuntimeMaterialsAndAudio()
        {
            if (_softParticleTex == null)
            {
                int size = 64;
                _softParticleTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                _softParticleTex.wrapMode = TextureWrapMode.Clamp;
                _softParticleTex.filterMode = FilterMode.Bilinear;

                float center = (size - 1) * 0.5f;
                Color[] pixels = new Color[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = (x - center) / center;
                        float dy = (y - center) / center;
                        float dist = Mathf.Sqrt(dx * dx + dy * dy);
                        float alpha = Mathf.Pow(Mathf.Clamp01(1f - dist), 2.4f);
                        pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                    }
                }
                _softParticleTex.SetPixels(pixels);
                _softParticleTex.Apply();
            }

            if (_runtimeParticleMat == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");

                if (shader != null)
                {
                    _runtimeParticleMat = new Material(shader);
                    _runtimeParticleMat.name = "Runtime_PhotonicParticleMat";
                    _runtimeParticleMat.mainTexture = _softParticleTex;
                    _runtimeParticleMat.color = Color.white;
                    if (_runtimeParticleMat.HasProperty("_BaseMap"))
                        _runtimeParticleMat.SetTexture("_BaseMap", _softParticleTex);
                    if (_runtimeParticleMat.HasProperty("_BaseColor"))
                        _runtimeParticleMat.SetColor("_BaseColor", new Color(0.45f, 0.92f, 1.0f, 0.9f));
                }
            }

            if (_runtimeTracerMat == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");

                if (shader != null)
                {
                    _runtimeTracerMat = new Material(shader);
                    _runtimeTracerMat.name = "Runtime_PhotonicTracerMat";
                    _runtimeTracerMat.mainTexture = _softParticleTex;
                    _runtimeTracerMat.color = Color.white;
                    if (_runtimeTracerMat.HasProperty("_BaseColor"))
                        _runtimeTracerMat.SetColor("_BaseColor", new Color(0.55f, 0.95f, 1.0f, 0.9f));
                }
            }

            if (_fallbackFireSfx == null)
            {
                int sampleRate = 44100;
                int samples = (int)(sampleRate * 0.22f);
                _fallbackFireSfx = AudioClip.Create("PhotonicBlast", samples, 1, sampleRate, false);
                float[] data = new float[samples];
                for (int i = 0; i < samples; i++)
                {
                    float t = (float)i / sampleRate;
                    float env = Mathf.Exp(-t * 22f);
                    float sweep = Mathf.Sin(2f * Mathf.PI * (680f - t * 1800f) * t);
                    float sub = Mathf.Sin(2f * Mathf.PI * 110f * t) * Mathf.Exp(-t * 35f);
                    data[i] = (sweep * 0.55f + sub * 0.45f) * env * 0.35f;
                }
                _fallbackFireSfx.SetData(data, 0);

                int clickSamples = (int)(sampleRate * 0.05f);
                _fallbackEmptyClickSfx = AudioClip.Create("PhotonicEmptyClick", clickSamples, 1, sampleRate, false);
                float[] clickData = new float[clickSamples];
                for (int i = 0; i < clickSamples; i++)
                {
                    float t = (float)i / sampleRate;
                    clickData[i] = Mathf.Sin(2f * Mathf.PI * 900f * t) * Mathf.Exp(-t * 90f) * 0.25f;
                }
                _fallbackEmptyClickSfx.SetData(clickData, 0);
            }
        }

        private void Start()
        {
            OnChargesChanged?.Invoke(_currentCharges);
            OnFlashlightToggled?.Invoke(_isFlashlightOn);
        }

        private void Update()
        {
            HandleInputs();

            if (_isFlashlightOn)
            {
                _lightNoiseTimer += Time.deltaTime;
                if (_lightNoiseTimer >= 1.0f)
                {
                    _lightNoiseTimer = 0f;
                    AttentionManager.Emit(transform.position, _lightNoiseRate, AttentionType.Flashlight);
                }
            }
        }

        private void HandleInputs()
        {
            // Flashlight Toggle [F]
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
                    if (Input.GetKeyDown(KeyCode.F)) togglePressed = true;
                }
                catch {}
            }

            if (togglePressed)
            {
                ToggleFlashlight();
            }

            // Stun Fire [LMB]
            bool firePressed = false;
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                firePressed = true;
            }
#endif
            if (!firePressed)
            {
                try
                {
                    if (Input.GetMouseButtonDown(0)) firePressed = true;
                }
                catch {}
            }

            if (firePressed)
            {
                TryFire();
            }
        }

        public void ToggleFlashlight()
        {
            _isFlashlightOn = !_isFlashlightOn;
            if (_flashlightSpotlight != null)
            {
                _flashlightSpotlight.enabled = _isFlashlightOn;
            }

            if (_toggleFlashlightSfx != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(_toggleFlashlightSfx, 0.5f);
            }

            OnFlashlightToggled?.Invoke(_isFlashlightOn);
            AttentionManager.Emit(transform.position, 1.5f, AttentionType.Interaction);
        }

        public bool TryFire()
        {
            EnsureRuntimeMaterialsAndAudio();

            if (_currentCharges <= 0)
            {
                AudioClip emptyClip = _emptyClickSfx != null ? _emptyClickSfx : _fallbackEmptyClickSfx;
                if (emptyClip != null && _audioSource != null)
                {
                    _audioSource.PlayOneShot(emptyClip, 0.35f);
                }
                return false;
            }

            _currentCharges--;
            OnChargesChanged?.Invoke(_currentCharges);

            FireStunBeam();
            return true;
        }

        private void FireStunBeam()
        {
            if (_cam == null) _cam = Camera.main;
            ConfigureVisualEffects();

            Vector3 origin = _muzzlePoint != null ? _muzzlePoint.position : transform.position;
            Ray ray = _cam != null
                ? _cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f))
                : new Ray(origin, transform.forward);

            Vector3 endPoint = ray.origin + ray.direction * _range;
            IDamageable hitDamageable = null;
            IStaggerable hitStaggerable = null;

            RaycastHit[] hits = Physics.RaycastAll(ray, _range, _hitMask, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            bool setEndPoint = false;
            foreach (var h in hits)
            {
                if (h.collider.CompareTag("Player") || h.collider.GetComponentInParent<LateSubmission.Player.FPSController>() != null)
                    continue;

                if (!setEndPoint)
                {
                    endPoint = h.point;
                    setEndPoint = true;
                }

                var dmg = h.collider.GetComponentInParent<IDamageable>();
                var stg = h.collider.GetComponentInParent<IStaggerable>();
                if (dmg != null || stg != null)
                {
                    hitDamageable = dmg;
                    hitStaggerable = stg;
                    endPoint = h.point;
                    break;
                }
            }

            // Fallback generous spherecast so shots aimed at the moving monster never miss between bones or doorway edges
            if (hitDamageable == null && hitStaggerable == null)
            {
                RaycastHit[] sphereHits = Physics.SphereCastAll(ray, 0.35f, _range, _hitMask, QueryTriggerInteraction.Ignore);
                Array.Sort(sphereHits, (a, b) => a.distance.CompareTo(b.distance));
                foreach (var sh in sphereHits)
                {
                    if (sh.collider.CompareTag("Player") || sh.collider.GetComponentInParent<LateSubmission.Player.FPSController>() != null)
                        continue;

                    var dmg = sh.collider.GetComponentInParent<IDamageable>();
                    var stg = sh.collider.GetComponentInParent<IStaggerable>();
                    if (dmg != null || stg != null)
                    {
                        hitDamageable = dmg;
                        hitStaggerable = stg;
                        endPoint = sh.point;
                        break;
                    }
                }
            }

            if (hitDamageable != null)
            {
                // 50 damage per shot -> 2 shots kill the 100 HP monster
                hitDamageable.TakeDamage(50f);
                Debug.Log($"<color=cyan>[MergedLightPistol] STUN BEAM HIT MONSTER! Dealt 50 damage (2 shots to kill).</color>");
            }
            else if (hitStaggerable != null)
            {
                hitStaggerable.Stagger(_staggerDuration);
                Debug.Log($"<color=cyan>[MergedLightPistol] STUN BEAM HIT! Staggered for {_staggerDuration}s.</color>");
            }

            // Recoil kick
            if (_viewmodelController != null)
            {
                _viewmodelController.AddRecoil();
            }

            // Visual FX
            if (_tracerLine != null)
            {
                StartCoroutine(TracerRoutine(origin, endPoint));
            }

            if (_muzzleFlash != null)
            {
                _muzzleFlash.Emit(10);
            }

            if (_muzzleLensGlow != null)
            {
                StartCoroutine(FlashMuzzleLight());
            }

            // Audio & Noise
            AudioClip fireClip = _fireSfx != null ? _fireSfx : _fallbackFireSfx;
            if (fireClip != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(fireClip, 0.35f);
            }

            AttentionManager.Emit(transform.position, _fireNoise, AttentionType.Flashlight);
        }

        private IEnumerator TracerRoutine(Vector3 start, Vector3 end)
        {
            _tracerLine.enabled = true;
            _tracerLine.SetPosition(0, start);
            _tracerLine.SetPosition(1, end);
            yield return new WaitForSeconds(0.07f);
            _tracerLine.enabled = false;
        }

        private IEnumerator FlashMuzzleLight()
        {
            float origIntensity = 0.35f;
            _muzzleLensGlow.intensity = origIntensity * 3.5f;
            yield return new WaitForSeconds(0.09f);
            _muzzleLensGlow.intensity = origIntensity;
        }

        public void Recharge(int charges = 3)
        {
            _currentCharges = Mathf.Clamp(_currentCharges + charges, 0, _maxCharges);
            OnChargesChanged?.Invoke(_currentCharges);
        }
    }
}

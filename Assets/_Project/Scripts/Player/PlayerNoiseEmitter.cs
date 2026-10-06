using UnityEngine;
using LateSubmission.Attention;

namespace LateSubmission.Player
{
    /// <summary>
    /// Surface-aware footstep, jump, and landing noise system.
    /// Uses audio clips from ElmanGameDevTools (Stone, Metal, Grass) with acoustic variation,
    /// and feeds graduated AttentionEvents to the AI perception system.
    /// </summary>
    public class PlayerNoiseEmitter : MonoBehaviour
    {
        [Header("Footstep Intervals")]
        [SerializeField] private float _walkStepInterval = 0.55f;
        [SerializeField] private float _sprintStepInterval = 0.32f;
        [SerializeField] private float _crouchStepInterval = 0.72f;

        [Header("Noise Strengths (AI Perception)")]
        [SerializeField] private float _crouchStrength = 0.25f;
        [SerializeField] private float _walkStrength = 1.5f;
        [SerializeField] private float _sprintStrength = 5.0f;
        [SerializeField] private float _jumpStrength = 2.0f;
        [SerializeField] private float _landStrength = 4.5f;

        [Header("Speed Thresholds")]
        [SerializeField] private float _minWalkSpeed = 0.6f;
        [SerializeField] private float _sprintSpeed = 5.0f;

        [Header("Surface Footstep Audio Clips (from Elman)")]
        [Tooltip("Default corridor floor, concrete, stone, tile")]
        [SerializeField] private AudioClip _stoneFootstepClip;
        [Tooltip("Metal grates, pipes, metal stairs")]
        [SerializeField] private AudioClip _metalFootstepClip;
        [Tooltip("Grass / outdoor areas")]
        [SerializeField] private AudioClip _grassFootstepClip;
        [Tooltip("Optional extra generic footstep clips")]
        [SerializeField] private AudioClip[] _footstepClips;

        [Header("Action Audio")]
        [SerializeField] private AudioClip _jumpSfx;
        [SerializeField] private AudioClip _landSfx;

        [Header("Audio Settings")]
        [Range(0f, 1f)] [SerializeField] private float _masterVolume = 0.42f;
        [Range(0f, 0.2f)] [SerializeField] private float _pitchVariation = 0.08f;
        [SerializeField] private AudioSource _audioSource;

        private CharacterController _controller;
        private FPSController _fpsController;
        private float _stepTimer = 0f;
        private float _stopGraceTimer = 0f;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _fpsController = GetComponent<FPSController>();

            if (_masterVolume > 0.48f) _masterVolume = 0.42f;

            if (_audioSource == null)
            {
                _audioSource = GetComponent<AudioSource>();
            }

            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
            }

            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0.05f; // Mostly 2D for crisp local player feedback
            _audioSource.volume = 0f;

            // Auto-load Elman footstep clips if not explicitly assigned in Inspector
            AutoLoadDefaultClips();
        }

        private void AutoLoadDefaultClips()
        {
#if UNITY_EDITOR
            if (_stoneFootstepClip == null)
            {
                _stoneFootstepClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(
                    "Assets/Imported Assets/ElmanGameDevTools/FirstPersonControllerPro/Player/Song/Stone.wav"
                );
            }
            if (_metalFootstepClip == null)
            {
                _metalFootstepClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(
                    "Assets/Imported Assets/ElmanGameDevTools/FirstPersonControllerPro/Player/Song/metal.wav"
                );
            }
            if (_grassFootstepClip == null)
            {
                _grassFootstepClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(
                    "Assets/Imported Assets/ElmanGameDevTools/FirstPersonControllerPro/Player/Song/grass.wav"
                );
            }
#endif
        }

        private void OnEnable()
        {
            if (_fpsController != null)
            {
                _fpsController.OnJumped += HandleJump;
                _fpsController.OnLanded += HandleLand;
            }
        }

        private void OnDisable()
        {
            if (_fpsController != null)
            {
                _fpsController.OnJumped -= HandleJump;
                _fpsController.OnLanded -= HandleLand;
            }
        }

        private void Update()
        {
            if (_controller == null)
            {
                StopFootstepAudio();
                return;
            }

            Vector3 horizontalVel = new Vector3(_controller.velocity.x, 0, _controller.velocity.z);
            float fpsSpeed = _fpsController != null ? _fpsController.CurrentSpeed : 0f;
            float speed = Mathf.Max(horizontalVel.magnitude, fpsSpeed);

            if (!_controller.isGrounded || speed < _minWalkSpeed)
            {
                _stopGraceTimer += Time.deltaTime;
                if (_stopGraceTimer >= 0.12f)
                {
                    _stepTimer = 0f;
                    StopFootstepAudio();
                }
                return;
            }

            _stopGraceTimer = 0f;

            bool isCrouching = _fpsController != null && _fpsController.IsCrouching;
            bool isSprinting = (_fpsController != null && _fpsController.IsSprinting) || (!isCrouching && speed >= _sprintSpeed);

            float targetInterval = _walkStepInterval;
            float strength = _walkStrength;
            float volume = _masterVolume * 0.65f;
            AttentionType type = AttentionType.FootstepWalk;

            if (isCrouching)
            {
                targetInterval = _crouchStepInterval;
                strength = _crouchStrength;
                volume = _masterVolume * 0.30f;
                type = AttentionType.FootstepWalk;
            }
            else if (isSprinting)
            {
                targetInterval = _sprintStepInterval;
                strength = _sprintStrength;
                volume = _masterVolume * 0.90f;
                type = AttentionType.FootstepSprint;
            }

            _stepTimer += Time.deltaTime;
            if (_stepTimer >= targetInterval)
            {
                _stepTimer = 0f;
                AttentionManager.Emit(transform.position, strength, type);
            }

            UpdateFootstepAudio(speed, isCrouching, isSprinting, volume);
        }

        private void UpdateFootstepAudio(float speed, bool isCrouching, bool isSprinting, float volume)
        {
            if (_audioSource == null) return;

            AudioClip desiredClip = GetSurfaceClip();
            if (desiredClip == null)
            {
                if (_audioSource.isPlaying) _audioSource.Pause();
                return;
            }

            if (_audioSource.clip != desiredClip)
            {
                _audioSource.clip = desiredClip;
                _audioSource.loop = true;
                _audioSource.Play();
            }
            else if (!_audioSource.isPlaying)
            {
                _audioSource.loop = true;
                _audioSource.UnPause();
                if (!_audioSource.isPlaying) _audioSource.Play();
            }

            float targetPitch = 0.95f;
            if (isCrouching) targetPitch = 0.78f;
            else if (isSprinting) targetPitch = 1.25f;

            _audioSource.pitch = Mathf.Lerp(_audioSource.pitch, targetPitch, Time.deltaTime * 12f);
            _audioSource.volume = Mathf.Lerp(_audioSource.volume, volume, Time.deltaTime * 12f);
        }

        private void StopFootstepAudio()
        {
            if (_audioSource != null && _audioSource.isPlaying)
            {
                _audioSource.volume = Mathf.MoveTowards(_audioSource.volume, 0f, Time.deltaTime * 8f);
                if (_audioSource.volume <= 0.01f)
                {
                    _audioSource.Pause();
                }
            }
        }

        private void HandleJump()
        {
            StopFootstepAudio();
            if (_jumpSfx != null && _audioSource != null)
            {
                _audioSource.pitch = 1.0f + Random.Range(-_pitchVariation, _pitchVariation);
                _audioSource.PlayOneShot(_jumpSfx, _masterVolume * 0.8f);
            }
            AttentionManager.Emit(transform.position, _jumpStrength, AttentionType.FootstepWalk);
        }

        private void HandleLand(float impactSpeed)
        {
            if (_audioSource != null && _landSfx != null)
            {
                _audioSource.pitch = 1.0f + Random.Range(-_pitchVariation, _pitchVariation);
                _audioSource.PlayOneShot(_landSfx, Mathf.Clamp01(_masterVolume * (impactSpeed / 5.0f)));
            }

            float finalStrength = Mathf.Clamp(_landStrength * (impactSpeed / 4.0f), 2.0f, 8.0f);
            AttentionManager.Emit(transform.position, finalStrength, AttentionType.FootstepSprint);
        }

        private AudioClip GetSurfaceClip()
        {
            if (Physics.Raycast(transform.position + Vector3.up * 0.2f, Vector3.down, out RaycastHit hit, 1.5f))
            {
                string nameLower = hit.collider.gameObject.name.ToLower();
                string tagLower = hit.collider.tag.ToLower();

                string matName = "";
                var rend = hit.collider.GetComponent<Renderer>();
                if (rend != null && rend.sharedMaterial != null)
                {
                    matName = rend.sharedMaterial.name.ToLower();
                }

                if (tagLower.Contains("metal") || nameLower.Contains("metal") || nameLower.Contains("grate") || nameLower.Contains("pipe") || matName.Contains("metal"))
                {
                    if (_metalFootstepClip != null) return _metalFootstepClip;
                }

                if (tagLower.Contains("grass") || nameLower.Contains("grass") || matName.Contains("grass"))
                {
                    if (_grassFootstepClip != null) return _grassFootstepClip;
                }
            }

            // Default floor (stone/tile/linoleum)
            if (_stoneFootstepClip != null) return _stoneFootstepClip;

            // Optional array fallback
            if (_footstepClips != null && _footstepClips.Length > 0)
            {
                return _footstepClips[Random.Range(0, _footstepClips.Length)];
            }

            return null;
        }
    }
}

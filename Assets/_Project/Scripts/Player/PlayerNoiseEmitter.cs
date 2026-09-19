using UnityEngine;
using LateSubmission.Attention;

namespace LateSubmission.Player
{
    /// <summary>
    /// Measures player movement speed and emits graduated AttentionEvents (Walk vs Sprint).
    /// </summary>
    public class PlayerNoiseEmitter : MonoBehaviour
    {
        [Header("Footstep Intervals")]
        [SerializeField] private float _walkStepInterval = 0.55f;
        [SerializeField] private float _sprintStepInterval = 0.32f;

        [Header("Noise Strengths")]
        [SerializeField] private float _walkStrength = 1.5f;
        [SerializeField] private float _sprintStrength = 5.0f;

        [Header("Speed Thresholds")]
        [SerializeField] private float _minWalkSpeed = 1.0f;
        [SerializeField] private float _sprintSpeed = 5.0f;

        [Header("Audio")]
        [SerializeField] private AudioClip[] _footstepClips;
        [SerializeField] private AudioSource _audioSource;

        private CharacterController _controller;
        private float _stepTimer = 0f;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (_audioSource == null) _audioSource = GetComponent<AudioSource>();
        }

        private void Update()
        {
            float speed = 0f;

            if (_controller != null)
            {
                Vector3 horizontalVel = new Vector3(_controller.velocity.x, 0, _controller.velocity.z);
                speed = horizontalVel.magnitude;
            }

            if (speed < _minWalkSpeed)
            {
                _stepTimer = 0f;
                return;
            }

            bool isSprinting = speed >= _sprintSpeed;
            float targetInterval = isSprinting ? _sprintStepInterval : _walkStepInterval;
            float strength = isSprinting ? _sprintStrength : _walkStrength;
            AttentionType type = isSprinting ? AttentionType.FootstepSprint : AttentionType.FootstepWalk;

            _stepTimer += Time.deltaTime;
            if (_stepTimer >= targetInterval)
            {
                _stepTimer = 0f;
                PlayFootstepAudio();
                AttentionManager.Emit(transform.position, strength, type);
            }
        }

        private void PlayFootstepAudio()
        {
            if (_audioSource != null && _footstepClips != null && _footstepClips.Length > 0)
            {
                int index = Random.Range(0, _footstepClips.Length);
                _audioSource.PlayOneShot(_footstepClips[index]);
            }
        }
    }
}

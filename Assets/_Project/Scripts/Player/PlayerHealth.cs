using System;
using System.Collections;
using UnityEngine;
using LateSubmission.AI;
using LateSubmission.Objectives;

namespace LateSubmission.Player
{
    /// <summary>
    /// Player health and mortality system for Late Submission.
    /// Manages damage taking, health events, death screen feedback,
    /// and respawning at the Floor 02 entry point (Floor02_Start_Spawn).
    /// </summary>
    public class PlayerHealth : MonoBehaviour
    {
        [Header("Health Attributes")]
        [SerializeField] private float _maxHealth = 100f;
        [SerializeField] private float _currentHealth = 100f;

        [Header("Audio")]
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _hurtSfx;
        [SerializeField] private AudioClip _deathSfx;
        [SerializeField] private AudioClip _heartbeatLowHealthSfx;

        [Header("Respawn Location")]
        [SerializeField] private Vector3 _floor02EntrySpawn = new Vector3(6.5f, 0.1f, 0.12f);

        public static PlayerHealth Instance { get; private set; }

        public float MaxHealth => _maxHealth;
        public float CurrentHealth => _currentHealth;
        public bool IsDead => _currentHealth <= 0f;

        public event Action<float, float> OnHealthChanged;
        public event Action<float> OnPlayerDamaged;
        public event Action OnPlayerDied;
        public event Action OnPlayerRespawned;

        private FPSController _fpsController;
        private CharacterController _characterController;
        private bool _isRespawning = false;

        private void Awake()
        {
            Instance = this;
            _currentHealth = _maxHealth;

            _fpsController = GetComponent<FPSController>();
            _characterController = GetComponent<CharacterController>();

            if (_audioSource == null)
            {
                _audioSource = GetComponent<AudioSource>();
                if (_audioSource == null)
                {
                    _audioSource = gameObject.AddComponent<AudioSource>();
                    _audioSource.spatialBlend = 0f;
                    _audioSource.playOnAwake = false;
                }
            }

            // Find start spawn object in scene if present
            var spawnGo = GameObject.Find("Floor02_Start_Spawn");
            if (spawnGo != null)
            {
                _floor02EntrySpawn = spawnGo.transform.position;
            }
        }

        private void Start()
        {
            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
        }

        public void TakeDamage(float damage, Vector3 sourcePosition)
        {
            if (IsDead || _isRespawning) return;

            _currentHealth = Mathf.Max(0f, _currentHealth - damage);
            Debug.Log($"<color=red>[PlayerHealth] Took {damage} damage from {sourcePosition}! HP: {_currentHealth}/{_maxHealth}</color>");

            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
            OnPlayerDamaged?.Invoke(damage);

            if (_hurtSfx == null) InitAudioClips();
            if (_audioSource != null)
            {
                AudioClip clipToPlay = _hurtSfx != null ? _hurtSfx : _fallbackHurtSfx;
                if (clipToPlay != null) _audioSource.PlayOneShot(clipToPlay, 0.95f);
            }

            if (_currentHealth <= 0f)
            {
                Die();
            }
        }

        public void Heal(float amount)
        {
            if (IsDead) return;

            _currentHealth = Mathf.Min(_maxHealth, _currentHealth + amount);
            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
        }

        private void Die()
        {
            _currentHealth = 0f;
            Debug.Log("<color=red>[PlayerHealth] PLAYER DIED! Initiating respawn at Floor 02 Entry...</color>");

            OnPlayerDied?.Invoke();

            if (_deathSfx == null) InitAudioClips();
            if (_audioSource != null)
            {
                AudioClip clipToPlay = _deathSfx != null ? _deathSfx : _fallbackDeathSfx;
                if (clipToPlay != null) _audioSource.PlayOneShot(clipToPlay, 1.0f);
            }

            if (_fpsController != null)
            {
                _fpsController.enabled = false;
            }

            if (!_isRespawning)
            {
                StartCoroutine(RespawnRoutine());
            }
        }

        private IEnumerator RespawnRoutine()
        {
            _isRespawning = true;

            // Wait on death screen
            yield return new WaitForSeconds(3.0f);

            // Relocate player to Floor 02 entry spawn
            if (_characterController != null)
            {
                _characterController.enabled = false;
            }

            transform.position = _floor02EntrySpawn;
            transform.rotation = Quaternion.Euler(0f, -90f, 0f); // Facing west down the corridor

            if (_characterController != null)
            {
                _characterController.enabled = true;
            }

            // Reset health
            _currentHealth = _maxHealth;
            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);

            // Relocate boss back to Mechatronics Lab open space and resume roam so player can retry
            if (BossMonsterController.Instance != null)
            {
                BossMonsterController.Instance.ResetToMechatronics();
            }

            // Restore player controls
            if (_fpsController != null)
            {
                _fpsController.enabled = true;
            }

            // Update objective
            if (ObjectiveManager.Instance != null)
            {
                ObjectiveManager.Instance.SetCustomObjective("RESPAWNED AT FLOOR 02 ENTRY. EVADE MUTATED PROF. ANISH MONDAL & RETRIEVE LAB KEYS!");
            }

            OnPlayerRespawned?.Invoke();
            _isRespawning = false;
            Debug.Log("<color=green>[PlayerHealth] Player successfully respawned at Floor 02 entry.</color>");
        }

        private static AudioClip _fallbackHurtSfx;
        private static AudioClip _fallbackDeathSfx;

        private static void InitAudioClips()
        {
            if (_fallbackHurtSfx != null) return;

            int sampleRate = 44100;
            // Hurt grunt / gasp sound (visceral low-frequency punch)
            int hurtSamples = (int)(sampleRate * 0.35f);
            _fallbackHurtSfx = AudioClip.Create("PlayerHurt", hurtSamples, 1, sampleRate, false);
            float[] hurtData = new float[hurtSamples];
            for (int i = 0; i < hurtSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 18f);
                float punch = Mathf.Sin(2f * Mathf.PI * (110f - t * 80f) * t);
                float noise = (UnityEngine.Random.value * 2f - 1f) * Mathf.Exp(-t * 28f);
                hurtData[i] = (punch * 0.7f + noise * 0.5f) * env;
            }
            _fallbackHurtSfx.SetData(hurtData, 0);

            // Death flatline & agony sound
            int deathSamples = (int)(sampleRate * 1.5f);
            _fallbackDeathSfx = AudioClip.Create("PlayerDeath", deathSamples, 1, sampleRate, false);
            float[] deathData = new float[deathSamples];
            for (int i = 0; i < deathSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Clamp01(1f - t / 1.5f);
                float heart = Mathf.Sin(2f * Mathf.PI * 55f * t) * Mathf.Exp(-((t % 0.6f) * 12f));
                float hiss = (UnityEngine.Random.value * 2f - 1f) * 0.2f * env;
                deathData[i] = (heart * 0.8f + hiss) * env;
            }
            _fallbackDeathSfx.SetData(deathData, 0);
        }
    }
}

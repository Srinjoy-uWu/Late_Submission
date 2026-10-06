using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using LateSubmission.AI;
using LateSubmission.Interaction;

namespace LateSubmission.Audio
{
    /// <summary>
    /// Game-wide atmospheric audio director for Late Submission.
    /// Persists across all Floor 01 and Floor 02 scenes (DontDestroyOnLoad) so that
    /// the ambient music plays continuously and consistently throughout the entire game
    /// without cutting out or restarting when entering/exiting rooms.
    /// </summary>
    public class GameAudioDirector : MonoBehaviour
    {
        [Header("Audio Sources")]
        [SerializeField] private AudioSource _ambientSource;
        [SerializeField] private AudioSource _chaseMusicSource;
        [SerializeField] private AudioSource _stingerSource;

        [Header("Audio Clips")]
        [SerializeField] private AudioClip _ambientDroneClip;
        [SerializeField] private AudioClip _chaseMusicClip;
        [SerializeField] private AudioClip _proximityStingerClip;
        [SerializeField] private AudioClip _safeRoomReliefClip;

        [Header("Volume Balances")]
        [SerializeField] private float _targetAmbientVolume = 0.45f;
        [SerializeField] private float _targetChaseVolume = 0.75f;
        [SerializeField] private float _fadeDuration = 1.2f;

        private Coroutine _ambientFadeRoutine;
        private Coroutine _chaseFadeRoutine;
        private bool _isBoltedInSafeRoom = false;

        public static GameAudioDirector Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (transform.parent != null)
            {
                transform.SetParent(null);
            }
            DontDestroyOnLoad(gameObject);

            if (_ambientSource == null)
            {
                _ambientSource = gameObject.AddComponent<AudioSource>();
                _ambientSource.loop = true;
                _ambientSource.playOnAwake = false;
                _ambientSource.spatialBlend = 0f;
                _ambientSource.volume = 0f;
            }

            if (_chaseMusicSource == null)
            {
                _chaseMusicSource = gameObject.AddComponent<AudioSource>();
                _chaseMusicSource.loop = true;
                _chaseMusicSource.playOnAwake = false;
                _chaseMusicSource.spatialBlend = 0f;
                _chaseMusicSource.volume = 0f;
            }

            if (_stingerSource == null)
            {
                _stingerSource = gameObject.AddComponent<AudioSource>();
                _stingerSource.loop = false;
                _stingerSource.playOnAwake = false;
                _stingerSource.spatialBlend = 0f;
            }

            SceneManager.sceneLoaded += OnSceneLoaded;

            // Floor 02: Boss Monster events
            BossMonsterController.OnBossChaseStarted += HandleChaseStarted;
            BossMonsterController.OnBossChaseEnded   += HandleChaseEnded;

            // Floor 01: Entity events
            EntityController.OnEntityHuntStarted += HandleChaseStarted;
            EntityController.OnEntityHuntEnded   += HandleChaseEnded;

            // Safe room bolt events (bathroom door and wood door)
            BathroomDoorController.OnBathroomBolted    += HandleBathroomSafeRoomBolted;
            BathroomDoorController.OnBathroomUnbolted  += HandleBathroomSafeRoomUnbolted;
            WoodDoorController.OnSafeDoorBolted        += HandleWoodSafeRoomBolted;
            WoodDoorController.OnSafeDoorUnbolted      += HandleWoodSafeRoomUnbolted;
        }

        private void OnDestroy()
        {
            if (Instance != this) return;

            SceneManager.sceneLoaded -= OnSceneLoaded;

            BossMonsterController.OnBossChaseStarted -= HandleChaseStarted;
            BossMonsterController.OnBossChaseEnded   -= HandleChaseEnded;

            EntityController.OnEntityHuntStarted -= HandleChaseStarted;
            EntityController.OnEntityHuntEnded   -= HandleChaseEnded;

            BathroomDoorController.OnBathroomBolted   -= HandleBathroomSafeRoomBolted;
            BathroomDoorController.OnBathroomUnbolted -= HandleBathroomSafeRoomUnbolted;
            WoodDoorController.OnSafeDoorBolted       -= HandleWoodSafeRoomBolted;
            WoodDoorController.OnSafeDoorUnbolted     -= HandleWoodSafeRoomUnbolted;
        }

        private void Start()
        {
            StartAmbience();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _isBoltedInSafeRoom = false;

            // Ensure chase music is stopped when moving to a new scene
            if (_chaseMusicSource != null && _chaseMusicSource.isPlaying)
            {
                FadeAudioSource(_chaseMusicSource, 0f, 0.8f, ref _chaseFadeRoutine);
            }

            // Keep ambient music playing seamlessly across scene transitions without restarting
            StartAmbience();
        }

        public void StartAmbience()
        {
            if (_ambientDroneClip != null && _ambientSource != null)
            {
                if (_ambientSource.clip != _ambientDroneClip || !_ambientSource.isPlaying)
                {
                    _ambientSource.clip = _ambientDroneClip;
                    _ambientSource.Play();
                }
                FadeAudioSource(_ambientSource, _targetAmbientVolume, _fadeDuration, ref _ambientFadeRoutine);
            }
        }

        private void HandleChaseStarted()
        {
            if (_isBoltedInSafeRoom) return;

            Debug.Log("<color=red>[GameAudioDirector] Chase started! Cross-fading chase track.</color>");
            if (_chaseMusicClip != null && _chaseMusicSource != null)
            {
                if (!_chaseMusicSource.isPlaying)
                {
                    _chaseMusicSource.clip = _chaseMusicClip;
                    _chaseMusicSource.Play();
                }
                FadeAudioSource(_chaseMusicSource, _targetChaseVolume, 0.8f, ref _chaseFadeRoutine);
            }
        }

        private void HandleChaseEnded()
        {
            if (_chaseMusicSource != null && _chaseMusicSource.isPlaying)
            {
                FadeAudioSource(_chaseMusicSource, 0f, 2.0f, ref _chaseFadeRoutine);
            }
        }

        private void HandleBathroomSafeRoomBolted(BathroomDoorController door) => OnSafeRoomStateChanged(true);
        private void HandleBathroomSafeRoomUnbolted(BathroomDoorController door) => OnSafeRoomStateChanged(false);
        private void HandleWoodSafeRoomBolted(WoodDoorController door) => OnSafeRoomStateChanged(true);
        private void HandleWoodSafeRoomUnbolted(WoodDoorController door) => OnSafeRoomStateChanged(false);

        private void OnSafeRoomStateChanged(bool isBolted)
        {
            _isBoltedInSafeRoom = isBolted;

            if (_isBoltedInSafeRoom)
            {
                // Immediately cut chase music in safe room
                if (_chaseMusicSource != null)
                    FadeAudioSource(_chaseMusicSource, 0f, 0.6f, ref _chaseFadeRoutine);

                // Muffle ambient drone
                if (_ambientSource != null)
                    FadeAudioSource(_ambientSource, _targetAmbientVolume * 0.4f, 1.0f, ref _ambientFadeRoutine);

                // Play relief stinger
                if (_safeRoomReliefClip != null && _stingerSource != null)
                    _stingerSource.PlayOneShot(_safeRoomReliefClip, 0.8f);
            }
            else
            {
                // Unbolted: restore ambient level
                if (_ambientSource != null)
                    FadeAudioSource(_ambientSource, _targetAmbientVolume, 1.2f, ref _ambientFadeRoutine);
            }
        }

        private void FadeAudioSource(AudioSource source, float targetVol, float duration, ref Coroutine routine)
        {
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(FadeRoutine(source, targetVol, duration));
        }

        private IEnumerator FadeRoutine(AudioSource source, float targetVol, float duration)
        {
            if (source == null) yield break;

            float startVol = source.volume;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                source.volume = Mathf.Lerp(startVol, targetVol, elapsed / duration);
                yield return null;
            }

            source.volume = targetVol;
            if (targetVol <= 0.001f)
                source.Stop();
        }
    }
}

using System.Collections;
using UnityEngine;
using LateSubmission.Player;
using LateSubmission.Objectives;
using LateSubmission.Interaction;

namespace LateSubmission.AI
{
    /// <summary>
    /// Attaches to the Mechatronics Lab door (or its parent).
    /// Listens for WoodDoorController.OnDoorOpened — when the player opens
    /// the Mechatronics Lab door for the first time, the mutated professor
    /// spawns inside and immediately hunts the player.
    /// </summary>
    public class BossSpawnTrigger : MonoBehaviour
    {
        [Header("Door Reference")]
        [Tooltip("Assign the WoodDoorController on the Mechatronics Lab door. " +
                 "If left empty, the component on this same GameObject is used.")]
        [SerializeField] private WoodDoorController _mechatronicsLabDoor;

        [Header("Spawn Position")]
        [Tooltip("Optional scene Transform for exact spawn location inside the lab.")]
        [SerializeField] private Transform _customSpawnPoint;

        [Tooltip("Fallback world position if no Transform is assigned.")]
        [SerializeField] private Vector3 _bossSpawnPos = new Vector3(-34.5f, 0.05f, 14.5f);

        [Header("Audio")]
        [SerializeField] private AudioClip _batteryDepletedSfx;
        [SerializeField] private AudioSource _audioSource;

        public Vector3 EffectiveSpawnPosition =>
            _customSpawnPoint != null ? _customSpawnPoint.position : _bossSpawnPos;

        private bool _hasTriggered = false;
        private bool _doorOpened = false;
        private bool _flashlightDepleted = false;
        private Transform _playerTransform;

        private void Awake()
        {
            // If no door is assigned, try to find one on this GameObject or its parent
            if (_mechatronicsLabDoor == null)
            {
                _mechatronicsLabDoor = GetComponent<WoodDoorController>();
                if (_mechatronicsLabDoor == null)
                    _mechatronicsLabDoor = GetComponentInParent<WoodDoorController>();
            }
        }

        private void Start()
        {
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                _playerTransform = playerObj.transform;
            }
            else
            {
                var fps = Object.FindFirstObjectByType<FPSController>();
                if (fps != null) _playerTransform = fps.transform;
            }
        }

        private void OnEnable()
        {
            WoodDoorController.OnDoorOpened += HandleDoorOpened;
        }

        private void OnDisable()
        {
            WoodDoorController.OnDoorOpened -= HandleDoorOpened;
        }

        private void Update()
        {
            if (!_doorOpened && _mechatronicsLabDoor != null && _mechatronicsLabDoor.IsOpen)
            {
                OnMechatronicsDoorOpened();
            }

            if (!_doorOpened) return;

            if (_playerTransform == null)
            {
                var playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null) _playerTransform = playerObj.transform;
                else
                {
                    var fps = Object.FindFirstObjectByType<FPSController>();
                    if (fps != null) _playerTransform = fps.transform;
                }
            }

            // Deplete flashlight when player steps into the Mechatronics Lab interior
            if (!_flashlightDepleted && _playerTransform != null)
            {
                Vector3 p = _playerTransform.position;
                if (p.z >= 2.15f && p.x >= -43.5f && p.x <= -24.5f)
                {
                    DepletePlayerFlashlight();
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            if (_doorOpened || _mechatronicsLabDoor == null || _mechatronicsLabDoor.IsOpen)
            {
                DepletePlayerFlashlight();
            }
        }

        /// <summary>
        /// Called whenever ANY WoodDoorController opens.
        /// When the Mechatronics Lab door opens, the monster starts walking towards the Mechatronics Lab door
        /// and activates as soon as it sees the player.
        /// </summary>
        private void HandleDoorOpened(WoodDoorController door)
        {
            if (_doorOpened) return;

            bool isMechatroDoor = (_mechatronicsLabDoor != null && door == _mechatronicsLabDoor)
                                  || door.gameObject.name.ToLower().Contains("mechatronics");

            if (!isMechatroDoor) return;

            OnMechatronicsDoorOpened();
        }

        private void OnMechatronicsDoorOpened()
        {
            if (_doorOpened) return;
            _doorOpened = true;
            _hasTriggered = true;

            Debug.Log("<color=yellow>[BossSpawnTrigger] Mechatronics Lab door opened! Monster is now approaching the Mechatronics Lab door...</color>");

            Vector3 spawnPos = EffectiveSpawnPosition;
            var boss = BossMonsterController.Instance != null
                ? BossMonsterController.Instance
                : Object.FindFirstObjectByType<BossMonsterController>(FindObjectsInactive.Include);

            if (boss != null)
            {
                boss.StartApproachingMechatronicsDoor(spawnPos);
            }
            else
            {
                Debug.LogError("[BossSpawnTrigger] BossMonsterController not found in scene!");
            }
        }

        public void DepletePlayerFlashlight()
        {
            if (_flashlightDepleted) return;
            _flashlightDepleted = true;

            Debug.Log("<color=red>[BossSpawnTrigger] Flashlight overloaded by Mechatronics Lab!</color>");

            var flashlight = Object.FindFirstObjectByType<FlashlightController>();
            if (flashlight != null)
            {
                if (flashlight.IsOn) flashlight.Toggle();
                flashlight.enabled = false;
            }

            var heldItems = Object.FindFirstObjectByType<LateSubmission.Weapon.HeldItemController>();
            if (heldItems != null)
            {
                heldItems.OnFlashlightDepleted();
            }

            if (_batteryDepletedSfx != null)
            {
                if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
                _audioSource.PlayOneShot(_batteryDepletedSfx, 1.0f);
            }

            if (ObjectiveManager.Instance != null)
            {
                ObjectiveManager.Instance.SetCustomObjective(
                    "BATTERY DEPLETED! EVADE MUTATED PROF. ANISH MONDAL — RETREAT TO RESTROOM!");
            }
        }

        private void OnDrawGizmos()
        {
            Vector3 pos = EffectiveSpawnPosition;
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.8f);
            Gizmos.DrawWireSphere(pos, 0.75f);
            Gizmos.DrawLine(pos, pos + Vector3.up * 2.2f);

            if (_mechatronicsLabDoor != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, _mechatronicsLabDoor.transform.position);
            }
        }

        // Public setter so the integrator can wire the door reference at edit time
        public void SetMechatronicsDoor(WoodDoorController door) => _mechatronicsLabDoor = door;
        public void SetCustomSpawnPoint(Transform spawnPoint) => _customSpawnPoint = spawnPoint;
    }
}

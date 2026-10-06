using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using LateSubmission.Inventory;
using LateSubmission.Attention;

namespace LateSubmission.Interaction
{
    /// <summary>
    /// Smooth rotating door controller optimized for Free Wood Door Pack assets.
    /// Handles key locking, prompt positioning on door mesh, obstacle carving,
    /// and safe room bolt integration.
    /// </summary>
    public class WoodDoorController : MonoBehaviour, IInteractable
    {
        [Header("Door Leaf & Hinge")]
        [SerializeField] private Transform _doorLeaf;
        [SerializeField] private float _openAngle = -90.0f;
        [SerializeField] private float _animationDuration = 0.85f;

        [Header("State")]
        [SerializeField] private bool _isOpen = false;
        [SerializeField] private bool _isLocked = false;
        [SerializeField] private bool _isPermanentlyLocked = false;
        [SerializeField] private ItemType _requiredKey = ItemType.FacultyKey;
        [SerializeField] private string _lockedMessage = "Locked. Requires key.";

        [Header("Safe Room Bolt")]
        [SerializeField] private bool _canBolt = false;
        [SerializeField] private bool _isBolted = false;

        [Header("Audio")]
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _openSfx;
        [SerializeField] private AudioClip _closeSfx;
        [SerializeField] private AudioClip _lockedSfx;
        [SerializeField] private AudioClip _unlockSfx;
        [SerializeField] private AudioClip _boltSfx;

        [Header("NavMesh")]
        [SerializeField] private NavMeshObstacle _navObstacle;

        public static event Action<WoodDoorController> OnSafeDoorBolted;
        public static event Action<WoodDoorController> OnSafeDoorUnbolted;
        /// <summary>Fired whenever any WoodDoorController is opened. Payload is the door that opened.</summary>
        public static event Action<WoodDoorController> OnDoorOpened;

        private Quaternion _closedRotation;
        private Quaternion _targetRotation;
        private Coroutine _animationRoutine;
        private Collider _leafCollider;
        private Collider _rootCollider;

        public bool IsOpen => _isOpen;
        public bool IsLocked => _isLocked;
        public bool IsPermanentlyLocked => _isPermanentlyLocked;
        public bool IsBolted => _isBolted;
        public ItemType RequiredKey => _requiredKey;

        private void Awake()
        {
            // Safeguard: disable any duplicate legacy Door script
            var legacyDoor = GetComponent<Door>();
            if (legacyDoor != null)
            {
                legacyDoor.enabled = false;
            }

            // Ensure EC Lab door starts locked and cannot be opened unless the player gets the EC Lab Key from Mechatronics Lab
            if (gameObject.name.IndexOf("ECLab", StringComparison.OrdinalIgnoreCase) >= 0 || _requiredKey == ItemType.ECLabKey)
            {
                _requiredKey = ItemType.ECLabKey;
                _isPermanentlyLocked = false;
                _isLocked = true;
                if (string.IsNullOrEmpty(_lockedMessage))
                {
                    _lockedMessage = "Locked. Requires EC Lab Key (Search Mechatronics Lab).";
                }
            }

            _rootCollider = GetComponent<Collider>();

            if (_doorLeaf == null || _doorLeaf == transform)
            {
                var leafChild = transform.Find("DoorLeaf")
                             ?? transform.Find("grid_3") 
                             ?? transform.Find("grid_2") 
                             ?? transform.Find("grid_1") 
                             ?? transform.Find("Door")
                             ?? transform.Find("door_3_LOD0")
                             ?? transform.Find("door_2_LOD0")
                             ?? transform.Find("door_1_LOD0");
                _doorLeaf = leafChild != null ? leafChild : transform;
            }

            // Fix Door_MechatronicsLab where _doorLeaf ("DoorLeaf") is an empty hinge pivot while the visual door meshes are siblings on root
            if (_doorLeaf != null && _doorLeaf != transform && _doorLeaf.childCount == 0 && _doorLeaf.GetComponent<MeshRenderer>() == null)
            {
                var childrenToReparent = new System.Collections.Generic.List<Transform>();
                for (int i = 0; i < transform.childCount; i++)
                {
                    Transform child = transform.GetChild(i);
                    if (child == null || child == _doorLeaf) continue;
                    // Keep the stationary door frame (grid_1 / grid_2 / grid_3) attached to the root
                    if (child.name.StartsWith("grid_", StringComparison.OrdinalIgnoreCase)) continue;
                    childrenToReparent.Add(child);
                }
                foreach (var child in childrenToReparent)
                {
                    child.SetParent(_doorLeaf, true);
                }
            }

            _closedRotation = _doorLeaf.localRotation;
            _targetRotation = _isOpen ? _closedRotation * Quaternion.Euler(0f, _openAngle, 0f) : _closedRotation;
            _doorLeaf.localRotation = _targetRotation;

            _leafCollider = _doorLeaf.GetComponent<Collider>();
            if (_leafCollider == null)
            {
                _leafCollider = _doorLeaf.GetComponentInChildren<Collider>();
                if (_leafCollider == null)
                {
                    _leafCollider = _doorLeaf.gameObject.AddComponent<BoxCollider>();
                }
            }

            if (_audioSource == null)
            {
                _audioSource = GetComponent<AudioSource>();
                if (_audioSource == null)
                {
                    _audioSource = gameObject.AddComponent<AudioSource>();
                    _audioSource.spatialBlend = 1.0f;
                    _audioSource.rolloffMode = AudioRolloffMode.Linear;
                    _audioSource.minDistance = 1.0f;
                    _audioSource.maxDistance = 25.0f;
                    _audioSource.playOnAwake = false;
                }
            }

            if (_navObstacle == null)
            {
                _navObstacle = GetComponent<NavMeshObstacle>();
                if (_navObstacle == null)
                {
                    _navObstacle = gameObject.AddComponent<NavMeshObstacle>();
                    _navObstacle.carving = true;
                    _navObstacle.size = new Vector3(1.2f, 2.2f, 0.3f);
                    _navObstacle.center = new Vector3(0f, 1.1f, 0f);
                }
            }

            UpdateObstacle();
            UpdateColliders();
        }

        private void Start()
        {
            UpdateColliders();
        }

        public void UpdateColliders()
        {
            // When door is open, set colliders so player and monster effortlessly pass through
            if (_rootCollider != null)
            {
                _rootCollider.enabled = !_isOpen;
            }

            if (_leafCollider != null)
            {
                _leafCollider.isTrigger = _isOpen;
            }

            // Also make sure all child colliders under doorLeaf become triggers when open
            if (_doorLeaf != null)
            {
                foreach (var c in _doorLeaf.GetComponentsInChildren<Collider>())
                {
                    if (c is MeshCollider mc && !mc.convex) continue;
                    c.isTrigger = _isOpen;
                }
            }
        }

        public void ConfigureLock(bool isLocked, bool isPermanentlyLocked, ItemType requiredKey, string lockedMessage)
        {
            _isLocked = isLocked;
            _isPermanentlyLocked = isPermanentlyLocked;
            _requiredKey = requiredKey;
            _lockedMessage = lockedMessage;
        }

        public void ConfigureBolt(bool canBolt)
        {
            _canBolt = canBolt;
        }

        public void ConfigureAudio(AudioClip openClip, AudioClip closeClip, AudioClip lockedClip = null)
        {
            _openSfx = openClip;
            _closeSfx = closeClip;
            if (lockedClip != null) _lockedSfx = lockedClip;
        }

        public string GetInteractionText()
        {
            if (_canBolt && _isBolted)
            {
                return "Unbolt & Open Door [E]";
            }

            if (_isPermanentlyLocked)
            {
                return _lockedMessage;
            }

            if (_isLocked)
            {
                if (InventoryManager.Instance != null && InventoryManager.Instance.HasItemType(_requiredKey))
                {
                    return "Unlock Door [E]";
                }
                return _lockedMessage;
            }

            if (_canBolt)
            {
                if (_isOpen && IsPlayerInsideSafeRoom())
                {
                    return "Close & Bolt Bathroom Door [E]";
                }
                if (!_isOpen)
                {
                    return IsPlayerInsideSafeRoom() ? "Bolt Bathroom Door [E]" : "Open Bathroom Door [E]";
                }
            }

            return _isOpen ? "Close Door [E]" : "Open Door [E]";
        }

        public bool CanInteract(Interactor interactor) => true;

        public void Interact(Interactor interactor)
        {
            if (_canBolt && _isBolted)
            {
                UnboltDoor();
                return;
            }

            if (_isPermanentlyLocked)
            {
                PlayClip(_lockedSfx);
                return;
            }

            if (_isLocked)
            {
                if (InventoryManager.Instance != null && InventoryManager.Instance.HasItemType(_requiredKey))
                {
                    _isLocked = false;
                    PlayClip(_unlockSfx != null ? _unlockSfx : _openSfx);
                    SetDoorOpen(true);
                }
                else
                {
                    PlayClip(_lockedSfx);
                }
                return;
            }

            if (_canBolt)
            {
                if (_isOpen)
                {
                    if (IsPlayerInsideSafeRoom())
                    {
                        BoltDoor();
                    }
                    else
                    {
                        SetDoorOpen(false);
                    }
                    return;
                }
                else
                {
                    if (IsPlayerInsideSafeRoom())
                    {
                        BoltDoor();
                    }
                    else
                    {
                        SetDoorOpen(true);
                    }
                    return;
                }
            }

            // Normal toggle
            SetDoorOpen(!_isOpen);
        }

        private void Update()
        {
            if (!_canBolt) return;

            bool inside = IsPlayerInsideSafeRoom();
            if (!_isOpen && !_isBolted && inside)
            {
                BoltDoor();
            }
            else if (_isBolted && !inside)
            {
                _isBolted = false;
                OnSafeDoorUnbolted?.Invoke(this);
            }
        }

        private bool IsPlayerInsideSafeRoom()
        {
            Transform playerTransform = null;
            var player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }
            else
            {
                var fps = UnityEngine.Object.FindFirstObjectByType<LateSubmission.Player.FPSController>();
                if (fps != null) playerTransform = fps.transform;
            }

            if (playerTransform == null) return false;
            Vector3 pos = playerTransform.position;
            return pos.z >= 2.15f && pos.x >= -16.5f && pos.x <= -3.5f;
        }

        public void BoltDoor()
        {
            if (!_canBolt || _isBolted) return;

            _isBolted = true;
            if (_isOpen)
            {
                SetDoorOpen(false);
            }

            PlayClip(_boltSfx != null ? _boltSfx : _fallbackBoltSfx);
            OnSafeDoorBolted?.Invoke(this);
            Debug.Log("<color=yellow>[WoodDoorController] Safe room door BOLTED!</color>");
        }

        public void UnboltDoor()
        {
            if (!_canBolt || !_isBolted) return;

            _isBolted = false;
            PlayClip(_boltSfx != null ? _boltSfx : _fallbackBoltSfx);
            OnSafeDoorUnbolted?.Invoke(this);
            SetDoorOpen(true);
            Debug.Log("<color=yellow>[WoodDoorController] Safe room door UNBOLTED.</color>");
        }

        public void SetDoorOpen(bool open)
        {
            if (_isOpen == open) return;
            _isOpen = open;

            Vector3 targetEuler = _isOpen ? new Vector3(0f, _openAngle, 0f) : Vector3.zero;
            _targetRotation = _closedRotation * Quaternion.Euler(targetEuler);

            if (_animationRoutine != null)
            {
                StopCoroutine(_animationRoutine);
            }
            _animationRoutine = StartCoroutine(AnimateDoorRoutine());

            PlayClip(_isOpen ? (_openSfx != null ? _openSfx : _fallbackOpenSfx) : (_closeSfx != null ? _closeSfx : _fallbackCloseSfx));
            AttentionManager.Emit(transform.position, 6.0f, AttentionType.Door);
            UpdateObstacle();
            UpdateColliders();

            // Notify any listeners that this door has been opened
            if (_isOpen)
            {
                OnDoorOpened?.Invoke(this);
            }
        }

        private IEnumerator AnimateDoorRoutine()
        {
            if (_doorLeaf == null) yield break;

            Quaternion startRot = _doorLeaf.localRotation;
            float elapsed = 0f;

            while (elapsed < _animationDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _animationDuration);
                float smoothT = Mathf.SmoothStep(0f, 1f, t);

                _doorLeaf.localRotation = Quaternion.Slerp(startRot, _targetRotation, smoothT);
                yield return null;
            }

            _doorLeaf.localRotation = _targetRotation;
            _animationRoutine = null;
            UpdateColliders();
        }

        private void UpdateObstacle()
        {
            if (_navObstacle != null)
            {
                // Carve NavMesh when closed to block monster; uncarve when open so monster/player pass through
                _navObstacle.enabled = !_isOpen;
            }
        }

        private void PlayClip(AudioClip clip)
        {
            InitAudioFallbacks();
            AudioClip clipToPlay = clip != null ? clip : _fallbackLockedSfx;
            if (clipToPlay != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(clipToPlay, 1.0f);
            }
        }

        private static AudioClip _fallbackOpenSfx;
        private static AudioClip _fallbackCloseSfx;
        private static AudioClip _fallbackLockedSfx;
        private static AudioClip _fallbackBoltSfx;

        private static void InitAudioFallbacks()
        {
            if (_fallbackOpenSfx != null) return;

            int sampleRate = 44100;

            // Wooden door creak
            int creakSamples = (int)(sampleRate * 0.75f);
            _fallbackOpenSfx = AudioClip.Create("DoorOpenCreak", creakSamples, 1, sampleRate, false);
            float[] creakData = new float[creakSamples];
            for (int i = 0; i < creakSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Sin(Mathf.PI * (t / 0.75f));
                float creak = Mathf.Sin(2f * Mathf.PI * (160f + 90f * Mathf.Sin(35f * t)) * t);
                creakData[i] = creak * env * 0.6f;
            }
            _fallbackOpenSfx.SetData(creakData, 0);

            // Door close latch & thud
            int closeSamples = (int)(sampleRate * 0.45f);
            _fallbackCloseSfx = AudioClip.Create("DoorCloseThud", closeSamples, 1, sampleRate, false);
            float[] closeData = new float[closeSamples];
            for (int i = 0; i < closeSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 22f);
                float thud = Mathf.Sin(2f * Mathf.PI * 95f * t);
                float latch = (UnityEngine.Random.value * 2f - 1f) * Mathf.Exp(-t * 60f);
                closeData[i] = (thud * 0.7f + latch * 0.5f) * env;
            }
            _fallbackCloseSfx.SetData(closeData, 0);

            // Locked door rattle (metallic jiggle)
            int lockSamples = (int)(sampleRate * 0.35f);
            _fallbackLockedSfx = AudioClip.Create("DoorLockedRattle", lockSamples, 1, sampleRate, false);
            float[] lockData = new float[lockSamples];
            for (int i = 0; i < lockSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 28f);
                float rattle = Mathf.Sin(2f * Mathf.PI * 650f * t) * (UnityEngine.Random.value > 0.4f ? 1f : -1f);
                lockData[i] = rattle * env * 0.7f;
            }
            _fallbackLockedSfx.SetData(lockData, 0);

            // Deadbolt slide & lock
            int boltSamples = (int)(sampleRate * 0.5f);
            _fallbackBoltSfx = AudioClip.Create("DoorBoltSlide", boltSamples, 1, sampleRate, false);
            float[] boltData = new float[boltSamples];
            for (int i = 0; i < boltSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 14f);
                float slide = Mathf.Sin(2f * Mathf.PI * (420f - t * 150f) * t);
                boltData[i] = slide * env * 0.75f;
            }
            _fallbackBoltSfx.SetData(boltData, 0);
        }
    }
}

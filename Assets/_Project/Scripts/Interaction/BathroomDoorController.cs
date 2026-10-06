using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using LateSubmission.Attention;

namespace LateSubmission.Interaction
{
    /// <summary>
    /// Interactive controller for bathroom entrance and stall doors.
    /// The main bathroom entrance door (Unisex) opens/closes normally and cannot be bolted.
    /// The interior stall doors in the Women and Men sections can be bolted when the player is inside that stall.
    /// </summary>
    public class BathroomDoorController : MonoBehaviour, IInteractable
    {
        [Header("Door Configuration")]
        [SerializeField] private bool _canBolt = false;
        [SerializeField] private bool _isBolted = false;
        [SerializeField] private bool _isOpen = false;
        [SerializeField] private Animator _animator;
        [SerializeField] private Collider _doorCollider;

        [Header("Audio")]
        [SerializeField] private AudioClip _openSfx;
        [SerializeField] private AudioClip _closeSfx;
        [SerializeField] private AudioClip _boltSfx;
        [SerializeField] private AudioClip _unboltSfx;
        [SerializeField] private AudioSource _audioSource;

        [Header("Safe Room Settings")]
        [SerializeField] private Transform _insideCheckPoint;

        public static event Action<BathroomDoorController> OnBathroomBolted;
        public static event Action<BathroomDoorController> OnBathroomUnbolted;

        public bool IsBolted => _isBolted;
        public bool IsOpen => _isOpen;
        public bool CanBolt => _canBolt;

        private static int _lastPropColliderSceneHandle = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitializePropCollidersOnLoad()
        {
            EnsureScenePropCollidersEnabled();
        }

        private Transform GetFrameRoot()
        {
            return transform.parent != null ? transform.parent : transform;
        }

        public bool IsMainBathroomEntranceDoor()
        {
            Transform root = GetFrameRoot();
            if (root.name.IndexOf("Unisex", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            return root.position.z < 3.5f;
        }

        private void Awake()
        {
            EnsureScenePropCollidersEnabled();

            Transform frameRoot = GetFrameRoot();

            // Remove any duplicate overlapping bathroom door at the same position
            var allDoors = UnityEngine.Object.FindObjectsByType<BathroomDoorController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var other in allDoors)
            {
                if (other == null || other == this || !other.gameObject.activeInHierarchy) continue;
                Transform otherRoot = other.GetFrameRoot();
                if (otherRoot != frameRoot && Vector3.Distance(frameRoot.position, otherRoot.position) < 0.25f)
                {
                    frameRoot.gameObject.SetActive(false);
                    Destroy(frameRoot.gameObject);
                    return;
                }
            }

            // Main door for the bathroom cannot be bolted; interior doors in women and men sections can be bolted
            if (IsMainBathroomEntranceDoor())
            {
                _canBolt = false;
                _isBolted = false;
            }
            else
            {
                _canBolt = true;
            }

            if (_animator == null)
            {
                _animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>();
            }

            if (_doorCollider == null)
            {
                _doorCollider = GetComponent<BoxCollider>() ?? GetComponent<Collider>() ?? GetComponentInChildren<Collider>();
            }

            // Disable non-convex child MeshColliders (handle / restroom sign) on the animated door leaf
            // so PhysX never glitches when the Animator rotates the door and the player never snags inside stalls
            foreach (var mc in GetComponentsInChildren<MeshCollider>(true))
            {
                if (mc != null && mc != _doorCollider)
                {
                    mc.enabled = false;
                }
            }

            if (_audioSource == null)
            {
                _audioSource = GetComponent<AudioSource>();
                if (_audioSource == null)
                {
                    _audioSource = gameObject.AddComponent<AudioSource>();
                    _audioSource.spatialBlend = 1.0f;
                    _audioSource.playOnAwake = false;
                }
            }

            UpdateColliderState();
        }

        private void Start()
        {
            if (_animator != null && _isOpen)
            {
                PlayAnim(true);
            }
        }

        public string GetInteractionText()
        {
            if (!_canBolt || IsMainBathroomEntranceDoor())
            {
                return _isOpen ? "Close Bathroom Door [E]" : "Open Bathroom Door [E]";
            }

            if (_isBolted)
            {
                return "Unbolt & Open Stall Door [E]";
            }

            bool playerInsideStall = IsPlayerInside();
            if (playerInsideStall)
            {
                return _isOpen ? "Close & Bolt Stall Door [E]" : "Bolt Stall Door [E]";
            }

            return _isOpen ? "Close Stall Door [E]" : "Open Stall Door [E]";
        }

        public bool CanInteract(Interactor interactor)
        {
            return true;
        }

        public void Interact(Interactor interactor)
        {
            if (!_canBolt || IsMainBathroomEntranceDoor())
            {
                if (_isOpen)
                    CloseDoor();
                else
                    OpenDoor();
                return;
            }

            if (_isBolted)
            {
                UnboltDoor();
                OpenDoor();
                return;
            }

            bool playerInsideStall = IsPlayerInside();
            if (playerInsideStall)
            {
                if (_isOpen)
                {
                    CloseDoor();
                    BoltDoor();
                }
                else
                {
                    BoltDoor();
                }
                return;
            }

            // Player is outside the stall: simply open or close the stall door without bolting
            if (_isOpen)
            {
                CloseDoor();
            }
            else
            {
                OpenDoor();
            }
        }

        private void PlayAnim(bool open)
        {
            if (_animator == null) return;

            string primary = open ? "Opening 1" : "Closing 1";
            string fallback = open ? "OpeningStall" : "ClosingStall";

            if (_animator.HasState(0, Animator.StringToHash(primary)))
            {
                _animator.Play(primary, 0, 0f);
            }
            else if (_animator.HasState(0, Animator.StringToHash(fallback)))
            {
                _animator.Play(fallback, 0, 0f);
            }
        }

        public void OpenDoor()
        {
            if (_isOpen || _isBolted) return;

            _isOpen = true;
            PlayAnim(true);

            PlaySound(_openSfx);
            AttentionManager.Emit(transform.position, 5.0f, AttentionType.Door);
            UpdateColliderState();
        }

        public void CloseDoor()
        {
            if (!_isOpen) return;

            _isOpen = false;
            PlayAnim(false);

            PlaySound(_closeSfx);
            AttentionManager.Emit(transform.position, 4.0f, AttentionType.Door);
            UpdateColliderState();
        }

        public void BoltDoor()
        {
            if (!_canBolt || IsMainBathroomEntranceDoor() || _isBolted) return;

            if (_isOpen)
            {
                CloseDoor();
            }

            _isBolted = true;
            PlaySound(_boltSfx);
            OnBathroomBolted?.Invoke(this);
            Debug.Log($"<color=cyan>[BathroomDoor] Stall door '{GetFrameRoot().name}' BOLTED! Safe room engaged.</color>");
        }

        public void UnboltDoor()
        {
            if (!_isBolted) return;

            _isBolted = false;
            PlaySound(_unboltSfx);
            OnBathroomUnbolted?.Invoke(this);
            Debug.Log($"<color=yellow>[BathroomDoor] Stall door '{GetFrameRoot().name}' UNBOLTED.</color>");
        }

        private void Update()
        {
            if (IsMainBathroomEntranceDoor())
            {
                // Allow the monster to push open the unboltable main bathroom entrance door when entering/leaving the bathroom
                if (!_isOpen && AI.BossMonsterController.Instance != null &&
                    AI.BossMonsterController.Instance.CurrentState != AI.BossState.Dormant &&
                    AI.BossMonsterController.Instance.CurrentState != AI.BossState.Defeated)
                {
                    Vector3 mPos = AI.BossMonsterController.Instance.transform.position;
                    Vector3 dPos = transform.position;
                    float dist = Vector2.Distance(new Vector2(mPos.x, mPos.z), new Vector2(dPos.x, dPos.z));
                    if (dist <= 2.2f)
                    {
                        OpenDoor();
                    }
                }
                return;
            }

            if (!_canBolt) return;

            // If player stepped out of this stall, ensure the stall door does not remain bolted from the outside
            if (_isBolted && !IsPlayerInside())
            {
                UnboltDoor();
            }
        }

        /// <summary>
        /// Checks whether the player is currently standing inside THIS specific bathroom stall
        /// (either in the Women section on the East side or the Men section on the West side).
        /// </summary>
        public bool IsPlayerInside()
        {
            if (IsMainBathroomEntranceDoor()) return false;

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
            Transform frameRoot = GetFrameRoot();
            float stallCenterZ = frameRoot.position.z;
            float doorWorldX = transform.position.x;

            // Each stall is ~1.32m wide in Z centered at frameRoot.position.z (6.33, 7.65, 8.98, 10.27, 11.58)
            if (Mathf.Abs(pos.z - stallCenterZ) > 0.85f)
            {
                return false;
            }

            // Women section stalls are on the East side (frameRoot.x ~ -4.327, doorWorldX ~ -5.175, East wall at -2.0)
            if (frameRoot.position.x > -10.0f)
            {
                return pos.x > (doorWorldX - 0.08f) && pos.x <= -1.8f;
            }
            // Men section stalls are on the West side (frameRoot.x ~ -17.77, doorWorldX ~ -16.922, West wall at -20.0)
            else
            {
                return pos.x < (doorWorldX + 0.08f) && pos.x >= -20.2f;
            }
        }

        private void UpdateColliderState()
        {
            if (_doorCollider != null)
            {
                _doorCollider.enabled = true;
                _doorCollider.isTrigger = _isOpen;
            }
        }

        private void PlaySound(AudioClip clip)
        {
            if (clip != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(clip);
            }
        }

        /// <summary>
        /// Ensures all environment and lab props in the scene have enabled solid colliders.
        /// Automatically enables disabled prop colliders and adds MeshColliders/BoxColliders to any props missing them.
        /// </summary>
        public static void EnsureScenePropCollidersEnabled()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || _lastPropColliderSceneHandle == activeScene.handle) return;
            _lastPropColliderSceneHandle = activeScene.handle;

            var meshRenderers = UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var mr in meshRenderers)
            {
                if (mr == null || !mr.enabled || !mr.gameObject.activeInHierarchy) continue;

                GameObject go = mr.gameObject;
                string lowerName = go.name.ToLowerInvariant();
                string rootName = go.transform.root != null ? go.transform.root.name.ToLowerInvariant() : string.Empty;

                // Skip player, monster, cameras, UI, viewmodels, lights, triggers, pickups, and doors
                if (rootName.Contains("player") || rootName.Contains("boss") || rootName.Contains("monster") ||
                    rootName.Contains("entity") || rootName.Contains("canvas") || rootName.Contains("ui"))
                    continue;

                if (lowerName.Contains("light") || lowerName.Contains("fixture") || lowerName.Contains("lamp") ||
                    lowerName.Contains("bulb") || lowerName.Contains("sign") || lowerName.Contains("handle") ||
                    lowerName.Contains("latch") || lowerName.Contains("window") || lowerName.Contains("key") ||
                    lowerName.Contains("note") || lowerName.Contains("pistol") || lowerName.Contains("flashlight"))
                    continue;

                if (go.GetComponentInParent<IInteractable>() != null ||
                    go.GetComponentInParent<BathroomDoorController>() != null ||
                    go.GetComponentInParent<WoodDoorController>() != null ||
                    go.GetComponentInParent<ItemPickup>() != null ||
                    go.GetComponentInParent<InspectNote>() != null ||
                    go.GetComponentInParent<Weapon.MergedLightPistol>() != null ||
                    go.GetComponentInParent<Player.FPSController>() != null ||
                    go.GetComponentInParent<AI.BossMonsterController>() != null)
                    continue;

                Collider existingCol = go.GetComponent<Collider>();
                if (existingCol != null)
                {
                    if (!existingCol.enabled)
                    {
                        existingCol.enabled = true;
                    }
                    continue;
                }

                // If a parent already has a non-trigger collider covering this prop, skip
                Collider parentCol = go.GetComponentInParent<Collider>();
                if (parentCol != null && parentCol.enabled && !parentCol.isTrigger)
                {
                    continue;
                }

                MeshFilter mf = go.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    MeshCollider mc = go.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                    mc.enabled = true;
                }
                else
                {
                    BoxCollider bc = go.AddComponent<BoxCollider>();
                    bc.enabled = true;
                }
            }
        }
    }
}

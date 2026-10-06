using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using LateSubmission.Weapon;
using LateSubmission.Interaction;
using LateSubmission.Objectives;
using LateSubmission.Attention;

namespace LateSubmission.AI
{
    public enum BossState
    {
        Dormant,
        Intro,
        Stalk,
        Chase,
        Search,
        Staggered,
        DoorAttack,
        Roam,
        Defeated
    }

    /// <summary>
    /// Master AI Controller for Mutated Professor Anish Mondal (Monster Mutant 7).
    /// Features NavMesh-validated spawning in free space, multi-sensory perception (sight + sound hearing),
    /// safe room door assaults, photonic stun vulnerability, and dynamic roaming.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class BossMonsterController : MonoBehaviour, IStaggerable, IDamageable
    {
        [Header("Movement Speeds")]
        [SerializeField] private float _walkSpeed = 2.4f;
        [SerializeField] private float _chaseSpeed = 4.6f;
        [SerializeField] private float _searchSpeed = 2.8f;

        [Header("Health & Defeat")]
        [SerializeField] private float _maxHealth = 100.0f;
        [SerializeField] private bool _startChasingImmediately = false;
        public float CurrentHealth { get; private set; }
        public static event Action OnBossDefeated;

        [Header("Combat & Vulnerability")]
        [SerializeField] private int _maxStunsBeforeDefeat = 2;
        [SerializeField] private int _currentStunCount = 0;
        [SerializeField] private float _attackDistance = 3.0f;
        [SerializeField] private float _attackDamage = 15.0f;
        [SerializeField] private float _attackInterval = 1.3f;
        [SerializeField] private float _firstAttackDelay = 0.4f;
        [SerializeField] private float _attackWindupDelay = 0.3f;
        [SerializeField] private GameObject _keycardDropPrefab;

        private float _attackTimer = 0.4f;
        private float _chaseScreamTimer = 0f;
        private bool _isWindingUpAttack = false;
        private bool _hasTriggeredIntroOnce = false;

        [Header("Activation Area & Sensory Perception")]
        [Tooltip("Center of the area where the monster can activate upon seeing the player.")]
        [SerializeField] private Vector3 _activationAreaCenter = new Vector3(-31.5f, 1.0f, 6.0f);
        [Tooltip("Size of the customizable activation area covering Mechatronics Lab and its doorway approach.")]
        [SerializeField] private Vector3 _activationAreaSize = new Vector3(24.0f, 5.0f, 20.0f);
        [SerializeField] private float _sightDistance = 24.0f;
        [SerializeField] private float _fieldOfViewAngle = 125.0f;
        [SerializeField] private LayerMask _obstacleMask = ~0;

        [Header("Audio")]
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _roarIntroSfx;
        [SerializeField] private AudioClip _roarChaseSfx;
        [SerializeField] private AudioClip _attackSwingSfx;
        [SerializeField] private AudioClip _attackHitSfx;
        [SerializeField] private AudioClip _staggerScreechSfx;
        [SerializeField] private AudioClip _doorPoundSfx;
        [SerializeField] private AudioClip _heavyFootstepSfx;
        [SerializeField] private AudioClip _deathSfx;

        [Header("Waypoints for Corridor Roam")]
        [SerializeField] private Vector3[] _roamPatrolPoints = new Vector3[]
        {
            new Vector3(-31.5f, 0.05f, 2.2f), // Mechatronics doorway inside
            new Vector3(-31.5f, 0.05f, 0f),   // Mechatronics corridor entrance
            new Vector3(-19.5f, 0.05f, 0f),   // EC Lab entrance
            new Vector3(-12.0f, 0.05f, 0f),   // Lone light corridor
            new Vector3(-34.5f, 0.05f, 9.0f)  // Mechatronics inside aisle
        };

        private readonly Vector3[] _mechatronicsRoamPoints = new Vector3[]
        {
            new Vector3(-34.5f, 0.05f, 14.5f), // Spawn / upper lab center
            new Vector3(-39.0f, 0.05f, 14.0f), // West side of Mechatronics Lab
            new Vector3(-38.5f, 0.05f, 7.5f),  // Southwest lab area
            new Vector3(-32.4f, 0.05f, 4.2f),  // Near Mechatronics Lab entrance inside
            new Vector3(-30.5f, 0.05f, 7.5f),  // Southeast lab area near workbenches
            new Vector3(-30.0f, 0.05f, 14.0f)  // Northeast lab area
        };

        private readonly Vector3[] _bathroomRoamPoints = new Vector3[]
        {
            new Vector3(-7.0f, 0.05f, 6.5f),   // Women section lower aisle
            new Vector3(-7.0f, 0.05f, 10.5f),  // Women section upper aisle
            new Vector3(-9.9f, 0.05f, 3.6f),   // Bathroom foyer
            new Vector3(-14.0f, 0.05f, 6.5f),  // Men section lower aisle
            new Vector3(-14.0f, 0.05f, 10.5f)  // Men section upper aisle
        };

        public NavMeshAgent Agent { get; private set; }
        public Animator Animator { get; private set; }
        public BossState CurrentState { get; private set; } = BossState.Dormant;

        public static event Action OnBossChaseStarted;
        public static event Action OnBossChaseEnded;

        private enum RoomZone
        {
            Corridor,
            MechatronicsLab,
            ECLab,
            Bathroom
        }

        private Transform _playerTransform;
        private int _currentRoamIndex = 0;
        private int _bathroomRoamIndex = 0;
        private Coroutine _stateCoroutine;
        private float _footstepTimer = 0f;
        private Vector3 _lastKnownPlayerPos;
        private float _lostSightTimer = 0f;
        private bool _isPausingAtPatrolPoint = false;
        private bool _isPlayerInSafeRoom = false;
        private bool _isMoving = false;
        private bool _hasReachedMechatronicsDoor = false;
        private bool _returnedToSpawnAfterCooldown = false;
        private bool _wasMechatronicsDoorOpen = false;
        private float _postSafeRoomGraceTimer = 0f;
        private float _lastVocalPlayTime = -10f;
        private AudioSource _footstepAudioSource;
        private float _groundY = 0.05f;
        private Vector3 _initialSpawnPos = new Vector3(-34.5f, 0.05f, 14.5f);
        private Vector3 _currentLogicalPos;
        private bool _hasLogicalPos = false;
        private NavMeshPath _navPath;

        private WoodDoorController _mechatronicsDoor;
        private WoodDoorController _ecLabDoor;
        private BathroomDoorController _bathroomDoor;
        private BathroomDoorController[] _allBathroomDoors;
        private WoodDoorController _bathroomWoodDoor;
        private bool _collisionsIgnored = false;

        public static BossMonsterController Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            Agent = GetComponent<NavMeshAgent>();
            Animator = GetComponentInChildren<Animator>();
            _navPath = new NavMeshPath();

            EnsureNavMeshAgentConfigured();
            DisableAnimatorRootMotion();
            EnsureMonsterAudioSources();

            // Enforce 2 pistol shots to kill (100 HP, 50 dmg per shot, 2 stuns max), lower player damage (15 dmg), and attack cadence
            _maxHealth = 100.0f;
            CurrentHealth = _maxHealth;
            _maxStunsBeforeDefeat = 2;
            _currentStunCount = 0;
            if (_attackDamage > 15.0f) _attackDamage = 15.0f;
            _attackInterval = 1.3f;
            _firstAttackDelay = 0.4f;
            _attackWindupDelay = 0.3f;

            if (_obstacleMask.value == 0) _obstacleMask = ~0;

            BathroomDoorController.OnBathroomBolted += HandleBathroomBolted;
            BathroomDoorController.OnBathroomUnbolted += HandleBathroomUnbolted;
            WoodDoorController.OnSafeDoorBolted += HandleSafeDoorBolted;
            WoodDoorController.OnSafeDoorUnbolted += HandleSafeDoorUnbolted;
            AttentionManager.OnAttentionEmitted += HandleAttention;
        }

        private void EnsureMonsterAudioSources()
        {
            var sources = GetComponents<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = sources.Length > 0 ? sources[0] : gameObject.AddComponent<AudioSource>();
            }

            _audioSource.playOnAwake = false;
            _audioSource.loop = false;
            _audioSource.spatialBlend = 1.0f;
            _audioSource.rolloffMode = AudioRolloffMode.Linear;
            _audioSource.minDistance = 1.5f;
            _audioSource.maxDistance = 22.0f;
            _audioSource.volume = 0.32f;

            if (_footstepAudioSource == null)
            {
                if (sources.Length > 1 && sources[1] != _audioSource)
                {
                    _footstepAudioSource = sources[1];
                }
                else
                {
                    _footstepAudioSource = gameObject.AddComponent<AudioSource>();
                }
            }

            _footstepAudioSource.playOnAwake = false;
            _footstepAudioSource.loop = false;
            _footstepAudioSource.spatialBlend = 1.0f;
            _footstepAudioSource.rolloffMode = AudioRolloffMode.Linear;
            _footstepAudioSource.minDistance = 1.5f;
            _footstepAudioSource.maxDistance = 18.0f;
            _footstepAudioSource.volume = 0.25f;
        }

        private void OnDestroy()
        {
            BathroomDoorController.OnBathroomBolted -= HandleBathroomBolted;
            BathroomDoorController.OnBathroomUnbolted -= HandleBathroomUnbolted;
            WoodDoorController.OnSafeDoorBolted -= HandleSafeDoorBolted;
            WoodDoorController.OnSafeDoorUnbolted -= HandleSafeDoorUnbolted;
            AttentionManager.OnAttentionEmitted -= HandleAttention;
        }

        private void Start()
        {
            EnsurePlayerReference();
            CacheDoorReferences();
            IgnoreEnvironmentAndPlayerCollisions();
            EnsureNavMeshAgentConfigured();
            DisableAnimatorRootMotion();
            EnsureMonsterAudioSources();

            if (transform.position.sqrMagnitude > 1f)
            {
                _initialSpawnPos = new Vector3(transform.position.x, _groundY, transform.position.z);
            }
            SetLogicalPosition(_initialSpawnPos, true);

            if (CurrentState == BossState.Dormant)
            {
                _isMoving = false;
                PlayAnim("idle1");

                if (_startChasingImmediately)
                {
                    StartCoroutine(AutoChaseRoutine());
                }
            }
        }

        private void EnsureNavMeshAgentConfigured()
        {
            if (Agent == null) Agent = GetComponent<NavMeshAgent>();
            if (Agent == null) return;

            Agent.radius = 0.25f;
            Agent.height = 1.8f;
            Agent.baseOffset = 0f;
            Agent.acceleration = 28.0f;
            Agent.angularSpeed = 540.0f;
            Agent.stoppingDistance = 0.15f;
            Agent.autoBraking = false;
            Agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
            Agent.updatePosition = false;
            Agent.updateRotation = false;

            if (!Agent.enabled)
            {
                Agent.enabled = true;
            }
        }

        private void DisableAnimatorRootMotion()
        {
            if (Animator == null) Animator = GetComponentInChildren<Animator>();
            if (Animator != null)
            {
                Animator.applyRootMotion = false;
                if (Animator.transform != transform)
                {
                    Animator.transform.localPosition = Vector3.zero;
                    Animator.transform.localRotation = Quaternion.identity;
                }
            }
            foreach (var anim in GetComponentsInChildren<Animator>(true))
            {
                if (anim != null) anim.applyRootMotion = false;
            }
        }

        // Empty OnAnimatorMove prevents Unity's Animator from applying built-in root motion to this GameObject
        private void OnAnimatorMove()
        {
        }

        private void LateUpdate()
        {
            DisableAnimatorRootMotion();
            if (_hasLogicalPos && CurrentState != BossState.Defeated)
            {
                transform.position = _currentLogicalPos;
                if (Agent != null && Agent.enabled)
                {
                    Agent.nextPosition = _currentLogicalPos;
                }
            }
        }

        private void SetLogicalPosition(Vector3 pos, bool warpAgent = false)
        {
            _currentLogicalPos = new Vector3(pos.x, _groundY, pos.z);
            _hasLogicalPos = true;
            transform.position = _currentLogicalPos;

            if (Agent != null && CurrentState != BossState.Defeated)
            {
                EnsureNavMeshAgentConfigured();
                if (Agent.enabled)
                {
                    if (warpAgent || !Agent.isOnNavMesh || Vector3.Distance(Agent.nextPosition, _currentLogicalPos) > 1.2f)
                    {
                        if (NavMesh.SamplePosition(_currentLogicalPos, out NavMeshHit hit, 3.0f, NavMesh.AllAreas))
                        {
                            Agent.Warp(hit.position);
                        }
                    }
                    Agent.nextPosition = _currentLogicalPos;
                }
            }
        }

        private void EnsurePlayerReference()
        {
            if (_playerTransform == null)
            {
                var player = GameObject.FindWithTag("Player");
                if (player != null) _playerTransform = player.transform;
                else
                {
                    var fps = UnityEngine.Object.FindFirstObjectByType<Player.FPSController>();
                    if (fps != null) _playerTransform = fps.transform;
                }
            }
        }

        private void CacheDoorReferences()
        {
            var woodDoors = UnityEngine.Object.FindObjectsByType<WoodDoorController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var wd in woodDoors)
            {
                if (wd == null) continue;
                string n = wd.gameObject.name.ToLower();

                if (n.Contains("mechatronics"))
                {
                    _mechatronicsDoor = wd;
                }
                else if (n.Contains("eclab") || n.Contains("ec_lab") || n.Contains("electrical"))
                {
                    _ecLabDoor = wd;
                }
                else if (n.Contains("bathroom") || n.Contains("restroom") || n.Contains("toilet") || wd.IsBolted)
                {
                    _bathroomWoodDoor = wd;
                }
            }

            _allBathroomDoors = UnityEngine.Object.FindObjectsByType<BathroomDoorController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var bd in _allBathroomDoors)
            {
                if (bd == null || !bd.enabled) continue;
                if (bd.IsMainBathroomEntranceDoor())
                {
                    _bathroomDoor = bd;
                    break;
                }
            }
        }

        private void IgnoreEnvironmentAndPlayerCollisions()
        {
            if (_collisionsIgnored) return;

            var myColliders = GetComponentsInChildren<Collider>(true);
            if (myColliders == null || myColliders.Length == 0) return;

            var allColliders = UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var myCol in myColliders)
            {
                if (myCol == null) continue;
                myCol.isTrigger = false;
                foreach (var otherCol in allColliders)
                {
                    if (otherCol == null || otherCol == myCol || otherCol.transform.IsChildOf(transform)) continue;
                    Physics.IgnoreCollision(myCol, otherCol, true);
                }
            }

            _collisionsIgnored = true;
        }

        private bool IsMechatronicsDoorOpen =>
            CurrentState != BossState.Dormant || _mechatronicsDoor == null || _mechatronicsDoor.IsOpen;

        private bool IsECLabDoorOpen =>
            _ecLabDoor == null || _ecLabDoor.IsOpen || (_playerTransform != null && GetRoomZone(_playerTransform.position) == RoomZone.ECLab);

        private bool IsPlayerActuallyInBathroom()
        {
            EnsurePlayerReference();
            if (_playerTransform == null) return false;
            Vector3 p = _playerTransform.position;
            // Full Bathroom bounds including both Women stalls (East, X up to -2.0) and Men stalls (West, X down to -20.0)
            return p.z >= 2.0f && p.z <= 14.8f && p.x >= -20.2f && p.x <= -1.8f;
        }

        private bool IsAnyBathroomStallBolted()
        {
            if (_allBathroomDoors == null || _allBathroomDoors.Length == 0)
            {
                _allBathroomDoors = UnityEngine.Object.FindObjectsByType<BathroomDoorController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            }

            if (_allBathroomDoors != null)
            {
                foreach (var bd in _allBathroomDoors)
                {
                    if (bd != null && bd.gameObject.activeInHierarchy && bd.IsBolted)
                    {
                        return true;
                    }
                }
            }

            return _bathroomWoodDoor != null && _bathroomWoodDoor.IsBolted;
        }

        private bool IsPlayerInsideAnyClosedBathroomStall()
        {
            if (!IsPlayerActuallyInBathroom()) return false;

            if (_allBathroomDoors == null || _allBathroomDoors.Length == 0)
            {
                _allBathroomDoors = UnityEngine.Object.FindObjectsByType<BathroomDoorController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            }

            if (_allBathroomDoors != null)
            {
                foreach (var bd in _allBathroomDoors)
                {
                    if (bd == null || !bd.gameObject.activeInHierarchy || bd.IsMainBathroomEntranceDoor()) continue;
                    if ((!bd.IsOpen || bd.IsBolted) && bd.IsPlayerInside())
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool IsBathroomDoorBolted =>
            IsPlayerActuallyInBathroom() && (_isPlayerInSafeRoom || IsAnyBathroomStallBolted());

        private bool IsBathroomDoorOpen => true;

        private RoomZone GetRoomZone(Vector3 pos)
        {
            if (pos.z > 1.85f && pos.x < -23.0f) return RoomZone.MechatronicsLab;
            if (pos.z >= 2.0f && pos.z <= 14.8f && pos.x >= -20.2f && pos.x <= -1.8f) return RoomZone.Bathroom;
            if (pos.z < -1.85f) return RoomZone.ECLab;
            return RoomZone.Corridor;
        }

        private bool CanCrossBetweenZones(RoomZone fromZone, RoomZone toZone)
        {
            return true;
        }

        public bool IsPlayerInActivationArea()
        {
            EnsurePlayerReference();
            if (_playerTransform == null) return false;

            Bounds activationBounds = new Bounds(_activationAreaCenter, _activationAreaSize);
            Vector3 p = _playerTransform.position;
            if (activationBounds.Contains(new Vector3(p.x, _activationAreaCenter.y, p.z)))
                return true;

            // Also treat the corridor and lab within sight distance as valid once the monster is approaching the door
            return Vector3.Distance(transform.position, p) <= _sightDistance;
        }

        /// <summary>
        /// Computes the next pursuit target so the monster passes directly through the doors
        /// and the walls surrounding the doors of Mechatronics Lab, EC Lab, and Bathroom when open,
        /// without ever getting stuck on doorway colliders or NavMesh boundaries.
        /// </summary>
        private Vector3 GetNextPursuitPoint(Vector3 currentPos, Vector3 targetPos)
        {
            float mechaX = _mechatronicsDoor != null ? _mechatronicsDoor.transform.position.x : -31.5f;
            float ecX = _ecLabDoor != null ? _ecLabDoor.transform.position.x : -19.5f;
            float bathX = _bathroomDoor != null
                ? _bathroomDoor.transform.position.x
                : (_bathroomWoodDoor != null ? _bathroomWoodDoor.transform.position.x : -9.87f);

            RoomZone currentZone = GetRoomZone(currentPos);
            RoomZone targetZone = GetRoomZone(targetPos);

            // Keep monster outside closed/bolted bathroom stalls in the bathroom aisles
            if (targetZone == RoomZone.Bathroom && (IsBathroomDoorBolted || IsPlayerInsideAnyClosedBathroomStall()))
            {
                targetPos.x = Mathf.Clamp(targetPos.x, -15.8f, -6.0f);
            }

            // Same zone: move straight to target
            if (currentZone == targetZone)
            {
                return new Vector3(targetPos.x, _groundY, targetPos.z);
            }

            // 1. Currently in Mechatronics Lab, target is outside
            if (currentZone == RoomZone.MechatronicsLab)
            {
                // Pass right through the walls surrounding the Mechatronics Lab door when near the door X range
                if (Mathf.Abs(currentPos.x - mechaX) <= 7.5f && targetZone == RoomZone.Corridor && Mathf.Abs(targetPos.x - mechaX) <= 7.5f)
                {
                    return new Vector3(targetPos.x, _groundY, targetPos.z);
                }

                return new Vector3(mechaX, _groundY, 0.0f);
            }

            // 2. Currently in EC Lab, target is outside
            if (currentZone == RoomZone.ECLab)
            {
                if (Mathf.Abs(currentPos.x - ecX) <= 7.5f && targetZone == RoomZone.Corridor && Mathf.Abs(targetPos.x - ecX) <= 7.5f)
                {
                    return new Vector3(targetPos.x, _groundY, targetPos.z);
                }

                return new Vector3(ecX, _groundY, 0.0f);
            }

            // 3. Currently in Bathroom, target is outside
            if (currentZone == RoomZone.Bathroom)
            {
                if (Mathf.Abs(currentPos.x - bathX) <= 7.5f && targetZone == RoomZone.Corridor && Mathf.Abs(targetPos.x - bathX) <= 7.5f)
                {
                    return new Vector3(targetPos.x, _groundY, targetPos.z);
                }

                return new Vector3(bathX, _groundY, 0.0f);
            }

            // 4. Currently in Corridor, target is inside one of the rooms
            if (targetZone == RoomZone.MechatronicsLab)
            {
                if (Mathf.Abs(currentPos.x - mechaX) <= 7.5f)
                {
                    return new Vector3(targetPos.x, _groundY, targetPos.z);
                }

                return new Vector3(mechaX, _groundY, 0.0f);
            }

            if (targetZone == RoomZone.ECLab)
            {
                if (!IsECLabDoorOpen)
                {
                    return new Vector3(ecX, _groundY, -0.8f);
                }

                if (Mathf.Abs(currentPos.x - ecX) <= 7.5f)
                {
                    return new Vector3(targetPos.x, _groundY, targetPos.z);
                }

                return new Vector3(ecX, _groundY, 0.0f);
            }

            if (targetZone == RoomZone.Bathroom)
            {
                if (Mathf.Abs(currentPos.x - bathX) <= 7.5f)
                {
                    return new Vector3(targetPos.x, _groundY, targetPos.z);
                }

                return new Vector3(bathX, _groundY, 0.0f);
            }

            return new Vector3(targetPos.x, _groundY, targetPos.z);
        }

        private bool IsCrossingOpenDoorwayWall(Vector3 currentPos, Vector3 targetPos)
        {
            RoomZone currentZone = GetRoomZone(currentPos);
            RoomZone targetZone = GetRoomZone(targetPos);
            if (currentZone == targetZone) return false;

            float mechaX = _mechatronicsDoor != null ? _mechatronicsDoor.transform.position.x : -31.5f;
            float ecX = _ecLabDoor != null ? _ecLabDoor.transform.position.x : -19.5f;
            float bathX = _bathroomDoor != null
                ? _bathroomDoor.transform.position.x
                : (_bathroomWoodDoor != null ? _bathroomWoodDoor.transform.position.x : -9.87f);

            // Crossing Mechatronics Lab doorway / surrounding walls (open)
            if (IsMechatronicsDoorOpen &&
                (currentZone == RoomZone.MechatronicsLab || targetZone == RoomZone.MechatronicsLab) &&
                Mathf.Abs(currentPos.x - mechaX) <= 7.5f &&
                Mathf.Abs(currentPos.z - 2.0f) <= 2.6f)
            {
                return true;
            }

            // Crossing EC Lab doorway / surrounding walls (open)
            if (IsECLabDoorOpen &&
                (currentZone == RoomZone.ECLab || targetZone == RoomZone.ECLab) &&
                Mathf.Abs(currentPos.x - ecX) <= 7.5f &&
                Mathf.Abs(currentPos.z - (-2.0f)) <= 2.6f)
            {
                return true;
            }

            // Crossing Bathroom doorway / surrounding walls (when unbolted & open)
            if (IsBathroomDoorOpen &&
                (currentZone == RoomZone.Bathroom || targetZone == RoomZone.Bathroom) &&
                Mathf.Abs(currentPos.x - bathX) <= 7.5f &&
                Mathf.Abs(currentPos.z - 2.0f) <= 2.6f)
            {
                return true;
            }

            return false;
        }

        private void MoveMonsterTowards(Vector3 rawDestination, float speed)
        {
            EnsureNavMeshAgentConfigured();
            DisableAnimatorRootMotion();

            Vector3 currentPos = _hasLogicalPos
                ? _currentLogicalPos
                : new Vector3(transform.position.x, _groundY, transform.position.z);

            Vector3 zonePursuitTarget = GetNextPursuitPoint(currentPos, rawDestination);
            Vector3 steerPoint = zonePursuitTarget;

            // Primary pathfinding on baked NavMeshSurface, with doorway/wall pass-through fallback when crossing open doors
            if (!IsCrossingOpenDoorwayWall(currentPos, rawDestination))
            {
                if (_navPath == null) _navPath = new NavMeshPath();

                if (NavMesh.SamplePosition(currentPos, out NavMeshHit startHit, 2.5f, NavMesh.AllAreas) &&
                    NavMesh.SamplePosition(zonePursuitTarget, out NavMeshHit endHit, 2.5f, NavMesh.AllAreas))
                {
                    if (Agent != null && Agent.enabled && Agent.isOnNavMesh)
                    {
                        Agent.speed = speed;
                        Agent.isStopped = false;
                        Agent.SetDestination(endHit.position);
                    }

                    if (NavMesh.CalculatePath(startHit.position, endHit.position, NavMesh.AllAreas, _navPath) &&
                        _navPath.status == NavMeshPathStatus.PathComplete &&
                        _navPath.corners != null &&
                        _navPath.corners.Length >= 2)
                    {
                        for (int i = 1; i < _navPath.corners.Length; i++)
                        {
                            Vector3 corner = new Vector3(_navPath.corners[i].x, _groundY, _navPath.corners[i].z);
                            float distToCorner = Vector3.Distance(
                                new Vector3(currentPos.x, 0f, currentPos.z),
                                new Vector3(corner.x, 0f, corner.z));

                            if (distToCorner > 0.25f || i == _navPath.corners.Length - 1)
                            {
                                steerPoint = corner;
                                break;
                            }
                        }
                    }
                }
            }

            Vector3 toSteer = steerPoint - currentPos;
            toSteer.y = 0f;

            if (toSteer.sqrMagnitude > 0.0004f)
            {
                _isMoving = true;
                Quaternion targetRot = Quaternion.LookRotation(toSteer.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 12f);
                Vector3 nextPos = Vector3.MoveTowards(currentPos, steerPoint, speed * Time.deltaTime);
                SetLogicalPosition(nextPos);
            }
            else
            {
                _isMoving = false;
            }
        }

        private IEnumerator AutoChaseRoutine()
        {
            yield return new WaitForSeconds(0.6f);
            if (CurrentState == BossState.Dormant)
            {
                Debug.Log("<color=red>[BossMonster] AutoChaseRoutine triggered! Monster actively pursues player!</color>");
                StartChase();
            }
        }

        private void Update()
        {
            if (CurrentState == BossState.Defeated) return;

            EnsurePlayerReference();
            DisableAnimatorRootMotion();

            if (_postSafeRoomGraceTimer > 0f)
            {
                _postSafeRoomGraceTimer -= Time.deltaTime;
            }

            bool isMechaOpenNow = _mechatronicsDoor != null && _mechatronicsDoor.IsOpen;
            bool mechaJustOpened = isMechaOpenNow && !_wasMechatronicsDoorOpen;
            _wasMechatronicsDoorOpen = isMechaOpenNow;

            // If player stepped out of the bathroom or unbolted the stall door, clear _isPlayerInSafeRoom
            if (_isPlayerInSafeRoom && (!IsPlayerActuallyInBathroom() || !IsAnyBathroomStallBolted()))
            {
                _isPlayerInSafeRoom = false;
            }

            // Check if player just bolted a stall door inside the bathroom while out of sight
            if (!_isPlayerInSafeRoom && IsBathroomDoorBolted && !CanSeePlayer())
            {
                HandleBathroomBolted(_bathroomDoor);
                return;
            }

            if (CurrentState == BossState.Dormant)
            {
                // When we open the Mechatronics Lab door, the monster starts roaming in the Mechatronics Lab
                if (isMechaOpenNow)
                {
                    StartApproachingMechatronicsDoor(_initialSpawnPos);
                    return;
                }

                // Or if monster sees the player on sight
                if (_playerTransform != null && CanSeePlayer())
                {
                    ActivateAndChaseOnSight();
                }
                return;
            }

            // If Mechatronics Lab door was just opened while monster is not already chasing/in cooldown, ensure it roams in Mechatronics Lab
            if (mechaJustOpened && CurrentState != BossState.Chase && CurrentState != BossState.DoorAttack && CurrentState != BossState.Staggered)
            {
                StartRoam();
            }

            UpdateFootstepAudio();

            switch (CurrentState)
            {
                case BossState.Stalk:
                    UpdateStalk();
                    break;
                case BossState.Chase:
                    UpdateChase();
                    break;
                case BossState.Roam:
                    if (!_isPlayerInSafeRoom && !IsBathroomDoorBolted && CanSeePlayer())
                    {
                        _returnedToSpawnAfterCooldown = false;
                        ActivateAndChaseOnSight();
                        return;
                    }
                    UpdateRoam();
                    break;
                case BossState.Search:
                    if (!_isPlayerInSafeRoom && !IsBathroomDoorBolted && CanSeePlayer())
                    {
                        _returnedToSpawnAfterCooldown = false;
                        ActivateAndChaseOnSight();
                        return;
                    }
                    break;
            }
        }

        /// <summary>
        /// Called when the Mechatronics Lab door is opened.
        /// The monster roams inside the Mechatronics Lab and attacks the player on sight.
        /// </summary>
        public void StartApproachingMechatronicsDoor(Vector3 spawnPosition)
        {
            gameObject.SetActive(true);
            EnsurePlayerReference();
            CacheDoorReferences();
            IgnoreEnvironmentAndPlayerCollisions();
            EnsureNavMeshAgentConfigured();
            DisableAnimatorRootMotion();

            if (CurrentState == BossState.Dormant)
            {
                _groundY = 0.05f;
                _initialSpawnPos = new Vector3(spawnPosition.x, _groundY, spawnPosition.z);
                SetLogicalPosition(_initialSpawnPos, true);
            }

            if (CurrentState == BossState.Chase || CurrentState == BossState.Defeated) return;

            _returnedToSpawnAfterCooldown = false;
            _hasReachedMechatronicsDoor = false;
            _currentRoamIndex = 1;
            StartRoam();
            Debug.Log("<color=yellow>[BossMonster] Mechatronics Lab door opened! Monster is now roaming in the Mechatronics Lab and will attack on sight.</color>");
        }

        private void UpdateStalk()
        {
            if (CanSeePlayer())
            {
                ActivateAndChaseOnSight();
                return;
            }

            StartRoam();
        }

        private void ActivateAndChaseOnSight()
        {
            if (CurrentState == BossState.Chase || CurrentState == BossState.Defeated) return;

            if (!_hasTriggeredIntroOnce)
            {
                _hasTriggeredIntroOnce = true;
                Debug.Log("<color=red>[BossMonster] Monster spotted the player! Activating chase & depleting flashlight!</color>");

                var spawnTrigger = UnityEngine.Object.FindFirstObjectByType<BossSpawnTrigger>();
                if (spawnTrigger != null)
                {
                    spawnTrigger.DepletePlayerFlashlight();
                }

                PlayRoarIntroSound();
            }

            StartChase();
        }

        public void TriggerSpawn(Vector3 spawnPosition)
        {
            gameObject.SetActive(true);
            EnsurePlayerReference();
            CacheDoorReferences();
            IgnoreEnvironmentAndPlayerCollisions();
            EnsureNavMeshAgentConfigured();
            DisableAnimatorRootMotion();

            _groundY = 0.05f;
            _initialSpawnPos = new Vector3(spawnPosition.x, _groundY, spawnPosition.z);

            SetLogicalPosition(_initialSpawnPos, true);
            _isMoving = false;

            ActivateAndChaseOnSight();
        }

        public void StartChase()
        {
            if (CurrentState == BossState.Defeated || CurrentState == BossState.Staggered || CurrentState == BossState.DoorAttack) return;
            if (_isPlayerInSafeRoom || IsBathroomDoorBolted) return;

            CurrentState = BossState.Chase;
            _returnedToSpawnAfterCooldown = false;
            _lostSightTimer = 0f;
            _attackTimer = _firstAttackDelay;
            _isWindingUpAttack = false;
            _chaseScreamTimer = UnityEngine.Random.Range(9.0f, 14.0f);

            EnsurePlayerReference();
            CacheDoorReferences();
            IgnoreEnvironmentAndPlayerCollisions();
            EnsureNavMeshAgentConfigured();
            DisableAnimatorRootMotion();

            PlayAnim("run1");
            if (Time.time - _lastVocalPlayTime >= 2.5f)
            {
                PlayChaseScream();
            }
            OnBossChaseStarted?.Invoke();
            Debug.Log("<color=red>[BossMonster] Mutated Prof. Anish Mondal started chasing player!</color>");
        }

        private void UpdateChase()
        {
            EnsurePlayerReference();
            if (_playerTransform == null) return;

            // If player bolted the stall door inside the bathroom and monster cannot see the player,
            // monster cannot hit and enters the 5s bathroom roam cooldown
            if ((_isPlayerInSafeRoom || IsBathroomDoorBolted) && !CanSeePlayer())
            {
                _isWindingUpAttack = false;
                TriggerDoorAssault(transform.position);
                return;
            }

            // Check if player is dead
            var playerHealth = _playerTransform.GetComponent<Player.PlayerHealth>()
                            ?? _playerTransform.GetComponentInParent<Player.PlayerHealth>()
                            ?? _playerTransform.GetComponentInChildren<Player.PlayerHealth>()
                            ?? Player.PlayerHealth.Instance;
            if (playerHealth != null && playerHealth.IsDead)
            {
                StartRoam();
                return;
            }

            // Periodic hunting growl during chase (well-spaced so it isn't repetitive or loud)
            _chaseScreamTimer -= Time.deltaTime;
            if (_chaseScreamTimer <= 0f)
            {
                _chaseScreamTimer = UnityEngine.Random.Range(11.0f, 16.0f);
                PlayChaseScream();
            }

            RoomZone myZone = GetRoomZone(transform.position);
            RoomZone playerZone = GetRoomZone(_playerTransform.position);
            bool canCross = CanCrossBetweenZones(myZone, playerZone);
            bool playerHiddenInStall = IsBathroomDoorBolted || IsPlayerInsideAnyClosedBathroomStall();

            float dist = Vector3.Distance(
                new Vector3(transform.position.x, 0f, transform.position.z),
                new Vector3(_playerTransform.position.x, 0f, _playerTransform.position.z));

            if (_isWindingUpAttack)
            {
                if (playerHiddenInStall)
                {
                    _isWindingUpAttack = false;
                    return;
                }

                _isMoving = false;
                Vector3 toPlayer = _playerTransform.position - transform.position;
                toPlayer.y = 0f;
                if (toPlayer.sqrMagnitude > 0.01f)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(toPlayer), Time.deltaTime * 10f);
                }
                return;
            }

            // Within melee attack range and not blocked by a closed/bolted bathroom stall door
            if (dist <= _attackDistance && canCross && !playerHiddenInStall)
            {
                Vector3 toPlayer = _playerTransform.position - transform.position;
                toPlayer.y = 0f;
                if (toPlayer.sqrMagnitude > 0.01f)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(toPlayer), Time.deltaTime * 14f);
                }

                _attackTimer -= Time.deltaTime;

                if (_attackTimer <= 0f)
                {
                    StartCoroutine(DelayedAttackSwingRoutine(playerHealth));
                }
                else if (dist > 1.8f)
                {
                    PlayAnim("walk1");
                    MoveMonsterTowards(_playerTransform.position, _walkSpeed);
                }
                else
                {
                    _isMoving = false;
                    PlayAnim("idle1");
                }
            }
            else
            {
                _attackTimer = Mathf.Max(_attackTimer - Time.deltaTime, _firstAttackDelay);
                PlayAnim("run1");
                MoveMonsterTowards(_playerTransform.position, _chaseSpeed);
            }
        }

        private IEnumerator DelayedAttackSwingRoutine(Player.PlayerHealth playerHealth)
        {
            _isWindingUpAttack = true;
            _isMoving = false;
            _attackTimer = _attackInterval;

            PlayAnim(UnityEngine.Random.value > 0.5f ? "attack1" : "attack2");
            InitMonsterAudio();
            EnsureMonsterAudioSources();
            if (_audioSource != null)
            {
                AudioClip swingClip = _attackSwingSfx != null ? _attackSwingSfx : _fallbackAttackSwing;
                if (swingClip != null)
                {
                    _audioSource.pitch = UnityEngine.Random.Range(0.92f, 1.06f);
                    _audioSource.PlayOneShot(swingClip, 0.26f);
                }
            }

            // Fast wind-up delay before hit connects
            yield return new WaitForSeconds(_attackWindupDelay);

            if (CurrentState == BossState.Chase &&
                !_isPlayerInSafeRoom &&
                !IsBathroomDoorBolted &&
                !IsPlayerInsideAnyClosedBathroomStall() &&
                _playerTransform != null)
            {
                float hitDist = Vector3.Distance(
                    new Vector3(transform.position.x, 0f, transform.position.z),
                    new Vector3(_playerTransform.position.x, 0f, _playerTransform.position.z));

                if (hitDist <= _attackDistance + 0.6f && playerHealth != null && !playerHealth.IsDead)
                {
                    if (_audioSource != null)
                    {
                        AudioClip hitClip = _attackHitSfx != null ? _attackHitSfx : _fallbackAttackHit;
                        if (hitClip != null) _audioSource.PlayOneShot(hitClip, 0.30f);
                    }
                    playerHealth.TakeDamage(_attackDamage, transform.position);
                    Debug.Log($"<color=red>[BossMonster] ATTACK HIT PLAYER! Dealt {_attackDamage} damage. Remaining HP: {playerHealth.CurrentHealth}</color>");
                }
            }

            _isWindingUpAttack = false;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0f, 1f, 1f, 0.35f);
            Gizmos.DrawWireCube(_activationAreaCenter, _activationAreaSize);
        }

        private void StartSearch()
        {
            CurrentState = BossState.Search;
            OnBossChaseEnded?.Invoke();

            PlayAnim("walk1");
            if (_stateCoroutine != null) StopCoroutine(_stateCoroutine);
            _stateCoroutine = StartCoroutine(SearchRoutine());
        }

        private IEnumerator SearchRoutine()
        {
            float searchTime = 6.0f;
            while (searchTime > 0f)
            {
                searchTime -= Time.deltaTime;

                if (CanSeePlayer())
                {
                    StartChase();
                    yield break;
                }

                MoveMonsterTowards(_lastKnownPlayerPos, _searchSpeed);
                yield return null;
            }

            StartRoam();
        }

        public void StartRoam()
        {
            CurrentState = BossState.Roam;
            OnBossChaseEnded?.Invoke();
            _isPausingAtPatrolPoint = false;
            PlayAnim("walk1");
        }

        private void UpdateRoam()
        {
            // While roaming in the Mechatronics Lab, attack the player on sight!
            if (!_isPlayerInSafeRoom && !IsBathroomDoorBolted && CanSeePlayer())
            {
                _returnedToSpawnAfterCooldown = false;
                ActivateAndChaseOnSight();
                return;
            }

            Vector3[] activeWaypoints = (_mechatronicsRoamPoints != null && _mechatronicsRoamPoints.Length > 0)
                ? _mechatronicsRoamPoints
                : _roamPatrolPoints;

            if (_isPausingAtPatrolPoint || activeWaypoints == null || activeWaypoints.Length == 0) return;

            if (_currentRoamIndex < 0 || _currentRoamIndex >= activeWaypoints.Length)
            {
                _currentRoamIndex = 0;
            }

            Vector3 targetWaypoint = activeWaypoints[_currentRoamIndex];
            float dist = Vector3.Distance(
                new Vector3(transform.position.x, 0f, transform.position.z),
                new Vector3(targetWaypoint.x, 0f, targetWaypoint.z));

            if (dist < 1.0f)
            {
                StartCoroutine(PatrolPauseRoutine());
            }
            else
            {
                PlayAnim("walk1");
                MoveMonsterTowards(targetWaypoint, _walkSpeed);
            }
        }

        private IEnumerator PatrolPauseRoutine()
        {
            _isPausingAtPatrolPoint = true;
            _isMoving = false;

            PlayAnim("idle1");
            yield return new WaitForSeconds(UnityEngine.Random.Range(0.5f, 0.9f));

            int count = (_mechatronicsRoamPoints != null && _mechatronicsRoamPoints.Length > 0)
                ? _mechatronicsRoamPoints.Length
                : (_roamPatrolPoints != null && _roamPatrolPoints.Length > 0 ? _roamPatrolPoints.Length : 1);

            _currentRoamIndex = (_currentRoamIndex + 1) % count;
            PlayAnim("walk1");
            _isPausingAtPatrolPoint = false;
        }

        private bool CanSeePlayer()
        {
            if (_isPlayerInSafeRoom || IsBathroomDoorBolted || IsPlayerInsideAnyClosedBathroomStall()) return false;

            EnsurePlayerReference();
            if (_playerTransform == null) return false;

            Vector3 monsterPos = transform.position;
            Vector3 playerPos = _playerTransform.position;
            float dist = Vector3.Distance(monsterPos, playerPos);

            if (dist > _sightDistance) return false;

            RoomZone myZone = GetRoomZone(monsterPos);
            RoomZone playerZone = GetRoomZone(playerPos);

            // Cannot see into/out of a room whose door is closed or bolted
            if (!CanCrossBetweenZones(myZone, playerZone)) return false;

            if ((myZone == RoomZone.MechatronicsLab || playerZone == RoomZone.MechatronicsLab) &&
                _mechatronicsDoor != null && !_mechatronicsDoor.IsOpen)
            {
                return false;
            }

            if ((myZone == RoomZone.ECLab || playerZone == RoomZone.ECLab) &&
                _ecLabDoor != null && !_ecLabDoor.IsOpen)
            {
                return false;
            }

            if ((myZone == RoomZone.Bathroom || playerZone == RoomZone.Bathroom) &&
                _bathroomDoor != null && !_bathroomDoor.IsOpen)
            {
                return false;
            }

            Vector3 headTarget = playerPos + Vector3.up * 1.6f;
            Vector3 chestTarget = playerPos + Vector3.up * 1.1f;
            Vector3 waistTarget = playerPos + Vector3.up * 0.45f;

            // If monster and player are both in the Mechatronics Lab with clear line of sight, detect player immediately
            if (myZone == RoomZone.MechatronicsLab && playerZone == RoomZone.MechatronicsLab && dist <= 18.0f)
            {
                if (HasClearLineOfSight(chestTarget) || HasClearLineOfSight(headTarget))
                {
                    return true;
                }
            }

            // Direction from monster eye height towards player
            Vector3 toPlayer = (playerPos - monsterPos);
            toPlayer.y = 0f;
            Vector3 dirToPlayer = toPlayer.normalized;

            // Wide conical field of view
            float fov = (dist <= 10.0f) ? 160.0f : _fieldOfViewAngle;
            float angle = Vector3.Angle(transform.forward, dirToPlayer);
            if (angle > fov * 0.5f) return false;

            if (HasClearLineOfSight(chestTarget) || HasClearLineOfSight(headTarget) || HasClearLineOfSight(waistTarget))
            {
                return true;
            }

            return false;
        }

        private bool HasClearLineOfSight(Vector3 targetPoint)
        {
            Vector3 eyePos = transform.position + Vector3.up * 1.85f;
            Vector3 rayDir = targetPoint - eyePos;
            float rayDist = rayDir.magnitude;
            if (rayDist < 0.05f) return true;

            Vector3 rayStart = eyePos + rayDir.normalized * 0.65f;
            float checkDist = Mathf.Max(0.1f, rayDist - 0.65f);

            RaycastHit[] hits = Physics.RaycastAll(rayStart, rayDir.normalized, checkDist, _obstacleMask, QueryTriggerInteraction.Ignore);
            if (hits.Length == 0) return true;

            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            float mechaX = _mechatronicsDoor != null ? _mechatronicsDoor.transform.position.x : -31.5f;
            float ecX = _ecLabDoor != null ? _ecLabDoor.transform.position.x : -19.5f;
            float bathX = _bathroomDoor != null ? _bathroomDoor.transform.position.x : -9.87f;

            foreach (var hit in hits)
            {
                // Ignore the monster itself and its children
                if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;

                // If hit belongs to player
                if (hit.transform == _playerTransform ||
                    hit.transform.IsChildOf(_playerTransform) ||
                    hit.collider.GetComponentInParent<Player.PlayerHealth>() != null ||
                    hit.collider.GetComponentInParent<Player.FPSController>() != null)
                {
                    return true;
                }

                // Ignore doorway colliders right around open doors of Mechatronics Lab, EC Lab, and Bathroom
                Vector3 hp = hit.point;
                if (IsMechatronicsDoorOpen && Mathf.Abs(hp.x - mechaX) <= 1.8f && Mathf.Abs(hp.z - 2.0f) <= 1.5f)
                    continue;
                if (IsECLabDoorOpen && Mathf.Abs(hp.x - ecX) <= 1.8f && Mathf.Abs(hp.z - (-2.0f)) <= 1.5f)
                    continue;
                if (IsBathroomDoorOpen && Mathf.Abs(hp.x - bathX) <= 1.8f && Mathf.Abs(hp.z - 2.0f) <= 1.5f)
                    continue;

                // Hit a solid wall/obstruction
                return false;
            }

            return true;
        }

        private void HandleAttention(AttentionEvent evt)
        {
            if (CurrentState == BossState.Defeated || _isPlayerInSafeRoom || IsBathroomDoorBolted) return;
            if (CurrentState == BossState.Staggered || CurrentState == BossState.DoorAttack) return;

            // Ignore door unbolt / footstep noises right after the safe-room cooldown so player can exit the Bathroom safely
            if (_postSafeRoomGraceTimer > 0f && evt.Type != AttentionType.Laser)
            {
                return;
            }

            float dist = Vector3.Distance(transform.position, evt.Position);

            // While roaming in the Mechatronics Lab after cooldown, only react if the sound is inside the Mechatronics Lab,
            // or a gunshot, or the monster has direct sight of the player
            if (_returnedToSpawnAfterCooldown &&
                GetRoomZone(evt.Position) != RoomZone.MechatronicsLab &&
                evt.Type != AttentionType.Laser &&
                !CanSeePlayer())
            {
                return;
            }

            // Acute mutant hearing range multipliers
            float hearingMultiplier = 4.5f;
            switch (evt.Type)
            {
                case AttentionType.Laser:
                    hearingMultiplier = 3.5f; // Gunshots echo across entire floor (~70m)
                    break;
                case AttentionType.FootstepSprint:
                    hearingMultiplier = 4.5f; // Sprint footsteps (~20m)
                    break;
                case AttentionType.Door:
                    hearingMultiplier = 2.8f; // Door creaks (~14m so unbolting bathroom at 27m doesn't instant-aggro from spawn)
                    break;
                case AttentionType.FootstepWalk:
                    hearingMultiplier = 6.0f; // Walking footsteps (~9m)
                    break;
                case AttentionType.Flashlight:
                    hearingMultiplier = 4.5f; // Click/switch sound (~13m)
                    break;
                default:
                    hearingMultiplier = Mathf.Max(4.0f, 12.0f / Mathf.Max(0.5f, evt.Strength));
                    break;
            }

            float hearingRadius = Mathf.Max(evt.Strength * hearingMultiplier, 9.0f);
            if (dist > hearingRadius) return;

            Debug.Log($"<color=yellow>[BossMonster] HEARD sound '{evt.Type}' (dist: {dist:F1}m <= range: {hearingRadius:F1}m)</color>");

            if (CurrentState == BossState.Dormant)
            {
                if (dist <= 15.0f || evt.Type == AttentionType.Laser)
                {
                    Debug.Log("<color=red>[BossMonster] Loud noise awakened dormant monster! Charging!</color>");
                    TriggerSpawn(transform.position);
                }
                return;
            }

            _returnedToSpawnAfterCooldown = false;
            _lastKnownPlayerPos = evt.Position;

            bool isLoudAudio = evt.Type == AttentionType.Laser
                           || (evt.Type == AttentionType.FootstepSprint && dist <= 18.0f)
                           || (evt.Type == AttentionType.Door && dist <= 15.0f)
                           || dist <= 12.0f;

            if (isLoudAudio)
            {
                Debug.Log($"<color=red>[BossMonster] Audio triggers IMMEDIATE ATTACK CHASE! ({evt.Type} at {dist:F1}m)</color>");
                StartChase();
            }
            else
            {
                if (CurrentState != BossState.Chase)
                {
                    StartSearch();
                }
            }
        }

        private void HandleBathroomBolted(BathroomDoorController door)
        {
            if (door != null && door.IsMainBathroomEntranceDoor()) return;
            if (!IsPlayerActuallyInBathroom()) return;
            if (_isPlayerInSafeRoom && CurrentState == BossState.DoorAttack) return;

            _isPlayerInSafeRoom = true;
            _isWindingUpAttack = false;
            Vector3 stallPos = door != null ? door.transform.position : transform.position;
            TriggerDoorAssault(stallPos);
        }

        private void HandleBathroomUnbolted(BathroomDoorController door)
        {
            if (door != null && door.IsMainBathroomEntranceDoor()) return;
            if (IsAnyBathroomStallBolted()) return;

            _isPlayerInSafeRoom = false;
            _postSafeRoomGraceTimer = 6.0f;
            if (CurrentState == BossState.Roam)
            {
                PlayAnim("walk1");
            }
        }

        private void HandleSafeDoorBolted(WoodDoorController door)
        {
            if (!IsPlayerActuallyInBathroom()) return;
            if (_isPlayerInSafeRoom && CurrentState == BossState.DoorAttack) return;

            _isPlayerInSafeRoom = true;
            _isWindingUpAttack = false;
            Vector3 target = door != null
                ? new Vector3(door.transform.position.x, _groundY, 0.5f)
                : new Vector3(-9.87f, _groundY, 0.5f);
            TriggerDoorAssault(target);
        }

        private void HandleSafeDoorUnbolted(WoodDoorController door)
        {
            _isPlayerInSafeRoom = false;
            _postSafeRoomGraceTimer = 6.0f;
            if (CurrentState == BossState.Roam)
            {
                PlayAnim("walk1");
            }
        }

        public void TakeDamage(float amount)
        {
            if (CurrentState == BossState.Defeated) return;

            // Only 2 shots from the pistol are required to kill the monster (50 damage per shot against 100 max HP)
            float effectiveDamage = Mathf.Max(amount, 50.0f);
            CurrentHealth = Mathf.Max(0f, CurrentHealth - effectiveDamage);
            _currentStunCount++;

            Debug.Log($"<color=red>[BossMonster] TOOK {effectiveDamage} DAMAGE! Shot {_currentStunCount}/{_maxStunsBeforeDefeat} | HP: {CurrentHealth}/{_maxHealth}</color>");

            if (CurrentHealth <= 0f || _currentStunCount >= _maxStunsBeforeDefeat)
            {
                DefeatBoss();
            }
            else
            {
                if (_stateCoroutine != null) StopCoroutine(_stateCoroutine);
                _stateCoroutine = StartCoroutine(StaggerRoutine(1.6f));
            }
        }

        public void Stagger(float duration = 3.5f)
        {
            if (CurrentState == BossState.Defeated) return;

            _currentStunCount++;
            CurrentHealth = Mathf.Max(0f, CurrentHealth - 50.0f);
            Debug.Log($"<color=cyan>[BossMonster] Mutated Prof. Anish Mondal STAGGERED! Hit count: {_currentStunCount}/{_maxStunsBeforeDefeat} | HP: {CurrentHealth}/{_maxHealth}</color>");

            if (_currentStunCount >= _maxStunsBeforeDefeat || CurrentHealth <= 0f)
            {
                DefeatBoss();
                return;
            }

            if (_stateCoroutine != null) StopCoroutine(_stateCoroutine);
            _stateCoroutine = StartCoroutine(StaggerRoutine(duration));
        }

        private IEnumerator StaggerRoutine(float duration)
        {
            CurrentState = BossState.Staggered;
            _isMoving = false;
            OnBossChaseEnded?.Invoke();

            PlayAnim("gethit1");
            PlayStaggerScreech();

            yield return new WaitForSeconds(duration);

            if (CurrentState == BossState.Staggered)
            {
                StartChase();
            }
        }

        private void TriggerDoorAssault(Vector3 targetPos)
        {
            if (CurrentState == BossState.Defeated) return;

            // If monster was still dormant inside Mechatronics Lab when player bolted bathroom stall door, keep it at spawn
            if (CurrentState == BossState.Dormant)
            {
                _postSafeRoomGraceTimer = 6.0f;
                return;
            }

            Debug.Log("<color=green>[BossMonster] Player hid behind bolted Bathroom stall door! Roaming Bathroom for 5s cooldown before returning to spawn.</color>");
            if (_stateCoroutine != null) StopCoroutine(_stateCoroutine);
            _stateCoroutine = StartCoroutine(DoorAssaultRoutine(targetPos));
        }

        private IEnumerator DoorAssaultRoutine(Vector3 boltedDoorPos)
        {
            CurrentState = BossState.DoorAttack;
            _isWindingUpAttack = false;
            OnBossChaseEnded?.Invoke();

            // Pick nearest bathroom aisle waypoint to start roaming the bathroom during the 5s cooldown
            if (_bathroomRoamPoints != null && _bathroomRoamPoints.Length > 0)
            {
                float bestDist = float.MaxValue;
                for (int i = 0; i < _bathroomRoamPoints.Length; i++)
                {
                    float d = Vector3.Distance(transform.position, _bathroomRoamPoints[i]);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        _bathroomRoamIndex = i;
                    }
                }
            }

            // Exact 5.0-second cooldown while player hides behind the bolted bathroom stall door and monster roams the bathroom
            const float cooldownDuration = 5.0f;
            float elapsed = 0f;
            int lastDisplayedSec = -1;

            while (elapsed < cooldownDuration)
            {
                // If player unbolts the stall door or leaves the bathroom before the 5s cooldown finishes, resume chase immediately
                if (!IsPlayerActuallyInBathroom() || (!IsAnyBathroomStallBolted() && !_isPlayerInSafeRoom))
                {
                    _isPlayerInSafeRoom = false;
                    StartChase();
                    yield break;
                }

                int remainingSec = Mathf.CeilToInt(cooldownDuration - elapsed);
                if (remainingSec != lastDisplayedSec && ObjectiveManager.Instance != null)
                {
                    lastDisplayedSec = remainingSec;
                    ObjectiveManager.Instance.SetCustomObjective($"STALL DOOR BOLTED! MONSTER ROAMING BATHROOM ({remainingSec}s UNTIL RETREAT TO SPAWN)...");
                }

                // Roam around the bathroom aisles during the 5-second cooldown
                if (_bathroomRoamPoints != null && _bathroomRoamPoints.Length > 0)
                {
                    Vector3 wp = _bathroomRoamPoints[_bathroomRoamIndex % _bathroomRoamPoints.Length];
                    float distToWp = Vector3.Distance(
                        new Vector3(transform.position.x, 0f, transform.position.z),
                        new Vector3(wp.x, 0f, wp.z));

                    if (distToWp <= 0.9f)
                    {
                        _bathroomRoamIndex = (_bathroomRoamIndex + 1) % _bathroomRoamPoints.Length;
                        wp = _bathroomRoamPoints[_bathroomRoamIndex];
                    }

                    PlayAnim("walk1");
                    MoveMonsterTowards(wp, _walkSpeed);
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            // 5s cooldown complete -> Return monster cleanly to its Mechatronics Lab spawn position and roam there!
            ReturnToSpawnAfterCooldown();
        }

        private void ReturnToSpawnAfterCooldown()
        {
            if (CurrentState == BossState.Defeated) return;

            EnsureNavMeshAgentConfigured();
            SetLogicalPosition(_initialSpawnPos, true);
            if (Agent != null && Agent.enabled && Agent.isOnNavMesh)
            {
                Agent.ResetPath();
            }
            _returnedToSpawnAfterCooldown = true;
            _postSafeRoomGraceTimer = 6.0f;
            _currentRoamIndex = 1; // Immediately walk from spawn point (-34.5, 0.05, 14.5) to the next Mechatronics Lab waypoint

            if (ObjectiveManager.Instance != null)
            {
                ObjectiveManager.Instance.SetCustomObjective("MONSTER RETURNED TO SPAWN! UNBOLT STALL DOOR [E] & COLLECT LASER PARTS!");
            }

            StartRoam();
            Debug.Log($"<color=green>[BossMonster] 5s cooldown elapsed! Monster returned to spawn at {_initialSpawnPos} and started roaming in Mechatronics Lab.</color>");
        }

        private void DefeatBoss()
        {
            CurrentState = BossState.Defeated;
            _isMoving = false;
            OnBossChaseEnded?.Invoke();
            OnBossDefeated?.Invoke();

            if (Agent != null && Agent.enabled) Agent.enabled = false;

            PlayAnim("fall1");
            PlayDeathSound();

            foreach (var col in GetComponentsInChildren<Collider>())
            {
                col.enabled = false;
            }

            if (_keycardDropPrefab != null)
            {
                Instantiate(_keycardDropPrefab, transform.position + Vector3.up * 0.5f, Quaternion.identity);
            }

            if (ObjectiveManager.Instance != null)
            {
                ObjectiveManager.Instance.SetCustomObjective("PROF. ANISH MONDAL PACIFIED! COLLECT MASTER SECURITY OVERRIDE & PROCEED TO ROOF STAIRWELL!");
            }

            Debug.Log("<color=green>[BossMonster] MUTATED PROF. ANISH MONDAL DEFEATED!</color>");
        }

        public void ResetToMechatronics()
        {
            if (CurrentState == BossState.Defeated) return;

            EnsureNavMeshAgentConfigured();
            SetLogicalPosition(_initialSpawnPos, true);
            if (Agent != null && Agent.enabled && Agent.isOnNavMesh)
            {
                Agent.ResetPath();
            }
            _currentRoamIndex = 1;
            _returnedToSpawnAfterCooldown = true;
            _postSafeRoomGraceTimer = 3.0f;

            StartRoam();
            Debug.Log("<color=yellow>[BossMonster] Reset to Mechatronics Lab spawn after player respawn and started roaming.</color>");
        }

        private void PlayAnim(string animName)
        {
            if (Animator != null)
            {
                Animator.Play(animName);
            }
        }

        #region Monster Audio & Procedural Sound Synthesis

        public void PlayRoarIntroSound()
        {
            EnsureMonsterAudioSources();
            InitMonsterAudio();
            if (Time.time - _lastVocalPlayTime < 0.8f) return;
            _lastVocalPlayTime = Time.time;

            AudioClip clip = _roarIntroSfx != null ? _roarIntroSfx : _fallbackRoarIntro;
            if (clip != null && _audioSource != null)
            {
                _audioSource.pitch = UnityEngine.Random.Range(0.88f, 0.98f);
                _audioSource.PlayOneShot(clip, 0.30f);
            }
        }

        public void PlayChaseScream()
        {
            EnsureMonsterAudioSources();
            InitMonsterAudio();
            if (Time.time - _lastVocalPlayTime < 1.2f) return;
            _lastVocalPlayTime = Time.time;

            AudioClip clip = _roarChaseSfx != null ? _roarChaseSfx : (_roarIntroSfx != null ? _roarIntroSfx : _fallbackChaseScream);
            if (clip != null && _audioSource != null)
            {
                _audioSource.pitch = UnityEngine.Random.Range(0.92f, 1.04f);
                _audioSource.PlayOneShot(clip, 0.26f);
            }
        }

        public void PlayAttackSound()
        {
            EnsureMonsterAudioSources();
            InitMonsterAudio();
            if (_audioSource != null)
            {
                AudioClip swingClip = _attackSwingSfx != null ? _attackSwingSfx : _fallbackAttackSwing;
                if (swingClip != null)
                {
                    _audioSource.pitch = UnityEngine.Random.Range(0.92f, 1.06f);
                    _audioSource.PlayOneShot(swingClip, 0.25f);
                }

                AudioClip hitClip = _attackHitSfx != null ? _attackHitSfx : _fallbackAttackHit;
                if (hitClip != null)
                {
                    _audioSource.PlayOneShot(hitClip, 0.28f);
                }
            }
        }

        public void PlayStaggerScreech()
        {
            EnsureMonsterAudioSources();
            InitMonsterAudio();
            _lastVocalPlayTime = Time.time;

            AudioClip clip = _staggerScreechSfx != null ? _staggerScreechSfx : _fallbackStaggerScreech;
            if (clip != null && _audioSource != null)
            {
                _audioSource.pitch = UnityEngine.Random.Range(0.95f, 1.03f);
                _audioSource.PlayOneShot(clip, 0.28f);
            }
        }

        private void UpdateFootstepAudio()
        {
            if (!_isMoving || CurrentState == BossState.Dormant || CurrentState == BossState.Defeated || CurrentState == BossState.Staggered)
            {
                _footstepTimer = 0.08f;
                if (_footstepAudioSource != null && _footstepAudioSource.isPlaying)
                {
                    _footstepAudioSource.Stop();
                }
                return;
            }

            _footstepTimer -= Time.deltaTime;
            if (_footstepTimer <= 0f)
            {
                float interval = CurrentState == BossState.Chase
                    ? 0.42f
                    : 0.68f;
                _footstepTimer = interval;
                PlayFootstepSound();
            }
        }

        public void PlayFootstepSound()
        {
            EnsureMonsterAudioSources();
            InitMonsterAudio();

            // IMPORTANT: Ignore multi-second looping clips (like metal.wav which is 4s long)
            // so we never stack overlapping loop tracks on every step!
            AudioClip clip = (_heavyFootstepSfx != null && _heavyFootstepSfx.length <= 0.45f)
                ? _heavyFootstepSfx
                : _fallbackFootstep;

            AudioSource stepSource = _footstepAudioSource != null ? _footstepAudioSource : _audioSource;
            if (clip != null && stepSource != null)
            {
                stepSource.pitch = UnityEngine.Random.Range(0.82f, 0.96f);
                float stepVol = CurrentState == BossState.Chase ? 0.26f : 0.18f;
                stepSource.PlayOneShot(clip, stepVol);
            }
        }

        public void PlayDeathSound()
        {
            EnsureMonsterAudioSources();
            InitMonsterAudio();
            if (_footstepAudioSource != null && _footstepAudioSource.isPlaying)
                _footstepAudioSource.Stop();

            AudioClip clip = _deathSfx != null ? _deathSfx : _fallbackDeath;
            if (clip != null && _audioSource != null)
            {
                _audioSource.pitch = 0.88f;
                _audioSource.PlayOneShot(clip, 0.32f);
            }
        }

        private static AudioClip _fallbackRoarIntro;
        private static AudioClip _fallbackChaseScream;
        private static AudioClip _fallbackAttackSwing;
        private static AudioClip _fallbackAttackHit;
        private static AudioClip _fallbackStaggerScreech;
        private static AudioClip _fallbackFootstep;
        private static AudioClip _fallbackDeath;

        private static void InitMonsterAudio()
        {
            if (_fallbackRoarIntro != null) return;

            int sampleRate = 44100;

            // 1. Intro Guttural Growl (Smooth low-frequency rumble, no harsh static)
            int roarSamples = (int)(sampleRate * 1.4f);
            _fallbackRoarIntro = AudioClip.Create("MonsterRoarIntro", roarSamples, 1, sampleRate, false);
            float[] roarData = new float[roarSamples];
            for (int i = 0; i < roarSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 1.4f)) * Mathf.Exp(-t * 0.7f);
                float sub = Mathf.Sin(2f * Mathf.PI * (58f + 12f * Mathf.Sin(8f * t)) * t);
                float growl = Mathf.Sin(2f * Mathf.PI * (112f + 18f * Mathf.Sin(16f * t)) * t);
                roarData[i] = (sub * 0.65f + growl * 0.35f) * env * 0.26f;
            }
            _fallbackRoarIntro.SetData(roarData, 0);

            // 2. Chase Growl (Mid-low tense pulse, no piercing high-frequency screech)
            int screamSamples = (int)(sampleRate * 1.1f);
            _fallbackChaseScream = AudioClip.Create("MonsterChaseScream", screamSamples, 1, sampleRate, false);
            float[] screamData = new float[screamSamples];
            for (int i = 0; i < screamSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 1.1f)) * Mathf.Exp(-t * 0.9f);
                float pitch = 135f + 35f * Mathf.Sin(14f * t) - t * 25f;
                float wave = Mathf.Sin(2f * Mathf.PI * pitch * t);
                float sub = Mathf.Sin(2f * Mathf.PI * (pitch * 0.5f) * t);
                screamData[i] = (wave * 0.55f + sub * 0.45f) * env * 0.24f;
            }
            _fallbackChaseScream.SetData(screamData, 0);

            // 3. Attack Swing (Soft low whoosh)
            int swingSamples = (int)(sampleRate * 0.28f);
            _fallbackAttackSwing = AudioClip.Create("MonsterAttackSwing", swingSamples, 1, sampleRate, false);
            float[] swingData = new float[swingSamples];
            for (int i = 0; i < swingSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Sin(Mathf.PI * (t / 0.28f));
                float whoosh = Mathf.Sin(2f * Mathf.PI * (95f + t * 140f) * t);
                swingData[i] = whoosh * env * 0.24f;
            }
            _fallbackAttackSwing.SetData(swingData, 0);

            // 4. Attack Hit Impact (Low muted thud)
            int hitSamples = (int)(sampleRate * 0.24f);
            _fallbackAttackHit = AudioClip.Create("MonsterAttackHit", hitSamples, 1, sampleRate, false);
            float[] hitData = new float[hitSamples];
            for (int i = 0; i < hitSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 28f);
                float thud = Mathf.Sin(2f * Mathf.PI * (85f - t * 45f) * t);
                hitData[i] = thud * env * 0.28f;
            }
            _fallbackAttackHit.SetData(hitData, 0);

            // 5. Stagger Groan (Low resonant groan instead of piercing 750Hz screech)
            int staggerSamples = (int)(sampleRate * 0.95f);
            _fallbackStaggerScreech = AudioClip.Create("MonsterStaggerScreech", staggerSamples, 1, sampleRate, false);
            float[] staggerData = new float[staggerSamples];
            for (int i = 0; i < staggerSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 3.0f);
                float groan = Mathf.Sin(2f * Mathf.PI * (160f - t * 55f + 20f * Mathf.Sin(18f * t)) * t);
                staggerData[i] = groan * env * 0.25f;
            }
            _fallbackStaggerScreech.SetData(staggerData, 0);

            // 6. Clean Single-Step Heavy Footstep Thud (Short 0.16s damped low thud)
            int footstepSamples = (int)(sampleRate * 0.16f);
            _fallbackFootstep = AudioClip.Create("MonsterFootstep", footstepSamples, 1, sampleRate, false);
            float[] footstepData = new float[footstepSamples];
            for (int i = 0; i < footstepSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 38f);
                float subThud = Mathf.Sin(2f * Mathf.PI * (52f - t * 120f) * t);
                float midTap = Mathf.Sin(2f * Mathf.PI * 110f * t) * Mathf.Exp(-t * 65f);
                footstepData[i] = (subThud * 0.75f + midTap * 0.25f) * env * 0.28f;
            }
            _fallbackFootstep.SetData(footstepData, 0);

            // 7. Death Low Exhale
            int deathSamples = (int)(sampleRate * 1.8f);
            _fallbackDeath = AudioClip.Create("MonsterDeath", deathSamples, 1, sampleRate, false);
            float[] deathData = new float[deathSamples];
            for (int i = 0; i < deathSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Clamp01(1f - t / 1.8f) * Mathf.Exp(-t * 1.2f);
                float low = Mathf.Sin(2f * Mathf.PI * (68f - t * 22f) * t);
                deathData[i] = low * env * 0.28f;
            }
            _fallbackDeath.SetData(deathData, 0);
        }

        #endregion
    }
}

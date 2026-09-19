using UnityEngine;
using LateSubmission.Attention;
using LateSubmission.Player;

namespace LateSubmission.AI
{
    /// <summary>
    /// Dual-tier sensory perception system:
    /// 1. Acoustic / Attention Hearing
    /// 2. Conical Vision with Line of Sight raycasting
    /// </summary>
    public class EntityPerception : MonoBehaviour
    {
        [Header("Hearing Settings")]
        [SerializeField] private float _hearingThreshold = 0.25f;
        [SerializeField] private float _maxHearingDistance = 30f;

        [Header("Vision Settings")]
        [SerializeField] private float _viewAngle = 80f;
        [SerializeField] private float _viewDistanceNormal = 16f;
        [SerializeField] private float _viewDistanceWithFlashlight = 25f;
        [SerializeField] private LayerMask _occlusionMask = ~0;
        [SerializeField] private Transform _eyeTransform;

        public bool CanSeePlayer { get; private set; }
        public Vector3 LastKnownPlayerPosition { get; private set; }
        public Vector3 LastAttentionPosition { get; private set; }
        public bool HasPendingAttentionEvent { get; private set; }

        private Transform _playerTransform;
        private FlashlightController _playerFlashlight;

        private void OnEnable()
        {
            AttentionManager.OnAttentionEmitted += HandleAttentionEmitted;
        }

        private void OnDisable()
        {
            AttentionManager.OnAttentionEmitted -= HandleAttentionEmitted;
        }

        private void Start()
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                _playerTransform = player.transform;
                _playerFlashlight = player.GetComponentInChildren<FlashlightController>();
            }

            if (_eyeTransform == null)
            {
                _eyeTransform = transform;
            }
        }

        private void Update()
        {
            EvaluateLineOfSight();
        }

        private void EvaluateLineOfSight()
        {
            if (_playerTransform == null)
            {
                GameObject player = GameObject.FindWithTag("Player");
                if (player != null)
                {
                    _playerTransform = player.transform;
                    _playerFlashlight = player.GetComponentInChildren<FlashlightController>();
                }
                CanSeePlayer = false;
                return;
            }

            Vector3 eyePos = _eyeTransform.position;
            Vector3 targetPos = _playerTransform.position + Vector3.up * 1.0f;
            Vector3 dirToPlayer = targetPos - eyePos;
            float distToPlayer = dirToPlayer.magnitude;

            bool flashlightOn = _playerFlashlight != null && _playerFlashlight.IsOn;
            float maxDist = flashlightOn ? _viewDistanceWithFlashlight : _viewDistanceNormal;

            if (distToPlayer > maxDist)
            {
                CanSeePlayer = false;
                return;
            }

            float angle = Vector3.Angle(_eyeTransform.forward, dirToPlayer.normalized);
            if (angle > _viewAngle * 0.5f)
            {
                CanSeePlayer = false;
                return;
            }

            if (Physics.Raycast(eyePos, dirToPlayer.normalized, out RaycastHit hit, distToPlayer, _occlusionMask))
            {
                if (hit.transform.CompareTag("Player") || hit.transform.IsChildOf(_playerTransform))
                {
                    CanSeePlayer = true;
                    LastKnownPlayerPosition = _playerTransform.position;
                    return;
                }
            }

            CanSeePlayer = false;
        }

        private void HandleAttentionEmitted(AttentionEvent evt)
        {
            float dist = Vector3.Distance(transform.position, evt.Position);
            if (dist > _maxHearingDistance) return;

            float effectiveStimulus = evt.Strength / Mathf.Max(1.0f, dist);
            if (effectiveStimulus >= _hearingThreshold)
            {
                LastAttentionPosition = evt.Position;
                HasPendingAttentionEvent = true;
            }
        }

        public void ClearPendingAttention()
        {
            HasPendingAttentionEvent = false;
        }
    }
}

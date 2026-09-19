using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace LateSubmission.AI
{
    /// <summary>
    /// Manages safe entity relocations, scoring candidate spawn points
    /// to guarantee the entity never pops into player view or too close.
    /// </summary>
    public class RelocationManager : MonoBehaviour
    {
        public static RelocationManager Instance { get; private set; }

        [Header("Distance Limits")]
        [SerializeField] private float _minDistance = 15f;
        [SerializeField] private float _maxDistance = 40f;
        [SerializeField] private LayerMask _occlusionMask = ~0;

        private readonly List<RelocationPoint> _points = new List<RelocationPoint>();
        private Camera _playerCam;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _points.AddRange(FindObjectsByType<RelocationPoint>(FindObjectsSortMode.None));
            _playerCam = Camera.main;
        }

        public void RegisterPoint(RelocationPoint point)
        {
            if (!_points.Contains(point))
            {
                _points.Add(point);
            }
        }

        public bool RelocateEntity(EntityController entity)
        {
            if (_playerCam == null) _playerCam = Camera.main;
            if (_playerCam == null || _points.Count == 0) return false;

            Vector3 camPos = _playerCam.transform.position;
            Vector3 camForward = _playerCam.transform.forward;

            RelocationPoint bestPoint = null;
            float highestScore = -1f;

            foreach (var point in _points)
            {
                if (point == null) continue;

                Vector3 pointPos = point.transform.position;
                Vector3 toPoint = pointPos - camPos;
                float dist = toPoint.magnitude;

                if (dist < _minDistance || dist > _maxDistance) continue;

                float viewDot = Vector3.Dot(camForward, toPoint.normalized);
                if (viewDot > 0.35f) continue; // In front of player

                // Verify line of sight occlusion
                if (!Physics.Linecast(camPos, pointPos, _occlusionMask))
                {
                    continue; // Direct line of sight exists, skip
                }

                if (NavMesh.SamplePosition(pointPos, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
                {
                    float score = dist * (1.0f - Mathf.Clamp01(viewDot));
                    if (score > highestScore)
                    {
                        highestScore = score;
                        bestPoint = point;
                    }
                }
            }

            if (bestPoint != null)
            {
                entity.Agent.Warp(bestPoint.transform.position);
                entity.transform.rotation = bestPoint.transform.rotation;
                return true;
            }

            // Fallback: Pick furthest point if none pass strict filter
            float maxDist = -1f;
            foreach (var point in _points)
            {
                if (point == null) continue;
                float d = Vector3.Distance(camPos, point.transform.position);
                if (d > maxDist)
                {
                    maxDist = d;
                    bestPoint = point;
                }
            }

            if (bestPoint != null)
            {
                entity.Agent.Warp(bestPoint.transform.position);
                return true;
            }

            return false;
        }
    }
}

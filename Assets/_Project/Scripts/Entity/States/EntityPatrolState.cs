using UnityEngine;
using LateSubmission.Core;

namespace LateSubmission.AI
{
    public class EntityPatrolState : IState
    {
        private readonly EntityController _entity;
        private int _currentWaypointIndex = 0;

        public EntityPatrolState(EntityController entity)
        {
            _entity = entity;
        }

        public void Enter()
        {
            _entity.Agent.isStopped = false;
            _entity.Agent.speed = _entity.PatrolSpeed;
            _entity.SetHuntAudio(false);
            MoveToNextWaypoint();
        }

        public void Tick()
        {
            if (_entity.Perception.CanSeePlayer)
            {
                _entity.StateMachine.ChangeState(new EntityHuntState(_entity));
                return;
            }

            if (_entity.Perception.HasPendingAttentionEvent)
            {
                _entity.StateMachine.ChangeState(new EntityInvestigateState(_entity, _entity.Perception.LastAttentionPosition));
                return;
            }

            if (!_entity.Agent.pathPending && _entity.Agent.remainingDistance <= _entity.Agent.stoppingDistance + 0.3f)
            {
                MoveToNextWaypoint();
            }
        }

        public void Exit()
        {
        }

        private void MoveToNextWaypoint()
        {
            if (_entity.Waypoints == null || _entity.Waypoints.Length == 0) return;

            Transform target = _entity.Waypoints[_currentWaypointIndex];
            if (target != null)
            {
                _entity.Agent.SetDestination(target.position);
            }

            _currentWaypointIndex = (_currentWaypointIndex + 1) % _entity.Waypoints.Length;
        }
    }
}

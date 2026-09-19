using UnityEngine;
using LateSubmission.Core;

namespace LateSubmission.AI
{
    public class EntityInvestigateState : IState
    {
        private readonly EntityController _entity;
        private Vector3 _targetPosition;

        public EntityInvestigateState(EntityController entity, Vector3 targetPosition)
        {
            _entity = entity;
            _targetPosition = targetPosition;
        }

        public void Enter()
        {
            _entity.Agent.isStopped = false;
            _entity.Agent.speed = _entity.InvestigateSpeed;
            _entity.Agent.SetDestination(_targetPosition);
            _entity.Perception.ClearPendingAttention();
            _entity.SetHuntAudio(false);
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
                _targetPosition = _entity.Perception.LastAttentionPosition;
                _entity.Agent.SetDestination(_targetPosition);
                _entity.Perception.ClearPendingAttention();
            }

            if (!_entity.Agent.pathPending && _entity.Agent.remainingDistance <= _entity.Agent.stoppingDistance + 0.5f)
            {
                _entity.StateMachine.ChangeState(new EntitySearchState(_entity, _targetPosition));
            }
        }

        public void Exit()
        {
        }
    }
}

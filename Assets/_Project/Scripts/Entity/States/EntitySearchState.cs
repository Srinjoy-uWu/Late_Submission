using UnityEngine;
using LateSubmission.Core;

namespace LateSubmission.AI
{
    public class EntitySearchState : IState
    {
        private readonly EntityController _entity;
        private readonly Vector3 _centerPosition;
        private float _searchTimer = 0f;
        private const float SearchDuration = 7.0f;
        private float _subDestinationTimer = 0f;

        public EntitySearchState(EntityController entity, Vector3 centerPosition)
        {
            _entity = entity;
            _centerPosition = centerPosition;
        }

        public void Enter()
        {
            _entity.Agent.isStopped = false;
            _entity.Agent.speed = _entity.PatrolSpeed * 1.2f;
            _searchTimer = 0f;
            _subDestinationTimer = 0f;
            _entity.SetHuntAudio(false);
            PickRandomSearchPoint();
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

            _searchTimer += Time.deltaTime;
            _subDestinationTimer += Time.deltaTime;

            if (_searchTimer >= SearchDuration)
            {
                _entity.StateMachine.ChangeState(new EntityPatrolState(_entity));
                return;
            }

            if (_subDestinationTimer >= 2.5f || (!_entity.Agent.pathPending && _entity.Agent.remainingDistance <= _entity.Agent.stoppingDistance + 0.3f))
            {
                _subDestinationTimer = 0f;
                PickRandomSearchPoint();
            }
        }

        public void Exit()
        {
        }

        private void PickRandomSearchPoint()
        {
            Vector2 randomCircle = Random.insideUnitCircle * 6.0f;
            Vector3 randomTarget = _centerPosition + new Vector3(randomCircle.x, 0, randomCircle.y);

            if (UnityEngine.AI.NavMesh.SamplePosition(randomTarget, out UnityEngine.AI.NavMeshHit hit, 4.0f, UnityEngine.AI.NavMesh.AllAreas))
            {
                _entity.Agent.SetDestination(hit.position);
            }
        }
    }
}

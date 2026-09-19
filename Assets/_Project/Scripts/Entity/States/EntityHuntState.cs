using UnityEngine;
using LateSubmission.Core;

namespace LateSubmission.AI
{
    public class EntityHuntState : IState
    {
        private readonly EntityController _entity;
        private float _lostSightTimer = 0f;
        private const float MaxLostSightDuration = 3.5f;

        public EntityHuntState(EntityController entity)
        {
            _entity = entity;
        }

        public void Enter()
        {
            _entity.Agent.isStopped = false;
            _entity.Agent.speed = _entity.HuntSpeed;
            _entity.SetHuntAudio(true);
            _lostSightTimer = 0f;
        }

        public void Tick()
        {
            if (_entity.Perception.CanSeePlayer)
            {
                _lostSightTimer = 0f;
                _entity.Agent.SetDestination(_entity.Perception.LastKnownPlayerPosition);
            }
            else
            {
                _lostSightTimer += Time.deltaTime;
                _entity.Agent.SetDestination(_entity.Perception.LastKnownPlayerPosition);

                if (_lostSightTimer >= MaxLostSightDuration)
                {
                    _entity.StateMachine.ChangeState(new EntitySearchState(_entity, _entity.Perception.LastKnownPlayerPosition));
                }
            }
        }

        public void Exit()
        {
            _entity.SetHuntAudio(false);
        }
    }
}

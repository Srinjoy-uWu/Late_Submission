using UnityEngine;
using LateSubmission.Core;

namespace LateSubmission.AI
{
    public class EntityStaggerState : IState
    {
        private readonly EntityController _entity;
        private readonly float _duration;
        private float _timer = 0f;

        public EntityStaggerState(EntityController entity, float duration = 3.0f)
        {
            _entity = entity;
            _duration = duration;
        }

        public void Enter()
        {
            _entity.Agent.isStopped = true;
            _entity.Agent.velocity = Vector3.zero;
            _entity.SetHuntAudio(false);
            _timer = 0f;
        }

        public void Tick()
        {
            _timer += Time.deltaTime;
            if (_timer >= _duration)
            {
                _entity.StateMachine.ChangeState(new EntityRelocateState(_entity));
            }
        }

        public void Exit()
        {
            _entity.Agent.isStopped = false;
        }
    }
}

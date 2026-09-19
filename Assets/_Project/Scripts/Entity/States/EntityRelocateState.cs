using LateSubmission.Core;

namespace LateSubmission.AI
{
    public class EntityRelocateState : IState
    {
        private readonly EntityController _entity;

        public EntityRelocateState(EntityController entity)
        {
            _entity = entity;
        }

        public void Enter()
        {
            if (RelocationManager.Instance != null)
            {
                RelocationManager.Instance.RelocateEntity(_entity);
            }

            // Return to patrol at new safe anchor point
            _entity.StateMachine.ChangeState(new EntityPatrolState(_entity));
        }

        public void Tick()
        {
        }

        public void Exit()
        {
        }
    }
}

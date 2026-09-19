using System;

namespace LateSubmission.Core
{
    /// <summary>
    /// Generic finite state machine driving state transitions cleanly.
    /// </summary>
    public class StateMachine
    {
        public IState CurrentState { get; private set; }
        public event Action<IState> OnStateChanged;

        public void ChangeState(IState newState)
        {
            if (CurrentState == newState) return;

            CurrentState?.Exit();
            CurrentState = newState;
            CurrentState?.Enter();
            OnStateChanged?.Invoke(CurrentState);
        }

        public void Tick()
        {
            CurrentState?.Tick();
        }
    }
}

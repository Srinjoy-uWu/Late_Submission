namespace LateSubmission.Core
{
    /// <summary>
    /// Contract for all FSM states.
    /// Enter is invoked upon entering the state,
    /// Tick is invoked per frame,
    /// Exit is invoked upon transitioning out.
    /// </summary>
    public interface IState
    {
        void Enter();
        void Tick();
        void Exit();
    }
}

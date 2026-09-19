namespace LateSubmission.Interaction
{
    /// <summary>
    /// Central interaction contract for all doors, pickups, notes, terminals and workbench.
    /// </summary>
    public interface IInteractable
    {
        string GetInteractionText();
        bool CanInteract(Interactor interactor);
        void Interact(Interactor interactor);
    }
}

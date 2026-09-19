using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace LateSubmission.Interaction
{
    /// <summary>
    /// Attached to the player camera to perform forward raycast checks for IInteractable objects.
    /// Emits events when an interactable is hovered or interacted with.
    /// </summary>
    public class Interactor : MonoBehaviour
    {
        [Header("Raycast Settings")]
        [SerializeField] private float _interactionDistance = 2.5f;
        [SerializeField] private LayerMask _interactableMask = ~0;

        [Header("Input Fallback")]
        [SerializeField] private KeyCode _interactKey = KeyCode.E;

        public IInteractable CurrentInteractable { get; private set; }

        public event Action<IInteractable> OnInteractableHoverEnter;
        public event Action OnInteractableHoverExit;
        public event Action<IInteractable> OnInteracted;

        private Camera _cam;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            if (_cam == null)
            {
                _cam = Camera.main;
            }
        }

        private void Update()
        {
            PerformHoverRaycast();
            CheckInteractionInput();
        }

        private void PerformHoverRaycast()
        {
            Transform rayOrigin = _cam != null ? _cam.transform : transform;
            Ray ray = new Ray(rayOrigin.position, rayOrigin.forward);

            if (Physics.Raycast(ray, out RaycastHit hit, _interactionDistance, _interactableMask))
            {
                IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();

                if (interactable != null && interactable.CanInteract(this))
                {
                    if (CurrentInteractable != interactable)
                    {
                        CurrentInteractable = interactable;
                        OnInteractableHoverEnter?.Invoke(CurrentInteractable);
                    }
                    return;
                }
            }

            if (CurrentInteractable != null)
            {
                CurrentInteractable = null;
                OnInteractableHoverExit?.Invoke();
            }
        }

        private void CheckInteractionInput()
        {
            bool triggerInteract = false;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                triggerInteract = true;
            }
#endif
            if (Input.GetKeyDown(_interactKey))
            {
                triggerInteract = true;
            }

            if (triggerInteract && CurrentInteractable != null && CurrentInteractable.CanInteract(this))
            {
                CurrentInteractable.Interact(this);
                OnInteracted?.Invoke(CurrentInteractable);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Transform origin = _cam != null ? _cam.transform : transform;
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(origin.position, origin.forward * _interactionDistance);
        }
    }
}

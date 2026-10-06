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
        [SerializeField] private float _interactionDistance = 3.2f;
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
            var hud = LateSubmission.UI.HUDController.Instance;
            if (hud != null && (hud.IsNoteOpen || hud.JustClosedNote))
            {
                if (CurrentInteractable != null)
                {
                    CurrentInteractable = null;
                    OnInteractableHoverExit?.Invoke();
                }
                return;
            }

            PerformHoverRaycast();
            CheckInteractionInput();
        }

        private void PerformHoverRaycast()
        {
            Transform rayOrigin = _cam != null ? _cam.transform : transform;
            Ray ray = new Ray(rayOrigin.position, rayOrigin.forward);

            IInteractable target = null;

            // 1. Direct raycast
            if (Physics.Raycast(ray, out RaycastHit hit, _interactionDistance, _interactableMask, QueryTriggerInteraction.Collide))
            {
                var interactable = hit.collider.GetComponentInParent<IInteractable>();
                if (interactable != null && interactable.CanInteract(this))
                {
                    target = interactable;
                }
            }

            // 2. Spherecast fallback (makes small items like notes and keys effortless to target)
            if (target == null)
            {
                if (Physics.SphereCast(ray, 0.16f, out RaycastHit sphereHit, _interactionDistance, _interactableMask, QueryTriggerInteraction.Collide))
                {
                    var interactable = sphereHit.collider.GetComponentInParent<IInteractable>();
                    if (interactable != null && interactable.CanInteract(this))
                    {
                        target = interactable;
                    }
                }
            }

            // 3. Multi-hit fallback if grazing a non-interactable table edge or prop
            if (target == null)
            {
                RaycastHit[] hits = Physics.RaycastAll(ray, _interactionDistance, _interactableMask, QueryTriggerInteraction.Collide);
                if (hits != null && hits.Length > 0)
                {
                    Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                    foreach (var h in hits)
                    {
                        var interactable = h.collider.GetComponentInParent<IInteractable>();
                        if (interactable != null && interactable.CanInteract(this))
                        {
                            target = interactable;
                            break;
                        }
                    }
                }
            }

            if (target != null)
            {
                if (CurrentInteractable != target)
                {
                    CurrentInteractable = target;
                    OnInteractableHoverEnter?.Invoke(CurrentInteractable);
                }
                return;
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
            if (!triggerInteract)
            {
                try
                {
                    if (Input.GetKeyDown(_interactKey))
                    {
                        triggerInteract = true;
                    }
                }
                catch {}
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

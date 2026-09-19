using System.Collections;
using UnityEngine;
using LateSubmission.Inventory;
using LateSubmission.Attention;

namespace LateSubmission.Interaction
{
    /// <summary>
    /// Smooth rotating or sliding door that can be locked with a required ItemType key.
    /// Emits AttentionEvent(Door) when opened.
    /// </summary>
    public class Door : MonoBehaviour, IInteractable
    {
        [Header("Door State")]
        [SerializeField] private bool _isOpen = false;
        [SerializeField] private bool _isLocked = false;
        [SerializeField] private ItemType _requiredKey = ItemType.FacultyKey;
        [SerializeField] private string _lockedMessage = "Locked. Requires Faculty Key.";

        [Header("Rotation Settings")]
        [SerializeField] private Transform _doorHinge;
        [SerializeField] private float _openAngle = 90f;
        [SerializeField] private float _animationSpeed = 3f;

        [Header("Audio")]
        [SerializeField] private AudioClip _openSfx;
        [SerializeField] private AudioClip _closeSfx;
        [SerializeField] private AudioClip _lockedSfx;

        [Header("Attention Noise")]
        [SerializeField] private float _doorNoise = 7.0f;

        private Quaternion _closedRotation;
        private Quaternion _targetRotation;
        private Coroutine _movementCoroutine;

        private void Awake()
        {
            if (_doorHinge == null)
            {
                _doorHinge = transform;
            }
            _closedRotation = _doorHinge.localRotation;
            _targetRotation = _closedRotation;
        }

        public string GetInteractionText()
        {
            if (_isLocked)
            {
                if (InventoryManager.Instance != null && InventoryManager.Instance.HasItemType(_requiredKey))
                {
                    return "Unlock Door [E]";
                }
                return _lockedMessage;
            }

            return _isOpen ? "Close Door [E]" : "Open Door [E]";
        }

        public bool CanInteract(Interactor interactor)
        {
            return true;
        }

        public void Interact(Interactor interactor)
        {
            if (_isLocked)
            {
                if (InventoryManager.Instance != null && InventoryManager.Instance.HasItemType(_requiredKey))
                {
                    _isLocked = false;
                    ToggleDoor();
                }
                else
                {
                    if (_lockedSfx != null)
                    {
                        AudioSource.PlayClipAtPoint(_lockedSfx, transform.position);
                    }
                }
                return;
            }

            ToggleDoor();
        }

        public void ToggleDoor()
        {
            _isOpen = !_isOpen;

            Vector3 eulerTarget = _isOpen ? new Vector3(0, _openAngle, 0) : Vector3.zero;
            _targetRotation = _closedRotation * Quaternion.Euler(eulerTarget);

            if (_movementCoroutine != null)
            {
                StopCoroutine(_movementCoroutine);
            }
            _movementCoroutine = StartCoroutine(AnimateDoor());

            AudioClip clipToPlay = _isOpen ? _openSfx : _closeSfx;
            if (clipToPlay != null)
            {
                AudioSource.PlayClipAtPoint(clipToPlay, transform.position);
            }

            AttentionManager.Emit(transform.position, _doorNoise, AttentionType.Door);
        }

        private IEnumerator AnimateDoor()
        {
            while (Quaternion.Angle(_doorHinge.localRotation, _targetRotation) > 0.5f)
            {
                _doorHinge.localRotation = Quaternion.Slerp(_doorHinge.localRotation, _targetRotation, Time.deltaTime * _animationSpeed);
                yield return null;
            }
            _doorHinge.localRotation = _targetRotation;
        }
    }
}

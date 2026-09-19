using UnityEngine;
using LateSubmission.Core;
using LateSubmission.Inventory;
using LateSubmission.Attention;

namespace LateSubmission.Interaction
{
    /// <summary>
    /// Checkpoint door at the end of the corridor.
    /// Requires a key, and when unlocked, triggers a smooth screen fade into the next scene.
    /// </summary>
    public class SceneTransitionDoor : MonoBehaviour, IInteractable
    {
        [Header("Scene Destination")]
        [SerializeField] private string _targetSceneName = "Floor02";
        [SerializeField] private string _destinationSpawnPoint = "";
        [SerializeField] private float _fadeDuration = 1.2f;

        [Header("Lock Settings")]
        [SerializeField] private bool _isLocked = true;
        [SerializeField] private ItemType _requiredKey = ItemType.FacultyKey;
        [SerializeField] private string _lockedMessage = "Locked. Needs Key from Security Room.";
        [SerializeField] private string _unlockedPrompt = "Unlock & Proceed to Next Floor [E]";

        public void Configure(string targetScene, string spawnPoint, bool isLocked, ItemType key, string lockedMsg, string unlockedPrompt)
        {
            _targetSceneName = targetScene;
            _destinationSpawnPoint = spawnPoint;
            _isLocked = isLocked;
            _requiredKey = key;
            _lockedMessage = lockedMsg;
            _unlockedPrompt = unlockedPrompt;
        }

        [Header("Audio")]
        [SerializeField] private AudioClip _unlockSfx;
        [SerializeField] private AudioClip _lockedSfx;

        private bool _isTransitioning = false;

        public string GetInteractionText()
        {
            if (_isTransitioning) return "Proceeding...";

            if (_isLocked)
            {
                if (InventoryManager.Instance != null && InventoryManager.Instance.HasItemType(_requiredKey))
                {
                    return _unlockedPrompt;
                }
                return _lockedMessage;
            }

            return _unlockedPrompt;
        }

        public bool CanInteract(Interactor interactor)
        {
            return !_isTransitioning;
        }

        public void Interact(Interactor interactor)
        {
            if (_isTransitioning) return;

            if (_isLocked)
            {
                if (InventoryManager.Instance != null && InventoryManager.Instance.HasItemType(_requiredKey))
                {
                    _isLocked = false;
                    TriggerTransition();
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

            TriggerTransition();
        }

        private void TriggerTransition()
        {
            _isTransitioning = true;
            if (!string.IsNullOrEmpty(_destinationSpawnPoint))
            {
                SceneTransitionManager.TargetSpawnPointName = _destinationSpawnPoint;
            }

            if (_unlockSfx != null)
            {
                AudioSource.PlayClipAtPoint(_unlockSfx, transform.position);
            }

            AttentionManager.Emit(transform.position, 6.0f, AttentionType.Door);

            if (SceneFader.Instance != null)
            {
                SceneFader.Instance.FadeOutAndLoadScene(_targetSceneName, _fadeDuration);
            }
            else
            {
                // Fallback direct load if SceneFader is not present in scene
                UnityEngine.SceneManagement.SceneManager.LoadScene(_targetSceneName);
            }
        }
    }
}

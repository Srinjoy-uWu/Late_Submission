using UnityEngine;
using LateSubmission.Inventory;
using LateSubmission.Attention;

namespace LateSubmission.Interaction
{
    /// <summary>
    /// Implements IInteractable for picking up components, keys, and documents.
    /// </summary>
    public class ItemPickup : MonoBehaviour, IInteractable
    {
        [SerializeField] private ItemData _itemData;
        [SerializeField] private AudioClip _pickupSfx;
        [SerializeField] private float _noiseStrength = 2.0f;

        public ItemData Item => _itemData;

        public string GetInteractionText()
        {
            if (_itemData != null)
            {
                return $"Take {_itemData.DisplayName} [E]";
            }
            return "Take Item [E]";
        }

        public bool CanInteract(Interactor interactor)
        {
            return _itemData != null;
        }

        public void Interact(Interactor interactor)
        {
            if (InventoryManager.Instance != null && _itemData != null)
            {
                bool added = InventoryManager.Instance.AddItem(_itemData);
                if (added)
                {
                    if (_pickupSfx != null)
                    {
                        AudioSource.PlayClipAtPoint(_pickupSfx, transform.position);
                    }

                    AttentionManager.Emit(transform.position, _noiseStrength, AttentionType.ItemPickup);
                    Destroy(gameObject);
                }
            }
        }
    }
}

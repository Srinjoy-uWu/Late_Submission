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

        private void Awake()
        {
            // Disable any non-convex child MeshColliders on rotating pickups so PhysX doesn't glitch
            foreach (var mc in GetComponentsInChildren<MeshCollider>(true))
            {
                if (mc != null && mc.gameObject != gameObject)
                {
                    mc.enabled = false;
                }
            }

            var box = GetComponent<BoxCollider>();
            if (box == null)
            {
                box = gameObject.AddComponent<BoxCollider>();
            }
            box.isTrigger = true;

            // Key pickups use a flat root scale (0.1, 0.02, 0.2) with the visual key elevated at local Y ~ 4.7
            if (transform.localScale.y < 0.05f)
            {
                box.center = new Vector3(0f, 4.5f, 0f);
                box.size = new Vector3(3.5f, 16.0f, 2.5f);
            }
            else if (box.size.x < 1.2f)
            {
                box.size = new Vector3(Mathf.Max(box.size.x, 1.4f), Mathf.Max(box.size.y, 1.8f), Mathf.Max(box.size.z, 1.4f));
            }
        }

        private void Start()
        {
            if (InventoryManager.Instance != null && _itemData != null && InventoryManager.Instance.HasItemType(_itemData.ItemType))
            {
                Destroy(gameObject);
            }
        }

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

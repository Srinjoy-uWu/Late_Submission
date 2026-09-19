using System;
using System.Collections.Generic;
using UnityEngine;

namespace LateSubmission.Inventory
{
    /// <summary>
    /// Lightweight singleton inventory manager.
    /// Tracks quest-critical keys and the 4 laser components.
    /// </summary>
    public class InventoryManager : MonoBehaviour
    {
        public static InventoryManager Instance { get; private set; }

        public event Action<ItemData> OnItemAdded;
        public event Action<ItemData> OnItemRemoved;
        public event Action OnInventoryUpdated;

        private readonly HashSet<ItemType> _collectedTypes = new HashSet<ItemType>();
        private readonly List<ItemData> _items = new List<ItemData>();

        public IReadOnlyList<ItemData> Items => _items;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public bool AddItem(ItemData item)
        {
            if (item == null) return false;

            if (!_items.Contains(item))
            {
                _items.Add(item);
                _collectedTypes.Add(item.ItemType);
                OnItemAdded?.Invoke(item);
                OnInventoryUpdated?.Invoke();
                return true;
            }
            return false;
        }

        public bool RemoveItem(ItemData item)
        {
            if (item == null) return false;

            if (_items.Remove(item))
            {
                _collectedTypes.Remove(item.ItemType);
                OnItemRemoved?.Invoke(item);
                OnInventoryUpdated?.Invoke();
                return true;
            }
            return false;
        }

        public bool HasItemType(ItemType type)
        {
            return _collectedTypes.Contains(type);
        }

        public bool HasAllLaserComponents()
        {
            return HasItemType(ItemType.Lens) &&
                   HasItemType(ItemType.BatteryPack) &&
                   HasItemType(ItemType.CircuitBoard) &&
                   HasItemType(ItemType.WeaponHousing);
        }

        public void ConsumeLaserComponents()
        {
            _items.RemoveAll(i => i.ItemType == ItemType.Lens ||
                                  i.ItemType == ItemType.BatteryPack ||
                                  i.ItemType == ItemType.CircuitBoard ||
                                  i.ItemType == ItemType.WeaponHousing);

            _collectedTypes.Remove(ItemType.Lens);
            _collectedTypes.Remove(ItemType.BatteryPack);
            _collectedTypes.Remove(ItemType.CircuitBoard);
            _collectedTypes.Remove(ItemType.WeaponHousing);

            OnInventoryUpdated?.Invoke();
        }
    }
}

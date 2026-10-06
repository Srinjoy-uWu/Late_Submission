using System;
using System.Collections.Generic;
using UnityEngine;

namespace LateSubmission.Inventory
{
    [System.Serializable]
    public class ArchivedNote
    {
        public string Title;
        public string LocationFound;
        public string Content;
    }

    /// <summary>
    /// Lightweight singleton inventory manager.
    /// Tracks quest-critical keys, 7 light gun components, and discovered story archives.
    /// </summary>
    public class InventoryManager : MonoBehaviour
    {
        public static InventoryManager Instance { get; private set; }

        public event Action<ItemData> OnItemAdded;
        public event Action<ItemData> OnItemRemoved;
        public event Action<ArchivedNote> OnNoteArchived;
        public event Action OnInventoryUpdated;

        private readonly HashSet<ItemType> _collectedTypes = new HashSet<ItemType>();
        private readonly List<ItemData> _items = new List<ItemData>();
        private readonly List<ArchivedNote> _archivedNotes = new List<ArchivedNote>();

        public IReadOnlyList<ItemData> Items => _items;
        public IReadOnlyList<ArchivedNote> ArchivedNotes => _archivedNotes;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (transform.parent != null)
            {
                transform.SetParent(null);
            }
            DontDestroyOnLoad(gameObject);
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

        public void ArchiveNote(string title, string locationFound, string content)
        {
            if (string.IsNullOrEmpty(title)) return;
            if (_archivedNotes.Exists(n => n.Title == title)) return;

            var note = new ArchivedNote
            {
                Title = title,
                LocationFound = string.IsNullOrEmpty(locationFound) ? "Floor 02 Archive" : locationFound,
                Content = content
            };

            _archivedNotes.Add(note);
            OnNoteArchived?.Invoke(note);
            OnInventoryUpdated?.Invoke();
            Debug.Log($"<color=cyan>[InventoryManager] Document Archived: '{title}' ({note.LocationFound})</color>");
        }

        public bool HasAllLaserComponents()
        {
            return HasItemType(ItemType.Lens) &&
                   HasItemType(ItemType.BatteryPack) &&
                   HasItemType(ItemType.CircuitBoard) &&
                   HasItemType(ItemType.WeaponHousing) &&
                   HasItemType(ItemType.ElectricalTape) &&
                   HasItemType(ItemType.Wires) &&
                   HasItemType(ItemType.Led);
        }

        public void ConsumeLaserComponents()
        {
            _items.RemoveAll(i => i.ItemType == ItemType.Lens ||
                                  i.ItemType == ItemType.BatteryPack ||
                                  i.ItemType == ItemType.CircuitBoard ||
                                  i.ItemType == ItemType.WeaponHousing ||
                                  i.ItemType == ItemType.ElectricalTape ||
                                  i.ItemType == ItemType.Wires ||
                                  i.ItemType == ItemType.Led);

            _collectedTypes.Remove(ItemType.Lens);
            _collectedTypes.Remove(ItemType.BatteryPack);
            _collectedTypes.Remove(ItemType.CircuitBoard);
            _collectedTypes.Remove(ItemType.WeaponHousing);
            _collectedTypes.Remove(ItemType.ElectricalTape);
            _collectedTypes.Remove(ItemType.Wires);
            _collectedTypes.Remove(ItemType.Led);

            OnInventoryUpdated?.Invoke();
        }

        /// <summary>Clears the entire inventory. Called at game start so the player begins with nothing.</summary>
        public void ClearAll()
        {
            _items.Clear();
            _collectedTypes.Clear();
            _archivedNotes.Clear();
            OnInventoryUpdated?.Invoke();
            Debug.Log("[InventoryManager] Inventory cleared — fresh start.");
        }
    }
}

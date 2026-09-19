using UnityEngine;

namespace LateSubmission.Inventory
{
    [CreateAssetMenu(fileName = "NewItemData", menuName = "Late Submission/Item Data")]
    public class ItemData : ScriptableObject
    {
        [SerializeField] private string _itemId;
        [SerializeField] private string _displayName;
        [TextArea(2, 4)]
        [SerializeField] private string _description;
        [SerializeField] private ItemType _itemType;
        [SerializeField] private Sprite _icon;

        public string ItemId => _itemId;
        public string DisplayName => _displayName;
        public string Description => _description;
        public ItemType ItemType => _itemType;
        public Sprite Icon => _icon;
    }
}

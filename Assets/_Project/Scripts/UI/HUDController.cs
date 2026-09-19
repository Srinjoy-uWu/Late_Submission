using UnityEngine;
using TMPro;
using LateSubmission.Interaction;
using LateSubmission.Objectives;
using LateSubmission.Inventory;
using LateSubmission.Weapon;

namespace LateSubmission.UI
{
    /// <summary>
    /// Master HUD controller managing crosshair, interaction prompt,
    /// objective text, story note modal, and laser charge counter.
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        [Header("Interaction & Crosshair")]
        [SerializeField] private GameObject _crosshair;
        [SerializeField] private TextMeshProUGUI _interactionPromptText;
        [SerializeField] private Interactor _interactor;

        [Header("Objectives")]
        [SerializeField] private TextMeshProUGUI _objectiveText;

        [Header("Note Reader Modal")]
        [SerializeField] private GameObject _noteModalPanel;
        [SerializeField] private TextMeshProUGUI _noteTitleText;
        [SerializeField] private TextMeshProUGUI _noteBodyText;

        [Header("Laser Weapon HUD")]
        [SerializeField] private GameObject _laserHudPanel;
        [SerializeField] private TextMeshProUGUI _laserChargesText;
        [SerializeField] private LaserWeapon _laserWeapon;

        [Header("Inventory Status")]
        [SerializeField] private GameObject _inventoryPanel;
        [SerializeField] private TextMeshProUGUI _componentListText;

        private void Start()
        {
            if (_interactor != null)
            {
                _interactor.OnInteractableHoverEnter += ShowInteractionPrompt;
                _interactor.OnInteractableHoverExit += HideInteractionPrompt;
            }

            if (ObjectiveManager.Instance != null)
            {
                ObjectiveManager.Instance.OnObjectiveUpdated += SetObjectiveText;
            }

            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.OnInventoryUpdated += UpdateInventoryDisplay;
            }

            if (_laserWeapon != null)
            {
                _laserWeapon.OnChargesChanged += UpdateLaserCharges;
            }

            InspectNote.OnOpenNoteReader += OpenNoteReader;

            HideInteractionPrompt();
            CloseNoteReader();
            UpdateInventoryDisplay();
        }

        private void OnDestroy()
        {
            if (_interactor != null)
            {
                _interactor.OnInteractableHoverEnter -= ShowInteractionPrompt;
                _interactor.OnInteractableHoverExit -= HideInteractionPrompt;
            }

            if (ObjectiveManager.Instance != null)
            {
                ObjectiveManager.Instance.OnObjectiveUpdated -= SetObjectiveText;
            }

            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.OnInventoryUpdated -= UpdateInventoryDisplay;
            }

            if (_laserWeapon != null)
            {
                _laserWeapon.OnChargesChanged -= UpdateLaserCharges;
            }

            InspectNote.OnOpenNoteReader -= OpenNoteReader;
        }

        private void Update()
        {
            // Close note modal if open
            if (_noteModalPanel != null && _noteModalPanel.activeSelf)
            {
                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.E))
                {
                    CloseNoteReader();
                }
            }

            // Toggle inventory display with Tab
            if (Input.GetKeyDown(KeyCode.Tab) && _inventoryPanel != null)
            {
                _inventoryPanel.SetActive(!_inventoryPanel.activeSelf);
            }
        }

        private void ShowInteractionPrompt(IInteractable interactable)
        {
            if (_interactionPromptText != null && interactable != null)
            {
                _interactionPromptText.text = interactable.GetInteractionText();
                _interactionPromptText.gameObject.SetActive(true);
            }
        }

        private void HideInteractionPrompt()
        {
            if (_interactionPromptText != null)
            {
                _interactionPromptText.gameObject.SetActive(false);
            }
        }

        private void SetObjectiveText(string text)
        {
            if (_objectiveText != null)
            {
                _objectiveText.text = text;
            }
        }

        public void OpenNoteReader(string title, string body)
        {
            if (_noteModalPanel != null)
            {
                _noteTitleText.text = title;
                _noteBodyText.text = body;
                _noteModalPanel.SetActive(true);
            }
        }

        public void CloseNoteReader()
        {
            if (_noteModalPanel != null)
            {
                _noteModalPanel.SetActive(false);
            }
        }

        private void UpdateLaserCharges(int charges)
        {
            if (_laserHudPanel != null)
            {
                _laserHudPanel.SetActive(true);
            }
            if (_laserChargesText != null)
            {
                _laserChargesText.text = $"Charges: {charges} / {_laserWeapon.MaxCharges}";
            }
        }

        private void UpdateInventoryDisplay()
        {
            if (_componentListText == null || InventoryManager.Instance == null) return;

            bool hasLens = InventoryManager.Instance.HasItemType(ItemType.Lens);
            bool hasBattery = InventoryManager.Instance.HasItemType(ItemType.BatteryPack);
            bool hasCircuit = InventoryManager.Instance.HasItemType(ItemType.CircuitBoard);
            bool hasHousing = InventoryManager.Instance.HasItemType(ItemType.WeaponHousing);

            _componentListText.text =
                $"<b>Laser Components:</b>\n" +
                $"[ {(hasLens ? "✓" : " ")} ] Focusing Lens\n" +
                $"[ {(hasBattery ? "✓" : " ")} ] Battery Pack\n" +
                $"[ {(hasCircuit ? "✓" : " ")} ] Circuit Board\n" +
                $"[ {(hasHousing ? "✓" : " ")} ] Weapon Housing";
        }
    }
}

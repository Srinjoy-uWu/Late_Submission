using System;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LateSubmission.Inventory;
using LateSubmission.Player;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace LateSubmission.UI
{
    /// <summary>
    /// Master controller for the full-screen Inventory & Notes Archive overlay.
    /// Toggled via [Tab] or [I]. Unlocks cursor, displays equipment & 7 components,
    /// and displays archived notes with their title, location found, and transcript.
    /// </summary>
    public class InventoryUIController : MonoBehaviour
    {
        [Header("UI Panels")]
        [SerializeField] private GameObject _mainInventoryPanel;
        [SerializeField] private GameObject _equipmentTabPanel;
        [SerializeField] private GameObject _notesArchiveTabPanel;

        [Header("Equipment & Crafting UI")]
        [SerializeField] private TextMeshProUGUI _equipmentStatusText;
        [SerializeField] private TextMeshProUGUI _componentsChecklistText;
        [SerializeField] private TextMeshProUGUI _craftingPromptText;

        [Header("Notes Archive UI")]
        [SerializeField] private Transform _notesListContainer;
        [SerializeField] private GameObject _noteButtonPrefab;
        [SerializeField] private TextMeshProUGUI _activeNoteTitleText;
        [SerializeField] private TextMeshProUGUI _activeNoteLocationText;
        [SerializeField] private TextMeshProUGUI _activeNoteBodyText;

        [Header("Audio")]
        [SerializeField] private AudioClip _openInventorySfx;
        [SerializeField] private AudioClip _closeInventorySfx;
        [SerializeField] private AudioSource _audioSource;

        public static InventoryUIController Instance { get; private set; }
        public bool IsOpen => _mainInventoryPanel != null && _mainInventoryPanel.activeSelf;

        private FPSController _fpsController;
        private int _selectedNoteIndex = 0;

        private void Awake()
        {
            Instance = this;
            if (_audioSource == null) _audioSource = GetComponent<AudioSource>();
        }

        private void Start()
        {
            _fpsController = UnityEngine.Object.FindFirstObjectByType<FPSController>();

            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.OnInventoryUpdated += RefreshAllUI;
                InventoryManager.Instance.OnNoteArchived += HandleNoteArchived;
            }

            if (_mainInventoryPanel != null)
            {
                _mainInventoryPanel.SetActive(false);
            }

            ShowEquipmentTab();
        }

        private void OnDestroy()
        {
            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.OnInventoryUpdated -= RefreshAllUI;
                InventoryManager.Instance.OnNoteArchived -= HandleNoteArchived;
            }
        }

        private void Update()
        {
            bool togglePressed = false;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (Keyboard.current.tabKey.wasPressedThisFrame || Keyboard.current.iKey.wasPressedThisFrame)
                {
                    togglePressed = true;
                }
                if (IsOpen && Keyboard.current.escapeKey.wasPressedThisFrame)
                {
                    Close();
                    return;
                }
            }
#endif
            if (!togglePressed)
            {
                try
                {
                    if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.I)) togglePressed = true;
                    if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
                    {
                        Close();
                        return;
                    }
                }
                catch {}
            }

            if (togglePressed)
            {
                Toggle();
            }
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public void Open()
        {
            if (_mainInventoryPanel == null) return;

            _mainInventoryPanel.SetActive(true);
            RefreshAllUI();

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (_fpsController != null)
            {
                _fpsController.enabled = false;
            }

            if (_openInventorySfx != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(_openInventorySfx);
            }
        }

        public void Close()
        {
            if (_mainInventoryPanel == null) return;

            _mainInventoryPanel.SetActive(false);

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (_fpsController != null)
            {
                _fpsController.enabled = true;
            }

            if (_closeInventorySfx != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(_closeInventorySfx);
            }
        }

        public void ShowEquipmentTab()
        {
            if (_equipmentTabPanel != null) _equipmentTabPanel.SetActive(true);
            if (_notesArchiveTabPanel != null) _notesArchiveTabPanel.SetActive(false);
            RefreshEquipmentUI();
        }

        public void ShowNotesArchiveTab()
        {
            if (_equipmentTabPanel != null) _equipmentTabPanel.SetActive(false);
            if (_notesArchiveTabPanel != null) _notesArchiveTabPanel.SetActive(true);
            RefreshNotesArchiveUI();
        }

        private void RefreshAllUI()
        {
            RefreshEquipmentUI();
            RefreshNotesArchiveUI();
        }

        private void RefreshEquipmentUI()
        {
            if (InventoryManager.Instance == null) return;

            var inv = InventoryManager.Instance;

            // Equipment status
            if (_equipmentStatusText != null)
            {
                var sbEquip = new StringBuilder();
                sbEquip.AppendLine("<b>EQUIPPED TOOLS:</b>");

                var weapon = UnityEngine.Object.FindFirstObjectByType<Weapon.MergedLightPistol>();
                if (weapon != null && weapon.gameObject.activeInHierarchy)
                {
                    sbEquip.AppendLine($"• <color=#00FFCC>Tactical Photonic Blaster</color> (Charges: {weapon.CurrentCharges}/{weapon.MaxCharges})");
                    sbEquip.AppendLine($"  Mounted Flashlight: {(weapon.IsFlashlightOn ? "<color=green>ON [F]</color>" : "<color=gray>OFF [F]</color>")}");
                }
                else
                {
                    bool hasFlashlight = inv.HasItemType(ItemType.Flashlight);
                    if (!hasFlashlight)
                    {
                        sbEquip.AppendLine("• Handheld Flashlight (Not Found)");
                    }
                    else if (Weapon.HeldItemController.Instance != null && Weapon.HeldItemController.Instance.IsFlashlightDepleted)
                    {
                        sbEquip.AppendLine("• Handheld Flashlight <color=#FF5555>(Battery Depleted — Overloaded)</color>");
                    }
                    else if (Weapon.HeldItemController.Instance != null && Weapon.HeldItemController.Instance.IsFlashlightOn)
                    {
                        sbEquip.AppendLine("• Handheld Flashlight <color=#00FF66>(ON [F])</color>");
                    }
                    else
                    {
                        sbEquip.AppendLine("• Handheld Flashlight <color=#CCCCCC>(OFF [F])</color>");
                    }
                }

                if (inv.HasItemType(ItemType.FacultyKey)) sbEquip.AppendLine("• Faculty Master Key");
                if (inv.HasItemType(ItemType.Floor02Key)) sbEquip.AppendLine("• Floor 02 Lab Key");

                _equipmentStatusText.text = sbEquip.ToString();
            }

            // Components checklist
            if (_componentsChecklistText != null)
            {
                var sb = new StringBuilder();
                sb.AppendLine("<b>LIGHT GUN COMPONENTS:</b>");
                sb.AppendLine(FormatComponentLine("Focusing Optical Lens", ItemType.Lens, "Mechatronics Lab"));
                sb.AppendLine(FormatComponentLine("High-Voltage Battery", ItemType.BatteryPack, "Mechatronics Lab"));
                sb.AppendLine(FormatComponentLine("Amplifier Circuit Board", ItemType.CircuitBoard, "Mechatronics Lab"));
                sb.AppendLine(FormatComponentLine("Chassis Housing", ItemType.WeaponHousing, "Mechatronics Lab"));
                sb.AppendLine(FormatComponentLine("Conductive Copper Wires", ItemType.Wires, "EC Lab"));
                sb.AppendLine(FormatComponentLine("Focusing Diode (LED)", ItemType.Led, "EC Lab"));
                sb.AppendLine(FormatComponentLine("Insulating Electrical Tape", ItemType.ElectricalTape, "EC Lab"));

                _componentsChecklistText.text = sb.ToString();
            }

            // Crafting readiness
            if (_craftingPromptText != null)
            {
                if (inv.HasAllLaserComponents())
                {
                    _craftingPromptText.text = "<color=#00FF66><b>STATUS: READY!</b> Assemble the weapon at the Central Workbench in Mechatronics Lab [E].</color>";
                }
                else
                {
                    _craftingPromptText.text = "<color=#FFCC00><b>STATUS INCOMPLETE:</b> Search Mechatronics and EC Labs for remaining components.</color>";
                }
            }
        }

        private string FormatComponentLine(string label, ItemType type, string location)
        {
            bool has = InventoryManager.Instance != null && InventoryManager.Instance.HasItemType(type);
            if (has)
            {
                return $"  <color=#00FF66>[x] {label}</color> <color=#888888>({location})</color>";
            }
            return $"  <color=#AAAAAA>[ ] {label}</color> <color=#666666>({location})</color>";
        }

        private void RefreshNotesArchiveUI()
        {
            if (InventoryManager.Instance == null) return;

            var notes = InventoryManager.Instance.ArchivedNotes;

            if (_activeNoteTitleText != null && _activeNoteBodyText != null)
            {
                if (notes.Count == 0)
                {
                    _activeNoteTitleText.text = "No Documents Discovered";
                    if (_activeNoteLocationText != null) _activeNoteLocationText.text = "";
                    _activeNoteBodyText.text = "Explore the academic block and inspect notes [E] to archive documents here.";
                }
                else
                {
                    if (_selectedNoteIndex < 0 || _selectedNoteIndex >= notes.Count)
                    {
                        _selectedNoteIndex = 0;
                    }

                    var activeNote = notes[_selectedNoteIndex];
                    _activeNoteTitleText.text = $"<b>{activeNote.Title}</b>";
                    if (_activeNoteLocationText != null)
                    {
                        _activeNoteLocationText.text = $"<color=#88CCFF><i>{activeNote.LocationFound}</i></color>";
                    }
                    _activeNoteBodyText.text = activeNote.Content;
                }
            }
        }

        public void SelectNoteIndex(int index)
        {
            _selectedNoteIndex = index;
            RefreshNotesArchiveUI();
        }

        private void HandleNoteArchived(ArchivedNote note)
        {
            RefreshNotesArchiveUI();
        }
    }
}

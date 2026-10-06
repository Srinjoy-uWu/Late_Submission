using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LateSubmission.Interaction;
using LateSubmission.Objectives;
using LateSubmission.Inventory;
using LateSubmission.Weapon;
using LateSubmission.Player;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace LateSubmission.UI
{
    /// <summary>
    /// Master HUD controller managing crosshair, interaction prompt,
    /// objective text, story note modal, laser charge counter, player health bar,
    /// damage flash vignette, and death overlay.
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        [Header("Interaction & Crosshair")]
        [SerializeField] private GameObject _crosshair;
        [SerializeField] private TextMeshProUGUI _interactionPromptText;
        [SerializeField] private Interactor _interactor;

        [Header("Player Health HUD")]
        [SerializeField] private Slider _healthSlider;
        [SerializeField] private Image _healthFillImage;
        [SerializeField] private TextMeshProUGUI _healthText;
        [SerializeField] private Image _damageVignetteImage;
        [SerializeField] private GameObject _deathOverlayPanel;

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

        public static HUDController Instance { get; private set; }

        private int _noteOpenedFrame = -1;
        private int _noteClosedFrame = -1;
        private Coroutine _flashRoutine;

        public bool IsNoteOpen => _noteModalPanel != null && _noteModalPanel.activeSelf;
        public bool JustClosedNote => Time.frameCount == _noteClosedFrame;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            if (_interactor == null)
            {
                _interactor = Object.FindFirstObjectByType<Interactor>();
            }

            if (_interactor != null)
            {
                _interactor.OnInteractableHoverEnter += ShowInteractionPrompt;
                _interactor.OnInteractableHoverExit += HideInteractionPrompt;
            }

            if (ObjectiveManager.Instance != null)
            {
                ObjectiveManager.Instance.OnObjectiveUpdated += SetObjectiveText;
                ObjectiveManager.Instance.RefreshObjectiveText();
            }

            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.OnInventoryUpdated += UpdateInventoryDisplay;
            }

            if (HeldItemController.Instance != null)
            {
                HeldItemController.Instance.OnFlashlightStateChanged += UpdateInventoryDisplay;
            }

            if (_laserWeapon != null)
            {
                _laserWeapon.OnChargesChanged += UpdateLaserCharges;
            }

            if (PlayerHealth.Instance != null)
            {
                PlayerHealth.Instance.OnHealthChanged += UpdateHealthDisplay;
                PlayerHealth.Instance.OnPlayerDamaged += TriggerDamageFlash;
                PlayerHealth.Instance.OnPlayerDied += ShowDeathScreen;
                PlayerHealth.Instance.OnPlayerRespawned += HideDeathScreen;
                UpdateHealthDisplay(PlayerHealth.Instance.CurrentHealth, PlayerHealth.Instance.MaxHealth);
            }

            InspectNote.OnOpenNoteReader += OpenNoteReader;

            HideInteractionPrompt();
            CloseNoteReader();
            UpdateInventoryDisplay();
            HideDeathScreen();
            if (_damageVignetteImage != null) _damageVignetteImage.gameObject.SetActive(false);
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

            if (HeldItemController.Instance != null)
            {
                HeldItemController.Instance.OnFlashlightStateChanged -= UpdateInventoryDisplay;
            }

            if (_laserWeapon != null)
            {
                _laserWeapon.OnChargesChanged -= UpdateLaserCharges;
            }

            if (PlayerHealth.Instance != null)
            {
                PlayerHealth.Instance.OnHealthChanged -= UpdateHealthDisplay;
                PlayerHealth.Instance.OnPlayerDamaged -= TriggerDamageFlash;
                PlayerHealth.Instance.OnPlayerDied -= ShowDeathScreen;
                PlayerHealth.Instance.OnPlayerRespawned -= HideDeathScreen;
            }

            InspectNote.OnOpenNoteReader -= OpenNoteReader;
        }

        private void Update()
        {
            // Close note modal if open
            if (_noteModalPanel != null && _noteModalPanel.activeSelf)
            {
                if (Time.frameCount == _noteOpenedFrame) return;

                bool close = false;
#if ENABLE_INPUT_SYSTEM
                if (Keyboard.current != null && (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.eKey.wasPressedThisFrame))
                {
                    close = true;
                }
#endif
                if (!close)
                {
                    try
                    {
                        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.E)) close = true;
                    }
                    catch {}
                }

                if (close) CloseNoteReader();
            }
        }

        public void UpdateHealthDisplay(float current, float max)
        {
            if (_healthSlider != null)
            {
                _healthSlider.maxValue = max;
                _healthSlider.value = current;
            }

            if (_healthFillImage != null && max > 0f)
            {
                _healthFillImage.fillAmount = current / max;
            }

            if (_healthText != null)
            {
                _healthText.text = $"HEALTH: {(int)current} / {(int)max}";
            }
        }

        public void TriggerDamageFlash(float damage)
        {
            if (_damageVignetteImage != null)
            {
                if (_flashRoutine != null) StopCoroutine(_flashRoutine);
                _flashRoutine = StartCoroutine(DamageFlashRoutine());
            }
        }

        private IEnumerator DamageFlashRoutine()
        {
            if (_damageVignetteImage == null) yield break;
            _damageVignetteImage.gameObject.SetActive(true);
            Color c = _damageVignetteImage.color;
            c.a = 0.65f;
            _damageVignetteImage.color = c;

            float elapsed = 0f;
            while (elapsed < 0.45f)
            {
                elapsed += Time.deltaTime;
                c.a = Mathf.Lerp(0.65f, 0f, elapsed / 0.45f);
                _damageVignetteImage.color = c;
                yield return null;
            }

            _damageVignetteImage.gameObject.SetActive(false);
            _flashRoutine = null;
        }

        public void ShowDeathScreen()
        {
            if (_deathOverlayPanel != null)
            {
                _deathOverlayPanel.SetActive(true);
            }
        }

        public void HideDeathScreen()
        {
            if (_deathOverlayPanel != null)
            {
                _deathOverlayPanel.SetActive(false);
            }
        }

        public void ShowInteractionPrompt(IInteractable interactable)
        {
            if (_interactionPromptText == null) return;
            string text = interactable.GetInteractionText();
            if (string.IsNullOrEmpty(text))
            {
                HideInteractionPrompt();
                return;
            }

            _interactionPromptText.text = text;
            _interactionPromptText.gameObject.SetActive(true);
        }

        public void HideInteractionPrompt()
        {
            if (_interactionPromptText != null)
            {
                _interactionPromptText.gameObject.SetActive(false);
            }
        }

        public void SetObjectiveText(string text)
        {
            if (_objectiveText != null)
            {
                _objectiveText.text = text;
            }
        }

        public void UpdateLaserCharges(int current)
        {
            if (_laserHudPanel == null) return;
            int max = _laserWeapon != null ? _laserWeapon.MaxCharges : 3;
            if (current <= 0 && max <= 0)
            {
                _laserHudPanel.SetActive(false);
                return;
            }

            _laserHudPanel.SetActive(true);
            if (_laserChargesText != null)
            {
                _laserChargesText.text = $"LASER CHARGES: {current}/{max}";
            }
        }

        public void UpdateInventoryDisplay()
        {
            if (_inventoryPanel == null || _componentListText == null) return;
            if (InventoryManager.Instance == null) return;

            var items = InventoryManager.Instance.Items;
            if (items == null || items.Count == 0)
            {
                _inventoryPanel.SetActive(false);
                return;
            }

            _inventoryPanel.SetActive(true);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<b>EQUIPMENT & ITEMS:</b>");
            foreach (var item in items)
            {
                if (item == null) continue;
                if (item.ItemType == ItemType.Flashlight)
                {
                    if (HeldItemController.Instance != null && HeldItemController.Instance.IsFlashlightDepleted)
                    {
                        sb.AppendLine($"• {item.DisplayName} <color=#FF5555>(Depleted)</color>");
                    }
                    else if (HeldItemController.Instance != null && HeldItemController.Instance.IsFlashlightOn)
                    {
                        sb.AppendLine($"• {item.DisplayName} <color=#00FF66>[F: ON]</color>");
                    }
                    else
                    {
                        sb.AppendLine($"• {item.DisplayName} <color=#CCCCCC>[F: OFF]</color>");
                    }
                }
                else
                {
                    sb.AppendLine($"• {item.DisplayName}");
                }
            }
            _componentListText.text = sb.ToString();
        }

        public void OpenNoteReader(string title, string body)
        {
            if (_noteModalPanel == null) return;

            if (_noteTitleText != null) _noteTitleText.text = title;
            if (_noteBodyText != null) _noteBodyText.text = body;

            _noteModalPanel.SetActive(true);
            _noteOpenedFrame = Time.frameCount;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            var fps = Object.FindFirstObjectByType<FPSController>();
            if (fps != null) fps.enabled = false;
        }

        public void CloseNoteReader()
        {
            if (_noteModalPanel == null) return;

            _noteModalPanel.SetActive(false);
            _noteClosedFrame = Time.frameCount;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            var fps = Object.FindFirstObjectByType<FPSController>();
            if (fps != null) fps.enabled = true;
        }
    }
}

using System;
using System.Collections;
using UnityEngine;
using LateSubmission.Inventory;
using LateSubmission.Player;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace LateSubmission.Weapon
{
    public enum HeldItemType
    {
        None,
        Flashlight,
        LightPistol,
        Key,
        GenericItem
    }

    /// <summary>
    /// Master first-person held item controller attached to PlayerCamera.
    /// Manages the flashlight beam (after the Flashlight item is collected in Cabin C104),
    /// crafted merged light pistol viewmodel, and inspected keys/items.
    /// The 3D flashlight viewmodel mesh on the player's hands is kept hidden at all times.
    /// </summary>
    public class HeldItemController : MonoBehaviour
    {
        [Header("Viewmodel References")]
        [SerializeField] private GameObject _flashlightViewmodel;
        [SerializeField] private GameObject _pistolViewmodel;
        [SerializeField] private GameObject _keyViewmodel;

        [Header("Flashlight Settings")]
        [SerializeField] private Light _flashlightSpotlight;
        [SerializeField] private AudioClip _flashlightToggleSfx;
        [SerializeField] private AudioClip _batteryDiedSfx;

        [Header("Active Item")]
        [SerializeField] private HeldItemType _currentHeldType = HeldItemType.None;

        private static bool _persistedFlashlightOn = false;

        private AudioSource _audioSource;
        private bool _isFlashlightOn = false;
        private bool _isFlashlightDepleted = false;

        public static HeldItemController Instance { get; private set; }
        public event Action OnFlashlightStateChanged;

        public HeldItemType CurrentHeldType => _currentHeldType;
        public bool IsFlashlightOn => _isFlashlightOn && !_isFlashlightDepleted;
        public bool IsFlashlightDepleted => _isFlashlightDepleted;
        public Light FlashlightSpotlight => _flashlightSpotlight;

        private bool HasCollectedFlashlight =>
            InventoryManager.Instance != null && InventoryManager.Instance.HasItemType(ItemType.Flashlight);

        private void Awake()
        {
            Instance = this;
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
                _audioSource.spatialBlend = 0f; // 2D player audio
            }

            _isFlashlightDepleted = false;
            _isFlashlightOn = false;

            if (_flashlightViewmodel != null)
            {
                _flashlightViewmodel.transform.localRotation = Quaternion.identity;
                var vmCtrl = _flashlightViewmodel.GetComponent<ViewmodelController>();
                if (vmCtrl != null) vmCtrl.enabled = false;
            }

            if (_flashlightSpotlight != null)
            {
                // Always align spotlight straight ahead along PlayerCamera.forward
                _flashlightSpotlight.transform.localRotation = Quaternion.identity;
                _flashlightSpotlight.enabled = false;
            }

            var mergedPistol = GetComponentInChildren<MergedLightPistol>(true);
            if (mergedPistol != null && _pistolViewmodel != mergedPistol.gameObject)
            {
                if (_pistolViewmodel != null)
                {
                    _pistolViewmodel.SetActive(false);
                }
                _pistolViewmodel = mergedPistol.gameObject;
            }

            HideFlashlightViewmodelMesh();
        }

        private void Start()
        {
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

            // On Floor 02, the player has progressed past Floor 01 Cabin 104 and must always have the Flashlight in inventory
            if (sceneName == "Floor02_Main" && InventoryManager.Instance != null && !InventoryManager.Instance.HasItemType(ItemType.Flashlight))
            {
                var fallbackFlashlight = ScriptableObject.CreateInstance<ItemData>();
                fallbackFlashlight.Configure(ItemType.Flashlight, "Flashlight", "Standard-issue security flashlight.");
                InventoryManager.Instance.AddItem(fallbackFlashlight);
            }

            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.OnInventoryUpdated += RefreshHeldState;
            }

            RefreshHeldState();
            UpdateViewmodelVisibility();

            // Restore persisted flashlight state if the player owns the flashlight and it is not depleted
            if (HasCollectedFlashlight && !_isFlashlightDepleted && _currentHeldType != HeldItemType.LightPistol)
            {
                _isFlashlightOn = _persistedFlashlightOn;
                if (_flashlightSpotlight != null)
                {
                    _flashlightSpotlight.transform.localRotation = Quaternion.identity;
                    _flashlightSpotlight.enabled = _isFlashlightOn;
                }
            }
            else
            {
                _isFlashlightOn = false;
                _persistedFlashlightOn = false;
                if (_flashlightSpotlight != null)
                {
                    _flashlightSpotlight.enabled = false;
                }
            }

            OnFlashlightStateChanged?.Invoke();
        }

        private void OnDestroy()
        {
            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.OnInventoryUpdated -= RefreshHeldState;
            }
        }

        private void Update()
        {
            CheckFlashlightInput();
        }

        private void CheckFlashlightInput()
        {
            bool togglePressed = false;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
            {
                togglePressed = true;
            }
#endif
            if (!togglePressed)
            {
                try
                {
                    if (Input.GetKeyDown(KeyCode.F)) togglePressed = true;
                }
                catch {}
            }

            if (togglePressed)
            {
                // Player can only use the flashlight after collecting it in Prof. Anish Mondal's room (Cabin C104)
                // and before it is depleted upon entering the Mechatronics Lab on Floor 02
                if (HasCollectedFlashlight && !_isFlashlightDepleted && _currentHeldType != HeldItemType.LightPistol)
                {
                    ToggleFlashlight();
                }
            }
        }

        public void ToggleFlashlight()
        {
            if (!HasCollectedFlashlight || _isFlashlightDepleted) return;

            _isFlashlightOn = !_isFlashlightOn;
            _persistedFlashlightOn = _isFlashlightOn;

            if (_flashlightSpotlight != null)
            {
                // Ensure parent object is active so the spotlight component can emit light in the scene
                if (_flashlightViewmodel != null && !_flashlightViewmodel.activeSelf)
                {
                    _flashlightViewmodel.SetActive(true);
                    HideFlashlightViewmodelMesh();
                }
                _flashlightSpotlight.transform.localRotation = Quaternion.identity;
                _flashlightSpotlight.enabled = _isFlashlightOn;
            }

            if (_flashlightToggleSfx != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(_flashlightToggleSfx, 0.8f);
            }

            OnFlashlightStateChanged?.Invoke();
        }

        public void OnFlashlightDepleted()
        {
            _isFlashlightDepleted = true;
            _isFlashlightOn = false;
            _persistedFlashlightOn = false;

            if (_flashlightSpotlight != null)
            {
                _flashlightSpotlight.enabled = false;
            }

            if (_batteryDiedSfx != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(_batteryDiedSfx, 1.0f);
            }

            OnFlashlightStateChanged?.Invoke();
        }

        public void EquipPistol()
        {
            _currentHeldType = HeldItemType.LightPistol;
            _isFlashlightOn = false;
            _persistedFlashlightOn = false;
            if (_flashlightSpotlight != null)
            {
                _flashlightSpotlight.enabled = false;
            }
            UpdateViewmodelVisibility();
            OnFlashlightStateChanged?.Invoke();
            Debug.Log("<color=green>[HeldItemController] Equipped Merged Light Pistol!</color>");
        }

        public void EquipKey(bool show)
        {
            if (show)
            {
                _currentHeldType = HeldItemType.Key;
            }
            else
            {
                if (InventoryManager.Instance != null && InventoryManager.Instance.HasItemType(ItemType.LightPistol))
                {
                    _currentHeldType = HeldItemType.LightPistol;
                }
                else if (HasCollectedFlashlight)
                {
                    _currentHeldType = HeldItemType.Flashlight;
                }
                else
                {
                    _currentHeldType = HeldItemType.None;
                }
            }
            UpdateViewmodelVisibility();
        }

        private void RefreshHeldState()
        {
            if (InventoryManager.Instance != null && InventoryManager.Instance.HasItemType(ItemType.LightPistol))
            {
                EquipPistol();
                return;
            }

            if (HasCollectedFlashlight)
            {
                if (_currentHeldType == HeldItemType.None)
                {
                    _currentHeldType = HeldItemType.Flashlight;
                }
            }
            else
            {
                if (_currentHeldType == HeldItemType.Flashlight)
                {
                    _currentHeldType = HeldItemType.None;
                }
                _isFlashlightOn = false;
                if (_flashlightSpotlight != null)
                {
                    _flashlightSpotlight.enabled = false;
                }
            }

            UpdateViewmodelVisibility();
        }

        private void HideFlashlightViewmodelMesh()
        {
            if (_flashlightViewmodel == null) return;

            // Never show the 3D flashlight mesh on the player's hands
            var renderers = _flashlightViewmodel.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (r != null) r.enabled = false;
            }

            Transform meshChild = _flashlightViewmodel.transform.Find("Flashlight_Mesh");
            if (meshChild != null)
            {
                meshChild.gameObject.SetActive(false);
            }
        }

        public void UpdateViewmodelVisibility()
        {
            // Keep _flashlightViewmodel GameObject active only so its child FlashlightBeam Light can work,
            // but ALWAYS hide its 3D mesh renderers so no flashlight is shown on the player's hands.
            if (_flashlightViewmodel != null)
            {
                _flashlightViewmodel.SetActive(true);
                HideFlashlightViewmodelMesh();
            }

            if (_pistolViewmodel != null)
            {
                _pistolViewmodel.SetActive(_currentHeldType == HeldItemType.LightPistol);
            }

            if (_keyViewmodel != null)
            {
                _keyViewmodel.SetActive(_currentHeldType == HeldItemType.Key);
            }
        }
    }
}

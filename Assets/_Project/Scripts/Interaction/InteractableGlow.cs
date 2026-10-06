using UnityEngine;
using LateSubmission.Inventory;

namespace LateSubmission.Interaction
{
    public enum GlowCategory
    {
        Custom,
        Note,           // Warm Amber, pulses on papers/logs
        Pickup,         // Bright Gold/Brass, pulses on keys/items
        UnlockableDoor  // Atmospheric Mint Green, pulses when door is unlocked or key is owned
    }

    /// <summary>
    /// Universal visual affordance component that adds a smooth, non-destructive emissive glow
    /// and optional subtle localized halo light to notes, pickups, and unlockable doors.
    /// </summary>
    public class InteractableGlow : MonoBehaviour
    {
        [Header("Glow Configuration")]
        [SerializeField] private GlowCategory _category = GlowCategory.Custom;
        [SerializeField] private Color _glowColor = new Color(1.0f, 0.72f, 0.28f);
        [SerializeField] private float _minIntensity = 0.20f;
        [SerializeField] private float _maxIntensity = 0.85f;
        [SerializeField] private float _pulseSpeed = 2.4f;
        [SerializeField] private float _hoverMultiplier = 1.35f;

        [Header("Conditional Unlock (For Doors)")]
        [SerializeField] private bool _onlyWhenUnlockable = false;

        [Header("Subtle Light Halo (For small items)")]
        [SerializeField] private bool _useSubtleLightHalo = false;
        [SerializeField] private float _haloRange = 0.8f;
        [SerializeField] private float _haloIntensity = 0.4f;

        [Header("Target Renderers (Leave empty to auto-detect)")]
        [SerializeField] private Renderer[] _targetRenderers;

        private MaterialPropertyBlock _propBlock;
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private Light _haloLight;
        private bool _isHovered = false;
        private bool _isUnlockable = true;
        private IInteractable _interactableComponent;
        private Interactor _interactor;

        private Door _doorComponent;
        private SceneTransitionDoor _sceneDoorComponent;

        public void Configure(GlowCategory category)
        {
            _category = category;
            ApplyCategoryDefaults();
        }

        private void Awake()
        {
            _propBlock = new MaterialPropertyBlock();
            _interactableComponent = GetComponent<IInteractable>();
            _doorComponent = GetComponent<Door>();
            _sceneDoorComponent = GetComponent<SceneTransitionDoor>();

            if (_doorComponent != null || _sceneDoorComponent != null)
            {
                _onlyWhenUnlockable = true;
                if (_category == GlowCategory.Custom)
                {
                    _category = GlowCategory.UnlockableDoor;
                }
            }

            ApplyCategoryDefaults();

            // Auto-detect renderers if not explicitly set
            if (_targetRenderers == null || _targetRenderers.Length == 0)
            {
                _targetRenderers = GetComponentsInChildren<Renderer>(true);
            }

            // Setup optional subtle point light halo
            if (_useSubtleLightHalo)
            {
                var haloGo = new GameObject("GlowHaloLight");
                haloGo.transform.SetParent(transform, true);
                haloGo.transform.position = transform.position + Vector3.up * 0.08f;
                _haloLight = haloGo.AddComponent<Light>();
                _haloLight.type = LightType.Point;
                _haloLight.range = _haloRange;
                _haloLight.color = _glowColor;
                _haloLight.intensity = _haloIntensity;
                _haloLight.shadows = LightShadows.None;
            }
        }

        private bool _subscribedInventory = false;
        private float _nextUnlockCheck = 0f;

        private void Start()
        {
            _interactor = Object.FindFirstObjectByType<Interactor>();
            if (_interactor != null)
            {
                _interactor.OnInteractableHoverEnter += OnHoverEnter;
                _interactor.OnInteractableHoverExit += OnHoverExit;
            }

            TrySubscribeInventory();
            EvaluateUnlockState();
        }

        private void TrySubscribeInventory()
        {
            if (!_subscribedInventory && InventoryManager.Instance != null)
            {
                InventoryManager.Instance.OnInventoryUpdated += EvaluateUnlockState;
                _subscribedInventory = true;
            }
        }

        private void OnDestroy()
        {
            if (_interactor != null)
            {
                _interactor.OnInteractableHoverEnter -= OnHoverEnter;
                _interactor.OnInteractableHoverExit -= OnHoverExit;
            }

            if (_subscribedInventory && InventoryManager.Instance != null)
            {
                InventoryManager.Instance.OnInventoryUpdated -= EvaluateUnlockState;
                _subscribedInventory = false;
            }
        }

        private void Update()
        {
            if (!_subscribedInventory)
            {
                TrySubscribeInventory();
            }

            if (_onlyWhenUnlockable && Time.time >= _nextUnlockCheck)
            {
                _nextUnlockCheck = Time.time + 0.25f;
                EvaluateUnlockState();
            }

            if (_onlyWhenUnlockable && !_isUnlockable)
            {
                SetEmissionZero();
                if (_haloLight != null) _haloLight.enabled = false;
                return;
            }

            // Smooth sine pulsation
            float pulse = Mathf.Sin(Time.time * _pulseSpeed) * 0.5f + 0.5f;
            float baseIntensity = Mathf.Lerp(_minIntensity, _maxIntensity, pulse);
            float currentIntensity = baseIntensity * (_isHovered ? _hoverMultiplier : 1.0f);
            Color finalEmission = _glowColor * currentIntensity;

            ApplyEmissionColor(finalEmission);

            if (_haloLight != null)
            {
                _haloLight.enabled = true;
                _haloLight.intensity = _haloIntensity * (pulse * 0.35f + 0.65f) * (_isHovered ? _hoverMultiplier : 1.0f);
            }
        }

        private void EvaluateUnlockState()
        {
            if (!_onlyWhenUnlockable)
            {
                _isUnlockable = true;
                return;
            }

            if (_doorComponent != null)
            {
                if (!_doorComponent.IsLocked)
                {
                    _isUnlockable = true;
                }
                else if (InventoryManager.Instance != null && InventoryManager.Instance.HasItemType(_doorComponent.RequiredKey))
                {
                    _isUnlockable = true;
                }
                else
                {
                    _isUnlockable = false;
                }
                return;
            }

            if (_sceneDoorComponent != null)
            {
                if (!_sceneDoorComponent.IsLocked)
                {
                    _isUnlockable = true;
                }
                else if (InventoryManager.Instance != null && InventoryManager.Instance.HasItemType(_sceneDoorComponent.RequiredKey))
                {
                    _isUnlockable = true;
                }
                else
                {
                    _isUnlockable = false;
                }
                return;
            }

            _isUnlockable = true;
        }

        private void OnHoverEnter(IInteractable interactable)
        {
            if (interactable != null)
            {
                if (interactable == _interactableComponent || (Component)interactable == this)
                {
                    _isHovered = true;
                }
                else if (interactable is Component comp)
                {
                    if (comp.gameObject == gameObject || comp.transform.IsChildOf(transform) || transform.IsChildOf(comp.transform))
                    {
                        _isHovered = true;
                    }
                }
            }
        }

        private void OnHoverExit()
        {
            _isHovered = false;
        }

        private void ApplyEmissionColor(Color color)
        {
            if (_targetRenderers == null) return;

            for (int i = 0; i < _targetRenderers.Length; i++)
            {
                var r = _targetRenderers[i];
                if (r == null) continue;

                r.GetPropertyBlock(_propBlock);
                _propBlock.SetColor(EmissionColorId, color);
                r.SetPropertyBlock(_propBlock);
            }
        }

        private void SetEmissionZero()
        {
            ApplyEmissionColor(Color.black);
        }

        private void ApplyCategoryDefaults()
        {
            switch (_category)
            {
                case GlowCategory.Note:
                    // Warm Amber: subtle paper-in-dim-light effect
                    _glowColor = new Color(1.0f, 0.68f, 0.22f);
                    _minIntensity = 0.08f;
                    _maxIntensity = 0.28f;
                    _pulseSpeed = 1.8f;
                    _useSubtleLightHalo = true;
                    _haloRange = 0.5f;
                    _haloIntensity = 0.15f;
                    _onlyWhenUnlockable = false;
                    break;

                case GlowCategory.Pickup:
                    // Brass / Gold: noticeable but not blinding
                    _glowColor = new Color(1.0f, 0.82f, 0.28f);
                    _minIntensity = 0.12f;
                    _maxIntensity = 0.40f;
                    _pulseSpeed = 2.0f;
                    _useSubtleLightHalo = true;
                    _haloRange = 0.6f;
                    _haloIntensity = 0.20f;
                    _onlyWhenUnlockable = false;
                    break;

                case GlowCategory.UnlockableDoor:
                    // Subtle atmospheric mint sheen
                    _glowColor = new Color(0.10f, 0.55f, 0.38f);
                    _minIntensity = 0.02f;
                    _maxIntensity = 0.10f;
                    _pulseSpeed = 1.6f;
                    _hoverMultiplier = 1.15f;
                    _useSubtleLightHalo = false;
                    _onlyWhenUnlockable = true;
                    break;
            }
        }
    }
}

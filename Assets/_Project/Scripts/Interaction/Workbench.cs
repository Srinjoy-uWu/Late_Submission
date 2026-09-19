using System;
using UnityEngine;
using LateSubmission.Inventory;
using LateSubmission.Attention;
using LateSubmission.Weapon;

namespace LateSubmission.Interaction
{
    /// <summary>
    /// Assembly workbench on Floor 2.
    /// Requires Lens, BatteryPack, CircuitBoard, and WeaponHousing to craft the Laser Weapon.
    /// </summary>
    public class Workbench : MonoBehaviour, IInteractable
    {
        [Header("State")]
        [SerializeField] private bool _isAssembled = false;

        [Header("Assembly Payoff")]
        [SerializeField] private GameObject _craftedWeaponPrefab;
        [SerializeField] private LaserWeapon _playerLaserWeapon;
        [SerializeField] private ParticleSystem _craftVfx;
        [SerializeField] private AudioClip _craftSfx;
        [SerializeField] private float _assemblyNoise = 30f;

        public static event Action OnLaserAssembled;

        public string GetInteractionText()
        {
            if (_isAssembled)
            {
                return "Assembly Bench (Weapon Complete)";
            }

            if (InventoryManager.Instance != null && InventoryManager.Instance.HasAllLaserComponents())
            {
                return "Assemble Laser Weapon [E]";
            }

            return "Assembly Bench (Missing Components)";
        }

        public bool CanInteract(Interactor interactor)
        {
            return !_isAssembled;
        }

        public void Interact(Interactor interactor)
        {
            if (_isAssembled) return;

            if (InventoryManager.Instance != null && InventoryManager.Instance.HasAllLaserComponents())
            {
                _isAssembled = true;
                InventoryManager.Instance.ConsumeLaserComponents();

                if (_craftVfx != null)
                {
                    _craftVfx.Play();
                }

                if (_craftSfx != null)
                {
                    AudioSource.PlayClipAtPoint(_craftSfx, transform.position);
                }

                AttentionManager.Emit(transform.position, _assemblyNoise, AttentionType.Machine);

                if (_playerLaserWeapon != null)
                {
                    _playerLaserWeapon.gameObject.SetActive(true);
                    _playerLaserWeapon.InitializeWeapon();
                }

                OnLaserAssembled?.Invoke();
            }
        }
    }
}

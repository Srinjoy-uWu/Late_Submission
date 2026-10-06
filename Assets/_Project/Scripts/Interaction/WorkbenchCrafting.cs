using System;
using UnityEngine;
using LateSubmission.Inventory;
using LateSubmission.Weapon;
using LateSubmission.Objectives;

namespace LateSubmission.Interaction
{
    /// <summary>
    /// Assembly Workbench in Mechatronics Lab.
    /// When all 7 components are gathered, enables crafting of the Merged Light Pistol.
    /// </summary>
    public class WorkbenchCrafting : MonoBehaviour, IInteractable
    {
        [Header("Weapon Viewmodel Reference")]
        [SerializeField] private MergedLightPistol _weaponViewmodel;

        [Header("Audio")]
        [SerializeField] private AudioClip _craftSfx;
        [SerializeField] private AudioSource _audioSource;

        private bool _isCrafted = false;

        private void Awake()
        {
            if (_audioSource == null) _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
        }

        public string GetInteractionText()
        {
            if (_isCrafted)
            {
                return "Assembly Completed.";
            }

            if (InventoryManager.Instance != null && InventoryManager.Instance.HasAllLaserComponents())
            {
                return "Assemble Merged Light Pistol [E]";
            }

            return "Workbench: Missing Components (Need all 7 parts)";
        }

        public bool CanInteract(Interactor interactor)
        {
            return !_isCrafted;
        }

        public void Interact(Interactor interactor)
        {
            if (_isCrafted) return;

            if (InventoryManager.Instance != null && InventoryManager.Instance.HasAllLaserComponents())
            {
                CraftWeapon();
            }
        }

        private void CraftWeapon()
        {
            _isCrafted = true;

            // Consume crafting components
            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.ConsumeLaserComponents();
            }

            // Play crafting audio
            if (_craftSfx != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(_craftSfx, 1.0f);
            }

            // Activate weapon viewmodel on player
            if (_weaponViewmodel != null)
            {
                _weaponViewmodel.gameObject.SetActive(true);
            }
            else
            {
                var weapon = UnityEngine.Object.FindFirstObjectByType<MergedLightPistol>(FindObjectsInactive.Include);
                if (weapon != null)
                {
                    weapon.gameObject.SetActive(true);
                }
            }

            if (HeldItemController.Instance != null)
            {
                HeldItemController.Instance.EquipPistol();
            }

            // Update objective
            if (ObjectiveManager.Instance != null)
            {
                ObjectiveManager.Instance.SetCustomObjective("Light Pistol Assembled! Stun Mutated Prof. Anish Mondal with [LMB] & Escape to Floor 03!");
            }

            Debug.Log("<color=green>[WorkbenchCrafting] Merged Light Pistol successfully assembled! Flashlight restored + Stun armed.</color>");
        }
    }
}

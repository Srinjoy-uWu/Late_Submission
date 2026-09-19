using UnityEngine;
using LateSubmission.Interaction;
using LateSubmission.Attention;

namespace LateSubmission.Weapon
{
    /// <summary>
    /// Wall-mounted charging outlet in Mechatronics labs to recharge the Laser Weapon.
    /// </summary>
    public class RechargeStation : MonoBehaviour, IInteractable
    {
        [SerializeField] private LaserWeapon _laserWeapon;
        [SerializeField] private int _rechargeAmount = 1;
        [SerializeField] private AudioClip _rechargeSfx;
        [SerializeField] private float _rechargeCooldown = 10f;

        private float _lastUsedTime = -999f;

        private bool IsReady => Time.time >= _lastUsedTime + _rechargeCooldown;

        public string GetInteractionText()
        {
            if (!IsReady)
            {
                float remaining = Mathf.Ceil((_lastUsedTime + _rechargeCooldown) - Time.time);
                return $"Charging Outlet Recharging ({remaining}s)";
            }

            if (_laserWeapon != null && _laserWeapon.CurrentCharges >= _laserWeapon.MaxCharges)
            {
                return "Laser Fully Charged";
            }

            return "Recharge Laser Weapon [E]";
        }

        public bool CanInteract(Interactor interactor)
        {
            if (!IsReady) return false;
            if (_laserWeapon == null) return false;
            return _laserWeapon.CurrentCharges < _laserWeapon.MaxCharges;
        }

        public void Interact(Interactor interactor)
        {
            if (!CanInteract(interactor)) return;

            _lastUsedTime = Time.time;
            _laserWeapon.AddCharges(_rechargeAmount);

            if (_rechargeSfx != null)
            {
                AudioSource.PlayClipAtPoint(_rechargeSfx, transform.position);
            }

            AttentionManager.Emit(transform.position, 12f, AttentionType.Machine);
        }
    }
}

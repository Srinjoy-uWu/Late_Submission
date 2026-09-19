using System;
using System.Collections;
using UnityEngine;
using LateSubmission.Attention;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace LateSubmission.Weapon
{
    public interface IStaggerable
    {
        void Stagger(float duration = 3.0f);
    }

    /// <summary>
    /// The makeshift high-intensity laser weapon assembled on Floor 2.
    /// Limited charges, raycast-based, staggers 'The Late One' and emits massive attention spike.
    /// </summary>
    public class LaserWeapon : MonoBehaviour
    {
        [Header("Charges")]
        [SerializeField] private int _maxCharges = 3;
        [SerializeField] private int _currentCharges = 3;

        [Header("Raycast & Damage")]
        [SerializeField] private float _range = 25f;
        [SerializeField] private LayerMask _hitMask = ~0;
        [SerializeField] private float _staggerDuration = 3.5f;

        [Header("Visuals & Audio")]
        [SerializeField] private Transform _muzzlePoint;
        [SerializeField] private LineRenderer _tracerLine;
        [SerializeField] private ParticleSystem _muzzleFlash;
        [SerializeField] private AudioClip _fireSfx;
        [SerializeField] private AudioClip _emptyClickSfx;

        [Header("Attention Emission")]
        [SerializeField] private float _fireNoise = 50f;

        public int CurrentCharges => _currentCharges;
        public int MaxCharges => _maxCharges;

        public event Action<int> OnChargesChanged;

        private Camera _cam;

        private void Awake()
        {
            _cam = Camera.main;
        }

        public void InitializeWeapon()
        {
            _currentCharges = _maxCharges;
            OnChargesChanged?.Invoke(_currentCharges);
        }

        private void Update()
        {
            bool firePressed = false;

#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                firePressed = true;
            }
#endif
            if (Input.GetMouseButtonDown(0))
            {
                firePressed = true;
            }

            if (firePressed)
            {
                TryFire();
            }
        }

        public bool TryFire()
        {
            if (_currentCharges <= 0)
            {
                if (_emptyClickSfx != null)
                {
                    AudioSource.PlayClipAtPoint(_emptyClickSfx, transform.position);
                }
                return false;
            }

            _currentCharges--;
            OnChargesChanged?.Invoke(_currentCharges);

            FireRaycast();
            return true;
        }

        private void FireRaycast()
        {
            if (_cam == null) _cam = Camera.main;
            Transform rayOrigin = _cam != null ? _cam.transform : transform;
            Ray ray = new Ray(rayOrigin.position, rayOrigin.forward);

            Vector3 hitPoint = ray.origin + ray.direction * _range;

            if (Physics.Raycast(ray, out RaycastHit hit, _range, _hitMask))
            {
                hitPoint = hit.point;

                IStaggerable staggerable = hit.collider.GetComponentInParent<IStaggerable>();
                if (staggerable != null)
                {
                    staggerable.Stagger(_staggerDuration);
                }
            }

            if (_muzzleFlash != null)
            {
                _muzzleFlash.Play();
            }

            if (_fireSfx != null)
            {
                AudioSource.PlayClipAtPoint(_fireSfx, transform.position);
            }

            if (_tracerLine != null)
            {
                StartCoroutine(DrawTracer(hitPoint));
            }

            AttentionManager.Emit(transform.position, _fireNoise, AttentionType.Laser);
        }

        private IEnumerator DrawTracer(Vector3 targetPoint)
        {
            Vector3 startPoint = _muzzlePoint != null ? _muzzlePoint.position : transform.position;
            _tracerLine.SetPosition(0, startPoint);
            _tracerLine.SetPosition(1, targetPoint);
            _tracerLine.enabled = true;

            yield return new WaitForSeconds(0.08f);

            _tracerLine.enabled = false;
        }

        public void AddCharges(int amount)
        {
            _currentCharges = Mathf.Clamp(_currentCharges + amount, 0, _maxCharges);
            OnChargesChanged?.Invoke(_currentCharges);
        }
    }
}

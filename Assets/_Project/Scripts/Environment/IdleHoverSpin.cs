using UnityEngine;

namespace LateSubmission.Environment
{
    /// <summary>
    /// Subtle floating hover and rotation for interactable key pickups.
    /// </summary>
    public class IdleHoverSpin : MonoBehaviour
    {
        [SerializeField] private float _spinSpeed = 45f;
        [SerializeField] private float _bobSpeed = 2.0f;
        [SerializeField] private float _bobAmount = 0.04f;

        private Vector3 _initialLocalPos;

        private void Awake()
        {
            _initialLocalPos = transform.localPosition;
        }

        private void Update()
        {
            transform.Rotate(Vector3.up, _spinSpeed * Time.deltaTime, Space.World);
            float offset = Mathf.Sin(Time.time * _bobSpeed) * _bobAmount;
            transform.localPosition = _initialLocalPos + new Vector3(0f, offset, 0f);
        }
    }
}

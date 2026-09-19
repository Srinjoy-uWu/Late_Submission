using UnityEngine;
using LateSubmission.Attention;

namespace LateSubmission.Interaction
{
    /// <summary>
    /// Trigger placed just past the entrance door.
    /// When player walks through, locks the entrance door behind them.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class EntranceLockTrigger : MonoBehaviour
    {
        [SerializeField] private Door _entranceDoor;
        [SerializeField] private AudioClip _lockLatchSfx;
        [SerializeField] private float _slamNoise = 8.0f;

        private bool _hasTriggered = false;

        private void OnTriggerEnter(Collider other)
        {
            if (_hasTriggered) return;

            if (other.CompareTag("Player") || other.GetComponentInParent<LateSubmission.Player.FPSController>() != null)
            {
                _hasTriggered = true;

                if (_entranceDoor != null)
                {
                    // Force door closed and locked
                    _entranceDoor.gameObject.SetActive(true);
                }

                if (_lockLatchSfx != null)
                {
                    AudioSource.PlayClipAtPoint(_lockLatchSfx, transform.position);
                }

                AttentionManager.Emit(transform.position, _slamNoise, AttentionType.Door);

                // Disable trigger component so it never fires again
                Collider col = GetComponent<Collider>();
                if (col != null) col.enabled = false;
            }
        }
    }
}

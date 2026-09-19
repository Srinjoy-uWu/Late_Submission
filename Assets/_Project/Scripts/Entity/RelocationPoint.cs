using UnityEngine;

namespace LateSubmission.AI
{
    /// <summary>
    /// Marker component for valid safe relocation anchor points on a floor.
    /// </summary>
    public class RelocationPoint : MonoBehaviour
    {
        [SerializeField] private int _floorNumber = 1;
        public int FloorNumber => _floorNumber;

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
            Gizmos.DrawRay(transform.position, transform.forward * 1.0f);
        }
    }
}

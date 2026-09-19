using System;
using UnityEngine;

namespace LateSubmission.Attention
{
    /// <summary>
    /// Central decoupled dispatcher for sound, movement, light, and weapon attention events.
    /// Observers (like the Entity Perception Sensor) register here to receive alerts.
    /// </summary>
    public static class AttentionManager
    {
        public static event Action<AttentionEvent> OnAttentionEmitted;

        public static void Emit(AttentionEvent attentionEvent)
        {
            OnAttentionEmitted?.Invoke(attentionEvent);
        }

        public static void Emit(Vector3 position, float strength, AttentionType type, float duration = 0f)
        {
            Emit(new AttentionEvent(position, strength, type, duration));
        }
    }
}

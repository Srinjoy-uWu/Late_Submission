using UnityEngine;

namespace LateSubmission.Attention
{
    public readonly struct AttentionEvent
    {
        public readonly Vector3 Position;
        public readonly float Strength;
        public readonly AttentionType Type;
        public readonly float Duration;

        public AttentionEvent(Vector3 position, float strength, AttentionType type, float duration = 0f)
        {
            Position = position;
            Strength = strength;
            Type = type;
            Duration = duration;
        }
    }
}

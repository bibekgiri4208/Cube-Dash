using UnityEngine;

namespace CubeDash
{
    /// <summary>One lane per deliberate stick tilt, with hysteresis to reject drift/chatter.</summary>
    public sealed class GamepadLaneInput
    {
        private const float PressThreshold = 0.55f;
        private const float ReleaseThreshold = 0.25f;
        private int heldDirection;

        public int Read(float horizontal)
        {
            if (Mathf.Abs(horizontal) <= ReleaseThreshold) heldDirection = 0;
            int direction = horizontal >= PressThreshold ? 1 : horizontal <= -PressThreshold ? -1 : 0;
            if (direction == 0 || direction == heldDirection) return 0;
            heldDirection = direction;
            return direction;
        }

        public void Reset(float currentHorizontal = 0)
        {
            heldDirection = Mathf.Abs(currentHorizontal) > ReleaseThreshold ? (currentHorizontal > 0 ? 1 : -1) : 0;
        }
    }
}

using UnityEngine;

namespace CubeDash
{
    /// <summary>Cosmetic speed response only; never changes game speed or the camera transform.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Camera))]
    public sealed class RunnerGraphics : MonoBehaviour
    {
        [SerializeField] private CubeDashGame game = null;
        [Tooltip("Set to zero to disable peripheral motion blur.")]
        [SerializeField, Range(0, 1)] private float motionBlurStrength = 0.65f;
        [SerializeField, Min(0)] private float blurStartSpeed = 14;
        [SerializeField, Min(1)] private float blurFullSpeed = 30;
        private float response;
        public float BlurAmount => enabled && game != null && game.isActiveAndEnabled
            && game.State == CubeDashGame.RunState.Running ? response * motionBlurStrength : 0;

        public static float SpeedResponse(float speed, float start, float full)
            => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(start, Mathf.Max(start + 1, full), speed));

        public void Tick(float deltaTime, CubeDashGame.RunState state, float speed)
        {
            // No stale-frame smear on pause, crash, menu or restart. The render pass also
            // checks the live state, irrespective of component update ordering.
            if (state != CubeDashGame.RunState.Running) { response = 0; return; }
            response = Mathf.Lerp(response, SpeedResponse(speed, blurStartSpeed, blurFullSpeed),
                1 - Mathf.Exp(-5 * Mathf.Max(0, deltaTime)));
        }

        private void LateUpdate()
        {
            if (game == null || !game.isActiveAndEnabled) { response = 0; return; }
            Tick(Time.deltaTime, game.State, game.Speed);
        }

        private void OnDisable() => response = 0;
    }
}

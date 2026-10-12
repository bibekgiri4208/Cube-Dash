using UnityEngine;

namespace CubeDash
{
    /// <summary>Small, drift-free menu motion around the camera's authored pose. Never runs in Edit mode or gameplay.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Camera))]
    public sealed class MainMenuCameraSway : MonoBehaviour
    {
        [SerializeField, Tooltip("Maximum sideways and vertical movement in metres.")]
        private Vector2 positionAmplitude = new Vector2(0.06f, 0.035f);
        [SerializeField, Tooltip("Maximum pitch, yaw and roll in degrees.")]
        private Vector3 rotationAmplitude = new Vector3(0.16f, 0.28f, 0.06f);
        [SerializeField, Min(1), Tooltip("Base sway period in seconds. Individual axes move at slightly different speeds.")]
        private float period = 10;
        [SerializeField, Min(0)] private float fadeIn = 2;
        private Vector3 anchorPosition;
        private Quaternion anchorRotation;
        private MainMenuController menu;
        private float elapsed;
        private bool captured;

        private void OnEnable() => CaptureAnchor();
        private void Start()
        {
            if (!captured) CaptureAnchor();
        }

        private void CaptureAnchor()
        {
            if (!Application.isPlaying) return;
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
                if (root.TryGetComponent(out MainMenuController candidate)) menu = candidate;
            if (menu == null) return;
            anchorPosition = transform.localPosition; anchorRotation = transform.localRotation;
            elapsed = 0; captured = true;
        }

        private void LateUpdate()
        {
            if (!captured || menu == null || !menu.isActiveAndEnabled) return;
            elapsed += Time.unscaledDeltaTime;
            SamplePose(elapsed, out Vector3 offset, out Quaternion rotation);
            transform.localPosition = anchorPosition + anchorRotation * offset;
            transform.localRotation = anchorRotation * rotation;
        }

        /// <summary>Camera-relative offsets, sampled from elapsed time rather than accumulated transforms.</summary>
        public void SamplePose(float seconds, out Vector3 offset, out Quaternion rotation)
        {
            seconds = Mathf.Max(0, seconds);
            float blend = fadeIn > 0 ? Mathf.SmoothStep(0, 1, Mathf.Clamp01(seconds / fadeIn)) : 1;
            float phase = seconds * (Mathf.PI * 2 / Mathf.Max(1, period));
            offset = new Vector3(Mathf.Sin(phase) * positionAmplitude.x, Mathf.Sin(phase * 0.73f) * positionAmplitude.y, 0) * blend;
            rotation = Quaternion.Euler(new Vector3(Mathf.Sin(phase * 0.82f) * rotationAmplitude.x,
                Mathf.Sin(phase * 0.61f) * rotationAmplitude.y, Mathf.Sin(phase * 0.47f) * rotationAmplitude.z) * blend);
        }

        private void OnDisable()
        {
            if (!captured) return;
            transform.localPosition = anchorPosition; transform.localRotation = anchorRotation;
            captured = false; menu = null;
        }
    }
}

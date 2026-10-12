using UnityEngine;

namespace CubeDash
{
    /// <summary>Mesh-only presentation: jelly idle and occasional menu backflips. Never reads input or moves a collider.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class PlayerCubeVisual : MonoBehaviour
    {
        [SerializeField] private MeshRenderer[] eyes = new MeshRenderer[0];
        [SerializeField] private bool autoplayIdle = true;
        private MeshRenderer body;
        private Vector3 restScale, restPosition;
        private Quaternion restRotation;
        private float bottom, age;
        private bool cached;

        public const float IdleAngularSpeed = 3.1f;
        public const float BackflipStart = 6f;
        public const float BackflipInterval = 8.5f;
        public const float BackflipDuration = 1.25f;
        private const float CrouchDuration = 0.18f;
        private const float FlightDuration = 0.82f;

        public MeshRenderer Body { get { CachePose(); return body; } }
        public MeshRenderer[] Eyes => eyes;
        public bool AutoplayIdle => autoplayIdle;
        public float IdleAge => age;

        private void Awake() => CachePose();

        private void CachePose()
        {
            if (cached) return;
            body = GetComponent<MeshRenderer>();
            Mesh mesh = GetComponent<MeshFilter>().sharedMesh;
            bottom = mesh != null ? mesh.bounds.min.y : -0.5f;
            restScale = transform.localScale;
            restPosition = transform.localPosition;
            restRotation = transform.localRotation;
            cached = true;
        }

        /// <summary>The game owns pickup/idle/pause animation; the menu preview owns only its own idle.</summary>
        public void UseGameplayAnimation()
        {
            CachePose();
            if (autoplayIdle) RestorePose();
            autoplayIdle = false;
        }

        public static Vector3 JellyScale(float seconds)
        {
            float phase = seconds * IdleAngularSpeed;
            float height = 1 + 0.085f * (Mathf.Sin(phase) + 0.22f * Mathf.Sin(phase * 2));
            float width = 1 / Mathf.Sqrt(height);
            float wobble = 1 + 0.014f * Mathf.Sin(phase * 2);
            // Preserve volume as the cube squashes and rebounds, with a small secondary jiggle.
            return new Vector3(width * wobble, height, width / wobble);
        }

        /// <summary>Menu-only pose: crouch, airborne backwards rotation, then a soft jelly landing.</summary>
        public static void SampleMenuIdle(float seconds, out Vector3 scale, out Quaternion rotation, out float lift)
        {
            scale = JellyScale(seconds);
            rotation = Quaternion.identity;
            lift = 0;
            float flipTime = Mathf.Repeat(Mathf.Max(0, seconds), BackflipInterval) - BackflipStart;
            if (flipTime < 0 || flipTime >= BackflipDuration) return;

            float height;
            if (flipTime < CrouchDuration)
                height = 1 - 0.18f * Mathf.Sin(Mathf.PI * flipTime / CrouchDuration);
            else if (flipTime < CrouchDuration + FlightDuration)
            {
                float progress = (flipTime - CrouchDuration) / FlightDuration;
                float arc = Mathf.Sin(Mathf.PI * progress);
                lift = 0.95f * arc;
                // Positive local X pitches the cube's -Z face upward for a backwards somersault.
                rotation = Quaternion.Euler(360 * Mathf.SmoothStep(0, 1, progress), 0, 0);
                height = 1 + 0.05f * arc;
            }
            else
            {
                float progress = (flipTime - CrouchDuration - FlightDuration) /
                    (BackflipDuration - CrouchDuration - FlightDuration);
                height = 1 - 0.15f * Mathf.Sin(Mathf.PI * progress);
            }
            float width = 1 / Mathf.Sqrt(height);
            scale = Vector3.Scale(scale, new Vector3(width, height, width));
        }

        public static Vector3 GroundedPosition(Vector3 position, Vector3 rest, Vector3 deformed, float meshBottom)
            => position + Vector3.up * (meshBottom * (rest.y - deformed.y));

        private void LateUpdate()
        {
            CachePose();
            if (autoplayIdle)
            {
                age += Time.deltaTime;
                SampleMenuIdle(age, out Vector3 scale, out Quaternion rotation, out float lift);
                transform.localScale = Vector3.Scale(restScale, scale);
                transform.localRotation = restRotation * rotation;
                transform.localPosition = GroundedPosition(restPosition, restScale, transform.localScale, bottom)
                    + Vector3.up * (lift * restScale.y);
            }
            SyncEyes();
        }

        public void SyncEyes()
        {
            CachePose();
            foreach (MeshRenderer eye in eyes)
                if (eye != null) eye.enabled = body != null && body.enabled;
        }

        private void RestorePose()
        {
            transform.localScale = restScale;
            transform.localPosition = restPosition;
            transform.localRotation = restRotation;
            age = 0;
        }

        private void OnDisable()
        {
            if (cached && autoplayIdle) RestorePose();
        }
    }
}

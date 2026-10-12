using UnityEngine;

namespace CubeDash
{
    /// <summary>Mesh-only presentation: two eyes and a grounded jelly idle. Never reads input or moves a collider.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class PlayerCubeVisual : MonoBehaviour
    {
        [SerializeField] private MeshRenderer[] eyes = new MeshRenderer[0];
        [SerializeField] private bool autoplayIdle = true;
        private MeshRenderer body;
        private Vector3 restScale, restPosition;
        private float bottom, age;
        private bool cached;

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
            float phase = seconds * 2.6f;
            float height = 1 + 0.085f * (Mathf.Sin(phase) + 0.22f * Mathf.Sin(phase * 2));
            float width = 1 / Mathf.Sqrt(height);
            float wobble = 1 + 0.014f * Mathf.Sin(phase * 2);
            // Preserve volume as the cube squashes and rebounds, with a small secondary jiggle.
            return new Vector3(width * wobble, height, width / wobble);
        }

        public static Vector3 GroundedPosition(Vector3 position, Vector3 rest, Vector3 deformed, float meshBottom)
            => position + Vector3.up * (meshBottom * (rest.y - deformed.y));

        private void LateUpdate()
        {
            CachePose();
            if (autoplayIdle)
            {
                age += Time.deltaTime;
                transform.localScale = Vector3.Scale(restScale, JellyScale(age));
                transform.localPosition = GroundedPosition(restPosition, restScale, transform.localScale, bottom);
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
            age = 0;
        }

        private void OnDisable()
        {
            if (cached && autoplayIdle) RestorePose();
        }
    }
}

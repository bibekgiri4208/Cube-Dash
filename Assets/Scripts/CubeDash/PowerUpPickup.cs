using UnityEngine;

namespace CubeDash
{
    public enum PowerUpType { Shield, DoublePoints }

    /// <summary>
    /// A harmless floating bonus. Collection is resolved by EndlessTrack's own sweep, so the
    /// pickup never ends a run. Everything here is an authored scene/prefab object; the track
    /// only moves and toggles it.
    /// </summary>
    public sealed class PowerUpPickup : MonoBehaviour
    {
        [SerializeField] private PowerUpType type = PowerUpType.Shield;
        [SerializeField] private Transform visual = null;
        [SerializeField] private Renderer visualRenderer = null;
        [SerializeField] private float spinSpeed = 82f;
        [SerializeField] private float bobHeight = 0.09f;
        private BoxCollider box;
        private Vector3 visualHome;
        private float phase;

        public PowerUpType Type => type;

        public BoxCollider Collider
        {
            get
            {
                if (box == null) box = GetComponent<BoxCollider>();
                return box;
            }
        }

        public void Configure(PowerUpType value, Material material)
        {
            type = value;
            CacheVisual();
            if (visualRenderer != null) visualRenderer.sharedMaterial = material;
        }

        private void Awake() => CacheVisual();

        private void OnEnable() => phase = transform.position.z * 0.13f + transform.position.x * 0.07f;

        private void Update()
        {
            if (visual == null) return;
            float time = Time.unscaledTime;
            visual.localRotation = Quaternion.Euler(18f, time * spinSpeed + phase * 57f, 42f);
            visual.localPosition = visualHome + Vector3.up * (Mathf.Sin(time * 2.4f + phase) * bobHeight);
        }

        private void CacheVisual()
        {
            if (visual == null) visual = transform.childCount > 0 ? transform.GetChild(0) : null;
            if (visualRenderer == null) visualRenderer = GetComponentInChildren<Renderer>();
            if (visual != null) visualHome = visual.localPosition;
        }
    }
}

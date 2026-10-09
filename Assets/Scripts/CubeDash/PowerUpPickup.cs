using UnityEngine;

namespace CubeDash
{
    public enum PowerUpType { Shield, DoublePoints, FighterPlane, Truck }

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
        [SerializeField] private Transform fighterVisual = null;
        [SerializeField] private Transform truckVisual = null;
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
            bool model = (value == PowerUpType.FighterPlane && fighterVisual != null) ||
                (value == PowerUpType.Truck && truckVisual != null);
            if (visual != null) visual.gameObject.SetActive(!model);
            if (fighterVisual != null) fighterVisual.gameObject.SetActive(value == PowerUpType.FighterPlane);
            if (truckVisual != null) truckVisual.gameObject.SetActive(value == PowerUpType.Truck);
        }

        private void Awake() => CacheVisual();

        private void OnEnable() => phase = transform.position.z * 0.13f + transform.position.x * 0.07f;

        private void Update()
        {
            if (visual == null) return;
            Transform display = type == PowerUpType.FighterPlane && fighterVisual != null ? fighterVisual : visual;
            if (type == PowerUpType.Truck && truckVisual != null) display = truckVisual;
            float time = Time.unscaledTime;
            display.localRotation = type == PowerUpType.FighterPlane || type == PowerUpType.Truck ? Quaternion.Euler(0, time * spinSpeed * 0.7f + phase * 57f, 0)
                : Quaternion.Euler(18f, time * spinSpeed + phase * 57f, 42f);
            display.localPosition = visualHome + Vector3.up * (Mathf.Sin(time * 2.4f + phase) * bobHeight);
        }

        private void CacheVisual()
        {
            if (visual == null) visual = transform.childCount > 0 ? transform.GetChild(0) : null;
            if (visualRenderer == null) visualRenderer = GetComponentInChildren<Renderer>();
            if (visual != null) visualHome = visual.localPosition;
        }
    }
}

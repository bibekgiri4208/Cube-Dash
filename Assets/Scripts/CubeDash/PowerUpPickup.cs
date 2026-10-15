using UnityEngine;

namespace CubeDash
{
    public enum PowerUpType { Shield, DoublePoints, FighterPlane, Truck, Magnet }

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
        [SerializeField] private Transform shieldVisual = null;
        [SerializeField] private Transform pointsVisual = null;
        [SerializeField] private Transform magnetVisual = null;
        [SerializeField] private MeshRenderer auraRenderer = null;
        [SerializeField] private float spinSpeed = 82f;
        [SerializeField] private float bobHeight = 0.09f;
        private BoxCollider box;
        private Vector3 visualHome;
        private float phase;
        private float age;
        private Vector3[] modelHomes;
        private Vector3[] modelCenters;
        private MaterialPropertyBlock auraProperties;
        private static readonly int AuraTint = Shader.PropertyToID("_Tint");
        private static readonly int AuraPhase = Shader.PropertyToID("_Phase");

        public PowerUpType Type => type;
        public MeshRenderer AuraRenderer => auraRenderer;

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
            bool model = Model(value) != null;
            if (visual != null) visual.gameObject.SetActive(!model);
            if (fighterVisual != null) fighterVisual.gameObject.SetActive(value == PowerUpType.FighterPlane);
            if (truckVisual != null) truckVisual.gameObject.SetActive(value == PowerUpType.Truck);
            if (shieldVisual != null) shieldVisual.gameObject.SetActive(value == PowerUpType.Shield);
            if (pointsVisual != null) pointsVisual.gameObject.SetActive(value == PowerUpType.DoublePoints);
            if (magnetVisual != null) magnetVisual.gameObject.SetActive(value == PowerUpType.Magnet);
            age = 0;
            Tick(0);
        }

        private void Awake() => CacheVisual();

        private void OnEnable() => phase = transform.position.z * 0.13f + transform.position.x * 0.07f;

        public void Tick(float dt)
        {
            CacheVisual();
            Transform display = Model(type);
            bool model = display != null;
            if (!model) display = visual;
            if (display == null) return;
            age += Mathf.Max(0, dt);
            bool vehicle = type == PowerUpType.FighterPlane || type == PowerUpType.Truck;
            display.localRotation = vehicle ? Quaternion.Euler(0, age * spinSpeed * 0.7f + phase * 57f, 0)
                : model ? Quaternion.Euler(0, Mathf.Sin(age * 1.5f + phase) * 25, 0)
                : Quaternion.Euler(18f, age * spinSpeed + phase * 57f, 42f);
            display.localPosition = (model ? modelHomes[(int)type] : visualHome)
                + Vector3.up * (Mathf.Sin(age * 2.4f + phase) * bobHeight);
            TickAura(display);
        }

        private void TickAura(Transform display)
        {
            if (auraRenderer == null) return;
            if (auraProperties == null) auraProperties = new MaterialPropertyBlock();
            Color tint;
            switch (type)
            {
                case PowerUpType.DoublePoints: tint = new Color(1f, 0.72f, 0.18f, 0.65f); break;
                case PowerUpType.FighterPlane: tint = new Color(0.65f, 0.42f, 1f, 0.65f); break;
                case PowerUpType.Truck: tint = new Color(0.22f, 0.48f, 1f, 0.65f); break;
                case PowerUpType.Magnet: tint = new Color(1f, 0.19f, 0.12f, 0.65f); break;
                default: tint = new Color(0.22f, 0.85f, 1f, 0.65f); break;
            }
            float pulse = Mathf.Sin(age * 2.4f + phase);
            float diameter = type == PowerUpType.FighterPlane ? 2.7f : type == PowerUpType.Truck ? 2.5f : 2.1f;
            Vector3 center = Model(type) != null ? modelCenters[(int)type] : Vector3.zero;
            auraRenderer.transform.localPosition = transform.InverseTransformPoint(display.TransformPoint(center));
            auraRenderer.transform.localScale = Vector3.one * (diameter * (1 + pulse * 0.045f));
            auraProperties.SetColor(AuraTint, tint);
            auraProperties.SetFloat(AuraPhase, age + phase);
            auraRenderer.SetPropertyBlock(auraProperties);
        }

        private Transform Model(PowerUpType value)
        {
            switch (value)
            {
                case PowerUpType.Shield: return shieldVisual;
                case PowerUpType.DoublePoints: return pointsVisual;
                case PowerUpType.FighterPlane: return fighterVisual;
                case PowerUpType.Truck: return truckVisual;
                case PowerUpType.Magnet: return magnetVisual;
                default: return null;
            }
        }

        private void CacheVisual()
        {
            if (visual == null) visual = transform.childCount > 0 ? transform.GetChild(0) : null;
            if (visualRenderer == null) visualRenderer = GetComponentInChildren<Renderer>();
            if (modelHomes != null) return;
            if (visual != null) visualHome = visual.localPosition;
            modelHomes = new Vector3[5];
            modelCenters = new Vector3[5];
            for (int i = 0; i < modelHomes.Length; i++)
            {
                Transform model = Model((PowerUpType)i);
                if (model == null) continue;
                modelHomes[i] = model.localPosition;
                bool found = false;
                Bounds bounds = default;
                foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.sharedMesh == null) continue;
                    Bounds mesh = filter.sharedMesh.bounds;
                    for (int corner = 0; corner < 8; corner++)
                    {
                        Vector3 point = mesh.center + Vector3.Scale(mesh.extents, new Vector3(
                            (corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                        point = model.InverseTransformPoint(filter.transform.TransformPoint(point));
                        if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; }
                        else bounds.Encapsulate(point);
                    }
                }
                modelCenters[i] = bounds.center;
            }
        }
    }
}

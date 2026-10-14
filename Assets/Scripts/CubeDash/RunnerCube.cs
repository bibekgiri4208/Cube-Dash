using UnityEngine;

namespace CubeDash
{
    public enum CubeColor { Red, Blue, Green }

    /// <summary>A pooled slot shows either an obstacle cube or its saved 3D coin visual.</summary>
    public sealed class RunnerCube : MonoBehaviour
    {
        [SerializeField] private CubeColor color = CubeColor.Blue;
        [SerializeField] private Renderer cubeRenderer = null;
        [SerializeField] private Transform coinVisual = null;
        private Vector3 coinHome;
        private bool coinHomeCached;
        private float coinAge;
        private Vector3 attractionOffset;
        private Mesh homeMesh;
        private Bounds homeRenderBounds;
        private Vector3 homeColliderSize, homeColliderCenter;
        private bool hazardLayoutCached;
        public Mesh HazardSourceMesh { get; private set; }
        public Renderer ObstacleRenderer => cubeRenderer != null ? cubeRenderer : GetComponent<Renderer>();
        public HighwayHazard Hazard { get; private set; }
        public CubeColor Color => color;
        public BoxCollider Collider => GetComponent<BoxCollider>();
        public Transform CoinVisual => coinVisual;
        public bool CoinVisible => coinVisual != null && coinVisual.gameObject.activeSelf;

        public void Configure(CubeColor value, Material material)
            => Configure(value, material, value == CubeColor.Red);

        public void Configure(CubeColor value, Material material, bool collectible)
        {
            color = value;
            if (cubeRenderer == null) cubeRenderer = GetComponent<Renderer>();
            if (!hazardLayoutCached)
            {
                homeMesh = GetComponent<MeshFilter>().sharedMesh;
                homeRenderBounds = cubeRenderer.localBounds;
                homeColliderSize = Collider.size;
                homeColliderCenter = Collider.center;
                hazardLayoutCached = true;
            }
            GetComponent<MeshFilter>().sharedMesh = homeMesh;
            cubeRenderer.localBounds = homeRenderBounds;
            Collider.size = homeColliderSize;
            Collider.center = homeColliderCenter;
            Hazard = HighwayHazard.Cube;
            HazardSourceMesh = homeMesh;
            cubeRenderer.sharedMaterials = new[] { material };
            cubeRenderer.enabled = !collectible || coinVisual == null;
            if (coinVisual == null) return;
            if (!coinHomeCached) { coinHome = coinVisual.localPosition; coinHomeCached = true; }
            coinAge = 0;
            attractionOffset = Vector3.zero;
            coinVisual.localPosition = coinHome;
            coinVisual.localRotation = Quaternion.Euler(0, 25, 0);
            coinVisual.gameObject.SetActive(collectible);
        }

        public void SetHazard(HighwayHazard hazard, Mesh mesh, Material[] materials, Vector3 worldSize, Mesh source = null)
        {
            Hazard = hazard;
            HazardSourceMesh = source != null ? source : mesh;
            GetComponent<MeshFilter>().sharedMesh = mesh;
            cubeRenderer.localBounds = HighwayPresentation.CurveBounds(mesh.bounds, transform);
            cubeRenderer.sharedMaterials = materials;
            Collider.size = new Vector3(worldSize.x / transform.lossyScale.x,
                worldSize.y / transform.lossyScale.y, worldSize.z / transform.lossyScale.z);
            Collider.center = new Vector3(0, (worldSize.y * 0.5f - transform.localPosition.y) / transform.lossyScale.y, 0);
        }

        public void TickCoin(float dt, bool attracted = false)
        {
            if (!CoinVisible || dt <= 0) return;
            coinAge += dt;
            if (!attracted) attractionOffset = Vector3.zero;
            coinVisual.localRotation = Quaternion.Euler(0, 25 + coinAge * 130, 0);
            coinVisual.localPosition = coinHome + attractionOffset + Vector3.up * (Mathf.Sin(coinAge * 3.5f) * 0.045f);
        }

        public void AttractCoinTo(Vector3 worldPosition)
        {
            if (!CoinVisible) return;
            Vector3 position = transform.InverseTransformPoint(worldPosition);
            attractionOffset = position - coinHome - Vector3.up * (Mathf.Sin(coinAge * 3.5f) * 0.045f);
            coinVisual.localPosition = position;
        }
    }
}

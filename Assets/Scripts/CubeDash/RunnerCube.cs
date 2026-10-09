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
            cubeRenderer.sharedMaterial = material;
            cubeRenderer.enabled = !collectible || coinVisual == null;
            if (coinVisual == null) return;
            if (!coinHomeCached) { coinHome = coinVisual.localPosition; coinHomeCached = true; }
            coinAge = 0;
            attractionOffset = Vector3.zero;
            coinVisual.localPosition = coinHome;
            coinVisual.localRotation = Quaternion.Euler(0, 25, 0);
            coinVisual.gameObject.SetActive(collectible);
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

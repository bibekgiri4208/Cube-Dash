using UnityEngine;

namespace CubeDash
{
    public enum CubeColor { Red, Blue, Green }

    /// <summary>Gameplay identity is an enum, not a fragile comparison of material RGB values.</summary>
    public sealed class RunnerCube : MonoBehaviour
    {
        [SerializeField] private CubeColor color = CubeColor.Blue;
        [SerializeField] private Renderer cubeRenderer = null;
        public CubeColor Color => color;
        public BoxCollider Collider => GetComponent<BoxCollider>();

        public void Configure(CubeColor value, Material material)
        {
            color = value;
            if (cubeRenderer == null) cubeRenderer = GetComponent<Renderer>();
            cubeRenderer.sharedMaterial = material;
        }
    }
}

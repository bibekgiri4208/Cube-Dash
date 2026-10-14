using UnityEngine;

namespace CubeDash
{
    /// <summary>Baked, shared traffic art. Mesh coordinates are road-space metres, with their feet at zero.</summary>
    public sealed class HighwayHazardVisuals : ScriptableObject
    {
        public const string ResourcePath = "HighwayHazards";
        [SerializeField] private Mesh barrier = null;
        [SerializeField] private Mesh van = null;
        [SerializeField] private Material[] materials = new Material[0];
        public Material[] Materials => materials;
        public Mesh MeshFor(HighwayHazard type) => type == HighwayHazard.Barrier ? barrier : type == HighwayHazard.Van ? van : null;
    }
}

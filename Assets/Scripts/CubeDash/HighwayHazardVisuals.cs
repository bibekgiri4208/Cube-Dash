using UnityEngine;

namespace CubeDash
{
    /// <summary>Baked, shared traffic art. Mesh coordinates are road-space metres, with their feet at zero.</summary>
    public sealed class HighwayHazardVisuals : ScriptableObject
    {
        public const string ResourcePath = "HighwayHazards";
        [SerializeField] private Mesh barrier = null;
        [SerializeField] private Mesh van = null;
        [SerializeField] private Mesh truck = null;
        [SerializeField] private Mesh[] barrierVariants = new Mesh[0];
        [SerializeField] private Mesh[] vanVariants = new Mesh[0];
        [SerializeField] private Mesh[] truckVariants = new Mesh[0];
        [SerializeField] private Material[] materials = new Material[0];
        public Material[] Materials => materials;
        private Mesh[] Variants(HighwayHazard type) => type == HighwayHazard.Barrier ? barrierVariants : type == HighwayHazard.Van ? vanVariants : type == HighwayHazard.Truck ? truckVariants : null;
        public int VariantCount(HighwayHazard type) => type == HighwayHazard.Cube ? 0 : 1 + (Variants(type)?.Length ?? 0);
        public Mesh MeshFor(HighwayHazard type, int variant = 0)
        {
            int count = VariantCount(type);
            if (count == 0) return null;
            int index = (variant % count + count) % count;
            return index == 0 ? (type == HighwayHazard.Barrier ? barrier : type == HighwayHazard.Van ? van : truck) : Variants(type)[index - 1];
        }
    }
}

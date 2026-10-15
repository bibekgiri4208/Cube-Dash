using UnityEngine;

namespace CubeDash
{
    /// <summary>Baked, shared traffic art. Mesh coordinates are road-space metres, with their feet at zero.</summary>
    public sealed class HighwayHazardVisuals : ScriptableObject
    {
        public const string ResourcePath = "HighwayHazards";
        [SerializeField] private Mesh compactObstacle = null;
        [SerializeField] private Mesh[] compactVariants = new Mesh[0];
        [SerializeField] private Mesh barrier = null;
        [SerializeField] private Mesh van = null;
        [SerializeField] private Mesh truck = null;
        [SerializeField] private Mesh[] barrierVariants = new Mesh[0];
        [SerializeField] private Mesh[] vanVariants = new Mesh[0];
        [SerializeField] private Mesh[] truckVariants = new Mesh[0];
        [SerializeField] private Material[] materials = new Material[0];
        [Tooltip("Use authored vehicle body colors instead of the obstacle's gameplay tint. Non-vehicle hazards are unaffected.")]
        [SerializeField] private bool overrideVehiclePaint = false;
        [SerializeField] private Color vanPaint = Color.white;
        [SerializeField] private Color truckPaint = Color.white;
        public Material[] Materials => materials;
        public Color PaintFor(HighwayHazard type, Color gameplayTint)
            => overrideVehiclePaint && type == HighwayHazard.Van ? vanPaint
                : overrideVehiclePaint && type == HighwayHazard.Truck ? truckPaint : gameplayTint;
        // Cube remains the serialized compact collision category, not a visible cube model.
        private Mesh[] Variants(HighwayHazard type) => type == HighwayHazard.Cube ? compactVariants : type == HighwayHazard.Barrier ? barrierVariants : type == HighwayHazard.Van ? vanVariants : type == HighwayHazard.Truck ? truckVariants : null;
        public int VariantCount(HighwayHazard type) => 1 + (Variants(type)?.Length ?? 0);
        public Mesh MeshFor(HighwayHazard type, int variant = 0)
        {
            int count = VariantCount(type);
            if (count == 0) return null;
            int index = (variant % count + count) % count;
            return index == 0 ? (type == HighwayHazard.Cube ? compactObstacle : type == HighwayHazard.Barrier ? barrier : type == HighwayHazard.Van ? van : truck) : Variants(type)[index - 1];
        }
    }
}

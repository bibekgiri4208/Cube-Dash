using UnityEngine;

namespace CubeDash
{
    /// <summary>Saved roadside flyover profiles meet at section edges and ramp down at city limits.</summary>
    public sealed class CityFlyover : MonoBehaviour
    {
        [SerializeField] private MeshFilter deck = null;
        [Tooltip("Level, entrance ramp, exit ramp.")]
        [SerializeField] private Mesh[] profiles = new Mesh[0];
        public Mesh[] Profiles => profiles;

        public void Configure(int sectionIndex, int segmentsPerBiome)
        {
            if (deck == null || profiles.Length != 3) return;
            int count = Mathf.Max(2, segmentsPerBiome);
            int within = ((sectionIndex % count) + count) % count;
            deck.sharedMesh = profiles[within == 0 ? 1 : within == count - 1 ? 2 : 0];
        }
    }
}

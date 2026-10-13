using UnityEngine;

namespace CubeDash
{
    /// <summary>Saved landscape and landmark compositions share materials and swap only offscreen.</summary>
    public sealed class BiomeScenery : MonoBehaviour
    {
        [SerializeField] private MeshFilter details = null;
        [SerializeField] private Mesh[] layouts = new Mesh[0];
        [SerializeField] private Mesh[] entranceLayouts = new Mesh[0];
        [SerializeField] private Mesh[] exitLayouts = new Mesh[0];
        [SerializeField] private MeshFilter variety = null;
        [SerializeField] private Mesh[] varietyLayouts = new Mesh[0];
        public Mesh[] Layouts => layouts;
        public Mesh[] EntranceLayouts => entranceLayouts;
        public Mesh[] ExitLayouts => exitLayouts;
        public Mesh[] VarietyLayouts => varietyLayouts;

        public Mesh DetailLayout(int sectionIndex, int segmentsPerBiome = 8, bool continuous = false)
        {
            int count = Mathf.Max(2, segmentsPerBiome);
            int within = ((sectionIndex % count) + count) % count;
            Mesh[] source = continuous ? layouts : within == 0 && entranceLayouts.Length > 0 ? entranceLayouts
                : within == count - 1 && exitLayouts.Length > 0 ? exitLayouts : layouts;
            return source.Length > 0 ? source[BiomeRules.Variation(sectionIndex, source.Length)] : null;
        }

        public void SelectLayout(int sectionIndex, int segmentsPerBiome = 8, bool continuous = false)
        {
            if (details != null && layouts.Length > 0)
                details.sharedMesh = DetailLayout(sectionIndex, segmentsPerBiome, continuous);
            if (variety != null && varietyLayouts.Length > 0)
                variety.sharedMesh = varietyLayouts[BiomeRules.Variation(sectionIndex, varietyLayouts.Length)];
        }
    }
}

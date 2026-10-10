using UnityEngine;

namespace CubeDash
{
    /// <summary>Saved landscape and landmark compositions share materials and swap only offscreen.</summary>
    public sealed class BiomeScenery : MonoBehaviour
    {
        [SerializeField] private MeshFilter details = null;
        [SerializeField] private Mesh[] layouts = new Mesh[0];
        [SerializeField] private MeshFilter variety = null;
        [SerializeField] private Mesh[] varietyLayouts = new Mesh[0];
        public Mesh[] Layouts => layouts;
        public Mesh[] VarietyLayouts => varietyLayouts;

        public void SelectLayout(int sectionIndex)
        {
            if (details != null && layouts.Length > 0)
                details.sharedMesh = layouts[BiomeRules.Variation(sectionIndex, layouts.Length)];
            if (variety != null && varietyLayouts.Length > 0)
                variety.sharedMesh = varietyLayouts[BiomeRules.Variation(sectionIndex, varietyLayouts.Length)];
        }
    }
}

using UnityEngine;

namespace CubeDash
{
    /// <summary>Three saved mesh layouts share materials and are swapped only offscreen.</summary>
    public sealed class BiomeScenery : MonoBehaviour
    {
        [SerializeField] private MeshFilter details = null;
        [SerializeField] private Mesh[] layouts = new Mesh[0];
        public Mesh[] Layouts => layouts;

        public void SelectLayout(int sectionIndex)
        {
            if (details != null && layouts.Length > 0)
                details.sharedMesh = layouts[BiomeRules.Variation(sectionIndex, layouts.Length)];
        }
    }
}

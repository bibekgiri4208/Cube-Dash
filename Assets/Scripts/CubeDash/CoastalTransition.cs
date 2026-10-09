using UnityEngine;

namespace CubeDash
{
    /// <summary>Shapes saved water into a bay at biome boundaries, without altering shared materials.</summary>
    public sealed class CoastalTransition : MonoBehaviour
    {
        [SerializeField] private Renderer[] waterSurfaces = new Renderer[0];
        [SerializeField] private Renderer[] landSurfaces = new Renderer[0];
        [SerializeField] private GameObject headland = null;
        private MaterialPropertyBlock block;
        private static readonly int CoastLimits = Shader.PropertyToID("_CoastLimits");
        private static readonly int LandEnd = Shader.PropertyToID("_LandEnd");
        private static readonly int EndTint = Shader.PropertyToID("_EndTint");
        public bool IsEntrance { get; private set; }
        public bool IsExit { get; private set; }

        public void Configure(int sectionIndex, int segmentsPerBiome, Color nextLandColor)
        {
            int length = Mathf.Max(2, segmentsPerBiome);
            int within = ((sectionIndex % length) + length) % length;
            IsEntrance = within == 0;
            IsExit = within == length - 1;
            if (block == null) block = new MaterialPropertyBlock();
            foreach (Renderer surface in waterSurfaces)
            {
                if (surface == null) continue;
                float offset = surface.transform.localPosition.z;
                surface.GetPropertyBlock(block);
                block.SetVector(CoastLimits, new Vector4(IsEntrance ? 1 : 0, IsExit ? 1 : 0,
                    -offset, RunnerRules.SegmentLength - offset));
                surface.SetPropertyBlock(block);
                block.Clear();
            }
            foreach (Renderer surface in landSurfaces)
            {
                if (surface == null) continue;
                surface.GetPropertyBlock(block);
                block.SetVector(LandEnd, new Vector4(IsExit ? 1 : 0,
                    RunnerRules.SegmentLength - surface.transform.localPosition.z, 18, 0));
                block.SetColor(EndTint, nextLandColor);
                surface.SetPropertyBlock(block);
                block.Clear();
            }
            if (headland != null) headland.SetActive(IsExit);
        }
    }
}

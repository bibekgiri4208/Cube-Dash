using System;
using UnityEngine;

namespace CubeDash
{
    /// <summary>Authored transition dressing and per-instance terrain blends, configured only when recycled.</summary>
    public sealed class BiomeBoundaryBlend : MonoBehaviour
    {
        [Serializable]
        public sealed class Region
        {
            public Renderer[] Ground = new Renderer[0];
            public Renderer[] Water = new Renderer[0];
            public Mesh[] NormalGround = new Mesh[0], BoundaryGround = new Mesh[0];
            public Material Floor;
            public GameObject EntranceDressing, ExitDressing;
        }

        [SerializeField] private Region[] regions = new Region[BiomeRules.Count];
        [SerializeField, Range(12, 42)] private float halfWidth = 42;
        [SerializeField] private Transform[] cityBuildings = new Transform[0];
        [SerializeField] private Vector3[] cityBuildingScales = new Vector3[0];
        private MaterialPropertyBlock block;
        public bool Entrance { get; private set; }
        public bool Exit { get; private set; }
        public Region[] Regions => regions;

        public void Configure(int sectionIndex, int segmentsPerBiome, EnvironmentBiome biome)
        {
            int count = Mathf.Max(2, segmentsPerBiome);
            int within = ((sectionIndex % count) + count) % count;
            // Initial City has no preceding Beach; later cycles do.
            Entrance = sectionIndex > 0 && within == 0;
            Exit = sectionIndex >= 0 && within == count - 1;
            for (int i = 0; i < cityBuildings.Length && i < cityBuildingScales.Length; i++)
            {
                Transform building = cityBuildings[i];
                if (building == null) continue;
                float distance = Entrance ? building.localPosition.z : RunnerRules.SegmentLength - building.localPosition.z;
                bool outskirts = biome == EnvironmentBiome.City && (Entrance || Exit);
                Vector3 scale = cityBuildingScales[i];
                if (outskirts) scale.y *= Mathf.Lerp(0.35f, 1, Mathf.SmoothStep(0, 1, Mathf.Clamp01(distance / halfWidth)));
                building.localScale = scale;
                building.gameObject.SetActive(!outskirts || distance > 6);
            }
            if (block == null) block = new MaterialPropertyBlock();
            for (int i = 0; i < regions.Length; i++)
            {
                Region region = regions[i];
                if (region == null) continue;
                bool entering = i == (int)biome && Entrance, leaving = i == (int)biome && Exit;
                bool blending = entering || leaving;
                if (region.EntranceDressing != null) region.EntranceDressing.SetActive(entering);
                if (region.ExitDressing != null) region.ExitDressing.SetActive(leaving);
                int neighbor = (i + (entering ? BiomeRules.Count - 1 : 1)) % BiomeRules.Count;
                Material other = regions[neighbor]?.Floor;
                for (int surfaceIndex = 0; surfaceIndex < region.Ground.Length; surfaceIndex++)
                {
                    Renderer ground = region.Ground[surfaceIndex];
                    if (ground == null) continue;
                    if (surfaceIndex < region.NormalGround.Length && surfaceIndex < region.BoundaryGround.Length)
                    {
                        MeshFilter filter = ground.GetComponent<MeshFilter>();
                        if (filter != null) filter.sharedMesh = blending ? region.BoundaryGround[surfaceIndex] : region.NormalGround[surfaceIndex];
                    }
                    float offset = transform.InverseTransformPoint(ground.transform.position).z;
                    ground.GetPropertyBlock(block);
                    block.SetVector("_BiomeBoundary", new Vector4(blending ? 1 : 0,
                        (entering ? 0 : RunnerRules.SegmentLength) - offset, entering ? -1 : 1, halfWidth));
                    if (other != null)
                    {
                        block.SetColor("_NeighborColor", other.GetColor("_BaseColor"));
                        block.SetColor("_NeighborSecondary", other.GetColor("_SecondaryColor"));
                        block.SetVector("_NeighborSurface", new Vector4(other.GetFloat("_DetailType"),
                            other.GetFloat("_DetailScale"), other.GetFloat("_Smoothness"), 0));
                    }
                    ground.SetPropertyBlock(block); block.Clear();
                }
                // River ends narrow into sheltered pools. Ocean keeps its existing curved bays.
                foreach (Renderer water in region.Water)
                {
                    if (water == null) continue;
                    float offset = transform.InverseTransformPoint(water.transform.position).z;
                    water.GetPropertyBlock(block);
                    block.SetVector("_RiverEnds", new Vector4(entering ? 1 : 0, leaving ? 1 : 0,
                        -offset, RunnerRules.SegmentLength - offset));
                    water.SetPropertyBlock(block); block.Clear();
                }
            }
        }
    }
}

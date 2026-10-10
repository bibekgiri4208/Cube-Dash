using UnityEngine;

namespace CubeDash
{
    /// <summary>All biome scenery is authored. Recycling only toggles the existing roots.</summary>
    public sealed class SegmentEnvironment : MonoBehaviour
    {
        [Tooltip("City, Jungle, Mountains, Desert, Beach.")]
        [SerializeField] private GameObject[] biomes = new GameObject[BiomeRules.Count];
        public GameObject[] Biomes => biomes;
        public EnvironmentBiome CurrentBiome { get; private set; }
        public int SectionIndex { get; private set; }
        private BiomeBoundaryBlend boundaryBlend;

        public void SetHorizonEdges(bool rear, bool front)
        {
            foreach (GameObject biome in biomes)
                if (biome != null && biome.TryGetComponent(out HorizonBackdrop backdrop))
                    backdrop.SetEdges(biome.activeSelf && rear, biome.activeSelf && front);
        }

        public void Configure(int sectionIndex, int segmentsPerBiome)
        {
            SectionIndex = sectionIndex;
            CurrentBiome = BiomeRules.AtDistance(sectionIndex * RunnerRules.SegmentLength, segmentsPerBiome);
            for (int i = 0; i < biomes.Length; i++)
            {
                if (biomes[i] == null) continue;
                bool active = i == (int)CurrentBiome;
                biomes[i].SetActive(active);
                if (active && biomes[i].TryGetComponent(out BiomeScenery scenery)) scenery.SelectLayout(sectionIndex);
                if (active && biomes[i].TryGetComponent(out CoastalTransition coast))
                {
                    Transform cityGround = biomes[0] != null ? biomes[0].transform.Find("City Ground") : null;
                    Renderer ground = cityGround != null ? cityGround.GetComponent<Renderer>() : null;
                    Color tint = ground != null ? ground.sharedMaterial.GetColor("_BaseColor") : new Color(0.5f, 0.61f, 0.6f);
                    coast.Configure(sectionIndex, segmentsPerBiome, tint);
                }
            }
            if (boundaryBlend == null) boundaryBlend = GetComponent<BiomeBoundaryBlend>();
            if (boundaryBlend != null) boundaryBlend.Configure(sectionIndex, segmentsPerBiome, CurrentBiome);
        }
    }
}

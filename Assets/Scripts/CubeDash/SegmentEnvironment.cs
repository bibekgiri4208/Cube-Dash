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
            }
        }
    }
}

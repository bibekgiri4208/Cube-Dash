using UnityEngine;

namespace CubeDash
{
    /// <summary>Staggered cosmetic rise for recycled scenery; terrain and gameplay remain fixed.</summary>
    public sealed class SceneryRise : MonoBehaviour
    {
        [SerializeField] private Renderer[] scenery = new Renderer[0];
        [SerializeField] private Transform[] buildings = new Transform[0];
        [SerializeField] private Vector3[] buildingHomes = new Vector3[0];
        [SerializeField, Min(0.1f)] private float duration = 1.25f;
        [SerializeField, Range(0, 1)] private float stagger = 0.55f;
        [SerializeField, Min(1)] private float drop = 80;
        private MaterialPropertyBlock block;
        private float started;
        public bool Rising { get; private set; }
        private static readonly int Rise = Shader.PropertyToID("_SceneryRise");
        private static readonly int Stagger = Shader.PropertyToID("_SceneryStagger");

        public static float Progress(float age, float delay, float seconds)
            => Mathf.SmoothStep(0, 1, Mathf.Clamp01((age - delay) / Mathf.Max(0.1f, seconds)));

        public static float Delay(float x, float z, float spread)
            => (Mathf.Clamp01(z / RunnerRules.SegmentLength) * 0.75f
                + Mathf.Repeat(Mathf.Abs(x) * 0.137f, 1) * 0.25f) * spread;

        public void Begin(bool animate, float time)
        {
            Settle();
            if (!animate) return;
            started = time; Rising = true;
            if (block == null) block = new MaterialPropertyBlock();
            foreach (Renderer renderer in scenery)
            {
                if (renderer == null) continue;
                renderer.GetPropertyBlock(block);
                block.SetVector(Rise, new Vector4(1, started, drop, duration));
                block.SetFloat(Stagger, stagger);
                renderer.SetPropertyBlock(block); block.Clear();
            }
            Tick(time);
        }

        public void Tick(float time)
        {
            if (!Rising) return;
            float age = Mathf.Max(0, time - started);
            if (age >= duration + stagger) { Settle(); return; }
            for (int i = 0; i < buildings.Length && i < buildingHomes.Length; i++)
            {
                Transform building = buildings[i];
                if (building == null) continue;
                Vector3 home = buildingHomes[i];
                float progress = Progress(age, Delay(home.x, home.z, stagger), duration);
                building.localPosition = home + Vector3.down * drop * (1 - progress);
            }
        }

        public void Settle()
        {
            Rising = false;
            if (block == null) block = new MaterialPropertyBlock();
            foreach (Renderer renderer in scenery)
            {
                if (renderer == null) continue;
                renderer.GetPropertyBlock(block); block.SetVector(Rise, Vector4.zero);
                renderer.SetPropertyBlock(block); block.Clear();
            }
            for (int i = 0; i < buildings.Length && i < buildingHomes.Length; i++)
                if (buildings[i] != null) buildings[i].localPosition = buildingHomes[i];
        }

        private void OnDisable() => Settle();
    }
}

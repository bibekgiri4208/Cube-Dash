using UnityEngine;

namespace CubeDash
{
    /// <summary>Moves saved tree roots with the cosmetic road bend/reveal, retaining their materials and LODs.</summary>
    public sealed class CitySakuraTrees : MonoBehaviour
    {
        [SerializeField] private Renderer streets;
        [SerializeField] private Transform[] trees = new Transform[0];
        [SerializeField] private Vector3[] homes = new Vector3[0];
        private MaterialPropertyBlock block;
        public Transform[] Trees => trees;
        public Vector3[] Homes => homes;

        public void SyncPresentation()
        {
            if (streets == null || trees.Length != homes.Length) return;
            if (block == null) block = new MaterialPropertyBlock();
            streets.GetPropertyBlock(block);
            Vector4 rise = block.GetVector("_SceneryRise");
            float stagger = block.GetFloat("_SceneryStagger");
            Vector4 curve = Shader.GetGlobalVector("_CubeDashRoadCurve");
            var sampler = new RoadCurve.Sampler(curve.x, curve.y, curve.z);
            float time = Shader.GetGlobalFloat("_CubeDashEnvironmentTime");
            Matrix4x4 world = transform.localToWorldMatrix, local = transform.worldToLocalMatrix;
            Vector3 right = local.MultiplyVector(Vector3.right), up = local.MultiplyVector(Vector3.up);
            for (int i = 0; i < trees.Length; i++)
            {
                if (trees[i] == null) continue;
                Vector3 home = homes[i], point = world.MultiplyPoint3x4(home);
                float delay = SceneryRise.Delay(home.x, home.z, stagger);
                float reveal = SceneryRise.Progress(time - rise.y, delay, rise.w);
                trees[i].localPosition = home + right * sampler.Offset(point.z - curve.w)
                    - up * (rise.x * rise.z * (1 - reveal));
            }
        }

        private void LateUpdate() => SyncPresentation();
        private void OnDisable()
        {
            for (int i = 0; i < trees.Length && i < homes.Length; i++)
                if (trees[i] != null) trees[i].localPosition = homes[i];
        }
    }
}

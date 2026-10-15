using UnityEngine;

namespace CubeDash
{
    /// <summary>Fixed pool of model-shaped parts. No physics bodies, mesh generation or allocations on impact.</summary>
    public sealed class ObstacleDebris : MonoBehaviour
    {
        [SerializeField] private Transform[] pieces = new Transform[0];
        [SerializeField] private MeshRenderer[] renderers = new MeshRenderer[0];
        [Tooltip("Optional map-local model fragments. Null retains the shared highway breakup library.")]
        [SerializeField] private ObstacleBreakupCatalog catalogOverride = null;
        private Vector3[] velocities;
        private float[] lifetimes;
        private Vector3[] spins, extents;
        private float[] bounce, drag, ground;
        private MeshFilter[] filters;
        private ObstacleBreakupCatalog catalog;
        private MaterialPropertyBlock tint;
        private int cursor;
        private uint impacts;
        public int Capacity => pieces.Length;
        public int ActiveCount
        {
            get { int count = 0; foreach (Transform piece in pieces) if (piece.gameObject.activeSelf) count++; return count; }
        }

        private void Awake() => EnsureState();

        private void EnsureState()
        {
            if (lifetimes != null && lifetimes.Length == pieces.Length) return;
            lifetimes = new float[pieces.Length];
            velocities = new Vector3[pieces.Length];
            spins = new Vector3[pieces.Length]; extents = new Vector3[pieces.Length];
            bounce = new float[pieces.Length]; drag = new float[pieces.Length]; ground = new float[pieces.Length];
            filters = new MeshFilter[pieces.Length]; tint = new MaterialPropertyBlock();
            catalog = catalogOverride != null ? catalogOverride : Resources.Load<ObstacleBreakupCatalog>(ObstacleBreakupCatalog.ResourcePath);
            if (catalog == null || catalog.Fallback == null || catalog.Materials.Length != 9)
                throw new System.InvalidOperationException("Missing baked Resources/ObstacleBreakups model fragments.");
            for (int i = 0; i < pieces.Length; i++)
            {
                filters[i] = pieces[i].GetComponent<MeshFilter>();
                renderers[i].sharedMaterials = catalog.Materials;
            }
        }

        public void Shatter(RunnerCube cube, float remainingTravel)
        {
            EnsureState();
            if (pieces.Length == 0) return;
            Bounds bounds = cube.Collider.bounds;
            var model = catalog.For(cube.HazardSourceMesh);
            if (model == null) return;
            Quaternion rotation = cube.transform.rotation;
            Vector3 origin = bounds.center - Vector3.up * bounds.extents.y + Vector3.forward * remainingTravel;
            Color color = cube.ObstacleRenderer.sharedMaterial.GetColor("_BaseColor");
            tint.Clear(); tint.SetColor("_BaseColor", color);
            impacts++;
            for (int i = 0; i < model.fragments.Length; i++)
            {
                int slot = cursor++ % pieces.Length;
                var fragment = model.fragments[i];
                Vector3 radial = rotation * (fragment.center - Vector3.up * bounds.extents.y);
                radial.y = 0; radial = radial.sqrMagnitude > 0.001f ? radial.normalized : Vector3.right * ((i & 1) == 0 ? -1 : 1);
                Transform piece = pieces[slot];
                piece.SetPositionAndRotation(origin + rotation * fragment.center, rotation);
                Vector3 parentScale = piece.parent != null ? piece.parent.lossyScale : Vector3.one;
                piece.localScale = new Vector3(1 / parentScale.x, 1 / parentScale.y, 1 / parentScale.z);
                filters[slot].sharedMesh = fragment.mesh;
                Bounds culling = fragment.mesh.bounds; culling.Expand(Vector3.one * 400); renderers[slot].localBounds = culling;
                renderers[slot].SetPropertyBlock(tint, 0); // Only paint changes color; tires, glazing, alloy and concrete retain their surfaces.
                float variation = Hash(impacts * 131u + (uint)i * 79u);
                bool wheel = fragment.role == ObstacleFragmentRole.Wheel;
                bool glass = fragment.role == ObstacleFragmentRole.Glass;
                bool concrete = fragment.role == ObstacleFragmentRole.Concrete;
                float outward = concrete ? 2.6f : glass ? 4.8f : 3.5f;
                velocities[slot] = radial * outward + Vector3.forward * (4.5f + variation * 2) + Vector3.up * (concrete ? 2.8f : wheel ? 3.5f : 4.2f + variation * 1.8f);
                spins[slot] = wheel ? new Vector3(420, 80, 50) : new Vector3(130 + variation * 190, (i % 2 == 0 ? 1 : -1) * (110 + variation * 170), 180 + variation * 210);
                bounce[slot] = wheel ? 0.55f : concrete ? 0.14f : glass ? 0.1f : 0.28f;
                drag[slot] = wheel ? 0.25f : concrete ? 1.4f : glass ? 2.3f : 0.9f;
                lifetimes[slot] = glass ? 0.85f : wheel ? 1.65f : concrete ? 1.25f : 1.35f;
                Bounds shape = fragment.mesh.bounds;
                extents[slot] = Vector3.Max(Abs(shape.min), Abs(shape.max)); ground[slot] = bounds.min.y;
                piece.gameObject.SetActive(true);
            }
        }

        public void Tick(float dt, float travel)
        {
            EnsureState();
            dt = Mathf.Max(0, dt);
            for (int i = 0; i < pieces.Length; i++)
            {
                if (lifetimes[i] <= 0) continue;
                lifetimes[i] = Mathf.Max(0, lifetimes[i] - dt);
                Transform piece = pieces[i];
                velocities[i] += Vector3.down * (14 * dt);
                velocities[i].x *= Mathf.Max(0, 1 - drag[i] * dt);
                velocities[i].z *= Mathf.Max(0, 1 - drag[i] * dt);
                piece.position += velocities[i] * dt + Vector3.back * travel;
                piece.Rotate(spins[i] * dt, Space.Self);
                // Rotating doors/tires must rest above the road, rather than clipping through it at a fixed cube height.
                Matrix4x4 matrix = Matrix4x4.Rotate(piece.rotation);
                float floor = ground[i] + Mathf.Abs(matrix.m10) * extents[i].x + Mathf.Abs(matrix.m11) * extents[i].y + Mathf.Abs(matrix.m12) * extents[i].z;
                if (piece.position.y < floor)
                {
                    piece.position = new Vector3(piece.position.x, floor, piece.position.z);
                    velocities[i] = new Vector3(velocities[i].x * 0.7f, Mathf.Abs(velocities[i].y) * bounce[i], velocities[i].z * 0.7f);
                    spins[i] *= 0.75f;
                }
                if (lifetimes[i] < 0.25f) piece.localScale *= Mathf.Max(0, 1 - dt * 12);
                if (lifetimes[i] <= 0) piece.gameObject.SetActive(false);
            }
        }

        public void ResetDebris()
        {
            EnsureState();
            cursor = 0;
            impacts = 0;
            for (int i = 0; i < pieces.Length; i++) { lifetimes[i] = 0; pieces[i].gameObject.SetActive(false); }
        }

        public void ShiftWorldOrigin(float offset)
        {
            foreach (Transform piece in pieces) piece.position += Vector3.back * offset;
        }

        private static float Hash(uint value)
        {
            unchecked { value ^= value >> 16; value *= 0x7feb352d; value ^= value >> 15; return (value & 65535u) / 65535f; }
        }
        private static Vector3 Abs(Vector3 value) => new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
    }
}

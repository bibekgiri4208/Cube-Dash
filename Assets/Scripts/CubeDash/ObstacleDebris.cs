using UnityEngine;

namespace CubeDash
{
    /// <summary>Fixed, editor-authored fragment pool. No physics bodies or allocations on impact.</summary>
    public sealed class ObstacleDebris : MonoBehaviour
    {
        [SerializeField] private Transform[] pieces = new Transform[0];
        [SerializeField] private MeshRenderer[] renderers = new MeshRenderer[0];
        private Vector3[] velocities;
        private float[] lifetimes;
        private int cursor;
        public int Capacity => pieces.Length;
        public int ActiveCount
        {
            get { int count = 0; foreach (Transform piece in pieces) if (piece.gameObject.activeSelf) count++; return count; }
        }

        private void EnsureState()
        {
            if (lifetimes != null && lifetimes.Length == pieces.Length) return;
            lifetimes = new float[pieces.Length];
            velocities = new Vector3[pieces.Length];
        }

        public void Shatter(RunnerCube cube, float remainingTravel)
        {
            EnsureState();
            if (pieces.Length == 0) return;
            Bounds bounds = cube.Collider.bounds;
            Material material = cube.GetComponentInChildren<Renderer>().sharedMaterial;
            for (int i = 0; i < 8; i++)
            {
                int slot = cursor++ % pieces.Length;
                Vector3 direction = new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1);
                Transform piece = pieces[slot];
                piece.position = bounds.center + Vector3.forward * remainingTravel + Vector3.Scale(bounds.extents * 0.5f, direction);
                piece.localScale = bounds.size * 0.38f;
                piece.rotation = Quaternion.Euler(i * 17, i * 31, i * 11);
                renderers[slot].sharedMaterial = material;
                velocities[slot] = new Vector3(direction.x * (3.5f + i * 0.15f), 4 + (i % 3), direction.z * 2 + 5);
                lifetimes[slot] = 1.15f;
                piece.gameObject.SetActive(true);
            }
        }

        public void Tick(float dt, float travel)
        {
            EnsureState();
            for (int i = 0; i < pieces.Length; i++)
            {
                if (lifetimes[i] <= 0) continue;
                lifetimes[i] = Mathf.Max(0, lifetimes[i] - dt);
                Transform piece = pieces[i];
                velocities[i] += Vector3.down * (14 * dt);
                piece.position += velocities[i] * dt + Vector3.back * travel;
                piece.Rotate(new Vector3(143, 97, 121) * dt, Space.Self);
                if (piece.position.y < 0.12f)
                {
                    piece.position = new Vector3(piece.position.x, 0.12f, piece.position.z);
                    velocities[i] = new Vector3(velocities[i].x * 0.7f, Mathf.Abs(velocities[i].y) * 0.3f, velocities[i].z * 0.7f);
                }
                if (lifetimes[i] < 0.25f) piece.localScale *= Mathf.Max(0, 1 - dt * 12);
                if (lifetimes[i] <= 0) piece.gameObject.SetActive(false);
            }
        }

        public void ResetDebris()
        {
            EnsureState();
            cursor = 0;
            for (int i = 0; i < pieces.Length; i++) { lifetimes[i] = 0; pieces[i].gameObject.SetActive(false); }
        }
    }
}

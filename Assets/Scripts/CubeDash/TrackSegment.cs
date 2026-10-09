using UnityEngine;

namespace CubeDash
{
    /// <summary>Scene/prefab references and authored positions for reusable three-lane obstacle/reward slots.</summary>
    public sealed class TrackSegment : MonoBehaviour
    {
        [Tooltip("Row 1 left/centre/right, then row 2, then row 3.")]
        [SerializeField] private BoxCollider[] obstacles = new BoxCollider[9];
        [SerializeField] private RunnerCube[] cubes = new RunnerCube[9];
        [Tooltip("Optional bonus before each coin row, in row order; midpoint slots never overlap coins.")]
        [SerializeField] private PowerUpPickup[] powerUps = new PowerUpPickup[3];
        [SerializeField] private SegmentEnvironment environment = null;
        private Vector3[] cubeHomePositions;
        private Vector3[] powerUpHomePositions;
        public BoxCollider[] Obstacles => obstacles;
        public RunnerCube[] Cubes => cubes;
        public PowerUpPickup[] PowerUps => powerUps;
        public SegmentEnvironment Environment => environment;

        public Vector3 CubeHomePosition(int index)
        {
            CacheLayout();
            return cubeHomePositions[index];
        }

        public Vector3 PowerUpHomePosition(int row)
        {
            CacheLayout();
            return powerUpHomePositions[row];
        }

        private void CacheLayout()
        {
            if (cubeHomePositions != null) return;
            // Cache the authored slot positions once. Recycling must not accumulate stagger offsets.
            cubeHomePositions = new Vector3[cubes.Length];
            for (int i = 0; i < cubes.Length; i++) cubeHomePositions[i] = cubes[i].transform.localPosition;
            powerUpHomePositions = new Vector3[powerUps.Length];
            for (int i = 0; i < powerUps.Length; i++)
                if (powerUps[i] != null) powerUpHomePositions[i] = powerUps[i].transform.localPosition;
        }
    }
}

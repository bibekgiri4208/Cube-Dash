using UnityEngine;

namespace CubeDash
{
    /// <summary>Scene/prefab references to three rows of three editable obstacle cubes.</summary>
    public sealed class TrackSegment : MonoBehaviour
    {
        [Tooltip("Row 1 left/centre/right, then row 2, then row 3.")]
        [SerializeField] private BoxCollider[] obstacles = new BoxCollider[9];
        [SerializeField] private RunnerCube[] cubes = new RunnerCube[9];
        [Tooltip("Optional bonus pickup per row, in row order.")]
        [SerializeField] private PowerUpPickup[] powerUps = new PowerUpPickup[3];
        public BoxCollider[] Obstacles => obstacles;
        public RunnerCube[] Cubes => cubes;
        public PowerUpPickup[] PowerUps => powerUps;
    }
}

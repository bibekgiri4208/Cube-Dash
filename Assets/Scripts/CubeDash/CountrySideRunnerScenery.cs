using UnityEngine;

namespace CubeDash
{
    /// <summary>Scrolls saved countryside tiles from track distance, without generating scenery at runtime.</summary>
    public sealed class CountrySideRunnerScenery : MonoBehaviour
    {
        [SerializeField] private EndlessTrack track;
        [SerializeField] private Transform[] tiles = new Transform[0];
        [SerializeField, Min(1)] private float tileLength = 284;
        [SerializeField] private float tileFront = 248;
        [SerializeField] private float recycleBehind = -32;

        public EndlessTrack Track => track;
        public Transform[] Tiles => tiles;
        public float TileLength => tileLength;

        public static float TilePosition(float distance, int index, int count, float length, float front, float behind)
        {
            float loop = length * count;
            float z = index * length - Mathf.Repeat(Mathf.Max(0, distance), loop);
            if (z + front < behind) z += loop;
            return z;
        }

        public void SyncDistance()
        {
            if (track == null || tiles.Length == 0) return;
            for (int i = 0; i < tiles.Length; i++)
            {
                if (tiles[i] == null) continue;
                Vector3 position = tiles[i].localPosition;
                position.z = TilePosition(track.Travelled, i, tiles.Length, tileLength, tileFront, recycleBehind);
                tiles[i].localPosition = position;
            }
        }

        private void LateUpdate() => SyncDistance();
    }
}

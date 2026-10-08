using System.Collections.Generic;
using UnityEngine;

namespace CubeDash
{
    /// <summary>Recycles authored track sections and resolves color-cube contacts in time order.</summary>
    public sealed class EndlessTrack : MonoBehaviour
    {
        private struct Contact
        {
            public RunnerCube Cube;
            public float Time;
        }

        [SerializeField, Min(2.2f)] private float laneWidth = 2.6f;
        [SerializeField] private TrackSegment[] segments = new TrackSegment[0];
        [Tooltip("Red, Blue, Green, in that order. Also used by the player.")]
        [SerializeField] private Material[] colorMaterials = new Material[3];
        private readonly List<Contact> contacts = new List<Contact>(12);
        private System.Random random;
        private int matchingLane = 1;
        private CubeColor playerColor;
        private bool firstVisibleRow;

        public float LaneWidth => laneWidth;
        public TrackSegment[] Segments => segments;
        public Material ColorMaterial(CubeColor color) => colorMaterials[(int)color];

        public void Reset(int seed, CubeColor color = CubeColor.Red)
        {
            random = new System.Random(seed);
            playerColor = color;
            matchingLane = 1;
            firstVisibleRow = true;
            for (int i = 0; i < segments.Length; i++)
            {
                segments[i].transform.localPosition = new Vector3(0, 0, (i - 1) * RunnerRules.SegmentLength);
                Populate(segments[i], i == 0 ? 3 : i == 1 ? 1 : 0);
            }
        }

        public bool Advance(float travel, float previousPlayerX, float playerX, float difficulty,
            Vector2 playerHalfSize, float playerZ = 0f)
        {
            return Advance(travel, previousPlayerX, playerX, difficulty, playerHalfSize, playerZ, out _);
        }

        public bool Advance(float travel, float previousPlayerX, float playerX, float difficulty,
            Vector2 playerHalfSize, float playerZ, out int collected)
        {
            Physics.SyncTransforms();
            contacts.Clear();
            collected = 0;
            float furthest = float.MinValue;
            foreach (TrackSegment segment in segments)
            {
                foreach (RunnerCube cube in segment.Cubes)
                {
                    if (!cube.gameObject.activeInHierarchy || !cube.Collider.enabled) continue;
                    Bounds bounds = cube.Collider.bounds;
                    Vector2 size = playerHalfSize + new Vector2(bounds.extents.x, bounds.extents.z);
                    if (RunnerRules.SweptHit(new Vector2(previousPlayerX, playerZ), new Vector2(playerX, playerZ),
                        new Vector2(bounds.center.x, bounds.center.z),
                        new Vector2(bounds.center.x, bounds.center.z - travel), size, out float time))
                        contacts.Add(new Contact { Cube = cube, Time = time });
                }
                segment.transform.position += Vector3.back * travel;
                furthest = Mathf.Max(furthest, segment.transform.position.z);
            }

            contacts.Sort((first, second) =>
            {
                int order = first.Time.CompareTo(second.Time);
                // A wrong-color touch wins ties. Never award pickups beyond a fatal contact.
                if (order == 0) return (first.Cube.Color == playerColor ? 1 : 0).CompareTo(second.Cube.Color == playerColor ? 1 : 0);
                return order;
            });
            bool hitWrongColor = false;
            foreach (Contact contact in contacts)
            {
                if (contact.Cube.Color != playerColor) { hitWrongColor = true; break; }
                contact.Cube.gameObject.SetActive(false);
                collected++;
            }

            while (true)
            {
                TrackSegment oldest = null;
                foreach (TrackSegment segment in segments)
                    if (segment.transform.position.z + RunnerRules.SegmentLength < playerZ - 18f &&
                        (oldest == null || segment.transform.position.z < oldest.transform.position.z)) oldest = segment;
                if (oldest == null) break;
                furthest += RunnerRules.SegmentLength;
                oldest.transform.position = new Vector3(transform.position.x, transform.position.y, furthest);
                Populate(oldest, 0);
            }
            return hitWrongColor;
        }

        private void Populate(TrackSegment segment, int warmupRows)
        {
            for (int row = 0; row < 3; row++)
            {
                bool visible = row >= warmupRows;
                if (visible)
                {
                    if (!firstVisibleRow) matchingLane = RunnerRules.NextSafeLane(random, matchingLane);
                    firstVisibleRow = false;
                }
                bool swapOthers = random.Next(2) == 0;
                for (int lane = 0; lane < 3; lane++)
                {
                    RunnerCube cube = segment.Cubes[row * 3 + lane];
                    CubeColor color = RunnerRules.ColorForLane(lane, matchingLane, playerColor, swapOthers);
                    cube.Configure(color, ColorMaterial(color));
                    Vector3 position = cube.transform.localPosition;
                    position.x = (lane - 1) * laneWidth;
                    cube.transform.localPosition = position;
                    cube.gameObject.SetActive(visible);
                }
            }
        }
    }
}

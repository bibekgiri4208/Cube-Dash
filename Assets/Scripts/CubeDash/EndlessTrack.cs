using System.Collections.Generic;
using UnityEngine;

namespace CubeDash
{
    /// <summary>Recycles authored track sections and resolves color-cube and bonus contacts in time order.</summary>
    public sealed class EndlessTrack : MonoBehaviour
    {
        private struct Contact
        {
            public RunnerCube Cube;
            public float Time;
        }

        private struct PowerUpContact
        {
            public PowerUpPickup Pickup;
            public float Time;
        }

        [SerializeField, Min(2.2f)] private float laneWidth = 2.6f;
        [SerializeField] private TrackSegment[] segments = new TrackSegment[0];
        [Tooltip("Red, Blue, Green, in that order. Also used by the player.")]
        [SerializeField] private Material[] colorMaterials = new Material[3];
        [Tooltip("Shield, Double Points, in that order.")]
        [SerializeField] private Material[] powerUpMaterials = new Material[2];
        [Tooltip("Chance that a visible row carries a bonus in its safe lane.")]
        [SerializeField, Range(0f, 0.5f)] private float powerUpChance = 0.16f;
        [SerializeField] private EnvironmentDirector environment = null;
        private readonly List<Contact> contacts = new List<Contact>(12);
        private readonly List<PowerUpContact> powerUpContacts = new List<PowerUpContact>(3);
        private readonly List<PowerUpType> collectedPowerUps = new List<PowerUpType>(3);
        private System.Random random;
        private int matchingLane = 1;
        private CubeColor playerColor;
        private bool firstVisibleRow;
        private float travelled;
        private int nextSectionIndex;

        public float LaneWidth => laneWidth;
        public TrackSegment[] Segments => segments;
        public Material ColorMaterial(CubeColor color) => colorMaterials[(int)color];
        public Material PowerUpMaterial(PowerUpType type) => powerUpMaterials[(int)type];
        public float PowerUpChance => powerUpChance;
        public EnvironmentDirector Environment => environment;
        public float Travelled => travelled;
        /// <summary>Bonuses collected by the most recent Advance call.</summary>
        public IReadOnlyList<PowerUpType> LastPowerUps => collectedPowerUps;

        public void Reset(int seed, CubeColor color = CubeColor.Red)
        {
            random = new System.Random(seed);
            playerColor = color;
            matchingLane = 1;
            firstVisibleRow = true;
            travelled = 0;
            nextSectionIndex = segments.Length - 1;
            if (environment != null) environment.ResetRun();
            for (int i = 0; i < segments.Length; i++)
            {
                segments[i].transform.localPosition = new Vector3(0, 0, (i - 1) * RunnerRules.SegmentLength);
                Populate(segments[i], i == 0 ? 3 : i == 1 ? 1 : 0);
                ConfigureEnvironment(segments[i], i - 1);
            }
        }

        public bool Advance(float travel, float previousPlayerX, float playerX, float difficulty,
            Vector2 playerHalfSize, float playerZ = 0f)
        {
            return Advance(travel, previousPlayerX, playerX, difficulty, playerHalfSize, playerZ, false,
                out _, out _);
        }

        public bool Advance(float travel, float previousPlayerX, float playerX, float difficulty,
            Vector2 playerHalfSize, float playerZ, out int collected)
        {
            return Advance(travel, previousPlayerX, playerX, difficulty, playerHalfSize, playerZ, false,
                out collected, out _);
        }

        /// <param name="shield">Absorbs the first wrong-color contact instead of ending the run.</param>
        public bool Advance(float travel, float previousPlayerX, float playerX, float difficulty,
            Vector2 playerHalfSize, float playerZ, bool shield,
            out int collected, out bool shieldUsed)
        {
            Physics.SyncTransforms();
            contacts.Clear();
            powerUpContacts.Clear();
            collectedPowerUps.Clear();
            collected = 0;
            shieldUsed = false;
            travelled += travel;
            if (environment != null) environment.ApplyDistance(travelled, Time.deltaTime);
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
                PowerUpPickup[] pickups = segment.PowerUps;
                for (int i = 0; i < pickups.Length; i++)
                {
                    PowerUpPickup pickup = pickups[i];
                    if (pickup == null || !pickup.gameObject.activeInHierarchy || !pickup.Collider.enabled) continue;
                    Bounds bounds = pickup.Collider.bounds;
                    Vector2 size = playerHalfSize + new Vector2(bounds.extents.x, bounds.extents.z);
                    if (RunnerRules.SweptHit(new Vector2(previousPlayerX, playerZ), new Vector2(playerX, playerZ),
                        new Vector2(bounds.center.x, bounds.center.z),
                        new Vector2(bounds.center.x, bounds.center.z - travel), size, out float time))
                        powerUpContacts.Add(new PowerUpContact { Pickup = pickup, Time = time });
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
            float fatalTime = float.MaxValue;
            bool hitWrongColor = false;
            foreach (Contact contact in contacts)
            {
                if (contact.Cube.Color != playerColor)
                {
                    if (shield) { shield = false; shieldUsed = true; contact.Cube.gameObject.SetActive(false); continue; }
                    hitWrongColor = true;
                    fatalTime = contact.Time;
                    break;
                }
                contact.Cube.gameObject.SetActive(false);
                collected++;
            }

            powerUpContacts.Sort((first, second) => first.Time.CompareTo(second.Time));
            foreach (PowerUpContact contact in powerUpContacts)
            {
                if (contact.Time > fatalTime) break;
                contact.Pickup.gameObject.SetActive(false);
                collectedPowerUps.Add(contact.Pickup.Type);
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
                Populate(oldest, 0, difficulty);
                ConfigureEnvironment(oldest, nextSectionIndex++);
            }
            return hitWrongColor;
        }

        private void Populate(TrackSegment segment, int warmupRows, float difficulty = 0f)
        {
            for (int row = 0; row < 3; row++)
            {
                bool visible = row >= warmupRows;
                if (visible)
                {
                    if (!firstVisibleRow) matchingLane = RunnerRules.NextSafeLane(random, matchingLane, difficulty);
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
                PlacePowerUp(segment, row, visible);
            }
        }

        private void ConfigureEnvironment(TrackSegment segment, int sectionIndex)
        {
            if (environment != null && segment.Environment != null)
                segment.Environment.Configure(sectionIndex, environment.SegmentsPerBiome);
        }

        private void PlacePowerUp(TrackSegment segment, int row, bool visible)
        {
            PowerUpPickup[] pickups = segment.PowerUps;
            if (row >= pickups.Length) return;
            PowerUpPickup pickup = pickups[row];
            if (pickup == null) return;
            bool spawn = visible && powerUpMaterials != null && powerUpMaterials.Length > 0 &&
                random.NextDouble() < powerUpChance;
            if (!spawn)
            {
                pickup.gameObject.SetActive(false);
                return;
            }
            // Always in the safe lane so a bonus never forces a collision.
            PowerUpType type = (PowerUpType)random.Next(2);
            pickup.Configure(type, PowerUpMaterial(type));
            Vector3 position = pickup.transform.localPosition;
            position.x = (matchingLane - 1) * laneWidth;
            pickup.transform.localPosition = position;
            pickup.gameObject.SetActive(true);
        }
    }
}

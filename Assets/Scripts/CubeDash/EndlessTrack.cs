using System.Collections.Generic;
using UnityEngine;

namespace CubeDash
{
    /// <summary>Recycles authored track sections and resolves obstacle, coin and bonus contacts in time order.</summary>
    public sealed class EndlessTrack : MonoBehaviour
    {
        private struct Contact
        {
            public RunnerCube Cube;
            public float Time;
            public bool Attracted;
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
        [Tooltip("Shield, Double Points, Fighter Plane, Truck, Magnet, in that order.")]
        [SerializeField] private Material[] powerUpMaterials = new Material[5];
        [SerializeField] private ObstacleDebris debris = null;
        [Tooltip("Bonus chance per eligible row. Distance gaps and rare, shared Truck/Flight cooldowns also apply.")]
        [SerializeField, Range(0f, 0.5f)] private float powerUpChance = 0.16f;
        [SerializeField] private EnvironmentDirector environment = null;
        [Header("Highway progression")]
        [SerializeField] private bool patternedEncounters = true;
        [SerializeField, Range(0, 1)] private float roadCurveStrength = 1f;
        [Tooltip("Optional map-local traffic art. Null retains the shared highway models and collision categories.")]
        [SerializeField] private HighwayHazardVisuals hazardVisuals = null;
        private HighwayPatterns patterns;
        private PowerUpSpawns powerUpSpawns;
        private HighwayPresentation presentation;
        private float curvePhase;
        private float curveOriginZ;
        private readonly List<Contact> contacts = new List<Contact>(12);
        private readonly List<PowerUpContact> powerUpContacts = new List<PowerUpContact>(3);
        private readonly List<PowerUpType> collectedPowerUps = new List<PowerUpType>(3);
        private System.Random random;
        private int matchingLane = 1;
        private int previousBlockedMask;
        private CubeColor playerColor;
        private bool firstVisibleRow;
        private float travelled;
        private int nextSectionIndex;
        private readonly int[] hazardVariants = new int[4];

        public float LaneWidth => laneWidth;
        public TrackSegment[] Segments => segments;
        public Material ColorMaterial(CubeColor color) => PresentationMaterial(colorMaterials[(int)color]);
        public Material PowerUpMaterial(PowerUpType type) => PresentationMaterial(powerUpMaterials[(int)type]);
        public Material PresentationMaterial(Material material) => presentation != null ? presentation.MaterialFor(material) : material;
        public void PreparePresentation(Transform player)
        {
            if (!Application.isPlaying || presentation != null) return;
            presentation = new HighwayPresentation(hazardVisuals);
            presentation.Prepare(transform);
            presentation.Prepare(player);
            foreach (TrackSegment segment in segments)
                foreach (RunnerCube cube in segment.Cubes) presentation.Prewarm(cube);
        }
        public float PowerUpChance => powerUpChance;
        public EnvironmentDirector Environment => environment;
        public float Travelled => travelled;
        public bool PlayerMovesForward { get; private set; }
        public float WorldOriginOffset { get; private set; }
        public ObstacleDebris Debris => debris;
        /// <summary>Actual obstacle destructions in the latest sweep; coins and shield hits are excluded.</summary>
        public int LastTruckSmashes { get; private set; }
        /// <summary>Bonuses collected by the most recent Advance call.</summary>
        public IReadOnlyList<PowerUpType> LastPowerUps => collectedPowerUps;

        /// <summary>Origin-centred authoring/relative-motion simulation. Live gameplay uses ResetForward.</summary>
        public void Reset(int seed, CubeColor color = CubeColor.Red)
            => ResetRun(seed, color, false, 0);

        /// <summary>Start a world-space run. Only sections behind the player are relocated.</summary>
        public void ResetForward(int seed, CubeColor color, float playerZ)
            => ResetRun(seed, color, true, playerZ);

        private void ResetRun(int seed, CubeColor color, bool forward, float playerZ)
        {
            random = new System.Random(seed);
            patterns = new HighwayPatterns(seed);
            powerUpSpawns = new PowerUpSpawns(seed);
            curvePhase = RoadCurve.Phase(seed);
            playerColor = color;
            matchingLane = 1;
            previousBlockedMask = 0;
            firstVisibleRow = true;
            travelled = 0;
            PlayerMovesForward = forward;
            WorldOriginOffset = 0;
            curveOriginZ = playerZ;
            contacts.Clear(); powerUpContacts.Clear(); collectedPowerUps.Clear();
            LastTruckSmashes = 0;
            nextSectionIndex = segments.Length - 1;
            System.Array.Clear(hazardVariants, 0, hazardVariants.Length);
            if (debris != null) debris.ResetDebris();
            if (environment != null) environment.ResetRun();
            for (int i = 0; i < segments.Length; i++)
            {
                segments[i].transform.localPosition = new Vector3(0, 0, (i - 1) * RunnerRules.SegmentLength);
                Populate(segments[i], i == 0 ? 3 : i == 1 ? 1 : 0, 0, (i - 1) * RunnerRules.SegmentLength);
                ConfigureEnvironment(segments[i], i - 1);
                SceneryRise rise = segments[i].GetComponent<SceneryRise>();
                // Only the original farthest spawn slot rises; closer starting scenery is already present.
                if (rise != null) rise.Begin(i == segments.Length - 1, 0);
            }
            UpdateHorizonEdges();
            if (Application.isPlaying) RoadCurve.Apply(0, curvePhase, roadCurveStrength, curveOriginZ);
        }

        /// <summary>Rare precision reset; distance/progression remain continuous and relative positions do not change.</summary>
        public void ShiftWorldOrigin(float offset)
        {
            WorldOriginOffset += offset;
            foreach (TrackSegment segment in segments) segment.transform.position += Vector3.back * offset;
            if (debris != null) debris.ShiftWorldOrigin(offset);
            curveOriginZ -= offset;
            if (Application.isPlaying) RoadCurve.Apply(travelled, curvePhase, roadCurveStrength, curveOriginZ);
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
            => Advance(travel, previousPlayerX, playerX, difficulty, playerHalfSize, playerZ, shield, false, false,
                out collected, out shieldUsed);

        /// <param name="airborne">Flight ignores ground hazards/bonuses and automatically attracts current-lane coins.</param>
        /// <param name="landingShield">Continuous protection: absorbs every wrong-color hit without spending a shield charge.</param>
        public bool Advance(float travel, float previousPlayerX, float playerX, float difficulty,
            Vector2 playerHalfSize, float playerZ, bool shield, bool airborne, bool landingShield,
            out int collected, out bool shieldUsed)
            => Advance(travel, previousPlayerX, playerX, difficulty, playerHalfSize, playerZ, shield,
                airborne, landingShield, false, out collected, out shieldUsed);

        public bool Advance(float travel, float previousPlayerX, float playerX, float difficulty,
            Vector2 playerHalfSize, float playerZ, bool shield, bool airborne, bool landingShield, bool trucking,
            out int collected, out bool shieldUsed)
            => Advance(travel, previousPlayerX, playerX, difficulty, playerHalfSize, playerZ,
                shield, airborne, landingShield, trucking, false, 8, new Vector3(playerX, 0.575f, playerZ), Time.deltaTime,
                out collected, out shieldUsed);

        public bool Advance(float travel, float previousPlayerX, float playerX, float difficulty,
            Vector2 playerHalfSize, float playerZ, bool shield, bool airborne, bool landingShield, bool trucking,
            bool magnet, float magnetRange, Vector3 magnetTarget, float dt, out int collected, out bool shieldUsed)
            => AdvanceRun(travel, previousPlayerX, playerX, difficulty, playerHalfSize, playerZ,
                shield, airborne, landingShield, trucking, magnet, magnetRange, magnetTarget, dt, false,
                out collected, out shieldUsed);

        /// <summary>Sweep the advancing player through stationary sections. playerZ is the end-of-frame contact centre.</summary>
        public bool AdvanceForward(float travel, float previousPlayerX, float playerX, float difficulty,
            Vector2 playerHalfSize, float playerZ, bool shield, bool airborne, bool landingShield, bool trucking,
            bool magnet, float magnetRange, Vector3 magnetTarget, float dt, out int collected, out bool shieldUsed)
            => AdvanceRun(travel, previousPlayerX, playerX, difficulty, playerHalfSize, playerZ,
                shield, airborne, landingShield, trucking, magnet, magnetRange, magnetTarget, dt, true,
                out collected, out shieldUsed);

        private bool AdvanceRun(float travel, float previousPlayerX, float playerX, float difficulty,
            Vector2 playerHalfSize, float playerZ, bool shield, bool airborne, bool landingShield, bool trucking,
            bool magnet, float magnetRange, Vector3 magnetTarget, float dt, bool forward,
            out int collected, out bool shieldUsed)
        {
            Physics.SyncTransforms();
            contacts.Clear();
            powerUpContacts.Clear();
            collectedPowerUps.Clear();
            LastTruckSmashes = 0;
            collected = 0;
            shieldUsed = false;
            PlayerMovesForward = forward;
            float scroll = forward ? 0 : travel;
            Vector2 playerFrom = new Vector2(previousPlayerX, forward ? playerZ - travel : playerZ);
            Vector2 playerTo = new Vector2(playerX, playerZ);
            travelled += travel;
            curveOriginZ = playerZ;
            if (Application.isPlaying) RoadCurve.Apply(travelled, curvePhase, roadCurveStrength, playerZ);
            if (debris != null) debris.Tick(dt, scroll);
            if (environment != null) environment.ApplyDistance(travelled, Time.deltaTime);
            float furthest = float.MinValue;
            foreach (TrackSegment segment in segments)
            {
                foreach (RunnerCube cube in segment.Cubes)
                {
                    if (cube.gameObject.activeInHierarchy) cube.TickCoin(dt, magnet || airborne);
                    if (airborne || !cube.gameObject.activeInHierarchy || !cube.Collider.enabled) continue;
                    Bounds bounds = cube.Collider.bounds;
                    Vector2 size = playerHalfSize + new Vector2(bounds.extents.x, bounds.extents.z);
                    if (RunnerRules.SweptHit(playerFrom, playerTo,
                        new Vector2(bounds.center.x, bounds.center.z),
                        new Vector2(bounds.center.x, bounds.center.z - scroll), size, out float time))
                        contacts.Add(new Contact { Cube = cube, Time = time });
                }
                PowerUpPickup[] pickups = segment.PowerUps;
                for (int i = 0; i < pickups.Length; i++)
                {
                    PowerUpPickup pickup = pickups[i];
                    if (pickup != null && pickup.gameObject.activeInHierarchy) pickup.Tick(dt);
                    if (airborne || pickup == null || !pickup.gameObject.activeInHierarchy || !pickup.Collider.enabled) continue;
                    Bounds bounds = pickup.Collider.bounds;
                    Vector2 size = playerHalfSize + new Vector2(bounds.extents.x, bounds.extents.z);
                    if (RunnerRules.SweptHit(playerFrom, playerTo,
                        new Vector2(bounds.center.x, bounds.center.z),
                        new Vector2(bounds.center.x, bounds.center.z - scroll), size, out float time))
                        powerUpContacts.Add(new PowerUpContact { Pickup = pickup, Time = time });
                }
                if (!forward) segment.transform.position += Vector3.back * scroll;
                furthest = Mathf.Max(furthest, segment.transform.position.z);
            }

            float fatalTime = float.MaxValue;
            float takeoffTime = float.MaxValue;
            float truckTime = trucking ? 0 : float.MaxValue;
            foreach (PowerUpContact contact in powerUpContacts)
            {
                if (contact.Pickup.Type == PowerUpType.Truck) truckTime = Mathf.Min(truckTime, contact.Time);
            }
            foreach (PowerUpContact contact in powerUpContacts)
            {
                // Truck blocks flight from its swept pickup time, including simultaneous pickups.
                if (contact.Pickup.Type == PowerUpType.FighterPlane && contact.Time < truckTime)
                    takeoffTime = Mathf.Min(takeoffTime, contact.Time);
            }
            float magnetTime = magnet ? 0 : float.MaxValue;
            foreach (PowerUpContact contact in powerUpContacts)
                if (contact.Pickup.Type == PowerUpType.Magnet && contact.Time < takeoffTime)
                    magnetTime = Mathf.Min(magnetTime, contact.Time);
            float flightTime = airborne ? 0 : takeoffTime;
            if (magnetRange > 0 && dt > 0)
            {
                if (magnetTime <= 1)
                    PullCoins(scroll, playerFrom, playerTo, magnetRange, magnetTarget, dt, magnetTime, false);
                else if (flightTime <= 1)
                    PullCoins(scroll, playerFrom, playerTo, magnetRange, magnetTarget, dt, flightTime, true);
            }
            contacts.Sort((first, second) =>
            {
                int order = first.Time.CompareTo(second.Time);
                // A wrong-color touch wins ties. Never award coins beyond a fatal contact.
                if (order == 0) return (first.Cube.Color == playerColor ? 1 : 0).CompareTo(second.Cube.Color == playerColor ? 1 : 0);
                return order;
            });
            bool hitWrongColor = false;
            foreach (Contact contact in contacts)
            {
                // A coin can have both a direct contact and a magnet contact, but scores only once.
                if (!contact.Cube.gameObject.activeSelf) continue;
                // Takeoff applies at its swept pickup time, even on a long/low-FPS frame.
                if (contact.Time >= takeoffTime && !contact.Attracted) continue;
                if (contact.Time >= truckTime)
                {
                    if (contact.Cube.Color != playerColor)
                    {
                        LastTruckSmashes++;
                        if (debris != null) debris.Shatter(contact.Cube, scroll * (1 - contact.Time));
                    }
                    contact.Cube.gameObject.SetActive(false);
                    if (contact.Cube.Color == playerColor) collected++;
                    continue;
                }
                if (contact.Cube.Color != playerColor)
                {
                    if (landingShield) { contact.Cube.gameObject.SetActive(false); continue; }
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
                if (contact.Time > fatalTime || contact.Time > takeoffTime) break;
                if (contact.Pickup.Type == PowerUpType.FighterPlane && contact.Time >= truckTime) continue;
                contact.Pickup.gameObject.SetActive(false);
                collectedPowerUps.Add(contact.Pickup.Type);
            }

            bool recycled = false;
            while (true)
            {
                TrackSegment oldest = null;
                foreach (TrackSegment segment in segments)
                    if (segment.transform.position.z + RunnerRules.SegmentLength < playerZ - 18f &&
                        (oldest == null || segment.transform.position.z < oldest.transform.position.z)) oldest = segment;
                if (oldest == null) break;
                furthest += RunnerRules.SegmentLength;
                oldest.transform.position = new Vector3(transform.position.x, transform.position.y, furthest);
                Populate(oldest, 0, difficulty, travelled + furthest - playerZ);
                ConfigureEnvironment(oldest, nextSectionIndex++);
                SceneryRise rise = oldest.GetComponent<SceneryRise>();
                if (rise != null) rise.Begin(true, Shader.GetGlobalFloat("_CubeDashEnvironmentTime"));
                recycled = true;
            }
            if (recycled) UpdateHorizonEdges();
            float sceneryTime = Shader.GetGlobalFloat("_CubeDashEnvironmentTime");
            foreach (TrackSegment segment in segments)
            {
                SceneryRise rise = segment.GetComponent<SceneryRise>();
                if (rise != null) rise.Tick(sceneryTime);
            }
            return hitWrongColor;
        }

        private void PullCoins(float scroll, Vector2 playerFrom, Vector2 playerTo, float range,
            Vector3 target, float dt, float activationTime, bool laneOnly)
        {
            foreach (TrackSegment segment in segments)
                foreach (RunnerCube coin in segment.Cubes)
                {
                    if (!coin.gameObject.activeInHierarchy || !coin.CoinVisible || coin.Color != playerColor || !coin.Collider.enabled) continue;
                    Vector3 end = coin.CoinVisual.position, start = end + Vector3.forward * scroll;
                    if (!RunnerRules.SweptRange(playerFrom, playerTo, new Vector2(start.x, start.z),
                        new Vector2(end.x, end.z), range, out float enter, out float exit)) continue;
                    enter = Mathf.Max(enter, activationTime);
                    if (laneOnly)
                    {
                        // Use the authored slot's lane, not the coin's moving attraction visual.
                        Vector2 laneFrom = new Vector2(coin.transform.position.x, playerFrom.y);
                        Vector2 laneTo = new Vector2(coin.transform.position.x, playerTo.y);
                        if (!RunnerRules.SweptHit(playerFrom, playerTo, laneFrom, laneTo,
                            new Vector2(laneWidth * 0.5f, range), out float laneEnter, out float laneExit)) continue;
                        enter = Mathf.Max(enter, laneEnter);
                        exit = Mathf.Min(exit, laneExit);
                    }
                    if (enter >= exit) continue;
                    Vector3 pulled = Vector3.Lerp(start, end, enter);
                    float cursor = enter;
                    // Bounded substeps preserve pickup/fatal-contact ordering even across long frames.
                    int steps = Mathf.Clamp(Mathf.CeilToInt(dt * (exit - enter) / 0.025f), 1, 128);
                    for (int step = 1; step <= steps; step++)
                    {
                        float next = Mathf.Lerp(enter, exit, step / (float)steps);
                        Vector3 destination = new Vector3(Mathf.Lerp(playerFrom.x, playerTo.x, next), target.y, Mathf.Lerp(playerFrom.y, playerTo.y, next));
                        Vector3 position = Vector3.MoveTowards(pulled + Vector3.back * (scroll * (next - cursor)),
                            destination, 30 * dt * (next - cursor));
                        if (RunnerRules.SweptHit(Vector2.Lerp(playerFrom, playerTo, cursor), Vector2.Lerp(playerFrom, playerTo, next),
                            new Vector2(pulled.x, pulled.z), new Vector2(position.x, position.z), Vector2.one * 0.45f,
                            out float time, out float contactExit) &&
                            RunnerRules.SweptHit(new Vector2(target.y, 0), new Vector2(target.y, 0),
                                new Vector2(pulled.y, 0), new Vector2(position.y, 0), new Vector2(0.6f, 1),
                                out float heightEnter, out float heightExit) &&
                            Mathf.Max(time, heightEnter) <= Mathf.Min(contactExit, heightExit))
                        {
                            time = Mathf.Max(time, heightEnter);
                            contacts.Add(new Contact { Cube = coin, Time = Mathf.Lerp(cursor, next, time), Attracted = true });
                            pulled = Vector3.Lerp(pulled, position, time);
                            cursor = Mathf.Lerp(cursor, next, time);
                            break;
                        }
                        pulled = position; cursor = next;
                    }
                    coin.AttractCoinTo(pulled + Vector3.back * (scroll * (1 - cursor)));
                }
        }

        private void UpdateHorizonEdges()
        {
            if (environment == null || segments.Length == 0) return;
            TrackSegment rear = segments[0], front = segments[0];
            foreach (TrackSegment segment in segments)
            {
                if (segment.transform.localPosition.z < rear.transform.localPosition.z) rear = segment;
                if (segment.transform.localPosition.z > front.transform.localPosition.z) front = segment;
            }
            foreach (TrackSegment segment in segments)
                if (segment.Environment != null) segment.Environment.SetHorizonEdges(segment == rear, segment == front);
        }

        private void Populate(TrackSegment segment, int warmupRows, float difficulty = 0f, float sectionDistance = 0f)
        {
            for (int row = 0; row < 3; row++)
            {
                bool visible = row >= warmupRows;
                int blockedMask = 0;
                int collectibleMask = 0;
                int leadLane = -1;
                int incomingBlockedMask = previousBlockedMask;
                int incomingSafeLane = matchingLane;
                HighwayHazard hazard = HighwayHazard.Cube;
                if (visible)
                {
                    if (patternedEncounters)
                    {
                        HighwayPatterns.Row pattern = patterns.Next(sectionDistance + segment.CubeHomePosition(row * 3).z);
                        matchingLane = pattern.SafeLane;
                        blockedMask = firstVisibleRow ? 0 : pattern.BlockedMask;
                        hazard = pattern.Hazard;
                    }
                    else
                    {
                        if (!firstVisibleRow) matchingLane = RunnerRules.NextSafeLane(random, matchingLane, difficulty);
                        blockedMask = firstVisibleRow ? 0 : RunnerRules.BlockedLanes(random, matchingLane, difficulty, previousBlockedMask);
                    }
                    collectibleMask = RunnerRules.CollectibleLanes(random, blockedMask, matchingLane);
                    if (!patternedEncounters && blockedMask != 0 && random.NextDouble() < 0.6f) leadLane = RunnerRules.PickLane(random, blockedMask);
                    previousBlockedMask = blockedMask;
                    firstVisibleRow = false;
                }
                bool swapOthers = random.Next(2) == 0;
                for (int lane = 0; lane < 3; lane++)
                {
                    RunnerCube cube = segment.Cubes[row * 3 + lane];
                    int bit = 1 << lane;
                    CubeColor color = (collectibleMask & bit) != 0 ? playerColor
                        : RunnerRules.ColorForLane(lane, matchingLane, playerColor, swapOthers);
                    cube.Configure(color, ColorMaterial(color), (collectibleMask & bit) != 0);
                    if (presentation != null && (blockedMask & bit) != 0)
                        // Cycle each category separately so the shorter opening still shows
                        // every compact prop. Art never consumes gameplay/spawn randomness.
                        presentation.ConfigureHazard(cube, hazard, hazardVariants[(int)hazard]++);
                    Vector3 position = segment.CubeHomePosition(row * 3 + lane);
                    position.x = (lane - 1) * laneWidth;
                    // Lead obstacles arrive before rewards, leaving the previous row clear when steering away.
                    if (lane == leadLane) position.z -= RunnerRules.HazardLeadDistance;
                    cube.transform.localPosition = position;
                    cube.gameObject.SetActive(visible && ((blockedMask | collectibleMask) & bit) != 0);
                }
                // Bonuses sit halfway between rows. Do not lure players into a lane still
                // occupied by the previous vehicle, or require a two-lane dash to reach one.
                int pickupLanes = 7 & ~blockedMask & ~incomingBlockedMask;
                for (int lane = 0; lane < 3; lane++)
                    if (Mathf.Abs(lane - incomingSafeLane) > 1) pickupLanes &= ~(1 << lane);
                float pickupDistance = sectionDistance + segment.CubeHomePosition(row * 3).z - RunnerRules.PowerUpRowOffset;
                PlacePowerUp(segment, row, visible, pickupLanes, pickupDistance);
            }
        }

        private void OnDestroy()
        {
            RoadCurve.Clear();
            presentation?.Dispose();
        }

        private void OnDisable() { if (Application.isPlaying) RoadCurve.Clear(); }

        private void ConfigureEnvironment(TrackSegment segment, int sectionIndex)
        {
            if (environment != null && segment.Environment != null)
                segment.Environment.Configure(sectionIndex, environment.SegmentsPerBiome,
                    environment.HasFixedBiome ? environment.FixedBiome : (EnvironmentBiome?)null);
        }

        private void PlacePowerUp(TrackSegment segment, int row, bool visible, int openMask, float distance)
        {
            PowerUpPickup[] pickups = segment.PowerUps;
            if (row >= pickups.Length) return;
            PowerUpPickup pickup = pickups[row];
            if (pickup == null) return;
            Vector3 position = segment.PowerUpHomePosition(row);
            // Dedicated midpoint slots, seven metres before the coin row and four before its earliest hazard.
            position.z = segment.CubeHomePosition(row * 3).z - RunnerRules.PowerUpRowOffset;
            pickup.transform.localPosition = position;
            PowerUpType type = PowerUpType.Shield;
            bool spawn = visible && openMask != 0 && powerUpMaterials != null &&
                powerUpSpawns.TryChoose(distance, powerUpChance, powerUpMaterials.Length, out type);
            if (!spawn)
            {
                pickup.gameObject.SetActive(false);
                return;
            }
            // Any open lane may carry a bonus, giving alternatives to following a single reward route.
            pickup.Configure(type, PowerUpMaterial(type));
            position.x = (powerUpSpawns.PickLane(openMask) - 1) * laneWidth;
            pickup.transform.localPosition = position;
            pickup.gameObject.SetActive(true);
        }
    }
}

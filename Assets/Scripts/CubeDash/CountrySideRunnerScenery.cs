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
        [Header("Consistent highway presentation")]
        [SerializeField] private bool followRoadCurves = false;
        [SerializeField] private Transform[] curvePlants = new Transform[0];
        [SerializeField] private Renderer[] curvedSurfaces = new Renderer[0];
        private Vector3[] plantHomes;
        private HighwayPresentation presentation;
        private float lastDistance = -1;
        private Vector4 lastCurve;
        public bool FollowsRoadCurves => followRoadCurves;
        public Transform[] CurvePlants => curvePlants;
        public Renderer[] CurvedSurfaces => curvedSurfaces;

        public EndlessTrack Track => track;
        public Transform[] Tiles => tiles;
        public float TileLength => tileLength;

        private void Awake()
        {
            if (!followRoadCurves) return;
            CachePlantHomes();
            presentation = new HighwayPresentation();
            // Only authored rigid leaf renderers are sliced/wrapped. Nature-pack meshes,
            // shader-graph materials, wind vertex channels and LODGroups remain untouched.
            foreach (Renderer surface in curvedSurfaces)
                if (surface != null) presentation.Prepare(surface.transform);
        }
        private void CachePlantHomes()
        {
            if (plantHomes != null) return;
            plantHomes = new Vector3[curvePlants.Length];
            for (int i = 0; i < curvePlants.Length; i++)
                if (curvePlants[i] != null) plantHomes[i] = curvePlants[i].localPosition;
        }

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
            Vector4 curve = followRoadCurves ? Shader.GetGlobalVector("_CubeDashRoadCurve") : Vector4.zero;
            if (track.Travelled == lastDistance && curve == lastCurve) return;
            lastDistance = track.Travelled; lastCurve = curve;
            for (int i = 0; i < tiles.Length; i++)
            {
                if (tiles[i] == null) continue;
                Vector3 position = tiles[i].localPosition;
                position.z = TilePosition(track.Travelled, i, tiles.Length, tileLength, tileFront, recycleBehind);
                tiles[i].localPosition = position;
            }
            if (!followRoadCurves) return;
            CachePlantHomes();
            for (int i = 0; i < curvePlants.Length; i++)
            {
                Transform plant = curvePlants[i];
                if (plant == null) continue;
                Vector3 world = plant.parent.TransformPoint(plantHomes[i]);
                world.x += RoadCurve.Offset(world.z - curve.w, curve.x, curve.y, curve.z);
                plant.localPosition = plant.parent.InverseTransformPoint(world);
            }
        }

        private void LateUpdate() => SyncDistance();
        private void OnEnable() { lastDistance = -1; }
        private void OnDisable()
        {
            if (plantHomes == null) return;
            for (int i = 0; i < curvePlants.Length; i++)
                if (curvePlants[i] != null) curvePlants[i].localPosition = plantHomes[i];
            lastDistance = -1;
        }
        private void OnDestroy() => presentation?.Dispose();
    }
}

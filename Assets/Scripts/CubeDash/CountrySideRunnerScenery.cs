using System.Collections.Generic;
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
        [SerializeField] private bool batchRigidScenery = true;
        [Tooltip("Experimental. Leave off to preserve stable native foliage wind and LOD rendering.")]
        [SerializeField] private bool instanceNativePlants = false;
        [SerializeField] private Material[] plantInstancingTemplates = new Material[0];
        [Header("World-space landscape travel")]
        [SerializeField] private Transform[] landscapeTiles = new Transform[0];
        [SerializeField, Min(1)] private float landscapeLength = 568;
        [SerializeField] private float landscapeFront = 700;
        [SerializeField] private float landscapeRecycleBehind = -200;
        private Vector3[] plantHomes;
        private HighwayPresentation presentation;
        private CountrySideSceneryBatch batches;
        private CountrySidePlantInstancing plantInstancing;
        private PlantGroup[] plantGroups;
        private sealed class PlantGroup
        {
            public Transform Parent;
            public int[] Indices;
        }
        private float lastDistance = -1;
        private Vector4 lastCurve;
        public bool FollowsRoadCurves => followRoadCurves;
        public Transform[] CurvePlants => curvePlants;
        public Renderer[] CurvedSurfaces => curvedSurfaces;
        public Transform[] LandscapeTiles => landscapeTiles;
        public float LandscapeLength => landscapeLength;
        public int BatchedSourceCount => batches?.SourceCount ?? 0;
        public int SceneryBatchCount => batches?.BatchCount ?? 0;
        public int InstancedPlantRendererCount => plantInstancing?.RendererCount ?? 0;

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
            if (batchRigidScenery) batches = new CountrySideSceneryBatch(tiles, curvedSurfaces);
            // Forcing these Shader Graph materials out of the SRP Batcher can introduce
            // temporal foliage/shadow flicker. Keep their native renderer state by default.
            if (instanceNativePlants) plantInstancing = new CountrySidePlantInstancing(curvePlants, plantInstancingTemplates);
        }
        private void CachePlantHomes()
        {
            if (plantHomes != null) return;
            plantHomes = new Vector3[curvePlants.Length];
            var parents = new Dictionary<Transform, List<int>>();
            for (int i = 0; i < curvePlants.Length; i++)
                if (curvePlants[i] != null)
                {
                    plantHomes[i] = curvePlants[i].localPosition;
                    Transform parent = curvePlants[i].parent;
                    if (!parents.TryGetValue(parent, out var indices)) parents.Add(parent, indices = new List<int>());
                    indices.Add(i);
                }
            plantGroups = new PlantGroup[parents.Count]; int group = 0;
            foreach (var parent in parents)
                plantGroups[group++] = new PlantGroup { Parent = parent.Key, Indices = parent.Value.ToArray() };
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
            // Hills are real world geometry, not a camera-following backdrop. Their distance
            // supplies parallax naturally; recycle only after the whole silhouette is behind us.
            for (int i = 0; i < landscapeTiles.Length; i++)
            {
                if (landscapeTiles[i] == null) continue;
                Vector3 position = landscapeTiles[i].localPosition;
                position.z = TilePosition(track.Travelled, i, landscapeTiles.Length, landscapeLength, landscapeFront, landscapeRecycleBehind);
                landscapeTiles[i].localPosition = position;
            }
            if (!followRoadCurves) return;
            CachePlantHomes();
            var sampler = new RoadCurve.Sampler(curve.x, curve.y, curve.z);
            foreach (PlantGroup group in plantGroups)
            {
                if (group.Parent == null) continue;
                Matrix4x4 world = group.Parent.localToWorldMatrix;
                // A world-X displacement becomes this vector in the shared parent's local space.
                Vector3 localRight = group.Parent.worldToLocalMatrix.MultiplyVector(Vector3.right);
                foreach (int i in group.Indices)
                {
                    Transform plant = curvePlants[i];
                    if (plant == null) continue;
                    Vector3 home = plantHomes[i];
                    float worldZ = world.m20 * home.x + world.m21 * home.y + world.m22 * home.z + world.m23;
                    plant.localPosition = home + localRight * sampler.Offset(worldZ - curve.w);
                }
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
        private void OnDestroy() { plantInstancing?.Dispose(); batches?.Dispose(); presentation?.Dispose(); }
    }
}

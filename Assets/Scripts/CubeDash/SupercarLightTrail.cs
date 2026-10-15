using UnityEngine;
using UnityEngine.Rendering;

namespace CubeDash
{
    /// <summary>Two distance-sampled, world-space light ribbons attached to the actual rear LEDs.</summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class SupercarLightTrail : MonoBehaviour
    {
        [SerializeField] private Transform car;
        [SerializeField] private Vector3 leftLamp = new Vector3(-0.68f, 0.553f, -2.335f);
        [SerializeField] private Vector3 rightLamp = new Vector3(0.68f, 0.553f, -2.335f);
        [SerializeField, Min(0.1f)] private float minimumLength = 2.8f;
        [SerializeField, Min(0.1f)] private float maximumLength = 4.6f;
        [SerializeField, Min(0.01f)] private float width = 0.46f;
        private const int HistorySize = 128, Segments = 64;
        private const float Spacing = 0.08f;
        private readonly Vector3[][] history = { new Vector3[HistorySize], new Vector3[HistorySize] };
        private readonly Vector3[] previous = new Vector3[2], current = new Vector3[2];
        private readonly Vector3[] vertices = new Vector3[(Segments + 1) * 4];
        private Mesh mesh;
        private MeshRenderer trailRenderer;
        private int newest;
        private float distance, sampleDistance;
        private bool seeded;
        public Mesh TrailMesh => mesh;
        public Renderer TrailRenderer => trailRenderer;
        public Vector3 LampPosition(int side) => car.TransformPoint(side == 0 ? leftLamp : rightLamp);

        private void Awake() => Initialize();
        private void Initialize()
        {
            if (mesh != null) return;
            mesh = new Mesh { name = "Supercar twin red light ribbons" }; mesh.MarkDynamic();
            var uv = new Vector2[vertices.Length];
            var triangles = new int[Segments * 12];
            for (int side = 0; side < 2; side++)
                for (int row = 0; row <= Segments; row++)
                {
                    int vertex = (side * (Segments + 1) + row) * 2;
                    uv[vertex] = new Vector2(0, row / (float)Segments); uv[vertex + 1] = new Vector2(1, row / (float)Segments);
                    if (row == Segments) continue;
                    int index = (side * Segments + row) * 6;
                    triangles[index] = vertex; triangles[index + 1] = vertex + 2; triangles[index + 2] = vertex + 1;
                    triangles[index + 3] = vertex + 1; triangles[index + 4] = vertex + 2; triangles[index + 5] = vertex + 3;
                }
            mesh.vertices = vertices; mesh.uv = uv; mesh.triangles = triangles;
            GetComponent<MeshFilter>().sharedMesh = mesh;
            trailRenderer = GetComponent<MeshRenderer>();
            trailRenderer.shadowCastingMode = ShadowCastingMode.Off; trailRenderer.receiveShadows = false;
            trailRenderer.lightProbeUsage = LightProbeUsage.Off; trailRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            trailRenderer.enabled = false;
        }

        public void ResetTrail()
        {
            seeded = false; newest = 0; distance = sampleDistance = 0;
            if (trailRenderer != null) trailRenderer.enabled = false;
        }

        // Driven by the game clock, not Time.time: pause really freezes both shape and brightness.
        public void Tick(float speed, Vector3 cameraPosition)
        {
            Initialize();
            if (!isActiveAndEnabled || car == null) return;
            for (int side = 0; side < 2; side++) current[side] = LampPosition(side);
            if (!seeded) { Seed(); return; }
            float travel = Vector3.Distance((previous[0] + previous[1]) * 0.5f, (current[0] + current[1]) * 0.5f);
            // A retry/teleport must never draw a beam across the whole map.
            if (travel > 8f) { ResetTrail(); Seed(); return; }
            float oldDistance = distance; distance += travel;
            while (sampleDistance + Spacing <= distance)
            {
                sampleDistance += Spacing; newest = (newest + 1) % HistorySize;
                float t = Mathf.InverseLerp(oldDistance, distance, sampleDistance);
                for (int side = 0; side < 2; side++) history[side][newest] = Vector3.Lerp(previous[side], current[side], t);
            }
            float length = Mathf.Min(distance, Mathf.Lerp(minimumLength, maximumLength, Mathf.InverseLerp(10, 35, speed)));
            for (int side = 0; side < 2; side++)
                for (int row = 0; row <= Segments; row++)
                {
                    float along = row / (float)Segments;
                    Vector3 point = Sample(side, distance - along * length);
                    Vector3 tangent = point - Sample(side, distance - along * length - Spacing);
                    if (tangent.sqrMagnitude < 0.000001f) tangent = car.forward;
                    // Flat, horizontal ribbons at lamp height, not camera-facing threads.
                    tangent.y = 0;
                    Vector3 across = Vector3.Cross(Vector3.up, tangent.normalized).normalized;
                    if (across.sqrMagnitude < 0.1f) across = Vector3.right;
                    Vector3 halfWidth = across * (width * 0.5f * Mathf.Lerp(1, 0.72f, along));
                    int index = (side * (Segments + 1) + row) * 2;
                    vertices[index] = transform.InverseTransformPoint(point - halfWidth);
                    vertices[index + 1] = transform.InverseTransformPoint(point + halfWidth);
                }
            mesh.vertices = vertices; mesh.RecalculateBounds(); trailRenderer.enabled = length > 0.02f;
            for (int side = 0; side < 2; side++) previous[side] = current[side];
        }

        private void Seed()
        {
            for (int side = 0; side < 2; side++)
            {
                previous[side] = current[side];
                for (int i = 0; i < HistorySize; i++) history[side][i] = current[side];
            }
            seeded = true;
        }

        private Vector3 Sample(int side, float atDistance)
        {
            if (atDistance >= sampleDistance)
                return Vector3.Lerp(history[side][newest], current[side], Mathf.InverseLerp(sampleDistance, distance, atDistance));
            float age = Mathf.Clamp((sampleDistance - atDistance) / Spacing, 0, HistorySize - 2);
            int index = Mathf.FloorToInt(age);
            return Vector3.Lerp(history[side][(newest - index + HistorySize) % HistorySize],
                history[side][(newest - index - 1 + HistorySize) % HistorySize], age - index);
        }

        public void ShiftWorldOrigin(float offset)
        {
            Vector3 shift = Vector3.back * offset;
            for (int side = 0; side < 2; side++)
            {
                previous[side] += shift; current[side] += shift;
                for (int i = 0; i < HistorySize; i++) history[side][i] += shift;
            }
        }
        private void OnDisable() => ResetTrail();
        private void OnDestroy() { if (mesh != null) Destroy(mesh); }
    }
}

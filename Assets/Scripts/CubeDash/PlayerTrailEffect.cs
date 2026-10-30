using UnityEngine;
using UnityEngine.Rendering;

namespace CubeDash
{
    /// <summary>Raised braided streamers, airborne sparks and rotating faceted particles in one reusable mesh.</summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class PlayerTrailEffect : MonoBehaviour
    {
        private const int HistorySize = 160, Segments = 64, Streams = 4, Layers = 2, Sparks = 48, Crystals = 16;
        private const int RibbonVertices = Streams * Layers * (Segments + 1) * 2;
        private const int SparkStart = RibbonVertices, CrystalStart = SparkStart + Sparks * 4;
        private const float Spacing = 0.1f;
        private readonly Vector3[] history = new Vector3[HistorySize];
        private readonly Vector3[] vertices = new Vector3[CrystalStart + Crystals * 24];
        private readonly Vector3[] normals = new Vector3[CrystalStart + Crystals * 24];
        private readonly Color[] colors = new Color[CrystalStart + Crystals * 24];
        private readonly Vector3[] crystalPoints = new Vector3[6];
        private static readonly int[] CrystalFaces = { 0,3,2, 0,4,3, 0,5,4, 0,2,5, 1,2,3, 1,3,4, 1,4,5, 1,5,2 };
        private CubeDashGame game;
        private Transform player;
        private Mesh mesh;
        private MeshRenderer trailRenderer;
        private MaterialPropertyBlock appearance;
        private Vector3 previous, current;
        private float distance, sampled, age;
        private int newest;
        private bool seeded;
        private bool suppressed;
        public PlayerTrail Style { get; private set; }
        public Mesh TrailMesh => mesh;
        public Renderer TrailRenderer => trailRenderer;
        public float Age => age;
        public const int SparkCount = Sparks;
        public const int CrystalCount = Crystals;

        public void SetSuppressed(bool value)
        {
            if (suppressed == value) return;
            suppressed = value;
            // Clear on both transitions so resuming never connects to pre-power-up history.
            ResetTrail();
        }

        private void Awake() => Prepare();

        private void Prepare()
        {
            if (mesh != null) return;
            mesh = new Mesh { name = "Volumetric trail - braided ribbons, sparks and faceted crystals" }; mesh.MarkDynamic();
            var uv = new Vector2[vertices.Length]; var data = new Vector2[vertices.Length];
            var triangles = new int[Streams * Layers * Segments * 6 + Sparks * 6 + Crystals * 24];
            int index = 0;
            for (int ribbon = 0; ribbon < Streams * Layers; ribbon++)
                for (int i = 0; i <= Segments; i++)
                {
                    int v = (ribbon * (Segments + 1) + i) * 2;
                    uv[v] = new Vector2(0, i / (float)Segments); uv[v + 1] = new Vector2(1, i / (float)Segments);
                    data[v] = data[v + 1] = new Vector2(ribbon % Layers == 0 ? 0 : 0.5f, 0);
                    if (i == Segments) continue;
                    triangles[index++] = v; triangles[index++] = v + 2; triangles[index++] = v + 1;
                    triangles[index++] = v + 1; triangles[index++] = v + 2; triangles[index++] = v + 3;
                }
            for (int i = 0; i < Sparks; i++)
            {
                int v = SparkStart + i * 4;
                uv[v] = Vector2.zero; uv[v + 1] = Vector2.right; uv[v + 2] = Vector2.one; uv[v + 3] = Vector2.up;
                for (int j = 0; j < 4; j++) data[v + j] = new Vector2(1, 0);
                triangles[index++] = v; triangles[index++] = v + 1; triangles[index++] = v + 2;
                triangles[index++] = v; triangles[index++] = v + 2; triangles[index++] = v + 3;
            }
            for (int i = 0; i < Crystals * 24; i++) { triangles[index++] = CrystalStart + i; data[CrystalStart + i] = new Vector2(2, 0); }
            for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.up;
            mesh.vertices = vertices; mesh.normals = normals; mesh.colors = colors; mesh.uv = uv; mesh.uv2 = data; mesh.triangles = triangles;
            GetComponent<MeshFilter>().sharedMesh = mesh;
            trailRenderer = GetComponent<MeshRenderer>(); trailRenderer.enabled = false;
            trailRenderer.shadowCastingMode = ShadowCastingMode.Off; trailRenderer.receiveShadows = false;
            trailRenderer.lightProbeUsage = LightProbeUsage.Off; trailRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            appearance = new MaterialPropertyBlock();
        }

        public void Initialize(CubeDashGame owner, Transform target) { Prepare(); game = owner; player = target; }

        public void Select(PlayerTrail style)
        {
            Prepare(); if (!TrailStyles.Valid(style)) style = PlayerTrail.None;
            if (Style == style) return;
            Style = style; ResetTrail();
            appearance.SetFloat("_Style", (int)style);
            appearance.SetColor("_TintA", TrailStyles.Primary(style));
            appearance.SetColor("_TintB", TrailStyles.Secondary(style));
            appearance.SetFloat("_Age", 0); trailRenderer.SetPropertyBlock(appearance);
        }

        public void ResetTrail()
        {
            seeded = false; newest = 0; distance = sampled = age = 0;
            if (trailRenderer != null) trailRenderer.enabled = false;
        }

        public void Tick(float dt)
        {
            Prepare();
            SetSuppressed(game != null && (game.Flying || game.Trucking));
            if (suppressed || Style == PlayerTrail.None || game == null || player == null || dt <= 0 || !isActiveAndEnabled || game.State != CubeDashGame.RunState.Running) return;
            float behind = game.VehicleDriving ? 1.55f : 0.65f;
            current = new Vector3(player.position.x, 0.065f, player.position.z - behind);
            age += dt;
            appearance.SetFloat("_Age", age); trailRenderer.SetPropertyBlock(appearance);
            if (!seeded) { Seed(); return; }
            float travel = Vector3.Distance(previous, current);
            if (travel > 12) { ResetTrail(); Seed(); return; }
            float old = distance; distance += travel;
            while (sampled + Spacing <= distance)
            {
                sampled += Spacing; newest = (newest + 1) % HistorySize;
                history[newest] = Vector3.Lerp(previous, current, Mathf.InverseLerp(old, distance, sampled));
            }
            float length = Mathf.Min(distance, Mathf.Lerp(4.8f, 7.2f, Mathf.InverseLerp(10, 35, game.Speed)));
            BuildVolume(length);
            mesh.vertices = vertices; mesh.normals = normals; mesh.colors = colors; mesh.RecalculateBounds();
            trailRenderer.enabled = length > 0.05f; previous = current;
        }

        private void BuildVolume(float length)
        {
            float growth = Mathf.SmoothStep(0, 1, Mathf.Clamp01(length / 1.5f));
            int count = Style == PlayerTrail.RainbowRush ? 4 : Style == PlayerTrail.FrostWave ? 2 : 3;
            Vector3 cameraPosition = game.GameCamera.transform.position;
            for (int strand = 0; strand < Streams; strand++)
                for (int layer = 0; layer < Layers; layer++)
                    for (int row = 0; row <= Segments; row++)
                    {
                        float along = row / (float)Segments;
                        Vector3 point = Sample(distance - along * length);
                        Vector3 tangent = Tangent(distance - along * length);
                        Vector3 side = Vector3.Cross(Vector3.up, tangent).normalized;
                        float phase = along * 12 - age * 2.5f + strand * Mathf.PI * 2 / count;
                        float spread = game.VehicleDriving ? 1.2f : 1;
                        Vector3 lift;
                        if (Style == PlayerTrail.SolarFlare)
                            lift = side * ((strand - 1) * (0.08f + along * 0.23f) + Mathf.Sin(phase * 2.1f) * 0.10f)
                                + Vector3.up * (0.20f + along * 0.22f + Mathf.Sin(phase * 1.7f) * 0.08f);
                        else if (Style == PlayerTrail.FrostWave)
                            lift = side * ((strand == 0 ? -1 : 1) * (0.17f + along * 0.12f))
                                + Vector3.up * (0.18f + along * 0.17f + Mathf.Sin(phase) * 0.07f);
                        else if (Style == PlayerTrail.RainbowRush)
                            lift = side * ((strand - 1.5f) * 0.15f + Mathf.Sin(phase) * 0.10f)
                                + Vector3.up * (0.30f + along * 0.25f + Mathf.Cos(phase) * 0.14f);
                        else
                            lift = side * (Mathf.Sin(phase) * (0.12f + along * 0.25f))
                                + Vector3.up * (0.30f + along * 0.20f + Mathf.Cos(phase) * (0.10f + along * 0.12f));
                        point += lift * growth * spread;
                        Vector3 facing = (cameraPosition - point).normalized;
                        Vector3 across = Vector3.Cross(tangent, facing).normalized;
                        if (across.sqrMagnitude < 0.1f) across = Vector3.up;
                        // The ribbon twists in actual XYZ space, instead of drawing a pattern on the road.
                        across = Quaternion.AngleAxis(Mathf.Sin(phase) * 24, tangent) * across;
                        float width = (layer == 0 ? 0.26f : Style == PlayerTrail.SolarFlare ? 0.105f : 0.065f)
                            * Mathf.Lerp(1, 0.25f, along) * growth;
                        int v = ((strand * Layers + layer) * (Segments + 1) + row) * 2;
                        vertices[v] = transform.InverseTransformPoint(point - across * width);
                        vertices[v + 1] = transform.InverseTransformPoint(point + across * width);
                        Color tint = Tint(along, strand);
                        tint.a = strand < count ? (layer == 0 ? 0.16f : 0.86f) * growth : 0;
                        colors[v] = colors[v + 1] = tint;
                    }
            for (int i = 0; i < Sparks; i++)
            {
                float seed = Hash(i + 1), life = Mathf.Repeat(age * (0.48f + seed * 0.22f) + seed * 13, 1);
                float along = 0.04f + life * 0.92f;
                Vector3 point = Sample(distance - along * length);
                Vector3 side = Vector3.Cross(Vector3.up, Tangent(distance - along * length));
                float rise = Style == PlayerTrail.SolarFlare ? life * 1.35f : 0.15f + life * 0.85f;
                point += (side * (Mathf.Sin(i * 4.17f + age * 0.7f) * (0.2f + life * 0.45f)) + Vector3.up * rise) * growth;
                Vector3 facing = (cameraPosition - point).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, facing).normalized;
                Vector3 up = Vector3.Cross(facing, right);
                float size = (0.035f + seed * 0.065f) * growth * Mathf.Sin(life * Mathf.PI);
                float rotation = age * 1.3f + i * 2.4f;
                Vector3 x = (right * Mathf.Cos(rotation) + up * Mathf.Sin(rotation)) * size;
                Vector3 y = (-right * Mathf.Sin(rotation) + up * Mathf.Cos(rotation)) * size * (Style == PlayerTrail.SolarFlare ? 1.7f : 1);
                int v = SparkStart + i * 4;
                vertices[v] = transform.InverseTransformPoint(point - x - y); vertices[v + 1] = transform.InverseTransformPoint(point + x - y);
                vertices[v + 2] = transform.InverseTransformPoint(point + x + y); vertices[v + 3] = transform.InverseTransformPoint(point - x + y);
                Color tint = Tint(life, i % count); tint = Color.Lerp(tint, Color.white, Style == PlayerTrail.SolarFlare ? 0.15f : 0.4f);
                tint.a = Mathf.Sin(life * Mathf.PI) * growth * (0.6f + 0.4f * Mathf.Sin(age * 4 + i));
                for (int j = 0; j < 4; j++) colors[v + j] = tint;
            }
            for (int i = 0; i < Crystals; i++) BuildCrystal(i, length, growth);
        }

        private void BuildCrystal(int index, float length, float growth)
        {
            bool visible = Style == PlayerTrail.FrostWave || Style == PlayerTrail.RainbowRush;
            float seed = Hash(index + 97), life = Mathf.Repeat(age * 0.28f + seed * 7, 1);
            float along = 0.10f + life * 0.80f;
            Vector3 point = Sample(distance - along * length);
            Vector3 side = Vector3.Cross(Vector3.up, Tangent(distance - along * length));
            point += (side * Mathf.Sin(index * 2.7f + age) * 0.55f + Vector3.up * (0.25f + life * 0.75f)) * growth;
            float size = (0.055f + seed * 0.075f) * growth * Mathf.Sin(life * Mathf.PI);
            Quaternion spin = Quaternion.Euler(age * 55 + index * 37, age * 80 + index * 21, age * 35 + index * 51);
            crystalPoints[0] = point + spin * Vector3.up * size * 1.65f;
            crystalPoints[1] = point + spin * Vector3.down * size * 1.65f;
            crystalPoints[2] = point + spin * Vector3.right * size * 0.7f;
            crystalPoints[3] = point + spin * Vector3.forward * size * 0.7f;
            crystalPoints[4] = point + spin * Vector3.left * size * 0.7f;
            crystalPoints[5] = point + spin * Vector3.back * size * 0.7f;
            Color tint = Style == PlayerTrail.FrostWave ? Color.Lerp(TrailStyles.Primary(Style), Color.white, 0.45f + seed * 0.35f) : Tint(life, index % 4);
            tint.a = visible ? Mathf.Sin(life * Mathf.PI) * growth * 0.85f : 0;
            for (int face = 0; face < 8; face++)
            {
                int v = CrystalStart + index * 24 + face * 3;
                for (int j = 0; j < 3; j++) { vertices[v + j] = transform.InverseTransformPoint(crystalPoints[CrystalFaces[face * 3 + j]]); colors[v + j] = tint; }
                Vector3 normal = Vector3.Cross(vertices[v + 1] - vertices[v], vertices[v + 2] - vertices[v]).normalized;
                normals[v] = normals[v + 1] = normals[v + 2] = normal;
            }
        }

        private Color Tint(float along, int strand) => Style == PlayerTrail.RainbowRush
            ? Color.HSVToRGB(Mathf.Repeat(along * 0.7f + strand * 0.18f - age * 0.12f, 1), 0.75f, 1)
            : Color.Lerp(TrailStyles.Primary(Style), TrailStyles.Secondary(Style), Mathf.Clamp01(strand * 0.45f + Mathf.Sin(along * 8 - age) * 0.15f));

        private Vector3 Tangent(float at)
        { Vector3 t = Sample(at) - Sample(at - Spacing); t.y = 0; return t.sqrMagnitude > 0.000001f ? t.normalized : Vector3.forward; }
        private static float Hash(int i) => Mathf.Repeat(Mathf.Sin(i * 127.1f) * 43758.5453f, 1);

        private void Seed()
        {
            previous = current;
            for (int i = 0; i < history.Length; i++) history[i] = current;
            seeded = true;
        }

        private Vector3 Sample(float at)
        {
            if (at >= sampled) return Vector3.Lerp(history[newest], current, Mathf.InverseLerp(sampled, distance, at));
            float rows = Mathf.Clamp((sampled - at) / Spacing, 0, HistorySize - 2);
            int index = Mathf.FloorToInt(rows);
            return Vector3.Lerp(history[(newest - index + HistorySize) % HistorySize],
                history[(newest - index - 1 + HistorySize) % HistorySize], rows - index);
        }

        public void ShiftWorldOrigin(float offset)
        {
            Vector3 shift = Vector3.back * offset; previous += shift; current += shift;
            for (int i = 0; i < history.Length; i++) history[i] += shift;
        }

        private void OnDisable() => ResetTrail();
        private void OnDestroy() { if (mesh != null) Destroy(mesh); }
    }
}

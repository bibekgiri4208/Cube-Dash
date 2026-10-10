using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CubeDash.Editor
{
    /// <summary>Editor-only organic meshes. The resulting layouts are baked into shared assets.</summary>
    internal static class EnvironmentModelMeshes
    {
        public static Mesh Foliage(int seed, bool faceted = false)
        {
            int sides = faceted ? 14 : 16, rings = faceted ? 8 : 10;
            var vertices = new List<Vector3> { Vector3.up * 0.5f };
            var triangles = new List<int>();
            float phase = seed * 0.37f;
            for (int ring = 1; ring < rings; ring++)
            {
                float latitude = ring * Mathf.PI / rings;
                for (int side = 0; side < sides; side++)
                {
                    float angle = side * Mathf.PI * 2 / sides;
                    float shape = 1 + 0.10f * Mathf.Sin(angle * 3 + phase + latitude)
                        + 0.06f * Mathf.Cos(angle * 5 - latitude * 3 + phase);
                    float radius = Mathf.Sin(latitude) * 0.5f * shape;
                    vertices.Add(new Vector3(Mathf.Cos(angle) * radius, Mathf.Cos(latitude) * 0.5f,
                        Mathf.Sin(angle) * radius));
                }
            }
            int bottom = vertices.Count;
            vertices.Add(Vector3.down * 0.5f);
            for (int side = 0; side < sides; side++)
            {
                int next = (side + 1) % sides;
                Triangle(triangles, 0, 1 + next, 1 + side);
                for (int ring = 0; ring < rings - 2; ring++)
                {
                    int a = 1 + ring * sides + side, b = 1 + ring * sides + next;
                    Triangle(triangles, a, b, a + sides);
                    Triangle(triangles, b, b + sides, a + sides);
                }
                int last = 1 + (rings - 2) * sides;
                Triangle(triangles, bottom, last + side, last + next);
            }
            return Finish(faceted ? "Weathered boulder" : "Lobed foliage", vertices, new[] { triangles }, !faceted);
        }

        public static Mesh Tube(Vector3[] centers, float[] radii, int sides = 10, bool levelRings = false)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int ring = 0; ring < centers.Length; ring++)
            {
                Vector3 direction = ring == centers.Length - 1 ? centers[ring] - centers[ring - 1]
                    : centers[ring + 1] - centers[ring];
                // Tall stems are scaled non-uniformly when baked. Horizontal rings prevent
                // that scaling from stretching tilted cross-sections into spikes at the crown.
                Quaternion orientation = levelRings ? Quaternion.identity : Quaternion.FromToRotation(Vector3.up, direction.normalized);
                for (int side = 0; side < sides; side++)
                {
                    float angle = side * Mathf.PI * 2 / sides;
                    vertices.Add(centers[ring] + orientation * new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radii[ring]);
                }
            }
            JoinRings(triangles, centers.Length, sides);
            Cap(vertices, triangles, centers[0], 0, sides, false);
            Cap(vertices, triangles, centers[centers.Length - 1], (centers.Length - 1) * sides, sides, true);
            return Finish("Tapered curved stem", vertices, new[] { triangles }, true);
        }

        public static Mesh PineTier(int seed)
        {
            const int sides = 16;
            float[] heights = { 0.02f, 0.16f, 0.43f, 0.72f };
            float[] radii = { 0.39f, 0.5f, 0.31f, 0.16f };
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int ring = 0; ring < heights.Length; ring++)
            {
                for (int side = 0; side < sides; side++)
                {
                    float angle = side * Mathf.PI * 2 / sides;
                    float lobe = side % 2 == 0 ? 1.10f : 0.84f;
                    float radius = radii[ring] * lobe * (1 + 0.06f * Mathf.Sin(angle * 3 + seed));
                    float droop = ring < 2 && side % 2 == 0 ? -0.07f : 0;
                    vertices.Add(new Vector3(Mathf.Cos(angle) * radius, heights[ring] + droop, Mathf.Sin(angle) * radius));
                }
            }
            JoinRings(triangles, heights.Length, sides);
            Cap(vertices, triangles, Vector3.zero, 0, sides, false);
            Cap(vertices, triangles, new Vector3(0.025f, 1, -0.02f), (heights.Length - 1) * sides, sides, true);
            return Finish("Scalloped evergreen branches", vertices, new[] { triangles }, false);
        }

        public static Mesh PalmFrond()
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int i = 0; i < 10; i++)
            {
                float t = i / 10f, next = (i + 1) / 10f;
                Vector3 a = FrondSpine(t), b = FrondSpine(next);
                float width = Mathf.Lerp(0.013f, 0.003f, t);
                DoubleQuad(vertices, triangles, a + Vector3.forward * width, b + Vector3.forward * width,
                    b - Vector3.forward * width, a - Vector3.forward * width);
                if (i == 0) continue;
                for (int side = -1; side <= 1; side += 2)
                {
                    float reach = 0.23f * Mathf.Pow(Mathf.Sin(t * Mathf.PI), 0.65f);
                    Vector3 tip = a + new Vector3(0.10f, -0.055f, side * reach);
                    Vector3 middle = Vector3.Lerp(a, tip, 0.48f) + Vector3.up * 0.012f;
                    Vector3 edge = Vector3.right * 0.038f;
                    DoubleQuad(vertices, triangles, a, middle + edge, tip, middle - edge);
                }
            }
            return Finish("Arched pinnate palm frond", vertices, new[] { triangles }, false);
        }

        private static Vector3 FrondSpine(float t)
            => new Vector3(t, Mathf.Sin(t * Mathf.PI) * 0.17f - t * t * 0.32f, Mathf.Sin(t * Mathf.PI * 2) * 0.025f);

        public static Mesh Hill(int seed, bool dune)
        {
            const int sides = 32, rings = 12;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            vertices.Add(new Vector3(0, HillHeight(0, 0, seed, dune), 0));
            for (int ring = 1; ring <= rings; ring++)
            {
                float radius = ring / (float)rings;
                for (int side = 0; side < sides; side++)
                {
                    float angle = side * Mathf.PI * 2 / sides;
                    float x = Mathf.Cos(angle) * radius, z = Mathf.Sin(angle) * radius;
                    vertices.Add(new Vector3(x * 0.5f, HillHeight(x, z, seed, dune), z * 0.5f));
                }
            }
            for (int side = 0; side < sides; side++)
            {
                int next = (side + 1) % sides;
                Triangle(triangles, 0, 1 + next, 1 + side);
                for (int ring = 0; ring < rings - 1; ring++)
                {
                    int a = 1 + ring * sides + side, b = 1 + ring * sides + next;
                    Triangle(triangles, a, b, a + sides);
                    Triangle(triangles, b, b + sides, a + sides);
                }
            }
            return Finish(dune ? "Wind-shaped sand dune" : "Rolling wooded hill", vertices, new[] { triangles }, true);
        }

        public static float HillHeight(float x, float z, int seed, bool dune)
        {
            float edge = Mathf.Max(0, 1 - x * x - z * z);
            float phase = seed * 0.13f;
            float profile = dune ? Mathf.Exp(-x * 1.1f) * (0.88f + 0.12f * Mathf.Cos(z * 4 + phase))
                : 0.83f + 0.11f * Mathf.Sin(x * 4 + z * 3 + phase) + 0.06f * Mathf.Cos(z * 6 - x * 2);
            return Mathf.Pow(edge, 1.65f) * profile;
        }

        /// <summary>Two material regions: exposed crags and a broken, slope-aware snowline.</summary>
        public static Mesh Mountain(int seed)
        {
            const int sides = 24;
            float[] heights = { 0, 0.07f, 0.15f, 0.25f, 0.36f, 0.48f, 0.61f, 0.72f, 0.82f, 0.92f };
            float[] radii = { 0.5f, 0.49f, 0.44f, 0.36f, 0.32f, 0.27f, 0.20f, 0.15f, 0.10f, 0.043f };
            var vertices = new List<Vector3>();
            var stone = new List<int>();
            var snow = new List<int>();
            float phase = seed * 0.43f;
            for (int ring = 0; ring < heights.Length; ring++)
            {
                for (int side = 0; side < sides; side++)
                {
                    float angle = side * Mathf.PI * 2 / sides;
                    float ridge = 1 + 0.20f * Mathf.Sin(angle * 3 + phase) + 0.10f * Mathf.Cos(angle * 7 - phase);
                    float radius = radii[ring] * ridge;
                    float y = heights[ring] + (ring == 0 ? 0 : 0.036f * Mathf.Sin(angle * 4 + ring * 1.3f + phase));
                    vertices.Add(new Vector3(Mathf.Cos(angle) * radius + heights[ring] * 0.10f, y,
                        Mathf.Sin(angle) * radius - heights[ring] * 0.075f));
                }
            }
            for (int ring = 0; ring < heights.Length - 1; ring++)
            {
                for (int side = 0; side < sides; side++)
                {
                    int a = ring * sides + side, b = ring * sides + (side + 1) % sides;
                    MountainTriangle(vertices, stone, snow, a, a + sides, b, phase);
                    MountainTriangle(vertices, stone, snow, b, a + sides, b + sides, phase);
                }
            }
            int tip = vertices.Count;
            vertices.Add(new Vector3(0.10f, 1, -0.075f));
            for (int side = 0; side < sides; side++)
                MountainTriangle(vertices, stone, snow, (heights.Length - 1) * sides + side, tip,
                    (heights.Length - 1) * sides + (side + 1) % sides, phase);
            Cap(vertices, stone, Vector3.zero, 0, sides, false);
            return Finish("Eroded alpine ridge", vertices, new[] { stone, snow }, false);
        }

        private static void MountainTriangle(List<Vector3> vertices, List<int> stone, List<int> snow,
            int a, int b, int c, float phase)
        {
            Vector3 center = (vertices[a] + vertices[b] + vertices[c]) / 3;
            float angle = Mathf.Atan2(center.z, center.x);
            float line = 0.66f + 0.065f * Mathf.Sin(angle * 3 + phase) + 0.055f * Mathf.Cos(angle * 5 - phase);
            Vector3 normal = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]).normalized;
            Triangle(center.y > line && normal.y > 0.24f ? snow : stone, a, b, c);
        }

        public static Mesh Mesa(int seed)
        {
            const int sides = 18;
            float[] heights = { 0, 0.12f, 0.33f, 0.35f, 0.57f, 0.60f, 0.84f, 0.89f, 0.96f, 1 };
            float[] radii = { 0.55f, 0.49f, 0.45f, 0.445f, 0.41f, 0.405f, 0.40f, 0.34f, 0.32f, 0.30f };
            var vertices = new List<Vector3>();
            var cliff = new List<int>();
            var strata = new List<int>();
            for (int ring = 0; ring < heights.Length; ring++)
            {
                for (int side = 0; side < sides; side++)
                {
                    float angle = side * Mathf.PI * 2 / sides;
                    float radius = radii[ring] * (1 + 0.13f * Mathf.Sin(angle * 3 + seed));
                    vertices.Add(new Vector3(Mathf.Cos(angle) * radius, heights[ring], Mathf.Sin(angle) * radius));
                }
            }
            for (int ring = 0; ring < heights.Length - 1; ring++)
            {
                List<int> material = ring == 2 || ring == 4 || ring >= 8 ? strata : cliff;
                for (int side = 0; side < sides; side++)
                {
                    int a = ring * sides + side, b = ring * sides + (side + 1) % sides;
                    Triangle(material, a, a + sides, b);
                    Triangle(material, b, a + sides, b + sides);
                }
            }
            Cap(vertices, strata, Vector3.up, (heights.Length - 1) * sides, sides, true);
            Cap(vertices, cliff, Vector3.zero, 0, sides, false);
            return Finish("Layered sandstone bluff", vertices, new[] { cliff, strata }, false);
        }

        public static Mesh WaterSurface(float[] xPositions, int rows = 21)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            int columns = xPositions.Length;
            for (int row = 0; row <= rows; row++)
                for (int column = 0; column < columns; column++)
                    vertices.Add(new Vector3(xPositions[column], 0, row * RunnerRules.SegmentLength / rows));
            for (int row = 0; row < rows; row++)
                for (int column = 0; column < columns - 1; column++)
                {
                    int a = row * columns + column;
                    Triangle(triangles, a, a + columns, a + 1);
                    Triangle(triangles, a + 1, a + columns, a + columns + 1);
                }
            Mesh mesh = Finish("Continuous wave surface", vertices, new[] { triangles }, true);
            Bounds bounds = mesh.bounds;
            // Cover the full shader wave-height range, including its three combined swells.
            bounds.Expand(new Vector3(0, 1.8f, 0));
            mesh.bounds = bounds;
            return mesh;
        }

        public static Mesh OceanSurface(bool apron = false)
        {
            var x = new List<float>();
            for (float p = 5.2f; p < 64; p += 0.75f) x.Add(p);
            for (float p = 64; p < 160; p += 4) x.Add(p);
            for (float p = 160; p < 320; p += 16) x.Add(p);
            for (float p = 320; p < 1024; p += 128) x.Add(p);
            x.Add(1024);
            var z = new List<float>();
            float stretch = apron ? 1024 / RunnerRules.SegmentLength : 1;
            if (apron)
            {
                // Both ends have dense geometry: the same mesh works behind and ahead of the pool.
                for (float p = 0; p < 96; p += 1.5f) z.Add(p / stretch);
                for (float p = 96; p < 928; p += 24) z.Add(p / stretch);
                for (float p = 928; p < 1024; p += 1.5f) z.Add(p / stretch);
                z.Add(RunnerRules.SegmentLength);
            }
            else for (int row = 0; row <= 84; row++) z.Add(row * 0.5f);
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            foreach (float depth in z) foreach (float across in x) vertices.Add(new Vector3(across, 0, depth));
            for (int row = 0; row < z.Count - 1; row++)
                for (int column = 0; column < x.Count - 1; column++)
                {
                    int a = row * x.Count + column;
                    Triangle(triangles, a, a + x.Count, a + 1);
                    Triangle(triangles, a + 1, a + x.Count, a + x.Count + 1);
                }
            Mesh mesh = Finish(apron ? "Ocean horizon waves" : "Dense coastal waves", vertices, new[] { triangles }, true);
            Bounds bounds = mesh.bounds;
            bounds.Expand(new Vector3(4.5f, 6, 4.5f / stretch)); mesh.bounds = bounds;
            return mesh;
        }

        public static float BeachFloorHeight(float x)
        {
            float depth = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(5.2f, 12, x)) * 4;
            return -9 - depth - Mathf.Clamp((x - 12) * 0.015f, 0, 8);
        }

        public static Mesh BeachSeabed(bool shoreline = false)
        {
            float[] x = shoreline ? new[] { 4.8f, 5.2f, 5.6f, 6, 6.4f, 6.8f, 7 }
                : new[] { -1024f, -64, 0, 4.8f, 5.2f, 5.6f, 6, 6.4f, 6.8f, 7, 8, 10, 12, 20, 64, 320, 1024 };
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            // Normalized to the existing saved floor/strip transforms: no scenery positions change.
            for (int row = 0; row < 2; row++)
                foreach (float across in x)
                {
                    float normalizedX = shoreline ? (across - 5.9f) / 2.2f : across / 2048;
                    float height = BeachFloorHeight(across) + (shoreline ? 0.035f : 0);
                    float normalizedY = shoreline ? (height + 9) / 0.06f : (height + 9.3f) / 0.6f;
                    vertices.Add(new Vector3(normalizedX, normalizedY, row - 0.5f));
                }
            for (int column = 0; column < x.Length - 1; column++)
            {
                Triangle(triangles, column, column + x.Length, column + 1);
                Triangle(triangles, column + 1, column + x.Length, column + x.Length + 1);
            }
            return Finish(shoreline ? "Sloping wet shore" : "Beach and submerged seabed", vertices, new[] { triangles }, true);
        }

        public static Mesh HipRoof()
        {
            var vertices = new List<Vector3>
            {
                new Vector3(-0.5f, 0, -0.5f), new Vector3(0.5f, 0, -0.5f),
                new Vector3(0.5f, 0, 0.5f), new Vector3(-0.5f, 0, 0.5f),
                new Vector3(-0.23f, 1, 0), new Vector3(0.23f, 1, 0)
            };
            var triangles = new List<int>();
            Triangle(triangles, 0, 4, 1); Triangle(triangles, 1, 4, 5);
            Triangle(triangles, 1, 5, 2);
            Triangle(triangles, 2, 5, 3); Triangle(triangles, 3, 5, 4);
            Triangle(triangles, 3, 4, 0);
            Triangle(triangles, 0, 1, 3); Triangle(triangles, 3, 1, 2);
            return Finish("Hipped thatched roof", vertices, new[] { triangles }, false);
        }

        private static void JoinRings(List<int> triangles, int rings, int sides)
        {
            for (int ring = 0; ring < rings - 1; ring++)
                for (int side = 0; side < sides; side++)
                {
                    int a = ring * sides + side, b = ring * sides + (side + 1) % sides;
                    Triangle(triangles, a, a + sides, b);
                    Triangle(triangles, b, a + sides, b + sides);
                }
        }

        private static void Cap(List<Vector3> vertices, List<int> triangles, Vector3 center, int start, int sides, bool top)
        {
            int tip = vertices.Count;
            vertices.Add(center);
            for (int side = 0; side < sides; side++)
            {
                int a = start + side, b = start + (side + 1) % sides;
                Triangle(triangles, tip, top ? b : a, top ? a : b);
            }
        }

        private static void DoubleQuad(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int start = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
            Triangle(triangles, start, start + 1, start + 2);
            Triangle(triangles, start, start + 2, start + 3);
            Triangle(triangles, start + 2, start + 1, start);
            Triangle(triangles, start + 3, start + 2, start);
        }

        private static void Triangle(List<int> triangles, int a, int b, int c)
        {
            triangles.Add(a); triangles.Add(b); triangles.Add(c);
        }

        private static Mesh Finish(string name, List<Vector3> vertices, List<int>[] surfaces, bool smooth)
        {
            if (!smooth)
            {
                var flat = new List<Vector3>();
                for (int surface = 0; surface < surfaces.Length; surface++)
                {
                    var indices = new List<int>();
                    foreach (int index in surfaces[surface]) { indices.Add(flat.Count); flat.Add(vertices[index]); }
                    surfaces[surface] = indices;
                }
                vertices = flat;
            }
            Mesh mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.subMeshCount = surfaces.Length;
            for (int surface = 0; surface < surfaces.Length; surface++) mesh.SetTriangles(surfaces[surface], surface);
            mesh.uv = new Vector2[vertices.Count];
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }
    }
}

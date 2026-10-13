using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CubeDash.Editor
{
    /// <summary>Original countryside architecture and landscape meshes, baked in the Editor only.</summary>
    internal static class CountrySideMeshes
    {
        public static float GroundHeight(float x, float z)
        {
            float edge = Mathf.SmoothStep(0, 1, Mathf.Clamp01((Mathf.Abs(x) - 44) / 100));
            return edge * (4 + 3 * Mathf.Sin(z * 0.023f + x * 0.017f));
        }

        public static Mesh Valley()
        {
            const int columns = 96, rows = 144;
            var vertices = new Vector3[(columns + 1) * (rows + 1)];
            var colors = new Color[vertices.Length]; var uv = new Vector2[vertices.Length];
            var triangles = new int[columns * rows * 6];
            for (int row = 0; row <= rows; row++)
                for (int column = 0; column <= columns; column++)
                {
                    int i = row * (columns + 1) + column;
                    float x = Mathf.Lerp(-190, 190, column / (float)columns), z = Mathf.Lerp(-70, 470, row / (float)rows);
                    vertices[i] = new Vector3(x, GroundHeight(x, z) - 0.025f, z);
                    float patch = Mathf.PerlinNoise(x * 0.035f + 12, z * 0.035f + 8);
                    colors[i] = Color.Lerp(new Color(0.31f, 0.49f, 0.19f), new Color(0.59f, 0.70f, 0.32f), patch);
                    uv[i] = new Vector2(x, z) * 0.08f;
                }
            int t = 0;
            for (int row = 0; row < rows; row++)
                for (int column = 0; column < columns; column++)
                {
                    int a = row * (columns + 1) + column, b = a + columns + 1;
                    triangles[t++] = a; triangles[t++] = b; triangles[t++] = a + 1;
                    triangles[t++] = a + 1; triangles[t++] = b; triangles[t++] = b + 1;
                }
            var mesh = new Mesh { name = "Countryside - rolling green valley" };
            mesh.vertices = vertices; mesh.colors = colors; mesh.uv = uv; mesh.triangles = triangles;
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }

        public static Mesh Hill()
        {
            var g = new Geometry(1);
            const int rings = 18, sides = 64;
            Vector3 Point(float radius, float angle)
            {
                float ripple = 1 + Mathf.SmoothStep(0, 1, radius * 3) * (0.055f * Mathf.Sin(angle * 3) + 0.035f * Mathf.Sin(angle * 5 + radius * 3));
                return new Vector3(Mathf.Cos(angle) * radius, Mathf.Pow(Mathf.Max(0, 1 - radius * radius), 1.65f) * ripple,
                    Mathf.Sin(angle) * radius);
            }
            for (int ring = 0; ring < rings; ring++)
                for (int side = 0; side < sides; side++)
                {
                    float a = side * Mathf.PI * 2 / sides, b = (side + 1) * Mathf.PI * 2 / sides;
                    float inner = ring / (float)rings, outer = (ring + 1) / (float)rings;
                    Color color = Color.Lerp(new Color(0.37f, 0.55f, 0.27f), new Color(0.60f, 0.69f, 0.40f), 1 - outer);
                    if (ring == 0) g.Triangle(Point(0, 0), Point(outer, a), Point(outer, b), 0, Vector3.up, color);
                    else g.Quad(Point(inner, a), Point(outer, a), Point(outer, b), Point(inner, b), 0, Vector3.up, color);
                }
            Mesh mesh = g.Finish("Countryside - sculpted soft hill");
            // Welded normals smooth the baked radial surface without smoothing architectural edges.
            var normals = mesh.normals; var verts = mesh.vertices;
            for (int i = 0; i < normals.Length; i++)
            {
                Vector3 p = verts[i]; float r2 = p.x * p.x + p.z * p.z;
                float slope = 3.3f * Mathf.Pow(Mathf.Max(0.001f, 1 - r2), 0.65f);
                normals[i] = new Vector3(p.x * slope, 1, p.z * slope).normalized;
            }
            mesh.normals = normals; return mesh;
        }

        // Shared slots: plaster, dark timber, warm timber, kawara tiles, shoji paper, stone, vermilion.
        public static Mesh Farmhouse(bool storehouse)
        {
            var g = new Geometry(7);
            float width = storehouse ? 5.4f : 7.6f, depth = storehouse ? 4.7f : 5.3f;
            float wallTop = storehouse ? 4.0f : 3.25f, ridge = wallTop + 1.8f;
            g.Box(new Vector3(0, 0.2f, 0), new Vector3(width + 0.3f, 0.4f, depth + 0.3f), 5);
            g.Box(new Vector3(0, (wallTop + 0.4f) * 0.5f, 0), new Vector3(width, wallTop - 0.4f, depth), 0);
            g.Box(new Vector3(0, 0.64f, 0), new Vector3(width + 0.06f, 0.46f, depth + 0.06f), 1);
            foreach (float x in new[] { -width * 0.49f, 0, width * 0.49f })
                foreach (float z in new[] { -depth * 0.505f, depth * 0.505f })
                    g.Box(new Vector3(x, (wallTop + 0.4f) * 0.5f, z), new Vector3(0.18f, wallTop - 0.3f, 0.18f), 1);
            foreach (float y in new[] { 1.0f, wallTop - 0.12f })
                g.Box(new Vector3(0, y, 0), new Vector3(width + 0.1f, 0.16f, depth + 0.1f), 1);
            foreach (float side in new[] { -1f, 1f })
            {
                float x = side * width * 0.5f;
                g.Triangle(new Vector3(x, wallTop, -depth * 0.5f), new Vector3(x, ridge - 0.15f, 0),
                    new Vector3(x, wallTop, depth * 0.5f), 0, Vector3.right * side);
                g.Beam(new Vector3(x * 1.01f, wallTop, -depth * 0.5f), new Vector3(x * 1.01f, ridge - 0.16f, 0), 0.16f, 1);
                g.Beam(new Vector3(x * 1.01f, wallTop, depth * 0.5f), new Vector3(x * 1.01f, ridge - 0.16f, 0), 0.16f, 1);
                g.Box(new Vector3(x * 1.01f, wallTop + 0.5f, 0), new Vector3(0.16f, 1.1f, 0.16f), 1);
            }
            float roofWidth = width + 1.5f, roofDepth = depth + 1.8f;
            Vector3 Roof(float x, float t, float side) => new Vector3(x,
                ridge - 1.95f * t + 0.40f * Mathf.Pow(t, 4), side * roofDepth * 0.5f * t);
            foreach (float side in new[] { -1f, 1f })
            {
                for (int row = 0; row < 10; row++)
                    g.Quad(Roof(-roofWidth * 0.5f, row / 10f, side), Roof(roofWidth * 0.5f, row / 10f, side),
                        Roof(roofWidth * 0.5f, (row + 1) / 10f, side), Roof(-roofWidth * 0.5f, (row + 1) / 10f, side), 3, Vector3.up);
                int tiles = Mathf.CeilToInt(roofWidth / 0.42f);
                for (int tile = 0; tile <= tiles; tile++)
                {
                    float x = Mathf.Lerp(-roofWidth * 0.5f, roofWidth * 0.5f, tile / (float)tiles);
                    var points = new Vector3[7];
                    for (int p = 0; p < points.Length; p++) points[p] = Roof(x, p / 6f, side) + Vector3.up * 0.045f;
                    g.Tube(points, 0.068f, 3, 6);
                }
                for (int row = 1; row <= 8; row++)
                    g.Beam(Roof(-roofWidth * 0.5f, row / 8f, side) + Vector3.up * 0.025f,
                        Roof(roofWidth * 0.5f, row / 8f, side) + Vector3.up * 0.025f, 0.055f, 3);
                g.Beam(Roof(-roofWidth * 0.5f, 1, side), Roof(roofWidth * 0.5f, 1, side), 0.16f, 1);
                foreach (float x in new[] { -roofWidth * 0.5f, roofWidth * 0.5f })
                    for (int row = 0; row < 8; row++) g.Beam(Roof(x, row / 8f, side), Roof(x, (row + 1) / 8f, side), 0.14f, 3);
            }
            g.Box(new Vector3(0, ridge + 0.08f, 0), new Vector3(roofWidth + 0.15f, 0.23f, 0.30f), 3);
            for (int i = 0; i < 14; i++)
                g.Box(new Vector3(Mathf.Lerp(-roofWidth * 0.46f, roofWidth * 0.46f, i / 13f), ridge + 0.22f, 0), new Vector3(0.24f, 0.08f, 0.33f), 3);

            float front = -depth * 0.5f - 0.07f;
            Shoji(g, new Vector3(0, 1.55f, front), new Vector2(1.55f, 2.05f), storehouse);
            if (!storehouse)
            {
                Shoji(g, new Vector3(-2.45f, 1.9f, front), new Vector2(1.65f, 1.35f), false);
                Shoji(g, new Vector3(2.45f, 1.9f, front), new Vector2(1.65f, 1.35f), false);
                foreach (float side in new[] { -1f, 1f })
                    Shoji(g, new Vector3(side * (width * 0.5f + 0.07f), 1.9f, 0.15f), new Vector2(1.7f, 1.35f), false, Quaternion.Euler(0, -side * 90, 0));
                g.Box(new Vector3(0, 0.34f, front - 0.62f), new Vector3(width + 0.5f, 0.18f, 1.45f), 2);
                for (int plank = 0; plank < 25; plank++)
                    g.Box(new Vector3(Mathf.Lerp(-width * 0.5f, width * 0.5f, plank / 24f), 0.445f, front - 0.62f), new Vector3(0.026f, 0.025f, 1.35f), 1);
                foreach (float x in new[] { -width * 0.48f, width * 0.48f })
                    g.Box(new Vector3(x, 1.76f, front - 1.12f), new Vector3(0.14f, 2.95f, 0.14f), 1);
                g.Box(new Vector3(0, 0.14f, front - 1.58f), new Vector3(2.1f, 0.28f, 0.68f), 5);
            }
            else
            {
                Shoji(g, new Vector3(0, 3.05f, front), new Vector2(0.85f, 0.7f), false);
                foreach (float x in new[] { -width * 0.48f, width * 0.48f })
                    for (int y = 0; y < 7; y++) g.Box(new Vector3(x, 0.7f + y * 0.45f, front), new Vector3(0.28f, 0.28f, 0.10f), 5);
            }
            return g.Finish(storehouse ? "Japanese kura - plaster storehouse and kawara roof" : "Japanese minka - timber, shoji, engawa and curved kawara roof");
        }

        private static void Shoji(Geometry g, Vector3 center, Vector2 size, bool solid, Quaternion? rotation = null)
        {
            Quaternion q = rotation ?? Quaternion.identity;
            void Panel(Vector3 offset, Vector3 dimensions, int slot) => g.Box(center + q * offset, dimensions, slot, q);
            Panel(Vector3.zero, new Vector3(size.x + 0.18f, size.y + 0.18f, 0.10f), 1);
            Panel(Vector3.back * 0.07f, new Vector3(size.x, size.y, 0.035f), solid ? 2 : 4);
            for (int x = -2; x <= 2; x++)
                Panel(new Vector3(x * size.x / 5, 0, -0.10f), new Vector3(0.04f, size.y, 0.045f), 1);
            if (!solid)
                for (int y = -2; y <= 2; y++)
                    Panel(new Vector3(0, y * size.y / 5, -0.11f), new Vector3(size.x, 0.035f, 0.045f), 1);
        }

        public static Mesh Torii()
        {
            var g = new Geometry(7);
            foreach (float x in new[] { -1.8f, 1.8f })
            {
                g.Box(new Vector3(x, 0.12f, 0), new Vector3(0.65f, 0.24f, 0.65f), 5);
                g.Beam(new Vector3(x, 0.18f, 0), new Vector3(x * 0.92f, 4.1f, 0), 0.33f, 6);
                g.Box(new Vector3(x, 0.50f, 0), new Vector3(0.37f, 0.65f, 0.37f), 1);
            }
            g.Box(new Vector3(0, 3.35f, 0), new Vector3(4.45f, 0.22f, 0.24f), 6);
            for (int i = 0; i < 8; i++)
            {
                float x = -2.65f + i * 0.6625f;
                g.Beam(new Vector3(x, 4.05f + 0.12f * x * x, 0), new Vector3(x + 0.6625f, 4.05f + 0.12f * (x + 0.6625f) * (x + 0.6625f), 0), 0.24f, 1);
            }
            g.Box(new Vector3(0, 3.72f, -0.15f), new Vector3(0.50f, 0.57f, 0.12f), 2);
            return g.Finish("Village torii - inclined pillars and lifted lintel");
        }

        public static Mesh Lantern()
        {
            var g = new Geometry(7);
            g.Box(new Vector3(0, 0.08f, 0), new Vector3(0.75f, 0.16f, 0.75f), 5);
            g.Box(new Vector3(0, 0.50f, 0), new Vector3(0.25f, 0.85f, 0.25f), 5);
            g.Box(new Vector3(0, 0.93f, 0), new Vector3(0.60f, 0.14f, 0.60f), 5);
            foreach (float x in new[] { -0.22f, 0.22f }) foreach (float z in new[] { -0.22f, 0.22f })
                g.Box(new Vector3(x, 1.22f, z), new Vector3(0.10f, 0.50f, 0.10f), 5);
            g.Box(new Vector3(0, 1.22f, 0), new Vector3(0.18f, 0.28f, 0.18f), 4);
            g.Box(new Vector3(0, 1.53f, 0), new Vector3(0.85f, 0.15f, 0.85f), 5);
            foreach (float side in new[] { -1f, 1f })
                g.Quad(new Vector3(-0.47f, 1.60f, side * 0.47f), new Vector3(0.47f, 1.60f, side * 0.47f),
                    new Vector3(0.22f, 1.84f, side * 0.22f), new Vector3(-0.22f, 1.84f, side * 0.22f), 5, Vector3.up);
            g.Box(new Vector3(0, 1.90f, 0), Vector3.one * 0.24f, 5);
            return g.Finish("Ishidoro - hollow stone garden lantern");
        }

        public static Mesh Bridge()
        {
            var g = new Geometry(7);
            float Height(float x) => 0.18f + 0.28f * Mathf.Sin((x + 2.65f) / 5.3f * Mathf.PI);
            for (int plank = 0; plank < 24; plank++)
            {
                float x = -2.55f + plank * 5.1f / 23;
                g.Box(new Vector3(x, Height(x), 0), new Vector3(0.20f, 0.14f, 2.1f), 2);
            }
            foreach (float z in new[] { -0.94f, 0.94f })
            {
                for (int post = 0; post < 7; post++)
                {
                    float x = -2.5f + post * 5f / 6;
                    g.Box(new Vector3(x, Height(x) + 0.45f, z), new Vector3(0.11f, 0.90f, 0.11f), 1);
                    if (post < 6) g.Beam(new Vector3(x, Height(x) + 0.90f, z), new Vector3(x + 5f / 6, Height(x + 5f / 6) + 0.90f, z), 0.10f, 2);
                }
            }
            return g.Finish("Countryside - arched plank footbridge");
        }

        public static Mesh Paddy()
        {
            var g = new Geometry(4); // soil, water, rice shade, fresh rice
            g.Box(new Vector3(0, -0.06f, 0), new Vector3(14, 0.18f, 26), 0);
            g.Box(new Vector3(0, 0.055f, 0), new Vector3(13.2f, 0.025f, 25.2f), 1);
            foreach (float x in new[] { -6.85f, 6.85f }) g.Box(new Vector3(x, 0.13f, 0), new Vector3(0.30f, 0.35f, 26), 0);
            foreach (float z in new[] { -12.85f, 12.85f }) g.Box(new Vector3(0, 0.13f, z), new Vector3(14, 0.35f, 0.30f), 0);
            for (int row = 0; row < 25; row++) for (int column = 0; column < 14; column++)
            {
                Vector3 p = new Vector3(-6 + column * 0.92f, 0.07f, -12 + row);
                float height = 0.30f + 0.09f * Mathf.Sin(row * 13 + column * 5);
                foreach (float side in new[] { -1f, 1f })
                {
                    g.Triangle(p + Vector3.left * 0.045f, p + new Vector3(side * 0.12f, height, 0.06f), p + Vector3.right * 0.045f, 2 + column % 2, Vector3.forward);
                    g.Triangle(p + Vector3.right * 0.045f, p + new Vector3(side * 0.12f, height, 0.06f), p + Vector3.left * 0.045f, 2 + column % 2, Vector3.back);
                }
            }
            return g.Finish("Flooded rice paddy - embankments and planted seedling rows");
        }

        internal sealed class Geometry
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<Color> colors = new List<Color>();
            private readonly List<Vector2> uv = new List<Vector2>();
            private readonly List<int>[] triangles;
            public Geometry(int slots)
            {
                triangles = new List<int>[slots]; for (int i = 0; i < slots; i++) triangles[i] = new List<int>();
            }
            private void Vertex(Vector3 p, Color color, Vector2 texture) { vertices.Add(p); colors.Add(color); uv.Add(texture); }
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, int slot, Vector3 outward, Color? tint = null)
            {
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0) { Vector3 swap = b; b = d; d = swap; }
                int start = vertices.Count; Color color = tint ?? Color.white;
                Vertex(a, color, Vector2.zero); Vertex(b, color, Vector2.right); Vertex(c, color, Vector2.one); Vertex(d, color, Vector2.up);
                triangles[slot].AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }
            public void Triangle(Vector3 a, Vector3 b, Vector3 c, int slot, Vector3 outward, Color? tint = null)
            {
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0) { Vector3 swap = b; b = c; c = swap; }
                int start = vertices.Count; Color color = tint ?? Color.white;
                Vertex(a, color, Vector2.zero); Vertex(b, color, Vector2.right); Vertex(c, color, Vector2.up);
                triangles[slot].AddRange(new[] { start, start + 1, start + 2 });
            }
            public void Box(Vector3 center, Vector3 size, int slot, Quaternion? rotation = null)
            {
                Quaternion q = rotation ?? Quaternion.identity; Vector3 half = size * 0.5f;
                Vector3 P(float x, float y, float z) => center + q * Vector3.Scale(new Vector3(x, y, z), half);
                Quad(P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1), P(1, -1, -1), slot, q * Vector3.back);
                Quad(P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), slot, q * Vector3.forward);
                Quad(P(-1, -1, -1), P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1), slot, q * Vector3.left);
                Quad(P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), P(1, -1, 1), slot, q * Vector3.right);
                Quad(P(-1, 1, -1), P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1), slot, q * Vector3.up);
                Quad(P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), P(-1, -1, 1), slot, q * Vector3.down);
            }
            public void Beam(Vector3 a, Vector3 b, float width, int slot)
                => Box((a + b) * 0.5f, new Vector3(width, Vector3.Distance(a, b), width), slot, Quaternion.FromToRotation(Vector3.up, b - a));
            public void Tube(Vector3[] points, float radius, int slot, int sides)
            {
                for (int p = 0; p < points.Length - 1; p++)
                {
                    Quaternion q = Quaternion.FromToRotation(Vector3.up, (points[p + 1] - points[p]).normalized);
                    for (int side = 0; side < sides; side++)
                    {
                        float a = side * Mathf.PI * 2 / sides, b = (side + 1) * Mathf.PI * 2 / sides;
                        Vector3 u = q * new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius;
                        Vector3 v = q * new Vector3(Mathf.Cos(b), 0, Mathf.Sin(b)) * radius;
                        Quad(points[p] + u, points[p + 1] + u, points[p + 1] + v, points[p] + v, slot, u + v);
                    }
                }
            }
            public Mesh Finish(string name)
            {
                var mesh = new Mesh { name = name, indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetUVs(0, uv); mesh.subMeshCount = triangles.Length;
                for (int i = 0; i < triangles.Length; i++) mesh.SetTriangles(triangles[i], i);
                mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
            }
        }
    }
}

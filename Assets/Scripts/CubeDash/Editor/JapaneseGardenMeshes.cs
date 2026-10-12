using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CubeDash.Editor
{
    /// <summary>Deterministic, editor-baked botanical geometry; no trees are generated during gameplay.</summary>
    internal static class JapaneseGardenMeshes
    {
        // Sakura slots: bark, bark markings, young leaves, blush, pale petals, rose buds, flower centers.
        public static Mesh Sakura()
        {
            var g = new Geometry(7);
            var random = new System.Random(314);
            Vector3[] trunk = { Vector3.zero, new Vector3(0.12f, 0.9f, 0.05f), new Vector3(-0.10f, 2, 0),
                new Vector3(0.2f, 3, -0.12f), new Vector3(0.35f, 4.1f, 0.1f), new Vector3(0.12f, 5.2f, 0.16f), new Vector3(0.36f, 6.4f, 0.02f) };
            g.Tube(trunk, new[] { 0.34f, 0.29f, 0.25f, 0.21f, 0.15f, 0.10f, 0.025f }, 0, 14);
            for (int i = 0; i < 7; i++)
            {
                Vector3 direction = Direction(i * 51 + 12);
                g.Tube(new[] { direction * 0.06f + Vector3.up * 0.3f, direction * 0.45f + Vector3.up * 0.08f, direction * 0.9f + Vector3.up * 0.02f },
                    new[] { 0.15f, 0.10f, 0.015f }, 0, 8);
            }
            // Prunus bark has horizontal lenticels rather than an unbroken, featureless cylinder.
            for (int i = 0; i < 95; i++)
            {
                float y = Range(random, 0.35f, 5.3f), angle = Range(random, 0, 360);
                Vector3 center = TrunkAt(trunk, y), outward = Direction(angle);
                float radius = Mathf.Lerp(0.31f, 0.10f, y / 5.3f);
                g.Leaf(center + outward * (radius + 0.01f), Quaternion.LookRotation(outward) * Quaternion.Euler(0, 0, 90),
                    0.018f, Range(random, 0.05f, 0.15f), 1);
            }
            for (int limb = 0; limb < 10; limb++)
            {
                float angle = limb * 137.5f + 18;
                Vector3 outward = Direction(angle), tangent = Vector3.Cross(Vector3.up, outward);
                float y = 2.35f + limb * 0.32f;
                Vector3 origin = TrunkAt(trunk, y);
                float reach = 2.65f - limb * 0.11f;
                Vector3 end = origin + outward * reach + Vector3.up * (2.1f - limb * 0.06f);
                g.Tube(new[] { origin, origin + outward * 0.45f + Vector3.up * 0.35f,
                    origin + outward * (reach * 0.65f) + Vector3.up * 1.15f, end },
                    new[] { 0.14f - limb * 0.006f, 0.115f - limb * 0.005f, 0.06f, 0.018f }, 0, 10);
                for (int fork = -1; fork <= 1; fork++)
                {
                    Vector3 forkEnd = end + tangent * (fork * 0.7f) + outward * Range(random, 0.15f, 0.5f)
                        + Vector3.up * Range(random, 0.25f, 0.8f);
                    Vector3 forkStart = Vector3.Lerp(origin, end, 0.72f);
                    g.Tube(new[] { forkStart, Vector3.Lerp(forkStart, forkEnd, 0.5f) + Vector3.up * 0.18f, forkEnd },
                        new[] { 0.047f, 0.032f, 0.007f }, 0, 7);
                    for (int twig = 0; twig < 3; twig++)
                    {
                        Vector3 tip = forkEnd + Direction(angle + twig * 105 + fork * 37) * Range(random, 0.22f, 0.58f)
                            + Vector3.up * Range(random, -0.05f, 0.42f);
                        g.Tube(new[] { forkEnd - outward * 0.2f, forkEnd, tip }, new[] { 0.018f, 0.012f, 0.0035f }, 0, 5);
                        BlossomCluster(g, tip, random, limb + fork + twig);
                    }
                }
            }
            return g.Finish("Sakura - curved trunk, fine twigs and notched five-petal flowers");
        }

        private static void BlossomCluster(Geometry g, Vector3 center, System.Random random, int variation)
        {
            Vector3 size = new Vector3(Range(random, 0.85f, 1.2f), Range(random, 0.65f, 0.95f), Range(random, 0.85f, 1.2f));
            // Small scalloped masses give the crown depth; actual flowers break up their surfaces and silhouette.
            g.Blob(center, size * 0.37f, variation * 19, variation % 3 == 0 ? 5 : 3, 5, 10);
            for (int flower = 0; flower < 26; flower++)
            {
                Vector3 normal = Unit(random);
                Vector3 point = center + Vector3.Scale(normal, size * Range(random, 0.43f, 0.59f));
                Quaternion rotation = Quaternion.FromToRotation(Vector3.forward, normal) * Quaternion.Euler(0, 0, Range(random, 0, 360));
                g.Flower(point, rotation, Range(random, 0.18f, 0.24f), flower % 5 == 0 ? 3 : 4, 6);
                if (flower % 6 == 0)
                    g.Leaf(point, rotation * Quaternion.Euler(0, 0, 25), 0.07f, 0.21f, 2);
            }
        }

        // Pine slots: bark, shadow needles, green needles, fresh tips.
        public static Mesh Pine()
        {
            var g = new Geometry(4); var random = new System.Random(72);
            Vector3[] trunk = { Vector3.zero, new Vector3(-0.2f, 1.6f, 0.1f), new Vector3(0.25f, 3.3f, -0.1f),
                new Vector3(0.48f, 4.8f, 0.08f), new Vector3(0.14f, 6.6f, 0.06f) };
            g.Tube(trunk, new[] { 0.23f, 0.18f, 0.14f, 0.09f, 0.015f }, 0, 12);
            for (int tier = 0; tier < 5; tier++)
                for (int side = 0; side < 3; side++)
                {
                    float y = 2.05f + tier * 0.91f;
                    Vector3 start = TrunkAt(trunk, y), direction = Direction(side * 120 + tier * 47);
                    float reach = 1.95f - tier * 0.24f;
                    Vector3 end = start + direction * reach + Vector3.up * 0.23f;
                    g.Tube(new[] { start, start + direction * (reach * 0.52f) + Vector3.down * 0.15f, end },
                        new[] { 0.07f, 0.04f, 0.006f }, 0, 8);
                    for (int pad = 0; pad < 3; pad++)
                    {
                        Vector3 center = end + Direction(side * 120 + pad * 100) * 0.35f + Vector3.up * (pad * 0.13f);
                        g.Blob(center, new Vector3(0.72f - tier * 0.055f, 0.27f, 0.58f), tier * 17 + pad, 1 + pad % 2, 5, 12);
                        for (int needle = 0; needle < 18; needle++)
                        {
                            Vector3 normal = Unit(random); normal.y = Mathf.Abs(normal.y) * 0.6f + 0.3f; normal.Normalize();
                            Vector3 point = center + Vector3.Scale(normal, new Vector3(0.65f, 0.24f, 0.52f));
                            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, normal);
                            for (int fan = -1; fan <= 1; fan++)
                                g.Leaf(point, rotation * Quaternion.Euler(0, fan * 60, fan * 18), 0.014f, Range(random, 0.16f, 0.28f), needle % 4 == 0 ? 3 : 2);
                        }
                    }
                }
            return g.Finish("Japanese black pine - irregular limbs and horizontal needle pads");
        }

        // Shrub slots: woody stems, deep leaves, green leaves, new growth, azalea petals, centers.
        public static Mesh Shrub()
        {
            var g = new Geometry(6); var random = new System.Random(118);
            for (int clump = 0; clump < 14; clump++)
            {
                float angle = clump * 137.5f;
                Vector3 center = Direction(angle) * (clump % 3 * 0.17f) + Vector3.up * Range(random, 0.27f, 0.53f);
                g.Tube(new[] { Vector3.up * 0.02f, center * 0.55f, center }, new[] { 0.025f, 0.015f, 0.005f }, 0, 6);
                Vector3 size = new Vector3(0.30f, 0.25f, 0.29f);
                g.Blob(center, size, clump * 27, 1 + clump % 2, 5, 10);
                for (int leaf = 0; leaf < 17; leaf++)
                {
                    Vector3 normal = Unit(random);
                    Vector3 point = center + Vector3.Scale(normal, size);
                    Quaternion rotation = Quaternion.FromToRotation(Vector3.forward, normal) * Quaternion.Euler(0, 0, Range(random, 0, 360));
                    g.Leaf(point, rotation, 0.055f, Range(random, 0.10f, 0.17f), leaf % 5 == 0 ? 3 : 2);
                    if (leaf % 11 == 0 && normal.y > 0.1f) g.Flower(point, rotation, 0.065f, 4, 5);
                }
            }
            return g.Finish("Rounded Japanese azalea - branching stems, folded leaves and scattered flowers");
        }

        // Grass slots: shaded blades, green blades, warm tips, silver seed heads.
        public static Mesh Grass(bool flowering)
        {
            var g = new Geometry(4); var random = new System.Random(flowering ? 907 : 407);
            for (int blade = 0; blade < (flowering ? 64 : 26); blade++)
            {
                float angle = Range(random, 0, 360), height = Range(random, 0.32f, flowering ? 0.9f : 0.6f);
                Vector3 direction = Direction(angle);
                Vector3 start = direction * Range(random, 0.015f, 0.19f);
                float lean = Range(random, 0.13f, 0.56f), width = Range(random, 0.018f, 0.037f);
                g.GrassBlade(start, direction, height, lean, width, blade % 3);
            }
            if (flowering)
                for (int stem = 0; stem < 7; stem++)
                {
                    Vector3 direction = Direction(stem * 137.5f), start = direction * 0.07f;
                    float height = Range(random, 0.9f, 1.25f);
                    Vector3 top = start + direction * 0.2f + Vector3.up * height;
                    g.Tube(new[] { start, start + direction * 0.08f + Vector3.up * height * 0.5f, top }, new[] { 0.006f, 0.004f, 0.002f }, 2, 5);
                    for (int seed = 0; seed < 16; seed++)
                    {
                        float progress = seed / 16f;
                        Vector3 point = top + Vector3.up * (progress * 0.22f);
                        Quaternion rotation = Quaternion.Euler(0, seed * 137.5f, -45 + progress * 25);
                        g.Leaf(point, rotation, 0.012f, 0.1f * Mathf.Sin((progress + 0.1f) * Mathf.PI), 3);
                    }
                }
            return g.Finish(flowering ? "Susuki ornamental grass - curved blades and silver panicles" : "Fine roadside grass - curved tapered blade clusters");
        }

        public static Mesh FallenPetals()
        {
            var g = new Geometry(2); var random = new System.Random(322);
            for (int i = 0; i < 150; i++)
            {
                float radius = Range(random, 0.4f, 2.2f);
                Vector3 position = Direction(Range(random, 0, 360)) * radius + Vector3.up * Range(random, 0.006f, 0.012f);
                g.Leaf(position, Quaternion.Euler(90, Range(random, 0, 360), 0), Range(random, 0.025f, 0.047f), Range(random, 0.055f, 0.085f), i % 2);
            }
            return g.Finish("Scattered sakura petals");
        }

        private static Vector3 TrunkAt(Vector3[] points, float y)
        {
            for (int i = 1; i < points.Length; i++)
                if (y <= points[i].y) return Vector3.Lerp(points[i - 1], points[i], Mathf.InverseLerp(points[i - 1].y, points[i].y, y));
            return points[points.Length - 1];
        }
        private static float Range(System.Random random, float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());
        private static Vector3 Direction(float degrees) => new Vector3(Mathf.Cos(degrees * Mathf.Deg2Rad), 0, Mathf.Sin(degrees * Mathf.Deg2Rad));
        private static Vector3 Unit(System.Random random)
        {
            float y = Range(random, -1, 1), angle = Range(random, 0, Mathf.PI * 2), radius = Mathf.Sqrt(1 - y * y);
            return new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
        }

        private sealed class Geometry
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<Vector3> normals = new List<Vector3>();
            private readonly List<Vector2> uv = new List<Vector2>();
            private readonly List<int>[] triangles;
            public Geometry(int slots)
            {
                triangles = new List<int>[slots];
                for (int i = 0; i < slots; i++) triangles[i] = new List<int>();
            }
            private int Vertex(Vector3 point, Vector3 normal, Vector2 texture)
            {
                int index = vertices.Count; vertices.Add(point); normals.Add(normal.normalized); uv.Add(texture); return index;
            }
            private void Triangle(int a, int b, int c, int slot) { triangles[slot].Add(a); triangles[slot].Add(b); triangles[slot].Add(c); }

            public void Tube(Vector3[] points, float[] radii, int slot, int sides)
            {
                int first = vertices.Count;
                for (int ring = 0; ring < points.Length; ring++)
                {
                    Vector3 tangent = points[Mathf.Min(ring + 1, points.Length - 1)] - points[Mathf.Max(0, ring - 1)];
                    Quaternion orientation = Quaternion.FromToRotation(Vector3.up, tangent.normalized);
                    for (int side = 0; side < sides; side++)
                    {
                        Vector3 normal = orientation * Direction(side * 360f / sides);
                        float ridge = 1 + 0.035f * Mathf.Sin(side * 3.7f + ring);
                        Vertex(points[ring] + normal * radii[ring] * ridge, normal, new Vector2(side / (float)sides, ring));
                    }
                }
                for (int ring = 0; ring < points.Length - 1; ring++)
                    for (int side = 0; side < sides; side++)
                    {
                        int a = first + ring * sides + side, b = first + ring * sides + (side + 1) % sides;
                        Triangle(a, a + sides, b, slot); Triangle(b, a + sides, b + sides, slot);
                    }
                int lower = Vertex(points[0], (points[0] - points[1]).normalized, Vector2.zero);
                int upper = Vertex(points[points.Length - 1], (points[points.Length - 1] - points[points.Length - 2]).normalized, Vector2.one);
                for (int side = 0; side < sides; side++)
                {
                    int next = (side + 1) % sides;
                    Triangle(lower, first + side, first + next, slot);
                    int last = first + (points.Length - 1) * sides;
                    Triangle(upper, last + next, last + side, slot);
                }
            }

            public void Blob(Vector3 center, Vector3 size, int seed, int slot, int rings, int sides)
            {
                int top = Vertex(center + Vector3.up * size.y, Vector3.up, Vector2.one), first = vertices.Count;
                for (int ring = 1; ring < rings; ring++)
                {
                    float latitude = Mathf.PI * ring / rings;
                    for (int side = 0; side < sides; side++)
                    {
                        float angle = side * Mathf.PI * 2 / sides;
                        float lobe = 1 + 0.09f * Mathf.Sin(angle * 5 + seed + latitude * 2) + 0.035f * Mathf.Cos(angle * 9 - latitude * 3);
                        Vector3 radial = new Vector3(Mathf.Cos(angle) * Mathf.Sin(latitude) * lobe, Mathf.Cos(latitude), Mathf.Sin(angle) * Mathf.Sin(latitude) * lobe);
                        Vector3 normal = new Vector3(radial.x / size.x, radial.y / size.y, radial.z / size.z);
                        Vertex(center + Vector3.Scale(radial, size), normal, new Vector2(side / (float)sides, ring / (float)rings));
                    }
                }
                int bottom = Vertex(center - Vector3.up * size.y, Vector3.down, Vector2.zero);
                for (int side = 0; side < sides; side++)
                {
                    int next = (side + 1) % sides;
                    Triangle(top, first + next, first + side, slot);
                    for (int ring = 0; ring < rings - 2; ring++)
                    {
                        int a = first + ring * sides + side, b = first + ring * sides + next;
                        Triangle(a, b, a + sides, slot); Triangle(b, b + sides, a + sides, slot);
                    }
                    int last = first + (rings - 2) * sides;
                    Triangle(bottom, last + side, last + next, slot);
                }
            }

            public void Leaf(Vector3 center, Quaternion rotation, float width, float length, int slot)
            {
                int first = vertices.Count;
                Vector3 normal = rotation * Vector3.forward;
                Vertex(center, normal, Vector2.zero);
                Vertex(center + rotation * new Vector3(-width * 0.5f, length * 0.46f, 0), normal, new Vector2(0, 0.5f));
                Vertex(center + rotation * new Vector3(0, length * 0.48f, width * 0.13f), normal, new Vector2(0.5f, 0.5f));
                Vertex(center + rotation * new Vector3(width * 0.5f, length * 0.46f, 0), normal, new Vector2(1, 0.5f));
                Vertex(center + rotation * new Vector3(0, length, -width * 0.05f), normal, Vector2.one);
                Triangle(first, first + 2, first + 1, slot); Triangle(first + 1, first + 2, first + 4, slot);
                Triangle(first, first + 3, first + 2, slot); Triangle(first + 2, first + 3, first + 4, slot);
            }

            public void Flower(Vector3 center, Quaternion rotation, float radius, int slot, int centerSlot)
            {
                Vector2[] rim = { new Vector2(-0.12f, 0.12f), new Vector2(-0.36f, 0.45f), new Vector2(-0.28f, 0.85f),
                    new Vector2(-0.1f, 1), new Vector2(0, 0.86f), new Vector2(0.1f, 1), new Vector2(0.28f, 0.85f),
                    new Vector2(0.36f, 0.45f), new Vector2(0.12f, 0.12f) };
                for (int petal = 0; petal < 5; petal++)
                {
                    Quaternion orientation = rotation * Quaternion.Euler(0, 0, petal * 72);
                    Vector3 normal = orientation * Vector3.forward;
                    int middle = Vertex(center + orientation * new Vector3(0, radius * 0.48f, radius * 0.06f), normal, new Vector2(0.5f, 0.5f));
                    int first = vertices.Count;
                    foreach (Vector2 point in rim)
                        Vertex(center + orientation * new Vector3(point.x * radius, point.y * radius, point.y * point.y * radius * 0.20f), normal, point);
                    for (int edge = 0; edge < rim.Length; edge++) Triangle(middle, first + (edge + 1) % rim.Length, first + edge, slot);
                }
                int bud = vertices.Count;
                Vector3 budCenter = center + rotation * Vector3.forward * (radius * 0.055f);
                foreach (Vector3 direction in new[] { Vector3.right, Vector3.up, Vector3.left, Vector3.down, Vector3.forward, Vector3.back })
                    Vertex(budCenter + rotation * direction * (radius * 0.09f), rotation * direction, Vector2.one * 0.5f);
                for (int side = 0; side < 4; side++)
                {
                    Triangle(bud + 4, bud + side, bud + (side + 1) % 4, centerSlot);
                    Triangle(bud + 5, bud + (side + 1) % 4, bud + side, centerSlot);
                }
            }

            public void GrassBlade(Vector3 start, Vector3 direction, float height, float lean, float width, int slot)
            {
                const int sections = 7;
                int first = vertices.Count;
                Vector3 side = Vector3.Cross(Vector3.up, direction);
                for (int i = 0; i <= sections; i++)
                {
                    float t = i / (float)sections;
                    Vector3 center = start + Vector3.up * (height * (t - 0.16f * t * t)) + direction * (lean * t * t);
                    Vector3 tangent = Vector3.up * height * (1 - 0.32f * t) + direction * (2 * lean * t);
                    Vector3 normal = Vector3.Cross(side, tangent).normalized;
                    float halfWidth = width * (1 - t) * 0.5f + 0.0005f;
                    Vertex(center - side * halfWidth, normal, new Vector2(0, t));
                    Vertex(center + side * halfWidth, normal, new Vector2(1, t));
                    if (i == 0) continue;
                    int a = first + (i - 1) * 2;
                    Triangle(a, a + 2, a + 1, slot); Triangle(a + 1, a + 2, a + 3, slot);
                }
            }

            public Mesh Finish(string name)
            {
                var mesh = new Mesh { name = name, indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uv); mesh.subMeshCount = triangles.Length;
                for (int i = 0; i < triangles.Length; i++) mesh.SetTriangles(triangles[i], i);
                mesh.RecalculateBounds(); return mesh;
            }
        }
    }
}

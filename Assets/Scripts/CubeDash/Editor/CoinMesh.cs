using System.Collections.Generic;
using UnityEngine;

namespace CubeDash.Editor
{
    /// <summary>A double-sided gold coin with a bevelled edge, raised rim and embossed star.</summary>
    internal static class CoinMesh
    {
        public static Mesh Build()
        {
            var vertices = new List<Vector3>();
            var triangles = new[] { new List<int>(), new List<int>(), new List<int>() };
            const int sides = 32;
            Vector3 Point(int i, float radius, float z)
            {
                float angle = i * Mathf.PI * 2 / sides;
                return new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, z);
            }
            void Triangle(Vector3 a, Vector3 b, Vector3 c, int material, bool reverse = false)
            {
                int start = vertices.Count;
                vertices.Add(a); vertices.Add(reverse ? c : b); vertices.Add(reverse ? b : c);
                triangles[material].Add(start); triangles[material].Add(start + 1); triangles[material].Add(start + 2);
            }
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, int material, bool reverse = false)
            {
                Triangle(a, b, c, material, reverse); Triangle(a, c, d, material, reverse);
            }
            float[] depth = { -0.065f, -0.043f, 0.043f, 0.065f };
            float[] radii = { 0.385f, 0.43f, 0.43f, 0.385f };
            for (int ring = 0; ring < 3; ring++)
                for (int i = 0; i < sides; i++)
                    Quad(Point(i, radii[ring], depth[ring]), Point(i + 1, radii[ring], depth[ring]),
                        Point(i + 1, radii[ring + 1], depth[ring + 1]), Point(i, radii[ring + 1], depth[ring + 1]),
                        ring == 1 && i % 2 == 0 ? 1 : 0);
            foreach (int side in new[] { -1, 1 })
            {
                float face = side * 0.065f, lip = side * 0.077f;
                for (int i = 0; i < sides; i++)
                {
                    Triangle(new Vector3(0, 0, face), Point(i, 0.385f, face), Point(i + 1, 0.385f, face), 0, side < 0);
                    Quad(Point(i, 0.36f, face), Point(i + 1, 0.36f, face),
                        Point(i + 1, 0.344f, lip), Point(i, 0.344f, lip), 2, side < 0);
                    Quad(Point(i, 0.344f, lip), Point(i + 1, 0.344f, lip),
                        Point(i + 1, 0.302f, lip), Point(i, 0.302f, lip), 2, side < 0);
                    Quad(Point(i, 0.302f, lip), Point(i + 1, 0.302f, lip),
                        Point(i + 1, 0.29f, face), Point(i, 0.29f, face), 1, side < 0);
                }
                Vector3[] star = new Vector3[10];
                for (int i = 0; i < star.Length; i++)
                {
                    float angle = Mathf.PI * 0.5f + i * Mathf.PI / 5;
                    float radius = i % 2 == 0 ? 0.205f : 0.092f;
                    star[i] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, side * 0.087f);
                }
                for (int i = 0; i < star.Length; i++)
                {
                    Vector3 a = star[i], b = star[(i + 1) % star.Length];
                    Triangle(new Vector3(0, 0, side * 0.087f), a, b, 2, side < 0);
                    Quad(new Vector3(a.x, a.y, face), new Vector3(b.x, b.y, face), b, a, 1, side < 0);
                }
            }
            Mesh mesh = new Mesh { name = "CubeDashCoin" };
            mesh.SetVertices(vertices); mesh.subMeshCount = triangles.Length;
            for (int i = 0; i < triangles.Length; i++) mesh.SetTriangles(triangles[i], i);
            mesh.uv = new Vector2[vertices.Count];
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }
    }
}

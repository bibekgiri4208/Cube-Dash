using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CubeDash.Editor
{
    public static partial class HighwayHazardArtBuilder
    {
        private static Mesh BuildTrafficDrum()
        {
            var g = new Geometry();
            g.Cylinder(new Vector3(0, 0.045f, 0), 0.51f, 0.09f, Rubber, Quaternion.Euler(0, 0, 90), 32);
            float[] heights = { 0.08f, 0.13f, 0.28f, 0.32f, 0.48f, 0.53f, 0.70f, 0.75f, 0.91f, 0.97f, 1.03f };
            float[] radii = { 0.42f, 0.445f, 0.43f, 0.45f, 0.44f, 0.45f, 0.43f, 0.45f, 0.42f, 0.42f, 0.38f };
            TurnedSurface(g, heights, radii, row => row == 2 || row == 5 ? Lamp : Paint);
            g.Cylinder(new Vector3(0, 1.034f, 0), 0.38f, 0.014f, Paint, Quaternion.Euler(0, 0, 90), 32);
            foreach (float x in new[] { -0.12f, 0.12f }) g.Box(new Vector3(x, 1.063f, 0), new Vector3(0.04f, 0.07f, 0.07f), Trim, 0.01f);
            g.Box(new Vector3(0, 1.09f, 0), new Vector3(0.28f, 0.028f, 0.065f), Trim, 0.008f);
            g.Cylinder(new Vector3(0, 1.035f, 0.24f), 0.053f, 0.035f, Amber, Quaternion.Euler(0, 0, 90), 16);
            return g.Finish("Traffic Drum");
        }

        private static Mesh BuildTrafficCone()
        {
            var g = new Geometry();
            g.Box(new Vector3(0, 0.055f, 0), new Vector3(1.04f, 0.11f, 1.04f), Rubber, 0.035f);
            g.Cylinder(new Vector3(0, 0.105f, 0), 0.44f, 0.06f, Trim, Quaternion.Euler(0, 0, 90), 32);
            float[] heights = { 0.13f, 0.18f, 0.42f, 0.58f, 0.73f, 0.87f, 1.06f, 1.09f };
            float[] radii = { 0.40f, 0.385f, 0.30f, 0.244f, 0.19f, 0.14f, 0.068f, 0.059f };
            TurnedSurface(g, heights, radii, row => row == 2 || row == 4 ? Lamp : Paint);
            g.Cylinder(new Vector3(0, 1.089f, 0), 0.059f, 0.014f, Paint, Quaternion.Euler(0, 0, 90), 24);
            g.Cylinder(new Vector3(0, 1.099f, 0), 0.037f, 0.007f, Trim, Quaternion.Euler(0, 0, 90), 24);
            foreach (float x in new[] { -0.43f, 0.43f })
                foreach (float z in new[] { -0.43f, 0.43f })
                    g.Cylinder(new Vector3(x, 0.116f, z), 0.02f, 0.012f, Steel, Quaternion.Euler(0, 0, 90), 8);
            return g.Finish("Weighted Traffic Cone");
        }

        private static void TurnedSurface(Geometry g, float[] heights, float[] radii, System.Func<int, int> material)
        {
            Vector3 Point(int row, int sector)
            {
                float angle = sector * Mathf.PI * 2 / 32;
                return new Vector3(Mathf.Cos(angle) * radii[row], heights[row], Mathf.Sin(angle) * radii[row]);
            }
            for (int row = 0; row < heights.Length - 1; row++)
                for (int sector = 0; sector < 32; sector++)
                {
                    Vector3 a = Point(row, sector), b = Point(row, sector + 1);
                    Vector3 hint = (a + b) * 0.5f; hint.y = 0;
                    g.Quad(a, b, Point(row + 1, sector + 1), Point(row + 1, sector), material(row), hint);
                }
        }

        private static Mesh BuildWaterBarrier()
        {
            var g = new Geometry();
            g.Box(new Vector3(0, 0.13f, 0), new Vector3(1.77f, 0.26f, 0.78f), Paint, 0.10f);
            g.Box(new Vector3(0, 0.58f, 0), new Vector3(1.65f, 0.97f, 0.51f), Paint, 0.12f);
            for (int side = -1; side <= 1; side += 2)
            {
                for (int rib = -3; rib <= 3; rib++)
                    g.Box(new Vector3(rib * 0.22f, 0.56f, side * 0.258f), new Vector3(0.055f, 0.68f, 0.035f), Paint, 0.014f);
                g.Box(new Vector3(0, 0.78f, side * 0.28f), new Vector3(1.45f, 0.16f, 0.018f), Lamp, 0.008f);
                for (int mark = -3; mark <= 3; mark++)
                    g.Box(new Vector3(mark * 0.20f, 0.78f, side * 0.294f), new Vector3(0.085f, 0.13f, 0.008f), Amber, 0.002f,
                        Quaternion.Euler(0, 0, -25));
                g.Cylinder(new Vector3(side * 0.62f, 0.34f, side * 0.277f), 0.037f, 0.024f, Trim, Quaternion.Euler(0, 90, 0), 12);
                g.Box(new Vector3(side * 0.77f, 0.92f, 0), new Vector3(0.10f, 0.13f, 0.26f), Trim, 0.035f);
            }
            g.Cylinder(new Vector3(0.47f, 1.078f, 0), 0.074f, 0.035f, Trim, Quaternion.Euler(0, 0, 90), 16);
            return g.Finish("Water Filled Barrier");
        }

        private static Mesh BuildWorkBarrier()
        {
            var g = new Geometry();
            for (int side = -1; side <= 1; side += 2)
            {
                g.Box(new Vector3(side * 0.67f, 0.035f, 0), new Vector3(0.27f, 0.07f, 0.77f), Rubber, 0.025f);
                g.Beam(new Vector3(side * 0.67f, 0.05f, -0.31f), new Vector3(side * 0.67f, 1.04f, 0), 0.075f, Steel);
                g.Beam(new Vector3(side * 0.67f, 0.05f, 0.31f), new Vector3(side * 0.67f, 1.04f, 0), 0.075f, Steel);
                g.Box(new Vector3(side * 0.67f, 1.065f, 0), new Vector3(0.15f, 0.055f, 0.15f), Trim, 0.012f);
                g.Cylinder(new Vector3(side * 0.67f, 1.115f, 0), 0.055f, 0.055f, Amber, Quaternion.Euler(0, 0, 90), 16);
            }
            foreach (float y in new[] { 0.37f, 0.80f })
            {
                g.Box(new Vector3(0, y, 0), new Vector3(1.77f, 0.27f, 0.10f), Paint, 0.025f);
                for (int side = -1; side <= 1; side += 2)
                {
                    for (float stripe = -1; stripe < 0.9f; stripe += 0.32f)
                    {
                        var polygon = new System.Collections.Generic.List<Vector2> { new Vector2(stripe, y - 0.10f), new Vector2(stripe + 0.13f, y - 0.10f),
                            new Vector2(stripe + 0.25f, y + 0.10f), new Vector2(stripe + 0.12f, y + 0.10f) };
                        polygon = ClipX(ClipX(polygon, -0.85f, true), 0.85f, false);
                        if (polygon.Count > 2) g.Cap(polygon.Select(p => new Vector3(p.x, p.y, side * 0.054f)).ToArray(), Lamp, Vector3.forward * side);
                    }
                    foreach (float x in new[] { -0.67f, 0.67f })
                        g.Cylinder(new Vector3(x, y, side * 0.065f), 0.023f, 0.018f, Steel, Quaternion.Euler(0, 90, 0), 12);
                }
            }
            return g.Finish("Work Zone Barricade");
        }

        private static Mesh BuildBoxTruck()
        {
            var g = new Geometry();
            // Chamfered cab with a sloping windscreen, separated from the ribbed delivery body.
            Vector3[] outline = { new Vector3(0, 0.61f, -2.16f), new Vector3(0, 1.12f, -2.16f),
                new Vector3(0, 1.76f, -1.75f), new Vector3(0, 1.82f, -1.60f), new Vector3(0, 1.82f, -0.80f), new Vector3(0, 0.61f, -0.80f) };
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3[] face = outline.Select(p => p + Vector3.right * side * 0.71f).ToArray();
                g.Cap(face, Paint, Vector3.right * side);
                g.Quad(new Vector3(side * 0.719f, 1.11f, -2.06f), new Vector3(side * 0.719f, 1.11f, -0.92f),
                    new Vector3(side * 0.719f, 1.67f, -0.92f), new Vector3(side * 0.719f, 1.67f, -1.71f), Glass, Vector3.right * side);
                g.Box(new Vector3(side * 0.73f, 0.94f, -1.08f), new Vector3(0.045f, 0.035f, 0.18f), Steel, 0.01f);
                g.Box(new Vector3(side * 0.78f, 1.29f, -1.94f), new Vector3(0.10f, 0.19f, 0.16f), Trim, 0.025f);
                g.Box(new Vector3(side * 0.78f, 1.29f, -2.026f), new Vector3(0.075f, 0.15f, 0.009f), Glass, 0.002f);
                g.Box(new Vector3(side * 0.51f, 0.82f, -2.175f), new Vector3(0.30f, 0.19f, 0.045f), Trim, 0.025f);
                g.Box(new Vector3(side * 0.50f, 0.84f, -2.205f), new Vector3(0.21f, 0.12f, 0.018f), Lamp, 0.01f);
                g.Box(new Vector3(side * 0.65f, 0.82f, -2.205f), new Vector3(0.065f, 0.12f, 0.018f), Amber, 0.007f);
                foreach (float axle in new[] { -1.65f, 1.42f })
                {
                    g.Tire(new Vector3(side * 0.70f, 0.30f, axle));
                    g.Cylinder(new Vector3(side * 0.803f, 0.30f, axle), 0.18f, 0.035f, Steel, Quaternion.identity, 20);
                    g.Cylinder(new Vector3(side * 0.826f, 0.30f, axle), 0.065f, 0.018f, Trim, Quaternion.identity, 16);
                    for (int lug = 0; lug < 6; lug++)
                    {
                        float angle = lug * Mathf.PI / 3;
                        g.Cylinder(new Vector3(side * 0.829f, 0.30f + Mathf.Cos(angle) * 0.11f, axle + Mathf.Sin(angle) * 0.11f), 0.023f, 0.013f, Trim, Quaternion.identity, 8);
                    }
                }
                g.Box(new Vector3(side * 0.79f, 0.61f, 1.42f), new Vector3(0.11f, 0.06f, 0.86f), Trim, 0.02f);
                g.Box(new Vector3(side * 0.70f, 0.42f, 1.82f), new Vector3(0.23f, 0.32f, 0.035f), Rubber, 0.01f);
            }
            for (int edge = 0; edge < outline.Length; edge++)
            {
                Vector3 a = outline[edge], b = outline[(edge + 1) % outline.Length];
                Vector3 hint = (a + b) * 0.5f - new Vector3(0, 1.15f, -1.5f);
                g.Quad(a + Vector3.left * 0.71f, a + Vector3.right * 0.71f, b + Vector3.right * 0.71f, b + Vector3.left * 0.71f, Paint, hint);
            }
            Vector3 Screen(float x, float y) => new Vector3(x, y, -2.16f + (y - 1.12f) * 0.41f / 0.64f - 0.012f);
            g.Quad(Screen(-0.64f, 1.20f), Screen(0.64f, 1.20f), Screen(0.61f, 1.68f), Screen(-0.61f, 1.68f), Glass, new Vector3(0, 1, -1));
            foreach (float side in new[] { -1f, 1f }) g.Beam(Screen(side * 0.10f, 1.22f), Screen(side * 0.54f, 1.32f), 0.014f, Trim);
            g.Box(new Vector3(0, 0.42f, 0), new Vector3(1.32f, 0.16f, 4.32f), Trim, 0.04f);
            g.Box(new Vector3(0, 1.50f, 0.69f), new Vector3(1.66f, 1.46f, 2.94f), Paint, 0.055f);
            foreach (float y in new[] { 0.81f, 2.21f })
                g.Box(new Vector3(0, y, 0.69f), new Vector3(1.68f, 0.055f, 2.96f), Steel, 0.018f);
            for (int side = -1; side <= 1; side += 2)
            {
                for (int rib = 0; rib < 11; rib++)
                    g.Box(new Vector3(side * 0.834f, 1.50f, -0.62f + rib * 0.26f), new Vector3(0.018f, 1.22f, 0.025f), Paint, 0.006f);
                g.Box(new Vector3(side * 0.83f, 0.97f, 0.69f), new Vector3(0.025f, 0.055f, 2.68f), Lamp, 0.009f);
                foreach (float z in new[] { -0.56f, 1.85f })
                    g.Box(new Vector3(side * 0.839f, 0.97f, z), new Vector3(0.015f, 0.07f, 0.13f), Amber, 0.005f);
                foreach (float y in new[] { 1.04f, 1.93f })
                    g.Box(new Vector3(side * 0.72f, y, 2.175f), new Vector3(0.12f, 0.045f, 0.028f), Steel, 0.008f);
                g.Beam(new Vector3(side * 0.35f, 0.91f, 2.175f), new Vector3(side * 0.35f, 2.10f, 2.175f), 0.025f, Steel);
                g.Box(new Vector3(side * 0.62f, 0.63f, 2.20f), new Vector3(0.24f, 0.13f, 0.025f), Tail, 0.012f);
            }
            g.Beam(new Vector3(0, 0.83f, 2.167f), new Vector3(0, 2.17f, 2.167f), 0.014f, Trim);
            g.Box(new Vector3(0, 0.51f, -2.19f), new Vector3(1.47f, 0.18f, 0.16f), Trim, 0.035f);
            g.Box(new Vector3(0, 0.47f, 2.21f), new Vector3(1.55f, 0.12f, 0.12f), Steel, 0.025f);
            g.Box(new Vector3(0, 0.89f, -2.174f), new Vector3(0.56f, 0.30f, 0.02f), Trim, 0.015f);
            for (int slat = 0; slat < 4; slat++) g.Box(new Vector3(0, 0.78f + slat * 0.06f, -2.19f), new Vector3(0.50f, 0.018f, 0.02f), Steel, 0.004f);
            return g.Finish("Delivery Box Truck");
        }

        private static void BakeGuardrails()
        {
            Mesh mesh = BuildGuardrails();
            Save(mesh, "Assets/3D Models/Environments/Highway Guardrails.asset");
            var material = new Material(Shader.Find("CubeDash/Highway Hazard")) { name = "Highway Guardrail", enableInstancing = true };
            material.SetColor("_BaseColor", Color.white); material.SetFloat("_Metallic", 0.68f); material.SetFloat("_Smoothness", 0.48f);
            material.SetFloat("_BumpScale", 0.22f); material.SetTexture("_BaseMap", Texture("Galvanized", "Albedo"));
            material.SetTexture("_BumpMap", Texture("Galvanized", "Normal")); material.SetTexture("_SurfaceMap", Texture("Galvanized", "Surface"));
            Save(material, "Assets/Material/Environments/Highway Guardrail.mat");
        }

        private static Mesh BuildGuardrails()
        {
            var g = new Geometry();
            float[] heights = { 0.32f, 0.35f, 0.41f, 0.48f, 0.55f, 0.61f, 0.64f };
            float[] offsets = { 0.015f, -0.045f, -0.070f, 0.025f, -0.070f, -0.045f, 0.015f };
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 P(int row, float z, float back = 0) => new Vector3(side * (5.06f + offsets[row] + back), heights[row], z);
                // True folded W-section, including back skin and rolled top/bottom edges. Matching seam rings at 0/42 m.
                for (int section = 0; section < 56; section++)
                {
                    float a = section * 0.75f, b = a + 0.75f;
                    for (int row = 0; row < heights.Length - 1; row++)
                    {
                        g.Quad(P(row, a), P(row, b), P(row + 1, b), P(row + 1, a), Steel, Vector3.left * side);
                        g.Quad(P(row, a, 0.012f), P(row + 1, a, 0.012f), P(row + 1, b, 0.012f), P(row, b, 0.012f), Steel, Vector3.right * side);
                    }
                    foreach (int row in new[] { 0, heights.Length - 1 })
                        g.Quad(P(row, a), P(row, a, 0.012f), P(row, b, 0.012f), P(row, b), Steel, row == 0 ? Vector3.down : Vector3.up);
                }
                for (int post = 0; post < 7; post++)
                {
                    float z = 3 + post * 6;
                    g.Box(new Vector3(side * 5.19f, 0.16f, z), new Vector3(0.075f, 0.82f, 0.045f), Steel, 0.01f);
                    foreach (float flange in new[] { -0.06f, 0.06f })
                        g.Box(new Vector3(side * 5.19f, 0.16f, z + flange), new Vector3(0.17f, 0.82f, 0.025f), Steel, 0.006f);
                    g.Box(new Vector3(side * 5.12f, 0.48f, z), new Vector3(0.11f, 0.18f, 0.14f), Steel, 0.018f);
                    g.Box(new Vector3(side * 5.19f, -0.235f, z), new Vector3(0.25f, 0.03f, 0.24f), Steel, 0.008f);
                    foreach (float y in new[] { 0.41f, 0.55f })
                        g.Cylinder(new Vector3(side * 4.982f, y, z), 0.022f, 0.022f, Steel, Quaternion.identity, 8);
                    g.Box(new Vector3(side * 5.035f, 0.71f, z), new Vector3(0.035f, 0.18f, 0.15f), Trim, 0.015f);
                    // Front and rear facing reflectors remain visible while driving along either verge.
                    foreach (int facing in new[] { -1, 1 })
                        g.Box(new Vector3(side * 5.025f, 0.72f, z + facing * 0.08f), new Vector3(0.033f, 0.12f, 0.012f), facing < 0 ? Amber : Lamp, 0.004f);
                }
                for (int lap = 1; lap < 7; lap++)
                    foreach (float y in new[] { 0.37f, 0.59f })
                        g.Cylinder(new Vector3(side * 5.004f, y, lap * 6), 0.018f, 0.016f, Steel, Quaternion.identity, 8);
            }
            Mesh result = g.Finish("Highway Guardrails");
            // Preserve the saved one-material renderer on every map; bake steel/reflector colors into the mesh.
            var colors = result.colors;
            Color[] tints = { Color.white, Color.gray, new Color(0.045f, 0.05f, 0.06f), new Color(0.61f, 0.66f, 0.68f), Color.gray,
                new Color(0.055f, 0.065f, 0.075f), new Color(1, 0.48f, 0.045f), new Color(0.92f, 0.94f, 0.88f), Color.red };
            int[] triangles = Enumerable.Range(0, result.subMeshCount).SelectMany(slot => {
                int[] indices = result.GetTriangles(slot); foreach (int i in indices) colors[i] = tints[slot]; return indices;
            }).ToArray();
            result.colors = colors; result.subMeshCount = 1; result.SetTriangles(triangles, 0); return result;
        }
    }
}

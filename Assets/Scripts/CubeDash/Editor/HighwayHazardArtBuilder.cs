using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace CubeDash.Editor
{
    /// <summary>Bakes detailed traffic art and tileable PBR textures, never rewrites scenes or downloaded art.</summary>
    public static partial class HighwayHazardArtBuilder
    {
        public const string ArtPath = "Assets/Highway Hazards";
        private const int Paint = 0, Concrete = 1, Rubber = 2, Steel = 3, Glass = 4, Trim = 5, Amber = 6, Lamp = 7, Tail = 8;
        private const int MaterialCount = 9, TextureSize = 256;

        [MenuItem("Tools/Cube Dash/Build Detailed Road Blocks and Vans")]
        public static void ApplyFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode before baking traffic art.");
            foreach (string path in new[] { ArtPath, ArtPath + "/Meshes", ArtPath + "/Materials", ArtPath + "/Textures" }) Folder(path);
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/HighwayHazard.shader");
            if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("The highway hazard shader must compile first.");
            foreach (string surface in new[] { "Paint", "Concrete", "Rubber", "Steel", "Glass", "Reflector", "Galvanized" }) BakeTextures(surface);
            Material Make(string name, string surface, Color tint, float metal, float gloss, float relief, Color emission = default)
            {
                var material = new Material(shader) { name = "Traffic " + name, enableInstancing = true };
                material.SetColor("_BaseColor", tint); material.SetColor("_EmissionColor", emission);
                material.SetFloat("_Metallic", metal); material.SetFloat("_Smoothness", gloss); material.SetFloat("_BumpScale", relief);
                material.SetTexture("_BaseMap", Texture(surface, "Albedo")); material.SetTexture("_BumpMap", Texture(surface, "Normal"));
                material.SetTexture("_SurfaceMap", Texture(surface, "Surface"));
                return Save(material, ArtPath + "/Materials/" + material.name + ".mat");
            }
            Material[] materials = {
                Make("Color Match Paint", "Paint", Color.white, 0.2f, 0.52f, 0.18f),
                Make("Cast Concrete", "Concrete", new Color(0.42f, 0.44f, 0.41f), 0, 0.13f, 0.48f),
                Make("Treaded Rubber", "Rubber", new Color(0.065f, 0.075f, 0.082f), 0, 0.19f, 0.65f),
                Make("Brushed Alloy", "Steel", new Color(0.52f, 0.57f, 0.61f), 0.75f, 0.58f, 0.25f),
                Make("Smoked Glass", "Glass", new Color(0.055f, 0.13f, 0.19f), 0.32f, 0.88f, 0.05f),
                Make("Recesses and Seals", "Paint", new Color(0.022f, 0.03f, 0.038f), 0.05f, 0.28f, 0.12f),
                Make("Amber Reflectors", "Reflector", new Color(1, 0.55f, 0.055f), 0.22f, 0.66f, 0.3f, new Color(0.07f, 0.026f, 0.001f)),
                Make("Headlamp Lenses", "Reflector", new Color(0.8f, 0.9f, 0.95f), 0.3f, 0.75f, 0.2f, new Color(0.16f, 0.17f, 0.16f)),
                Make("Rear Lamp Lenses", "Reflector", new Color(0.58f, 0.025f, 0.018f), 0.15f, 0.66f, 0.25f, new Color(0.07f, 0.001f, 0.001f))
            };
            Mesh barrier = Save(BuildBarrier(), ArtPath + "/Meshes/Reinforced Road Block.asset");
            Mesh van = Save(BuildVan(), ArtPath + "/Meshes/Panel Van.asset");
            Mesh truck = Save(BuildBoxTruck(), ArtPath + "/Meshes/Delivery Box Truck.asset");
            Mesh[] barrierVariants = { Save(BuildWaterBarrier(), ArtPath + "/Meshes/Water Filled Barrier.asset"), Save(BuildWorkBarrier(), ArtPath + "/Meshes/Work Zone Barricade.asset") };
            Mesh[] vanVariants = { Save(BuildVan(1), ArtPath + "/Meshes/Passenger Shuttle.asset"), Save(BuildVan(2), ArtPath + "/Meshes/Service Van.asset") };
            Mesh delivery = BuildVan(); delivery.name = "Long Wheelbase Delivery Van";
            Vector3 stretch = new Vector3(1.05f, 1.20f, 1.345f);
            delivery.vertices = delivery.vertices.Select(p => Vector3.Scale(p, stretch)).ToArray();
            delivery.normals = delivery.normals.Select(p => new Vector3(p.x / stretch.x, p.y / stretch.y, p.z / stretch.z).normalized).ToArray();
            delivery.RecalculateTangents(); delivery.RecalculateBounds();
            Mesh[] truckVariants = { Save(delivery, ArtPath + "/Meshes/Long Wheelbase Delivery Van.asset") };
            const string resource = "Assets/Resources/HighwayHazards.asset";
            var data = AssetDatabase.LoadAssetAtPath<HighwayHazardVisuals>(resource);
            if (data == null) { data = ScriptableObject.CreateInstance<HighwayHazardVisuals>(); AssetDatabase.CreateAsset(data, resource); }
            var serialized = new SerializedObject(data);
            serialized.FindProperty("barrier").objectReferenceValue = barrier; serialized.FindProperty("van").objectReferenceValue = van;
            serialized.FindProperty("truck").objectReferenceValue = truck;
            void Variants(string name, Mesh[] values)
            {
                SerializedProperty property = serialized.FindProperty(name); property.arraySize = values.Length;
                for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
            Variants("barrierVariants", barrierVariants); Variants("vanVariants", vanVariants); Variants("truckVariants", truckVariants);
            var array = serialized.FindProperty("materials"); array.arraySize = materials.Length;
            for (int i = 0; i < materials.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = materials[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            BakeGuardrails(); AssetDatabase.SaveAssets();
            Debug.Log("Baked eight detailed traffic variants and galvanized W-beam guardrails. Legacy runtime box traffic removed; scenes, hitboxes, encounters and downloaded assets unchanged.");
        }

        private static Mesh BuildBarrier()
        {
            var g = new Geometry();
            float[] ys = { 0, 0.08f, 0.18f, 0.59f, 0.95f, 1.02f };
            float[] widths = { 0.84f, 0.89f, 0.86f, 0.80f, 0.80f, 0.77f };
            float[] depths = { 0.35f, 0.395f, 0.33f, 0.19f, 0.145f, 0.13f };
            Vector3[] Ring(int row) => RectangleRing(widths[row], depths[row], 0.045f).Select(p => new Vector3(p.x, ys[row], p.y)).ToArray();
            for (int row = 0; row < ys.Length - 1; row++)
            {
                Vector3[] a = Ring(row), b = Ring(row + 1);
                for (int side = 0; side < 8; side++)
                {
                    int next = (side + 1) % 8;
                    Vector3 outward = (a[side] + a[next]) * 0.5f; outward.y = 0;
                    g.Quad(a[side], a[next], b[next], b[side], Concrete, outward);
                }
            }
            g.Cap(Ring(0), Concrete, Vector3.down); g.Cap(Ring(ys.Length - 1), Concrete, Vector3.up);
            float Depth(float y) => Sample(ys, depths, y);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 Face(float x, float y, float offset = 0.008f) => new Vector3(x, y, side * (Depth(y) + offset));
                float[] rows = { 0.28f, 0.59f, 0.95f };
                for (int row = 0; row < rows.Length - 1; row++)
                    g.Quad(Face(-0.75f, rows[row]), Face(0.75f, rows[row]), Face(0.75f, rows[row + 1]), Face(-0.75f, rows[row + 1]), Paint, Vector3.forward * side);
                g.Quad(Face(-0.745f, 0.72f, 0.014f), Face(0.745f, 0.72f, 0.014f), Face(0.745f, 0.94f, 0.014f), Face(-0.745f, 0.94f, 0.014f), Trim, Vector3.forward * side);
                for (float stripe = -0.96f; stripe < 0.9f; stripe += 0.265f)
                {
                    var polygon = new List<Vector2> { new Vector2(stripe, 0.72f), new Vector2(stripe + 0.115f, 0.72f),
                        new Vector2(stripe + 0.265f, 0.94f), new Vector2(stripe + 0.15f, 0.94f) };
                    polygon = ClipX(ClipX(polygon, -0.735f, true), 0.735f, false);
                    if (polygon.Count > 2) g.Cap(polygon.Select(p => Face(p.x, p.y, 0.019f)).ToArray(), Amber, Vector3.forward * side);
                }
                foreach (float x in new[] { -0.67f, 0.67f })
                    foreach (float y in new[] { 0.37f, 0.64f })
                        g.Cylinder(Face(x, y, 0.02f), 0.022f, 0.014f, Steel, Quaternion.FromToRotation(Vector3.right, Vector3.forward), 12);
                // Recessed end grips with a raised alloy lip, not flat painted handles.
                float end = side * 0.803f;
                g.Box(new Vector3(end, 0.76f, 0), new Vector3(0.014f, 0.10f, 0.17f), Trim, 0.008f);
                for (int edge = -1; edge <= 1; edge += 2)
                    g.Box(new Vector3(end + side * 0.008f, 0.76f + edge * 0.057f, 0), new Vector3(0.024f, 0.018f, 0.20f), Steel, 0.005f);
            }
            foreach (float x in new[] { -0.64f, 0.64f })
            {
                g.Box(new Vector3(x, 1.027f, 0), new Vector3(0.14f, 0.028f, 0.15f), Trim, 0.012f);
                g.Box(new Vector3(x, 1.09f, 0), new Vector3(0.105f, 0.12f, 0.115f), Amber, 0.028f);
            }
            foreach (float x in new[] { -0.32f, 0.32f })
            {
                g.Box(new Vector3(x, 1.023f, 0), new Vector3(0.17f, 0.006f, 0.075f), Trim, 0.01f);
                for (int side = -1; side <= 1; side += 2)
                    g.Box(new Vector3(x, 1.029f, side * 0.047f), new Vector3(0.20f, 0.016f, 0.018f), Steel, 0.004f);
            }
            return g.Finish("Reinforced Road Block");
        }

        private static Mesh BuildVan(int style = 0)
        {
            var g = new Geometry();
            const float wheelY = 0.30f, frontAxle = -1.04f, rearAxle = 1.02f;
            float[] stations = { -1.57f, -1.42f, -0.82f, -0.62f, 1.40f, 1.57f };
            float[] heights = { 0.91f, 0.97f, 1.78f, 1.82f, 1.82f, 1.74f };
            if (style == 2) for (int i = 2; i < heights.Length; i++) heights[i] -= 0.17f;
            float Top(float z) => Sample(stations, heights, z);
            float Width(float z) => z < -1.42f ? Mathf.Lerp(0.69f, 0.73f, Mathf.InverseLerp(-1.57f, -1.42f, z)) : z > 1.40f ? Mathf.Lerp(0.73f, 0.70f, Mathf.InverseLerp(1.40f, 1.57f, z)) : 0.73f;
            float Bottom(float z)
            {
                foreach (float axle in new[] { frontAxle, rearAxle })
                    if (Mathf.Abs(z - axle) < 0.345f) return wheelY + Mathf.Sqrt(Mathf.Max(0, 0.345f * 0.345f - (z - axle) * (z - axle)));
                return 0.39f;
            }
            Vector3[] Ring(float z)
            {
                float w = Width(z), h = Top(z), low = Bottom(z);
                return new[] { new Vector3(-w, low, z), new Vector3(-w, h - 0.14f, z), new Vector3(-w * 0.93f, h - 0.04f, z),
                    new Vector3(-w * 0.80f, h, z), new Vector3(w * 0.80f, h, z), new Vector3(w * 0.93f, h - 0.04f, z),
                    new Vector3(w, h - 0.14f, z), new Vector3(w, low, z) };
            }
            var sections = new SortedSet<float>(stations);
            // Dense sampling creates real open wheel arches and follows the curved road without long rigid faces.
            for (float z = -1.57f; z < 1.57f; z += 0.18f) sections.Add(z);
            foreach (float axle in new[] { frontAxle, rearAxle })
            {
                sections.Add(axle - 0.345f); sections.Add(axle + 0.345f);
                for (int step = 0; step <= 16; step++) sections.Add(axle + Mathf.Sin((-90 + step * 180f / 16) * Mathf.Deg2Rad) * 0.345f);
            }
            var spaced = new List<float>();
            foreach (float z in sections) if (spaced.Count == 0 || z - spaced[spaced.Count - 1] > 0.001f) spaced.Add(z);
            float[] rows = spaced.ToArray();
            for (int row = 0; row < rows.Length - 1; row++)
            {
                Vector3[] a = Ring(rows[row]), b = Ring(rows[row + 1]);
                for (int face = 0; face < 8; face++)
                {
                    int next = (face + 1) % 8;
                    Vector3 hint = (a[face] + a[next]) * 0.5f - new Vector3(0, (Top(rows[row]) + Bottom(rows[row])) * 0.5f, rows[row]);
                    g.Quad(a[face], b[face], b[next], a[next], face == 7 ? Trim : Paint, hint);
                }
            }
            g.Cap(Ring(-1.57f), Paint, Vector3.back); g.Cap(Ring(1.57f), Paint, Vector3.forward);
            g.Box(new Vector3(0, 0.32f, 0), new Vector3(1.22f, 0.12f, 3.04f), Trim, 0.035f);
            // Sloping windscreen, inset opaque glass, dark weather seals and separate wiper arms.
            Vector3 Screen(float x, float z, float lift) => new Vector3(x, Top(z) + lift, z);
            g.Quad(Screen(-0.60f, -1.36f, 0.008f), Screen(0.60f, -1.36f, 0.008f), Screen(0.58f, -0.86f, 0.008f), Screen(-0.58f, -0.86f, 0.008f), Trim, new Vector3(0, 1, -1));
            g.Quad(Screen(-0.565f, -1.325f, 0.014f), Screen(0.565f, -1.325f, 0.014f), Screen(0.55f, -0.89f, 0.014f), Screen(-0.55f, -0.89f, 0.014f), Glass, new Vector3(0, 1, -1));
            for (int side = -1; side <= 1; side += 2)
            {
                g.Beam(Screen(side * 0.08f, -1.30f, 0.03f), Screen(side * 0.50f, -1.10f, 0.03f), 0.012f, Trim);
                float x = side * 0.738f;
                Vector3[] window = { new Vector3(x, 1.00f, -1.30f), new Vector3(x, 1.00f, -0.18f),
                    new Vector3(x, style == 2 ? 1.43f : 1.60f, -0.18f), new Vector3(x, style == 2 ? 1.43f : 1.60f, -0.85f) };
                g.Quad(window[0], window[1], window[2], window[3], Trim, Vector3.right * side);
                Vector3 center = window.Aggregate(Vector3.zero, (sum, p) => sum + p) / 4;
                Vector3[] pane = window.Select(p => Vector3.Lerp(center, p, 0.91f) + Vector3.right * side * 0.005f).ToArray();
                g.Quad(pane[0], pane[1], pane[2], pane[3], Glass, Vector3.right * side);
                // Recessed driver/cargo door outlines and three-dimensional handles.
                g.Beam(new Vector3(x, 0.64f, -0.12f), new Vector3(x, style == 2 ? 1.49f : 1.67f, -0.12f), 0.012f, Trim);
                Vector3[] door = { new Vector3(x, 0.69f, 0.09f), new Vector3(x, 0.69f, 1.40f), new Vector3(x, style == 2 ? 1.42f : 1.59f, 1.40f), new Vector3(x, style == 2 ? 1.42f : 1.59f, 0.09f) };
                g.Quad(door[0], door[1], door[2], door[3], Paint, Vector3.right * side, 0.94f);
                for (int edge = 0; edge < 4; edge++) g.Beam(door[edge] + Vector3.right * side * 0.004f, door[(edge + 1) % 4] + Vector3.right * side * 0.004f, 0.009f, Trim);
                foreach (float z in new[] { -0.31f, 0.29f })
                {
                    g.Box(new Vector3(side * 0.747f, 0.92f, z), new Vector3(0.022f, 0.068f, 0.20f), Trim, 0.012f);
                    g.Box(new Vector3(side * 0.765f, 0.93f, z), new Vector3(0.025f, 0.022f, 0.145f), Steel, 0.006f);
                }
                g.Box(new Vector3(side * 0.746f, 0.69f, 0.01f), new Vector3(0.024f, 0.055f, 2.84f), Trim, 0.012f);
                foreach (float y in new[] { 1.10f, 1.47f })
                    g.Box(new Vector3(side * 0.64f, y, 1.589f), new Vector3(0.045f, 0.085f, 0.026f), Steel, 0.008f);
                g.Box(new Vector3(side * 0.755f, 1.20f, -1.06f), new Vector3(0.09f, 0.15f, 0.16f), Trim, 0.025f);
                g.Box(new Vector3(side * 0.755f, 1.20f, -1.147f), new Vector3(0.065f, 0.115f, 0.009f), Glass, 0.006f);
                g.Box(new Vector3(side * 0.52f, 0.75f, -1.59f), new Vector3(0.28f, 0.19f, 0.065f), Trim, 0.03f);
                g.Box(new Vector3(side * 0.50f, 0.76f, -1.627f), new Vector3(0.21f, 0.12f, 0.025f), Lamp, 0.014f);
                g.Box(new Vector3(side * 0.655f, 0.74f, -1.616f), new Vector3(0.07f, 0.10f, 0.025f), Amber, 0.01f);
                g.Box(new Vector3(side * 0.60f, 0.73f, 1.584f), new Vector3(0.13f, 0.32f, 0.035f), Trim, 0.018f);
                g.Box(new Vector3(side * 0.60f, 0.79f, 1.607f), new Vector3(0.09f, 0.17f, 0.016f), Tail, 0.007f);
                g.Box(new Vector3(side * 0.60f, 0.66f, 1.607f), new Vector3(0.09f, 0.065f, 0.016f), Amber, 0.007f);
                foreach (float axle in new[] { frontAxle, rearAxle })
                {
                    // Tires have chamfered sidewalls and actual circular tread, not square wheel blocks.
                    g.Tire(new Vector3(side * 0.65f, wheelY, axle));
                    g.Cylinder(new Vector3(side * 0.752f, wheelY, axle), 0.17f, 0.024f, Steel, Quaternion.identity, 20);
                    g.Cylinder(new Vector3(side * 0.768f, wheelY, axle), 0.115f, 0.012f, Trim, Quaternion.identity, 20);
                    for (int spoke = 0; spoke < 5; spoke++)
                    {
                        float angle = spoke * Mathf.PI * 2 / 5;
                        Vector3 hub = new Vector3(side * 0.78f, wheelY, axle);
                        g.Beam(hub, hub + new Vector3(0, Mathf.Sin(angle), Mathf.Cos(angle)) * 0.145f, 0.034f, Steel);
                        g.Cylinder(hub + new Vector3(0, Mathf.Sin(angle), Mathf.Cos(angle)) * 0.065f, 0.014f, 0.008f, Steel, Quaternion.identity, 8);
                    }
                    g.Cylinder(new Vector3(side * 0.785f, wheelY, axle), 0.045f, 0.014f, Steel, Quaternion.identity, 16);
                    for (int step = 0; step < 16; step++)
                    {
                        Vector3 Arc(int index, float radius)
                        {
                            float angle = (-90 + index * 180f / 16) * Mathf.Deg2Rad;
                            return new Vector3(side * 0.741f, wheelY + Mathf.Cos(angle) * radius, axle + Mathf.Sin(angle) * radius);
                        }
                        g.Quad(Arc(step, 0.345f), Arc(step + 1, 0.345f), Arc(step + 1, 0.374f), Arc(step, 0.374f), Trim, Vector3.right * side);
                    }
                }
                // Raised roof strengthening ribs, flush enough to stay within the original hitbox.
                g.Box(new Vector3(side * 0.30f, 1.826f - (style == 2 ? 0.17f : 0), 0.39f), new Vector3(0.025f, 0.018f, 1.87f), Paint, 0.006f);
                if (style == 1)
                    foreach (float z in new[] { 0.38f, 1.04f })
                    {
                        g.Box(new Vector3(side * 0.747f, 1.36f, z), new Vector3(0.015f, 0.48f, 0.57f), Trim, 0.012f);
                        g.Box(new Vector3(side * 0.758f, 1.37f, z), new Vector3(0.012f, 0.42f, 0.51f), Glass, 0.009f);
                    }
                Vector3[] rearGlass = { new Vector3(side * 0.07f, 1.20f, 1.578f), new Vector3(side * 0.54f, 1.20f, 1.578f),
                    new Vector3(side * 0.54f, style == 2 ? 1.39f : 1.56f, 1.578f), new Vector3(side * 0.07f, style == 2 ? 1.39f : 1.56f, 1.578f) };
                g.Quad(rearGlass[0], rearGlass[1], rearGlass[2], rearGlass[3], Glass, Vector3.forward);
            }
            g.Box(new Vector3(0, 0.46f, -1.60f), new Vector3(1.48f, 0.19f, 0.19f), Trim, 0.045f);
            g.Box(new Vector3(0, 0.46f, 1.59f), new Vector3(1.46f, 0.18f, 0.16f), Trim, 0.04f);
            g.Box(new Vector3(0, 0.69f, -1.587f), new Vector3(0.64f, 0.27f, 0.025f), Trim, 0.025f);
            for (int slat = 0; slat < 4; slat++)
                g.Box(new Vector3(0, 0.60f + slat * 0.056f, -1.608f), new Vector3(0.56f, 0.016f, 0.016f), Steel, 0.005f);
            g.Box(new Vector3(0, 0.48f, -1.705f + 0.012f), new Vector3(0.28f, 0.075f, 0.008f), Lamp, 0.008f);
            for (int mark = 0; mark < 7; mark++)
                g.Box(new Vector3(-0.105f + mark * 0.034f, 0.48f, -1.698f), new Vector3(0.012f, 0.038f, 0.003f), Trim, 0);
            g.Beam(new Vector3(0, 0.56f, 1.582f), new Vector3(0, style == 2 ? 1.52f : 1.69f, 1.582f), 0.012f, Trim);
            g.Box(new Vector3(0.07f, 0.99f, 1.594f), new Vector3(0.14f, 0.035f, 0.022f), Steel, 0.007f);
            g.Cylinder(new Vector3(Width(1.48f) + 0.006f, 0.94f, 1.48f), 0.073f, 0.012f, Trim, Quaternion.identity, 16);
            g.Cylinder(new Vector3(Width(1.48f) + 0.015f, 0.94f, 1.48f), 0.061f, 0.012f, Paint, Quaternion.identity, 16);
            g.Box(new Vector3(0, 0.70f, 1.59f), new Vector3(0.32f, 0.10f, 0.014f), Trim, 0.012f);
            g.Box(new Vector3(0, 0.70f, 1.601f), new Vector3(0.28f, 0.073f, 0.008f), Lamp, 0.008f);
            for (int mark = 0; mark < 7; mark++)
                g.Box(new Vector3(-0.105f + mark * 0.034f, 0.70f, 1.608f), new Vector3(0.012f, 0.038f, 0.003f), Trim, 0);
            if (style == 2)
            {
                foreach (float z in new[] { -0.37f, 0.99f })
                {
                    g.Box(new Vector3(0, 1.69f, z), new Vector3(1.20f, 0.08f, 0.075f), Trim, 0.015f);
                    g.Box(new Vector3(0, 1.75f, z), new Vector3(1.35f, 0.045f, 0.065f), Steel, 0.012f);
                }
                foreach (float x in new[] { -0.36f, 0.36f })
                    g.Box(new Vector3(x, 1.80f, 0.30f), new Vector3(0.045f, 0.045f, 2.02f), Steel, 0.01f);
                for (int rung = 0; rung < 9; rung++)
                    g.Box(new Vector3(0, 1.80f, -0.58f + rung * 0.22f), new Vector3(0.70f, 0.028f, 0.035f), Steel, 0.006f);
            }
            return g.Finish(style == 1 ? "Passenger Shuttle" : style == 2 ? "Service Van" : "Panel Van");
        }

        private static float Sample(float[] positions, float[] values, float point)
        {
            for (int i = 1; i < positions.Length; i++) if (point <= positions[i]) return Mathf.Lerp(values[i - 1], values[i], Mathf.InverseLerp(positions[i - 1], positions[i], point));
            return values[values.Length - 1];
        }
        private static Vector2[] RectangleRing(float x, float z, float bevel) => new[] {
            new Vector2(-x + bevel, -z), new Vector2(x - bevel, -z), new Vector2(x, -z + bevel), new Vector2(x, z - bevel),
            new Vector2(x - bevel, z), new Vector2(-x + bevel, z), new Vector2(-x, z - bevel), new Vector2(-x, -z + bevel) };
        private static List<Vector2> ClipX(List<Vector2> polygon, float edge, bool minimum)
        {
            var result = new List<Vector2>();
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 a = polygon[i], b = polygon[(i + 1) % polygon.Count];
                bool insideA = minimum ? a.x >= edge : a.x <= edge, insideB = minimum ? b.x >= edge : b.x <= edge;
                if (insideA) result.Add(a);
                if (insideA != insideB) result.Add(Vector2.Lerp(a, b, (edge - a.x) / (b.x - a.x)));
            }
            return result;
        }

        private static Texture2D Texture(string surface, string map) => AssetDatabase.LoadAssetAtPath<Texture2D>(ArtPath + "/Textures/" + surface + " " + map + ".png");
        private static float Noise(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(((x + TextureSize) % TextureSize) * 374761393 + ((y + TextureSize) % TextureSize) * 668265263 + seed * 1274126177);
                h = (h ^ (h >> 13)) * 1274126177; return (h ^ (h >> 16)) / (float)uint.MaxValue;
            }
        }
        private static float SmoothTileNoise(int x, int y, int cell, int seed)
        {
            int a = x / cell * cell, b = y / cell * cell;
            float u = Mathf.SmoothStep(0, 1, (x - a) / (float)cell), v = Mathf.SmoothStep(0, 1, (y - b) / (float)cell);
            return Mathf.Lerp(Mathf.Lerp(Noise(a, b, seed), Noise(a + cell, b, seed), u),
                Mathf.Lerp(Noise(a, b + cell, seed), Noise(a + cell, b + cell, seed), u), v);
        }
        private static void BakeTextures(string surface)
        {
            int n = TextureSize; var albedo = new Color32[n * n]; var packed = new Color32[n * n]; var height = new float[n * n];
            int seed = Array.IndexOf(new[] { "Paint", "Concrete", "Rubber", "Steel", "Glass", "Reflector", "Galvanized" }, surface) * 79 + 13;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float grain = Noise(x, y, seed), value = 0.94f + grain * 0.055f, h = grain * 0.04f, gloss = 0.88f + grain * 0.12f, ao = 1, metal = 1;
                    switch (surface)
                    {
                        case "Concrete":
                            float pore = Noise(x / 2, y / 2, seed + 4) > 0.96f ? 0.23f : 0;
                            value = 0.70f + grain * 0.23f - pore; h = grain * 0.20f - pore; ao = 0.86f + value * 0.14f; gloss = 0.65f + grain * 0.22f; break;
                        case "Rubber":
                            bool cut = (x + (y / 32 % 2 == 0 ? y : -y) / 4 + n) % 32 < 4 || y % 64 < 5;
                            value = (cut ? 0.34f : 0.78f) + grain * 0.14f; h = cut ? 0.1f : 0.65f + grain * 0.06f; ao = cut ? 0.65f : 1; gloss = cut ? 0.4f : 0.8f; break;
                        case "Steel":
                            value = 0.78f + Noise(0, y, seed) * 0.13f + grain * 0.06f; h = Noise(0, y, seed) * 0.13f;
                            gloss = 0.74f + grain * 0.22f; break;
                        case "Galvanized":
                            float spangle = SmoothTileNoise(x, y, 8, seed + 7) * 0.65f + SmoothTileNoise(x, y, 32, seed + 9) * 0.35f;
                            value = 0.84f + spangle * 0.055f + grain * 0.025f; h = grain * 0.02f + spangle * 0.015f;
                            gloss = 0.76f + spangle * 0.12f; break;
                        case "Glass":
                            value = 0.61f + 0.30f * Mathf.Sin(y / (float)n * Mathf.PI) + grain * 0.018f; h = grain * 0.005f; gloss = 0.96f; break;
                        case "Reflector":
                            float facet = (x % 16 + y % 16) / 30f; value = 0.72f + facet * 0.26f; h = facet * 0.15f; gloss = 0.75f + facet * 0.25f; break;
                    }
                    int index = y * n + x; albedo[index] = (Color32)new Color(value, value, value, 1);
                    packed[index] = (Color32)new Color(ao, gloss, metal, 1); height[index] = h;
                }
            var normal = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float At(int a, int b) => height[((b + n) % n) * n + (a + n) % n];
                    Vector3 direction = new Vector3((At(x - 1, y) - At(x + 1, y)) * 2, (At(x, y - 1) - At(x, y + 1)) * 2, 1).normalized;
                    normal[y * n + x] = (Color32)new Color(direction.x * 0.5f + 0.5f, direction.y * 0.5f + 0.5f, direction.z * 0.5f + 0.5f, 1);
                }
            WriteTexture(surface, "Albedo", albedo); WriteTexture(surface, "Normal", normal); WriteTexture(surface, "Surface", packed);
        }
        private static void WriteTexture(string surface, string map, Color32[] pixels)
        {
            string path = ArtPath + "/Textures/" + surface + " " + map + ".png";
            var image = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false); image.SetPixels32(pixels); image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG()); Object.DestroyImmediate(image);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = map == "Normal" ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = map == "Albedo"; importer.mipmapEnabled = true; importer.isReadable = false;
            importer.wrapMode = TextureWrapMode.Repeat; importer.filterMode = FilterMode.Trilinear; importer.anisoLevel = 4;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
        }
        private static void Folder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
        }
        private static T Save<T>(T asset, string path) where T : Object
        {
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null) { AssetDatabase.CreateAsset(asset, path); return asset; }
            EditorUtility.CopySerialized(asset, existing); EditorUtility.SetDirty(existing); Object.DestroyImmediate(asset); return existing;
        }

        private sealed class Geometry
        {
            private readonly List<Vector3> vertices = new List<Vector3>(), normals = new List<Vector3>();
            private readonly List<Vector2> uv = new List<Vector2>();
            private readonly List<Color32> colors = new List<Color32>();
            private readonly List<int>[] triangles = Enumerable.Range(0, MaterialCount).Select(_ => new List<int>()).ToArray();
            // Normalize the edges first: tiny bevels otherwise fall below Vector3's area-normal epsilon.
            private static Vector3 FaceNormal(Vector3 a, Vector3 b, Vector3 c) => Vector3.Cross((b - a).normalized, (c - a).normalized).normalized;
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, int material, Vector3 outward, float shade = 1)
            {
                Vector3 normal = FaceNormal(a, b, c);
                bool reverse = Vector3.Dot(normal, outward) < 0;
                if (reverse) normal = -normal;
                int start = vertices.Count;
                vertices.AddRange(new[] { a, b, c, d }); normals.AddRange(Enumerable.Repeat(normal, 4));
                colors.AddRange(Enumerable.Repeat((Color32)new Color(shade, shade, shade, 1), 4));
                float u = Mathf.Max(0.01f, Vector3.Distance(a, b) * 2), v = Mathf.Max(0.01f, Vector3.Distance(a, d) * 2);
                uv.AddRange(new[] { Vector2.zero, new Vector2(u, 0), new Vector2(u, v), new Vector2(0, v) });
                triangles[material].AddRange(reverse ? new[] { start, start + 2, start + 1, start, start + 3, start + 2 }
                    : new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }
            private void Triangle(Vector3 a, Vector3 b, Vector3 c, int material, Vector3 outward)
            {
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0) { Vector3 swap = b; b = c; c = swap; }
                int start = vertices.Count; vertices.AddRange(new[] { a, b, c });
                normals.AddRange(Enumerable.Repeat(FaceNormal(a, b, c), 3));
                colors.AddRange(Enumerable.Repeat(new Color32(255, 255, 255, 255), 3));
                uv.AddRange(new[] { Vector2.zero, Vector2.right * Vector3.Distance(a, b) * 2, Vector2.up * Vector3.Distance(a, c) * 2 });
                triangles[material].AddRange(new[] { start, start + 1, start + 2 });
            }
            public void Cap(Vector3[] polygon, int material, Vector3 outward)
            {
                Vector3 center = polygon.Aggregate(Vector3.zero, (sum, point) => sum + point) / polygon.Length;
                for (int i = 0; i < polygon.Length; i++) Triangle(center, polygon[i], polygon[(i + 1) % polygon.Length], material, outward);
            }
            public void Box(Vector3 center, Vector3 size, int material, float bevel, Quaternion? rotation = null)
            {
                Quaternion q = rotation ?? Quaternion.identity;
                Vector3 half = size * 0.5f; bevel = Mathf.Min(bevel, Mathf.Min(half.x, Mathf.Min(half.y, half.z)) * 0.8f);
                Vector3 inner = half - Vector3.one * bevel;
                Vector3 World(Vector3 p) => center + q * p;
                Vector3 Axis(int axis) => axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward;
                for (int axis = 0; axis < 3; axis++)
                    for (int sign = -1; sign <= 1; sign += 2)
                    {
                        int b = (axis + 1) % 3, c = (axis + 2) % 3;
                        Vector3 n = Axis(axis) * sign, origin = n * half[axis];
                        Vector3 u = Axis(b) * inner[b], v = Axis(c) * inner[c];
                        Quad(World(origin - u - v), World(origin + u - v), World(origin + u + v), World(origin - u + v), material, q * n);
                    }
                if (bevel <= 0) return;
                for (int axis = 0; axis < 3; axis++)
                    for (int sb = -1; sb <= 1; sb += 2)
                        for (int sc = -1; sc <= 1; sc += 2)
                        {
                            int b = (axis + 1) % 3, c = (axis + 2) % 3;
                            Vector3 along = Axis(axis) * inner[axis];
                            Vector3 first = Axis(b) * half[b] * sb + Axis(c) * inner[c] * sc;
                            Vector3 next = Axis(b) * inner[b] * sb + Axis(c) * half[c] * sc;
                            Quad(World(first - along), World(first + along), World(next + along), World(next - along), material, q * (Axis(b) * sb + Axis(c) * sc));
                        }
                for (int sx = -1; sx <= 1; sx += 2)
                    for (int sy = -1; sy <= 1; sy += 2)
                        for (int sz = -1; sz <= 1; sz += 2)
                        {
                            Vector3 p = Vector3.Scale(inner, new Vector3(sx, sy, sz));
                            Triangle(World(p + Vector3.right * sx * bevel), World(p + Vector3.up * sy * bevel), World(p + Vector3.forward * sz * bevel), material, q * new Vector3(sx, sy, sz));
                        }
            }
            public void Beam(Vector3 a, Vector3 b, float width, int material)
                => Box((a + b) * 0.5f, new Vector3(width, Vector3.Distance(a, b), width), material, width * 0.2f, Quaternion.FromToRotation(Vector3.up, (b - a).normalized));
            public void Cylinder(Vector3 center, float radius, float width, int material, Quaternion rotation, int sides)
            {
                Vector3 Point(float x, int side) => center + rotation * new Vector3(x, Mathf.Cos(side * Mathf.PI * 2 / sides) * radius, Mathf.Sin(side * Mathf.PI * 2 / sides) * radius);
                for (int side = 0; side < sides; side++)
                {
                    Quad(Point(-width * 0.5f, side), Point(width * 0.5f, side), Point(width * 0.5f, side + 1), Point(-width * 0.5f, side + 1), material, Point(0, side) - center);
                    Triangle(center + rotation * Vector3.left * width * 0.5f, Point(-width * 0.5f, side), Point(-width * 0.5f, side + 1), material, rotation * Vector3.left);
                    Triangle(center + rotation * Vector3.right * width * 0.5f, Point(width * 0.5f, side), Point(width * 0.5f, side + 1), material, rotation * Vector3.right);
                }
            }
            public void Tire(Vector3 center)
            {
                float[] xs = { -0.10f, -0.08f, 0.08f, 0.10f }, radii = { 0.25f, 0.30f, 0.30f, 0.25f };
                Vector3 Point(int ring, int step) => center + new Vector3(xs[ring], Mathf.Cos(step * Mathf.PI * 2 / 24) * radii[ring], Mathf.Sin(step * Mathf.PI * 2 / 24) * radii[ring]);
                for (int step = 0; step < 24; step++)
                    for (int ring = 0; ring < xs.Length - 1; ring++)
                        Quad(Point(ring, step), Point(ring + 1, step), Point(ring + 1, step + 1), Point(ring, step + 1), Rubber,
                            Point(ring, step) - center);
                Cylinder(center, 0.25f, 0.196f, Rubber, Quaternion.identity, 24);
            }
            public Mesh Finish(string name)
            {
                var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
                mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uv); mesh.SetColors(colors); mesh.subMeshCount = MaterialCount;
                for (int i = 0; i < triangles.Length; i++) mesh.SetTriangles(triangles[i], i, false);
                mesh.RecalculateTangents(); mesh.RecalculateBounds(); return mesh;
            }
        }
    }
}

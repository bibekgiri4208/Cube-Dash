using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace CubeDash.Editor
{
    /// <summary>Separates the supplied traffic geometry offline. Never modifies the original models or scenes.</summary>
    public static class ObstacleBreakupBuilder
    {
        public const string ArtPath = HighwayHazardArtBuilder.ArtPath + "/Breakup Parts";
        private const int Paint = 0, Concrete = 1, Rubber = 2, Steel = 3, Glass = 4, Trim = 5;

        [MenuItem("Tools/Cube Dash/Bake Model-shaped Truck Smash Parts")]
        public static void ApplyFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode before baking fragments.");
            var art = Resources.Load<HighwayHazardVisuals>(HighwayHazardVisuals.ResourcePath);
            if (art == null) throw new InvalidOperationException("Bake highway obstacle models first.");
            if (!AssetDatabase.IsValidFolder(ArtPath)) AssetDatabase.CreateFolder(HighwayHazardArtBuilder.ArtPath, "Breakup Parts");
            var models = new List<ObstacleBreakupCatalog.Breakup>();
            foreach (HighwayHazard type in Enum.GetValues(typeof(HighwayHazard)))
                for (int variant = 0; variant < art.VariantCount(type); variant++)
                {
                    Mesh source = art.MeshFor(type, variant);
                    bool vehicle = type == HighwayHazard.Van || type == HighwayHazard.Truck;
                    bool concrete = source.name == "Reinforced Road Block";
                    bool frame = source.name.Contains("Barricade");
                    List<Face> faces = Read(source);
                    var parts = new List<ObstacleBreakupCatalog.Fragment>();
                    if (vehicle) Vehicle(source, faces, parts);
                    else if (frame) Frame(source, faces, parts);
                    else
                    {
                        // Slanted fracture planes give concrete wedges, rather than eight rectangular mini-blocks.
                        Vector3 across = concrete ? new Vector3(1, 0.16f, 0.08f).normalized : Vector3.right;
                        Vector3 up = concrete ? new Vector3(0.20f, 1, -0.15f).normalized : Vector3.up;
                        if (concrete)
                            for (int x = 0; x < 4; x++)
                                for (int y = 0; y < 2; y++)
                                {
                                    var cell = x == 0 ? faces : Clip(faces, across, -0.9f + x * 0.45f, true, true);
                                    if (x < 3) cell = Clip(cell, across, -0.9f + (x + 1) * 0.45f, false, true);
                                    cell = Clip(cell, up, 0.54f, y == 1, true);
                                    Add(source, cell, parts, ObstacleFragmentRole.Concrete, false);
                                }
                        else
                            for (int x = 0; x < 2; x++)
                                for (int z = 0; z < 2; z++)
                                    for (int y = 0; y < 2; y++)
                                    {
                                        var cell = Clip(faces, across, 0, x == 1);
                                        cell = Clip(cell, Vector3.forward, 0, z == 1);
                                        cell = Clip(cell, up, source.bounds.max.y * 0.51f, y == 1);
                                        Add(source, cell, parts, ObstacleFragmentRole.Plastic, true);
                                    }
                    }
                    Mesh alias = AssetDatabase.LoadAssetAtPath<Mesh>(HighwayObstacleEditorVisuals.PreviewPath + "/" + source.name + " (saved slot).asset");
                    models.Add(new ObstacleBreakupCatalog.Breakup { source = source, aliases = alias != null ? new[] { alias } : new Mesh[0], fragments = parts.ToArray() });
                }
            var data = ScriptableObject.CreateInstance<ObstacleBreakupCatalog>();
            var serialized = new SerializedObject(data);
            var list = serialized.FindProperty("models"); list.arraySize = models.Count;
            for (int i = 0; i < models.Count; i++)
            {
                var model = list.GetArrayElementAtIndex(i); var value = models[i];
                model.FindPropertyRelative("source").objectReferenceValue = value.source;
                var aliases = model.FindPropertyRelative("aliases"); aliases.arraySize = value.aliases.Length;
                for (int j = 0; j < value.aliases.Length; j++) aliases.GetArrayElementAtIndex(j).objectReferenceValue = value.aliases[j];
                var fragments = model.FindPropertyRelative("fragments"); fragments.arraySize = value.fragments.Length;
                for (int j = 0; j < value.fragments.Length; j++)
                {
                    var fragment = fragments.GetArrayElementAtIndex(j);
                    fragment.FindPropertyRelative("mesh").objectReferenceValue = value.fragments[j].mesh;
                    fragment.FindPropertyRelative("center").vector3Value = value.fragments[j].center;
                    fragment.FindPropertyRelative("role").enumValueIndex = (int)value.fragments[j].role;
                }
            }
            var materials = serialized.FindProperty("materials"); materials.arraySize = art.Materials.Length;
            for (int i = 0; i < art.Materials.Length; i++) materials.GetArrayElementAtIndex(i).objectReferenceValue = art.Materials[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Save(data, "Assets/Resources/ObstacleBreakups.asset"); AssetDatabase.SaveAssets();
            Debug.Log("Baked model-shaped Truck smash parts for " + models.Count + " obstacles; original art, scenes and gameplay untouched.");
        }

        private static void Vehicle(Mesh source, List<Face> faces, List<ObstacleBreakupCatalog.Fragment> parts)
        {
            bool box = source.name == "Delivery Box Truck";
            Vector3 scale = source.name == "Long Wheelbase Delivery Van" ? new Vector3(1.05f, 1.20f, 1.345f) : Vector3.one;
            var wheels = Enumerable.Range(0, 4).Select(_ => new List<Face>()).ToArray();
            var glass = new List<Face>(); var body = new List<Face>();
            foreach (Face face in faces)
            {
                Vector3 p = face.Center; p = new Vector3(p.x / scale.x, p.y / scale.y, p.z / scale.z);
                float axle = p.z < 0 ? (box ? -1.65f : -1.04f) : (box ? 1.42f : 1.02f);
                // Keep each round tire and its alloy hub together. Do not scoop up the bumpers/chassis.
                bool wheel = (face.material == Rubber || face.material == Steel || face.material == Trim)
                    && Mathf.Abs(p.x) > (box ? 0.59f : 0.54f) && p.y < 0.605f
                    && new Vector2(p.y - 0.30f, p.z - axle).magnitude < 0.31f;
                if (wheel) wheels[(p.x > 0 ? 2 : 0) + (p.z > 0 ? 1 : 0)].Add(face);
                else if (face.material == Glass) glass.Add(face);
                else body.Add(face);
            }
            foreach (var wheel in wheels) Add(source, wheel, parts, ObstacleFragmentRole.Wheel, false);
            float glassSplit = (glass.Min(f => Mathf.Min(f.a.p.y, Mathf.Min(f.b.p.y, f.c.p.y))) + glass.Max(f => Mathf.Max(f.a.p.y, Mathf.Max(f.b.p.y, f.c.p.y)))) * 0.5f;
            for (int side = 0; side < 2; side++)
                for (int end = 0; end < 2; end++)
                {
                    var cell = Clip(glass, Vector3.right, 0, side == 1);
                    Add(source, Clip(cell, Vector3.up, glassSplit, end == 1), parts, ObstacleFragmentRole.Glass, true);
                }
            float front = box ? -0.80f : -0.16f * scale.z;
            float rear = box ? 0.85f : 0.80f * scale.z;
            for (int side = 0; side < 2; side++)
                for (int panel = 0; panel < 3; panel++)
                {
                    var cell = Clip(body, Vector3.right, 0, side == 1);
                    if (panel > 0) cell = Clip(cell, Vector3.forward, panel == 1 ? front : rear, true);
                    if (panel < 2) cell = Clip(cell, Vector3.forward, panel == 0 ? front : rear, false);
                    Add(source, cell, parts, ObstacleFragmentRole.BodyPanel, true);
                }
        }

        private static void Frame(Mesh source, List<Face> faces, List<ObstacleBreakupCatalog.Fragment> parts)
        {
            // Cross rails break into four recognisable striped boards; feet/A-frames remain long metal assemblies.
            var rails = faces.Where(face => face.material == Paint || face.material == 7).ToList();
            var supports = faces.Where(face => face.material != Paint && face.material != 7).ToList();
            for (int x = 0; x < 2; x++)
                for (int y = 0; y < 2; y++)
                {
                    var cell = Clip(rails, Vector3.right, 0, x == 1);
                    cell = Clip(cell, Vector3.up, 0.60f, y == 1);
                    Add(source, cell, parts, ObstacleFragmentRole.Plastic, true);
                }
            for (int x = 0; x < 2; x++)
                for (int z = 0; z < 2; z++)
                {
                    var cell = Clip(supports, Vector3.right, 0, x == 1);
                    cell = Clip(cell, Vector3.forward, 0, z == 1);
                    Add(source, cell, parts, ObstacleFragmentRole.Frame, true);
                }
        }

        private struct Vertex
        {
            public Vector3 p, n; public Vector2 uv; public Color color;
            public static Vertex Lerp(Vertex a, Vertex b, float t) => new Vertex { p = Vector3.Lerp(a.p, b.p, t), n = Vector3.Lerp(a.n, b.n, t).normalized, uv = Vector2.Lerp(a.uv, b.uv, t), color = Color.Lerp(a.color, b.color, t) };
        }
        private struct Face
        {
            public Vertex a, b, c; public int material;
            public Vector3 Center => (a.p + b.p + c.p) / 3;
        }
        private static List<Face> Read(Mesh mesh)
        {
            var faces = new List<Face>(); var vertices = mesh.vertices; var normals = mesh.normals; var uv = mesh.uv; var colors = mesh.colors;
            Vertex V(int i) => new Vertex { p = vertices[i], n = normals[i], uv = uv[i], color = colors.Length > i ? colors[i] : Color.white };
            for (int m = 0; m < mesh.subMeshCount; m++)
            {
                var indices = mesh.GetTriangles(m);
                for (int i = 0; i < indices.Length; i += 3) faces.Add(new Face { a = V(indices[i]), b = V(indices[i + 1]), c = V(indices[i + 2]), material = m });
            }
            return faces;
        }
        private static List<Face> Clip(List<Face> faces, Vector3 normal, float offset, bool minimum, bool cap = false)
        {
            var output = new List<Face>(); var cuts = new List<Vector3>();
            foreach (Face face in faces)
            {
                var input = new[] { face.a, face.b, face.c }; var polygon = new List<Vertex>();
                for (int i = 0; i < 3; i++)
                {
                    Vertex a = input[i], b = input[(i + 1) % 3];
                    float da = Vector3.Dot(a.p, normal) - offset, db = Vector3.Dot(b.p, normal) - offset;
                    bool insideA = minimum ? da >= -1e-6f : da <= 1e-6f, insideB = minimum ? db >= -1e-6f : db <= 1e-6f;
                    if (insideA) polygon.Add(a);
                    if (insideA != insideB)
                    {
                        Vertex v = Vertex.Lerp(a, b, da / (da - db)); polygon.Add(v);
                        if (cap && face.material == Concrete && !cuts.Any(p => (p - v.p).sqrMagnitude < 1e-8f)) cuts.Add(v.p);
                    }
                }
                for (int i = 1; i < polygon.Count - 1; i++) Emit(output, polygon[0], polygon[i], polygon[i + 1], face.material);
            }
            if (cap && cuts.Count >= 3)
            {
                Vector3 center = cuts.Aggregate(Vector3.zero, (sum, p) => sum + p) / cuts.Count;
                Vector3 u = Vector3.Cross(normal, Mathf.Abs(normal.y) > 0.8f ? Vector3.forward : Vector3.up).normalized;
                Vector3 v = Vector3.Cross(normal, u); Vector3 outward = minimum ? -normal : normal;
                cuts = cuts.OrderBy(p => Mathf.Atan2(Vector3.Dot(p - center, v), Vector3.Dot(p - center, u))).ToList();
                Vertex V(Vector3 p) => new Vertex { p = p, n = outward, uv = new Vector2(Vector3.Dot(p, u), Vector3.Dot(p, v)) * 2, color = new Color(0.82f, 0.82f, 0.82f, 1) };
                for (int i = 0; i < cuts.Count; i++)
                {
                    Vertex a = V(cuts[i]), b = V(cuts[(i + 1) % cuts.Count]);
                    if (Vector3.Dot(Vector3.Cross(a.p - center, b.p - center), outward) < 0) { var swap = a; a = b; b = swap; }
                    Emit(output, V(center), a, b, Concrete);
                }
            }
            return output;
        }
        private static void Emit(List<Face> faces, Vertex a, Vertex b, Vertex c, int material)
        {
            if (Vector3.Cross(b.p - a.p, c.p - a.p).sqrMagnitude > 1e-12f) faces.Add(new Face { a = a, b = b, c = c, material = material });
        }
        private static void Add(Mesh source, List<Face> faces, List<ObstacleBreakupCatalog.Fragment> parts, ObstacleFragmentRole role, bool shell)
        {
            if (faces.Count == 0) throw new InvalidOperationException("Empty breakup region: " + source.name);
            Vector3 min = faces[0].a.p, max = min;
            foreach (Face face in faces) foreach (Vertex vertex in new[] { face.a, face.b, face.c }) { min = Vector3.Min(min, vertex.p); max = Vector3.Max(max, vertex.p); }
            Vector3 center = (min + max) * 0.5f;
            var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var uv = new List<Vector2>(); var colors = new List<Color>();
            var triangles = Enumerable.Range(0, 9).Select(_ => new List<int>()).ToArray();
            void Triangle(Vertex a, Vertex b, Vertex c, int material)
            {
                int start = vertices.Count;
                foreach (Vertex vertex in new[] { a, b, c }) { vertices.Add(vertex.p - center); normals.Add(vertex.n); uv.Add(vertex.uv); colors.Add(vertex.color); }
                triangles[material].AddRange(new[] { start, start + 1, start + 2 });
            }
            foreach (Face face in faces)
            {
                Triangle(face.a, face.b, face.c, face.material);
                // Thin inner skins make detached curved plastic, windows and metal panels visible from both sides.
                if (shell)
                {
                    Vertex Inner(Vertex vertex) { vertex.p -= vertex.n * (role == ObstacleFragmentRole.Glass ? 0.003f : 0.012f); vertex.n = -vertex.n; vertex.color *= 0.8f; return vertex; }
                    Triangle(Inner(face.c), Inner(face.b), Inner(face.a), face.material);
                }
            }
            var mesh = new Mesh { name = source.name + " - " + parts.Count.ToString("00") + " " + role, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uv); mesh.SetColors(colors); mesh.subMeshCount = 9;
            for (int i = 0; i < 9; i++) mesh.SetTriangles(triangles[i], i, false);
            mesh.RecalculateBounds(); mesh.RecalculateTangents();
            parts.Add(new ObstacleBreakupCatalog.Fragment { mesh = Save(mesh, ArtPath + "/" + mesh.name + ".asset"), center = center, role = role });
        }
        private static T Save<T>(T value, string path) where T : Object
        {
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null) { AssetDatabase.CreateAsset(value, path); return value; }
            EditorUtility.CopySerialized(value, existing); EditorUtility.SetDirty(existing); Object.DestroyImmediate(value); return existing;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CubeDash.Editor
{
    /// <summary>Repairs only the standalone Jungle's baked scenery; preserves existing planting and source packs.</summary>
    public static class JungleSceneryRepair
    {
        public const string StonePath = "Assets/3D Models/Sticks-and-stones/";
        private const string Art = JungleNatureBuilder.ArtPath;

        [MenuItem("Tools/Cube Dash/Fix Jungle Bamboo and Ground Stones")]
        public static void ApplyFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) ApplyFromCommandLine();
        }

        public static void ApplyFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene edits before repairing Jungle.");
            Scene scene = SceneManager.GetSceneByPath(JungleNatureBuilder.ScenePath);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(JungleNatureBuilder.ScenePath);
            var track = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<EndlessTrack>(true)).Single();
            var roots = track.Segments.Select(segment => segment.Environment.Biomes[(int)EnvironmentBiome.Jungle]).ToArray();
            var scenery = roots[0].GetComponent<BiomeScenery>();
            if (scenery.Layouts.Any(mesh => !AssetDatabase.GetAssetPath(mesh).StartsWith(Art + "/", StringComparison.Ordinal)))
                throw new InvalidOperationException("Expected the scene-local Idyllic Jungle layouts.");
            Material[] palette = roots[0].transform.Find("Landscape Details").GetComponent<Renderer>().sharedMaterials;
            int[] stoneSurfaces = Enumerable.Range(0, palette.Length).Where(i => palette[i].name.Contains("Stone_Medium")).ToArray();
            if (stoneSurfaces.Length != 3) throw new InvalidOperationException("Expected three scene-local stone surfaces.");
            var rocks = AssetDatabase.FindAssets("t:Prefab", new[] { StonePath + "source" })
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid)).OrderBy(path => path, StringComparer.Ordinal)
                .Select(LoadStone).ToArray();
            if (rocks.Length == 0) throw new InvalidOperationException("Missing Sticks-and-stones prefabs.");
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(StonePath + "textures/internal_ground_ao_texture.jpeg");
            if (texture == null) throw new InvalidOperationException("Missing stone texture.");
            Color tone = palette[3].GetColor("_BaseColor");
            for (int i = 0; i < stoneSurfaces.Length; i++)
            {
                Material material = palette[stoneSurfaces[i]];
                // Retain the jungle's subdued grey-green stone family, not the pack's default finish.
                // The new pack's nearly-white AO map needs a darker tint than the old grey albedo.
                Color color = Color.Lerp(tone, new Color(tone.g * 0.91f, tone.g, tone.g * 0.87f, 1), 0.35f) * (0.60f + i * 0.03f);
                color.a = 1;
                material.SetTexture("_BaseMap", texture);
                Color top = color * 1.04f; top.a = 1;
                material.SetColor("_BaseColor", color); material.SetColor("_TopColor", top);
                material.SetFloat("_WindStrength", 0); material.SetFloat("_Foliage", 0);
                material.SetFloat("_Cutoff", 0); material.SetFloat("_Smoothness", 0.04f);
                EditorUtility.SetDirty(material);
            }
            Material[] varietyPalette = roots[0].transform.Find("Landscape Variety").GetComponent<Renderer>().sharedMaterials.Take(15)
                .Concat(new[] { BambooMaterial("Culm", new Color(0.16f, 0.26f, 0.105f), false),
                    BambooMaterial("Nodes", new Color(0.22f, 0.31f, 0.14f), false),
                    BambooMaterial("Leaves", new Color(0.075f, 0.23f, 0.115f), true) }).ToArray();
            var details = new Mesh[8]; var understory = new Mesh[8];
            try
            {
                for (int layout = 0; layout < details.Length; layout++)
                {
                    Mesh old = scenery.Layouts[layout];
                    var terrain = new Ground(old);
                    var mesh = new Geometry(palette.Length);
                    for (int surface = 0; surface < old.subMeshCount; surface++)
                        if (!stoneSurfaces.Contains(surface)) mesh.Copy(old, surface, surface);
                    List<Bounds> trunks = TrunkFootprints(old, palette);
                    var occupied = new List<Bounds>(trunks);
                    var random = new System.Random(98641 + layout * 179);
                    float bambooX = layout % 2 == 0 ? 12.5f : -13;
                    for (int side = -1; side <= 1; side += 2)
                        for (int rock = 0; rock < 5; rock++)
                        {
                            Mesh model = rocks[(layout * 5 + rock + (side > 0 ? 3 : 0)) % rocks.Length];
                            bool placed = false;
                            for (int attempt = 0; attempt < 120 && !placed; attempt++)
                            {
                                float x = side * Range(random, 15.5f, 24), z = Range(random, 5, 37);
                                float scale = Range(random, 2.8f, 4.3f) / Mathf.Max(model.bounds.size.x, model.bounds.size.z);
                                var matrix = Matrix4x4.TRS(new Vector3(x, 0, z), Quaternion.Euler(0, Range(random, 0, 360), 0), Vector3.one * scale);
                                Vector3[] points = model.vertices.Select(matrix.MultiplyPoint3x4).ToArray();
                                Bounds footprint = BoundsOf(points);
                                footprint.Expand(new Vector3(1.5f, 0, 1.5f));
                                if (occupied.Any(bounds => Overlaps(footprint, bounds))) continue;
                                if (Overlaps(footprint, new Bounds(new Vector3(bambooX, 0, 8), new Vector3(6, 1, 8))) ||
                                    Overlaps(footprint, new Bounds(new Vector3(13.5f, 0, 33), new Vector3(7, 1, 5)))) continue;
                                float height = model.bounds.size.y * scale;
                                // Fit the entire bottom skirt, not the imported pivot or a single centre sample.
                                Vector3[] skirt = points.Where(point => point.y <= height * 0.22f).ToArray();
                                float[] ground = skirt.Select(point => terrain.Height(point.x, point.z)).ToArray();
                                float low = ground.Min(), high = ground.Max();
                                if (high - low > Mathf.Min(0.65f, height * 0.45f)) continue;
                                float y = skirt.Select((point, index) => ground[index] - point.y).Min() - 0.09f;
                                if (terrain.Height(x, z) - y > height * 0.5f) continue;
                                matrix.m13 = y;
                                mesh.Add(model, 0, stoneSurfaces[rock % 3], matrix, new Vector2(x, z), model.bounds, 0);
                                occupied.Add(footprint); placed = true;
                            }
                            if (!placed) throw new InvalidOperationException($"Could not safely ground stone {rock} in Jungle layout {layout + 1}.");
                        }
                    details[layout] = Save(mesh.Bake(old.name), AssetDatabase.GetAssetPath(old));
                    // Retain saved log/gateway edits, but never accumulate previously baked bamboo.
                    var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/CubeDash/Environments/Jungle.prefab");
                    Mesh original = source.GetComponent<BiomeScenery>().VarietyLayouts[layout];
                    Mesh savedVariety = scenery.VarietyLayouts[layout];
                    var variety = new Geometry(varietyPalette.Length);
                    Vector2[] anchors = savedVariety.uv3;
                    for (int surface = 0; surface < 15; surface++)
                        if (surface != 13) variety.Copy(savedVariety, surface, surface, index =>
                        {
                            Vector2 anchor = anchors[index];
                            bool log = Mathf.Abs(anchor.x - 13.5f) < 0.01f;
                            bool gateway = layout % 3 == 0 && Mathf.Abs(anchor.x - 28) < 0.01f;
                            return log || gateway; // Remove old bamboo, banana and every umbrella-like fan palm.
                        });
                    AddBamboo(variety, terrain, bambooX, BambooZ(original, bambooX), layout);
                    understory[layout] = Save(variety.Bake("Jungle Understory " + (layout + 1)),
                        Art + "/Layouts/Jungle Understory " + (layout + 1) + ".asset");
                }
                for (int i = 0; i < roots.Length; i++)
                {
                    var item = roots[i].GetComponent<BiomeScenery>();
                    var data = new SerializedObject(item);
                    foreach (string field in new[] { "layouts", "entranceLayouts", "exitLayouts", "varietyLayouts" })
                    {
                        Mesh[] values = field == "varietyLayouts" ? understory : details;
                        var array = data.FindProperty(field); array.arraySize = values.Length;
                        for (int j = 0; j < values.Length; j++) array.GetArrayElementAtIndex(j).objectReferenceValue = values[j];
                    }
                    data.ApplyModifiedPropertiesWithoutUndo();
                    var renderer = roots[i].transform.Find("Landscape Variety").GetComponent<Renderer>();
                    renderer.sharedMaterials = varietyPalette;
                    item.SelectLayout(i - 1, track.Environment.SegmentsPerBiome, true);
                    foreach (Component component in new Component[] { item, renderer, renderer.GetComponent<MeshFilter>(),
                        roots[i].transform.Find("Landscape Details").GetComponent<MeshFilter>() })
                        if (PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                }
                AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save Jungle repairs.");
                Debug.Log("Repaired all eight Jungle layouts: segmented leafy bamboo, no fan palms, ten grounded Sticks-and-stones rocks per layout with trunk clearance. Existing planting, terrain, river and gameplay preserved.");
            }
            finally { foreach (Mesh rock in rocks) Object.DestroyImmediate(rock); }
        }

        private static float BambooZ(Mesh original, float x) => original.uv3.First(anchor => Mathf.Abs(anchor.x - x) < 0.01f).y;
        private static float Range(System.Random random, float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());
        private static bool Overlaps(Bounds a, Bounds b) => a.min.x < b.max.x && a.max.x > b.min.x && a.min.z < b.max.z && a.max.z > b.min.z;
        private static Bounds BoundsOf(IEnumerable<Vector3> points)
        {
            Vector3[] array = points.ToArray(); var bounds = new Bounds(array[0], Vector3.zero);
            foreach (Vector3 point in array) bounds.Encapsulate(point);
            return bounds;
        }

        internal static List<Bounds> TrunkFootprints(Mesh mesh, Material[] palette)
        {
            Vector3[] points = mesh.vertices; Vector2[] anchors = mesh.uv3;
            var result = new List<Bounds>();
            for (int surface = 15; surface < palette.Length; surface++)
                if (palette[surface].name.Contains("Bark"))
                    foreach (var group in mesh.GetTriangles(surface).Distinct().GroupBy(index => anchors[index]))
                    {
                        float bottom = group.Min(index => points[index].y);
                        result.Add(BoundsOf(group.Where(index => points[index].y < bottom + 3).Select(index => points[index])));
                    }
            return result;
        }

        private static Mesh LoadStone(string path)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Mesh source = prefab.GetComponent<MeshFilter>().sharedMesh;
            // The downloaded prefabs carry a -90 degree root rotation and 100x unit conversion.
            // Keep those, discard their demonstration-scene translation, then centre the actual mesh.
            Matrix4x4 orientation = Matrix4x4.TRS(Vector3.zero, prefab.transform.rotation, prefab.transform.lossyScale);
            Vector3[] points = source.vertices.Select(orientation.MultiplyPoint3x4).ToArray();
            Bounds bounds = BoundsOf(points);
            Vector3 origin = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            var mesh = Object.Instantiate(source); mesh.name = prefab.name;
            mesh.vertices = points.Select(point => point - origin).ToArray();
            mesh.normals = source.normals.Select(normal => orientation.inverse.transpose.MultiplyVector(normal).normalized).ToArray();
            mesh.RecalculateBounds(); return mesh;
        }

        private static Material BambooMaterial(string name, Color color, bool leaves)
        {
            var material = new Material(Shader.Find("CubeDash/Jungle Vegetation")) { name = "Jungle Bamboo " + name, enableInstancing = true };
            Color top = color * 1.12f; top.a = 1;
            material.SetColor("_BaseColor", color); material.SetColor("_TopColor", top);
            material.SetFloat("_Cutoff", 0); material.SetFloat("_WindStrength", leaves ? 0.11f : 0);
            material.SetFloat("_Foliage", leaves ? 1 : 0); material.SetFloat("_BushLighting", leaves ? 1 : 0);
            material.SetFloat("_LeafLightGain", 0.76f); material.SetFloat("_Smoothness", 0.06f);
            return Save(material, Art + "/Materials/" + material.name + ".mat");
        }

        private static void AddBamboo(Geometry mesh, Ground terrain, float x, float z, int layout)
        {
            var random = new System.Random(4711 + layout * 113);
            for (int stem = 0; stem < 7; stem++)
            {
                float angle = stem * 2.39996f, radius = stem == 0 ? 0 : Range(random, 0.65f, 1.55f);
                var foot = new Vector3(x + Mathf.Cos(angle) * radius, 0, z + Mathf.Sin(angle) * radius);
                foot.y = terrain.Height(foot.x, foot.z) - 0.12f;
                float height = Range(random, 6.2f, 9), thickness = Range(random, 0.075f, 0.11f);
                var lean = new Vector3(Range(random, -0.35f, 0.35f), 0, Range(random, -0.35f, 0.35f));
                Vector3 At(float t) => foot + Vector3.up * height * t + lean * t * t;
                var anchor = new Vector2(foot.x, foot.z);
                var bounds = new Bounds(foot + Vector3.up * height * 0.5f, new Vector3(1, height, 1));
                void Tube(int surface, Vector3[] centers, float[] radii)
                {
                    Mesh tube = EnvironmentModelMeshes.Tube(centers, radii, 8);
                    mesh.Add(tube, 0, surface, Matrix4x4.identity, anchor, bounds, 0.4f);
                    Object.DestroyImmediate(tube);
                }
                Tube(15, Enumerable.Range(0, 11).Select(i => At(i / 10f)).ToArray(),
                    Enumerable.Range(0, 11).Select(i => Mathf.Lerp(thickness, thickness * 0.48f, i / 10f)).ToArray());
                for (int node = 1; node < 11; node++)
                {
                    float t = node / 11f, width = Mathf.Lerp(thickness, thickness * 0.48f, t) * 1.2f;
                    Tube(16, new[] { At(t) - Vector3.up * 0.027f, At(t), At(t) + Vector3.up * 0.027f },
                        new[] { width * 0.91f, width, width * 0.91f });
                    if (node < 5) continue;
                    float yaw = angle + node * 2.4f;
                    var direction = new Vector3(Mathf.Cos(yaw), 0, Mathf.Sin(yaw));
                    Vector3 start = At(t), end = start + direction * Range(random, 0.85f, 1.35f) + Vector3.up * 0.15f;
                    Tube(15, new[] { start, Vector3.Lerp(start, end, 0.5f) + Vector3.up * 0.12f, end }, new[] { 0.026f, 0.018f, 0.004f });
                    for (int leaf = 0; leaf < 7; leaf++)
                    {
                        float along = 0.22f + leaf * 0.105f;
                        Vector3 basePoint = Vector3.Lerp(start, end, along);
                        float leafYaw = yaw + (leaf % 2 == 0 ? 0.7f : -0.7f);
                        Vector3 leafDirection = new Vector3(Mathf.Cos(leafYaw), -0.22f - leaf * 0.025f, Mathf.Sin(leafYaw)).normalized;
                        Mesh blade = LanceLeaf(basePoint, leafDirection, Range(random, 0.48f, 0.8f), Range(random, 0.045f, 0.075f));
                        mesh.Add(blade, 0, 17, Matrix4x4.identity, anchor, bounds, 0.5f);
                        Object.DestroyImmediate(blade);
                    }
                }
            }
        }

        private static Mesh LanceLeaf(Vector3 start, Vector3 direction, float length, float halfWidth)
        {
            Vector3 middle = start + direction * length * 0.43f;
            Vector3 across = Vector3.Cross(direction, Vector3.up).normalized * halfWidth;
            var mesh = new Mesh { vertices = new[] { start, middle - across, middle + Vector3.up * 0.025f, middle + across, start + direction * length },
                triangles = new[] { 0, 1, 2, 0, 2, 3, 1, 4, 2, 2, 4, 3 } };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }

        private static T Save<T>(T asset, string path) where T : Object
        {
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null) { AssetDatabase.CreateAsset(asset, path); return asset; }
            EditorUtility.CopySerialized(asset, existing); EditorUtility.SetDirty(existing); Object.DestroyImmediate(asset); return existing;
        }

        internal sealed class Ground
        {
            private readonly Vector3[] vertices;
            private readonly int[] triangles;
            internal Ground(Mesh mesh) { vertices = mesh.vertices; triangles = mesh.GetTriangles(7); }
            internal float Height(float x, float z)
            {
                float height = -9;
                for (int tile = -1; tile <= 1; tile++)
                    for (int i = 0; i < triangles.Length; i += 3)
                    {
                        Vector3 a = vertices[triangles[i]], b = vertices[triangles[i + 1]], c = vertices[triangles[i + 2]];
                        float sample = z + tile * 42;
                        float d = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
                        if (Mathf.Abs(d) < 0.000001f) continue;
                        float u = ((b.z - c.z) * (x - c.x) + (c.x - b.x) * (sample - c.z)) / d;
                        float v = ((c.z - a.z) * (x - c.x) + (a.x - c.x) * (sample - c.z)) / d;
                        if (u >= -0.00001f && v >= -0.00001f && u + v <= 1.00001f)
                            height = Mathf.Max(height, u * a.y + v * b.y + (1 - u - v) * c.y);
                    }
                return height;
            }
        }

        private sealed class Geometry
        {
            private readonly List<Vector3> vertices = new List<Vector3>(), normals = new List<Vector3>();
            private readonly List<Vector2> uv = new List<Vector2>(), wind = new List<Vector2>(), anchors = new List<Vector2>();
            private readonly List<Color32> colors = new List<Color32>();
            private readonly List<int>[] triangles;
            internal Geometry(int count) { triangles = Enumerable.Range(0, count).Select(_ => new List<int>()).ToArray(); }
            internal void Copy(Mesh source, int surface, int target, Func<int, bool> keep = null)
                => Append(source, surface, target, Matrix4x4.identity, null, default, 0, keep);
            internal void Add(Mesh source, int surface, int target, Matrix4x4 matrix, Vector2 anchor, Bounds bounds, float variation)
                => Append(source, surface, target, matrix, anchor, bounds, variation, null);
            private void Append(Mesh source, int surface, int target, Matrix4x4 matrix, Vector2? anchor, Bounds bounds, float variation, Func<int, bool> keep)
            {
                Vector3[] points = source.vertices, directions = source.normals;
                Vector2[] tex = source.uv, sway = source.uv2, feet = source.uv3; Color32[] tint = source.colors32;
                int[] indices = source.GetTriangles(surface); var map = new Dictionary<int, int>();
                Matrix4x4 normalMatrix = matrix.inverse.transpose;
                for (int i = 0; i < indices.Length; i += 3)
                {
                    if (keep != null && !keep(indices[i])) continue;
                    for (int corner = 0; corner < 3; corner++)
                    {
                        int index = indices[i + corner];
                        if (!map.TryGetValue(index, out int destination))
                        {
                            destination = vertices.Count; map.Add(index, destination);
                            vertices.Add(matrix.MultiplyPoint3x4(points[index]));
                            normals.Add(anchor.HasValue ? normalMatrix.MultiplyVector(directions[index]).normalized : directions[index]);
                            uv.Add(tex.Length == points.Length ? tex[index] : Vector2.zero);
                            float h = anchor.HasValue ? Mathf.Clamp01((points[index].y - bounds.min.y) / Mathf.Max(0.001f, bounds.size.y)) : 0;
                            wind.Add(anchor.HasValue ? new Vector2(h * h, anchor.Value.x * 0.31f + anchor.Value.y * 0.73f)
                                : sway.Length == points.Length ? sway[index] : Vector2.zero);
                            anchors.Add(anchor ?? (feet.Length == points.Length ? feet[index] : Vector2.zero));
                            colors.Add(anchor.HasValue ? (Color32)new Color(h * 0.7f, variation, 1, 1)
                                : tint.Length == points.Length ? tint[index] : new Color32(255, 255, 255, 255));
                        }
                        triangles[target].Add(destination);
                    }
                }
            }
            internal Mesh Bake(string name)
            {
                var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
                mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uv); mesh.SetUVs(1, wind);
                mesh.SetUVs(2, anchors); mesh.SetColors(colors); mesh.subMeshCount = triangles.Length;
                for (int surface = 0; surface < triangles.Length; surface++) mesh.SetTriangles(triangles[surface], surface, false);
                mesh.RecalculateBounds(); return mesh;
            }
        }
    }
}

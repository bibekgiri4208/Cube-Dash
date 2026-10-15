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
    /// <summary>Surgically replaces only City pavement and the original faceted sakura.</summary>
    public static class JapaneseCitySidewalks
    {
        public const string TreesName = "City Sakura Avenue";
        public const int OriginalSurfaces = 22;
        public const int SurfaceCount = 29;
        public static readonly string[] Species = { "BlossomTree_01", "BlossomTree_03", "BlossomTree_05" };
        private static readonly string[] Names = { "Sidewalk sandstone", "Sidewalk pale limestone", "Sidewalk honey stone",
            "Sidewalk recessed joints", "Sidewalk precast curb", "Sidewalk planting soil", "Sidewalk drainage stone" };
        private static readonly string[] Colors = { "D9CFBE", "E2D8C8", "D2C8B7", "9C978D", "D9D2C4", "514537", "3E3A42" };

        [MenuItem("Tools/Cube Dash/Upgrade City Sidewalks and Use Menu Sakura Trees")]
        public static void ApplyFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) ApplyFromCommandLine();
        }

        public static void ApplyFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene edits first.");
            Scene scene = SceneManager.GetSceneByPath(JapaneseCityBuilder.ScenePath);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(JapaneseCityBuilder.ScenePath);
            var track = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<EndlessTrack>(true)).Single();
            if (!track.Environment.HasFixedBiome || track.Environment.FixedBiome != EnvironmentBiome.City)
                throw new InvalidOperationException("Only the standalone City is supported.");
            Transform[] roots = track.Segments.Select(s => s.Environment.Biomes[0].transform).ToArray();
            bool[] upgraded = roots.Select(r => r.Find(TreesName) != null).ToArray();
            if (upgraded.All(v => v)) { Debug.Log("City sidewalks and native sakura are already saved; manual edits preserved."); return; }
            if (upgraded.Any(v => v)) throw new InvalidOperationException("Inspect the partial City sidewalk/tree upgrade first.");
            Mesh[] layouts = roots[0].GetComponent<BiomeScenery>().Layouts;
            if (layouts.Length != 8 || layouts.Any(m => m.subMeshCount != OriginalSurfaces || m.uv3.Length != m.vertexCount))
                throw new InvalidOperationException("Expected the eight original Japanese streetscape layouts.");
            var prefabs = Species.Select(n => AssetDatabase.LoadAssetAtPath<GameObject>(MainMenuAssetStoreTrees.PrefabPath + n + ".prefab")).ToArray();
            Material blossoms = AssetDatabase.LoadAssetAtPath<Material>(MainMenuSakuraBlossoms.MaterialPath);
            Material sourceBlossoms = AssetDatabase.LoadAssetAtPath<Material>("Assets/Idyllic Fantasy Nature/Materials/Trees/Tree_Blossoms.mat");
            if (prefabs.Any(p => p == null) || blossoms == null || sourceBlossoms == null)
                throw new InvalidOperationException("The Main Menu's Idyllic sakura prefabs and blossom material are required.");
            foreach (var prefab in prefabs)
                if (prefab.GetComponent<LODGroup>() == null || prefab.GetComponentsInChildren<MonoBehaviour>(true).Length != 0)
                    throw new InvalidOperationException("Inspect the imported tree's LODs/scripts before using it.");
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/JapaneseCitySurface.shader");
            if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("City surface shader must compile first.");
            var extras = new Material[Names.Length];
            for (int i = 0; i < extras.Length; i++)
            {
                string path = JapaneseCityBuilder.ArtPath + "/Materials/" + Names[i] + ".mat";
                if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) throw new InvalidOperationException("Existing sidewalk assets need inspection: " + path);
                ColorUtility.TryParseHtmlString("#" + Colors[i], out Color color);
                var material = new Material(shader) { name = Names[i], enableInstancing = true };
                material.SetColor("_BaseColor", color); material.SetColor("_ShadeColor", new Color(0.94f, 0.94f, 0.96f));
                material.SetFloat("_SurfaceType", 5); material.SetFloat("_Smoothness", 0.12f); material.SetFloat("_OcclusionStrength", 0.88f);
                AssetDatabase.CreateAsset(material, path); extras[i] = material;
            }
            for (int layout = 0; layout < layouts.Length; layout++)
            {
                Mesh revised = ReplacePavementAndOldTrees(layouts[layout], layout);
                revised.name = layouts[layout].name;
                EditorUtility.CopySerialized(revised, layouts[layout]); EditorUtility.SetDirty(layouts[layout]); Object.DestroyImmediate(revised);
            }
            for (int section = 0; section < roots.Length; section++)
            {
                Transform streets = roots[section].Find(JapaneseCityBuilder.StreetsName);
                var renderer = streets.GetComponent<MeshRenderer>();
                if (renderer.sharedMaterials.Length != OriginalSurfaces) throw new InvalidOperationException("Unexpected City palette.");
                renderer.sharedMaterials = renderer.sharedMaterials.Concat(extras).ToArray();
                Transform group = new GameObject(TreesName, typeof(CitySakuraTrees)).transform;
                group.SetParent(roots[section], false); group.localPosition = streets.localPosition;
                var trees = new List<Transform>(); var homes = new List<Vector3>();
                for (int side = -1; side <= 1; side += 2)
                    for (int slot = 0; slot < 2; slot++)
                    {
                        int species = (section + slot + (side > 0 ? 1 : 0)) % prefabs.Length;
                        var prefab = prefabs[species]; Bounds bounds = BoundsOfTree(prefab);
                        Quaternion rotation = Quaternion.Euler(0, (section * 73 + slot * 137 + (side > 0 ? 43 : 17)) % 360, 0);
                        float radius = 0;
                        for (int corner = 0; corner < 8; corner++)
                        {
                            Vector3 sign = new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1);
                            Vector3 point = rotation * (bounds.center + Vector3.Scale(bounds.extents, sign));
                            radius = Mathf.Max(radius, Mathf.Abs(point.x), Mathf.Abs(point.z));
                        }
                        float scale = Mathf.Min(7.4f / bounds.size.y, 3.15f / radius);
                        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                        instance.transform.SetParent(group, false); instance.transform.localRotation = rotation; instance.transform.localScale = Vector3.one * scale;
                        Vector3 home = new Vector3(side * 9.3f, 0.225f - bounds.min.y * scale, 8.05f + slot * 21);
                        instance.transform.localPosition = home;
                        foreach (var collider in instance.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
                        foreach (Transform child in instance.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(child.gameObject, 0);
                        foreach (var treeRenderer in instance.GetComponentsInChildren<MeshRenderer>(true))
                        {
                            Material[] materials = treeRenderer.sharedMaterials;
                            for (int i = 0; i < materials.Length; i++) if (materials[i] == sourceBlossoms) materials[i] = blossoms;
                            treeRenderer.sharedMaterials = materials;
                            PrefabUtility.RecordPrefabInstancePropertyModifications(treeRenderer);
                        }
                        PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
                        trees.Add(instance.transform); homes.Add(home);
                    }
                var data = new SerializedObject(group.GetComponent<CitySakuraTrees>());
                data.FindProperty("streets").objectReferenceValue = renderer;
                var ts = data.FindProperty("trees"); var hs = data.FindProperty("homes"); ts.arraySize = hs.arraySize = trees.Count;
                for (int i = 0; i < trees.Count; i++) { ts.GetArrayElementAtIndex(i).objectReferenceValue = trees[i]; hs.GetArrayElementAtIndex(i).vector3Value = homes[i]; }
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save City sidewalks and sakura.");
            JapaneseCityReferenceFinish.ApplyFromCommandLine();
            Debug.Log("City saved: stone paving, recessed joints, precast curbs, drainage strips and inset tree beds; 32 City-specific low-poly sakura with wind and LODs. Gameplay, Main Menu and source pack preserved.");
        }

        public static bool IsRetiredGeometry(int surface, Vector2 anchor)
        {
            bool tree = Mathf.Abs(Mathf.Abs(anchor.x) - 9.3f) < 0.001f &&
                (Mathf.Abs(anchor.y - 8.05f) < 0.001f || Mathf.Abs(anchor.y - 29.05f) < 0.001f);
            bool pavement = (surface == 8 || surface == 12) && Mathf.Abs(Mathf.Abs(anchor.x) - 12) < 0.001f && Mathf.Abs(anchor.y - 21) < 0.001f;
            return tree || pavement;
        }

        public static Bounds BoundsOfTree(GameObject tree, bool worldSpace = false)
        {
            Bounds bounds = default; bool first = true;
            foreach (var renderer in tree.GetComponentsInChildren<MeshRenderer>(true))
            {
                Matrix4x4 matrix = worldSpace ? renderer.transform.localToWorldMatrix : tree.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
                Bounds local = renderer.localBounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 sign = new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1);
                    Vector3 point = matrix.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents, sign));
                    if (first) { bounds = new Bounds(point, Vector3.zero); first = false; } else bounds.Encapsulate(point);
                }
            }
            return bounds;
        }

        public static Mesh ReplacePavementAndOldTrees(Mesh original, int layout, float extension = 0)
        {
            var g = new PavementMesh(); Vector3[] vertices = original.vertices, normals = original.normals;
            Vector2[] uv = original.uv, anchors = original.uv3; Color32[] colors = original.colors32;
            var remap = new Dictionary<int, int>();
            for (int surface = 0; surface < Mathf.Min(original.subMeshCount, OriginalSurfaces); surface++)
            {
                int[] indices = original.GetTriangles(surface);
                for (int triangle = 0; triangle < indices.Length; triangle += 3)
                {
                    if (IsRetiredGeometry(surface, anchors[indices[triangle]]) &&
                        IsRetiredGeometry(surface, anchors[indices[triangle + 1]]) && IsRetiredGeometry(surface, anchors[indices[triangle + 2]])) continue;
                    for (int corner = 0; corner < 3; corner++)
                    {
                        int index = indices[triangle + corner];
                        if (!remap.TryGetValue(index, out int target))
                        {
                            target = g.Vertex(vertices[index], normals[index], uv[index], anchors[index], colors.Length > index ? colors[index] : new Color32(255, 255, 255, 255));
                            remap.Add(index, target);
                        }
                        g.Indices[surface].Add(target);
                    }
                }
            }
            for (int side = -1; side <= 1; side += 2)
            {
                g.Anchor = new Vector2(side * 12, 21);
                g.Box(25, side * (8.84f + extension * 0.5f), 0.035f, 21, 4.16f + extension, 0.33f, 42);
                g.Box(28, side * 5.85f, -0.04f, 21, 1.6f, 0.08f, 42);
                for (int row = 0; row < 42; row++)
                {
                    g.Slab(26, side * 6.7f, row + 0.5f, 0.2f, 0.98f, -0.08f, 0.255f, 0.015f);
                    int columns = Mathf.CeilToInt((4.12f + extension) / (4.12f / 6));
                    for (int column = 0; column < columns; column++)
                    {
                        float x0 = 6.8f + column * (4.12f / 6), x1 = Mathf.Min(10.92f + extension, x0 + 4.12f / 6);
                        if (x1 - x0 < 0.05f) continue;
                        var pieces = new List<Rect> { Rect.MinMaxRect(x0 + 0.012f, row + 0.012f, x1 - 0.012f, row + 1 - 0.012f) };
                        foreach (float z in new[] { 8.05f, 29.05f })
                            pieces = pieces.SelectMany(r => Subtract(r, Rect.MinMaxRect(8.3f, z - 1, 10.3f, z + 1))).ToList();
                        foreach (Rect r in pieces)
                        {
                            if (r.width < 0.045f || r.height < 0.045f) continue;
                            int variation = (layout * 17 + row * 13 + column * 7 + (side > 0 ? 3 : 0)) % 19;
                            g.Slab(variation < 2 ? 24 : variation < 5 ? 23 : 22, side * r.center.x, r.center.y, r.width, r.height, 0.195f, 0.23f, 0.012f);
                        }
                    }
                }
                foreach (float z in new[] { 8.05f, 29.05f })
                {
                    g.Anchor = new Vector2(side * 9.3f, z);
                    g.Box(27, side * 9.3f, 0.18f, z, 1.86f, 0.09f, 1.86f);
                    for (int edge = -1; edge <= 1; edge += 2)
                    {
                        g.Slab(26, side * 9.3f + edge * 0.965f, z, 0.07f, 2, 0.195f, 0.25f, 0.01f);
                        g.Slab(26, side * 9.3f, z + edge * 0.965f, 1.86f, 0.07f, 0.195f, 0.25f, 0.01f);
                    }
                }
            }
            return g.Finish();
        }

        private static IEnumerable<Rect> Subtract(Rect tile, Rect hole)
        {
            float x0 = Mathf.Max(tile.xMin, hole.xMin), x1 = Mathf.Min(tile.xMax, hole.xMax);
            float z0 = Mathf.Max(tile.yMin, hole.yMin), z1 = Mathf.Min(tile.yMax, hole.yMax);
            if (x1 <= x0 || z1 <= z0) { yield return tile; yield break; }
            if (tile.xMin < x0) yield return Rect.MinMaxRect(tile.xMin, tile.yMin, x0, tile.yMax);
            if (tile.xMax > x1) yield return Rect.MinMaxRect(x1, tile.yMin, tile.xMax, tile.yMax);
            if (tile.yMin < z0) yield return Rect.MinMaxRect(x0, tile.yMin, x1, z0);
            if (tile.yMax > z1) yield return Rect.MinMaxRect(x0, z1, x1, tile.yMax);
        }

        private sealed class PavementMesh
        {
            private readonly List<Vector3> vertices = new List<Vector3>(), normals = new List<Vector3>();
            private readonly List<Vector2> uv = new List<Vector2>(), anchors = new List<Vector2>();
            private readonly List<Color32> colors = new List<Color32>();
            public readonly List<int>[] Indices = Enumerable.Range(0, SurfaceCount).Select(_ => new List<int>()).ToArray();
            public Vector2 Anchor;
            public int Vertex(Vector3 point, Vector3 normal, Vector2 tex, Vector2 anchor, Color32 color)
            { int index = vertices.Count; vertices.Add(point); normals.Add(normal); uv.Add(tex); anchors.Add(anchor); colors.Add(color); return index; }
            private void Quad(int surface, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                byte ao = normal.y < 0.5f ? (byte)232 : (byte)255;
                int first = vertices.Count;
                foreach (Vector3 v in new[] { a, b, c, d }) Vertex(v, normal, new Vector2(v.x, v.z), Anchor, new Color32(ao, ao, ao, 255));
                Indices[surface].AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
            }
            public void Box(int surface, float x, float y, float z, float width, float height, float depth)
                => Slab(surface, x, z, width, depth, y - height * 0.5f, y + height * 0.5f, 0);
            public void Slab(int surface, float x, float z, float width, float depth, float bottom, float top, float bevel)
            {
                float x0 = x - width * 0.5f, x1 = x + width * 0.5f, z0 = z - depth * 0.5f, z1 = z + depth * 0.5f;
                bevel = Mathf.Min(bevel, width * 0.2f, depth * 0.2f, (top - bottom) * 0.4f);
                Vector3[] low = { new Vector3(x0, bottom, z0), new Vector3(x0, bottom, z1), new Vector3(x1, bottom, z1), new Vector3(x1, bottom, z0) };
                Vector3[] rim = low.Select(v => new Vector3(v.x, top - bevel, v.z)).ToArray();
                Vector3[] upper = { new Vector3(x0 + bevel, top, z0 + bevel), new Vector3(x0 + bevel, top, z1 - bevel), new Vector3(x1 - bevel, top, z1 - bevel), new Vector3(x1 - bevel, top, z0 + bevel) };
                Quad(surface, upper[0], upper[1], upper[2], upper[3]);
                for (int edge = 0; edge < 4; edge++)
                {
                    int next = (edge + 1) % 4;
                    Quad(surface, low[edge], low[next], rim[next], rim[edge]);
                    if (bevel > 0) Quad(surface, rim[edge], rim[next], upper[next], upper[edge]);
                }
                Quad(surface, low[3], low[2], low[1], low[0]);
            }
            public Mesh Finish()
            {
                var mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
                mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uv); mesh.SetUVs(2, anchors); mesh.SetColors(colors);
                mesh.subMeshCount = SurfaceCount;
                for (int i = 0; i < SurfaceCount; i++) mesh.SetTriangles(Indices[i], i, false);
                mesh.RecalculateBounds(); Bounds bounds = mesh.bounds; bounds.Encapsulate(bounds.min + Vector3.down * 80); mesh.bounds = bounds; return mesh;
            }
        }
    }
}

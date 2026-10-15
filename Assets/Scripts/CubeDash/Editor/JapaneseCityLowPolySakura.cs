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
    /// <summary>Original, editor-baked solid-geometry sakura made specifically for the City's faceted architecture.</summary>
    public static class JapaneseCityLowPolySakura
    {
        public const string ArtPath = JapaneseCityBuilder.ArtPath + "/Low Poly Sakura";
        public const string CanopyPath = ArtPath + "/Sakura Blossom Canopy.mat";
        public const string BarkPath = ArtPath + "/Sakura Timber.mat";
        public const string ShaderPath = "Assets/Shaders/CityLowPolySakura.shader";
        public const string LegacyTreesName = "Idyllic Sakura Avenue";
        public const int Variants = 3;
        public static string PrefabPath(int variant) => ArtPath + "/Sakura Avenue " + (variant + 1).ToString("00") + ".prefab";
        public static string MeshPath(int variant, int lod) => ArtPath + "/Sakura Avenue " + (variant + 1).ToString("00") + " LOD" + lod + ".asset";

        [MenuItem("Tools/Cube Dash/Replace City Trees with Low Poly Sakura")]
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
                throw new InvalidOperationException("Only standalone City may be changed.");
            var groups = track.Segments.Select(s => s.Environment.Biomes[0].transform.Find(JapaneseCitySidewalks.TreesName) ??
                s.Environment.Biomes[0].transform.Find(LegacyTreesName)).ToArray();
            if (groups.Any(g => g == null || g.GetComponent<CitySakuraTrees>() == null))
                throw new InvalidOperationException("Expected the saved City sakura avenue.");
            var presentations = groups.Select(g => g.GetComponent<CitySakuraTrees>()).ToArray();
            if (presentations.Any(p => p.Trees.Length != 4 || p.Homes.Length != 4 || p.Trees.Any(t => t == null)))
                throw new InvalidOperationException("Inspect incomplete tree/home assignments before replacement.");
            bool[] replaced = presentations.SelectMany(p => p.Trees).Select(t =>
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject).StartsWith(ArtPath + "/", StringComparison.Ordinal)).ToArray();
            if (replaced.All(v => v)) { Debug.Log("City low-poly sakura already saved; manual edits preserved."); return; }
            if (replaced.Any(v => v) || AssetDatabase.IsValidFolder(ArtPath))
                throw new InvalidOperationException("Inspect the partial low-poly sakura replacement before authoring.");
            // Replacement is deliberately limited to the previous imported tree instances, never arbitrary user objects.
            if (presentations.SelectMany(p => p.Trees).Any(t => !PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject)
                .StartsWith(MainMenuAssetStoreTrees.PrefabPath, StringComparison.Ordinal)))
                throw new InvalidOperationException("Unrecognized authored trees need inspection before replacement.");
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null || ShaderUtil.ShaderHasError(shader))
                throw new InvalidOperationException("Low-poly sakura shader must compile first. " + (shader == null ? "Missing shader asset." :
                    string.Join("\n", ShaderUtil.GetShaderMessages(shader).Select(m => m.message))));
            AssetDatabase.CreateFolder(JapaneseCityBuilder.ArtPath, "Low Poly Sakura");
            Material CreateMaterial(string path, float smoothness)
            {
                var result = new Material(shader) { name = System.IO.Path.GetFileNameWithoutExtension(path), enableInstancing = true };
                result.SetColor("_BaseColor", Color.white); result.SetFloat("_Smoothness", smoothness);
                result.SetFloat("_WindStrength", 0.055f); AssetDatabase.CreateAsset(result, path); return result;
            }
            var materials = new[] { CreateMaterial(BarkPath, 0.13f), CreateMaterial(CanopyPath, 0.08f) };
            var prefabs = new GameObject[Variants];
            for (int variant = 0; variant < Variants; variant++)
            {
                var root = new GameObject("Sakura Avenue " + (variant + 1).ToString("00"), typeof(LODGroup));
                try
                {
                    var levels = new LOD[3];
                    for (int lod = 0; lod < levels.Length; lod++)
                    {
                        Mesh mesh = BuildModel(variant, lod); AssetDatabase.CreateAsset(mesh, MeshPath(variant, lod));
                        var child = new GameObject("LOD" + lod, typeof(MeshFilter), typeof(MeshRenderer)); child.transform.SetParent(root.transform, false);
                        child.GetComponent<MeshFilter>().sharedMesh = mesh;
                        var renderer = child.GetComponent<MeshRenderer>(); renderer.sharedMaterials = materials;
                        renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
                        levels[lod] = new LOD(lod == 0 ? 0.22f : lod == 1 ? 0.09f : 0.012f, new Renderer[] { renderer });
                    }
                    var group = root.GetComponent<LODGroup>(); group.SetLODs(levels); group.fadeMode = LODFadeMode.None; group.RecalculateBounds();
                    prefabs[variant] = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(variant));
                }
                finally { Object.DestroyImmediate(root); }
            }
            for (int section = 0; section < groups.Length; section++)
            {
                Transform group = groups[section]; group.name = JapaneseCitySidewalks.TreesName;
                var presentation = presentations[section]; Transform[] oldTrees = presentation.Trees.ToArray();
                Vector3[] homes = presentation.Homes.ToArray(); var trees = new Transform[4];
                for (int i = 0; i < trees.Length; i++)
                {
                    int variant = (section + i * 2) % Variants;
                    GameObject prefab = prefabs[variant]; Bounds bounds = JapaneseCitySidewalks.BoundsOfTree(prefab);
                    Quaternion rotation = Quaternion.Euler(0, ((section * 3 + i) % 4) * 90, 0);
                    float radius = 0;
                    for (int corner = 0; corner < 8; corner++)
                    {
                        Vector3 sign = new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1);
                        Vector3 point = rotation * (bounds.center + Vector3.Scale(bounds.extents, sign));
                        radius = Mathf.Max(radius, Mathf.Abs(point.x), Mathf.Abs(point.z));
                    }
                    float scale = Mathf.Min((6.65f + (section + i) % 3 * 0.2f) / bounds.size.y, 3.05f / radius);
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                    trees[i] = instance.transform; trees[i].SetParent(group, false);
                    trees[i].localRotation = rotation; trees[i].localScale = Vector3.one * scale;
                    homes[i].y = 0.225f - bounds.min.y * scale; trees[i].localPosition = homes[i];
                    PrefabUtility.RecordPrefabInstancePropertyModifications(trees[i]);
                }
                var data = new SerializedObject(presentation);
                var ts = data.FindProperty("trees"); var hs = data.FindProperty("homes");
                for (int i = 0; i < trees.Length; i++)
                {
                    ts.GetArrayElementAtIndex(i).objectReferenceValue = trees[i]; hs.GetArrayElementAtIndex(i).vector3Value = homes[i];
                }
                data.ApplyModifiedPropertiesWithoutUndo();
                foreach (Transform old in oldTrees) Object.DestroyImmediate(old.gameObject);
            }
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save City low-poly sakura.");
            Debug.Log("City sakura replaced: three original sculpted, solid faceted models, tapered forks/roots, layered rose/blush crowns and three baked LODs. Same 32 planting positions and pooled bend/reveal; no texture cards, imported tree dependencies, runtime generation or colliders. Other maps/Main Menu unchanged.");
        }

        public static Mesh BuildModel(int variant, int lod)
        {
            if (variant < 0 || variant >= Variants || lod < 0 || lod > 2) throw new ArgumentOutOfRangeException();
            var mesh = new TreeMesh(); int sides = lod == 0 ? 8 : lod == 1 ? 6 : 5;
            float lean = (variant - 1) * 0.13f;
            Vector3 trunk = new Vector3(lean, 2.55f, 0.04f);
            mesh.Branch(new[] { Vector3.zero, new Vector3(-0.03f, 0.32f, 0), new Vector3(0.08f, 1.3f, -0.06f), trunk,
                new Vector3(lean + 0.14f, 3.7f, 0.13f), new Vector3(0.08f, 5.15f, 0.06f) },
                new[] { 0.32f, 0.24f, 0.2f, 0.165f, 0.11f, 0.035f }, sides);
            for (int root = 0; root < 5; root++)
            {
                float angle = (root * 72 + variant * 31) * Mathf.Deg2Rad;
                var outwards = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                mesh.Branch(new[] { outwards * 0.14f + Vector3.up * 0.22f, outwards * 0.42f + Vector3.up * 0.12f,
                    outwards * (0.64f + root % 2 * 0.09f) + Vector3.up * 0.035f }, new[] { 0.115f, 0.075f, 0.025f }, sides);
            }
            // Boughs fork upward/outward like a pruned street cherry, not a round lollipop crown.
            for (int bough = 0; bough < 6; bough++)
            {
                float angle = (bough * 60 + variant * 23 + (bough % 2) * 9) * Mathf.Deg2Rad;
                Vector3 outward = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                Vector3 tangent = new Vector3(-outward.z, 0, outward.x);
                float tier = bough % 3 * 0.23f + variant * 0.05f;
                Vector3 start = trunk + Vector3.up * (bough % 2 * 0.38f);
                Vector3 elbow = outward * 0.85f + new Vector3(lean, 3.35f + tier, 0.04f);
                Vector3 fork = outward * (1.52f + bough % 2 * 0.13f) + Vector3.up * (4.13f + tier);
                mesh.Branch(new[] { start, elbow, fork }, new[] { 0.13f, 0.095f, 0.052f }, sides);
                for (int split = -1; split <= 1; split += 2)
                {
                    Vector3 tip = outward * (1.88f + (bough % 2) * 0.12f) + tangent * (split * 0.42f) + Vector3.up * (4.65f + tier + split * 0.09f);
                    mesh.Branch(new[] { fork, Vector3.Lerp(fork, tip, 0.55f) + Vector3.up * 0.13f, tip },
                        new[] { 0.052f, 0.036f, 0.012f }, sides);
                    mesh.Crown(tip + Vector3.up * 0.22f, new Vector3(0.86f + (bough % 2) * 0.1f, 0.66f, 0.88f),
                        angle + split * 0.3f, bough * 7 + split + variant * 31, lod, 0.97f + (bough % 3) * 0.025f);
                }
                Vector3 upper = outward * 1.08f + tangent * 0.12f + Vector3.up * (5.55f + tier);
                mesh.Branch(new[] { elbow, outward * 0.91f + Vector3.up * 4.62f, upper }, new[] { 0.065f, 0.044f, 0.014f }, sides);
                mesh.Crown(upper, new Vector3(1.08f, 0.73f, 1.02f), angle, bough * 19 + variant * 11, lod, 1);
            }
            // Offset upper crowns create an asymmetrical umbrella outline with a few purposeful gaps.
            mesh.Crown(new Vector3(-0.33f + lean, 6.05f, 0.08f), new Vector3(1.14f, 0.84f, 1.04f), 0.35f + variant, 71 + variant, lod, 1.025f);
            mesh.Crown(new Vector3(0.65f, 5.99f, -0.62f + variant * 0.12f), new Vector3(1.03f, 0.7f, 0.95f), 1.3f, 89 + variant, lod, 1);
            return mesh.Finish("Sakura Avenue " + (variant + 1).ToString("00") + " LOD" + lod);
        }

        private sealed class TreeMesh
        {
            private readonly List<Vector3> vertices = new List<Vector3>(), normals = new List<Vector3>();
            private readonly List<Vector2> uv = new List<Vector2>();
            private readonly List<Color32> colors = new List<Color32>();
            private readonly List<int>[] indices = { new List<int>(), new List<int>() };
            private static Color Hex(string value) { ColorUtility.TryParseHtmlString("#" + value, out Color c); return c; }
            private static readonly Color Bark = Hex("725246"), Rose = Hex("D47B9C"), Blush = Hex("F7C2D3");

            private void Face(int surface, Vector3 a, Vector3 b, Vector3 c, Vector3 inside, float tone = 1)
            {
                a.y = Mathf.Max(0, a.y); b.y = Mathf.Max(0, b.y); c.y = Mathf.Max(0, c.y);
                Vector3 normal = Vector3.Cross(b - a, c - a);
                if (normal.sqrMagnitude < 0.00000001f) return;
                Vector3 center = (a + b + c) / 3;
                if (Vector3.Dot(normal, center - inside) < 0) { Vector3 swap = b; b = c; c = swap; normal = -normal; }
                normal.Normalize();
                float height = Mathf.Clamp01((center.y - 3.7f) / 3.2f);
                Color color = surface == 0 ? Bark * (0.95f + normal.y * 0.07f + Mathf.Sin(center.y * 9) * 0.025f) :
                    Color.Lerp(Rose, Blush, Mathf.Clamp01(height * 0.66f + normal.y * 0.18f + 0.19f)) * tone;
                color = color.linear; color.a = surface == 0 ? Mathf.Lerp(0.72f, 1, Mathf.Clamp01(center.y / 1.2f)) : Mathf.Lerp(0.84f, 1, height);
                int first = vertices.Count;
                foreach (Vector3 point in new[] { a, b, c })
                {
                    vertices.Add(point); normals.Add(normal); colors.Add(color); uv.Add(new Vector2(point.x, point.z));
                }
                indices[surface].AddRange(new[] { first, first + 1, first + 2 });
            }

            public void Branch(Vector3[] path, float[] radii, int sides)
            {
                var rings = new Vector3[path.Length][];
                Vector3 firstDirection = (path[1] - path[0]).normalized;
                Vector3 reference = Mathf.Abs(firstDirection.y) < 0.9f ? Vector3.up : Vector3.forward;
                for (int i = 0; i < path.Length; i++)
                {
                    Vector3 direction = (path[Mathf.Min(i + 1, path.Length - 1)] - path[Mathf.Max(0, i - 1)]).normalized;
                    Vector3 tangent = Vector3.Cross(direction, reference).normalized;
                    Vector3 bitangent = Vector3.Cross(direction, tangent);
                    rings[i] = new Vector3[sides];
                    for (int side = 0; side < sides; side++)
                    {
                        float angle = (side + 0.12f) * Mathf.PI * 2 / sides;
                        rings[i][side] = path[i] + (tangent * Mathf.Cos(angle) + bitangent * Mathf.Sin(angle)) * radii[i];
                    }
                }
                for (int i = 0; i < path.Length - 1; i++)
                    for (int side = 0; side < sides; side++)
                    {
                        int next = (side + 1) % sides; Vector3 inside = (path[i] + path[i + 1]) * 0.5f;
                        Face(0, rings[i][side], rings[i + 1][side], rings[i + 1][next], inside);
                        Face(0, rings[i][side], rings[i + 1][next], rings[i][next], inside);
                    }
                for (int side = 0; side < sides; side++)
                {
                    int next = (side + 1) % sides;
                    Face(0, path[0], rings[0][next], rings[0][side], path[1]);
                    int end = path.Length - 1; Face(0, path[end], rings[end][side], rings[end][next], path[end - 1]);
                }
            }

            public void Crown(Vector3 center, Vector3 size, float yaw, int seed, int lod, float tone)
            {
                int sectors = lod == 0 ? 9 : lod == 1 ? 7 : 5, rings = lod == 0 ? 6 : 4;
                Vector3 Point(int ring, int sector)
                {
                    if (ring == 0) return center + Vector3.up * size.y;
                    if (ring == rings) return center - Vector3.up * size.y;
                    float phi = ring * Mathf.PI / rings, angle = sector * Mathf.PI * 2 / sectors + yaw + (ring % 2) * 0.13f;
                    float ripple = 1 + Mathf.Sin(angle * 3 + seed * 0.7f) * 0.075f;
                    Vector3 point = new Vector3(Mathf.Cos(angle) * Mathf.Sin(phi) * ripple,
                        Mathf.Cos(phi) + Mathf.Sin(angle * 2 + seed) * Mathf.Sin(phi) * 0.055f,
                        Mathf.Sin(angle) * Mathf.Sin(phi) * ripple);
                    return center + Vector3.Scale(point, size);
                }
                for (int ring = 0; ring < rings; ring++)
                    for (int sector = 0; sector < sectors; sector++)
                    {
                        Vector3 a = Point(ring, sector), b = Point(ring + 1, sector), c = Point(ring + 1, sector + 1), d = Point(ring, sector + 1);
                        Face(1, a, b, c, center, tone); Face(1, a, c, d, center, tone);
                    }
            }

            public Mesh Finish(string name)
            {
                var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
                mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetColors(colors); mesh.SetUVs(0, uv);
                mesh.subMeshCount = 2; for (int i = 0; i < 2; i++) mesh.SetTriangles(indices[i], i, false);
                mesh.RecalculateBounds(); Bounds bounds = mesh.bounds; bounds.extents += new Vector3(0.08f, 0, 0.08f); mesh.bounds = bounds;
                return mesh;
            }
        }
    }
}

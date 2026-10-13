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
    /// <summary>Scene-local, pooled Idyllic planting. Shared biomes and downloaded assets stay untouched.</summary>
    public static class JungleNatureBuilder
    {
        public const string ScenePath = "Assets/Scenes/Jungle.unity";
        public const string ArtPath = "Assets/Maps/Jungle Nature";
        private const string Prefabs = "Assets/Idyllic Fantasy Nature/Prefabs/";
        private static readonly string[] Trees = { "BroadleafTree_01_Green", "BroadleafTree_02_Green",
            "BroadleafTree_03_Green", "BroadleafTree_04_Green", "BroadleafTree_05_Green" };
        private static readonly string[] Bushes = { "Bush_01_01", "Bush_02_01", "Bush_03_01",
            "Bush_01_02", "Bush_02_02", "Bush_03_02" };
        private static readonly string[] Grasses = { "Grass_01", "Grass_02", "Grass_03" };
        private static readonly string[] Rocks = { "Stone_Medium_01", "Stone_Medium_02", "Stone_Medium_03" };

        [MenuItem("Tools/Cube Dash/Beautify Jungle with Idyllic Nature")]
        public static void ApplyFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) ApplyFromCommandLine();
        }

        public static void ApplyFromCommandLine()
            => Apply(false);

        [MenuItem("Tools/Cube Dash/Refine Jungle Leaves Rocks and Understory")]
        public static void RefineFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) RefineFromCommandLine();
        }

        public static void RefineFromCommandLine() => Apply(true);

        [MenuItem("Tools/Cube Dash/Tone Down Jungle Greens")]
        public static void ToneDownFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/CubeDash/Environments/Jungle.prefab");
            Material[] original = source.transform.Find("Landscape Details").GetComponent<Renderer>().sharedMaterials;
            var materials = AssetDatabase.FindAssets("t:Material", new[] { ArtPath + "/Materials" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(material => material.HasProperty("_Foliage") && material.GetFloat("_Foliage") > 0.5f).ToArray();
            if (materials.Length != 7) throw new InvalidOperationException("Expected the seven saved Jungle leaf, bush and grass materials.");
            foreach (Material material in materials)
            {
                bool grass = material.name.Contains("Grass_"), bush = material.name.Contains("Bush_");
                material.SetColor("_BaseColor", FoliageTone(original[1].GetColor("_BaseColor"), grass, bush));
                material.SetColor("_TopColor", bush ? BushTopTone(original[2].GetColor("_BaseColor"))
                    : FoliageTone(original[2].GetColor("_BaseColor"), grass, false));
                material.SetFloat("_LeafLightGain", grass ? 0.72f : 0.76f);
                EditorUtility.SetDirty(material);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Applied deep rainforest greens to seven Jungle foliage materials and reduced leaf light gain. Bushes/grass are visibly darker; tree highlights no longer wash pale. Scene, geometry, rocks and other maps unchanged.");
        }

        [MenuItem("Tools/Cube Dash/Apply Two-Tone Jungle Bushes")]
        public static void ApplyDualToneBushesFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/CubeDash/Environments/Jungle.prefab");
            Material[] original = source.transform.Find("Landscape Details").GetComponent<Renderer>().sharedMaterials;
            for (int i = 1; i <= 3; i++)
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(ArtPath + "/Materials/Jungle Idyllic Bush_0" + i + ".mat");
                if (material == null) throw new InvalidOperationException("Missing saved Jungle bush material: " + i);
                material.SetColor("_BaseColor", FoliageTone(original[1].GetColor("_BaseColor"), false, true));
                material.SetColor("_TopColor", BushTopTone(original[2].GetColor("_BaseColor")));
                EditorUtility.SetDirty(material);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Applied two-tone Jungle bushes: deep green lower foliage and richer, slightly warmer green upper leaves. Trees, grass, lighting and geometry unchanged.");
        }

        private static Color BushTopTone(Color color)
        {
            Color.RGBToHSV(color, out float hue, out float saturation, out float value);
            return Color.HSVToRGB(Mathf.Repeat(hue - 0.018f, 1), saturation * 0.88f, value * 0.78f);
        }

        private static Color FoliageTone(Color color, bool grass, bool bush)
        {
            Color.RGBToHSV(color, out float hue, out float saturation, out float value);
            float saturationScale = grass ? 0.76f : bush ? 0.78f : 0.80f;
            float valueScale = grass ? 0.58f : bush ? 0.60f : 0.72f;
            Color muted = Color.HSVToRGB(hue, saturation * saturationScale, value * valueScale);
            muted.a = 1;
            return muted;
        }

        private static void Apply(bool refine)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene edits before updating the jungle.");
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath);
            var track = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<EndlessTrack>(true)).Single();
            if (!track.Environment.HasFixedBiome || track.Environment.FixedBiome != EnvironmentBiome.Jungle)
                throw new InvalidOperationException("Expected the existing standalone Jungle map.");
            var roots = track.Segments.Select(segment => segment.Environment.Biomes[(int)EnvironmentBiome.Jungle]).ToArray();
            if (roots.Any(root => root == null)) throw new InvalidOperationException("A saved jungle section is missing.");
            bool[] upgraded = roots.Select(root => root.GetComponent<BiomeScenery>().Layouts.All(mesh =>
                AssetDatabase.GetAssetPath(mesh).StartsWith(ArtPath + "/", StringComparison.Ordinal))).ToArray();
            if (upgraded.All(value => value) && !refine)
            {
                Debug.Log("Idyllic jungle planting is already saved. Existing scene and planting edits were preserved.");
                return;
            }
            if (refine && !upgraded.All(value => value))
                throw new InvalidOperationException("Refinement requires the saved Idyllic jungle layouts.");
            if (!refine && (upgraded.Any(value => value) || AssetDatabase.IsValidFolder(ArtPath)))
                throw new InvalidOperationException("Partial/existing jungle nature assets require inspection before reauthoring.");
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/CubeDash/Environments/Jungle.prefab");
            Mesh[] originals = source.GetComponent<BiomeScenery>().Layouts;
            Material[] originalPalette = source.transform.Find("Landscape Details").GetComponent<Renderer>().sharedMaterials;
            if (originals.Length != 8 || originalPalette.Length != 15)
                throw new InvalidOperationException("Inspect the existing jungle layouts before changing their surfaces.");
            var plants = Trees.Concat(Bushes).Concat(Grasses).Concat(Rocks).Distinct().ToDictionary(name => name, LoadPlant);
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/JungleVegetation.shader");
            if (shader == null) throw new InvalidOperationException("The jungle foliage shader is missing.");
            if (ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("The jungle foliage shader must compile first: "
                + string.Join("; ", ShaderUtil.GetShaderMessages(shader).Select(message => message.message + " at " + message.line)));
            if (!refine)
            {
                AssetDatabase.CreateFolder("Assets/Maps", "Jungle Nature");
                AssetDatabase.CreateFolder(ArtPath, "Materials");
                AssetDatabase.CreateFolder(ArtPath, "Layouts");
            }
            var palette = originalPalette.ToList();
            var surfaces = new Dictionary<Material, int>();
            foreach (Plant plant in plants.Values)
                foreach (Renderer renderer in plant.Renderers)
                    foreach (Material material in renderer.sharedMaterials)
                    {
                        if (surfaces.ContainsKey(material)) continue;
                        surfaces.Add(material, palette.Count);
                        palette.Add(MakeMaterial(material, shader, originalPalette));
                    }
            var layouts = new Mesh[originals.Length];
            for (int layout = 0; layout < layouts.Length; layout++)
            {
                var geometry = new PlantingMesh(palette.Count);
                // Keep the actual hills; retire the round boulders and their separate green caps.
                geometry.Add(originals[layout], 7, 7, Matrix4x4.identity, null, 0);
                var random = new System.Random(61573 + layout * 139);
                var terrain = new Terrain(originals[layout]);
                int species = layout;
                void Place(string name, float x, float z, float height, float width = 100)
                {
                    Plant plant = plants[name];
                    float scale = Mathf.Min(height / plant.Bounds.size.y, width / Mathf.Max(plant.Bounds.size.x, plant.Bounds.size.z));
                    Vector3 position = new Vector3(x, terrain.Height(x, z) - 0.1f - plant.Bounds.min.y * scale, z);
                    float yaw = Range(random, 0, 360), variation = Range(random, 0.15f, 0.85f);
                    Matrix4x4 transform = Matrix4x4.TRS(position, Quaternion.Euler(0, yaw, 0), Vector3.one * scale);
                    foreach (Renderer renderer in plant.Renderers)
                    {
                        Mesh mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                        Matrix4x4 toRoot = plant.Prefab.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
                        for (int surface = 0; surface < mesh.subMeshCount; surface++)
                            geometry.Add(mesh, surface, surfaces[renderer.sharedMaterials[surface]], transform * toRoot,
                                new Vector4(x, z, plant.Bounds.min.y, plant.Bounds.size.y), variation, toRoot);
                    }
                }
                for (int side = -1; side <= 1; side += 2)
                {
                    for (int row = 0; row < 5; row++)
                    {
                        float z = 4 + row * 8 + Range(random, -1.8f, 1.8f);
                        bool clearing = (row + layout) % 4 == 0;
                        float nearX = side * Range(random, clearing ? 34 : 18, clearing ? 39 : 23);
                        Place(Trees[species++ % Trees.Length], nearX, z, Range(random, 15, 20), 15);
                        Place(Trees[species++ % Trees.Length], side * Range(random, 40, 53), z + Range(random, -2, 2), Range(random, 19, 24), 17);
                        // Asymmetric shrub islands break the old straight rows, with small grass skirts.
                        float bushX = side * Range(random, 12.5f, 22), bushZ = z + 1.8f;
                        for (int clump = 0; clump < 3; clump++)
                        {
                            float x = bushX + Range(random, -2, 2), patchZ = Mathf.Clamp(bushZ + Range(random, -2, 2), 1, 41);
                            Place(Bushes[(row + clump + layout) % Bushes.Length], x, patchZ, Range(random, 1.6f, 3), 4);
                            for (int tuft = 0; tuft < 3; tuft++)
                                Place(Grasses[(tuft + row + layout) % Grasses.Length], x + Range(random, -1.9f, 1.9f),
                                    Mathf.Clamp(patchZ + Range(random, -2, 2), 0.8f, 41.2f), Range(random, 0.7f, 1.4f), 2.6f);
                        }
                    }
                    // A broken low verge frames the highway without covering shoulders or guardrails.
                    for (int tuft = 0; tuft < 28; tuft++)
                        Place(Grasses[(tuft + layout) % Grasses.Length], side * Range(random, 10.2f, 13.8f),
                            Range(random, 0.6f, 41.4f), Range(random, 0.65f, 1.15f), 2.2f);
                    // Small understory pockets climb the actual saved hillside triangles.
                    for (int clump = 0; clump < 6; clump++)
                        Place(Bushes[(clump + layout) % Bushes.Length], side * Range(random, 38, 51),
                            Range(random, 1.5f, 40.5f), Range(random, 2, 3.5f), 4.5f);
                }
                // Separate planting on both banks keeps the downstream water ribbon visible.
                for (int bank = 0; bank < 2; bank++)
                    for (int patch = 0; patch < 14; patch++)
                    {
                        float x = bank == 0 ? Range(random, -25.1f, -24.3f) : Range(random, -33.8f, -33.1f);
                        float z = 1.5f + patch * 2.9f + Range(random, -0.5f, 0.5f);
                        Place(Grasses[(patch + bank + layout) % Grasses.Length], x, z, Range(random, 0.8f, 1.5f), 1.8f);
                        if (patch % 3 == 0) Place(Bushes[(patch + layout) % Bushes.Length], x, z, Range(random, 1.3f, 2), 2.4f);
                    }
                // Irregular pack rocks use only the stone texture, never the pack's moss overlay.
                for (int side = -1; side <= 1; side += 2)
                    for (int rock = 0; rock < 5; rock++)
                        Place(Rocks[(rock + layout) % Rocks.Length], side * Range(random, 15, 24),
                            3 + rock * 8 + Range(random, -1.8f, 1.8f), Range(random, 1.8f, 3.3f), 4.8f);
                layouts[layout] = geometry.Bake("Idyllic Jungle Layout " + (layout + 1));
                layouts[layout] = Save(layouts[layout], ArtPath + "/Layouts/" + layouts[layout].name + ".asset");
            }
            var varietyLayouts = source.GetComponent<BiomeScenery>().VarietyLayouts.Select((mesh, index) =>
            {
                Mesh clean = Object.Instantiate(mesh); clean.name = "Jungle Understory " + (index + 1);
                Vector2[] anchors = mesh.uv3;
                for (int surface = 0; surface < clean.subMeshCount; surface++)
                {
                    var triangles = new List<int>(); int[] original = mesh.GetTriangles(surface);
                    if (surface != 13)
                        for (int triangle = 0; triangle < original.Length; triangle += 3)
                        {
                            Vector2 anchor = anchors[original[triangle]];
                            if (Mathf.Abs(anchor.x + 12.5f) < 0.01f && anchor.y > 24 && anchor.y < 30) continue;
                            triangles.Add(original[triangle]); triangles.Add(original[triangle + 1]); triangles.Add(original[triangle + 2]);
                        }
                    clean.SetTriangles(triangles, surface, false);
                }
                clean.RecalculateBounds();
                return Save(clean, ArtPath + "/Layouts/" + clean.name + ".asset");
            }).ToArray();
            for (int i = 0; i < roots.Length; i++)
            {
                var scenery = roots[i].GetComponent<BiomeScenery>();
                var data = new SerializedObject(scenery);
                foreach (string field in new[] { "layouts", "entranceLayouts", "exitLayouts" })
                {
                    var array = data.FindProperty(field); array.arraySize = layouts.Length;
                    for (int layout = 0; layout < layouts.Length; layout++) array.GetArrayElementAtIndex(layout).objectReferenceValue = layouts[layout];
                }
                data.ApplyModifiedPropertiesWithoutUndo();
                var varietyData = new SerializedObject(scenery); var variety = varietyData.FindProperty("varietyLayouts");
                for (int layout = 0; layout < variety.arraySize; layout++) variety.GetArrayElementAtIndex(layout).objectReferenceValue = varietyLayouts[layout];
                varietyData.ApplyModifiedPropertiesWithoutUndo();
                var details = roots[i].transform.Find("Landscape Details");
                details.GetComponent<Renderer>().sharedMaterials = palette.ToArray();
                scenery.SelectLayout(i - 1, track.Environment.SegmentsPerBiome, true);
                var varietyFilter = roots[i].transform.Find("Landscape Variety").GetComponent<MeshFilter>();
                foreach (Component component in new Component[] { scenery, details.GetComponent<MeshFilter>(), details.GetComponent<Renderer>(), varietyFilter })
                    if (PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            }
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the jungle planting.");
            Debug.Log("Refined all eight jungle layouts: softly lit green foliage, ten angular Idyllic rocks per section, no moss caps or banana plant. Forest palette, river, hills, remaining landmarks and gameplay preserved.");
        }

        private static float Range(System.Random random, float min, float max) => min + (float)random.NextDouble() * (max - min);

        private sealed class Plant
        {
            public GameObject Prefab;
            public Renderer[] Renderers;
            public Bounds Bounds;
        }

        private static Plant LoadPlant(string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + name + ".prefab");
            if (prefab == null) throw new InvalidOperationException("Missing Idyllic plant: " + name);
            var lod = prefab.GetComponent<LODGroup>();
            // Use the pack's middle LOD for a dense forest at runner-camera distances.
            Renderer[] renderers = lod != null ? lod.GetLODs()[Mathf.Min(1, lod.GetLODs().Length - 1)].renderers
                : prefab.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers.Length == 0 || renderers.Any(renderer => renderer == null || renderer.GetComponent<MeshFilter>()?.sharedMesh == null))
                throw new InvalidOperationException("Plant has no usable mesh: " + name);
            foreach (Renderer renderer in renderers)
                if (renderer.sharedMaterials.Length != renderer.GetComponent<MeshFilter>().sharedMesh.subMeshCount)
                    throw new InvalidOperationException("Unexpected Idyllic material assignments: " + name);
            return new Plant { Prefab = prefab, Renderers = renderers, Bounds = MainMenuAssetStoreTrees.BoundsOf(prefab) };
        }

        private static Material MakeMaterial(Material source, Shader shader, Material[] original)
        {
            bool bark = source.name.IndexOf("Bark", StringComparison.OrdinalIgnoreCase) >= 0;
            bool grass = source.name.StartsWith("Grass", StringComparison.Ordinal);
            bool rock = source.name.StartsWith("Rock", StringComparison.Ordinal) || source.name.StartsWith("Stone", StringComparison.Ordinal);
            var result = new Material(shader) { name = "Jungle Idyllic " + source.name, enableInstancing = true, doubleSidedGI = !bark };
            Texture texture = source.HasProperty("_Texture") ? source.GetTexture("_Texture") : null;
            if (texture == null && source.HasProperty("_Base_Map")) texture = source.GetTexture("_Base_Map");
            if (texture == null && source.HasProperty("_BaseMap")) texture = source.GetTexture("_BaseMap");
            if (texture == null) throw new InvalidOperationException("Missing Idyllic texture: " + source.name);
            result.SetTexture("_BaseMap", texture);
            Color baseColor = original[rock ? 3 : bark ? 0 : 1].GetColor("_BaseColor");
            Color topColor = bark || rock ? baseColor * 1.08f : original[2].GetColor("_BaseColor");
            if (!bark && !rock)
            {
                bool bush = source.name.StartsWith("Bush", StringComparison.Ordinal);
                baseColor = FoliageTone(baseColor, grass, bush);
                topColor = bush ? BushTopTone(topColor) : FoliageTone(topColor, grass, false);
            }
            baseColor.a = topColor.a = 1;
            result.SetColor("_BaseColor", baseColor); result.SetColor("_TopColor", topColor);
            result.SetFloat("_WindStrength", bark || rock ? 0 : grass ? 0.12f : 0.08f);
            result.SetFloat("_Foliage", bark || rock ? 0 : 1);
            result.SetFloat("_BushLighting", source.name.StartsWith("Bush", StringComparison.Ordinal) ? 1 : 0);
            result.SetFloat("_LeafLightGain", bark || rock ? 1 : grass ? 0.72f : 0.76f);
            result.SetFloat("_Cutoff", bark || rock ? 0 : 0.4f); result.SetFloat("_Smoothness", 0.08f);
            return Save(result, ArtPath + "/Materials/" + result.name + ".mat");
        }

        private static T Save<T>(T asset, string path) where T : Object
        {
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null) { AssetDatabase.CreateAsset(asset, path); return asset; }
            EditorUtility.CopySerialized(asset, existing); EditorUtility.SetDirty(existing); Object.DestroyImmediate(asset);
            return existing;
        }

        private sealed class Terrain
        {
            private readonly Vector3[] vertices;
            private readonly int[] triangles;
            public Terrain(Mesh mesh) { vertices = mesh.vertices; triangles = mesh.GetTriangles(7); }
            public float Height(float x, float z)
            {
                float height = -9;
                for (int tile = -1; tile <= 1; tile++)
                    for (int i = 0; i < triangles.Length; i += 3)
                    {
                        Vector3 a = vertices[triangles[i]], b = vertices[triangles[i + 1]], c = vertices[triangles[i + 2]];
                        float sampleZ = z + tile * 42;
                        float denominator = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
                        if (Mathf.Abs(denominator) < 0.000001f) continue;
                        float u = ((b.z - c.z) * (x - c.x) + (c.x - b.x) * (sampleZ - c.z)) / denominator;
                        float v = ((c.z - a.z) * (x - c.x) + (a.x - c.x) * (sampleZ - c.z)) / denominator;
                        if (u >= -0.00001f && v >= -0.00001f && u + v <= 1.00001f)
                            height = Mathf.Max(height, u * a.y + v * b.y + (1 - u - v) * c.y);
                    }
                return height;
            }
        }

        private sealed class PlantingMesh
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<Vector3> normals = new List<Vector3>();
            private readonly List<Vector2> uvs = new List<Vector2>();
            private readonly List<Vector2> winds = new List<Vector2>();
            private readonly List<Vector2> anchors = new List<Vector2>();
            private readonly List<Color32> colors = new List<Color32>();
            private readonly List<int>[] triangles;
            public PlantingMesh(int surfaces) { triangles = Enumerable.Range(0, surfaces).Select(_ => new List<int>()).ToArray(); }
            public void Add(Mesh mesh, int surface, int target, Matrix4x4 transform, Vector4? plant, float variation, Matrix4x4? toRoot = null)
            {
                Vector3[] source = mesh.vertices, sourceNormals = mesh.normals;
                Vector2[] sourceUV = mesh.uv;
                var remap = new Dictionary<int, int>();
                Matrix4x4 normalMatrix = transform.inverse.transpose;
                foreach (int index in mesh.GetTriangles(surface))
                {
                    if (!remap.TryGetValue(index, out int destination))
                    {
                        destination = vertices.Count; remap.Add(index, destination);
                        vertices.Add(transform.MultiplyPoint3x4(source[index]));
                        normals.Add(normalMatrix.MultiplyVector(sourceNormals[index]).normalized);
                        uvs.Add(sourceUV.Length == source.Length ? sourceUV[index] : Vector2.zero);
                        float rootY = toRoot.HasValue ? toRoot.Value.MultiplyPoint3x4(source[index]).y : source[index].y;
                        float height = plant.HasValue ? Mathf.Clamp01((rootY - plant.Value.z) / plant.Value.w) : 0;
                        winds.Add(new Vector2(height * height, plant.HasValue ? plant.Value.x * 0.31f + plant.Value.y * 0.73f : 0));
                        anchors.Add(plant.HasValue ? new Vector2(plant.Value.x, plant.Value.y) : Vector2.zero);
                        colors.Add((Color32)new Color(height * 0.7f, variation, 1, 1));
                    }
                    triangles[target].Add(destination);
                }
            }
            public Mesh Bake(string name)
            {
                var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
                mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uvs); mesh.SetUVs(1, winds);
                mesh.SetUVs(2, anchors); mesh.SetColors(colors); mesh.subMeshCount = triangles.Length;
                for (int surface = 0; surface < triangles.Length; surface++) mesh.SetTriangles(triangles[surface], surface, false);
                mesh.RecalculateBounds(); return mesh;
            }
        }
    }
}

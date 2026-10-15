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
    /// <summary>City-local reference road/path appearance and physically lit native sakura. No collision edits.</summary>
    public static class JapaneseCityReferenceFinish
    {
        public const string ArtPath = JapaneseCityBuilder.ArtPath + "/Reference Finish";
        public const string BlossomPath = ArtPath + "/City Sakura Blossoms.mat";
        public const string PedestrianName = "City Pedestrian Railings";

        [MenuItem("Tools/Cube Dash/Match City Reference Roads Paths and Sakura Shading")]
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
            bool[] done = track.Segments.Select(s => s.transform.Find(PedestrianName) != null).ToArray();
            if (done.All(v => v)) { Debug.Log("City reference finish already saved; manual edits preserved."); return; }
            if (done.Any(v => v) || AssetDatabase.IsValidFolder(ArtPath)) throw new InvalidOperationException("Inspect the partial reference finish before authoring.");
            foreach (var segment in track.Segments)
                if (segment.Environment.Biomes[0].transform.Find(JapaneseCitySidewalks.TreesName) == null)
                    throw new InvalidOperationException("Apply the City sidewalk/native-sakura upgrade first.");
            Shader petals = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/JapaneseCitySakura.shader");
            Shader surface = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/JapaneseCitySurface.shader");
            if (petals == null || surface == null || ShaderUtil.ShaderHasError(petals) || ShaderUtil.ShaderHasError(surface))
                throw new InvalidOperationException("City shaders must compile before authoring.");
            Texture texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Idyllic Fantasy Nature/Textures/Trees/Tree_Blossoms.png");
            if (texture == null) throw new InvalidOperationException("Native blossom texture is required.");
            AssetDatabase.CreateFolder(JapaneseCityBuilder.ArtPath, "Reference Finish");
            Material Surface(string name, string hex, float smoothness = 0.12f, float metallic = 0)
            {
                var material = new Material(surface) { name = name, enableInstancing = true };
                material.SetColor("_BaseColor", Hex(hex)); material.SetColor("_ShadeColor", Color.white);
                material.SetFloat("_Smoothness", smoothness); material.SetFloat("_Metallic", metallic);
                AssetDatabase.CreateAsset(material, ArtPath + "/" + name + ".mat"); return material;
            }
            var road = Surface("Reference Asphalt", "37323A", 0.14f);
            var shoulder = Surface("Reference Road Shoulder", "524C53");
            var paint = Surface("Reference Ivory Markings", "FFF2DE", 0.08f);
            var cream = Surface("Reference Barrier Ivory", "E4DECF");
            var mint = Surface("Reference Barrier Mint", "A1D6CE");
            var metal = Surface("Reference Rail Steel", "667384", 0.24f, 0.12f);
            var amber = Surface("Reference Amber Reflectors", "F6A82D", 0.2f);
            var iron = Surface("Reference Pedestrian Iron", "39454E", 0.18f, 0.1f);
            var blossom = new Material(petals) { name = "City Sakura Blossoms", enableInstancing = true, doubleSidedGI = true };
            blossom.SetTexture("_BaseMap", texture); blossom.SetColor("_BaseColor", Hex("F69AB1"));
            blossom.SetColor("_TopColor", Hex("FFD0DA")); blossom.SetFloat("_Cutoff", 0.35f);
            blossom.SetFloat("_Smoothness", 0.08f); blossom.SetFloat("_WindStrength", 0.12f);
            AssetDatabase.CreateAsset(blossom, BlossomPath);
            Mesh barriers = BuildBarriers(), pedestrians = BuildPedestrians();
            AssetDatabase.CreateAsset(barriers, ArtPath + "/Reference Barriers.asset");
            AssetDatabase.CreateAsset(pedestrians, ArtPath + "/Reference Pedestrian Rails.asset");
            var first = track.Segments[0].Environment.Biomes[0].GetComponent<BiomeScenery>();
            for (int i = 0; i < first.Layouts.Length; i++)
            {
                Mesh mesh = first.Layouts[i], replacement = JapaneseCitySidewalks.ReplacePavementAndOldTrees(mesh, i);
                replacement.name = mesh.name; EditorUtility.CopySerialized(replacement, mesh);
                EditorUtility.SetDirty(mesh); Object.DestroyImmediate(replacement);
            }
            string[] names = { "sandstone", "pale limestone", "honey stone", "recessed joints", "precast curb", "planting soil", "drainage stone" };
            string[] colors = { "D9CFBE", "E2D8C8", "D2C8B7", "9C978D", "D9D2C4", "514537", "3E3A42" };
            for (int i = 0; i < names.Length; i++)
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(JapaneseCityBuilder.ArtPath + "/Materials/Sidewalk " + names[i] + ".mat");
                material.SetColor("_BaseColor", Hex(colors[i])); EditorUtility.SetDirty(material);
            }
            var menu = AssetDatabase.LoadAssetAtPath<Material>(MainMenuSakuraBlossoms.MaterialPath);
            var original = AssetDatabase.LoadAssetAtPath<Material>("Assets/Idyllic Fantasy Nature/Materials/Trees/Tree_Blossoms.mat");
            int changed = 0;
            foreach (var segment in track.Segments)
            {
                foreach (string name in new[] { "Road", "Plane" })
                {
                    Transform deck = segment.transform.Find(name);
                    if (deck != null) deck.GetComponent<Renderer>().sharedMaterial = road;
                }
                segment.transform.Find("Highway Shoulders").GetComponent<Renderer>().sharedMaterial = shoulder;
                segment.transform.Find("Highway Lane Markings").GetComponent<Renderer>().sharedMaterial = paint;
                Transform rail = segment.transform.Find("Highway Guardrails");
                rail.GetComponent<MeshFilter>().sharedMesh = barriers;
                rail.GetComponent<Renderer>().sharedMaterials = new[] { cream, mint, metal, amber };
                var fence = new GameObject(PedestrianName, typeof(MeshFilter), typeof(MeshRenderer));
                fence.transform.SetParent(segment.transform, false); fence.GetComponent<MeshFilter>().sharedMesh = pedestrians;
                fence.GetComponent<MeshRenderer>().sharedMaterial = iron;
                Transform trees = segment.Environment.Biomes[0].transform.Find(JapaneseCitySidewalks.TreesName);
                foreach (var renderer in trees.GetComponentsInChildren<MeshRenderer>(true))
                {
                    Material[] materials = renderer.sharedMaterials; bool replace = false;
                    for (int i = 0; i < materials.Length; i++)
                        if (materials[i] == menu || materials[i] == original) { materials[i] = blossom; replace = true; }
                    if (!replace) continue;
                    renderer.sharedMaterials = materials; PrefabUtility.RecordPrefabInstancePropertyModifications(renderer); changed++;
                }
            }
            if (changed != 96) throw new InvalidOperationException("Expected exactly 96 native blossom LOD renderers, found " + changed);
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save City reference finish.");
            JapaneseCityStreetRepair.ApplyFromCommandLine();
            JapaneseCityLowPolySakura.ApplyFromCommandLine();
            JapaneseCityArtFinish.ApplyFromCommandLine();
            Debug.Log("City reference finish saved: warm charcoal road, ivory markings, mint/ivory barriers, pedestrian rails and wide paths. City-specific low-poly sakura and tree clearance applied. Gameplay, Main Menu and source pack preserved.");
        }

        private static Color Hex(string hex) { ColorUtility.TryParseHtmlString("#" + hex, out Color color); return color; }

        private sealed class Boxes
        {
            private readonly List<CombineInstance>[] groups;
            public Boxes(int surfaces) { groups = Enumerable.Range(0, surfaces).Select(_ => new List<CombineInstance>()).ToArray(); }
            public void Box(int surface, float x, float y, float z, float width, float height, float depth)
                => groups[surface].Add(new CombineInstance { mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx"), transform = Matrix4x4.TRS(new Vector3(x, y, z), Quaternion.identity, new Vector3(width, height, depth)) });
            public Mesh Finish(string name)
            {
                var parts = new Mesh[groups.Length];
                try
                {
                    for (int i = 0; i < parts.Length; i++) { parts[i] = new Mesh(); parts[i].CombineMeshes(groups[i].ToArray(), true, true); }
                    var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
                    mesh.CombineMeshes(parts.Select(m => new CombineInstance { mesh = m, transform = Matrix4x4.identity }).ToArray(), false, true);
                    mesh.RecalculateBounds(); return mesh;
                }
                finally { foreach (var part in parts) if (part != null) Object.DestroyImmediate(part); }
            }
        }

        private static Mesh BuildBarriers()
        {
            var g = new Boxes(4);
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * 4.98f;
                g.Box(0, x, 0.27f, 21, 0.16f, 0.08f, 42);
                g.Box(1, x, 0.47f, 21, 0.11f, 0.32f, 42);
                g.Box(0, x, 0.66f, 21, 0.18f, 0.07f, 42);
                g.Box(2, x, 0.75f, 21, 0.20f, 0.10f, 42);
                for (int post = 0; post < 12; post++)
                {
                    float z = 1.75f + post * 3.5f;
                    g.Box(0, x, 0.27f, z, 0.14f, 1.16f, 0.15f);
                    g.Box(2, x, 0.86f, z, 0.17f, 0.04f, 0.17f);
                    g.Box(3, x, 0.92f, z, 0.11f, 0.08f, 0.11f);
                }
            }
            return g.Finish("City Reference Barriers");
        }

        private static Mesh BuildPedestrians()
        {
            var g = new Boxes(1);
            for (int side = -1; side <= 1; side += 2)
                foreach (float z in new[] { 3.5f, 18.5f, 35.5f })
                {
                    float x = side * 6.98f;
                    g.Box(0, x, 1.05f, z, 0.045f, 0.055f, 4.5f);
                    g.Box(0, x, 0.63f, z, 0.04f, 0.045f, 4.5f);
                    for (int post = 0; post <= 3; post++) g.Box(0, x, 0.65f, z - 2.25f + post * 1.5f, 0.055f, 0.86f, 0.055f);
                }
            return g.Finish("City Reference Pedestrian Rails");
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace CubeDash.Editor
{
    /// <summary>Saved highway art pass; gameplay coordinates, colliders and pools stay unchanged.</summary>
    public static class CubeDashHighway
    {
        public const float LandscapeLift = 8.75f;
        private const string TrackPath = "Assets/Prefab/CubeDash/TrackSegment.prefab";
        private const string MeshFolder = "Assets/3D Models/Environments/";
        private const string MaterialFolder = "Assets/Material/Environments/";

        [MenuItem("Tools/Cube Dash/Apply Ground-Level Highway")]
        public static void ApplyFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) ApplyFromCommandLine();
        }

        public static void ApplyFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before authoring the highway.");
            // Do not reopen or save over an unsaved user scene.
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save scene changes before applying the highway.");

            Material road = Surface("Assets/Material/CubeDashRoad.mat", "CubeDashRoad",
                new Color(0.17f, 0.18f, 0.19f), new Color(0.09f, 0.10f, 0.11f), 7, 0.10f);
            Material shoulder = Surface(MaterialFolder + "Highway Shoulder.mat", "Highway Shoulder",
                new Color(0.30f, 0.31f, 0.30f), new Color(0.21f, 0.22f, 0.21f), 7, 0.08f);
            Material paint = Surface(MaterialFolder + "Highway Paint.mat", "Highway Paint",
                new Color(0.90f, 0.88f, 0.78f), new Color(0.80f, 0.79f, 0.72f), 0, 0.16f);
            Material steel = Surface(MaterialFolder + "Highway Guardrail.mat", "Highway Guardrail",
                new Color(0.52f, 0.57f, 0.59f), new Color(0.38f, 0.42f, 0.44f), 0, 0.35f);
            steel.SetFloat("_Metallic", 0.55f);
            Mesh markings = Markings(), shoulders = Shoulders(), rails = Guardrails();

            // Source landscapes stay editable and inherit the same lift when rebaked.
            for (int i = 1; i < BiomeRules.Count; i++)
            {
                string path = "Assets/Prefab/CubeDash/Environments/" + (EnvironmentBiome)i + ".prefab";
                GameObject scenery = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    SetLandscapeHeight(scenery.transform);
                    PrefabUtility.SaveAsPrefabAsset(scenery, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(scenery); }
            }

            GameObject root = PrefabUtility.LoadPrefabContents(TrackPath);
            try
            {
                foreach (GameObject biome in root.GetComponent<SegmentEnvironment>().Biomes)
                {
                    SetLandscapeHeight(biome.transform);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(biome.transform);
                }
                Transform deck = root.transform.Find("Road");
                deck.localPosition = new Vector3(0, -0.125f, 21);
                deck.localScale = new Vector3(8.4f, 0.25f, RunnerRules.SegmentLength);
                deck.GetComponent<Renderer>().sharedMaterial = road;

                // Remove only the known, generated bridge dressing. Preserve any custom children.
                Transform bridge = root.transform.Find("Bridge Rails");
                if (bridge != null)
                {
                    var generated = new List<GameObject>();
                    foreach (Transform child in bridge)
                        if (child.name == "Bridge Support" || child.name == "Rail Base") generated.Add(child.gameObject);
                    foreach (GameObject child in generated) Object.DestroyImmediate(child);
                    if (bridge.childCount == 0) Object.DestroyImmediate(bridge.gameObject);
                }
                Transform edges = root.transform.Find("Matte Road Edges");
                if (edges != null)
                {
                    var generated = new List<GameObject>();
                    foreach (Transform child in edges) if (child.name == "Road Edge") generated.Add(child.gameObject);
                    foreach (GameObject child in generated) Object.DestroyImmediate(child);
                    if (edges.childCount == 0) Object.DestroyImmediate(edges.gameObject);
                }
                Part(root.transform, "Highway Lane Markings", markings, paint, false);
                Part(root.transform, "Highway Shoulders", shoulders, shoulder, false);
                Part(root.transform, "Highway Guardrails", rails, steel, true);
                PrefabUtility.SaveAsPrefabAsset(root, TrackPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Level.unity");
            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>();
            if (game == null) throw new InvalidOperationException("The authored Level is required.");
            foreach (TrackSegment segment in game.Track.Segments)
            {
                // The original Plane is still the starting surface; its zero-height top is unchanged.
                Transform plane = segment.transform.Find("Plane");
                if (plane != null) plane.GetComponent<Renderer>().sharedMaterial = road;
                foreach (GameObject biome in segment.Environment.Biomes)
                {
                    SetLandscapeHeight(biome.transform);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(biome.transform);
                }
            }
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Ground-level highway saved across all five biomes: asphalt, three marked lanes, graded shoulders and steel guardrails. Terrain, models, water and horizon aprons lifted together; gameplay unchanged.");
        }

        internal static void SetLandscapeHeight(Transform root)
        {
            Vector3 position = root.localPosition;
            position.y = LandscapeLift; // Absolute assignment makes the pass safe to repeat.
            root.localPosition = position;
        }

        private static Material Surface(string path, string name, Color color, Color secondary, float type, float smoothness)
        {
            Shader shader = Shader.Find("CubeDash/Environment Surface");
            if (shader == null) throw new InvalidOperationException("Missing environment surface shader.");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader; material.name = name; material.enableInstancing = true;
            material.shaderKeywords = Array.Empty<string>();
            material.SetColor("_BaseColor", color); material.SetColor("_SecondaryColor", secondary);
            material.SetFloat("_DetailType", type); material.SetFloat("_DetailScale", 1);
            material.SetFloat("_Smoothness", smoothness); material.SetFloat("_Metallic", 0);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void Part(Transform parent, string name, Mesh mesh, Material material, bool castShadow)
        {
            Transform part = parent.Find(name);
            if (part == null)
            {
                part = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)).transform;
                part.SetParent(parent, false);
            }
            part.localPosition = Vector3.zero; part.localRotation = Quaternion.identity; part.localScale = Vector3.one;
            part.GetComponent<MeshFilter>().sharedMesh = mesh;
            Renderer renderer = part.GetComponent<Renderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = castShadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
        }

        private static Mesh Markings()
        {
            var boxes = new List<CombineInstance>();
            for (int side = -1; side <= 1; side += 2)
            {
                Box(boxes, new Vector3(side * 3.94f, 0.008f, 21), new Vector3(0.14f, 0.008f, 42));
                for (int dash = 0; dash < 7; dash++)
                    Box(boxes, new Vector3(side * 1.3f, 0.008f, dash * 6 + 3), new Vector3(0.13f, 0.008f, 3));
            }
            return Combine(boxes, "Highway Lane Markings");
        }

        private static Mesh Guardrails()
        {
            var boxes = new List<CombineInstance>();
            for (int side = -1; side <= 1; side += 2)
            {
                // Thin paired bands evoke a W-beam without a wall blocking the landscapes.
                for (int band = 0; band < 2; band++)
                    Box(boxes, new Vector3(side * 5.05f, 0.42f + band * 0.13f, 21), new Vector3(0.10f, 0.14f, 42));
                for (int post = 0; post < 7; post++)
                    Box(boxes, new Vector3(side * 5.11f, 0.13f, post * 6 + 3), new Vector3(0.12f, 0.76f, 0.12f));
            }
            return Combine(boxes, "Highway Guardrails");
        }

        private static Mesh Shoulders()
        {
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            for (int side = -1; side <= 1; side += 2)
            {
                // Asphalt verge falls gently into the actual terrain, 25 cm below the carriageway.
                int first = vertices.Count;
                vertices.AddRange(new[] { new Vector3(side * 4.2f, 0, 0), new Vector3(side * 4.2f, 0, 42),
                    new Vector3(side * 5.2f, -0.25f, 0), new Vector3(side * 5.2f, -0.25f, 42) });
                if (side > 0) triangles.AddRange(new[] { first, first + 1, first + 2, first + 2, first + 1, first + 3 });
                else triangles.AddRange(new[] { first, first + 2, first + 1, first + 2, first + 3, first + 1 });
            }
            Mesh mesh = new Mesh { name = "Highway Shoulders" };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return SaveMesh(mesh);
        }

        private static void Box(List<CombineInstance> boxes, Vector3 position, Vector3 scale)
            => boxes.Add(new CombineInstance { mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx"),
                transform = Matrix4x4.TRS(position, Quaternion.identity, scale) });

        private static Mesh Combine(List<CombineInstance> boxes, string name)
        {
            Mesh mesh = new Mesh { name = name };
            mesh.CombineMeshes(boxes.ToArray(), true, true); mesh.RecalculateBounds();
            return SaveMesh(mesh);
        }

        private static Mesh SaveMesh(Mesh mesh)
        {
            string path = MeshFolder + mesh.name + ".asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
            EditorUtility.CopySerialized(mesh, existing); Object.DestroyImmediate(mesh); EditorUtility.SetDirty(existing);
            return existing;
        }
    }
}

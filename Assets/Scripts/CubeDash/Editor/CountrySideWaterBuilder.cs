using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CubeDash.Editor
{
    /// <summary>Upgrades the saved shallow-water surfaces without changing the camera, rice or road.</summary>
    public static class CountrySideWaterBuilder
    {
        public const string ShaderName = "CubeDash/Countryside Water";
        public const string CanalMaterialPath = CountrySideSceneBuilder.ArtPath + "Materials/Canal Water.mat";
        public const string PaddyMaterialPath = CountrySideSceneBuilder.ArtPath + "Materials/Rice Paddy Water.mat";
        private const string Art = CountrySideSceneBuilder.ArtPath;

        [MenuItem("Tools/Cube Dash/Improve Country Side Water")]
        public static void ApplyFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) ApplyFromCommandLine();
        }
        public static void ApplyFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene edits first.");
            Scene scene = SceneManager.GetSceneByPath(CountrySideSceneBuilder.ScenePath);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(CountrySideSceneBuilder.ScenePath);
            ApplyToScene(scene); AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save Country Side.");
        }
        public static void ApplyToScene(Scene scene)
        {
            if (scene.path != CountrySideSceneBuilder.ScenePath) throw new InvalidOperationException("Only Country Side water can be upgraded.");
            var scenery = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CountrySideRunnerScenery>(true)).Single();
            Material canal = AssetDatabase.LoadAssetAtPath<Material>(CanalMaterialPath);
            Material paddy = AssetDatabase.LoadAssetAtPath<Material>(PaddyMaterialPath);
            var waterRenderers = scenery.Tiles.SelectMany(tile => tile.GetComponentsInChildren<MeshRenderer>(true))
                .Where(renderer => renderer.sharedMaterials.Any(material => material != null && material.shader.name == ShaderName)).ToArray();
            Mesh infrastructure = AssetDatabase.LoadAssetAtPath<Mesh>(Art + "Meshes/Rural Infrastructure.asset");
            Mesh rice = AssetDatabase.LoadAssetAtPath<Mesh>(Art + "Meshes/Rice Paddy.asset");
            // Once upgraded, do not rewrite meshes/prefabs or disturb the user's manual scene edits.
            if (canal != null && canal.shader.name == ShaderName && paddy != null && paddy.shader.name == ShaderName
                && infrastructure.GetIndexCount(3) == 6 && rice.GetIndexCount(1) == 6
                && waterRenderers.Length >= scenery.Tiles.Length * 5 && waterRenderers.All(renderer => scenery.CurvedSurfaces.Contains(renderer))) return;
            Shader shader = Shader.Find(ShaderName);
            if (shader == null) throw new InvalidOperationException("Import the countryside water shader first.");
            if (canal == null) throw new InvalidOperationException("The original canal water material is required.");
            // Material configuration is one-time; reapplication preserves authored water colors and speeds.
            if (canal.shader.name != ShaderName) Configure(canal, shader, false);
            if (paddy == null)
            {
                paddy = new Material(shader) { name = "Rice Paddy Water" };
                Configure(paddy, shader, true); AssetDatabase.CreateAsset(paddy, PaddyMaterialPath);
            }
            else if (paddy.shader.name != ShaderName) Configure(paddy, shader, true);
            PrepareWaterMesh(infrastructure, 3, true);
            PrepareWaterMesh(rice, 1, false);
            string path = Art + "Prefabs/Rice Paddy.prefab";
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var renderer = contents.GetComponentInChildren<MeshRenderer>();
                Material[] palette = renderer.sharedMaterials; palette[1] = paddy; renderer.sharedMaterials = palette;
                if (PrefabUtility.SaveAsPrefabAsset(contents, path) == null) throw new InvalidOperationException("Could not save rice paddy water.");
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            Transform environment = scene.GetRootGameObjects().Single(root => root.name == CountrySideSceneBuilder.RootName).transform;
            CountrySideDetailBuilder.RefreshCurveReferences(environment, scenery);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static void Configure(Material material, Shader shader, bool paddy)
        {
            material.shader = shader; material.shaderKeywords = new string[0]; material.enableInstancing = true;
            material.renderQueue = -1; material.SetOverrideTag("RenderType", "Opaque");
            material.SetColor("_BaseColor", paddy ? new Color(0.12f,0.25f,0.20f) : new Color(0.065f,0.25f,0.25f));
            material.SetColor("_ShallowColor", paddy ? new Color(0.36f,0.43f,0.26f) : new Color(0.28f,0.46f,0.36f));
            material.SetColor("_BedColor", paddy ? new Color(0.34f,0.30f,0.17f) : new Color(0.30f,0.33f,0.24f));
            material.SetColor("_ReflectionColor", new Color(0.59f,0.76f,0.80f));
            material.SetColor("_FoamColor", new Color(0.75f,0.84f,0.72f));
            material.SetVector("_WaterSize", paddy ? new Vector4(13.2f,25.2f,0,0) : new Vector4(2.35f,284,0,0));
            material.SetVector("_Flow", paddy ? new Vector4(0.012f,0.025f,0,0) : new Vector4(0,0.45f,0,0));
            material.SetFloat("_RippleScale", paddy ? 0.82f : 1.1f);
            material.SetFloat("_RippleStrength", paddy ? 0.055f : 0.12f);
            material.SetFloat("_ShallowWidth", paddy ? 0.95f : 0.48f);
            material.SetFloat("_BedVisibility", paddy ? 0.48f : 0.28f);
            material.SetFloat("_ReflectionStrength", paddy ? 0.78f : 0.65f);
            material.SetFloat("_Smoothness", paddy ? 0.90f : 0.82f);
            material.SetFloat("_FoamStrength", paddy ? 0.025f : 0.12f);
            EditorUtility.SetDirty(material);
        }

        private static void PrepareWaterMesh(Mesh mesh, int slot, bool canal)
        {
            Vector3[] vertices = mesh.vertices, normals = mesh.normals; Vector2[] uv = mesh.uv;
            int[] water = mesh.GetTriangles(slot);
            if (canal)
            {
                // Only the known bank end vertices are extended. Fences, paths and bridges remain unchanged.
                foreach (int i in mesh.GetTriangles(0).Distinct())
                    if ((Mathf.Abs(vertices[i].x - 6.31f) < 0.001f || Mathf.Abs(vertices[i].x - 6.61f) < 0.001f
                        || Mathf.Abs(vertices[i].x - 8.99f) < 0.001f || Mathf.Abs(vertices[i].x - 9.29f) < 0.001f))
                        vertices[i].z = ExtendCanalEnd(vertices[i].z);
                foreach (int i in water.Distinct()) vertices[i].z = ExtendCanalEnd(vertices[i].z);
            }
            var surface = new System.Collections.Generic.List<int>();
            for (int i = 0; i < water.Length; i += 3)
                if (normals[water[i]].y > 0.9f && normals[water[i + 1]].y > 0.9f && normals[water[i + 2]].y > 0.9f)
                    surface.AddRange(new[] { water[i], water[i + 1], water[i + 2] });
            if (surface.Count == 0) throw new InvalidOperationException("The authored water surface is missing: " + mesh.name);
            Vector3 first = vertices[surface[0]]; Bounds bounds = new Bounds(first, Vector3.zero);
            foreach (int i in surface) bounds.Encapsulate(vertices[i]);
            foreach (int i in surface.Distinct())
                uv[i] = new Vector2((vertices[i].x - bounds.min.x) / bounds.size.x, (vertices[i].z - bounds.min.z) / bounds.size.z);
            mesh.vertices = vertices; mesh.uv = uv; mesh.SetTriangles(surface, slot);
            mesh.RecalculateBounds(); mesh.UploadMeshData(false); EditorUtility.SetDirty(mesh);
        }
        private static float ExtendCanalEnd(float z) => Mathf.Abs(z + 23) < 0.001f ? -36 : Mathf.Abs(z - 239) < 0.001f ? 248 : z;
    }
}

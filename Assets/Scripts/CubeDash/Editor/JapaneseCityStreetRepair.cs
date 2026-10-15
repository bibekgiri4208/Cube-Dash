using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CubeDash.Editor
{
    /// <summary>Widens saved City paving and sets back architecture without rebuilding the street or gameplay.</summary>
    public static class JapaneseCityStreetRepair
    {
        public const float Extension = 6f;
        public const float PavingEdge = 10.92f + Extension;
        public const float BridgeWidthScale = 1.23f;

        [MenuItem("Tools/Cube Dash/Fix City Shadows Tree Clearance and Sidewalk Width")]
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
                throw new InvalidOperationException("Only standalone City may be repaired.");
            var roots = track.Segments.Select(s => s.Environment.Biomes[0].transform).ToArray();
            if (roots.Any(r => r.Find(JapaneseCitySidewalks.TreesName) == null))
                throw new InvalidOperationException("Apply the native City sakura/reference finish first.");
            Mesh[] layouts = roots[0].GetComponent<BiomeScenery>().Layouts;
            bool changed = false;
            for (int i = 0; i < layouts.Length; i++)
            {
                Mesh mesh = layouts[i];
                if (!AssetDatabase.GetAssetPath(mesh).StartsWith(JapaneseCityBuilder.ArtPath + "/Layouts/", StringComparison.Ordinal) ||
                    mesh.subMeshCount != JapaneseCitySidewalks.SurfaceCount)
                    throw new InvalidOperationException("Expected scene-local City layouts.");
                Vector3[] vertices = mesh.vertices;
                float edge = new[] { 22, 23, 24 }.SelectMany(mesh.GetTriangles).Max(v => Mathf.Abs(vertices[v].x));
                // Reapplication preserves later manual edits instead of moving the buildings a second time.
                if (edge > PavingEdge - 0.1f) continue;
                if (Mathf.Abs(edge - (10.92f - 0.012f)) > 0.02f)
                    throw new InvalidOperationException("Inspect manually modified pavement before widening: " + mesh.name);
                Mesh replacement = JapaneseCitySidewalks.ReplacePavementAndOldTrees(mesh, i, Extension);
                SetBackArchitecture(replacement);
                replacement.name = mesh.name; EditorUtility.CopySerialized(replacement, mesh);
                EditorUtility.SetDirty(mesh); Object.DestroyImmediate(replacement); changed = true;
            }
            var data = new SerializedObject(track.Environment);
            var sun = (Light)data.FindProperty("sunlight").objectReferenceValue;
            changed |= ConfigureShadows(sun);
            if (!changed) { Debug.Log("City street repair already saved; manual edits preserved."); return; }
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save City street repair.");
            Debug.Log("City repaired: 10.12 m paved sidewalks, recessed shops/signs/planters, clear native sakura crowns and bridge supports, high-quality soft sun shadows. Road, gameplay and other maps unchanged.");
        }

        private static void SetBackArchitecture(Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices; Vector2[] anchors = mesh.uv3;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector2 anchor = anchors[i]; float x = Mathf.Abs(anchor.x);
                bool ground = Mathf.Abs(x - 12) < 0.001f && Mathf.Abs(anchor.y - 21) < 0.001f;
                bool bridge = Mathf.Abs(x - 12) < 0.001f && Mathf.Abs(anchor.y - 29) < 0.001f;
                if (bridge)
                {
                    // Keep the bridge centred over the road, but move its supports beyond the crowns.
                    vertices[i].x *= BridgeWidthScale; anchors[i].x *= BridgeWidthScale;
                }
                else if (!ground && x >= 9.59f)
                {
                    float offset = Mathf.Sign(anchor.x) * Extension;
                    vertices[i].x += offset; anchors[i].x += offset;
                }
            }
            mesh.vertices = vertices; mesh.uv3 = anchors;
            mesh.RecalculateBounds(); Bounds bounds = mesh.bounds;
            bounds.Encapsulate(bounds.min + Vector3.down * 80); mesh.bounds = bounds;
        }

        internal static bool ConfigureShadows(Light sun)
        {
            var settings = sun.GetUniversalAdditionalLightData();
            if (!settings.usePipelineSettings && settings.softShadowQuality == SoftShadowQuality.High &&
                sun.shadows == LightShadows.Soft && Mathf.Approximately(sun.shadowBias, 0.35f) &&
                Mathf.Approximately(sun.shadowNormalBias, 0.3f)) return false;
            // These are per-light URP overrides, not ignored legacy Light values or global pipeline changes.
            settings.usePipelineSettings = false; settings.softShadowQuality = SoftShadowQuality.High;
            sun.shadows = LightShadows.Soft; sun.shadowBias = 0.35f; sun.shadowNormalBias = 0.3f;
            EditorUtility.SetDirty(sun); EditorUtility.SetDirty(settings); return true;
        }
    }
}

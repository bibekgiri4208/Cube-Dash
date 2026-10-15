using System;
using System.Collections.Generic;
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
    /// <summary>City-local art direction. Bakes short-range ambient occlusion, never lightmaps or runtime colliders.</summary>
    public static class JapaneseCityLighting
    {
        [MenuItem("Tools/Cube Dash/Refine Japanese City Lighting and Shading")]
        public static void ApplyFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) ApplyFromCommandLine();
        }

        public static void ApplyFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene edits first.");
            var scene = SceneManager.GetSceneByPath(JapaneseCityBuilder.ScenePath);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(JapaneseCityBuilder.ScenePath);
            ApplyToScene(scene);
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save City lighting.");
            Debug.Log("City lighting saved: warm key light, cool restrained fill, full sun shadows, baked crevice shading, reflective glazing and restrained grading/bloom. Other maps and shared assets untouched.");
        }

        internal static void ApplyToScene(Scene scene)
        {
            if (scene.path != JapaneseCityBuilder.ScenePath) throw new InvalidOperationException("Only standalone City may be refined.");
            var track = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<EndlessTrack>(true)).Single();
            if (!track.Environment.HasFixedBiome || track.Environment.FixedBiome != EnvironmentBiome.City)
                throw new InvalidOperationException("Expected the standalone City biome.");
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/JapaneseCitySurface.shader");
            if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("City surface shader must compile.");
            var renderers = track.Segments.Select(s => s.Environment.Biomes[0].transform.Find(JapaneseCityBuilder.StreetsName)?.GetComponent<MeshRenderer>()).ToArray();
            if (renderers.Any(r => r == null)) throw new InvalidOperationException("Upgrade all eight City streetscapes first.");
            foreach (Material material in renderers.SelectMany(r => r.sharedMaterials).Distinct())
            {
                string path = AssetDatabase.GetAssetPath(material);
                if (!path.StartsWith(JapaneseCityBuilder.ArtPath + "/Materials/", StringComparison.Ordinal))
                    throw new InvalidOperationException("Refusing to edit a shared/non-City material: " + path);
                if (material.shader.name == "CubeDash/Japanese City Lettering") continue;
                Color color = material.GetColor("_BaseColor"); material.shader = shader;
                material.SetColor("_BaseColor", color); material.SetColor("_ShadeColor", new Color(0.89f, 0.91f, 0.97f));
                material.SetFloat("_SurfaceType", 0); material.SetFloat("_Smoothness", 0.18f);
                material.SetFloat("_Metallic", 0); material.SetFloat("_EmissionStrength", 0);
                material.SetFloat("_OcclusionStrength", 0.88f);
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                if (name.Contains("plaster") || name == "Blue concrete") material.SetFloat("_SurfaceType", 2);
                if (name == "Timber" || name == "Bark") { material.SetFloat("_SurfaceType", 1); material.SetFloat("_Smoothness", 0.12f); }
                if (name.StartsWith("Window", StringComparison.Ordinal))
                {
                    material.SetFloat("_SurfaceType", 3); material.SetFloat("_Smoothness", 0.78f); material.SetFloat("_Metallic", 0.18f);
                    material.SetColor("_ShadeColor", new Color(0.82f, 0.9f, 1));
                }
                if (name.StartsWith("Sakura", StringComparison.Ordinal) || name == "Leaf green")
                {
                    material.SetFloat("_SurfaceType", 4); material.SetFloat("_Smoothness", 0.08f);
                    material.SetColor("_ShadeColor", new Color(0.79f, 0.84f, 0.97f));
                }
                if (name.Contains("paving") || name == "Paving joints") { material.SetFloat("_SurfaceType", 5); material.SetFloat("_Smoothness", 0.12f); }
                if (name.StartsWith("Sidewalk", StringComparison.Ordinal))
                {
                    material.SetFloat("_SurfaceType", 5); material.SetFloat("_Smoothness", 0.12f);
                    material.SetColor("_ShadeColor", new Color(0.94f, 0.94f, 0.96f));
                }
                if (name == "Iron") { material.SetFloat("_SurfaceType", 6); material.SetFloat("_Smoothness", 0.44f); material.SetFloat("_Metallic", 0.48f); }
                if (name == "Lantern gold") { material.SetFloat("_SurfaceType", 7); material.SetFloat("_EmissionStrength", 0.38f); }
                EditorUtility.SetDirty(material);
            }
            foreach (string guid in AssetDatabase.FindAssets("t:Mesh", new[] { JapaneseCityBuilder.ArtPath + "/Layouts" }))
            {
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(AssetDatabase.GUIDToAssetPath(guid));
                // Reapplication retains saved artwork and avoids needless rebaking/re-serializing.
                if (mesh.colors32.Length == mesh.vertexCount) continue;
                BakeOcclusion(mesh);
            }
            var data = new SerializedObject(track.Environment); var a = data.FindProperty("atmospheres").GetArrayElementAtIndex(0);
            void ColorField(string field, string hex) => a.FindPropertyRelative(field).colorValue = Hex(hex);
            ColorField("Horizon", "B8CDDB"); ColorField("Zenith", "327AB3"); ColorField("Ground", "85898D");
            ColorField("Sun", "FFE8C8"); ColorField("AmbientSky", "9EB2C4");
            ColorField("AmbientEquator", "7D8E9F"); ColorField("AmbientGround", "6D6568");
            a.FindPropertyRelative("SunIntensity").floatValue = 2.15f; data.ApplyModifiedPropertiesWithoutUndo();
            var sun = (Light)data.FindProperty("sunlight").objectReferenceValue;
            sun.transform.rotation = Quaternion.Euler(42, -48, 0); sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 1;
            JapaneseCityStreetRepair.ConfigureShadows(sun);
            var fill = (Light)data.FindProperty("skyFill").objectReferenceValue; if (fill != null) fill.intensity = 0.065f;
            RenderSettings.ambientMode = AmbientMode.Trilight; RenderSettings.ambientIntensity = 1;
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 100; RenderSettings.fogEndDistance = 350;
            var sky = AssetDatabase.LoadAssetAtPath<Material>(JapaneseCityBuilder.ArtPath + "/Fuji Sky.mat");
            sky.SetColor("_HorizonColor", a.FindPropertyRelative("Horizon").colorValue);
            sky.SetColor("_ZenithColor", a.FindPropertyRelative("Zenith").colorValue);
            sky.SetColor("_GroundColor", a.FindPropertyRelative("Ground").colorValue);
            sky.SetColor("_SunColor", a.FindPropertyRelative("Sun").colorValue);
            sky.SetVector("_SunDirection", -sun.transform.forward); EditorUtility.SetDirty(sky);
            RenderSettings.skybox = sky; track.Environment.ApplyDistance(0, 0);
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(JapaneseCityBuilder.ArtPath + "/Clear Day.asset");
            if (profile == null) throw new InvalidOperationException("Missing City-local volume.");
            T Effect<T>() where T : VolumeComponent
            {
                if (!profile.TryGet(out T effect)) { effect = profile.Add<T>(true); AssetDatabase.AddObjectToAsset(effect, profile); }
                return effect;
            }
            Effect<DepthOfField>().mode.Override(DepthOfFieldMode.Off);
            var vignette = Effect<Vignette>(); vignette.intensity.Override(0.11f); vignette.smoothness.Override(0.38f);
            var bloom = Effect<Bloom>(); bloom.intensity.Override(0.12f); bloom.threshold.Override(1.25f); bloom.scatter.Override(0.55f);
            var grade = Effect<ColorAdjustments>(); grade.postExposure.Override(0.35f); grade.contrast.Override(12); grade.saturation.Override(-3);
            var white = Effect<WhiteBalance>(); white.temperature.Override(-2); white.tint.Override(0);
            var tone = Effect<Tonemapping>(); tone.mode.Override(TonemappingMode.ACES);
            var split = Effect<SplitToning>(); split.shadows.Override(new Color(0.465f, 0.49f, 0.525f));
            split.highlights.Override(new Color(0.525f, 0.51f, 0.485f)); split.balance.Override(10);
            EditorUtility.SetDirty(profile);
            foreach (var effect in profile.components) EditorUtility.SetDirty(effect);
            JapaneseCityArtFinish.ApplyToScene(scene);
        }

        private static Color Hex(string value) { ColorUtility.TryParseHtmlString("#" + value, out var color); return color; }

        private static void BakeOcclusion(Mesh mesh)
        {
            var usedLayers = new HashSet<int>(Object.FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None).Select(c => c.gameObject.layer));
            int layer = Enumerable.Range(8, 24).Reverse().First(i => !usedLayers.Contains(i));
            var root = new GameObject("Temporary City AO bake") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                for (int tile = -1; tile <= 1; tile++)
                {
                    var go = new GameObject("AO neighbor", typeof(MeshCollider)) { layer = layer, hideFlags = HideFlags.HideAndDontSave };
                    go.transform.SetParent(root.transform, false); go.transform.localPosition = Vector3.forward * (tile * 42);
                    go.GetComponent<MeshCollider>().sharedMesh = mesh;
                }
                Physics.SyncTransforms();
                Vector3[] vertices = mesh.vertices, normals = mesh.normals; var colors = new Color32[vertices.Length];
                var cache = new Dictionary<(Vector3, Vector3), byte>(); const int samples = 12; const float radius = 1.65f;
                for (int i = 0; i < vertices.Length; i++)
                {
                    var key = (vertices[i], normals[i]);
                    if (!cache.TryGetValue(key, out byte shade))
                    {
                        Vector3 normal = normals[i].normalized;
                        Vector3 tangent = Vector3.Cross(normal, Mathf.Abs(normal.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                        Vector3 bitangent = Vector3.Cross(normal, tangent);
                        Vector3 start = vertices[i] + normal * 0.035f; float blocked = 0;
                        for (int sample = 0; sample < samples; sample++)
                        {
                            float u = (sample + 0.5f) / samples, angle = sample * 2.39996323f;
                            Vector3 direction = normal * Mathf.Sqrt(u) + (tangent * Mathf.Cos(angle) + bitangent * Mathf.Sin(angle)) * Mathf.Sqrt(1 - u);
                            if (Physics.Raycast(start, direction, out var hit, radius, 1 << layer, QueryTriggerInteraction.Ignore))
                                blocked += 1 - 0.65f * Mathf.Clamp01(hit.distance / radius);
                        }
                        shade = (byte)Mathf.RoundToInt(255 * Mathf.Max(0.38f, 1 - 0.86f * blocked / samples)); cache.Add(key, shade);
                    }
                    colors[i] = new Color32(shade, shade, shade, 255);
                }
                mesh.colors32 = colors; EditorUtility.SetDirty(mesh);
                Debug.Log("Baked City crevice shading: " + mesh.name + ", " + cache.Count + " surface samples.");
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}

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
    /// <summary>City-local JDM traffic and matching baked smash parts; shared highway art is never rewritten.</summary>
    public static class JapaneseCityTraffic
    {
        public const string ArtPath = JapaneseCityBuilder.ArtPath + "/JDM Traffic";
        public const string VisualsPath = ArtPath + "/City JDM Traffic.asset";
        public const string BreakupsPath = ArtPath + "/City JDM Breakups.asset";

        [MenuItem("Tools/Cube Dash/Replace City Vans with JDM Coupes and Kei Trucks")]
        public static void ApplyFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) ApplyFromCommandLine();
        }

        public static void ApplyFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode before authoring traffic.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene edits first.");
            Scene scene = SceneManager.GetSceneByPath(JapaneseCityBuilder.ScenePath);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(JapaneseCityBuilder.ScenePath);
            var track = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<EndlessTrack>(true)).Single();
            if (!track.Environment.HasFixedBiome || track.Environment.FixedBiome != EnvironmentBiome.City)
                throw new InvalidOperationException("Only standalone City may receive these traffic overrides.");
            var data = new SerializedObject(track); var debris = new SerializedObject(track.Debris);
            var trafficField = data.FindProperty("hazardVisuals"); var fragmentsField = debris.FindProperty("catalogOverride");
            if (AssetDatabase.GetAssetPath(trafficField.objectReferenceValue) == VisualsPath &&
                AssetDatabase.GetAssetPath(fragmentsField.objectReferenceValue) == BreakupsPath)
            { Debug.Log("City JDM traffic already saved; manual art edits preserved."); return; }
            if (trafficField.objectReferenceValue != null || fragmentsField.objectReferenceValue != null || AssetDatabase.IsValidFolder(ArtPath))
                throw new InvalidOperationException("Inspect partial/custom City traffic overrides before replacing them.");
            var original = Resources.Load<HighwayHazardVisuals>(HighwayHazardVisuals.ResourcePath);
            var breakup = Resources.Load<ObstacleBreakupCatalog>(ObstacleBreakupCatalog.ResourcePath);
            if (original == null || breakup == null || original.Materials.Length != 9)
                throw new InvalidOperationException("The shared traffic art and breakup library are required.");
            AssetDatabase.CreateFolder(JapaneseCityBuilder.ArtPath, "JDM Traffic");
            AssetDatabase.CreateFolder(ArtPath, "Breakup Parts");
            Mesh Save(Mesh mesh)
            { AssetDatabase.CreateAsset(mesh, ArtPath + "/" + mesh.name + ".asset"); return mesh; }
            Mesh[] kei = Enumerable.Range(0, 3).Select(v => Save(HighwayHazardArtBuilder.BuildCityKeiTruck(v))).ToArray();
            Mesh[] coupes = { Save(HighwayHazardArtBuilder.BuildCityJdmCoupe(false)), Save(HighwayHazardArtBuilder.BuildCityJdmCoupe(true)) };
            var art = Object.Instantiate(original); art.name = "City JDM Traffic";
            var settings = new SerializedObject(art);
            void Models(string first, string variants, Mesh[] meshes)
            {
                settings.FindProperty(first).objectReferenceValue = meshes[0];
                var values = settings.FindProperty(variants); values.arraySize = meshes.Length - 1;
                for (int i = 1; i < meshes.Length; i++) values.GetArrayElementAtIndex(i - 1).objectReferenceValue = meshes[i];
            }
            Models("van", "vanVariants", kei); Models("truck", "truckVariants", coupes);
            settings.FindProperty("overrideVehiclePaint").boolValue = true;
            settings.FindProperty("vanPaint").colorValue = new Color(0.90f, 0.86f, 0.74f);
            settings.FindProperty("truckPaint").colorValue = Color.white;
            settings.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.CreateAsset(art, VisualsPath);
            var catalog = Object.Instantiate(breakup); catalog.name = "City JDM Breakups";
            var partsData = new SerializedObject(catalog); var list = partsData.FindProperty("models");
            int oldCount = list.arraySize; Mesh[] vehicles = kei.Concat(coupes).ToArray(); list.arraySize += vehicles.Length;
            for (int model = 0; model < vehicles.Length; model++)
            {
                Mesh mesh = vehicles[model]; var value = list.GetArrayElementAtIndex(oldCount + model);
                value.FindPropertyRelative("source").objectReferenceValue = mesh;
                value.FindPropertyRelative("aliases").arraySize = 0;
                var fragments = BuildFragments(mesh, model < kei.Length);
                var items = value.FindPropertyRelative("fragments"); items.arraySize = fragments.Length;
                for (int i = 0; i < items.arraySize; i++)
                {
                    var item = items.GetArrayElementAtIndex(i);
                    item.FindPropertyRelative("mesh").objectReferenceValue = fragments[i].mesh;
                    item.FindPropertyRelative("center").vector3Value = fragments[i].center;
                    item.FindPropertyRelative("role").enumValueIndex = (int)fragments[i].role;
                }
            }
            partsData.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.CreateAsset(catalog, BreakupsPath);
            trafficField.objectReferenceValue = art; fragmentsField.objectReferenceValue = catalog;
            data.ApplyModifiedPropertiesWithoutUndo(); debris.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save City traffic overrides.");
            Debug.Log("City traffic saved: three ivory cab-over kei flatbeds, two white/black AE86-inspired liftback coupes, and matching prebaked wheel/glass/body smash parts. Vehicle paint is independent of gameplay tint; hazard footprints, seeded variants, pools, progression, other maps and shared assets retained.");
        }

        private static ObstacleBreakupCatalog.Fragment[] BuildFragments(Mesh source, bool kei)
        {
            // Split the actual authored triangles offline: no generic van chunks or runtime mesh generation.
            Vector3[] vertices = source.vertices, normals = source.normals; Vector2[] uv = source.uv; Color32[] colors = source.colors32;
            var bins = Enumerable.Range(0, 14).Select(_ => Enumerable.Range(0, 9).Select(__ => new List<int>()).ToArray()).ToArray();
            for (int surface = 0; surface < source.subMeshCount; surface++)
            {
                int[] triangles = source.GetTriangles(surface);
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    Vector3 c = (vertices[triangles[i]] + vertices[triangles[i + 1]] + vertices[triangles[i + 2]]) / 3;
                    float axle = c.z < 0 ? (kei ? -1.04f : -1.42f) : (kei ? 1.02f : 1.32f);
                    bool wheel = (surface == 2 || surface == 3 || surface == 5) && Mathf.Abs(c.x) > 0.54f &&
                        c.y < 0.61f && new Vector2(c.y - 0.30f, c.z - axle).magnitude < 0.315f;
                    int bin = wheel ? (c.x > 0 ? 2 : 0) + (c.z > 0 ? 1 : 0) : surface == 4 ?
                        4 + (c.x > 0 ? 2 : 0) + (c.z > (kei ? -0.8f : 0) ? 1 : 0) :
                        8 + (c.x > 0 ? 3 : 0) + (c.z < (kei ? -0.4f : -0.6f) ? 0 : c.z < 0.8f ? 1 : 2);
                    bins[bin][surface].AddRange(new[] { triangles[i], triangles[i + 1], triangles[i + 2] });
                }
            }
            var result = new List<ObstacleBreakupCatalog.Fragment>();
            for (int bin = 0; bin < bins.Length; bin++)
            {
                int[] all = bins[bin].SelectMany(v => v).ToArray(); if (all.Length == 0) continue;
                Vector3 min = vertices[all[0]], max = min;
                foreach (int i in all) { min = Vector3.Min(min, vertices[i]); max = Vector3.Max(max, vertices[i]); }
                Vector3 center = (min + max) * 0.5f;
                var points = new List<Vector3>(); var ns = new List<Vector3>(); var uvs = new List<Vector2>(); var cs = new List<Color32>();
                var mesh = new Mesh { name = source.name + " Part " + bin.ToString("00"), indexFormat = IndexFormat.UInt32 };
                var indices = Enumerable.Range(0, 9).Select(_ => new List<int>()).ToArray();
                for (int surface = 0; surface < 9; surface++)
                    for (int triangle = 0; triangle < bins[bin][surface].Count; triangle += 3)
                    {
                        void Face(bool inside)
                        {
                            int first = points.Count;
                            for (int corner = 0; corner < 3; corner++)
                            {
                                int index = bins[bin][surface][triangle + (inside ? 2 - corner : corner)];
                                points.Add(vertices[index] - center - (inside ? normals[index] * 0.004f : Vector3.zero));
                                ns.Add(normals[index] * (inside ? -1 : 1)); uvs.Add(uv[index]); cs.Add(colors[index]);
                            }
                            indices[surface].AddRange(new[] { first, first + 1, first + 2 });
                        }
                        Face(false); if (bin >= 4) Face(true);
                    }
                mesh.SetVertices(points); mesh.SetNormals(ns); mesh.SetUVs(0, uvs); mesh.SetColors(cs); mesh.subMeshCount = 9;
                for (int surface = 0; surface < 9; surface++) mesh.SetTriangles(indices[surface], surface, false);
                mesh.RecalculateTangents(); mesh.RecalculateBounds();
                AssetDatabase.CreateAsset(mesh, ArtPath + "/Breakup Parts/" + mesh.name + ".asset");
                result.Add(new ObstacleBreakupCatalog.Fragment { mesh = mesh, center = center,
                    role = bin < 4 ? ObstacleFragmentRole.Wheel : bin < 8 ? ObstacleFragmentRole.Glass : ObstacleFragmentRole.BodyPanel });
            }
            return result.ToArray();
        }
    }
}

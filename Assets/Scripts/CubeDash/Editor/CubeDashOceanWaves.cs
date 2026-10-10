using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CubeDash.Editor
{
    /// <summary>Rebakes only ocean/floor assets and wet-sand settings; existing scenery and the level are preserved.</summary>
    public static class CubeDashOceanWaves
    {
        private const string Meshes = "Assets/3D Models/Environments/";
        // Land is authored at -9. Put the sea noticeably below the road, not just under its edge.
        public const float OceanLocalHeight = -12.4f;
        public const float ShorelineX = 9f;

        [MenuItem("Tools/Cube Dash/Lower Beach Ocean Below Ground")]
        public static void LowerOceanFromCommandLine()
        {
            RebuildBeachFloor();
            foreach (string name in new[] { "Lagoon Water", "Coastal Sand", "Wet Sand" })
            {
                Material material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/Environments/" + name + ".mat");
                if (material == null) throw new InvalidOperationException("Missing beach material: " + name);
                material.SetFloat("_ShoreX", ShorelineX);
                EditorUtility.SetDirty(material);
            }
            const string path = "Assets/Prefab/CubeDash/Environments/Beach.prefab";
            GameObject beach = PrefabUtility.LoadPrefabContents(path);
            try
            {
                SetOceanHeight(beach.transform);
                PrefabUtility.SaveAsPrefabAsset(beach, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(beach); }
            AssetDatabase.SaveAssets();
            Debug.Log("Beach ocean lowered 3.4m below dry land with matching horizon water, decks and rocks; seabed deepened for wave troughs. Highway and gameplay preserved.");
        }

        private static void RebuildBeachFloor()
        {
            Mesh floor = SaveMesh(EnvironmentModelMeshes.BeachSeabed(), "Beach Seabed");
            SaveMesh(EnvironmentModelMeshes.BeachSeabed(true), "Shoreline Sand");
            CubeDashEnvironments.BoundaryFloor(floor, "Beach Transition Floor", true);
        }

        internal static void SetOceanHeight(Transform beach)
        {
            if (beach.Find("Ocean") == null) return;
            foreach (string name in new[] { "Ocean", "Rear Horizon/Ocean Apron", "Forward Horizon/Ocean Apron" })
            {
                Transform surface = beach.Find(name);
                if (surface == null) continue;
                Vector3 position = surface.localPosition;
                position.y = OceanLocalHeight;
                surface.localPosition = position;
            }
            // Move only the authored seaside props, not land-bound huts or palms.
            BiomeScenery scenery = beach.GetComponent<BiomeScenery>();
            if (scenery == null) return;
            foreach (Mesh layout in scenery.Layouts)
            {
                if (layout == null || layout.subMeshCount < 13) continue;
                Vector3[] vertices = layout.vertices;
                var jetty = new HashSet<int>();
                float top = float.MinValue;
                foreach (int index in layout.GetTriangles(9)) // Timber: jetty is on the positive-X ocean side.
                    if (vertices[index].x > 10.5f) { jetty.Add(index); top = Mathf.Max(top, vertices[index].y); }
                foreach (int index in layout.GetTriangles(12)) // Its ivory handrail.
                    if (vertices[index].x > 10.5f) jetty.Add(index);
                if (top == float.MinValue) continue;
                float delta = OceanLocalHeight + 3.82f - top; // Preserve the posts' original sea-relative height.
                bool changed = Mathf.Abs(delta) >= 0.0001f;
                if (changed) foreach (int index in jetty) vertices[index].y += delta;
                var seaRocks = new HashSet<int>();
                float rockBottom = float.MaxValue;
                foreach (int index in layout.GetTriangles(3))
                    if (vertices[index].x > 8) { seaRocks.Add(index); rockBottom = Mathf.Min(rockBottom, vertices[index].y); }
                if (rockBottom != float.MaxValue)
                {
                    float rockDelta = OceanLocalHeight - 1.2f - rockBottom;
                    if (Mathf.Abs(rockDelta) >= 0.0001f)
                    {
                        foreach (int index in seaRocks) vertices[index].y += rockDelta;
                        changed = true;
                    }
                }
                if (!changed) continue;
                layout.vertices = vertices;
                layout.RecalculateBounds();
                Bounds bounds = layout.bounds;
                bounds.Encapsulate(new Vector3(bounds.center.x, bounds.min.y - 80, bounds.center.z));
                layout.bounds = bounds; // Preserve the existing below-ground scenery reveal culling margin.
                EditorUtility.SetDirty(layout);
            }
        }

        [MenuItem("Tools/Cube Dash/Improve Beach Ocean Waves")]
        public static void ApplyFromCommandLine()
        {
            string path = "Assets/Prefab/CubeDash/Environments/Beach.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                throw new InvalidOperationException("The authored Beach environment is required.");
            Mesh ocean = SaveMesh(EnvironmentModelMeshes.OceanSurface(), "Ocean Surface");
            Mesh apron = SaveMesh(EnvironmentModelMeshes.OceanSurface(true), "Ocean Apron Surface");
            Mesh seabed = SaveMesh(EnvironmentModelMeshes.BeachSeabed(), "Beach Seabed");
            Mesh shore = SaveMesh(EnvironmentModelMeshes.BeachSeabed(true), "Shoreline Sand");
            CubeDashEnvironments.BoundaryFloor(seabed, "Beach Transition Floor", true);
            Material water = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/Environments/Lagoon Water.mat");
            ConfigureWater(water);
            Material wet = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/Environments/Wet Sand.mat");
            ConfigureSand(wet);
            // Creating a new material/prefab can trigger reimports; persist existing settings first.
            AssetDatabase.SaveAssets();
            Material sand = CoastalSand();
            GameObject beach = PrefabUtility.LoadPrefabContents(path);
            try
            {
                SetOceanHeight(beach.transform);
                beach.transform.Find("Ocean").GetComponent<MeshFilter>().sharedMesh = ocean;
                beach.transform.Find("Landscape Ground").GetComponent<MeshFilter>().sharedMesh = seabed;
                beach.transform.Find("Landscape Ground").GetComponent<Renderer>().sharedMaterial = sand;
                beach.transform.Find("Shoreline").GetComponent<MeshFilter>().sharedMesh = shore;
                foreach (string edge in new[] { "Rear Horizon", "Forward Horizon" })
                {
                    Transform horizon = beach.transform.Find(edge);
                    horizon.Find("Ocean Apron").GetComponent<MeshFilter>().sharedMesh = apron;
                    horizon.Find("Ground Apron").GetComponent<MeshFilter>().sharedMesh = seabed;
                    horizon.Find("Ground Apron").GetComponent<Renderer>().sharedMaterial = sand;
                }
                PrefabUtility.SaveAsPrefabAsset(beach, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(beach); }
            AssetDatabase.SaveAssets();
            Debug.Log("Beach ocean saved: four-direction Gerstner swells, dense coastal/horizon geometry, rolling foam, tidal swash and wet sand above a sloping seabed. River, scenery layouts, level and gameplay preserved.");
        }

        internal static void ConfigureWater(Material water)
        {
            water.SetFloat("_WaveHeight", 0.65f); water.SetFloat("_WaveSpeed", 1);
            water.SetFloat("_Choppiness", 0.55f); water.SetFloat("_FoamStrength", 0.9f);
            water.SetFloat("_TideHeight", 0.12f); water.SetFloat("_TideDistance", 0.85f); water.SetFloat("_TidePeriod", 40);
            water.SetFloat("_ShoreMode", 1); water.SetFloat("_ShoreX", ShorelineX); water.SetFloat("_ShallowWidth", 22);
            EditorUtility.SetDirty(water);
        }

        internal static void ConfigureSand(Material sand)
        {
            sand.SetFloat("_CoastalWetness", 1); sand.SetFloat("_ShoreX", ShorelineX);
            sand.SetFloat("_TideDistance", 0.85f); sand.SetFloat("_TidePeriod", 40);
            EditorUtility.SetDirty(sand);
        }

        internal static Material CoastalSand()
        {
            string path = "Assets/Material/Environments/Coastal Sand.mat";
            Material source = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/Environments/Golden Sand.mat");
            Material sand = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (sand == null) { sand = new Material(source) { name = "Coastal Sand" }; AssetDatabase.CreateAsset(sand, path); }
            ConfigureSand(sand); return sand;
        }

        private static Mesh SaveMesh(Mesh generated, string name)
        {
            string path = Meshes + name + ".asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) { generated.name = name; AssetDatabase.CreateAsset(generated, path); return generated; }
            EditorUtility.CopySerialized(generated, existing); existing.name = name;
            Object.DestroyImmediate(generated); EditorUtility.SetDirty(existing); return existing;
        }
    }
}

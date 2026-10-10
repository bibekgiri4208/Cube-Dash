using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CubeDash.Editor
{
    /// <summary>Rebakes only ocean/floor assets and wet-sand settings; existing scenery and the level are preserved.</summary>
    public static class CubeDashOceanWaves
    {
        private const string Meshes = "Assets/3D Models/Environments/";

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
            water.SetFloat("_ShoreMode", 1); water.SetFloat("_ShoreX", 7); water.SetFloat("_ShallowWidth", 22);
            EditorUtility.SetDirty(water);
        }

        internal static void ConfigureSand(Material sand)
        {
            sand.SetFloat("_CoastalWetness", 1); sand.SetFloat("_ShoreX", 7);
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
